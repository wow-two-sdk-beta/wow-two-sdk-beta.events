using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Catalogs;

/// <summary>Seeds catalogs and builds facts for the module's tests.</summary>
internal static class TestCatalogs
{
    /// <summary>Creates a catalog for the current principal's organization.</summary>
    public static async Task<CatalogModel> CreateAsync(IServiceProvider services, string slug, CatalogVisibility visibility = CatalogVisibility.Private, params string[] kinds)
    {
        await using var scope = services.CreateAsyncScope();
        var catalogs = scope.ServiceProvider.GetRequiredService<IEventCatalogsBroker>();
        return (await catalogs.PutAsync(slug, new CatalogSpec
        {
            Name = "Test catalog",
            Visibility = visibility,
            Locales = ["uz", "ru"],
            Kinds = kinds.Select(kind => new CatalogKindModel { Key = kind }).ToList(),
        })).ValueOrThrow();
    }

    /// <summary>Builds facts with a title and, optionally, a start.</summary>
    public static EventFacts Facts(string title, string? starts = null, DatePrecision precision = DatePrecision.Day, string kind = "runway") => new()
    {
        Title = new Dictionary<string, string> { ["und"] = title },
        Kind = kind,
        Starts = starts is null ? new EventWhen() : new EventWhen { Value = starts, Precision = precision },
    };
}
