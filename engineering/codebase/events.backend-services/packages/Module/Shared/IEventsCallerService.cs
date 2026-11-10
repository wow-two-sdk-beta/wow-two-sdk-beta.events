namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Defines who every broker call acts for.</summary>
internal interface IEventsCallerService
{
    /// <summary>Gets the current caller.</summary>
    EventsCaller Current { get; }
}
