using System.Globalization;
using System.Text;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reduces an event's title to the words that identify it, so one fair written two ways by two sources compares
/// equal (events-integration E17).</summary>
/// <remarks>
///   - letters and digits only, lower case, <c>ё</c> as <c>е</c>; punctuation, quotes and spacing separate words, and an
///     apostrophe inside a word joins it (<c>Qo'qon</c> is <c>qoqon</c>)
///   - a year (1900 to 2099) is no word: <c>TITF 2026</c> and <c>TITF</c> are one fair, and its edition is the dates' job
///   - two titles match when their words are equal as sets, or when the shared words are at least
///     <see cref="MinOverlap"/> of the longer title's words
/// </remarks>
internal static class EventTitleKeys
{
    /// <summary>Holds the least share of the longer title's words that two different titles must have in common.</summary>
    public const double MinOverlap = 0.8;

    /// <summary>Reads a title's words as a set; empty when the title holds none (a year alone, or only punctuation).</summary>
    /// <param name="title">The title as a source wrote it.</param>
    /// <returns>The words, lower case and without years.</returns>
    public static HashSet<string> Words(string? title)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(title))
            return words;

        var word = new StringBuilder();
        foreach (var letter in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(letter))
                word.Append(letter == 'ё' ? 'е' : letter);
            else if (letter is '\'' or '’' or 'ʼ' or 'ʻ' or '‘' or '`' or '´')
                continue;
            else
                Flush(word, words);
        }

        Flush(word, words);
        return words;
    }

    /// <summary>Scores how alike two titles read: 1 when their words are equal as sets, else the shared words over the longer
    /// title's words; 0 when either holds none.</summary>
    /// <param name="left">One title's words.</param>
    /// <param name="right">The other's.</param>
    /// <returns>The score, from 0 to 1.</returns>
    public static double Overlap(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0;

        var shared = left.Count(right.Contains);
        return (double)shared / Math.Max(left.Count, right.Count);
    }

    /// <summary>Tells whether two titles name one event: <see cref="Overlap"/> reaches <see cref="MinOverlap"/>.</summary>
    /// <param name="left">One title.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they match.</returns>
    public static bool Same(string? left, string? right) => Overlap(Words(left), Words(right)) >= MinOverlap;

    private static void Flush(StringBuilder word, HashSet<string> words)
    {
        if (word.Length == 0)
            return;

        var text = word.ToString();
        word.Clear();
        if (!IsYear(text))
            words.Add(text);
    }

    private static bool IsYear(string text) =>
        text.Length == 4 && text.All(char.IsAsciiDigit) && int.Parse(text, CultureInfo.InvariantCulture) is >= 1900 and <= 2099;
}
