namespace WoW.Two.Sdk.Beta.Be.Events.Catalogs;

/// <summary>Says who reads a catalog's published events.</summary>
public enum CatalogVisibility
{
    /// <summary>Only the owner organization.</summary>
    Private,

    /// <summary>The owner and the organizations it shares with (P2; reads as private until sharing lands).</summary>
    Shared,

    /// <summary>Every authenticated caller.</summary>
    Public,
}
