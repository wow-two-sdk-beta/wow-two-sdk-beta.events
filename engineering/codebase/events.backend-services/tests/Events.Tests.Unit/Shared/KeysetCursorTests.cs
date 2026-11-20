using System.Buffers.Text;
using System.Text;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace Events.Tests.Unit.Shared;

/// <summary>A keyset cursor carries the last row's sort values for the list and the sort it came from, and reads nowhere else.</summary>
public sealed class KeysetCursorTests
{
    private const string Scope = "tracking:ocharo-fashion";

    [Fact]
    public void TryDecode_ShouldReturnTheValuesEncoded_ForTheSameScopeAndSort()
    {
        var cursor = KeysetCursor.Encode(Scope, "starts", "0", "2026-12-01", "title | with a pipe and \"quotes\"", "0193f3a4-0000-7000-8000-000000000001");

        KeysetCursor.TryDecode(Scope, "starts", cursor, 4, out var values).Should().BeTrue();

        values.Should().Equal("0", "2026-12-01", "title | with a pipe and \"quotes\"", "0193f3a4-0000-7000-8000-000000000001");
    }

    [Fact]
    public void TryDecode_ShouldReadNoCursorAsTheFirstPage()
    {
        KeysetCursor.TryDecode(Scope, "added", null, 3, out var none).Should().BeTrue();
        KeysetCursor.TryDecode(Scope, "added", string.Empty, 3, out var empty).Should().BeTrue();

        none.Should().BeEmpty();
        empty.Should().BeEmpty();
    }

    [Fact]
    public void TryDecode_ShouldRefuseACursorOfAnotherSortScopeOrShape()
    {
        var cursor = KeysetCursor.Encode(Scope, "added", "0", "638000000000000000", "0193f3a4-0000-7000-8000-000000000001");

        KeysetCursor.TryDecode(Scope, "starts", cursor, 3, out _).Should().BeFalse("another sort");
        KeysetCursor.TryDecode("tracking:other", "added", cursor, 3, out _).Should().BeFalse("another catalog");
        KeysetCursor.TryDecode(Scope, "added", cursor, 4, out _).Should().BeFalse("another arity");
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("MXwy")]
    [InlineData("%%%")]
    public void TryDecode_ShouldRefuseAnythingElse(string cursor)
    {
        KeysetCursor.TryDecode(Scope, "starts", cursor, 4, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldRefuseJsonThatIsNotAListOfTexts()
    {
        foreach (var json in new[] { "{}", "[1,2,3,4,5,6]", "[null,\"starts\",\"0\",\"\",\"t\",\"id\"]", "\"tracking\"" })
            KeysetCursor.TryDecode(Scope, "starts", Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json)), 4, out _).Should().BeFalse(json);
    }

    [Fact]
    public void TryDecode_ShouldRefuseAValueTheStoreCannotHold()
    {
        var id = "0193f3a4-0000-7000-8000-000000000001";

        // Raw JSON, since the encoder would not write an unpaired surrogate: NUL, a lone high and a lone low surrogate, and a
        // value no title reaches.
        foreach (var title in new[] { "a\\u0000b", "\\ud800", "x\\ud800y", "\\udc00", new string('t', 1_001) })
        {
            var json = $"[\"{Scope}\",\"starts\",\"0\",\"2026-12-01\",\"{title}\",\"{id}\"]";

            KeysetCursor.TryDecode(Scope, "starts", Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json)), 4, out _)
                .Should().BeFalse(title.Length > 20 ? "a long value" : title);
        }
    }

    [Theory]
    [InlineData("tab\there")]
    [InlineData("two\nlines")]
    [InlineData("\u0001 control")]
    [InlineData("o'brien; drop table x; --")]
    [InlineData("пятый показ 🎭")]
    public void TryDecode_ShouldKeepAnyOtherTitleValid_ATabAndAnEmojiIncluded(string title)
    {
        var cursor = KeysetCursor.Encode(Scope, "starts", "0", "2026-12-01", title, "0193f3a4-0000-7000-8000-000000000001");

        KeysetCursor.TryDecode(Scope, "starts", cursor, 4, out var values).Should().BeTrue();

        values[2].Should().Be(title);
    }
}
