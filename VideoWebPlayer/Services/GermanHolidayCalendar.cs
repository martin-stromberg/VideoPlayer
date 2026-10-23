namespace VideoWebPlayer.Services;

/// <summary>
/// Fixed German public holiday calendar ("Feiertagskalender") for the automated TV channel's
/// holiday rules. Computed locally — no external data source. The federal state is given as an
/// ISO-3166-2 <c>DE-*</c> code; <see langword="null"/> means only Germany-wide holidays apply.
/// </summary>
public static class GermanHolidayCalendar
{
    private static readonly string[] EpiphanyStates = ["DE-BW", "DE-BY", "DE-ST"];
    private static readonly string[] WomensDayStates = ["DE-BE", "DE-MV"];
    private static readonly string[] CorpusChristiStates = ["DE-BW", "DE-BY", "DE-HE", "DE-NW", "DE-RP", "DE-SL"];
    private static readonly string[] AssumptionStates = ["DE-BY", "DE-SL"];
    private static readonly string[] ChildrensDayStates = ["DE-TH"];
    private static readonly string[] ReformationDayStates = ["DE-BB", "DE-HB", "DE-HH", "DE-MV", "DE-NI", "DE-SN", "DE-ST", "DE-SH", "DE-TH"];
    private static readonly string[] AllSaintsStates = ["DE-BW", "DE-BY", "DE-NW", "DE-RP", "DE-SL"];
    private static readonly string[] RepentanceDayStates = ["DE-SN"];

    /// <summary>
    /// Computes all public holidays of the given year valid in the given federal state
    /// (or Germany-wide only when <paramref name="federalState"/> is <see langword="null"/>).
    /// </summary>
    /// <param name="year">The calendar year.</param>
    /// <param name="federalState">ISO-3166-2 <c>DE-*</c> code, or <see langword="null"/>.</param>
    /// <returns>The set of holiday dates.</returns>
    public static IReadOnlySet<DateOnly> GetHolidays(int year, string? federalState)
    {
        var holidays = new HashSet<DateOnly>
        {
            new(year, 1, 1),    // Neujahr
            new(year, 5, 1),    // Tag der Arbeit
            new(year, 10, 3),   // Tag der Deutschen Einheit
            new(year, 12, 25),  // 1. Weihnachtstag
            new(year, 12, 26)   // 2. Weihnachtstag
        };

        var easter = EasterSunday(year);
        holidays.Add(easter.AddDays(-2));   // Karfreitag
        holidays.Add(easter.AddDays(1));    // Ostermontag
        holidays.Add(easter.AddDays(39));   // Christi Himmelfahrt
        holidays.Add(easter.AddDays(50));   // Pfingstmontag

        if (federalState is null)
            return holidays;

        if (EpiphanyStates.Contains(federalState))
            holidays.Add(new DateOnly(year, 1, 6));
        if (WomensDayStates.Contains(federalState))
            holidays.Add(new DateOnly(year, 3, 8));
        if (CorpusChristiStates.Contains(federalState))
            holidays.Add(easter.AddDays(60));
        if (AssumptionStates.Contains(federalState))
            holidays.Add(new DateOnly(year, 8, 15));
        if (ChildrensDayStates.Contains(federalState))
            holidays.Add(new DateOnly(year, 9, 20));
        if (ReformationDayStates.Contains(federalState))
            holidays.Add(new DateOnly(year, 10, 31));
        if (AllSaintsStates.Contains(federalState))
            holidays.Add(new DateOnly(year, 11, 1));
        if (RepentanceDayStates.Contains(federalState))
            holidays.Add(RepentanceDay(year));

        return holidays;
    }

    /// <summary>
    /// Whether the given date is a public holiday in the given federal state.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <param name="federalState">ISO-3166-2 <c>DE-*</c> code, or <see langword="null"/> for Germany-wide holidays only.</param>
    /// <returns><see langword="true"/> when the date is a public holiday.</returns>
    public static bool IsHoliday(DateOnly date, string? federalState)
        => GetHolidays(date.Year, federalState).Contains(date);

    // Buß- und Bettag: the Wednesday before November 23rd.
    private static DateOnly RepentanceDay(int year)
    {
        var day = new DateOnly(year, 11, 22);
        while (day.DayOfWeek != DayOfWeek.Wednesday)
            day = day.AddDays(-1);
        return day;
    }

    // Anonymous Gregorian computus.
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
