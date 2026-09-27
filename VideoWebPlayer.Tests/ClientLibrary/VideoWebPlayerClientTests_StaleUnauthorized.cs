using System.Net;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// A 401 that is only handled after another request has already renewed the session answers a session
/// that no longer exists. It must lead to a repetition, not to a second renewal. Reproduced here without
/// any waiting time: a message handler holds the second 401 back until the renewal of the first request
/// is provably through, so the order is fixed instead of depending on timing.
/// </summary>
public sealed class VideoWebPlayerClientTests_StaleUnauthorized : DeviceClientTestBase
{
    private readonly DeferSecondUnauthorizedHandler _handler = new("/api/playlists");

    /// <inheritdoc />
    protected override DelegatingHandler[] CreateExtraHandlers() => [_handler];

    [Fact]
    public async Task UnauthorizedHandledAfterAnotherRenewal_RepeatsWithoutRenewingAgain()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"stale-401-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        await CreatePlaylistWithTwoMoviesAsync("Veraltete-401-Playlist", firstMovieId, secondMovieId);

        _handler.Arm(TestContext.Current.CancellationToken);
        ExpireSession();
        var first = Client.RequestPlaylistsAsync();
        var second = Client.RequestPlaylistsAsync();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, playlists => Assert.Single(playlists));
        Assert.Equal(1, Requests.CountTo("/api/auth/refresh"));
    }
}

/// <summary>
/// Fixes the order of two requests to the watched path that both run into 401:
/// <list type="number">
/// <item><description>both originals are sent and are answered with 401,</description></item>
/// <item><description>the first 401 is passed through at once, the second one is held back,</description></item>
/// <item><description>the first request renews the session and sends its repetition — the third request
/// to that path, which proves the renewal is complete and the new session already applied,</description></item>
/// <item><description>only then is the second, now stale 401 handed over.</description></item>
/// </list>
/// The release is tied to an observed request, not to a waiting time, so the order is the same in every
/// run and on every machine.
/// </summary>
public sealed class DeferSecondUnauthorizedHandler : DelegatingHandler
{
    private readonly string _watchedPathPrefix;
    private readonly TaskCompletionSource _renewalApplied = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _watchedRequestsSeen;
    private bool _armed;
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Creates the handler for a request path.
    /// </summary>
    /// <param name="watchedPathPrefix">Path prefix whose answers are ordered, e.g. <c>/api/playlists</c>.</param>
    public DeferSecondUnauthorizedHandler(string watchedPathPrefix)
    {
        _watchedPathPrefix = watchedPathPrefix;
    }

    /// <summary>
    /// Starts ordering the answers. Called after the setup calls, so only the requests under test are
    /// affected.
    /// </summary>
    /// <param name="cancellationToken">Token that ends the waiting if the expected repetition never happens.</param>
    public void Arm(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _armed = true;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var isWatched = _armed && path.StartsWith(_watchedPathPrefix, StringComparison.OrdinalIgnoreCase);
        var position = isWatched ? Interlocked.Increment(ref _watchedRequestsSeen) : 0;

        // The third request to the watched path is the repetition of the first one: it carries the
        // renewed session, so the renewal is provably finished and applied.
        if (position == 3)
            _renewalApplied.TrySetResult();

        var response = await base.SendAsync(request, cancellationToken);

        if (position == 2 && response.StatusCode == HttpStatusCode.Unauthorized)
            await _renewalApplied.Task.WaitAsync(_cancellationToken);

        return response;
    }
}
