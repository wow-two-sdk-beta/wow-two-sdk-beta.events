using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Validates the intake's settings: unique source names, a catalog slug, an SCFD URL and subscription, sane
/// paging, and a complete token client.</summary>
internal sealed partial class EventsIntakeOptionsValidator : IValidateOptions<EventsIntakeOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EventsIntakeOptions options)
    {
        var failures = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < options.Sources.Count; index++)
        {
            var source = options.Sources[index];
            var at = $"{EventsIntakeOptions.SectionName}:Sources:{index}";
            if (source is null)
            {
                failures.Add($"{at}: required");
                continue;
            }

            if (!NamePattern().IsMatch(source.Name ?? string.Empty))
                failures.Add($"{at}:Name must match ^[a-z0-9-]{{1,64}}$");
            else if (!names.Add(source.Name!))
                failures.Add($"{at}:Name '{source.Name}' is used twice");
            if (!EventFactsValidator.KeyPattern().IsMatch(source.Catalog ?? string.Empty))
                failures.Add($"{at}:Catalog must be a catalog slug");
            if (!IntakeSources.IsConfigured(source))
                continue; // declared, not yet typed: stays off
            if (string.IsNullOrWhiteSpace(source.Instance) || source.Instance.Length > 128)
                failures.Add($"{at}:Instance must be 1 to 128 characters");
            if (string.IsNullOrWhiteSpace(source.SubscriptionId) || IntakeSources.Subscription(source).Length > 64)
                failures.Add($"{at}:SubscriptionId must be 1 to 64 characters");
            if (source.PipelineId is { Length: > 64 })
                failures.Add($"{at}:PipelineId must be at most 64 characters");
            if (source.PageSize is < 1 or > 500)
                failures.Add($"{at}:PageSize must be 1 to 500");
            if (source.MaxPagesPerPoll < 1)
                failures.Add($"{at}:MaxPagesPerPoll must be at least 1");
            if (source.PollInterval <= TimeSpan.Zero)
                failures.Add($"{at}:PollInterval must be positive");
            if (source.Enabled && !IsHttpUrl(source.BaseUrl))
                failures.Add($"{at}:BaseUrl must be an absolute http(s) URL");
            if (!string.IsNullOrWhiteSpace(source.TokenEndpoint))
            {
                if (!IsHttpUrl(source.TokenEndpoint))
                    failures.Add($"{at}:TokenEndpoint must be an absolute http(s) URL");
                if (string.IsNullOrWhiteSpace(source.ClientId))
                    failures.Add($"{at}:ClientId is required with a TokenEndpoint");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex NamePattern();
}
