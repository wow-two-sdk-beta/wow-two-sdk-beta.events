using System.Globalization;
using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Represents a classifier's verdict on one delivered record: relevant, not, or undecided.</summary>
/// <param name="Relevant">Whether the record is relevant; none when the classifier did not decide.</param>
/// <param name="Classifier">The classifier's name, recorded as the review's actor <c>classifier:{name}</c>.</param>
/// <param name="Reason">Why, recorded as the review's reason.</param>
internal sealed record IntakeVerdict(bool? Relevant, string Classifier, string Reason)
{
    /// <summary>Holds the actor prefix of an automatic review.</summary>
    public const string ClassifierActor = "classifier:";

    /// <summary>Gets the review's actor.</summary>
    public string Actor => ClassifierActor + Classifier;

    /// <summary>Classifies a record by the source's configured classifier (events-integration #53).</summary>
    /// <remarks>
    ///   - <c>filter</c>: SCFD delivers only what the subscription's filter passed, so every record is relevant
    ///   - <c>payload</c>: the record's own <c>classification</c> object decides — <c>{ classifier, verdict:
    ///     relevant | irrelevant | uncertain, confidence?: 0..1, reason? }</c>, as an AI relevance step writes it; a missing
    ///     or uncertain verdict, or a confidence under the source's minimum, decides nothing
    /// </remarks>
    /// <param name="source">The intake source.</param>
    /// <param name="payload">The record's payload.</param>
    public static IntakeVerdict Of(EventsIntakeSourceOptions source, JsonElement payload)
    {
        if (string.Equals(source.Classifier, EventsIntakeSourceOptions.PayloadClassifier, StringComparison.OrdinalIgnoreCase))
            return FromPayload(source, payload);

        var filter = string.IsNullOrWhiteSpace(source.Filter) ? source.Name : source.Filter.Trim();
        return new IntakeVerdict(true, $"filter:{filter}", $"passed SCFD filter {filter}");
    }

    private static IntakeVerdict FromPayload(EventsIntakeSourceOptions source, JsonElement payload)
    {
        if (!payload.TryGetProperty("classification", out var classification) || classification.ValueKind != JsonValueKind.Object)
            return new IntakeVerdict(null, "payload", "no classification in the record");

        var name = Text(classification, "classifier") ?? "payload";
        var verdict = Text(classification, "verdict")?.ToLowerInvariant();
        double? confidence = classification.TryGetProperty("confidence", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        var because = Text(classification, "reason");
        var detail = $"{verdict ?? "no verdict"}{(confidence is { } c ? $" ({c.ToString("0.##", CultureInfo.InvariantCulture)})" : string.Empty)}{(because is null ? string.Empty : $": {because}")}";

        bool? relevant = verdict switch
        {
            "relevant" when confidence is null || confidence >= source.MinConfidence => true,
            "relevant" => null,
            "irrelevant" => false,
            _ => null,
        };
        return new IntakeVerdict(relevant, name, detail.Length > 500 ? detail[..500] : detail);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
}
