using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Encodes a list's position as the sort values of the last row it returned, bound to its list and its sort, so
/// the next page starts after that row whatever changed meanwhile and a cursor from one list or sort fails on another. A
/// cursor that does not decode, or carries a value the store cannot hold, fails the same way: the caller answers 400.</summary>
internal static class KeysetCursor
{
    /// <summary>Holds the longest value a cursor may carry: a title is at most 200 characters, so anything near this is crafted.</summary>
    private const int MaxValueLength = 1_000;

    /// <summary>Encodes the position after a row.</summary>
    /// <param name="scope">The list's name, such as <c>tracking:ocharo-fashion</c>.</param>
    /// <param name="sort">The sort's name.</param>
    /// <param name="values">The row's sort values as text, ending on its id.</param>
    public static string Encode(string scope, string sort, params string[] values) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new[] { scope, sort }.Concat(values)));

    /// <summary>Decodes a cursor; none decodes to no values.</summary>
    /// <param name="scope">The list's name.</param>
    /// <param name="sort">The sort's name.</param>
    /// <param name="cursor">The cursor a previous page returned.</param>
    /// <param name="arity">How many values the sort's cursor holds.</param>
    /// <param name="values">The row's sort values; empty without a cursor.</param>
    /// <returns>Whether the cursor belongs to the list and the sort.</returns>
    public static bool TryDecode(string scope, string sort, string? cursor, int arity, out string[] values)
    {
        values = [];
        if (string.IsNullOrEmpty(cursor))
            return true;

        try
        {
            var parts = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)));
            if (parts is not { Length: >= 2 } || parts.Length != arity + 2 || parts.Any(part => part is null)
                || !string.Equals(parts[0], scope, StringComparison.Ordinal) || !string.Equals(parts[1], sort, StringComparison.Ordinal))
                return false;

            if (!parts.Skip(2).All(Storable))
                return false;

            values = parts[2..];
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Tells whether a value could be a sort value the store holds: a text the store reads, which is no NUL (a text
    /// column never holds one) and no unpaired surrogate (no UTF-8 spells it), and no longer than a title can be. Any other
    /// character, a tab or a line break included, may sit in a title and stays valid.</summary>
    private static bool Storable(string text)
    {
        if (text.Length > MaxValueLength)
            return false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\0' || char.IsLowSurrogate(character))
                return false;
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                    return false;
                index++;
            }
        }

        return true;
    }
}
