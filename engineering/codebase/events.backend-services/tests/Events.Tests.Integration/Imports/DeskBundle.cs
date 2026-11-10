using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace Events.Tests.Integration.Imports;

/// <summary>Builds bundles in the shape Ocharo Desk exports: month and estimated dates, undated rows, <c>und</c> titles,
/// tracking in Ocharo's stages with <c>participation</c> and <c>cost</c> fields, and the legacy row as evidence.</summary>
internal static class DeskBundle
{
    /// <summary>Holds the producing system's name.</summary>
    public const string Source = "ocharo-desk";

    /// <summary>Builds a bundle of the three Desk rows.</summary>
    public static EventImportBundle Create() => new() { Source = Source, Rows = [MonthRow(), UndatedRow(), DayRow()] };

    /// <summary>A month start, estimated, no end, a day deadline and shortlisted tracking with fields.</summary>
    public static EventImportRow MonthRow() => new()
    {
        LegacyId = "0193a1b2-0001",
        Facts = new EventFacts
        {
            Key = "tashkent-fashion-week-2026",
            Title = new Dictionary<string, string> { [EventsConstants.UnknownLocale] = "Tashkent Fashion Week" },
            Kind = "runway",
            Organizer = new EventOrganizer { Name = "Fashion Council", Website = "https://council.example/" },
            Place = new EventPlace { Venue = "Expo Centre", City = "Tashkent", Country = "uz" },
            Starts = new EventWhen { Value = "2026-10", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated },
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = "2026-08-15", Precision = DatePrecision.Day } }],
            Website = "https://tfw.example/",
        },
        SourceUrl = "https://desk.example/showcase/1",
        RecordedAtUtc = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero),
        Tracking = new EventImportTracking
        {
            Stage = "shortlisted",
            Priority = TrackingPriority.High,
            Notes = "Apply with the spring capsule",
            Fields = Json("""{ "participation": "Runway slot", "cost": "1500 USD" }"""),
        },
        Legacy = Json("""{ "id": "0193a1b2-0001", "status": "shortlisted", "date_precision": "estimated", "format": "runway" }"""),
    };

    /// <summary>An undated row with candidate tracking.</summary>
    public static EventImportRow UndatedRow() => new()
    {
        LegacyId = "0193a1b2-0002",
        Facts = new EventFacts
        {
            Title = new Dictionary<string, string> { [EventsConstants.UnknownLocale] = "Bukhara Silk Festival" },
            Kind = "festival",
            Starts = new EventWhen { Precision = DatePrecision.Unknown },
        },
        Tracking = new EventImportTracking { Stage = "candidate", Fields = Json("""{ "participation": null, "cost": null }""") },
    };

    /// <summary>A day start, announced, imported without tracking.</summary>
    public static EventImportRow DayRow() => new()
    {
        LegacyId = "0193a1b2-0003",
        Facts = new EventFacts
        {
            Title = new Dictionary<string, string> { [EventsConstants.UnknownLocale] = "Samarkand Craft Expo" },
            Kind = "fair",
            Starts = new EventWhen { Value = "2026-12-05", Precision = DatePrecision.Day },
        },
        SourceUrl = "https://desk.example/showcase/3",
    };

    /// <summary>Parses JSON into a detached element.</summary>
    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
