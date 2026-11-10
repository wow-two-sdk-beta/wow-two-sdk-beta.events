namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says who may attend an event.</summary>
public enum EventAudience
{
    /// <summary>Anyone.</summary>
    Public,

    /// <summary>The trade: buyers, exhibitors, press.</summary>
    Trade,

    /// <summary>By invitation only.</summary>
    InviteOnly,
}
