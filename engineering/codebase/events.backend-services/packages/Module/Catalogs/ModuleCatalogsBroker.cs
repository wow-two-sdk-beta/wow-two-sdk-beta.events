using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Catalogs;

/// <summary>Integrates catalogs in process, over the module's store: reads by the record rules of
/// <see cref="CatalogAccess"/>, writes by the owner organization only.</summary>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed partial class ModuleCatalogsBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : IEventCatalogsBroker
{
    private static readonly string Private = EventsLabels.Of(CatalogVisibility.Private);
    private static readonly string Public = EventsLabels.Of(CatalogVisibility.Public);
    private static readonly string Scfd = EventsLabels.Of(Events.CatalogEvents.EventSourceKind.Scfd);

    /// <inheritdoc />
    public async Task<Result<CatalogModel>> GetAsync(string slug, CancellationToken cancellationToken = default)
    {
        var found = await CatalogAccess.FindReadableAsync(db, callers.Current, slug ?? string.Empty, cancellationToken);
        return found.Map(ToModel);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CatalogModel>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (caller.Kind == PrincipalKind.Anonymous)
            return Result<IReadOnlyList<CatalogModel>>.Ok([]);

        var issuer = caller.Issuer;
        var organization = caller.OrganizationId ?? string.Empty;
        var owned = caller.HasOrganization;
        var rows = await db.Catalogs.AsNoTracking()
            .Where(row => row.Visibility == Public || (owned && row.OwnerIssuer == issuer && row.OwnerOrganizationId == organization))
            .OrderBy(row => row.Slug)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CatalogModel>>.Ok(rows.Where(row => CatalogAccess.CanRead(row, caller)).Select(ToModel).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<CatalogModel>> PutAsync(string slug, CatalogSpec spec, CancellationToken cancellationToken = default)
    {
        if (Validate(slug, spec, out var normalized) is { } invalid)
            return Result<CatalogModel>.Fail(invalid);

        var caller = callers.Current;
        var now = time.GetUtcNow();
        var row = await db.Catalogs.FirstOrDefaultAsync(catalog => catalog.Slug == slug, cancellationToken);
        if (row is null)
        {
            if (!caller.HasOrganization)
                return Result<CatalogModel>.Fail(EventsErrors.OrganizationRequired());

            row = new CatalogEntity
            {
                Id = Guid.CreateVersion7(now),
                Slug = slug,
                OwnerIssuer = caller.Issuer,
                OwnerOrganizationId = caller.OrganizationId!,
                Revision = 1,
                CreatedAt = now,
            };
            Apply(row, normalized, now);
            db.Catalogs.Add(row);
        }
        else
        {
            if (!CatalogAccess.CanRead(row, caller))
                return Result<CatalogModel>.Fail(EventsErrors.CatalogNotFound());
            if (!CatalogAccess.IsOwner(row, caller))
                return Result<CatalogModel>.Fail(EventsErrors.NotCatalogOwner());
            if (spec.ExpectedRevision is not { } expected)
                return Result<CatalogModel>.Fail(EventsErrors.Invalid("expectedRevision", "required to replace a catalog"));
            if (expected != row.Revision)
                return Result<CatalogModel>.Fail(EventsErrors.RevisionConflict());

            // Events is for internal tracking: a catalog holding sourced records never opens up (events-integration #50).
            var visibility = EventsLabels.Of(normalized.Visibility);
            if (visibility != Private && await HoldsSourcedRecordsAsync(row.Id, cancellationToken))
                return Result<CatalogModel>.Fail(EventsErrors.Invalid("visibility", "a catalog holding sourced records stays private"));

            Apply(row, normalized, now);
            row.Revision++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CatalogModel>.Fail(EventsErrors.RevisionConflict());
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Result<CatalogModel>.Fail(EventsErrors.RevisionConflict());
        }

        return Result<CatalogModel>.Ok(ToModel(row));
    }

    private async Task<bool> HoldsSourcedRecordsAsync(Guid catalogId, CancellationToken cancellationToken) =>
        await db.Sources.AnyAsync(source => source.CatalogId == catalogId && source.Kind == Scfd, cancellationToken)
        || await db.Observations.AnyAsync(observation => observation.CatalogId == catalogId && observation.SourceKind == Scfd, cancellationToken);

    private static void Apply(CatalogEntity row, CatalogSpec spec, DateTimeOffset now)
    {
        row.Name = spec.Name;
        row.Visibility = EventsLabels.Of(spec.Visibility);
        row.Locales = EventsJson.Write(spec.Locales);
        row.Kinds = EventsJson.Write(spec.Kinds);
        row.UpdatedAt = now;
    }

    private static AppError? Validate(string slug, CatalogSpec spec, out CatalogSpec normalized)
    {
        normalized = spec;
        if (string.IsNullOrEmpty(slug) || !EventFactsValidator.KeyPattern().IsMatch(slug))
            return EventsErrors.Invalid("slug", EventsConstants.KeyPattern);
        if (spec is null)
            return EventsErrors.Invalid("spec", "required");

        var name = spec.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > EventsConstants.MaxNameLength)
            return EventsErrors.Invalid("name", $"1 to {EventsConstants.MaxNameLength} characters");
        if (!Enum.IsDefined(spec.Visibility))
            return EventsErrors.Invalid("visibility", "private, shared or public");

        var locales = (spec.Locales ?? []).Select(locale => locale?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();
        if (locales.Count > EventsConstants.MaxListLength || locales.Any(locale => !LocalePattern().IsMatch(locale)))
            return EventsErrors.Invalid("locales", "BCP 47 tags such as uz, ru or en");

        var kinds = new List<CatalogKindModel>();
        foreach (var kind in spec.Kinds ?? [])
        {
            var key = kind?.Key?.Trim() ?? string.Empty;
            if (!EventFactsValidator.KeyPattern().IsMatch(key))
                return EventsErrors.Invalid("kinds", $"each key matches {EventsConstants.KeyPattern}");
            if (kinds.Any(existing => existing.Key == key))
                return EventsErrors.Invalid("kinds", $"'{key}' appears twice");

            var label = (kind!.Label ?? new Dictionary<string, string>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.Ordinal);
            if (label.Any(pair => !LocalePattern().IsMatch(pair.Key) || pair.Value.Length > EventsConstants.MaxNameLength))
                return EventsErrors.Invalid("kinds", $"labels are locale → text of at most {EventsConstants.MaxNameLength} characters");
            kinds.Add(new CatalogKindModel { Key = key, Label = label });
        }

        if (kinds.Count > EventsConstants.MaxListLength)
            return EventsErrors.Invalid("kinds", $"at most {EventsConstants.MaxListLength}");

        normalized = spec with { Name = name, Locales = locales, Kinds = kinds };
        return null;
    }

    private static CatalogModel ToModel(CatalogEntity row) => new()
    {
        Slug = row.Slug,
        Name = row.Name,
        OwnerIssuer = row.OwnerIssuer,
        OwnerOrganizationId = row.OwnerOrganizationId,
        Visibility = EventsLabels.Parse<CatalogVisibility>(row.Visibility),
        Locales = EventsJson.Read<List<string>>(row.Locales),
        Kinds = EventsJson.Read<List<CatalogKindModel>>(row.Kinds),
        Revision = row.Revision,
        CreatedAtUtc = row.CreatedAt,
        UpdatedAtUtc = row.UpdatedAt,
    };

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LocalePattern();
}
