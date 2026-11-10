namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says how sure a date is.</summary>
public enum DateCertainty
{
    /// <summary>Announced by the organizer or a source.</summary>
    Announced,

    /// <summary>Confirmed by a reviewer against the organizer.</summary>
    Confirmed,

    /// <summary>Predicted from earlier editions; never becomes confirmed without a reviewer.</summary>
    Estimated,
}
