using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class TmdbAgeRatingServiceTests
{
    [Fact]
    public async Task GetMovieAgeRatingAsync_PrefersConfiguredCountry()
    {
        var service = CreateService(JsonHandler(_ =>
            Ok("""{"results":[{"iso_3166_1":"US","release_dates":[{"certification":"R"}]},{"iso_3166_1":"DE","release_dates":[{"certification":"16"}]}]}""")),
            new TmdbOptions { Enabled = true, ApiKey = "key", PreferredCountry = "DE" });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.NotNull(rating);
        Assert.Equal("DE", rating!.Country);
        Assert.Equal("16", rating.Certification);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_FallsBackToUsThenFirst()
    {
        var service = CreateService(JsonHandler(_ =>
            Ok("""{"results":[{"iso_3166_1":"GB","release_dates":[{"certification":"12"}]},{"iso_3166_1":"US","release_dates":[{"certification":"PG-13"}]}]}""")),
            new TmdbOptions { Enabled = true, ApiKey = "key", PreferredCountry = "DE" });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.NotNull(rating);
        Assert.Equal("US", rating!.Country);
        Assert.Equal("PG-13", rating.Certification);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_UsesFirstEntryWithoutPreferredOrUs()
    {
        var service = CreateService(JsonHandler(_ =>
            Ok("""{"results":[{"iso_3166_1":"FR","release_dates":[{"certification":"12"}]}]}""")),
            new TmdbOptions { Enabled = true, ApiKey = "key", PreferredCountry = "DE" });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.NotNull(rating);
        Assert.Equal("FR", rating!.Country);
        Assert.Equal("12", rating.Certification);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_SearchesByTitleWhenNoTmdbId()
    {
        var handler = JsonHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("/search/movie")
                ? Ok("""{"results":[{"id":550}]}""")
                : Ok("""{"results":[{"iso_3166_1":"DE","release_dates":[{"certification":"12"}]}]}"""));
        var service = CreateService(handler, new TmdbOptions { Enabled = true, ApiKey = "key" });

        var rating = await service.GetMovieAgeRatingAsync("Fight Club", 1999, null, TestContext.Current.CancellationToken);

        Assert.NotNull(rating);
        Assert.Equal("DE", rating!.Country);
        Assert.Contains("/search/movie", handler.RequestUrls[0]);
        Assert.Contains("query=Fight%20Club", handler.RequestUrls[0]);
        Assert.Contains("year=1999", handler.RequestUrls[0]);
        Assert.Contains("/movie/550/release_dates", handler.RequestUrls[1]);
    }

    [Fact]
    public async Task GetTVShowAgeRatingAsync_UsesContentRatings()
    {
        var handler = JsonHandler(_ =>
            Ok("""{"results":[{"iso_3166_1":"US","rating":"TV-MA"},{"iso_3166_1":"DE","rating":"16"}]}"""));
        var service = CreateService(handler, new TmdbOptions { Enabled = true, ApiKey = "key", PreferredCountry = "DE" });

        var rating = await service.GetTVShowAgeRatingAsync("Show", 2021, "99", TestContext.Current.CancellationToken);

        Assert.NotNull(rating);
        Assert.Equal("DE", rating!.Country);
        Assert.Equal("16", rating.Certification);
        Assert.Contains("/tv/99/content_ratings", handler.RequestUrls[0]);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_Disabled_ReturnsNullWithoutRequest()
    {
        var handler = JsonHandler(_ => Ok("{}"));
        var service = CreateService(handler, new TmdbOptions { Enabled = false, ApiKey = "key" });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.Null(rating);
        Assert.Empty(handler.RequestUrls);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_NoApiKey_ReturnsNullWithoutRequest()
    {
        var handler = JsonHandler(_ => Ok("{}"));
        var service = CreateService(handler, new TmdbOptions { Enabled = true, ApiKey = null });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.Null(rating);
        Assert.Empty(handler.RequestUrls);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_HttpError_ReturnsNull()
    {
        var service = CreateService(
            JsonHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            new TmdbOptions { Enabled = true, ApiKey = "key" });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.Null(rating);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_InvalidJson_ReturnsNull()
    {
        var service = CreateService(
            JsonHandler(_ => Ok("kein json {")),
            new TmdbOptions { Enabled = true, ApiKey = "key" });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.Null(rating);
    }

    [Fact]
    public async Task GetMovieAgeRatingAsync_Timeout_ReturnsNull()
    {
        var handler = new TmdbStubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Ok("{}");
        });
        var service = CreateService(handler, new TmdbOptions { Enabled = true, ApiKey = "key", TimeoutSeconds = 1 });

        var rating = await service.GetMovieAgeRatingAsync("Film", 2020, "550", TestContext.Current.CancellationToken);

        Assert.Null(rating);
    }

    private static TmdbAgeRatingService CreateService(TmdbStubHandler handler, TmdbOptions options)
        => new(
            new StubHttpClientFactory(handler),
            Options.Create(options),
            NullLogger<TmdbAgeRatingService>.Instance);

    private static TmdbStubHandler JsonHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new((request, _) => Task.FromResult(responder(request)));

    private static HttpResponseMessage Ok(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class TmdbStubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public TmdbStubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        public List<string> RequestUrls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUrls.Add(request.RequestUri!.AbsoluteUri);
            return _responder(request, cancellationToken);
        }
    }
}
