using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;

namespace Events.Tests.Integration.Tracking;

/// <summary>Seeds catalogs and events straight into the module's store, so these tiers never depend on the catalog brokers.</summary>
internal static class CatalogSeed
{
    /// <summary>Inserts a catalog owned by an organization at the test issuer.</summary>
    public static async Task<CatalogEntity> CatalogAsync(
        ModuleFixture fixture, string organizationId, CatalogVisibility visibility = CatalogVisibility.Private, string name = "Fashion events", params string[] kinds)
    {
        var now = fixture.Clock.GetUtcNow();
        var row = new CatalogEntity
        {
            Id = Guid.CreateVersion7(now),
            Slug = ModuleFixture.NewSlug(),
            Name = name,
            OwnerIssuer = TestPrincipal.DefaultIssuer,
            OwnerOrganizationId = organizationId,
            Visibility = EventsLabels.Of(visibility),
            Locales = "[\"en\"]",
            Kinds = EventsJson.Write(kinds.Select(kind => new CatalogKindModel { Key = kind }).ToList()),
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Catalogs.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    /// <summary>Inserts an event with its filter columns and deadline rows, written as the module writes them.</summary>
    public static async Task<CatalogEventEntity> EventAsync(
        ModuleFixture fixture, CatalogEntity catalog, EventFacts facts, EventReviewState review = EventReviewState.Published)
    {
        EventFactsValidator.Validate(facts, [], out var normalized).Should().BeNull();
        var now = fixture.Clock.GetUtcNow();
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

        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Events.Add(row);
        await db.SaveChangesAsync();
        db.Deadlines.AddRange(deadlines);
        await db.SaveChangesAsync();
        return row;
    }

    /// <summary>Builds facts with a title and, optionally, a start and deadlines.</summary>
    public static EventFacts Facts(
        string title, string? starts = null, DatePrecision precision = DatePrecision.Day, string? key = null, params EventDeadline[] deadlines) => new()
    {
        Key = key,
        Title = new Dictionary<string, string> { ["en"] = title },
        Starts = starts is null ? new EventWhen() : new EventWhen { Value = starts, Precision = precision },
        Deadlines = deadlines,
    };

    /// <summary>Builds a day deadline.</summary>
    public static EventDeadline Deadline(string day, DeadlineKind kind = DeadlineKind.Application) =>
        new() { Kind = kind, At = new EventWhen { Value = day, Precision = DatePrecision.Day } };
}
