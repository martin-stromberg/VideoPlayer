namespace VideoWebPlayer.Configuration;

/// <summary>
/// Options for the automated TV channel's program schedule generation
/// (section <c>ProgramSchedule</c>).
/// </summary>
public sealed class ProgramScheduleOptions
{
    /// <summary>
    /// Gets or sets the number of days a movie airing may lie ahead of a filler position for its
    /// trailer to be preferred as gap filler ("trailer of upcoming movies" lead time).
    /// Default: 7 days; values &lt;= 0 fall back to the default.
    /// </summary>
    public int TrailerLeadDays { get; set; } = 7;

    /// <summary>
    /// Gets the normalized lead window (values &lt;= 0 fall back to the 7-day default).
    /// </summary>
    public TimeSpan EffectiveTrailerLead => TrailerLeadDays > 0 ? TimeSpan.FromDays(TrailerLeadDays) : TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the minimum remaining time (in seconds) before an occurrence window's
    /// boundary that an item needs to even start airing. Below the threshold nothing is
    /// scheduled — fillers cover the remainder instead. A started item always airs completely,
    /// so a larger remainder still places (and overshoots) a longer item. Default: 30 seconds;
    /// values &lt;= 0 fall back to the default.
    /// </summary>
    public int MinStartRemainSeconds { get; set; } = 30;

    /// <summary>
    /// Gets the normalized minimum remainder (values &lt;= 0 fall back to the 30-second default).
    /// </summary>
    public TimeSpan EffectiveMinStartRemain => MinStartRemainSeconds > 0 ? TimeSpan.FromSeconds(MinStartRemainSeconds) : TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum lookback in days the schedule re-evaluation may reach behind the
    /// coverage start when proving a still-running airing reproducible — long-running broadcasts
    /// (marathon blocks, multi-day events) start further back than the regular 24-hour window.
    /// Airings whose real start is older than this bound are displaced and re-planned.
    /// Default: 7 days; values &lt;= 0 fall back to the default, values above 30 are clamped
    /// (an absurdly large window would make the occurrence day loop practically endless).
    /// </summary>
    public int MaxOccurrenceLookbackDays { get; set; } = 7;

    /// <summary>
    /// Gets the normalized maximum lookback (values &lt;= 0 fall back to the 7-day default,
    /// values above 30 days are clamped).
    /// </summary>
    public TimeSpan EffectiveMaxOccurrenceLookback => MaxOccurrenceLookbackDays > 0 ? TimeSpan.FromDays(Math.Min(MaxOccurrenceLookbackDays, 30)) : TimeSpan.FromDays(7);
}
