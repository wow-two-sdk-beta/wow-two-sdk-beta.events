namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Holds the SCFD intake's settings: the sources it pulls, from the <see cref="SectionName"/> section.</summary>
public sealed class EventsIntakeOptions
{
    /// <summary>Holds the configuration section the intake binds.</summary>
    public const string SectionName = "Events:Intake";

    /// <summary>Gets the sources: one SCFD subscription each, delivering into one catalog.</summary>
    public IList<EventsIntakeSourceOptions> Sources { get; init; } = new List<EventsIntakeSourceOptions>();
}
