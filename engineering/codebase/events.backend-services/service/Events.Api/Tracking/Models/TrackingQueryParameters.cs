using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Events.Api.Shared;

namespace Events.Api.Tracking.Models;

/// <summary>Holds the filters of the tracking list, as the query names them (<see cref="TrackingQuery"/>'s properties in
/// camelCase).</summary>
public sealed class TrackingQueryParameters
{
    /// <summary>Gets or sets whether only tracked events list.</summary>
    public bool? TrackedOnly { get; set; }

    /// <summary>Gets or sets the stage's key.</summary>
    public string? Stage { get; set; }

    /// <summary>Gets or sets the priority's wire name, such as <c>high</c>.</summary>
    public string? Priority { get; set; }

    /// <summary>Gets or sets the first day, <c>yyyy-MM-dd</c>.</summary>
    public string? From { get; set; }

    /// <summary>Gets or sets the last day, <c>yyyy-MM-dd</c>.</summary>
    public string? To { get; set; }

    /// <summary>Gets or sets the kind's key.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the country code.</summary>
    public string? Country { get; set; }

    /// <summary>Gets or sets the free text.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the day a deadline falls before, <c>yyyy-MM-dd</c>.</summary>
    public string? DeadlineBefore { get; set; }

    /// <summary>Gets or sets whether undated events list; true when absent.</summary>
    public bool? IncludeUndated { get; set; }

    /// <summary>Gets or sets the page's cursor.</summary>
    public string? Cursor { get; set; }

    /// <summary>Gets or sets the page's size.</summary>
    public int? Limit { get; set; }

    /// <summary>Maps the parameters onto the broker's query.</summary>
    /// <param name="query">The query; unset when the parameters are malformed.</param>
    /// <returns>The validation error; none when the parameters map.</returns>
    public AppError? TryMap(out TrackingQuery query)
    {
        query = new TrackingQuery();
        AppError?[] errors =
        [
            WireQueryMapper.TryDay("from", From, out var from),
            WireQueryMapper.TryDay("to", To, out var to),
            WireQueryMapper.TryDay("deadlineBefore", DeadlineBefore, out var deadlineBefore),
            WireQueryMapper.TryEnum<TrackingPriority>("priority", Priority, out var priority),
        ];
        if (errors.FirstOrDefault(error => error is not null) is { } invalid)
            return invalid;

        query = new TrackingQuery
        {
            TrackedOnly = TrackedOnly ?? false,
            Stage = Stage,
            Priority = priority,
            From = from,
            To = to,
            Kind = Kind,
            Country = Country,
            Text = Text,
            DeadlineBefore = deadlineBefore,
            IncludeUndated = IncludeUndated ?? true,
            Cursor = Cursor,
            Limit = Limit ?? EventsConstants.DefaultPageSize,
        };
        return null;
    }
}
