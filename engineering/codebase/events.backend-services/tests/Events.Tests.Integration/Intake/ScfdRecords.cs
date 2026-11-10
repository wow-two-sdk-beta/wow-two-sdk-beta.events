using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Integration.Intake;

/// <summary>Builds SCFD deliveries as the wire carries them: post records with a <c>wow2.event.v1</c> projection, and
/// lifecycle and run records.</summary>
internal static class ScfdRecords
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Gets when the deliveries happened.</summary>
    public static DateTimeOffset OccurredAt { get; } = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a post record with a well-formed event projection.</summary>
    public static ScfdDeliveryRecord Post(Guid postId, int revision, string title, string type = "post.new", Action<Dictionary<string, object?>>? edit = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["schema"] = "wow2.event.v1",
            ["title"] = title,
            ["description"] = "Sourced description text.",
            ["starts"] = new { value = "2026-12-05", precision = "day" },
            ["ends"] = null,
            ["organizer"] = new { name = "Mstar", url = "https://t.me/mstar" },
            ["place"] = new { mentions = new object[] { new { text = "Tashkent", kind = 0 }, new { text = "Humo Arena", kind = "venue" } } },
            ["offers"] = new object[] { new { amount = 150000m, currency = "UZS", free = false, role = "ticket" } },
            ["media"] = new object[] { new { kind = "image", url = "https://cdn.example/poster.jpg" } },
            ["language"] = new { code = "ru", script = "Cyrl" },
            ["postId"] = postId,
            ["revision"] = revision,
            ["state"] = "live",
            ["links"] = new object[]
            {
                new { id = Guid.NewGuid(), sourceId = Guid.NewGuid(), url = $"https://afisha.example/{postId:N}", captureId = Guid.NewGuid(), capturedAtUtc = "2026-11-09T10:00:00Z" },
            },
            ["contentLinks"] = new object[] { new { url = "https://mstar.uz/event", kind = "site" } },
            ["mapping"] = new { schema = "wow2.event.v1", norm = "norm@2", rules = "rules@4", parser = "schemaOrg" },
            ["capture"] = new { captureId = Guid.NewGuid(), capturedAtUtc = "2026-11-09T11:00:00Z", url = $"https://afisha.example/{postId:N}" },
        };
        edit?.Invoke(data);
        return Raw(type, JsonSerializer.SerializeToElement(data, Web), postId, revision);
    }

    /// <summary>Builds a record with any payload.</summary>
    public static ScfdDeliveryRecord Raw(string type, JsonElement? data, Guid? postId = null, int? revision = null) => new()
    {
        EventId = Guid.NewGuid(),
        ItemId = $"{postId:N}:{revision}",
        Type = type,
        OccurredAtUtc = OccurredAt,
        PostId = postId,
        Revision = revision,
        Reason = "ingest",
        Data = data,
    };

    /// <summary>Builds the same delivery again, as a retry carries it.</summary>
    public static ScfdDeliveryRecord Retry(ScfdDeliveryRecord record) => record with { };
}
