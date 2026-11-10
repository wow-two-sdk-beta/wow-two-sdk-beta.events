namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Represents what one backlog pass of a source did (events-integration #53).</summary>
/// <param name="Checked">The held events it read: unreviewed candidates and events its classifier reviewed.</param>
/// <param name="Remapped">How many of them it mapped again into changed facts.</param>
/// <param name="Published">How many candidates its classifier published.</param>
internal sealed record BacklogPass(int Checked, int Remapped, int Published)
{
    /// <summary>Holds a pass that read nothing.</summary>
    public static BacklogPass None { get; } = new(0, 0, 0);
}
