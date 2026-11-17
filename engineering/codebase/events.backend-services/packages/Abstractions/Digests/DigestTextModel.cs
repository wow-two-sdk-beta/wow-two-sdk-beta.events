namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents a digest rendered in one language: the message every recipient reading it receives.</summary>
public sealed record DigestTextModel
{
    /// <summary>Gets the language (<see cref="DigestLanguageConstants"/>).</summary>
    public required string Language { get; init; }

    /// <summary>Gets the text in Telegram's HTML subset (<c>b</c>, <c>i</c>, <c>a href</c>), at most
    /// <see cref="EventsConstants.MaxDigestTextLength"/> characters as sent.</summary>
    public required string Text { get; init; }

    /// <summary>Gets how many recipients read it.</summary>
    public required int Recipients { get; init; }
}
