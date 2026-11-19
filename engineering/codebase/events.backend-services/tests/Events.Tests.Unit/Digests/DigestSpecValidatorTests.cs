using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

namespace Events.Tests.Unit.Digests;

/// <summary>Covers a digest's validation: normalized lists, and the field each invalid spec names.</summary>
public sealed class DigestSpecValidatorTests
{
    private static readonly string[] Channels = [DigestChannelConstants.Log, DigestChannelConstants.Telegram];

    [Fact]
    public void Validate_ShouldNormalizeTheSpec()
    {
        var spec = Spec() with
        {
            Name = "  Weekly  ",
            Schedule = new DigestSchedule { Days = [DayOfWeek.Sunday, DayOfWeek.Monday], Times = ["18:00", "09:00"], TimeZone = " Asia/Tashkent " },
            Selection = new DigestSelection
            {
                Cities = ["Tashkent", "TASHKENT", " Samarkand "],
                Statuses = [EventStatus.Scheduled, EventStatus.Scheduled],
                Stages = [" shortlisted ", "applying", "shortlisted"],
                ExcludeStages = ["skipped", "skipped"],
            },
            LinkTemplate = " https://app.example.test/events/{id} ",
        };

        DigestSpecValidator.Validate(spec, Channels, out var normalized).Should().BeNull();

        normalized.Name.Should().Be("Weekly");
        normalized.Schedule.Days.Should().Equal(DayOfWeek.Monday, DayOfWeek.Sunday);
        normalized.Schedule.Times.Should().Equal("09:00", "18:00");
        normalized.Schedule.TimeZone.Should().Be("Asia/Tashkent");
        normalized.Selection.Cities.Should().Equal("tashkent", "samarkand");
        normalized.Selection.Statuses.Should().Equal(EventStatus.Scheduled);
        normalized.Selection.Stages.Should().Equal("shortlisted", "applying");
        normalized.Selection.ExcludeStages.Should().Equal("skipped");
        normalized.LinkTemplate.Should().Be("https://app.example.test/events/{id}");
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("schedule.days")]
    [InlineData("schedule.times[0]")]
    [InlineData("schedule.timeZone")]
    [InlineData("selection.startingWithinDays")]
    [InlineData("selection.kinds")]
    [InlineData("selection.stages")]
    [InlineData("selection.excludeStages")]
    [InlineData("recipients")]
    [InlineData("recipients[0]")]
    [InlineData("recipients[0].language")]
    [InlineData("language")]
    [InlineData("itemCap")]
    [InlineData("linkTemplate")]
    public void Validate_ShouldNameTheInvalidField(string field)
    {
        var spec = field switch
        {
            "channel" => Spec() with { Channel = "pigeon" },
            "schedule.days" => Spec() with { Schedule = Spec().Schedule with { Days = [] } },
            "schedule.times[0]" => Spec() with { Schedule = Spec().Schedule with { Times = ["9am"] } },
            "schedule.timeZone" => Spec() with { Schedule = Spec().Schedule with { TimeZone = "Mars/Olympus" } },
            "selection.startingWithinDays" => Spec() with { Selection = new DigestSelection { StartingWithinDays = -1 } },
            "selection.kinds" => Spec() with { Selection = new DigestSelection { Kinds = ["Trade Fair"] } },
            "selection.stages" => Spec() with { Selection = new DigestSelection { Stages = ["Short listed"] } },
            "selection.excludeStages" => Spec() with { Selection = new DigestSelection { Stages = ["skipped"], ExcludeStages = ["skipped"] } },
            "recipients" => Spec() with { Recipients = [] },
            "recipients[0]" => Spec() with { Recipients = [new DigestRecipient { Subject = "a", Role = "member" }] },
            "recipients[0].language" => Spec() with { Recipients = [new DigestRecipient { Subject = "a", Language = "de" }] },
            "language" => Spec() with { Language = "uz" },
            "itemCap" => Spec() with { ItemCap = 51 },
            _ => Spec() with { LinkTemplate = "ftp://files.example.test/{id}" },
        };

        DigestSpecValidator.Validate(spec, Channels, out _)!.Message.Should().StartWith(field + ":");
    }

    private static DigestSpec Spec() => new()
    {
        Name = "Weekly",
        Channel = DigestChannelConstants.Telegram,
        Schedule = new DigestSchedule { Days = [DayOfWeek.Monday], Times = ["09:00"], TimeZone = "Asia/Tashkent" },
        Recipients = [new DigestRecipient { Subject = Guid.NewGuid().ToString("D") }],
    };
}
