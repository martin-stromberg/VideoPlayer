namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The named, federal-state-independent holiday anchors a
    /// <see cref="ProgramRuleValidityMode.HolidayWindow"/> validity window can be placed around.
    /// </summary>
    public enum ProgramRuleHolidayAnchor
    {
        /// <summary>January 1st.</summary>
        NewYear = 0,

        /// <summary>Good Friday (Easter Sunday −2 days).</summary>
        GoodFriday = 1,

        /// <summary>Easter Sunday (Gregorian computus).</summary>
        EasterSunday = 2,

        /// <summary>Easter Monday (Easter Sunday +1 day).</summary>
        EasterMonday = 3,

        /// <summary>Ascension Day (Easter Sunday +39 days).</summary>
        AscensionDay = 4,

        /// <summary>Pentecost Sunday (Easter Sunday +49 days).</summary>
        PentecostSunday = 5,

        /// <summary>Pentecost Monday (Easter Sunday +50 days).</summary>
        PentecostMonday = 6,

        /// <summary>First Sunday of Advent (the Sunday between November 27th and December 3rd).</summary>
        FirstAdvent = 7,

        /// <summary>Christmas Eve, December 24th.</summary>
        ChristmasEve = 8,

        /// <summary>Christmas Day, December 25th.</summary>
        ChristmasDay = 9,

        /// <summary>Second Day of Christmas, December 26th.</summary>
        SecondChristmasDay = 10,

        /// <summary>New Year's Eve, December 31st.</summary>
        NewYearsEve = 11
    }
}
