namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Says what started a digest run.</summary>
public enum DigestRunTrigger
{
    /// <summary>The schedule's occurrence.</summary>
    Schedule,

    /// <summary>A person's send now (<see cref="IEventDigestsBroker.SendNowAsync"/>).</summary>
    Manual,
}
