using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Imports;

/// <summary>Integrates <c>wow2.events-import.v1</c> imports in process: the catalog's owner organization moves its own
/// events into the catalog and its tracking.</summary>
/// <remarks>
///   - the whole bundle validates first (<see cref="ImportValidation"/>); any failing row fails it with
///     <see cref="EventsErrors.ImportInvalid"/> before a write
///   - each row is found through the id map, keyed by (catalog, issuer, organization, legacy id), or through its source
///     identity <c>import</c> <c>{source}/{legacyId}</c> when the map row is missing; found, it updates what differs
///     (revision + 1) or stays unchanged; not found, it creates a published event, its source identity, its tracking with
///     a history entry, and its map row
///   - a key another event of the catalog already holds fails that row alone (<see cref="EventImportOutcome.Failed"/>);
///     the other rows still import
///   - a row's legacy record lands as an <c>import.row</c> observation when it differs from the event's newest one; a
///     changed legacy record alone makes the row <see cref="EventImportOutcome.Updated"/>
///   - a real run writes in one transaction; a dry run writes nothing and returns the same report, whose ids for rows it
///     would create are provisional: the real run's first import mints its own, and every rerun returns those
/// </remarks>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleImportsBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : IEventImportsBroker
{
    private const string LegacyRowType = "import.row";
    private const int MaxSourceLength = 128;

    private static readonly string ImportKind = EventsLabels.Of(EventSourceKind.Import);
    private static readonly string RecordedState = EventsLabels.Of(ObservationState.Recorded);

    /// <inheritdoc />
    public async Task<Result<EventImportReport>> ImportAsync(string catalog, EventImportBundle bundle, bool dryRun, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<EventImportReport>.Fail(EventsErrors.OrganizationRequired());

        var owned = await CatalogAccess.FindOwnedAsync(db, caller, catalog, cancellationToken);
        if (owned is not Result<CatalogEntity>.Success { Value: var catalogRow })
            return Result<EventImportReport>.Fail(((Result<CatalogEntity>.Failure)owned).Error);

        if (bundle is null)
            return Result<EventImportReport>.Fail(EventsErrors.Invalid("bundle", "required"));
        if (!string.Equals(bundle.Format, EventsConstants.ImportFormat, StringComparison.Ordinal))
            return Result<EventImportReport>.Fail(EventsErrors.Invalid("format", $"must be {EventsConstants.ImportFormat}"));

        var source = bundle.Source?.Trim();
        if (string.IsNullOrEmpty(source) || source.Length > MaxSourceLength)
            return Result<EventImportReport>.Fail(EventsErrors.Invalid("source", $"1 to {MaxSourceLength} characters"));

        var rows = bundle.Rows ?? [];
        if (rows.Count is 0 or > EventsConstants.MaxImportRows)
            return Result<EventImportReport>.Fail(EventsErrors.Invalid("rows", $"1 to {EventsConstants.MaxImportRows} rows"));

        var workflow = await WorkflowStore.LoadAsync(db, caller, cancellationToken);
        var failures = new List<string>();
        var inputs = ImportValidation.Validate(source, rows, CatalogAccess.KindKeys(catalogRow), workflow, failures);
        if (failures.Count > 0)
            return Result<EventImportReport>.Fail(EventsErrors.ImportInvalid(failures));

        await using var transaction = dryRun ? null : await db.Database.BeginTransactionAsync(cancellationToken);
        var writes = new ImportWrites();
        var report = await PlanAsync(catalogRow, caller, source, inputs, dryRun, writes, cancellationToken);
        if (dryRun)
        {
            db.ChangeTracker.Clear();
            return Result<EventImportReport>.Ok(report);
        }

        try
        {
            await SaveAsync(writes, cancellationToken);
            await transaction!.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<EventImportReport>.Fail(EventsErrors.RevisionConflict());
        }
        catch (DbUpdateException exception) when (StoreErrors.IsUniqueViolation(exception))
        {
            return Result<EventImportReport>.Fail(EventsErrors.RevisionConflict());
        }

        return Result<EventImportReport>.Ok(report);
    }

    private async Task<EventImportReport> PlanAsync(
        CatalogEntity catalogRow,
        EventsCaller caller,
        string source,
        List<ImportRowInput> inputs,
        bool dryRun,
        ImportWrites writes,
        CancellationToken cancellationToken)
    {
        var issuer = caller.Issuer;
        var organization = caller.OrganizationId!;
        var catalogId = catalogRow.Id;

        var legacyIds = inputs.Select(input => input.LegacyId).ToArray();
        var maps = await db.ImportMap
            .Where(map => map.CatalogId == catalogId && map.Issuer == issuer && map.OrganizationId == organization && legacyIds.Contains(map.LegacyId))
            .ToDictionaryAsync(map => map.LegacyId, StringComparer.Ordinal, cancellationToken);

        var sourceIds = inputs.Select(input => input.SourceId).ToArray();
        var sources = await db.Sources
            .Where(item => item.CatalogId == catalogId && item.Kind == ImportKind && sourceIds.Contains(item.SourceId))
            .ToDictionaryAsync(item => item.SourceId, StringComparer.Ordinal, cancellationToken);

        var knownIds = maps.Values.Select(map => map.EventId).Concat(sources.Values.Select(item => item.EventId)).Distinct().ToArray();
        var events = await db.Events
            .Where(row => row.CatalogId == catalogId && knownIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var trackings = await db.Tracking
            .Where(row => row.Issuer == issuer && row.OrganizationId == organization && knownIds.Contains(row.EventId))
            .ToDictionaryAsync(row => row.EventId, cancellationToken);
        var legacy = (await db.Observations.AsNoTracking()
                .Where(row => row.CatalogId == catalogId && row.SourceKind == ImportKind && row.Type == LegacyRowType
                    && row.EventId != null && knownIds.Contains(row.EventId.Value))
                .Select(row => new { EventId = row.EventId!.Value, row.Seq, row.Payload })
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.EventId)
            .ToDictionary(group => group.Key, group => group.MaxBy(row => row.Seq)!.Payload);

        var keys = inputs.Select(input => input.Facts.Key).OfType<string>().ToArray();
        var keyHolders = await db.Events.AsNoTracking()
            .Where(row => row.CatalogId == catalogId && row.Key != null && keys.Contains(row.Key))
            .Select(row => new { Key = row.Key!, row.Id })
            .ToDictionaryAsync(row => row.Key, row => row.Id, StringComparer.Ordinal, cancellationToken);

        var now = time.StoredUtcNow();
        var entries = new List<EventImportMapEntry>(inputs.Count);
        foreach (var input in inputs)
        {
            maps.TryGetValue(input.LegacyId, out var map);
            sources.TryGetValue(input.SourceId, out var sourceRow);
            var known = map?.EventId ?? sourceRow?.EventId;
            var eventRow = known is { } id ? events.GetValueOrDefault(id) : null;

            if (input.Facts.Key is { } key && keyHolders.TryGetValue(key, out var holder) && holder != eventRow?.Id)
            {
                entries.Add(new EventImportMapEntry
                {
                    LegacyId = input.LegacyId,
                    EventId = eventRow?.Id ?? Guid.Empty,
                    Outcome = EventImportOutcome.Failed,
                    Error = EventsErrors.Invalid("facts.key", $"'{key}' is already taken by another event of the catalog").Message,
                });
                continue;
            }

            entries.Add(eventRow is null
                ? Create(catalogId, caller, source, input, now, dryRun, writes)
                : Update(catalogId, caller, source, input, eventRow, map, sourceRow, trackings.GetValueOrDefault(eventRow.Id),
                    legacy.GetValueOrDefault(eventRow.Id), now, dryRun, writes));
        }

        return new EventImportReport
        {
            DryRun = dryRun,
            Source = source,
            Created = entries.Count(entry => entry.Outcome == EventImportOutcome.Created),
            Updated = entries.Count(entry => entry.Outcome == EventImportOutcome.Updated),
            Unchanged = entries.Count(entry => entry.Outcome == EventImportOutcome.Unchanged),
            Failed = entries.Count(entry => entry.Outcome == EventImportOutcome.Failed),
            Map = entries,
        };
    }

    private static EventImportMapEntry Create(
        Guid catalogId, EventsCaller caller, string source, ImportRowInput input, DateTimeOffset now, bool dryRun, ImportWrites writes)
    {
        var eventId = Guid.CreateVersion7(now);
        Guid? trackingId = input.Tracking is null ? null : Guid.CreateVersion7(now);
        if (!dryRun)
        {
            var row = new CatalogEventEntity
            {
                Id = eventId,
                CatalogId = catalogId,
                Review = EventListQuery.Published,
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = input.RecordedAtUtc ?? now,
            };
            writes.Deadlines.AddRange(EventRows.ApplyFacts(row, input.Facts));
            writes.Events.Add(row);
            writes.Sources.Add(Source(catalogId, input, eventId, now));
            if (input.Tracking is { } tracking)
            {
                writes.Tracking.Add(TrackingRows.Create(trackingId!.Value, caller, eventId, tracking, now));
                writes.History.Add(TrackingRows.History(trackingId.Value, null, tracking.Stage, caller, now));
            }

            if (input.Legacy is not null)
                writes.Observations.Add(Evidence(catalogId, input, eventId, now));
            writes.Map.Add(new ImportMapEntity
            {
                CatalogId = catalogId,
                Issuer = caller.Issuer,
                OrganizationId = caller.OrganizationId!,
                LegacyId = input.LegacyId,
                Source = source,
                EventId = eventId,
                TrackingId = trackingId,
                ImportedAt = now,
                UpdatedAt = now,
            });
        }

        return new EventImportMapEntry { LegacyId = input.LegacyId, EventId = eventId, TrackingId = trackingId, Outcome = EventImportOutcome.Created };
    }

    private static EventImportMapEntry Update(
        Guid catalogId,
        EventsCaller caller,
        string source,
        ImportRowInput input,
        CatalogEventEntity eventRow,
        ImportMapEntity? map,
        EventSourceEntity? sourceRow,
        TrackingEntity? trackingRow,
        string? lastLegacy,
        DateTimeOffset now,
        bool dryRun,
        ImportWrites writes)
    {
        var changed = false;
        if (!JsonComparison.Same(eventRow.Facts, EventsJson.Write(input.Facts)))
        {
            changed = true;
            if (!dryRun)
            {
                writes.ReplacedDeadlines.Add(eventRow.Id);
                writes.Deadlines.AddRange(EventRows.ApplyFacts(eventRow, input.Facts));
                eventRow.Revision++;
                eventRow.UpdatedAt = now;
            }
        }

        if (sourceRow is null)
        {
            changed = true;
            if (!dryRun)
                writes.Sources.Add(Source(catalogId, input, eventRow.Id, now));
        }
        else if (sourceRow.EventId == eventRow.Id && !string.Equals(sourceRow.Url, input.SourceUrl, StringComparison.Ordinal))
        {
            changed = true;
            if (!dryRun)
            {
                sourceRow.Url = input.SourceUrl;
                sourceRow.LastSeenAt = now;
            }
        }

        Guid? trackingId = null;
        if (input.Tracking is { } wanted)
        {
            if (trackingRow is null)
            {
                changed = true;
                trackingId = Guid.CreateVersion7(now);
                if (!dryRun)
                {
                    writes.Tracking.Add(TrackingRows.Create(trackingId.Value, caller, eventRow.Id, wanted, now));
                    writes.History.Add(TrackingRows.History(trackingId.Value, null, wanted.Stage, caller, now));
                }
            }
            else
            {
                trackingId = trackingRow.Id;
                var values = wanted with { Assignee = trackingRow.Assignee };
                if (TrackingRows.Differs(trackingRow, values))
                {
                    changed = true;
                    if (!dryRun)
                    {
                        if (!string.Equals(trackingRow.Stage, values.Stage, StringComparison.Ordinal))
                            writes.History.Add(TrackingRows.History(trackingRow.Id, trackingRow.Stage, values.Stage, caller, now));
                        TrackingRows.Apply(trackingRow, values);
                        trackingRow.Revision++;
                        trackingRow.UpdatedAt = now;
                    }
                }
            }
        }

        if (input.Legacy is not null && !JsonComparison.Same(lastLegacy, input.Legacy))
        {
            changed = true;
            if (!dryRun)
                writes.Observations.Add(Evidence(catalogId, input, eventRow.Id, now));
        }

        if (map is null)
        {
            changed = true;
            if (!dryRun)
            {
                writes.Map.Add(new ImportMapEntity
                {
                    CatalogId = catalogId,
                    Issuer = caller.Issuer,
                    OrganizationId = caller.OrganizationId!,
                    LegacyId = input.LegacyId,
                    Source = source,
                    EventId = eventRow.Id,
                    TrackingId = trackingId,
                    ImportedAt = now,
                    UpdatedAt = now,
                });
            }
        }
        else if (input.Tracking is not null && map.TrackingId != trackingId)
        {
            changed = true;
            if (!dryRun)
                map.TrackingId = trackingId;
        }

        if (changed && !dryRun && map is not null)
            map.UpdatedAt = now;

        return new EventImportMapEntry
        {
            LegacyId = input.LegacyId,
            EventId = eventRow.Id,
            TrackingId = trackingId,
            Outcome = changed ? EventImportOutcome.Updated : EventImportOutcome.Unchanged,
        };
    }

    // Saves in foreign-key order: events (with every change to loaded rows), then the rows that reference them, then the
    // history that references new tracking.
    private async Task SaveAsync(ImportWrites writes, CancellationToken cancellationToken)
    {
        if (writes.ReplacedDeadlines.Count > 0)
        {
            var replaced = writes.ReplacedDeadlines.ToArray();
            await db.Deadlines.Where(row => replaced.Contains(row.EventId)).ExecuteDeleteAsync(cancellationToken);
        }

        db.Events.AddRange(writes.Events);
        await db.SaveChangesAsync(cancellationToken);

        db.Deadlines.AddRange(writes.Deadlines);
        db.Sources.AddRange(writes.Sources);
        db.Tracking.AddRange(writes.Tracking);
        db.ImportMap.AddRange(writes.Map);
        db.Observations.AddRange(writes.Observations);
        await db.SaveChangesAsync(cancellationToken);

        db.TrackingHistory.AddRange(writes.History);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static EventSourceEntity Source(Guid catalogId, ImportRowInput input, Guid eventId, DateTimeOffset now) => new()
    {
        CatalogId = catalogId,
        Kind = ImportKind,
        SourceId = input.SourceId,
        EventId = eventId,
        Url = input.SourceUrl,
        FirstSeenAt = input.RecordedAtUtc ?? now,
        LastSeenAt = now,
    };

    private static ObservationEntity Evidence(Guid catalogId, ImportRowInput input, Guid eventId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        CatalogId = catalogId,
        SourceKind = ImportKind,
        Type = LegacyRowType,
        State = RecordedState,
        Payload = input.Legacy,
        EventId = eventId,
        ObservedAt = input.RecordedAtUtc ?? now,
        ReceivedAt = now,
        Links = "[]",
    };
}
