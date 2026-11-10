using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Encodes a list's position as an opaque cursor bound to its list, so a cursor from one list fails on another.</summary>
internal static class PageCursor
{
    /// <summary>Encodes the offset of the next page.</summary>
    /// <param name="scope">The list's name, such as <c>events:ocharo-fashion</c>.</param>
    /// <param name="offset">The next page's offset.</param>
    public static string Encode(string scope, int offset) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{scope}|{offset.ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>Decodes a cursor; none decodes to offset 0.</summary>
    /// <param name="scope">The list's name.</param>
    /// <param name="cursor">The cursor a previous page returned.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>Whether the cursor belongs to the list.</returns>
    public static bool TryDecode(string scope, string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrEmpty(cursor))
            return true;

        try
        {
            var text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor));
            var separator = text.LastIndexOf('|');
            return separator > 0
                && string.Equals(text[..separator], scope, StringComparison.Ordinal)
                && int.TryParse(text[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out offset);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Clamps a requested page size to 1..<see cref="EventsConstants.MaxPageSize"/>.</summary>
    public static int Limit(int requested) => Math.Clamp(requested, 1, EventsConstants.MaxPageSize);
}
