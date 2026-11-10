using System.Text.Json.Nodes;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Compares JSON texts by meaning, since jsonb hands back objects with their keys reordered.</summary>
internal static class JsonComparison
{
    /// <summary>Tells whether two JSON texts hold the same value; two missing texts are the same.</summary>
    public static bool Same(string? left, string? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (string.Equals(left, right, StringComparison.Ordinal))
            return true;

        return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right));
    }
}
