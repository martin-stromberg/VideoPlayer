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
}
