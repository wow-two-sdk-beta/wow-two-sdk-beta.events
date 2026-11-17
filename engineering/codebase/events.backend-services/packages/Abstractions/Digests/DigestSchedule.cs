namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents when a digest goes out: the days of the week and the local times, in one IANA time zone.</summary>
/// <remarks>Each pair of a day and a time is one occurrence, such as Monday 09:00 <c>Asia/Tashkent</c> (04:00 UTC). A time a
/// daylight-saving change skips runs at the first instant after the gap; a repeated time runs once, at its first instant.</remarks>
public sealed record DigestSchedule
{
    /// <summary>Gets the days of the week it goes out on; 1 to 7, each once.</summary>
    public IReadOnlyList<DayOfWeek> Days { get; init; } = [];

    /// <summary>Gets the local times it goes out at, <c>HH:mm</c>; 1 to <see cref="EventsConstants.MaxDigestTimes"/>, each once.</summary>
    public IReadOnlyList<string> Times { get; init; } = [];

    /// <summary>Gets the IANA time zone the days and times are read in, such as <c>Asia/Tashkent</c>.</summary>
    public required string TimeZone { get; init; }
}
