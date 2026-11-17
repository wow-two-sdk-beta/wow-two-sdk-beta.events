namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Names Uzbekistan's regional centres by one Latin spelling, the one Ocharo's curated events use, so a sourced
/// event and a curated one group under one city: Afisha writes <c>Ташкент</c>, the catalog's own events say
/// <c>Tashkent</c>.</summary>
/// <remarks>The table holds each centre's Russian, Uzbek Latin, Uzbek Cyrillic and English spellings. A name outside it
/// passes through unchanged, so nothing is guessed about a city the table does not know. The source spelling stays in
/// the observation's raw payload.</remarks>
internal static class CityNames
{
    private static readonly (string Canonical, string[] Spellings)[] Table =
    [
        ("Tashkent", ["Ташкент", "Toshkent", "Тошкент"]),
        ("Samarkand", ["Самарканд", "Samarqand", "Самарқанд"]),
        ("Bukhara", ["Бухара", "Buxoro", "Бухоро"]),
        ("Khiva", ["Хива", "Xiva"]),
        ("Namangan", ["Наманган"]),
        ("Andijan", ["Андижан", "Andijon", "Андижон"]),
        ("Fergana", ["Фергана", "Farg'ona", "Фарғона"]),
        ("Margilan", ["Маргилан", "Marg'ilon", "Марғилон"]),
        ("Kokand", ["Коканд", "Qo'qon", "Қўқон"]),
        ("Nukus", ["Нукус"]),
        ("Karshi", ["Карши", "Qarshi", "Қарши"]),
        ("Termez", ["Термез", "Termiz", "Термиз"]),
        ("Navoi", ["Навои", "Navoiy", "Навоий"]),
        ("Jizzakh", ["Джизак", "Jizzax", "Жиззах"]),
        ("Gulistan", ["Гулистан", "Guliston", "Гулистон"]),
        ("Urgench", ["Ургенч", "Urganch", "Урганч"]),
    ];

    private static readonly Dictionary<string, string> Canonicals = Build();

    /// <summary>Gets the city's canonical Latin name when the table knows it, else the city as written.</summary>
    /// <param name="city">The city as the source wrote it.</param>
    /// <returns>The canonical name, or <paramref name="city"/> when it is not in the table.</returns>
    public static string Canonical(string city) => Canonicals.GetValueOrDefault(Key(city), city);

    /// <summary>Tells whether two spellings name the same city: equal without case and apostrophes, or both known
    /// spellings of one table city.</summary>
    /// <param name="left">One spelling.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they name one city.</returns>
    public static bool Same(string left, string right) => Key(Canonical(left)) == Key(Canonical(right));

    /// <summary>Reduces a spelling to its comparison form: lower case, apostrophes of every kind dropped, <c>ё</c> as
    /// <c>е</c>, whitespace collapsed.</summary>
    /// <param name="text">The spelling.</param>
    /// <returns>The comparison form.</returns>
    internal static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var letters = text.Trim().ToLowerInvariant().Where(letter => letter is not ('\'' or '’' or 'ʼ' or 'ʻ' or '‘' or '`' or '´'))
            .Select(letter => letter == 'ё' ? 'е' : letter);
        return string.Join(' ', new string(letters.ToArray()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static Dictionary<string, string> Build()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (canonical, spellings) in Table)
        {
            names[Key(canonical)] = canonical;
            foreach (var spelling in spellings)
                names[Key(spelling)] = canonical;
        }

        return names;
    }
}
