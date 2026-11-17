using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Integrates digests in process, keyed by the caller's (issuer, organization) and a catalog it reads.</summary>
/// <remarks>A write names the revision it read; a changed schedule (days, times or zone) restarts it from the change, so
/// an occurrence before the change never runs. A delete takes the run history with it.</remarks>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="runner">Starts and sends runs.</param>
/// <param name="deliveries">The registered channels.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleDigestsBroker(
    EventsDbContext db,
    IEventsCallerService callers,
    DigestRunner runner,
    IEnumerable<IDigestDelivery> deliveries,
    TimeProvider time) : IEventDigestsBroker
{
    private const string RunsScope = "digest-runs:";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DigestModel>>> ListAsync(string catalog, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        var found = await FindCatalogAsync(caller, catalog, cancellationToken);
        if (found is Result<CatalogEntity>.Failure { Error: var error })
            return Result<IReadOnlyList<DigestModel>>.Fail(error);

        var row = ((Result<CatalogEntity>.Success)found).Value;

        var digests = await db.Digests.AsNoTracking()
            .Where(digest => digest.CatalogId == row.Id && digest.Issuer == caller.Issuer && digest.OrganizationId == caller.OrganizationId)
            .OrderBy(digest => digest.Name)
            .ThenBy(digest => digest.Id)
            .ToListAsync(cancellationToken);
        var ids = digests.Select(digest => digest.Id).ToList();
        var lastRuns = await db.DigestRuns.AsNoTracking()
            .Where(run => ids.Contains(run.DigestId))
            .GroupBy(run => run.DigestId)
            .Select(group => new { DigestId = group.Key, StartedAt = group.Max(run => run.StartedAt) })
            .ToDictionaryAsync(group => group.DigestId, group => group.StartedAt, cancellationToken);

        return Result<IReadOnlyList<DigestModel>>.Ok(digests
            .Select(digest => DigestRows.ToModel(digest, row.Slug, lastRuns.TryGetValue(digest.Id, out var last) ? last : null))
            .ToList());
    }

    /// <inheritdoc />
    public async Task<Result<DigestModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(catalog, id, tracked: false, cancellationToken);
        if (found is not Result<(CatalogEntity Catalog, DigestEntity Digest)>.Success { Value: var pair })
            return Result<DigestModel>.Fail(((Result<(CatalogEntity, DigestEntity)>.Failure)found).Error);

        return Result<DigestModel>.Ok(await ModelAsync(pair.Digest, pair.Catalog.Slug, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<DigestModel>> CreateAsync(string catalog, DigestSpec spec, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        var found = await FindCatalogAsync(caller, catalog, cancellationToken);
        if (found is Result<CatalogEntity>.Failure { Error: var error })
            return Result<DigestModel>.Fail(error);

        var row = ((Result<CatalogEntity>.Success)found).Value;
        if (DigestSpecValidator.Validate(spec, Channels(), out var normalized) is { } invalid)
            return Result<DigestModel>.Fail(invalid);

        var now = time.StoredUtcNow();
        var digest = new DigestEntity
        {
            Id = Guid.CreateVersion7(now),
            CatalogId = row.Id,
            Issuer = caller.Issuer,
            OrganizationId = caller.OrganizationId!,
            ScheduleSince = now,
            Revision = 1,
            CreatedAt = now,
        };
        DigestRows.Apply(digest, normalized, now);
        digest.NextRunAt = DigestRows.NextRunAt(digest, now);
        db.Digests.Add(digest);
        await db.SaveChangesAsync(cancellationToken);

        return Result<DigestModel>.Ok(DigestRows.ToModel(digest, row.Slug, null));
    }

    /// <inheritdoc />
    public async Task<Result<DigestModel>> UpdateAsync(string catalog, Guid id, DigestSpec spec, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(catalog, id, tracked: true, cancellationToken);
        if (found is not Result<(CatalogEntity Catalog, DigestEntity Digest)>.Success { Value: var pair })
            return Result<DigestModel>.Fail(((Result<(CatalogEntity, DigestEntity)>.Failure)found).Error);
        if (DigestSpecValidator.Validate(spec, Channels(), out var normalized) is { } invalid)
            return Result<DigestModel>.Fail(invalid);
        if (spec.ExpectedRevision is not { } expected)
            return Result<DigestModel>.Fail(EventsErrors.Invalid("expectedRevision", "required to replace a digest"));

        var digest = pair.Digest;
        if (expected != digest.Revision)
            return Result<DigestModel>.Fail(EventsErrors.RevisionConflict());

        var now = time.StoredUtcNow();
        var before = DigestRows.Schedule(digest);
        if (!SameSchedule(before, normalized.Schedule))
            digest.ScheduleSince = now;

        DigestRows.Apply(digest, normalized, now);
        digest.NextRunAt = DigestRows.NextRunAt(digest, now);
        digest.Revision++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<DigestModel>.Fail(EventsErrors.RevisionConflict());
        }

        return Result<DigestModel>.Ok(await ModelAsync(digest, pair.Catalog.Slug, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<DigestModel>> DeleteAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(catalog, id, tracked: true, cancellationToken);
        if (found is not Result<(CatalogEntity Catalog, DigestEntity Digest)>.Success { Value: var pair })
            return Result<DigestModel>.Fail(((Result<(CatalogEntity, DigestEntity)>.Failure)found).Error);
        if (expectedRevision != pair.Digest.Revision)
            return Result<DigestModel>.Fail(EventsErrors.RevisionConflict());

        var model = await ModelAsync(pair.Digest, pair.Catalog.Slug, cancellationToken);
        db.Digests.Remove(pair.Digest);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<DigestModel>.Fail(EventsErrors.RevisionConflict());
        }

        return Result<DigestModel>.Ok(model);
    }

    /// <inheritdoc />
    public async Task<Result<DigestPreviewModel>> PreviewAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(catalog, id, tracked: false, cancellationToken);
        if (found is not Result<(CatalogEntity Catalog, DigestEntity Digest)>.Success { Value: var pair })
            return Result<DigestPreviewModel>.Fail(((Result<(CatalogEntity, DigestEntity)>.Failure)found).Error);

        var draft = await runner.PreviewAsync(pair.Digest, pair.Catalog, cancellationToken);
        return Result<DigestPreviewModel>.Ok(new DigestPreviewModel
        {
            DigestId = pair.Digest.Id,
            AsOfUtc = draft.AsOf,
            Matched = draft.Matched,
            Items = draft.Items,
            Texts = draft.Texts,
            Recipients = draft.Recipients
                .Select(recipient => new DigestRunRecipientModel
                {
                    Subject = recipient.Subject,
                    Language = recipient.Language,
                    Status = DigestRecipientStatusConstants.Pending,
                    Attempts = 0,
                    UpdatedAtUtc = draft.AsOf,
                })
                .ToList(),
            Notes = draft.Notes,
        });
    }

    /// <inheritdoc />
    public async Task<Result<DigestRunModel>> SendNowAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(catalog, id, tracked: false, cancellationToken);
        if (found is not Result<(CatalogEntity Catalog, DigestEntity Digest)>.Success { Value: var pair })
            return Result<DigestRunModel>.Fail(((Result<(CatalogEntity, DigestEntity)>.Failure)found).Error);

        if (await runner.StartNowAsync(pair.Digest, pair.Catalog, cancellationToken) is not { } runId)
            return Result<DigestRunModel>.Fail(AppErrorFactory.Conflict("A run of this digest started at the same instant; retry."));

        await runner.SendAsync(runId, cancellationToken);
        db.ChangeTracker.Clear();
        var run = await db.DigestRuns.AsNoTracking().FirstAsync(row => row.Id == runId, cancellationToken);
        var recipients = await db.DigestDeliveries.AsNoTracking().Where(row => row.RunId == runId).ToListAsync(cancellationToken);
        return Result<DigestRunModel>.Ok(DigestRows.ToModel(run, recipients));
    }

    /// <inheritdoc />
    public async Task<Result<DigestRunPage>> ListRunsAsync(string catalog, Guid id, DigestRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var found = await FindAsync(catalog, id, tracked: false, cancellationToken);
        if (found is not Result<(CatalogEntity Catalog, DigestEntity Digest)>.Success { Value: var pair })
            return Result<DigestRunPage>.Fail(((Result<(CatalogEntity, DigestEntity)>.Failure)found).Error);

        var scope = RunsScope + pair.Digest.Id.ToString("N");
        if (!PageCursor.TryDecode(scope, query.Cursor, out var offset))
            return Result<DigestRunPage>.Fail(EventsErrors.CursorInvalid());

        var limit = PageCursor.Limit(query.Limit);
        var runs = await db.DigestRuns.AsNoTracking()
            .Where(run => run.DigestId == pair.Digest.Id)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.Id)
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = runs.Take(limit).ToList();
        var ids = page.Select(run => run.Id).ToList();
        var recipients = (await db.DigestDeliveries.AsNoTracking().Where(row => ids.Contains(row.RunId)).ToListAsync(cancellationToken))
            .ToLookup(row => row.RunId);

        return Result<DigestRunPage>.Ok(new DigestRunPage(
            page.Select(run => DigestRows.ToModel(run, recipients[run.Id])).ToList(),
            runs.Count > limit ? PageCursor.Encode(scope, offset + limit) : null));
    }

    private IReadOnlyCollection<string> Channels() =>
        deliveries.Select(delivery => delivery.Channel).ToHashSet(StringComparer.Ordinal);

    private async Task<DigestModel> ModelAsync(DigestEntity digest, string catalog, CancellationToken cancellationToken)
    {
        var last = await db.DigestRuns.AsNoTracking()
            .Where(run => run.DigestId == digest.Id)
            .OrderByDescending(run => run.StartedAt)
            .Select(run => (DateTimeOffset?)run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return DigestRows.ToModel(digest, catalog, last);
    }

    private async Task<Result<CatalogEntity>> FindCatalogAsync(EventsCaller caller, string catalog, CancellationToken cancellationToken) =>
        caller.HasOrganization
            ? await CatalogAccess.FindReadableAsync(db, caller, catalog ?? string.Empty, cancellationToken)
            : Result<CatalogEntity>.Fail(EventsErrors.OrganizationRequired());

    private async Task<Result<(CatalogEntity Catalog, DigestEntity Digest)>> FindAsync(string catalog, Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var caller = callers.Current;
        var found = await FindCatalogAsync(caller, catalog, cancellationToken);
        if (found is Result<CatalogEntity>.Failure { Error: var error })
            return Result<(CatalogEntity, DigestEntity)>.Fail(error);

        var row = ((Result<CatalogEntity>.Success)found).Value;
        var digests = tracked ? db.Digests : db.Digests.AsNoTracking();
        var digest = await digests.FirstOrDefaultAsync(
            item => item.Id == id && item.CatalogId == row.Id && item.Issuer == caller.Issuer && item.OrganizationId == caller.OrganizationId,
            cancellationToken);
        return digest is null
            ? Result<(CatalogEntity, DigestEntity)>.Fail(EventsErrors.DigestNotFound())
            : Result<(CatalogEntity, DigestEntity)>.Ok((row, digest));
    }

    private static bool SameSchedule(DigestSchedule left, DigestSchedule right) =>
        left.TimeZone == right.TimeZone && left.Days.SequenceEqual(right.Days) && left.Times.SequenceEqual(right.Times);
}
