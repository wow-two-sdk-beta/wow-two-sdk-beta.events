namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Represents what one committed page did.</summary>
/// <param name="Received">The new records stored.</param>
/// <param name="Quarantined">How many of them were quarantined.</param>
/// <param name="HasMore">Whether more records wait.</param>
internal sealed record IntakePageOutcome(int Received, int Quarantined, bool HasMore);
