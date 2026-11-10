using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Xunit.Sdk;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The base of every Events conformance suite: who calls, and the steps the cases share.</summary>
/// <remarks>Derive a suite per implementation — the module, the client against a host — and supply
/// <see cref="As"/>. Each case makes its own organizations and catalog slugs, so cases sharing a store never see each
/// other's rows and no reset runs between them.</remarks>
public abstract class EventsConformance
{
    /// <summary>Returns the brokers as <paramref name="actor"/> reaches them: the module switches its test principal, the
    /// client sends a token for that organization and person.</summary>
    /// <param name="actor">Who calls.</param>
    /// <returns>The brokers.</returns>
    protected abstract EventsBrokerSet As(EventsActor actor);

    /// <summary>Returns a person of a fresh organization, or of <paramref name="organizationId"/>.</summary>
    /// <param name="organizationId">The organization; a new one when none.</param>
    /// <returns>The actor.</returns>
    protected static EventsActor NewMember(string? organizationId = null) =>
        EventsActor.Member(organizationId ?? EventsActor.NewOrganization());

    /// <summary>Returns a fresh catalog slug.</summary>
    /// <param name="prefix">The slug's start, at most 30 characters of <c>[a-z0-9-]</c>.</param>
    /// <returns>The slug.</returns>
    protected static string NewSlug(string prefix = "conformance") => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Creates a catalog the actor's organization owns.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="visibility">Who reads it.</param>
    /// <returns>The catalog.</returns>
    protected async Task<CatalogModel> CreateCatalogAsync(EventsActor owner, CatalogVisibility visibility = CatalogVisibility.Private) =>
        Value(await As(owner).Catalogs.PutAsync(NewSlug(), new CatalogSpec { Name = "Conformance catalog", Visibility = visibility }));

    /// <summary>Creates a published event by hand, as a curator.</summary>
    /// <param name="owner">The catalog's owner.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="facts">The facts; <see cref="Facts"/> with a fresh title when none.</param>
    /// <returns>The event.</returns>
    protected async Task<EventModel> CreateEventAsync(EventsActor owner, string catalog, EventFacts? facts = null) =>
        Value(await As(owner).Events.CreateAsync(catalog, new EventSpec { Facts = facts ?? Facts() }));

    /// <summary>Proposes a candidate, as a member.</summary>
    /// <param name="owner">The catalog's owner.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="facts">The facts; <see cref="Facts"/> with a fresh title when none.</param>
    /// <returns>The candidate.</returns>
    protected async Task<EventModel> ProposeEventAsync(EventsActor owner, string catalog, EventFacts? facts = null) =>
        Value(await As(owner).Events.ProposeAsync(catalog, new EventSpec { Facts = facts ?? Facts() }));

    /// <summary>Returns valid facts: a title in an unknown language and, when given, a start.</summary>
    /// <param name="title">The title; a fresh one when none.</param>
    /// <param name="starts">The start's value, such as <c>2026-12-01</c> or <c>2026-12</c>.</param>
    /// <param name="precision">The start's precision.</param>
    /// <param name="key">The event's key.</param>
    /// <returns>The facts.</returns>
    protected static EventFacts Facts(string? title = null, string? starts = null, DatePrecision precision = DatePrecision.Day, string? key = null) => new()
    {
        Key = key,
        Title = new Dictionary<string, string> { [EventsConstants.UnknownLocale] = title ?? $"Conformance event {Guid.NewGuid():N}"[..40] },
        Starts = starts is null ? new EventWhen() : new EventWhen { Value = starts, Precision = precision },
    };

    /// <summary>Returns a result's value, failing the case with the error otherwise.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The value.</returns>
    protected static T Value<T>(Result<T> result) where T : notnull => result switch
    {
        Result<T>.Success success => success.Value,
        Result<T>.Failure failure => throw new XunitException($"Expected success, got {failure.Error.Type}: {failure.Error.Message}"),
        _ => throw new XunitException("Expected success."),
    };

    /// <summary>Returns a result's error, failing the case when it succeeded.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The error.</returns>
    protected static AppError Error<T>(Result<T> result) where T : notnull => result switch
    {
        Result<T>.Failure failure => failure.Error,
        _ => throw new XunitException("Expected a failure, got success."),
    };
}
