using System.Globalization;
using System.Net;

namespace Events.Api.IntakeCheck;

/// <summary>Formats the lines <c>intake check</c> prints: the source's name, the check, statuses and counts, never a token,
/// a header, a body, a URL or a host name.</summary>
public static class IntakeCheckLine
{
    /// <summary>Holds the check that pulls the source's own subscription.</summary>
    public const string Pull = "pull";

    /// <summary>Holds the check that reads the source's subscription: its filter, schema and state must be what the
    /// source expects.</summary>
    public const string Subscription = "subscription";

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

    /// <summary>Formats the subscription check's line: 200 with nothing different passes; a difference names itself,
    /// <c>filter-mismatch</c>, <c>schema-mismatch</c> or <c>state-{state}</c>, never the values.</summary>
    /// <param name="source">The source's name.</param>
    /// <param name="status">The answer's status; none when no answer came.</param>
    /// <param name="problem">The difference, or why no answer came; none when the subscription matches.</param>
    /// <returns>The line.</returns>
    public static string SubscriptionCheck(string source, HttpStatusCode? status, string? problem)
    {
        var line = $"intake-check source={source} check={Subscription} status={Code(status)} expected=200 ok={Bool(status == HttpStatusCode.OK && problem is null)}";
        return problem is null ? line : line + $" reason={problem}";
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

    /// <summary>Writes the last line with every source's statuses, since a runner may keep only the last line:
    /// <c>intake-check-result ok=false source=… token=… pull=… records=… event-records=… subscription=… tenants=… foreign=…</c>,
    /// sources joined by <c> | </c>.</summary>
    /// <param name="ok">Whether every check matched.</param>
    /// <param name="summaries">One <see cref="Summary"/> per source.</param>
    public static string Result(bool ok, IReadOnlyList<string> summaries) =>
        summaries.Count == 0 ? Result(ok) : $"intake-check-result ok={Bool(ok)} {string.Join(" | ", summaries)}";

    /// <summary>Writes one source's statuses for the last line: statuses, counts and error kinds only.</summary>
    /// <param name="source">The source's name.</param>
    /// <param name="token">The token's outcome: <c>ok</c>, <c>token-refused:{oauth error}</c>, <c>unreachable</c>,
    /// <c>timeout</c> or <c>not-configured</c>.</param>
    /// <param name="pull">The pull's status.</param>
    /// <param name="records">The pull's records.</param>
    /// <param name="eventRecords">The pull's <c>wow2.event.v1</c> records.</param>
    /// <param name="subscription">The subscription check: <c>ok</c>, its difference, or its status when it answered no 200.</param>
    /// <param name="tenants">The tenants check's status.</param>
    /// <param name="foreign">The foreign check's status.</param>
    /// <param name="foreignAsked">Whether a foreign subscription was given.</param>
    public static string Summary(string source, string token, HttpStatusCode? pull, int? records, int? eventRecords, string subscription, HttpStatusCode? tenants, HttpStatusCode? foreign, bool foreignAsked) =>
        $"source={source} token={token} pull={Code(pull)} records={Count(records)} event-records={Count(eventRecords)} subscription={subscription} tenants={Code(tenants)} foreign={(foreignAsked ? Code(foreign) : "skipped")}";

    /// <summary>Writes the subscription check's value for the last line: <c>ok</c>, its difference, or its status.</summary>
    /// <param name="status">The answer's status.</param>
    /// <param name="problem">The difference or why no answer came.</param>
    public static string SubscriptionSummary(HttpStatusCode? status, string? problem) =>
        status == HttpStatusCode.OK ? problem ?? "ok" : problem ?? Code(status);

    /// <summary>Keeps an OAuth error code to its safe characters (<c>invalid_target</c>), or <c>unknown</c>.</summary>
    /// <param name="error">The token endpoint's <c>error</c>.</param>
    public static string Code(string? error)
    {
        var safe = new string((error ?? string.Empty).Where(c => char.IsAsciiLetterLower(c) || c == '_').Take(40).ToArray());
        return safe.Length == 0 ? "unknown" : safe;
    }

    private static string Count(int? value) => (value ?? 0).ToString(CultureInfo.InvariantCulture);

    private static string Code(HttpStatusCode? status) => status is { } code ? ((int)code).ToString(CultureInfo.InvariantCulture) : "0";

    private static string Bool(bool value) => value ? "true" : "false";
}
