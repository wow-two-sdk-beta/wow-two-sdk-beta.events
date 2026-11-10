namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Holds a validated tracking write in the row's shape.</summary>
/// <param name="Stage">The stage, a key of the organization's workflow.</param>
/// <param name="Priority">The priority's label.</param>
/// <param name="Assignee">The assignee's subject.</param>
/// <param name="Notes">The notes.</param>
/// <param name="Fields">The custom fields as JSON text; none when absent.</param>
/// <param name="Refs">The references as JSON text.</param>
internal sealed record TrackingValues(string Stage, string Priority, string? Assignee, string Notes, string? Fields, string Refs);
