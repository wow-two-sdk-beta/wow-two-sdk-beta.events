using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Renders a digest as of an instant: selects its catalog's published events by its rules and filters on the
/// instant's local day and its organization's tracking stages, resolves its recipients and renders one message per
/// recipient language. Writes nothing.</summary>
/// <param name="db">The module's store.</param>
/// <param name="resolver">Reads a role's members.</param>
/// <param name="options">The digests' settings: the host's link templates.</param>
internal sealed class DigestDrafter(EventsDbContext db, IDigestRecipientResolver resolver, IOptions<EventDigestsOptions> options)
{
    private static readonly string Published = EventsLabels.Of(EventReviewState.Published);

    /// <summary>Renders a digest.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="catalog">Its catalog.</param>
    /// <param name="asOf">The instant: a run's occurrence or start, a preview's now.</param>
    /// <param name="before">Counts the previous run among runs started before this instant.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The draft.</returns>
    public async Task<DigestDraft> DraftAsync(DigestEntity digest, CatalogEntity catalog, DateTimeOffset asOf, DateTimeOffset before, CancellationToken cancellationToken)
    {
        DigestOccurrences.TryFindZone(digest.TimeZone, out var zone);
        var day = DigestOccurrences.LocalDay(asOf, zone);
        var selection = DigestRows.Selection(digest);
        var notes = new List<string>();

        // "New since the last run" counts from the previous run that sent or found nothing, else from the digest's creation.
        var previous = await db.DigestRuns.AsNoTracking()
            .Where(run => run.DigestId == digest.Id && run.StartedAt < before && DigestRows.CountedStatuses.Contains(run.Status))
            .OrderByDescending(run => run.StartedAt)
            .Select(run => new { run.StartedAt, run.EventIds })
            .FirstOrDefaultAsync(cancellationToken);
        var since = previous?.StartedAt ?? digest.CreatedAt;
        var sentBefore = previous is null ? [] : EventsJson.Read<List<Guid>>(previous.EventIds);

        var readable = CatalogAccess.CanRead(catalog, new EventsCaller(PrincipalKind.Person, digest.Issuer, digest.OrganizationId, null));
        if (!readable)
            notes.Add("The digest's organization can no longer read the catalog; nothing matches.");

        // Stages read against the organization's workflow: an untracked event stands in its default stage.
        var defaultStage = selection.Stages.Count > 0 || selection.ExcludeStages.Count > 0
            ? (await WorkflowStore.LoadAsync(db, new EventsCaller(PrincipalKind.Person, digest.Issuer, digest.OrganizationId, null), cancellationToken)).DefaultStage
            : string.Empty;

        var query = Select(digest, catalog, selection, defaultStage, day, since, sentBefore, readable);
        var matched = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(row => row.StartsMin == null)
            .ThenBy(row => row.StartsMin)
            .ThenBy(row => row.TitleSort)
            .ThenBy(row => row.Id)
            .Take(digest.ItemCap)
            .ToListAsync(cancellationToken);

        var template = digest.LinkTemplate
            ?? (options.Value.LinkTemplates.GetValueOrDefault(catalog.Slug) is { Length: > 0 } fallback && !string.IsNullOrWhiteSpace(fallback) ? fallback.Trim() : null);
        var items = rows.Select(row => Item(row, catalog.Slug, day, since, sentBefore, template)).ToList();

        var recipients = await ResolveAsync(digest, notes, cancellationToken);
        var languages = recipients.Count == 0 ? [digest.Language] : recipients.Select(recipient => recipient.Language).Distinct(StringComparer.Ordinal).ToList();
        var texts = languages
            .Select(language => new DigestTextModel
            {
                Language = language,
                Text = DigestText.Render(digest.Name, day, items, matched, language),
                Recipients = recipients.Count(recipient => recipient.Language == language),
            })
            .ToList();

        return new DigestDraft(asOf, matched, items, texts, recipients, notes);
    }

    private IQueryable<CatalogEventEntity> Select(
        DigestEntity digest, CatalogEntity catalog, DigestSelection selection, string defaultStage, DateOnly day, DateTimeOffset since, List<Guid> sentBefore, bool readable)
    {
        var query = db.Events.AsNoTracking()
            .Where(row => readable && row.CatalogId == catalog.Id && row.Review == Published)
            .Where(row => row.EndsMax == null || row.EndsMax >= day);

        if (selection.Statuses.Count > 0)
        {
            var statuses = selection.Statuses.Select(EventsLabels.Of).ToList();
            query = query.Where(row => statuses.Contains(row.Status));
        }

        if (selection.Kinds.Count > 0)
        {
            var kinds = selection.Kinds.ToList();
            query = query.Where(row => kinds.Contains(row.Kind));
        }

        if (selection.Cities.Count > 0)
        {
            var cities = selection.Cities.Select(city => city.ToLowerInvariant()).ToList();
            query = query.Where(row => row.City != null && cities.Contains(row.City));
        }

        if (selection.TrackedOnly)
        {
            var issuer = digest.Issuer;
            var organization = digest.OrganizationId;
            query = query.Where(row => db.Tracking.Any(tracking => tracking.Issuer == issuer && tracking.OrganizationId == organization && tracking.EventId == row.Id));
        }

        if (selection.Stages.Count > 0)
        {
            var issuer = digest.Issuer;
            var organization = digest.OrganizationId;
            var stages = selection.Stages.ToList();
            var untracked = stages.Contains(defaultStage);
            query = query.Where(row =>
                db.Tracking.Any(tracking => tracking.Issuer == issuer && tracking.OrganizationId == organization && tracking.EventId == row.Id && stages.Contains(tracking.Stage))
                || (untracked && !db.Tracking.Any(tracking => tracking.Issuer == issuer && tracking.OrganizationId == organization && tracking.EventId == row.Id)));
        }

        if (selection.ExcludeStages.Count > 0)
        {
            var issuer = digest.Issuer;
            var organization = digest.OrganizationId;
            var excluded = selection.ExcludeStages.ToList();
            var untracked = excluded.Contains(defaultStage);
            query = query.Where(row =>
                !db.Tracking.Any(tracking => tracking.Issuer == issuer && tracking.OrganizationId == organization && tracking.EventId == row.Id && excluded.Contains(tracking.Stage))
                && (!untracked || db.Tracking.Any(tracking => tracking.Issuer == issuer && tracking.OrganizationId == organization && tracking.EventId == row.Id)));
        }

        var isNew = selection.NewSinceLastRun;
        var starting = selection.StartingWithinDays is not null;
        var startingEnd = day.AddDays(selection.StartingWithinDays ?? 0);
        var deadline = selection.DeadlineWithinDays is not null;
        var deadlineEnd = day.AddDays(selection.DeadlineWithinDays ?? 0);
        if (!isNew && !starting && !deadline)
            return query.Where(row => row.StartsMin != null);

        return query.Where(row =>
            (isNew && row.PublishedAt > since && !sentBefore.Contains(row.Id))
            || (starting && row.StartsMin != null && row.StartsMax >= day && row.StartsMin <= startingEnd)
            || (deadline && db.Deadlines.Any(item => item.EventId == row.Id && item.AtMin != null && item.AtMax >= day && item.AtMin <= deadlineEnd)));
    }

    private static DigestItemModel Item(CatalogEventEntity row, string catalog, DateOnly day, DateTimeOffset since, List<Guid> sentBefore, string? template)
    {
        var facts = EventsJson.Read<EventFacts>(row.Facts);
        var next = facts.Deadlines
            .Select(deadline => (Deadline: deadline, Span: DateSpan.Of(deadline.At)))
            .Where(pair => pair.Span is { } span && span.Max >= day)
            .OrderBy(pair => pair.Span!.Value.Min)
            .Select(pair => pair.Deadline)
            .FirstOrDefault();

        return new DigestItemModel
        {
            EventId = row.Id,
            Key = facts.Key,
            Title = facts.Title,
            Starts = facts.Starts,
            Ends = facts.Ends,
            City = facts.Place?.City,
            Status = facts.Status,
            NextDeadline = next,
            Url = template is null ? null : DigestLinks.Fill(template, row.Id, facts.Key, catalog),
            IsNew = row.PublishedAt > since && !sentBefore.Contains(row.Id),
        };
    }

    private async Task<List<(string Subject, string Language)>> ResolveAsync(DigestEntity digest, List<string> notes, CancellationToken cancellationToken)
    {
        var recipients = new List<(string Subject, string Language)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipient in DigestRows.Recipients(digest))
        {
            var language = recipient.Language ?? digest.Language;
            if (recipient.Subject is { } subject)
            {
                if (seen.Add(subject))
                    recipients.Add((subject, language));
                continue;
            }

            var members = await resolver.ResolveRoleAsync(digest.Issuer, digest.OrganizationId, recipient.Role!, cancellationToken);
            if (members is Result<IReadOnlyList<string>>.Failure { Error: var error })
            {
                notes.Add($"Role '{recipient.Role}' could not be read: {error.Message}");
                continue;
            }

            foreach (var member in ((Result<IReadOnlyList<string>>.Success)members).Value)
            {
                if (!string.IsNullOrWhiteSpace(member) && seen.Add(member))
                    recipients.Add((member, language));
            }
        }

        return recipients;
    }
}
