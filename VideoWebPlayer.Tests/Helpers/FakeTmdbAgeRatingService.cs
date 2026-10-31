using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Test double for <see cref="ITmdbAgeRatingService"/>: returns preset ratings and
/// records the calls so tests can assert whether the online lookup ran at all.
/// </summary>
public sealed class FakeTmdbAgeRatingService : ITmdbAgeRatingService
{
    private readonly TmdbAgeRating? _movieRating;
    private readonly TmdbAgeRating? _tvShowRating;

    public FakeTmdbAgeRatingService(TmdbAgeRating? movieRating = null, TmdbAgeRating? tvShowRating = null)
    {
        _movieRating = movieRating;
        _tvShowRating = tvShowRating;
    }

    public int MovieCalls { get; private set; }

    public int TVShowCalls { get; private set; }

    public string? LastTitle { get; private set; }

    public int? LastYear { get; private set; }

    public string? LastTmdbId { get; private set; }

    public Task<TmdbAgeRating?> GetMovieAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken cancellationToken)
    {
        MovieCalls++;
        LastTitle = title;
        LastYear = year;
        LastTmdbId = tmdbId;
        return Task.FromResult(_movieRating);
    }

    public Task<TmdbAgeRating?> GetTVShowAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken cancellationToken)
    {
        TVShowCalls++;
        LastTitle = title;
        LastYear = year;
        LastTmdbId = tmdbId;
        return Task.FromResult(_tvShowRating);
    }
}
