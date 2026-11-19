using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake;

/// <summary>Reading two titles as one event's (events-integration E17): case, punctuation, quotes and years drop out, and
/// the words alone decide, equal as sets or at least four fifths shared.</summary>
public sealed class EventTitleKeysTests
{
    [Theory]
    [InlineData("TITF 2026", "titf")]
    [InlineData("“TITF”, 2026!", "titf")]
    [InlineData("Visa  Fashion-Week 2026", "visa fashion week")]
    [InlineData("Qo'qon Kuzi", "qoqon kuzi")]
    [InlineData("Ташкентская ярмарка «Ёлка»", "ташкентская ярмарка елка")]
    [InlineData("Expo 1850", "expo 1850")]
    [InlineData("Show 2100", "show 2100")]
    public void Words_ShouldKeepTheLettersAndDigitsOfEachWord_WithoutCaseQuotesPunctuationOrYears(string title, string words) =>
        EventTitleKeys.Words(title).Order().Should().Equal(words.Split(' ').Order());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2026")]
    [InlineData("—— “” ——")]
    public void Words_ShouldBeEmpty_WhenTheTitleHoldsNoWord(string? title) => EventTitleKeys.Words(title).Should().BeEmpty();

    [Theory]
    [InlineData("TITF 2026", "titf", true)]
    [InlineData("Visa Fashion Week", "Fashion Week Visa 2026", true)]
    [InlineData("Tashkent International Trade Fair", "Tashkent International Trade Fair Autumn", true)]
    [InlineData("Tashkent International Trade Fair", "Tashkent International Trade", false)]
    [InlineData("Fashion", "Visa Fashion Week", false)]
    [InlineData("TITF 2026", "TITF Spring", false)]
    [InlineData("2026", "2026", false)]
    [InlineData(null, "TITF", false)]
    public void Same_ShouldMatchTitlesOfEqualWords_OrAtLeastFourFifthsShared(string? left, string? right, bool expected) =>
        EventTitleKeys.Same(left, right).Should().Be(expected);

    [Fact]
    public void Overlap_ShouldDivideTheSharedWordsByTheLongerTitlesWords()
    {
        var overlap = EventTitleKeys.Overlap(EventTitleKeys.Words("a b c d"), EventTitleKeys.Words("a b c d e"));

        overlap.Should().Be(0.8);
    }
}
