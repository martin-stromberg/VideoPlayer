namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The behavior of the automated TV channel's program schedule for material without a
    /// known age rating (<see cref="IAgeRated.AgeRatingMinAge"/> is <see langword="null"/> —
    /// including clips, trailers and fillers that carry no rating at all). Persisted as
    /// <see cref="Setup.ProgramScheduleUnknownRatingMode"/> (int column);
    /// <see cref="RestrictToNightWindow"/> must keep the value 0 so legacy backups and
    /// corrupted values fall back to the restrictive behaviour.
    /// </summary>
    public enum ProgramScheduleUnknownRatingMode
    {
        /// <summary>
        /// Material without a known rating may only air inside the FSK-18 night window —
        /// conservative default: unclassified does not mean proven child-safe.
        /// </summary>
        RestrictToNightWindow = 0,

        /// <summary>
        /// Material without a known rating may air at any time — the age window check only
        /// applies to titles with a normalized minimum age.
        /// </summary>
        Unrestricted = 1
    }
}
