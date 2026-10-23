namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The recurrence pattern of a <see cref="ProgramRule"/>.
    /// </summary>
    public enum ProgramRuleRecurrence
    {
        /// <summary>The rule applies every day.</summary>
        Daily = 0,

        /// <summary>The rule applies once a week on the configured day of week.</summary>
        Weekly = 1,

        /// <summary>The rule applies exactly once on the configured date.</summary>
        Once = 2,

        /// <summary>The rule applies on public holidays (German holiday calendar).</summary>
        Holiday = 3,

        /// <summary>The rule applies on weekdays (Monday to Friday).</summary>
        Weekday = 4,

        /// <summary>The rule applies on weekends (Saturday and Sunday).</summary>
        Weekend = 5
    }
}
