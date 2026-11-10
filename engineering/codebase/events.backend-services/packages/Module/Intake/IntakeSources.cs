namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Derives the names an intake source is kept and called under.</summary>
internal static class IntakeSources
{
    /// <summary>Gets the subscription as stored: a GUID in its canonical lowercase form, other ids trimmed.</summary>
    public static string Subscription(EventsIntakeSourceOptions source) =>
        Guid.TryParse(source.SubscriptionId, out var id) ? id.ToString("D") : (source.SubscriptionId ?? string.Empty).Trim();

    /// <summary>Gets the name of the source's HTTP client.</summary>
    public static string ClientName(string sourceName) => "events-intake:" + sourceName;

    /// <summary>Tells whether a source names both its SCFD address and its subscription; a deploy declares sources before
    /// their subscription ids are typed, and such a source stays off instead of failing the host's start.</summary>
    public static bool IsConfigured(EventsIntakeSourceOptions source) =>
        !string.IsNullOrWhiteSpace(source.BaseUrl) && !string.IsNullOrWhiteSpace(source.SubscriptionId);

    /// <summary>Tells whether a source polls: enabled and configured.</summary>
    public static bool IsActive(EventsIntakeSourceOptions source) => source.Enabled && IsConfigured(source);
}
