using System.Globalization;
using System.Net;

namespace Events.Api.IntakeCheck;

/// <summary>Formats the lines <c>intake check</c> prints: the source's name, the check, statuses and counts, never a token,
/// a header, a body, a URL or a host name.</summary>
public static class IntakeCheckLine
{
    /// <summary>Holds the check that pulls the source's own subscription.</summary>
    public const string Pull = "pull";

    /// <summary>Holds the check that asks for the tenants list, which the token must not reach.</summary>
    public const string Tenants = "tenants";

    /// <summary>Holds the check that asks for another tenant's subscription, which must not exist for the token.</summary>
    public const string Foreign = "foreign";

    /// <summary>Formats one check's line.</summary>
    /// <param name="source">The source's name.</param>
    /// <param name="check">The check: <see cref="Pull"/>, <see cref="Tenants"/> or <see cref="Foreign"/>.</param>
    /// <param name="status">The answer's status; none when no answer came.</param>
    /// <param name="expected">The status the check expects.</param>
    /// <param name="records">The pull's record count; the pull only.</param>
    /// <param name="eventRecords">The pull's <c>wow2.event.v1</c> record count; the pull only.</param>
    /// <param name="reason">Why no answer came, such as <c>unreachable</c>; none otherwise.</param>
    /// <returns>The line.</returns>
    public static string Check(string source, string check, HttpStatusCode? status, HttpStatusCode expected, int? records = null, int? eventRecords = null, string? reason = null)
    {
        var line = $"intake-check source={source} check={check} status={Code(status)} expected={(int)expected} ok={Bool(status == expected)}";
        if (records is { } count)
            line += $" records={count.ToString(CultureInfo.InvariantCulture)} event-records={(eventRecords ?? 0).ToString(CultureInfo.InvariantCulture)}";
        return reason is null ? line : line + $" reason={reason}";
    }

    /// <summary>Formats a source that could not be checked, such as one missing its settings.</summary>
    /// <param name="source">The source's name.</param>
    /// <param name="reason">Why: <c>not-configured</c> or <c>not-found</c>.</param>
    /// <returns>The line.</returns>
    public static string Skipped(string source, string reason) => $"intake-check source={source} ok=false reason={reason}";

    /// <summary>Formats the last line.</summary>
    /// <param name="ok">Whether every check matched.</param>
    /// <param name="reason">Why it failed before any check, such as <c>usage</c>; none otherwise.</param>
    /// <returns>The line.</returns>
    public static string Result(bool ok, string? reason = null) =>
        reason is null ? $"intake-check-result ok={Bool(ok)}" : $"intake-check-result ok={Bool(ok)} reason={reason}";

    private static string Code(HttpStatusCode? status) => status is { } code ? ((int)code).ToString(CultureInfo.InvariantCulture) : "0";

    private static string Bool(bool value) => value ? "true" : "false";
}
