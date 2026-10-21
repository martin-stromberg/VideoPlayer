using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Canonical genre-name resolution shared by the TV-channel genre material options and the
/// schedule expansion (<see cref="ProgramScheduleService"/>). It mirrors the scan-time mapping
/// of <c>MediaSourceClassifier.GetOrCreateGenresAsync</c>: a genre row with
/// <see cref="Genre.AlternateNames"/> forwards to the genres named by them — resolved within the
/// same media source, recursively and cycle-safe. A row without effective alternate names is
/// canonical under its own name, but only while visible (<see cref="Genre.IsHidden"/> plus the
/// seasonal <see cref="Genre.StartDate"/>/<see cref="Genre.EndDate"/> window, same semantics as
/// <see cref="GenreService.GetSeasonalGenresAsync"/>); invisible rows without alternates
/// contribute nothing.
/// </summary>
internal static class GenreCanonicalResolver
{
    /// <summary>
    /// Whether the genre row is visible at <paramref name="now"/>: not hidden and — only when
    /// both seasonal bounds are set — inside the yearly recurring window.
    /// </summary>
    /// <param name="genre">The genre row to check.</param>
    /// <param name="now">Reference instant for the seasonal window (use UTC date semantics).</param>
    /// <returns><see langword="true"/> when the genre may be selected/displayed.</returns>
    public static bool IsVisible(Genre genre, DateTime now)
    {
        if (genre.IsHidden)
            return false;

        if (genre.StartDate is null || genre.EndDate is null)
            return true;

        var start = genre.StartDate.Value;
        var end = genre.EndDate.Value;
        var date = now.Date;
        while (end < date)
        {
            start = start.AddYears(1);
            end = end.AddYears(1);
        }
        return start <= date && end >= date;
    }

    /// <summary>
    /// Whether the row has effective alternate names (non-empty, different from its own name) —
    /// such rows forward to their targets instead of being canonical themselves.
    /// </summary>
    /// <param name="genre">The genre row to check.</param>
    /// <returns><see langword="true"/> when the row redirects through alternate names.</returns>
    public static bool HasEffectiveAlternates(Genre genre)
        => EffectiveAlternateNames(genre).Count > 0;

    /// <summary>
    /// Resolves every genre row in <paramref name="genres"/> to the canonical names it feeds
    /// into. Rows with alternate names forward (same media source, recursive, cycle-safe);
    /// visible rows without alternates contribute their own name; invisible rows without
    /// alternates contribute nothing.
    /// </summary>
    /// <param name="genres">Genre rows including their <see cref="Genre.AlternateNames"/>.</param>
    /// <param name="now">Reference instant for the seasonal visibility window.</param>
    /// <returns>Genre row id → case-insensitive set of canonical names the row resolves to.</returns>
    public static Dictionary<long, HashSet<string>> ResolveCanonicalNames(IReadOnlyCollection<Genre> genres, DateTime now)
    {
        var bySourceAndName = genres
            .GroupBy(g => g.MediaSourceId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => (x.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                      .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase));

        var result = new Dictionary<long, HashSet<string>>();
        foreach (var genre in genres)
            result[genre.Id] = ResolveNames(genre, bySourceAndName, now, []);
        return result;
    }

    /// <summary>
    /// Builds the selectable genre options: the deduplicated names of all visible canonical
    /// (non-redirecting) rows in <paramref name="genres"/>. The option id is the smallest row id
    /// of that name, so the selection is deterministic and maps back to a real genre row.
    /// </summary>
    /// <param name="genres">Genre rows in the current scope (already source-filtered).</param>
    /// <param name="now">Reference instant for the seasonal visibility window.</param>
    /// <returns>Distinct canonical genre options ordered by name.</returns>
    public static List<DtoGenreOption> BuildOptions(IReadOnlyCollection<Genre> genres, DateTime now)
        => genres
            .Where(g => !string.IsNullOrWhiteSpace(g.Name) && !HasEffectiveAlternates(g) && IsVisible(g, now))
            .GroupBy(g => g.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new DtoGenreOption { Id = g.Min(x => x.Id), Name = g.Key })
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> EffectiveAlternateNames(Genre genre)
        => (genre.AlternateNames ?? Enumerable.Empty<GenreName>())
            .Select(a => a.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n)
                && !string.Equals(n, (genre.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()!;

    private static HashSet<string> ResolveNames(
        Genre genre,
        Dictionary<long, Dictionary<string, List<Genre>>> bySourceAndName,
        DateTime now,
        HashSet<long> visiting)
    {
        var alternateNames = EffectiveAlternateNames(genre);
        if (alternateNames.Count == 0)
        {
            return IsVisible(genre, now) && !string.IsNullOrWhiteSpace(genre.Name)
                ? new HashSet<string>([genre.Name.Trim()], StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (!visiting.Add(genre.Id))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase); // alternate-name cycle

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (bySourceAndName.TryGetValue(genre.MediaSourceId, out var byName))
        {
            foreach (var alternateName in alternateNames)
            {
                if (!byName.TryGetValue(alternateName, out var targets))
                    continue;
                foreach (var target in targets)
                    names.UnionWith(ResolveNames(target, bySourceAndName, now, [.. visiting]));
            }
        }
        return names;
    }
}
