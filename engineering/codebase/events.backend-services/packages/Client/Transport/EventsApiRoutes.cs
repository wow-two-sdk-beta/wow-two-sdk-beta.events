namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Maps broker calls to the service's routes (contract § HTTP mirror), relative to the service root.</summary>
internal static class EventsApiRoutes
{
    /// <summary>Maps to the catalogs.</summary>
    public static string Catalogs() => EventsRouteConstants.Catalogs;

    /// <summary>Maps to one catalog.</summary>
    public static string Catalog(string catalog) => $"{EventsRouteConstants.Catalogs}/{Uri.EscapeDataString(catalog ?? string.Empty)}";

    /// <summary>Maps to a part of one catalog, such as its events.</summary>
    public static string Catalog(string catalog, string part) => $"{Catalog(catalog)}/{part}";

    /// <summary>Maps to one item under a part of a catalog, such as an event.</summary>
    public static string Catalog(string catalog, string part, Guid id) => $"{Catalog(catalog, part)}/{id:D}";

    /// <summary>Maps to an action on one item under a part of a catalog, such as accepting a candidate.</summary>
    public static string Catalog(string catalog, string part, Guid id, string action) => $"{Catalog(catalog, part, id)}/{action}";
}
