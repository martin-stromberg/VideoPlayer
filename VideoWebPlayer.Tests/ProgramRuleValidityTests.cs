using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Unit tests for <see cref="ProgramRuleValidity"/>: the per-mode window evaluation
/// (<see cref="ProgramRuleValidity.IsValidOn"/>) and the expiry detection
/// (<see cref="ProgramRuleValidity.IsExpired"/>) that drives the automatic purge.
/// </summary>
public sealed class ProgramRuleValidityTests
{
    private static ProgramRule Rule(ProgramRuleValidityMode mode = ProgramRuleValidityMode.Unrestricted)
        => new() { Name = "Regel", ValidityMode = mode };

    [Fact]
    public void IsValidOn_Unrestricted_AlwaysTrue()
    {
        var rule = Rule();

        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 1, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 24)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2030, 12, 31)));
    }

    [Fact]
    public void IsValidOn_OneTime_BoundariesInclusive()
    {
        var rule = Rule(ProgramRuleValidityMode.OneTime);
        rule.ValidFrom = new DateOnly(2026, 3, 1);
        rule.ValidTo = new DateOnly(2026, 6, 30);

        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 2, 28)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 3, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 4, 15)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 6, 30)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void IsValidOn_OneTime_OpenBounds()
    {
        // Nur Von: gilt ab dem Von-Tag unbegrenzt.
        var fromOnly = Rule(ProgramRuleValidityMode.OneTime);
        fromOnly.ValidFrom = new DateOnly(2026, 3, 1);
        Assert.False(ProgramRuleValidity.IsValidOn(fromOnly, new DateOnly(2026, 2, 28)));
        Assert.True(ProgramRuleValidity.IsValidOn(fromOnly, new DateOnly(2026, 3, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(fromOnly, new DateOnly(2030, 1, 1)));

        // Nur Bis: gilt bis zum Bis-Tag (inklusive), danach nie mehr.
        var toOnly = Rule(ProgramRuleValidityMode.OneTime);
        toOnly.ValidTo = new DateOnly(2026, 6, 30);
        Assert.True(ProgramRuleValidity.IsValidOn(toOnly, new DateOnly(2020, 1, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(toOnly, new DateOnly(2026, 6, 30)));
        Assert.False(ProgramRuleValidity.IsValidOn(toOnly, new DateOnly(2026, 7, 1)));

        // Keine Grenze: Schutz vor korrupten Daten — ungültig an jedem Tag.
        var none = Rule(ProgramRuleValidityMode.OneTime);
        Assert.False(ProgramRuleValidity.IsValidOn(none, new DateOnly(2026, 4, 1)));
    }

    [Fact]
    public void IsValidOn_Annual_InsideAndOutside()
    {
        var rule = Rule(ProgramRuleValidityMode.AnnualWindow);
        // Konventionsjahr 2000 — ausgewertet werden nur Monat und Tag.
        rule.ValidFrom = new DateOnly(2000, 3, 1);
        rule.ValidTo = new DateOnly(2000, 6, 30);

        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 2, 28)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 3, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2027, 5, 20)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 6, 30)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void IsValidOn_Annual_YearCrossing()
    {
        var rule = Rule(ProgramRuleValidityMode.AnnualWindow);
        rule.ValidFrom = new DateOnly(2000, 12, 20);
        rule.ValidTo = new DateOnly(2000, 1, 6);

        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 19)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 20)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 31)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2027, 1, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2027, 1, 6)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2027, 1, 7)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public void IsValidOn_Monthly_InsideAndOutside()
    {
        var rule = Rule(ProgramRuleValidityMode.MonthlyWindow);
        rule.ValidFromDay = 10;
        rule.ValidToDay = 20;

        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 9)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 10)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 11, 15)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 20)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 21)));
    }

    [Fact]
    public void IsValidOn_Monthly_MonthCrossing()
    {
        var rule = Rule(ProgramRuleValidityMode.MonthlyWindow);
        rule.ValidFromDay = 25;
        rule.ValidToDay = 5;

        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 24)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 25)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 31)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 11, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 11, 5)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 11, 6)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void IsValidOn_Monthly_MissingDaySkipped()
    {
        var rule = Rule(ProgramRuleValidityMode.MonthlyWindow);
        rule.ValidFromDay = 31;
        rule.ValidToDay = 31;

        // Der 31. gilt in Monaten, die ihn haben, und entfällt sonst einfach.
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 1, 31)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 3, 31)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 2, 28)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 4, 30)));
    }

    [Fact]
    public void IsValidOn_Weekly_WeekdayMask()
    {
        var rule = Rule(ProgramRuleValidityMode.WeeklyWindow);
        // Mo(1) + Mi(3) + Fr(5)
        rule.ValidWeekdays = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Wednesday) | (1 << (int)DayOfWeek.Friday);

        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 26)));  // Montag
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 27))); // Dienstag
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 28)));  // Mittwoch
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 29))); // Donnerstag
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 30)));  // Freitag
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 10, 31))); // Samstag
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 11, 1)));  // Sonntag
    }

    [Fact]
    public void IsValidOn_Weekly_EmptyMask_NotValid()
    {
        var nullMask = Rule(ProgramRuleValidityMode.WeeklyWindow);
        Assert.False(ProgramRuleValidity.IsValidOn(nullMask, new DateOnly(2026, 10, 26)));

        var emptyMask = Rule(ProgramRuleValidityMode.WeeklyWindow);
        emptyMask.ValidWeekdays = 0;
        Assert.False(ProgramRuleValidity.IsValidOn(emptyMask, new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public void IsValidOn_Holiday_AroundAnchor()
    {
        var rule = Rule(ProgramRuleValidityMode.HolidayWindow);
        rule.ValidHolidayAnchor = ProgramRuleHolidayAnchor.EasterSunday;
        rule.ValidHolidayBeforeDays = 3;
        rule.ValidHolidayAfterDays = 0;

        // Ostersonntag 2026 = 05.04. → Fenster 02.04.–05.04. (Gründonnerstag–Ostersonntag).
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 4, 1)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 4, 2)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 4, 5)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 4, 6)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 25)));
    }

    [Fact]
    public void IsValidOn_Holiday_YearCrossing()
    {
        var rule = Rule(ProgramRuleValidityMode.HolidayWindow);
        // 2. Weihnachtstag +7 Tage → 26.12.2026–02.01.2027.
        rule.ValidHolidayAnchor = ProgramRuleHolidayAnchor.SecondChristmasDay;
        rule.ValidHolidayBeforeDays = 0;
        rule.ValidHolidayAfterDays = 7;

        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 26)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 31)));
        Assert.True(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2027, 1, 2)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2027, 1, 3)));
        Assert.False(ProgramRuleValidity.IsValidOn(rule, new DateOnly(2026, 12, 20)));
    }

    [Fact]
    public void IsValidOn_Holiday_MissingAnchor_NotValid()
    {
        var noAnchor = Rule(ProgramRuleValidityMode.HolidayWindow);
        noAnchor.ValidHolidayBeforeDays = 1;
        noAnchor.ValidHolidayAfterDays = 1;
        Assert.False(ProgramRuleValidity.IsValidOn(noAnchor, new DateOnly(2026, 4, 5)));

        var noOffsets = Rule(ProgramRuleValidityMode.HolidayWindow);
        noOffsets.ValidHolidayAnchor = ProgramRuleHolidayAnchor.ChristmasDay;
        Assert.False(ProgramRuleValidity.IsValidOn(noOffsets, new DateOnly(2026, 12, 25)));
    }

    [Fact]
    public void IsExpired_OneTime_PastValidTo()
    {
        var rule = Rule(ProgramRuleValidityMode.OneTime);
        rule.ValidFrom = new DateOnly(2026, 3, 1);
        rule.ValidTo = new DateOnly(2026, 6, 30);

        Assert.False(ProgramRuleValidity.IsExpired(rule, new DateOnly(2026, 6, 30)));
        Assert.True(ProgramRuleValidity.IsExpired(rule, new DateOnly(2026, 7, 1)));
        Assert.True(ProgramRuleValidity.IsExpired(rule, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void IsExpired_OpenEnd_NeverExpires()
    {
        var openEnd = Rule(ProgramRuleValidityMode.OneTime);
        openEnd.ValidFrom = new DateOnly(2020, 1, 1);
        Assert.False(ProgramRuleValidity.IsExpired(openEnd, new DateOnly(2030, 1, 1)));

        // Wiederkehrende Fenster laufen nie ab.
        var annual = Rule(ProgramRuleValidityMode.AnnualWindow);
        annual.ValidFrom = new DateOnly(2000, 3, 1);
        annual.ValidTo = new DateOnly(2000, 6, 30);
        Assert.False(ProgramRuleValidity.IsExpired(annual, new DateOnly(2030, 12, 31)));
    }
}
