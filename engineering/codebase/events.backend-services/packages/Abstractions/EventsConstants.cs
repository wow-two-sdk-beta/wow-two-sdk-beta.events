namespace WoW.Two.Sdk.Beta.Be.Events;

/// <summary>Holds the limits of the Events contract.</summary>
public static class EventsConstants
{
    /// <summary>Holds the pattern of a catalog slug and an event key.</summary>
    public const string KeyPattern = "^[a-z0-9][a-z0-9-]{0,63}$";

    /// <summary>Holds the longest catalog slug and event key.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>Holds the longest title, per locale.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Holds the longest summary, per locale.</summary>
    public const int MaxSummaryLength = 2_000;

    /// <summary>Holds the longest name: a catalog's, an organizer's, a venue's, a stage label.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Holds the longest URL.</summary>
    public const int MaxUrlLength = 2_048;

    /// <summary>Holds the longest tracking note.</summary>
    public const int MaxNotesLength = 8_000;

    /// <summary>Holds the longest free text: an offer's terms, a participation route, a cost.</summary>
    public const int MaxTextLength = 2_000;

    /// <summary>Holds the most tags, deadlines, offers, images or references one record carries.</summary>
    public const int MaxListLength = 50;

    /// <summary>Holds the default page size.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Holds the largest page size.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Holds the most rows one import bundle carries.</summary>
    public const int MaxImportRows = 5_000;

    /// <summary>Holds the locale of text whose language is unknown.</summary>
    public const string UnknownLocale = "und";

    /// <summary>Holds the import bundle's format name.</summary>
    public const string ImportFormat = "wow2.events-import.v1";

    /// <summary>Holds the payload schema Events reads from SCFD.</summary>
    public const string EventSchema = "wow2.event.v1";
}
