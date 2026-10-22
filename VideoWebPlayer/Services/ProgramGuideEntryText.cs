namespace VideoWebPlayer.Services;

/// <summary>
/// Shared display-text formatting for the TV-channel program cards: the episode sub-line shown
/// under the card title, the genre badge text and the fallback shown when the content carries no
/// description. Centralized so that the API-backed view (<see cref="ProgramGuideService"/> →
/// <c>DtoProgramGuideEntry</c>) and the direct-EF admin schedule view
/// (<c>TVSenderAdmin</c>) render identical wording.
/// </summary>
public static class ProgramGuideEntryText
{
    /// <summary>
    /// The text shown in the description area when neither the aired content nor (for episodes)
    /// its show carries a plot.
    /// </summary>
    public const string NoDescriptionText = "— keine Beschreibung in den Metadaten hinterlegt —";

    /// <summary>
    /// Reads the season number from a season name like "Staffel 6" or "Staffel 06" — the seasons
    /// have no numeric column, their number only exists inside the name (or "Specials" for
    /// season 0).
    /// </summary>
    /// <param name="seasonName">The season name, or <see langword="null"/>.</param>
    /// <returns>The parsed season number, or <see langword="null"/> when the name carries no digits.</returns>
    public static int? SeasonNumberFromName(string? seasonName)
    {
        if (string.IsNullOrWhiteSpace(seasonName))
            return null;

        var digits = 0;
        var found = false;
        foreach (var c in seasonName)
        {
            if (char.IsDigit(c))
            {
                digits = digits * 10 + (c - '0');
                found = true;
            }
        }

        return found ? digits : null;
    }

    /// <summary>
    /// Builds the admin-card sub-line for an episode broadcast, e.g.
    /// "Staffel 6, Episode 42 — „Die alte Försterei"", "Staffel 1, Episode 3" or "Folge 112"
    /// when no season name is known.
    /// </summary>
    /// <param name="seasonName">The season name, or <see langword="null"/>.</param>
    /// <param name="episodeNumber">The episode number, or <see langword="null"/>.</param>
    /// <param name="episodeName">The episode title, appended in quotes when present.</param>
    /// <returns>The formatted sub-line, or <see langword="null"/> when nothing is known.</returns>
    public static string? Subtitle(string? seasonName, int? episodeNumber, string? episodeName)
    {
        var lead = (seasonName, episodeNumber) switch
        {
            ({ Length: > 0 } season, { } number) => $"{season}, Episode {number}",
            ({ Length: > 0 } season, null) => season,
            (null, { } number) => $"Folge {number}",
            _ => null
        };
        if (lead is null)
            return null;

        return string.IsNullOrWhiteSpace(episodeName) ? lead : $"{lead} — „{episodeName}“";
    }

    /// <summary>
    /// Builds the compact-card sub-line for an episode broadcast, e.g. "S6 E42", or "E42" when
    /// no season number could be derived.
    /// </summary>
    /// <param name="seasonNumber">The parsed season number (see <see cref="SeasonNumberFromName"/>).</param>
    /// <param name="episodeNumber">The episode number.</param>
    /// <returns>The formatted short sub-line, or <see langword="null"/> when nothing is known.</returns>
    public static string? ShortSubtitle(int? seasonNumber, int? episodeNumber)
        => (seasonNumber, episodeNumber) switch
        {
            ({ } season, { } number) => $"S{season} E{number}",
            (null, { } number) => $"E{number}",
            ({ } season, null) => $"S{season}",
            _ => null
        };

    /// <summary>
    /// Picks the first entry of a comma-separated genre name list (as stored by the NFO import),
    /// for the genre badge on the full card.
    /// </summary>
    /// <param name="genreNames">The comma-separated genre names, or <see langword="null"/>.</param>
    /// <returns>The first genre name, or <see langword="null"/> when the list is empty.</returns>
    public static string? FirstGenre(string? genreNames)
    {
        if (string.IsNullOrWhiteSpace(genreNames))
            return null;

        var first = genreNames.Split(',')[0].Trim();
        return first.Length == 0 ? null : first;
    }
}
