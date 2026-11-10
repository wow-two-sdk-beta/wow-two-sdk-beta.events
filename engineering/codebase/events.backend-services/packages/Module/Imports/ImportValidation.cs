using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Imports;

/// <summary>Validates every row of an import bundle before anything is written.</summary>
/// <remarks>Rules per row: a legacy id of 1 to 256 characters, unique in the bundle, whose source identity
/// (<c>{source}/{legacyId}</c>) fits 256 characters; facts by <see cref="EventFactsValidator"/> against the catalog's kinds,
/// keys unique in the bundle; an http(s) source URL; tracking with a stage of the organization's workflow and the tracking
/// broker's limits. A key already held by another event of the catalog is not a validation failure: that row fails alone
/// in the report.</remarks>
internal static class ImportValidation
{
    private const int MaxLegacyIdLength = 256;
    private const int MaxSourceIdLength = 256;
    private const int MaxFailures = 50;

    /// <summary>Validates the rows.</summary>
    /// <param name="source">The bundle's source, trimmed.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="kinds">The catalog's kind keys.</param>
    /// <param name="workflow">The importing organization's workflow.</param>
    /// <param name="failures">Receives one line per failing row, at most 50.</param>
    /// <returns>The validated rows in the bundle's order; complete only when no failure was added.</returns>
    public static List<ImportRowInput> Validate(
        string source, IReadOnlyList<EventImportRow> rows, IReadOnlyCollection<string> kinds, WorkflowModel workflow, List<string> failures)
    {
        var inputs = new List<ImportRowInput>(rows.Count);
        var legacyIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count && failures.Count < MaxFailures; index++)
        {
            var row = rows[index];
            var error = Check(index, source, row, kinds, workflow, legacyIds, keys, out var input);
            if (error is not null)
                failures.Add($"rows[{index}] ({Shorten(row?.LegacyId)}): {error.Message}");
            else
                inputs.Add(input!);
        }

        return inputs;
    }

    private static AppError? Check(
        int index,
        string source,
        EventImportRow? row,
        IReadOnlyCollection<string> kinds,
        WorkflowModel workflow,
        Dictionary<string, int> legacyIds,
        Dictionary<string, int> keys,
        out ImportRowInput? input)
    {
        input = null;
        if (row is null)
            return EventsErrors.Invalid("row", "required");

        var legacyId = row.LegacyId;
        if (string.IsNullOrWhiteSpace(legacyId) || legacyId.Length > MaxLegacyIdLength)
            return EventsErrors.Invalid("legacyId", $"1 to {MaxLegacyIdLength} characters");
        if (!legacyIds.TryAdd(legacyId, index))
            return EventsErrors.Invalid("legacyId", $"repeats rows[{legacyIds[legacyId]}]");

        var sourceId = $"{source}/{legacyId}";
        if (sourceId.Length > MaxSourceIdLength)
            return EventsErrors.Invalid("legacyId", $"with the source, '{{source}}/{{legacyId}}' must fit {MaxSourceIdLength} characters");

        if (EventFactsValidator.Validate(row.Facts!, kinds, out var facts) is { } factsError)
            return factsError;
        if (facts.Key is { } key && !keys.TryAdd(key, index))
            return EventsErrors.Invalid("facts.key", $"'{key}' repeats rows[{keys[key]}]");

        var sourceUrl = string.IsNullOrWhiteSpace(row.SourceUrl) ? null : row.SourceUrl.Trim();
        if (sourceUrl is not null && !IsUrl(sourceUrl))
            return EventsErrors.Invalid("sourceUrl", "an http(s) URL");

        TrackingValues? tracking = null;
        if (row.Tracking is { } wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted.Stage))
                return EventsErrors.Invalid("tracking.stage", "required");
            if (TrackingRules.Validate("tracking.", workflow, wanted.Stage, wanted.Priority, null, wanted.Notes, wanted.Fields, wanted.References, out var values) is { } trackingError)
                return trackingError;
            tracking = values;
        }

        input = new ImportRowInput(legacyId, sourceId, facts, sourceUrl, row.RecordedAtUtc is { } recorded ? StoredClock.Truncate(recorded) : null, tracking, EventsJson.Write(row.Legacy));
        return null;
    }

    private static bool IsUrl(string url) =>
        url.Length <= EventsConstants.MaxUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string Shorten(string? legacyId) =>
        legacyId is null ? "no legacyId" : legacyId.Length <= 64 ? legacyId : legacyId[..64] + "…";
}
