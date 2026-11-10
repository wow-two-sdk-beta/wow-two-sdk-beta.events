using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Review;

/// <summary>Integrates a catalog's review in process: the queue, candidate decisions, source changes and observations,
/// for the owner organization only.</summary>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleReviewBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : IEventReviewBroker
{
    private static readonly string Candidate = EventsLabels.Of(EventReviewState.Candidate);
    private static readonly string Published = EventsLabels.Of(EventReviewState.Published);
    private static readonly string Rejected = EventsLabels.Of(EventReviewState.Rejected);
    private static readonly string Archived = EventsLabels.Of(EventReviewState.Archived);
    private static readonly string Pending = EventsLabels.Of(ObservationState.Pending);
    private static readonly string Applied = EventsLabels.Of(ObservationState.Applied);
    private static readonly string Accepted = EventsLabels.Of(ObservationState.Accepted);
    private static readonly string Dismissed = EventsLabels.Of(ObservationState.Dismissed);

    /// <inheritdoc />
    public async Task<Result<ReviewPage>> ListAsync(string catalog, ReviewQuery query, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<ReviewPage>.Fail(Error(owned));

        query ??= new ReviewQuery();
        var scope = $"review:{found.Slug}";
        if (!PageCursor.TryDecode(scope, query.Cursor, out var offset))
            return Result<ReviewPage>.Fail(EventsErrors.CursorInvalid());

        var limit = PageCursor.Limit(query.Limit);
        var candidates = db.Events.AsNoTracking().Where(row => row.CatalogId == found.Id && row.Review == Candidate);
        var candidateCount = await candidates.CountAsync(cancellationToken);
        var page = new List<CatalogEventEntity>();
        if (offset < candidateCount)
        {
            page.AddRange(await candidates.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id)
                .Skip(offset).Take(limit + 1).ToListAsync(cancellationToken));
        }

        if (page.Count <= limit)
        {
            var changed = await db.Events.AsNoTracking()
                .Where(row => row.CatalogId == found.Id && row.Review == Published)
                .Select(row => new
                {
                    Row = row,
                    Oldest = db.Observations.Where(observation => observation.EventId == row.Id && observation.CatalogId == found.Id && observation.State == Pending)
                        .Min(observation => (long?)observation.Seq),
                })
                .Where(item => item.Oldest != null)
                .OrderBy(item => item.Oldest).ThenBy(item => item.Row.Id)
                .Skip(Math.Max(0, offset - candidateCount)).Take(limit + 1 - page.Count)
                .ToListAsync(cancellationToken);
            page.AddRange(changed.Select(item => item.Row));
        }

        var next = page.Count > limit ? PageCursor.Encode(scope, offset + limit) : null;
        var rows = page.Take(limit).ToList();
        var models = await EventModels.LoadAsync(db, found.Slug, rows, cancellationToken);
        var ids = rows.Select(row => row.Id).ToList();
        var evidence = (await db.Observations.AsNoTracking()
                .Where(observation => observation.EventId != null && ids.Contains(observation.EventId.Value) && observation.CatalogId == found.Id
                    && (observation.State == Pending || observation.State == Applied))
                .OrderBy(observation => observation.Seq)
                .ToListAsync(cancellationToken))
            .ToLookup(observation => observation.EventId!.Value);

        var items = models.Select((model, index) => new ReviewItemModel
        {
            Event = model,
            Observations = evidence[model.Id]
                .Where(observation => observation.State == Pending || (rows[index].Review == Candidate && observation.State == Applied))
                .Select(observation => ObservationModels.ToModel(observation, found.Slug))
                .ToList(),
        }).ToList();
        return Result<ReviewPage>.Ok(new ReviewPage(items, next));
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> AcceptAsync(string catalog, Guid id, ReviewDecision decision, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));
        if (decision is null)
            return Result<EventModel>.Fail(EventsErrors.Invalid("decision", "required"));

        var row = await db.Events.FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        if (row is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (row.Review != Candidate)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("a candidate"));
        if (row.Revision != decision.ExpectedRevision)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());

        if (decision.Facts is not null)
        {
            if (EventFactsValidator.Validate(decision.Facts, CatalogAccess.KindKeys(found), out var facts) is { } invalid)
                return Result<EventModel>.Fail(invalid);
            if (await EventWrites.KeyTakenAsync(db, found.Id, facts.Key, row.Id, cancellationToken))
                return Result<EventModel>.Fail(EventsErrors.KeyTaken());
            await EventWrites.ApplyAsync(db, row, facts, isNew: false, cancellationToken);
        }

        var now = time.StoredUtcNow();
        row.Review = Published;
        row.PublishedAt ??= now;
        row.Revision++;
        row.UpdatedAt = now;
        ReviewStamp.ByPerson(row, callers.Current, decision.Facts is null ? "accepted" : "accepted with edits", now);
        return await SaveEventAsync(found.Slug, row, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> RejectAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));

        var row = await db.Events.FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        if (row is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (row.Review != Candidate)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("a candidate"));
        if (row.Revision != expectedRevision)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());

        row.Review = Rejected;
        row.Revision++;
        row.UpdatedAt = time.StoredUtcNow();
        ReviewStamp.ByPerson(row, callers.Current, "rejected", row.UpdatedAt);
        return await SaveEventAsync(found.Slug, row, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> ReopenAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));

        var row = await db.Events.FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        if (row is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (row.Review != Published)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("published"));
        if (row.Revision != expectedRevision)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());

        row.Review = Candidate;
        row.Revision++;
        row.UpdatedAt = time.StoredUtcNow();
        ReviewStamp.ByPerson(row, callers.Current, "reopened for review", row.UpdatedAt);
        return await SaveEventAsync(found.Slug, row, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> MergeAsync(string catalog, Guid id, MergeDecision decision, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));
        if (decision is null)
            return Result<EventModel>.Fail(EventsErrors.Invalid("decision", "required"));
        if (decision.IntoId == id)
            return Result<EventModel>.Fail(EventsErrors.Invalid("intoId", "another event than the candidate"));

        var candidate = await db.Events.FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        var target = await db.Events.FirstOrDefaultAsync(item => item.Id == decision.IntoId && item.CatalogId == found.Id, cancellationToken);
        if (candidate is null || target is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (candidate.Review != Candidate)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("a candidate"));
        if (target.Review == Archived)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("not archived to merge into"));
        if (candidate.Revision != decision.ExpectedRevision || target.Revision != decision.IntoExpectedRevision)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());

        var now = time.StoredUtcNow();
        Guid? into = target.Id;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The candidate's identities, posts and evidence now speak for the target: a later revision lands there.
        await db.Sources.Where(source => source.EventId == candidate.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(source => source.EventId, target.Id), cancellationToken);
        await db.SourcePosts.Where(post => post.EventId == candidate.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(post => post.EventId, into), cancellationToken);
        await db.Observations.Where(observation => observation.EventId == candidate.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(observation => observation.EventId, into), cancellationToken);

        candidate.Review = Archived;
        candidate.MergedInto = target.Id;
        candidate.Revision++;
        candidate.UpdatedAt = now;
        ReviewStamp.ByPerson(candidate, callers.Current, "merged", now);
        target.Revision++;
        target.UpdatedAt = now;
        ReviewStamp.ByPerson(target, callers.Current, "merged a candidate in", now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (EventWrites.ErrorOf(exception) is not null)
        {
            return Result<EventModel>.Fail(EventWrites.ErrorOf(exception)!);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result<EventModel>.Ok(await EventModels.LoadAsync(db, found.Slug, target, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<EventModel>> ApplyChangeAsync(string catalog, Guid observationId, ReviewDecision decision, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<EventModel>.Fail(Error(owned));
        if (decision is null)
            return Result<EventModel>.Fail(EventsErrors.Invalid("decision", "required"));

        var observation = await db.Observations.FirstOrDefaultAsync(item => item.Id == observationId && item.CatalogId == found.Id, cancellationToken);
        if (observation is null)
            return Result<EventModel>.Fail(EventsErrors.ObservationNotFound());
        if (observation.State != Pending)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("a pending change"));

        var row = observation.EventId is { } eventId
            ? await db.Events.FirstOrDefaultAsync(item => item.Id == eventId && item.CatalogId == found.Id, cancellationToken)
            : null;
        if (row is null)
            return Result<EventModel>.Fail(EventsErrors.EventNotFound());
        if (row.Review != Published)
            return Result<EventModel>.Fail(EventsErrors.ReviewStateInvalid("published"));
        if (row.Revision != decision.ExpectedRevision)
            return Result<EventModel>.Fail(EventsErrors.RevisionConflict());

        var kinds = CatalogAccess.KindKeys(found);
        EventFacts candidateFacts;
        if (decision.Facts is not null)
            candidateFacts = decision.Facts;
        else if (observation.Proposed is not null)
        {
            candidateFacts = ProposedFacts.Overlay(EventsJson.Read<EventFacts>(row.Facts), EventsJson.Read<EventFacts>(observation.Proposed));
            kinds = ProposedFacts.SourcedKinds(kinds);
        }
        else
            return Result<EventModel>.Fail(EventsErrors.Invalid("facts", "required: the change proposes none"));

        if (EventFactsValidator.Validate(candidateFacts, kinds, out var facts) is { } invalid)
            return Result<EventModel>.Fail(invalid);
        if (await EventWrites.KeyTakenAsync(db, found.Id, facts.Key, row.Id, cancellationToken))
            return Result<EventModel>.Fail(EventsErrors.KeyTaken());

        var now = time.StoredUtcNow();
        await EventWrites.ApplyAsync(db, row, facts, isNew: false, cancellationToken);
        row.Revision++;
        row.UpdatedAt = now;
        ReviewStamp.ByPerson(row, callers.Current, "applied a source change", now);
        Resolve(observation, Accepted, now);
        return await SaveEventAsync(found.Slug, row, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<ObservationModel>> DismissChangeAsync(string catalog, Guid observationId, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<ObservationModel>.Fail(Error(owned));

        var observation = await db.Observations.FirstOrDefaultAsync(item => item.Id == observationId && item.CatalogId == found.Id, cancellationToken);
        if (observation is null)
            return Result<ObservationModel>.Fail(EventsErrors.ObservationNotFound());
        if (observation.State != Pending)
            return Result<ObservationModel>.Fail(EventsErrors.ReviewStateInvalid("a pending change"));

        Resolve(observation, Dismissed, time.StoredUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result<ObservationModel>.Ok(ObservationModels.ToModel(observation, found.Slug));
    }

    /// <inheritdoc />
    public async Task<Result<ObservationPage>> ListObservationsAsync(string catalog, ObservationQuery query, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<ObservationPage>.Fail(Error(owned));

        query ??= new ObservationQuery();
        var scope = $"observations:{found.Slug}:{query.State}:{query.EventId:N}";
        if (!PageCursor.TryDecode(scope, query.Cursor, out var offset))
            return Result<ObservationPage>.Fail(EventsErrors.CursorInvalid());

        var limit = PageCursor.Limit(query.Limit);
        var rows = db.Observations.AsNoTracking().Where(observation => observation.CatalogId == found.Id);
        if (query.State is { } state)
        {
            var label = EventsLabels.Of(state);
            rows = rows.Where(observation => observation.State == label);
        }

        if (query.EventId is { } eventId)
            rows = rows.Where(observation => observation.EventId == eventId);

        var page = await rows.OrderByDescending(observation => observation.Seq).Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);
        var next = page.Count > limit ? PageCursor.Encode(scope, offset + limit) : null;
        return Result<ObservationPage>.Ok(new ObservationPage(
            page.Take(limit).Select(observation => ObservationModels.ToModel(observation, found.Slug)).ToList(), next));
    }

    /// <inheritdoc />
    public async Task<Result<ObservationModel>> GetObservationAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        var owned = await OwnedAsync(catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var found })
            return Result<ObservationModel>.Fail(Error(owned));

        var observation = await db.Observations.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.CatalogId == found.Id, cancellationToken);
        return observation is null
            ? Result<ObservationModel>.Fail(EventsErrors.ObservationNotFound())
            : Result<ObservationModel>.Ok(ObservationModels.ToModel(observation, found.Slug));
    }

    private Task<Result<CatalogEntity>> OwnedAsync(string catalog, CancellationToken cancellationToken) =>
        CatalogAccess.FindOwnedAsync(db, callers.Current, catalog ?? string.Empty, cancellationToken);

    private void Resolve(ObservationEntity observation, string state, DateTimeOffset now)
    {
        var caller = callers.Current;
        observation.State = state;
        observation.ResolvedAt = now;
        observation.ResolvedByIssuer = caller.Issuer.Length == 0 ? null : caller.Issuer;
        observation.ResolvedBySubject = caller.Subject;
    }

    private async Task<Result<EventModel>> SaveEventAsync(string slug, CatalogEventEntity row, CancellationToken cancellationToken)
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

    private static AppError Error(Result<CatalogEntity> result) =>
        result is Result<CatalogEntity>.Failure { Error: var error } ? error : EventsErrors.CatalogNotFound();
}
