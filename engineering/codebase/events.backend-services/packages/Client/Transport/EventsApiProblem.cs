namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Represents the parts of a problem body the client reads.</summary>
/// <param name="Title">The problem's title.</param>
/// <param name="Detail">What went wrong.</param>
internal sealed record EventsApiProblem(string? Title, string? Detail);
