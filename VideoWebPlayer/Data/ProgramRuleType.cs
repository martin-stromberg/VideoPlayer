namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The kind of a program rule (<see cref="ProgramRule"/>), i.e. which sort of recurring broadcast
    /// it describes.
    /// </summary>
    public enum ProgramRuleType
    {
        /// <summary>A TV show airing at a fixed time (series slot).</summary>
        SeriesFixedTime = 0,

        /// <summary>A movie airing at prime time.</summary>
        MoviePrimeTime = 1,

        /// <summary>A rotation through the contents of a genre or format.</summary>
        GenreRotation = 2,

        /// <summary>A rerun of previously selected material.</summary>
        Repeat = 3,

        /// <summary>A special airing on public holidays.</summary>
        HolidaySpecial = 4,

        /// <summary>Filler material used to pad gaps in the schedule.</summary>
        Filler = 5
    }
}
