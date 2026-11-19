using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace Events.Tests.Integration.Digests;

/// <summary>One host of the module over the digest fixture's database: its brokers act for <see cref="Principal"/>, its
/// scheduler turns on <see cref="Clock"/>. Disposing it is the host going down.</summary>
/// <param name="services">The host's container.</param>
/// <param name="principal">Who calls.</param>
/// <param name="clock">The host's clock.</param>
/// <param name="notices">Identity's fake app notices.</param>
public sealed class DigestHost(ServiceProvider services, TestPrincipal principal, FakeTimeProvider clock, FakeAppNoticeBroker notices) : IAsyncDisposable
{
    /// <summary>Gets who calls.</summary>
    public TestPrincipal Principal { get; } = principal;

    /// <summary>Gets the host's clock.</summary>
    public FakeTimeProvider Clock { get; } = clock;

    /// <summary>Gets Identity's fake app notices.</summary>
    public FakeAppNoticeBroker Notices { get; } = notices;

    /// <summary>Gets the host's container.</summary>
    public ServiceProvider Services { get; } = services;

    /// <summary>Calls the digests broker in a scope of its own.</summary>
    public async Task<T> DigestsAsync<T>(Func<IEventDigestsBroker, Task<T>> call)
    {
        await using var scope = Services.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventDigestsBroker>());
    }

    /// <summary>Runs one turn of the scheduler.</summary>
    /// <returns>How many runs it started.</returns>
    internal Task<int> TurnAsync() => Services.GetRequiredService<DigestSchedulerPass>().RunAsync(CancellationToken.None);

    /// <summary>Calls the store in a scope of its own.</summary>
    internal async Task<T> StoreAsync<T>(Func<EventsDbContext, Task<T>> call)
    {
        await using var scope = Services.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<EventsDbContext>());
    }

    /// <summary>Turns off every digest earlier tests left on the shared database, so this test's turns run its own only.</summary>
    internal Task DisableOtherDigestsAsync() =>
        StoreAsync(db => db.Digests.ExecuteUpdateAsync(set => set.SetProperty(row => row.Enabled, false)));

    /// <summary>Inserts a catalog the organization owns.</summary>
    internal async Task<CatalogEntity> CatalogAsync(string organizationId, string? slug = null)
    {
        var now = Clock.GetUtcNow();
        var row = new CatalogEntity
        {
            Id = Guid.CreateVersion7(now),
            Slug = slug ?? $"digests-{Guid.NewGuid():N}",
            Name = "Fashion events",
            OwnerIssuer = TestPrincipal.DefaultIssuer,
            OwnerOrganizationId = organizationId,
            Visibility = EventsLabels.Of(CatalogVisibility.Private),
            Locales = "[\"en\"]",
            Kinds = "[]",
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await StoreAsync(async db =>
        {
            db.Catalogs.Add(row);
            return await db.SaveChangesAsync();
        });
        return row;
    }

    /// <summary>Inserts an event as the module writes it, published now unless told otherwise.</summary>
    internal async Task<CatalogEventEntity> EventAsync(CatalogEntity catalog, EventFacts facts, EventReviewState review = EventReviewState.Published)
    {
        EventFactsValidator.Validate(facts, [], out var normalized).Should().BeNull();
        var now = Clock.GetUtcNow();
        var row = new CatalogEventEntity
        {
            Id = Guid.CreateVersion7(now),
            CatalogId = catalog.Id,
            Review = EventsLabels.Of(review),
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = review == EventReviewState.Published ? now : null,
        };
        var deadlines = EventRows.ApplyFacts(row, normalized);
        await StoreAsync(async db =>
        {
            db.Events.Add(row);
            await db.SaveChangesAsync();
            db.Deadlines.AddRange(deadlines);
            return await db.SaveChangesAsync();
        });
        return row;
    }

    /// <summary>Inserts the organization's tracking of an event, in <paramref name="stage"/>.</summary>
    internal Task TrackAsync(string organizationId, Guid eventId, string stage = "interested") => StoreAsync(async db =>
    {
        var now = Clock.GetUtcNow();
        db.Tracking.Add(new TrackingEntity
        {
            Id = Guid.CreateVersion7(now),
            Issuer = TestPrincipal.DefaultIssuer,
            OrganizationId = organizationId,
            EventId = eventId,
            Stage = stage,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        return await db.SaveChangesAsync();
    });

    /// <summary>Moves the organization's tracking of an event to <paramref name="stage"/>.</summary>
    internal Task RestageAsync(string organizationId, Guid eventId, string stage) =>
        StoreAsync(db => db.Tracking
            .Where(row => row.Issuer == TestPrincipal.DefaultIssuer && row.OrganizationId == organizationId && row.EventId == eventId)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.Stage, stage)));

    /// <summary>Writes the calling organization's workflow.</summary>
    public async Task<WorkflowModel> WorkflowAsync(WorkflowSpec spec)
    {
        await using var scope = Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>().PutAsync(spec)).ValueOrThrow();
    }

    /// <summary>Reads a digest's runs, oldest first.</summary>
    internal Task<List<DigestRunEntity>> RunsAsync(Guid digestId) =>
        StoreAsync(db => db.DigestRuns.AsNoTracking().Where(run => run.DigestId == digestId).OrderBy(run => run.StartedAt).ToListAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Services.DisposeAsync();
}
