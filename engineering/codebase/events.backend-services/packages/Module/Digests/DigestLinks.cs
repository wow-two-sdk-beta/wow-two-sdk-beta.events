namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Builds an event's link from a digest's template: <c>{id}</c>, <c>{key}</c> (the key, or the id when none) and
/// <c>{catalog}</c>, each escaped as a URL's data.</summary>
internal static class DigestLinks
{
    /// <summary>Fills a template.</summary>
    /// <param name="template">The template.</param>
    /// <param name="id">The event's id.</param>
    /// <param name="key">The event's key.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <returns>The link.</returns>
    public static string Fill(string template, Guid id, string? key, string catalog) => template
        .Replace("{id}", id.ToString("D"), StringComparison.Ordinal)
        .Replace("{key}", Uri.EscapeDataString(string.IsNullOrEmpty(key) ? id.ToString("D") : key), StringComparison.Ordinal)
        .Replace("{catalog}", Uri.EscapeDataString(catalog), StringComparison.Ordinal);
}
