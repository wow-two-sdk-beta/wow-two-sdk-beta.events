using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.CatalogEvents;

/// <summary>Maps an <see cref="EventQuery"/> onto the query string the events and calendar routes read.</summary>
internal static class EventQueryMapper
{
    /// <summary>Maps the filters, and the page unless <paramref name="withPage"/> is off (the calendar takes no page).</summary>
    public static EventsApiQuery ToQuery(EventQuery query, bool withPage = true)
    {
        ArgumentNullException.ThrowIfNull(query);
        var mapped = new EventsApiQuery()
            .Add("from", query.From)
            .Add("to", query.To)
            .Add("kind", query.Kind)
            .Add("country", query.Country)
            .Add("city", query.City)
            .Add("text", query.Text)
            .AddEnum("status", query.Status)
            .AddEnum("review", query.Review)
            .Add("deadlineBefore", query.DeadlineBefore)
            .Add("includeUndated", query.IncludeUndated ? null : false);
        return withPage ? mapped.Add("cursor", query.Cursor).Add("limit", query.Limit) : mapped;
    }
}
