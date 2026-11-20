using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Integrates the caller organization's tracking in process, keyed by its (issuer, organization), so another
/// organization's tracking never answers.</summary>
/// <remarks>
///   - the list, its facets and the summary read the catalog's published events; one event's reads and writes also take a candidate
///     the caller's organization sees (the owner's), so a member tracks what it proposed before review; a rejected or
///     archived event answers <see cref="EventsErrors.EventNotFound"/>
///   - a write to existing tracking names its revision; a missing or stale one fails with
///     <see cref="EventsErrors.RevisionConflict"/>, as does a first write naming a revision other than 0
///   - a write that changes nothing keeps the revision; the first write and every stage change land in the history
/// </remarks>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleTrackingBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : IEventTrackingBroker
{
    /// <inheritdoc />
    public async Task<Result<TrackingPage>> ListAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<TrackingPage>.Fail(EventsErrors.OrganizationRequired());
        var found = await CatalogAccess.FindReadableAsync(db, caller, catalog, cancellationToken);
        if (found is not Result<CatalogEntity>.Success { Value: var catalogRow })
            return Result<TrackingPage>.Fail(((Result<CatalogEntity>.Failure)found).Error);

        query ??= new TrackingQuery();
        if (TrackingFilter.TryCreate(query, out var filter) is { } invalid)
            return Result<TrackingPage>.Fail(invalid);

        var scope = $"tracking:{catalogRow.Slug}";
        if (!KeysetCursor.TryDecode(scope, TrackingOrder.Name(filter.Sort), query.Cursor, TrackingOrder.Arity(filter.Sort), out var after))
            return Result<TrackingPage>.Fail(EventsErrors.CursorInvalid());

        var limit = PageCursor.Limit(query.Limit);
        var today = EventListQuery.Today(time);
        var defaultStage = filter.NeedsWorkflow
            ? (await WorkflowStore.LoadAsync(db, caller, cancellationToken)).DefaultStage
            : string.Empty;
        var ordered = TrackingOrder.Page(filter.Rows(db, catalogRow.Id, caller, defaultStage, today), db, filter.Sort, today, after);
        if (ordered is null)
            return Result<TrackingPage>.Fail(EventsErrors.CursorInvalid());

        var page = await ordered.Take(limit + 1).ToListAsync(cancellationToken);
        var items = page.Take(limit).ToList();
        var models = await EventModels.LoadAsync(db, catalogRow.Slug, items.Select(item => item.Event).ToList(), CatalogAccess.IsOwner(catalogRow, caller), cancellationToken);
        var next = page.Count > limit ? TrackingOrder.Encode(scope, filter.Sort, items[^1]) : null;
        return Result<TrackingPage>.Ok(new TrackingPage(
            items.Select(item => new TrackedEventModel
            {
                Event = models[item.Event.Id],
                Tracking = item.Tracking is null ? null : TrackingRows.ToModel(item.Tracking),
            }).ToList(),
            next));
    }

    /// <inheritdoc />
    public async Task<Result<TrackingFacetsModel>> GetFacetsAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<TrackingFacetsModel>.Fail(EventsErrors.OrganizationRequired());
        var found = await CatalogAccess.FindReadableAsync(db, caller, catalog, cancellationToken);
        if (found is not Result<CatalogEntity>.Success { Value: var catalogRow })
            return Result<TrackingFacetsModel>.Fail(((Result<CatalogEntity>.Failure)found).Error);

        query ??= new TrackingQuery();
        if (TrackingFilter.TryCreate(query, out var filter) is { } invalid)
            return Result<TrackingFacetsModel>.Fail(invalid);

        var workflow = await WorkflowStore.LoadAsync(db, caller, cancellationToken);
        return Result<TrackingFacetsModel>.Ok(
            await TrackingFacets.ReadAsync(db, catalogRow, caller, filter, workflow, EventListQuery.Today(time), cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<TrackedEventModel>> GetAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default)
    {
        var found = await FindEventAsync(catalog, eventId, cancellationToken);
        if (found is not Result<TrackingTarget>.Success { Value: var target })
            return Result<TrackedEventModel>.Fail(((Result<TrackingTarget>.Failure)found).Error);

        var tracking = await FindTrackingAsync(target.Caller, eventId, tracked: false, cancellationToken);
        var models = await EventModels.LoadAsync(db, target.Catalog.Slug, [target.Event], CatalogAccess.IsOwner(target.Catalog, target.Caller), cancellationToken);
        return Result<TrackedEventModel>.Ok(new TrackedEventModel
        {
            Event = models[eventId],
            Tracking = tracking is null ? null : TrackingRows.ToModel(tracking),
        });
    }

    /// <inheritdoc />
    public async Task<Result<TrackingModel>> PutAsync(string catalog, Guid eventId, TrackingSpec spec, CancellationToken cancellationToken = default)
    {
        var found = await FindEventAsync(catalog, eventId, cancellationToken);
        if (found is not Result<TrackingTarget>.Success { Value: var target })
            return Result<TrackingModel>.Fail(((Result<TrackingTarget>.Failure)found).Error);
        if (spec is null)
            return Result<TrackingModel>.Fail(EventsErrors.Invalid("tracking", "required"));

        var caller = target.Caller;
        var workflow = await WorkflowStore.LoadAsync(db, caller, cancellationToken);
        if (TrackingRules.Validate(string.Empty, workflow, spec.Stage, spec.Priority, spec.Assignee, spec.Notes, spec.Fields, spec.References, out var values) is { } invalid)
            return Result<TrackingModel>.Fail(invalid);

        var now = time.StoredUtcNow();
        var row = await FindTrackingAsync(caller, eventId, tracked: true, cancellationToken);
        TrackingHistoryEntity? history = null;
        if (row is null)
        {
            if (spec.ExpectedRevision is not (null or 0))
                return Result<TrackingModel>.Fail(EventsErrors.RevisionConflict());

            row = TrackingRows.Create(Guid.CreateVersion7(now), caller, eventId, values, now);
            db.Tracking.Add(row);
            history = TrackingRows.History(row.Id, null, values.Stage, caller, now);
        }
        else
        {
            if (spec.ExpectedRevision != row.Revision)
                return Result<TrackingModel>.Fail(EventsErrors.RevisionConflict());
            if (!TrackingRows.Differs(row, values))
                return Result<TrackingModel>.Ok(TrackingRows.ToModel(row));

            if (!string.Equals(row.Stage, values.Stage, StringComparison.Ordinal))
                history = TrackingRows.History(row.Id, row.Stage, values.Stage, caller, now);
            TrackingRows.Apply(row, values);
            row.Revision++;
            row.UpdatedAt = now;
        }

        // The model declares no relationships, so the history row goes in a second save, after the tracking it references.
        var saved = await SaveAsync(async () =>
        {
            await db.SaveChangesAsync(cancellationToken);
            if (history is not null)
            {
                db.TrackingHistory.Add(history);
                await db.SaveChangesAsync(cancellationToken);
            }
        }, cancellationToken);

        return saved is null ? Result<TrackingModel>.Ok(TrackingRows.ToModel(row)) : Result<TrackingModel>.Fail(saved);
    }

    /// <inheritdoc />
    public async Task<Result<TrackingModel>> DeleteAsync(string catalog, Guid eventId, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var found = await FindEventAsync(catalog, eventId, cancellationToken);
        if (found is not Result<TrackingTarget>.Success { Value: var target })
            return Result<TrackingModel>.Fail(((Result<TrackingTarget>.Failure)found).Error);

        var row = await FindTrackingAsync(target.Caller, eventId, tracked: true, cancellationToken);
        if (row is null)
            return Result<TrackingModel>.Fail(EventsErrors.TrackingNotFound());
        if (row.Revision != expectedRevision)
            return Result<TrackingModel>.Fail(EventsErrors.RevisionConflict());

        var model = TrackingRows.ToModel(row);
        db.Tracking.Remove(row);

        // The history goes with the tracking: its foreign key cascades.
        var saved = await SaveAsync(() => db.SaveChangesAsync(cancellationToken), cancellationToken);
        return saved is null ? Result<TrackingModel>.Ok(model) : Result<TrackingModel>.Fail(saved);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetHistoryAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default)
    {
        var found = await FindEventAsync(catalog, eventId, cancellationToken);
        if (found is not Result<TrackingTarget>.Success { Value: var target })
            return Result<IReadOnlyList<TrackingHistoryEntryModel>>.Fail(((Result<TrackingTarget>.Failure)found).Error);

        var issuer = target.Caller.Issuer;
        var organization = target.Caller.OrganizationId!;
        var rows = await (
                from history in db.TrackingHistory.AsNoTracking()
                join tracking in db.Tracking.AsNoTracking() on history.TrackingId equals tracking.Id
                where tracking.Issuer == issuer && tracking.OrganizationId == organization && tracking.EventId == eventId
                orderby history.At, history.Id
                select history)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<TrackingHistoryEntryModel>>.Ok(rows.Select(TrackingRows.ToModel).ToList());
    }

    /// <inheritdoc />
    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default) =>
        GetSummaryAsync(catalog, new TrackingSummaryQuery { Today = today }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, TrackingSummaryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<TrackingSummaryModel>.Fail(EventsErrors.OrganizationRequired());
        var found = await CatalogAccess.FindReadableAsync(db, caller, catalog, cancellationToken);
        if (found is not Result<CatalogEntity>.Success { Value: var catalogRow })
            return Result<TrackingSummaryModel>.Fail(((Result<CatalogEntity>.Failure)found).Error);

        var today = query.Today;
        var workflow = await WorkflowStore.LoadAsync(db, caller, cancellationToken);
        var issuer = caller.Issuer;
        var organization = caller.OrganizationId!;
        var tracking = db.Tracking.AsNoTracking().Where(item => item.Issuer == issuer && item.OrganizationId == organization);
        var tracked =
            from item in tracking
            join row in EventListQuery.PublishedEvents(db, catalogRow.Id) on item.EventId equals row.Id
            select new { Tracking = item, Event = row };

        var counts = await tracked
            .GroupBy(item => item.Tracking.Stage)
            .Select(group => new { Stage = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Stage, item => item.Count, StringComparer.Ordinal, cancellationToken);

        var byStage = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stage in workflow.Stages)
            byStage[stage.Key] = counts.GetValueOrDefault(stage.Key);
        foreach (var (stage, count) in counts.Where(pair => !byStage.ContainsKey(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            byStage[stage] = count;

        // Every published event that is not cancelled lists its deadlines (PG14): an untracked one stands in the default
        // stage, and an event in a lost stage (or untracked while the default stage is lost) is left out.
        var lost = workflow.Stages.Where(stage => stage.Class == StageClass.Lost).Select(stage => stage.Key).ToArray();
        var defaultStage = workflow.DefaultStage;
        var untrackedLost = lost.Contains(defaultStage, StringComparer.Ordinal);
        var cancelled = EventsLabels.Of(EventStatus.Cancelled);
        var kinds = query.DeadlineKinds.Distinct().Select(kind => EventsLabels.Of(kind)).ToArray();
        var events =
            from row in EventListQuery.PublishedEvents(db, catalogRow.Id)
            where row.Status != cancelled
            join item in tracking on row.Id equals item.EventId into rows
            from item in rows.DefaultIfEmpty()
            select new { Event = row, Stage = item == null ? null : item.Stage };
        events = untrackedLost
            ? events.Where(item => item.Stage != null && !lost.Contains(item.Stage))
            : events.Where(item => item.Stage == null || !lost.Contains(item.Stage));
        var deadlines = db.Deadlines.AsNoTracking().Where(deadline => deadline.AtMin != null && deadline.AtMin >= today);
        if (kinds.Length > 0)
            deadlines = deadlines.Where(deadline => kinds.Contains(deadline.Kind));
        var nearest = await (
                from item in events
                join deadline in deadlines on item.Event.Id equals deadline.EventId
                orderby deadline.AtMin, item.Event.TitleSort, item.Event.Id, deadline.Ordinal
                select new { item.Event.Id, deadline.Ordinal, item.Stage, item.Event.Facts })
            .Take(5)
            .ToListAsync(cancellationToken);

        var upcoming = new List<UpcomingDeadlineModel>(nearest.Count);
        foreach (var item in nearest)
        {
            var facts = EventsJson.Read<EventFacts>(item.Facts);
            if (item.Ordinal < 0 || item.Ordinal >= facts.Deadlines.Count)
                continue;
            upcoming.Add(new UpcomingDeadlineModel
            {
                EventId = item.Id, Title = facts.Title, Deadline = facts.Deadlines[item.Ordinal], Stage = item.Stage ?? defaultStage,
                Tracked = item.Stage is not null,
            });
        }

        var untracked = await EventListQuery.PublishedEvents(db, catalogRow.Id)
            .CountAsync(row => !db.Tracking.Any(item => item.EventId == row.Id && item.Issuer == issuer && item.OrganizationId == organization), cancellationToken);

        return Result<TrackingSummaryModel>.Ok(new TrackingSummaryModel { ByStage = byStage, Untracked = untracked, UpcomingDeadlines = upcoming });
    }

    /// <summary>Finds an event one tracking call may act on: a published event of a readable catalog, or a candidate the
    /// caller's organization owns.</summary>
    private async Task<Result<TrackingTarget>> FindEventAsync(string catalog, Guid eventId, CancellationToken cancellationToken)
    {
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<TrackingTarget>.Fail(EventsErrors.OrganizationRequired());

        var found = await CatalogAccess.FindReadableAsync(db, caller, catalog, cancellationToken);
        if (found is not Result<CatalogEntity>.Success { Value: var catalogRow })
            return Result<TrackingTarget>.Fail(((Result<CatalogEntity>.Failure)found).Error);

        var states = CatalogAccess.IsOwner(catalogRow, caller)
            ? new[] { EventListQuery.Published, EventListQuery.Candidate }
            : [EventListQuery.Published];
        var row = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId && item.CatalogId == catalogRow.Id && states.Contains(item.Review), cancellationToken);

        return row is null
            ? Result<TrackingTarget>.Fail(EventsErrors.EventNotFound())
            : Result<TrackingTarget>.Ok(new TrackingTarget(caller, catalogRow, row));
    }

    private Task<TrackingEntity?> FindTrackingAsync(EventsCaller caller, Guid eventId, bool tracked, CancellationToken cancellationToken)
    {
        var rows = tracked ? db.Tracking : db.Tracking.AsNoTracking();
        return rows.FirstOrDefaultAsync(
            item => item.Issuer == caller.Issuer && item.OrganizationId == caller.OrganizationId && item.EventId == eventId,
            cancellationToken);
    }

    private async Task<AppError?> SaveAsync(Func<Task> save, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await save();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return EventsErrors.RevisionConflict();
        }
        catch (DbUpdateException exception) when (StoreErrors.IsUniqueViolation(exception))
        {
            return EventsErrors.RevisionConflict();
        }
    }
}
