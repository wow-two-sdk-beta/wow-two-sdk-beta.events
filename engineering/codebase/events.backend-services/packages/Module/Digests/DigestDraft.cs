using WoW.Two.Sdk.Beta.Be.Events.Digests;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Represents a digest rendered as of an instant: what a run stores and sends, and what a preview shows.</summary>
/// <param name="AsOf">The instant it was rendered as of.</param>
/// <param name="Matched">How many events matched.</param>
/// <param name="Items">The events after the item cap, in order.</param>
/// <param name="Texts">The message per language.</param>
/// <param name="Recipients">Each recipient's <c>sub</c> and language, in order.</param>
/// <param name="Notes">What could not be done, such as a role no resolver could read.</param>
internal sealed record DigestDraft(
    DateTimeOffset AsOf,
    int Matched,
    IReadOnlyList<DigestItemModel> Items,
    IReadOnlyList<DigestTextModel> Texts,
    IReadOnlyList<(string Subject, string Language)> Recipients,
    IReadOnlyList<string> Notes);
