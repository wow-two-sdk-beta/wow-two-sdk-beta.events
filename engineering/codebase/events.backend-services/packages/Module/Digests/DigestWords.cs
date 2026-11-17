using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Holds a digest's words in one language: month names, deadline and status labels, the new mark and the tail.</summary>
/// <remarks>Fixed tables, not the host's culture data, so a message reads the same on every host.</remarks>
internal sealed class DigestWords
{
    private static readonly DigestWords English = new(
        months: ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
        monthsOfDay: null,
        dayPattern: "{0} {1} {2}",
        monthPattern: "{0} {1}",
        deadlines: new Dictionary<DeadlineKind, string>
        {
            [DeadlineKind.Application] = "Applications close",
            [DeadlineKind.Registration] = "Registration closes",
            [DeadlineKind.EarlyBird] = "Early-bird price ends",
            [DeadlineKind.Submission] = "Submissions close",
            [DeadlineKind.Payment] = "Payment due",
            [DeadlineKind.Other] = "Deadline",
        },
        statuses: new Dictionary<EventStatus, string>
        {
            [EventStatus.Postponed] = "postponed",
            [EventStatus.Rescheduled] = "new dates",
            [EventStatus.Cancelled] = "cancelled",
            [EventStatus.MovedOnline] = "moved online",
        },
        estimated: "(estimated)",
        dateUnknown: "Date to be announced",
        @new: "new",
        more: "…and {0} more");

    private static readonly DigestWords Russian = new(
        months: ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"],
        monthsOfDay: ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"],
        dayPattern: "{0} {1} {2}",
        monthPattern: "{0} {1}",
        deadlines: new Dictionary<DeadlineKind, string>
        {
            [DeadlineKind.Application] = "Приём заявок до",
            [DeadlineKind.Registration] = "Регистрация до",
            [DeadlineKind.EarlyBird] = "Ранняя цена до",
            [DeadlineKind.Submission] = "Подача работ до",
            [DeadlineKind.Payment] = "Оплата до",
            [DeadlineKind.Other] = "Срок",
        },
        statuses: new Dictionary<EventStatus, string>
        {
            [EventStatus.Postponed] = "перенесено",
            [EventStatus.Rescheduled] = "новые даты",
            [EventStatus.Cancelled] = "отменено",
            [EventStatus.MovedOnline] = "онлайн",
        },
        estimated: "(ориентировочно)",
        dateUnknown: "Дата уточняется",
        @new: "новое",
        more: "…и ещё {0}");

    private static readonly DigestWords UzbekLatin = new(
        months: ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avgust", "sentabr", "oktabr", "noyabr", "dekabr"],
        monthsOfDay: null,
        dayPattern: "{0}-{1} {2}",
        monthPattern: "{0} {1}",
        deadlines: new Dictionary<DeadlineKind, string>
        {
            [DeadlineKind.Application] = "Arizalar qabuli",
            [DeadlineKind.Registration] = "Roʻyxatdan oʻtish",
            [DeadlineKind.EarlyBird] = "Erta narx",
            [DeadlineKind.Submission] = "Ishlarni topshirish",
            [DeadlineKind.Payment] = "Toʻlov",
            [DeadlineKind.Other] = "Muddat",
        },
        statuses: new Dictionary<EventStatus, string>
        {
            [EventStatus.Postponed] = "qoldirildi",
            [EventStatus.Rescheduled] = "sana oʻzgardi",
            [EventStatus.Cancelled] = "bekor qilindi",
            [EventStatus.MovedOnline] = "onlayn",
        },
        estimated: "(taxminan)",
        dateUnknown: "Sana aniqlanmoqda",
        @new: "yangi",
        more: "…yana {0} ta");

    private readonly string[] months;
    private readonly string[] monthsOfDay;
    private readonly string dayPattern;
    private readonly string monthPattern;
    private readonly IReadOnlyDictionary<DeadlineKind, string> deadlines;
    private readonly IReadOnlyDictionary<EventStatus, string> statuses;

    private DigestWords(
        string[] months,
        string[]? monthsOfDay,
        string dayPattern,
        string monthPattern,
        IReadOnlyDictionary<DeadlineKind, string> deadlines,
        IReadOnlyDictionary<EventStatus, string> statuses,
        string estimated,
        string dateUnknown,
        string @new,
        string more)
    {
        this.months = months;
        this.monthsOfDay = monthsOfDay ?? months;
        this.dayPattern = dayPattern;
        this.monthPattern = monthPattern;
        this.deadlines = deadlines;
        this.statuses = statuses;
        Estimated = estimated;
        DateUnknown = dateUnknown;
        New = @new;
        More = more;
    }

    /// <summary>Gets the mark of an estimated date.</summary>
    public string Estimated { get; }

    /// <summary>Gets the words of an unknown date.</summary>
    public string DateUnknown { get; }

    /// <summary>Gets the mark of an event new since the previous run.</summary>
    public string New { get; }

    /// <summary>Gets the tail's pattern, with the count of events left out.</summary>
    public string More { get; }

    /// <summary>Gets the words of a language; English for one the table lacks.</summary>
    public static DigestWords For(string language) => language switch
    {
        DigestLanguageConstants.Russian => Russian,
        DigestLanguageConstants.UzbekLatin => UzbekLatin,
        _ => English,
    };

    /// <summary>Writes a day, such as <c>14 March 2026</c>, <c>14 марта 2026</c> or <c>14-mart 2026</c>.</summary>
    public string Day(int day, int month, int year) =>
        string.Format(CultureInfo.InvariantCulture, dayPattern, day, monthsOfDay[month - 1], year);

    /// <summary>Writes a month, such as <c>March 2026</c>.</summary>
    public string Month(int month, int year) =>
        string.Format(CultureInfo.InvariantCulture, monthPattern, months[month - 1], year);

    /// <summary>Gets a deadline kind's label.</summary>
    public string Deadline(DeadlineKind kind) => deadlines.TryGetValue(kind, out var label) ? label : deadlines[DeadlineKind.Other];

    /// <summary>Gets a status's label; empty for a scheduled event.</summary>
    public string Status(EventStatus status) => statuses.TryGetValue(status, out var label) ? label : string.Empty;
}
