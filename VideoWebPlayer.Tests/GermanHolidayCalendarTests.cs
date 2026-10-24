using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class GermanHolidayCalendarTests
{
    [Fact]
    public void GetHolidays_ReturnsNationwideHolidays()
    {
        var holidays = GermanHolidayCalendar.GetHolidays(2026, null);

        Assert.Contains(new DateOnly(2026, 1, 1), holidays);   // Neujahr
        Assert.Contains(new DateOnly(2026, 5, 1), holidays);   // Tag der Arbeit
        Assert.Contains(new DateOnly(2026, 10, 3), holidays);  // Tag der Deutschen Einheit
        Assert.Contains(new DateOnly(2026, 12, 25), holidays); // 1. Weihnachtstag
        Assert.Contains(new DateOnly(2026, 12, 26), holidays); // 2. Weihnachtstag

        // Easter-based (2026: Easter Sunday = 05.04.)
        Assert.Contains(new DateOnly(2026, 4, 3), holidays);   // Karfreitag
        Assert.Contains(new DateOnly(2026, 4, 6), holidays);   // Ostermontag
        Assert.Contains(new DateOnly(2026, 5, 14), holidays);  // Christi Himmelfahrt
        Assert.Contains(new DateOnly(2026, 5, 25), holidays);  // Pfingstmontag

        // State-specific days absent without a state.
        Assert.DoesNotContain(new DateOnly(2026, 6, 4), holidays);  // Fronleichnam
        Assert.DoesNotContain(new DateOnly(2026, 1, 6), holidays);  // Hl. Drei Könige
        Assert.DoesNotContain(new DateOnly(2026, 10, 31), holidays);// Reformationstag
        Assert.DoesNotContain(new DateOnly(2026, 11, 1), holidays); // Allerheiligen
        Assert.DoesNotContain(new DateOnly(2026, 11, 18), holidays);// Buß- und Bettag
    }

    [Fact]
    public void GetHolidays_StateSpecific()
    {
        var nw = GermanHolidayCalendar.GetHolidays(2026, "DE-NW");
        Assert.Contains(new DateOnly(2026, 6, 4), nw);   // Fronleichnam
        Assert.Contains(new DateOnly(2026, 11, 1), nw);  // Allerheiligen
        Assert.DoesNotContain(new DateOnly(2026, 1, 6), nw);  // Hl. Drei Könige

        var hh = GermanHolidayCalendar.GetHolidays(2026, "DE-HH");
        Assert.DoesNotContain(new DateOnly(2026, 6, 4), hh);  // kein Fronleichnam
        Assert.Contains(new DateOnly(2026, 10, 31), hh);      // Reformationstag

        var bb = GermanHolidayCalendar.GetHolidays(2026, "DE-BB");
        Assert.Contains(new DateOnly(2026, 10, 31), bb);
        Assert.DoesNotContain(new DateOnly(2026, 11, 1), bb);

        var be = GermanHolidayCalendar.GetHolidays(2026, "DE-BE");
        Assert.Contains(new DateOnly(2026, 3, 8), be);   // Frauentag

        var th = GermanHolidayCalendar.GetHolidays(2026, "DE-TH");
        Assert.Contains(new DateOnly(2026, 9, 20), th);  // Weltkindertag

        var sn = GermanHolidayCalendar.GetHolidays(2026, "DE-SN");
        Assert.Contains(new DateOnly(2026, 11, 18), sn); // Buß- und Bettag (Mi. vor 23.11.)

        var bw = GermanHolidayCalendar.GetHolidays(2026, "DE-BW");
        Assert.Contains(new DateOnly(2026, 1, 6), bw);   // Hl. Drei Könige
        Assert.DoesNotContain(new DateOnly(2026, 10, 31), bw); // kein Reformationstag
    }

    [Fact]
    public void IsHoliday_WorksForKnownDates()
    {
        Assert.True(GermanHolidayCalendar.IsHoliday(new DateOnly(2026, 12, 25), null));
        Assert.False(GermanHolidayCalendar.IsHoliday(new DateOnly(2026, 12, 24), null));
        Assert.True(GermanHolidayCalendar.IsHoliday(new DateOnly(2026, 6, 4), "DE-NW"));
        Assert.False(GermanHolidayCalendar.IsHoliday(new DateOnly(2026, 6, 4), "DE-HH"));
    }
}
