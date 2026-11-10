using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.CatalogEvents;

/// <summary>Integrates a catalog's events in process: readers see published events, the owner organization sees and
/// writes every state.</summary>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleCatalogEventsBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : ICatalogEventsBroker
{
    private const int MaxSourceSubjectLength = 200;
    private static readonly string Published = EventsLabels.Of(EventReviewState.Published);
    private static readonly string Archived = EventsLabels.Of(EventReviewState.Archived);
    private static readonly string Candidate = EventsLabels.Of(EventReviewState.Candidate);
    private static readonly string Manual = EventsLabels.Of(EventSourceKind.Manual);

    /// <inheritdoc />
    public async Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (await CatalogAccess.FindReadableAsync(db, caller, catalog ?? string.Empty, cancellationToken) is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventPage>.Fail(EventsErrors.CatalogNotFound());

        query ??= new EventQuery();
        var owner = CatalogAccess.IsOwner(found, caller);
        if (!owner && query.Review is { } asked && asked != EventReviewState.Published)
            return Result<EventPage>.Ok(new EventPage([], null));

        var scope = $"events:{found.Slug}:{Fingerprint(query, owner)}";
        if (!PageCursor.TryDecode(scope, query.Cursor, out var offset))
            return Result<EventPage>.Fail(EventsErrors.CursorInvalid());

        var limit = PageCursor.Limit(query.Limit);
        var review = owner ? EventsLabels.Of(query.Review ?? EventReviewState.Published) : Published;
        var rows = Filter(db.Events.AsNoTracking().Where(row => row.CatalogId == found.Id && row.Review == review), query)
            .OrderBy(row => row.StartsMin)
            .ThenBy(row => row.TitleSort)
            .ThenBy(row => row.Id);

        var page = await rows.Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);
        var next = page.Count > limit ? PageCursor.Encode(scope, offset + limit) : null;
        var items = await EventModels.LoadAsync(db, found.Slug, page.Take(limit).ToList(), cancellationToken);
        return Result<EventPage>.Ok(new EventPage(items, next));
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (await CatalogAccess.FindReadableAsync(db, caller, catalog ?? string.Empty, cancellationToken) is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(EventsErrors.CatalogNotFound());

        var owner = CatalogAccess.IsOwner(found, caller);
        var row = await db.Events.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        if (row is null || (!owner && row.Review != Published))
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());

        return Result<EventModel>.Ok(await EventModels.LoadAsync(db, found.Slug, row, cancellationToken));
    }

    /// <inheritdoc />
    public Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        EnterAsync(catalog, spec, EventReviewState.Published, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> ProposeAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        EnterAsync(catalog, spec, EventReviewState.Candidate, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default)
    {
        var owned = await CatalogAccess.FindOwnedAsync(db, callers.Current, catalog ?? string.Empty, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));

        if (spec?.Facts is null)
            return Result<EventModel>.Fail(EventsErrors.Invalid("facts", "required"));
        if (spec.ExpectedRevision is not { } expected)
            return Result<EventModel>.Fail(EventsErrors.Invalid("expectedRevision", "required to replace an event"));
        if (EventFactsValidator.Validate(spec.Facts, CatalogAccess.KindKeys(found), out var facts) is { } invalid)
            return Result<EventModel>.Fail(invalid);

        var row = await db.Events.FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        if (row is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (row.Revision != expected)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());
        if (row.Review == Archived)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("not archived"));
        if (await EventWrites.KeyTakenAsync(db, found.Id, facts.Key, row.Id, cancellationToken))
            return Result<EventModel>.Fail(EventsErrors.KeyTaken());

        await EventWrites.ApplyAsync(db, row, facts, isNew: false, cancellationToken);
        row.Revision++;
        row.UpdatedAt = time.GetUtcNow();
        return await SaveAsync(found.Slug, row, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var owned = await CatalogAccess.FindOwnedAsync(db, callers.Current, catalog ?? string.Empty, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));

        var row = await db.Events.FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        if (row is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (row.Revision != expectedRevision)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());
        if (row.Review == Archived)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("not archived"));

        row.Review = Archived;
        row.Revision++;
        row.UpdatedAt = time.GetUtcNow();
        return await SaveAsync(found.Slug, row, cancellationToken);
    }

    private async Task<Result<EventModel>> EnterAsync(string catalog, EventSpec spec, EventReviewState review, CancellationToken cancellationToken)
    {
        var caller = callers.Current;
        var owned = await CatalogAccess.FindOwnedAsync(db, caller, catalog ?? string.Empty, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));

        if (spec?.Facts is null)
            return Result<EventModel>.Fail(EventsErrors.Invalid("facts", "required"));
        if (EventFactsValidator.Validate(spec.Facts, CatalogAccess.KindKeys(found), out var facts) is { } invalid)
            return Result<EventModel>.Fail(invalid);
        if (await EventWrites.KeyTakenAsync(db, found.Id, facts.Key, Guid.Empty, cancellationToken))
            return Result<EventModel>.Fail(EventsErrors.KeyTaken());

        var now = time.GetUtcNow();
        var row = new CatalogEventEntity
        {
            Id = Guid.CreateVersion7(now),
            CatalogId = found.Id,
            Review = review == EventReviewState.Published ? Published : Candidate,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = review == EventReviewState.Published ? now : null,
        };
        await EventWrites.ApplyAsync(db, row, facts, isNew: true, cancellationToken);
        db.Events.Add(row);

        // A curator's identity per event: the source key is unique per catalog, so it names the event too.
        var subject = caller.Subject ?? "unknown";
        db.Sources.Add(new EventSourceEntity
        {
            CatalogId = found.Id,
            Kind = Manual,
            SourceId = $"{(subject.Length > MaxSourceSubjectLength ? subject[..MaxSourceSubjectLength] : subject)}/{row.Id:N}",
            EventId = row.Id,
            FirstSeenAt = now,
            LastSeenAt = now,
        });

        return await SaveAsync(found.Slug, row, cancellationToken);
    }

    private async Task<Result<EventModel>> SaveAsync(string slug, CatalogEventEntity row, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (EventWrites.ErrorOf(exception) is not null)
        {
            return Result<EventModel>.Fail(EventWrites.ErrorOf(exception)!);
        }

        return Result<EventModel>.Ok(await EventModels.LoadAsync(db, slug, row, cancellationToken));
    }

    private IQueryable<CatalogEventEntity> Filter(IQueryable<CatalogEventEntity> rows, EventQuery query)
    {
        if (!query.IncludeUndated)
            rows = rows.Where(row => row.StartsMin != null);
        if (query.From is { } from)
            rows = rows.Where(row => row.StartsMin == null || row.EndsMax >= from);
        if (query.To is { } to)
            rows = rows.Where(row => row.StartsMin == null || row.StartsMin <= to);
        if (Text(query.Kind)?.ToLowerInvariant() is { } kind)
            rows = rows.Where(row => row.Kind == kind);
        if (Text(query.Country)?.ToUpperInvariant() is { } country)
            rows = rows.Where(row => row.Country == country);
        if (Text(query.City)?.ToLowerInvariant() is { } city)
            rows = rows.Where(row => row.City == city);
        if (Text(query.Text)?.ToLowerInvariant() is { } text)
            rows = rows.Where(row => row.Search.Contains(text));
        if (query.Status is { } status)
        {
            var label = EventsLabels.Of(status);
            rows = rows.Where(row => row.Status == label);
        }

        if (query.DeadlineBefore is { } before)
        {
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            rows = rows.Where(row => db.Deadlines.Any(deadline =>
                deadline.EventId == row.Id && deadline.AtMin != null && deadline.AtMin <= before && deadline.AtMax >= today));
        }

        return rows;
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Fingerprint(EventQuery query, bool owner)
    {
        var text = string.Join('|',
            owner ? "owner" : "reader",
            query.From?.ToString("O", CultureInfo.InvariantCulture),
            query.To?.ToString("O", CultureInfo.InvariantCulture),
            Text(query.Kind)?.ToLowerInvariant(),
            Text(query.Country)?.ToUpperInvariant(),
            Text(query.City)?.ToLowerInvariant(),
            Text(query.Text)?.ToLowerInvariant(),
            query.Status?.ToString(),
            query.Review?.ToString(),
            query.DeadlineBefore?.ToString("O", CultureInfo.InvariantCulture),
            query.IncludeUndated ? "undated" : "dated");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private static Foundation.Errors.AppError Error(Result<CatalogEntity> result) =>
        result is Result<CatalogEntity>.Failure { Error: var error } ? error : EventsErrors.CatalogNotFound();
}
