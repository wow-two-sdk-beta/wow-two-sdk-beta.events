using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Review;

/// <summary>Lays a source's proposed facts over a published event's: what the source states replaces the reviewed value,
/// and what it does not know keeps it, so applying a change never blanks a reviewer's value.</summary>
internal static class ProposedFacts
{
    /// <summary>Gets the kind a source reports when it cannot tell one.</summary>
    public const string UnknownKind = "other";

    /// <summary>Lays proposed facts over the current ones.</summary>
    /// <param name="current">The event's reviewed facts.</param>
    /// <param name="proposed">The facts a source proposes.</param>
    /// <returns>The facts to apply.</returns>
    public static EventFacts Overlay(EventFacts current, EventFacts proposed)
    {
        var dated = proposed.Starts is { Precision: not DatePrecision.Unknown };
        return proposed with
        {
            Key = proposed.Key ?? current.Key,
            Summary = proposed.Summary.Count > 0 ? proposed.Summary : current.Summary,
            Kind = proposed.Kind == UnknownKind ? current.Kind : proposed.Kind,
            Tags = proposed.Tags.Count > 0 ? proposed.Tags : current.Tags,
            Organizer = proposed.Organizer ?? current.Organizer,
            Place = proposed.Place ?? current.Place,
            Attendance = proposed.Attendance == default ? current.Attendance : proposed.Attendance,
            Audience = proposed.Audience == default ? current.Audience : proposed.Audience,
            Status = proposed.Status == default ? current.Status : proposed.Status,
            Starts = dated ? proposed.Starts : current.Starts,
            Ends = dated ? proposed.Ends : current.Ends,
            TimeZone = proposed.TimeZone ?? current.TimeZone,
            Deadlines = proposed.Deadlines.Count > 0 ? proposed.Deadlines : current.Deadlines,
            Offers = proposed.Offers.Count > 0 ? proposed.Offers : current.Offers,
            Website = proposed.Website ?? current.Website,
            TicketUrl = proposed.TicketUrl ?? current.TicketUrl,
            OnlineUrl = proposed.OnlineUrl ?? current.OnlineUrl,
            Images = proposed.Images.Count > 0 ? proposed.Images : current.Images,
            Languages = proposed.Languages.Count > 0 ? proposed.Languages : current.Languages,
        };
    }

    /// <summary>Gets the kinds sourced facts may carry: the catalog's, plus the unknown kind a source falls back to.</summary>
    /// <param name="kinds">The catalog's kind keys; empty accepts any key.</param>
    public static IReadOnlyCollection<string> SourcedKinds(IReadOnlyCollection<string> kinds) =>
        kinds.Count == 0 ? kinds : kinds.Append(UnknownKind).ToHashSet(StringComparer.Ordinal);
}
