using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Global genre configuration inheritance. <see cref="Genre"/> rows are per media source,
/// but the admin page (<c>GenreAdmin</c>) manages them globally per name
/// (case-insensitive). This helper locates the configured template row for a name in
/// other media sources and applies its configuration — so newly created rows (scan or
/// metadata edit) and untouched default rows inherit the global admin intent.
/// </summary>
internal static class GenreGlobalConfig
{
    /// <summary>
    /// Whether the row carries explicit admin configuration (hidden, seasonal window or
    /// effective alternate names). Rows in the untouched default state may be healed from
    /// a configured template; configured rows are never overwritten.
    /// </summary>
    /// <param name="genre">The genre row to inspect (with <see cref="Genre.AlternateNames"/> loaded).</param>
    /// <returns><see langword="true"/> when the row was configured by the admin.</returns>
    public static bool HasCustomConfig(Genre genre)
        => genre.IsHidden
            || genre.StartDate is not null
            || genre.EndDate is not null
            || GenreCanonicalResolver.HasEffectiveAlternates(genre);

    /// <summary>
    /// Loads the configured templates from other media sources: per genre name (trimmed,
    /// case-insensitive) the oldest configured row outside <paramref name="mediaSourceId"/>.
    /// The admin applies configuration to all same-named rows at once, so configured rows
    /// are mutually consistent and the oldest one is a stable proxy for the admin intent.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="mediaSourceId">The media source being worked on (excluded from templates).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Name key (<c>Name.Trim()</c>, case-insensitive) → template row.</returns>
    public static async Task<Dictionary<string, Genre>> LoadConfiguredTemplatesAsync(
        ApplicationDbContext db, long mediaSourceId, CancellationToken cancellationToken)
    {
        var candidates = await db.Genres
            .Include(g => g.AlternateNames)
            .Where(g => g.MediaSourceId != mediaSourceId)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(g => !string.IsNullOrWhiteSpace(g.Name) && HasCustomConfig(g))
            .GroupBy(g => g.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.MinBy(x => x.Id)!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies <see cref="Genre.IsHidden"/>, <see cref="Genre.StartDate"/>,
    /// <see cref="Genre.EndDate"/> and the alternate names from
    /// <paramref name="template"/> onto <paramref name="target"/>. The alternate names are
    /// replaced completely; works for new (not yet added) and already tracked rows.
    /// Does not call <c>SaveChanges</c>.
    /// </summary>
    /// <param name="db">Application database context (used to remove old <see cref="GenreName"/> rows).</param>
    /// <param name="target">The genre row receiving the configuration.</param>
    /// <param name="template">The configured template row.</param>
    public static void ApplyConfig(ApplicationDbContext db, Genre target, Genre template)
    {
        target.IsHidden = template.IsHidden;
        target.StartDate = template.StartDate;
        target.EndDate = template.EndDate;

        var names = (template.AlternateNames ?? Enumerable.Empty<GenreName>())
            .Select(a => a.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var alternateNames = target.AlternateNames ??= new List<GenreName>();
        db.GenreNames.RemoveRange(alternateNames.ToList());
        alternateNames.Clear();

        foreach (var name in names)
        {
            var alternate = new GenreName { Name = name! };
            if (target.Id != 0)
                alternate.GenreId = target.Id;
            alternateNames.Add(alternate);
        }
    }
}
