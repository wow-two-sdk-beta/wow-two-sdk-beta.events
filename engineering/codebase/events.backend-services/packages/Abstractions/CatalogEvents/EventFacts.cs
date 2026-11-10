namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents what is known about an event: the fields a curator writes, a reviewer accepts and a source proposes.</summary>
/// <remarks>Localized texts map a locale (<c>uz</c>, <c>ru</c>, <c>en</c>, <see cref="EventsConstants.UnknownLocale"/>) to
/// the text; a title needs at least one locale.</remarks>
public sealed record EventFacts
{
    /// <summary>Gets the event's key, unique per catalog and matching <see cref="EventsConstants.KeyPattern"/>.</summary>
    public string? Key { get; init; }

    /// <summary>Gets the title per locale, 1 to <see cref="EventsConstants.MaxTitleLength"/> characters each.</summary>
    public required IReadOnlyDictionary<string, string> Title { get; init; }

    /// <summary>Gets the summary per locale, at most <see cref="EventsConstants.MaxSummaryLength"/> characters each.</summary>
    public IReadOnlyDictionary<string, string> Summary { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the kind's key from the catalog's taxonomy, such as <c>runway</c> or <c>trade-fair</c>.</summary>
    public string Kind { get; init; } = "other";

    /// <summary>Gets free lowercase tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets who runs the event.</summary>
    public EventOrganizer? Organizer { get; init; }

    /// <summary>Gets where it happens; none for an online-only event.</summary>
    public EventPlace? Place { get; init; }

    /// <summary>Gets where it is attended.</summary>
    public EventAttendance Attendance { get; init; } = EventAttendance.Offline;

    /// <summary>Gets who may attend.</summary>
    public EventAudience Audience { get; init; } = EventAudience.Public;

    /// <summary>Gets whether it still happens as listed.</summary>
    public EventStatus Status { get; init; } = EventStatus.Scheduled;

    /// <summary>Gets when it starts.</summary>
    public EventWhen Starts { get; init; } = new();

    /// <summary>Gets when it ends; never before it starts.</summary>
    public EventWhen? Ends { get; init; }

    /// <summary>Gets the IANA time zone of its local times.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Gets its deadlines.</summary>
    public IReadOnlyList<EventDeadline> Deadlines { get; init; } = [];

    /// <summary>Gets its offers.</summary>
    public IReadOnlyList<EventOffer> Offers { get; init; } = [];

    /// <summary>Gets its official website.</summary>
    public string? Website { get; init; }

    /// <summary>Gets where to buy tickets.</summary>
    public string? TicketUrl { get; init; }

    /// <summary>Gets where to join online.</summary>
    public string? OnlineUrl { get; init; }

    /// <summary>Gets its images, by reference.</summary>
    public IReadOnlyList<EventImage> Images { get; init; } = [];

    /// <summary>Gets the languages it runs in.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];
}
