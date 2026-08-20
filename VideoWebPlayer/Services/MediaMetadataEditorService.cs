using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services;

/// <summary>
/// Saves user-edited media metadata and keeps genre values normalized.
/// </summary>
public sealed class MediaMetadataEditorService
{
    private const int MaxNameLength = 512;
    private const int MaxPlotLength = 10000;
    private readonly ApplicationDbContext _db;
    private readonly IBackgroundProcessingGate? _backgroundGate;
    private readonly IMediaMetadataWriteCoordinator? _writeCoordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaMetadataEditorService"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    public MediaMetadataEditorService(
        ApplicationDbContext db,
        IBackgroundProcessingGate? backgroundGate = null,
        IMediaMetadataWriteCoordinator? writeCoordinator = null)
    {
        _db = db;
        _backgroundGate = backgroundGate;
        _writeCoordinator = writeCoordinator;
    }

    /// <summary>
    /// Gets the distinct genre names used by the genre administration page.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The available genre options.</returns>
    public async Task<List<DtoGenreOption>> GetGenreOptionsAsync(CancellationToken cancellationToken = default)
    {
        var genres = await _db.Genres
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return genres
            .Where(g => !string.IsNullOrWhiteSpace(g.Name))
            .GroupBy(g => g.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(g => g.Name)
            .Select(g => new DtoGenreOption { Id = g.Id, Name = g.Name.Trim() })
            .ToList();
    }

    /// <summary>
    /// Updates metadata for one supported media object and marks it as manually edited.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task UpdateAsync(MediaMetadataUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await using var processingLease = _backgroundGate is null
            ? null
            : await _backgroundGate.EnterOperationAsync("Metadaten speichern", cancellationToken);
        await using var writeLease = _writeCoordinator is null
            ? null
            : await _writeCoordinator.EnterAsync(cancellationToken);

        ValidateCommon(request);

        switch (NormalizeType(request.ObjectType))
        {
            case "movie":
                await UpdateMovieAsync(request, cancellationToken);
                break;
            case "moviecollection":
                await UpdateMovieCollectionAsync(request, cancellationToken);
                break;
            case "tvshow":
                await UpdateTVShowAsync(request, cancellationToken);
                break;
            case "tvshowseason":
                await UpdateTVShowSeasonAsync(request, cancellationToken);
                break;
            case "tvshowepisode":
                await UpdateTVShowEpisodeAsync(request, cancellationToken);
                break;
            default:
                throw new ArgumentException("Unbekannter Medientyp.");
        }
    }

    private async Task UpdateMovieAsync(MediaMetadataUpdateRequest request, CancellationToken cancellationToken)
    {
        ValidateReleaseDateOnly(request, "Filme");

        var movie = await _db.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Film nicht gefunden.");

        movie.Name = request.Name.Trim();
        movie.ReleaseDate = NormalizeDate(request.ReleaseDate);
        movie.Plot = NormalizeText(request.Plot);
        movie.IsManuallyEdited = true;
        await ApplyMovieGenresAsync(movie, request.GenreNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateMovieCollectionAsync(MediaMetadataUpdateRequest request, CancellationToken cancellationToken)
    {
        ValidateReleaseDateOnly(request, "Filmsammlungen");
        ValidateNoPlotOrGenres(request, "Filmsammlungen besitzen keine eigenen Plot- oder Genre-Felder.");

        var collection = await _db.MovieCollections
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Filmsammlung nicht gefunden.");

        collection.Name = request.Name.Trim();
        collection.ReleaseDate = NormalizeDate(request.ReleaseDate);
        collection.IsManuallyEdited = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateTVShowAsync(MediaMetadataUpdateRequest request, CancellationToken cancellationToken)
    {
        ValidateReleaseDateOnly(request, "Serien");

        var show = await _db.TVShows
            .Include(s => s.TVShowGenres)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Serie nicht gefunden.");

        show.Name = request.Name.Trim();
        show.ReleaseDate = NormalizeDate(request.ReleaseDate);
        show.Plot = NormalizeText(request.Plot);
        show.IsManuallyEdited = true;
        await ApplyTVShowGenresAsync(show, request.GenreNames, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateTVShowSeasonAsync(MediaMetadataUpdateRequest request, CancellationToken cancellationToken)
    {
        ValidatePremieredAtOnly(request, "Staffeln");
        ValidateNoPlotOrGenres(request, "Staffeln besitzen keine eigenen Plot- oder Genre-Felder.");

        var season = await _db.TVShowSeasons
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Staffel nicht gefunden.");

        season.Name = request.Name.Trim();
        season.PremieredAt = NormalizeDate(request.PremieredAt);
        season.IsManuallyEdited = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateTVShowEpisodeAsync(MediaMetadataUpdateRequest request, CancellationToken cancellationToken)
    {
        ValidateReleaseDateOnly(request, "Episoden");

        if (request.GenreNames.Length > 0)
            throw new ArgumentException("Episoden besitzen keine eigenen Genre-Felder.");

        var episode = await _db.TVShowEpisodes
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Episode nicht gefunden.");

        episode.Name = request.Name.Trim();
        episode.ReleaseDate = NormalizeDate(request.ReleaseDate);
        episode.Plot = NormalizeText(request.Plot);
        episode.IsManuallyEdited = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyMovieGenresAsync(Movie movie, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var genres = await NormalizeGenresAsync(names, movie.MediaSourceId, cancellationToken);
        var genreIds = genres.Select(g => g.Id).ToHashSet();

        movie.GenreNames = string.Join(",", genres.Select(g => g.Name));
        _db.MovieGenres.RemoveRange(movie.MovieGenres.Where(mg => !genreIds.Contains(mg.GenreId)));
        foreach (var genre in genres.Where(g => movie.MovieGenres.All(mg => mg.GenreId != g.Id)))
            movie.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
    }

    private async Task ApplyTVShowGenresAsync(TVShow show, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var genres = await NormalizeGenresAsync(names, show.MediaSourceId, cancellationToken);
        var genreIds = genres.Select(g => g.Id).ToHashSet();

        show.GenreNames = string.Join(",", genres.Select(g => g.Name));
        _db.TVShowGenres.RemoveRange(show.TVShowGenres.Where(tg => !genreIds.Contains(tg.GenreId)));
        foreach (var genre in genres.Where(g => show.TVShowGenres.All(tg => tg.GenreId != g.Id)))
            show.TVShowGenres.Add(new TVShowGenre { TVShowId = show.Id, GenreId = genre.Id });
    }

    private async Task<List<Genre>> NormalizeGenresAsync(IEnumerable<string> names, long mediaSourceId, CancellationToken cancellationToken)
    {
        var requestedNames = names
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingGenres = await _db.Genres
            .Where(g => g.MediaSourceId == mediaSourceId)
            .ToListAsync(cancellationToken);

        var result = new List<Genre>();
        foreach (var name in requestedNames)
        {
            var genre = existingGenres.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (genre is null)
            {
                genre = new Genre { Name = name, MediaSourceId = mediaSourceId };
                _db.Genres.Add(genre);
                existingGenres.Add(genre);
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (!result.Any(g => g.Name.Equals(genre.Name, StringComparison.OrdinalIgnoreCase)))
                result.Add(genre);
        }

        return result;
    }

    private static void ValidateCommon(MediaMetadataUpdateRequest request)
    {
        if (request.Id <= 0)
            throw new ArgumentException("Ungueltige ID.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Der Titel darf nicht leer sein.");

        if (request.Name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Der Titel darf maximal {MaxNameLength} Zeichen lang sein.");

        if (request.Plot?.Length > MaxPlotLength)
            throw new ArgumentException($"Der Plot darf maximal {MaxPlotLength} Zeichen lang sein.");
    }

    private static void ValidateNoPlotOrGenres(MediaMetadataUpdateRequest request, string message)
    {
        if (!string.IsNullOrWhiteSpace(request.Plot) || request.GenreNames.Length > 0)
            throw new ArgumentException(message);
    }

    private static void ValidateReleaseDateOnly(MediaMetadataUpdateRequest request, string objectTypeLabel)
    {
        if (request.PremieredAt.HasValue)
            throw new ArgumentException($"{objectTypeLabel} duerfen kein PremieredAt-Datum speichern.");
    }

    private static void ValidatePremieredAtOnly(MediaMetadataUpdateRequest request, string objectTypeLabel)
    {
        if (request.ReleaseDate.HasValue)
            throw new ArgumentException($"{objectTypeLabel} duerfen kein ReleaseDate-Datum speichern.");
    }

    private static string NormalizeType(string type)
        => type.Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).Trim().ToLowerInvariant();

    private static DateTime? NormalizeDate(DateTime? value)
        => value?.Date;

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
