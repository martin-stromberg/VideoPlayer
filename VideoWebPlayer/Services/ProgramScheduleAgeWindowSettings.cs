namespace VideoWebPlayer.Services;

/// <summary>
/// The channel-wide youth-protection window schema of the program schedule ("Sendefenster"):
/// the earliest local air time per age-rating step plus the common end of all night windows.
/// A title may only be materialized when its normalized minimum age
/// (<see cref="Data.IAgeRated.AgeRatingMinAge"/>) fits the local slot start — evaluated by
/// <see cref="ProgramScheduleAgeWindow"/>.
/// </summary>
/// <param name="Fsk12Earliest">Earliest local air time for titles with minimum age 12–15.</param>
/// <param name="Fsk16Earliest">Earliest local air time for titles with minimum age 16–17.</param>
/// <param name="Fsk18Earliest">Earliest local air time for titles with minimum age ≥ 18.</param>
/// <param name="NightEnd">Common end of all night windows (all rated titles air freely
/// before this time again).</param>
public sealed record ProgramScheduleAgeWindowSettings(
    TimeOnly Fsk12Earliest,
    TimeOnly Fsk16Earliest,
    TimeOnly Fsk18Earliest,
    TimeOnly NightEnd)
{
    /// <summary>
    /// The delivered default schema following the German youth-protection convention:
    /// FSK 12 from 20:00, FSK 16 from 22:00, FSK 18 from 23:00, all night windows ending 06:00.
    /// </summary>
    public static readonly ProgramScheduleAgeWindowSettings Default = new(
        new TimeOnly(20, 0), new TimeOnly(22, 0), new TimeOnly(23, 0), new TimeOnly(6, 0));

    /// <summary>
    /// Whether the schema is internally consistent: the earliest air times must be monotone
    /// (<c>Fsk12 ≤ Fsk16 ≤ Fsk18</c>) and the night end must lie before the first window
    /// start (<c>NightEnd &lt; Fsk12</c>) so the night window is unambiguous.
    /// </summary>
    public bool IsValid()
        => Fsk12Earliest <= Fsk16Earliest
            && Fsk16Earliest <= Fsk18Earliest
            && NightEnd < Fsk12Earliest;
}
