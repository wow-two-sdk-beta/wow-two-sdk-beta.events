namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Names the languages a digest is rendered in.</summary>
public static class DigestLanguageConstants
{
    /// <summary>Uzbek in Latin script.</summary>
    public const string UzbekLatin = "uz-Latn";

    /// <summary>Russian.</summary>
    public const string Russian = "ru";

    /// <summary>English.</summary>
    public const string English = "en";

    /// <summary>Gets every language, in that order.</summary>
    public static IReadOnlyList<string> All { get; } = [UzbekLatin, Russian, English];
}
