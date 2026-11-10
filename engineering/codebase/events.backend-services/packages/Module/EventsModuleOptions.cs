namespace WoW.Two.Sdk.Beta.Be.Events.Module;

/// <summary>Holds the settings of the in-process Events module.</summary>
public sealed class EventsModuleOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string of the database that holds the module's schema.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
