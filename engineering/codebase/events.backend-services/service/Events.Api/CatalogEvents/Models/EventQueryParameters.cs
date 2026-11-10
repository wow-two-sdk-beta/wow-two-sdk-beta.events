using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Events.Api.Shared;

namespace Events.Api.CatalogEvents.Models;

/// <summary>Holds the event filters of the events and calendar routes, as the query names them (<see cref="EventQuery"/>'s
/// properties in camelCase).</summary>
public sealed class EventQueryParameters
{
    /// <summary>Gets or sets the first day, <c>yyyy-MM-dd</c>.</summary>
    public string? From { get; set; }

    /// <summary>Gets or sets the last day, <c>yyyy-MM-dd</c>.</summary>
    public string? To { get; set; }

    /// <summary>Gets or sets the kind's key.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the country code.</summary>
    public string? Country { get; set; }

    /// <summary>Gets or sets the city.</summary>
    public string? City { get; set; }

    /// <summary>Gets or sets the free text.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the status's wire name, such as <c>movedOnline</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the review state's wire name, such as <c>candidate</c>.</summary>
    public string? Review { get; set; }

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
    public AppError? TryMap(out EventQuery query)
    {
        query = new EventQuery();
        AppError?[] errors =
        [
            WireQueryMapper.TryDay("from", From, out var from),
            WireQueryMapper.TryDay("to", To, out var to),
            WireQueryMapper.TryDay("deadlineBefore", DeadlineBefore, out var deadlineBefore),
            WireQueryMapper.TryEnum<EventStatus>("status", Status, out var status),
            WireQueryMapper.TryEnum<EventReviewState>("review", Review, out var review),
        ];
        if (errors.FirstOrDefault(error => error is not null) is { } invalid)
            return invalid;

        query = new EventQuery
        {
            From = from,
            To = to,
            Kind = Kind,
            Country = Country,
            City = City,
            Text = Text,
            Status = status,
            Review = review,
            DeadlineBefore = deadlineBefore,
            IncludeUndated = IncludeUndated ?? true,
            Cursor = Cursor,
            Limit = Limit ?? EventsConstants.DefaultPageSize,
        };
        return null;
    }
}
