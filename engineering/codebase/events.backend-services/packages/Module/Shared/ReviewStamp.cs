using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Records who reviewed an event and why (events-integration #53).</summary>
/// <remarks>A person's stamp takes the event away from its source's automatic review: a later revision then waits as a
/// pending change instead of applying itself.</remarks>
internal static class ReviewStamp
{
    /// <summary>Holds the actor prefix of a person's review.</summary>
    public const string PersonActor = "person:";

    /// <summary>Stamps a person's review.</summary>
    /// <param name="row">The event.</param>
    /// <param name="caller">The person.</param>
    /// <param name="reason">What they did, such as <c>accepted</c>.</param>
    /// <param name="now">When.</param>
    public static void ByPerson(CatalogEventEntity row, EventsCaller caller, string reason, DateTimeOffset now)
    {
        var subject = caller.Subject ?? "unknown";
        var actor = PersonActor + subject;
        row.ReviewedBy = actor.Length > 256 ? actor[..256] : actor;
        row.ReviewReason = reason;
        row.ReviewedAt = now;
    }
}
