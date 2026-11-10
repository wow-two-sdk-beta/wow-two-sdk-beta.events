using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Events.Api.Shared;

namespace Events.Api.Review.Models;

/// <summary>Holds the filters of the observations list, as the query names them (<see cref="ObservationQuery"/>'s
/// properties in camelCase).</summary>
public sealed class ObservationQueryParameters
{
    /// <summary>Gets or sets the state's wire name, such as <c>pending</c>.</summary>
    public string? State { get; set; }

    /// <summary>Gets or sets the event the observations concern.</summary>
    public Guid? EventId { get; set; }

    /// <summary>Gets or sets the page's cursor.</summary>
    public string? Cursor { get; set; }

    /// <summary>Gets or sets the page's size.</summary>
    public int? Limit { get; set; }

    /// <summary>Maps the parameters onto the broker's query.</summary>
    /// <param name="query">The query; unset when the parameters are malformed.</param>
    /// <returns>The validation error; none when the parameters map.</returns>
    public AppError? TryMap(out ObservationQuery query)
    {
        query = new ObservationQuery();
        if (WireQueryMapper.TryEnum<ObservationState>("state", State, out var state) is { } error)
            return error;

        query = new ObservationQuery { State = state, EventId = EventId, Cursor = Cursor, Limit = Limit ?? EventsConstants.DefaultPageSize };
        return null;
    }
}
