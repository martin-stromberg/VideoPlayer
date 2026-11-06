using VideoWebPlayer.Data;
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
    public void GetAnchorDate_ReturnsExpectedDates()
    {
        // Oster-Anker (2026: Ostersonntag = 05.04.).
        Assert.Equal(new DateOnly(2026, 4, 3), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.GoodFriday, 2026));
        Assert.Equal(new DateOnly(2026, 4, 5), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.EasterSunday, 2026));
        Assert.Equal(new DateOnly(2026, 4, 6), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.EasterMonday, 2026));
        Assert.Equal(new DateOnly(2026, 5, 14), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.AscensionDay, 2026));
        Assert.Equal(new DateOnly(2026, 5, 24), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.PentecostSunday, 2026));
        Assert.Equal(new DateOnly(2026, 5, 25), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.PentecostMonday, 2026));

        // 1. Advent: der Sonntag zwischen 27.11. und 03.12. (2026: 29.11., 2025: 30.11.).
        Assert.Equal(new DateOnly(2026, 11, 29), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.FirstAdvent, 2026));
        Assert.Equal(new DateOnly(2025, 11, 30), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.FirstAdvent, 2025));

        // Feste Anker.
        Assert.Equal(new DateOnly(2026, 1, 1), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.NewYear, 2026));
        Assert.Equal(new DateOnly(2026, 12, 24), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.ChristmasEve, 2026));
        Assert.Equal(new DateOnly(2026, 12, 25), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.ChristmasDay, 2026));
        Assert.Equal(new DateOnly(2026, 12, 26), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.SecondChristmasDay, 2026));
        Assert.Equal(new DateOnly(2026, 12, 31), GermanHolidayCalendar.GetAnchorDate(ProgramRuleHolidayAnchor.NewYearsEve, 2026));
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
