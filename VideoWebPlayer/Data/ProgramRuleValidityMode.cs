namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The validity mode of a <see cref="ProgramRule"/> — a dimension independent of
    /// <see cref="ProgramRule.Recurrence"/> that bounds the days on which the rule may produce
    /// occurrences at all.
    /// </summary>
    public enum ProgramRuleValidityMode
    {
        /// <summary>The rule is valid on every day (default; backwards compatible).</summary>
        Unrestricted = 0,

        /// <summary>The rule is valid inside a single one-off date range; each bound may be
        /// left open, at least one bound is required.</summary>
        OneTime = 1,

        /// <summary>The rule is valid inside an annually recurring month-day window; the window
        /// may wrap over the turn of the year (e.g. 20.12.–06.01.).</summary>
        AnnualWindow = 2,

        /// <summary>The rule is valid inside a monthly recurring day-of-month window; the window
        /// may wrap over the turn of the month (e.g. 25.–5.).</summary>
        MonthlyWindow = 3,

        /// <summary>The rule is valid on the days of week selected via a bitmask.</summary>
        WeeklyWindow = 4,

        /// <summary>The rule is valid inside a window of days around a named holiday anchor.</summary>
        HolidayWindow = 5
    }
}
