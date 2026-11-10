namespace WoW.Two.Sdk.Beta.Be.Events.Calendar;

/// <summary>Represents an exported iCalendar (RFC 5545) file.</summary>
/// <param name="FileName">The file's name, such as <c>ocharo-fashion.ics</c>.</param>
/// <param name="Content">The file's text.</param>
public sealed record CalendarFile(string FileName, string Content);
