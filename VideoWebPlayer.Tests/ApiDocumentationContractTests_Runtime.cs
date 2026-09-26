using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Runtime half of the <c>docs/API.md</c> contract for the areas added by the playlist feature and the QR
/// bootstrap: the documented playlist walkthrough (sign in, create a playlist, add titles, start playback,
/// fetch the next title) and the documented session endpoints are executed against the running application,
/// so the document is not just complete on paper. The pure document check lives in
/// <see cref="ApiDocumentationContractTests"/>.
/// </summary>
public sealed class ApiDocumentationContractTests_Runtime : IDisposable
{
    private const string MauiGateKey = "test-maui-api-token";
    private const string Password = "ApiContract123!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    /// <summary>
    /// Creates the test host with the shared pairing/API test settings (temp SQLite file, generated JWT key,
    /// the three API gate keys).
    /// </summary>
    public ApiDocumentationContractTests_Runtime()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-api-contract-runtime");
        _factory = PairingWebApplicationFactory.Create(_dbPath, builder =>
            builder.UseSetting("AutoUpdate:HostedServicesEnabled", "false"));
    }

    /// <summary>
    /// The walkthrough <c>docs/API.md</c> promises for a client app: sign in, create a playlist, add titles,
    /// start playback and fetch the next title — each step with the documented status code and response
    /// shape.
    /// </summary>
    [Fact]
    public async Task PlaylistWalkthrough_SignInCreateAddPlayNext_MatchesDocumentedContract()
    {
        var ct = TestContext.Current.CancellationToken;
        var (email, firstMovieId, secondMovieId) = await SeedUserWithTwoAccessibleMoviesAsync();
        using var client = CreateClient();

        var token = await LoginAsync(client, email, ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync(
            "/api/playlists",
            new DtoCreatePlaylistRequest { Name = "Vertragstest-Playlist", SortMode = "ByReleaseDate" },
            ct);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var playlist = await createResponse.Content.ReadFromJsonAsync<DtoPlaylist>(JsonOptions, ct);
        Assert.NotNull(playlist);
        Assert.True(playlist!.Id > 0);

        var firstEntryId = await AddMovieAsync(client, playlist.Id, firstMovieId, ct);
        var secondEntryId = await AddMovieAsync(client, playlist.Id, secondMovieId, ct);
        Assert.NotEqual(firstEntryId, secondEntryId);

        var playResponse = await client.PostAsync($"/api/playlists/{playlist.Id}/play", null, ct);
        Assert.Equal(HttpStatusCode.OK, playResponse.StatusCode);
        var playback = await playResponse.Content.ReadFromJsonAsync<DtoPlaylistPlaybackStart>(JsonOptions, ct);
        Assert.NotNull(playback);
        Assert.Equal(playlist.Id, playback!.PlaylistId);
        Assert.Equal(2, playback.TotalCount);
        Assert.Equal(1, playback.CurrentPosition);
        Assert.Equal(firstEntryId, playback.CurrentEntryId);
        Assert.Equal($"/api/items/movie/{firstMovieId}/stream", playback.StreamUrl);

        var nextResponse = await client.PostAsync(
            $"/api/playlists/{playlist.Id}/play/next?currentEntryId={playback.CurrentEntryId}", null, ct);
        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
        var next = await nextResponse.Content.ReadFromJsonAsync<DtoPlaylistNavigationResult>(JsonOptions, ct);
        Assert.NotNull(next);
        Assert.Equal(secondEntryId, next!.Entry.Id);
        Assert.Equal(2, next.Position);

        var afterLastResponse = await client.PostAsync(
            $"/api/playlists/{playlist.Id}/play/next?currentEntryId={next.Entry.Id}", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, afterLastResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/playlists/{playlist.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    /// <summary>
    /// The session endpoints of the QR bootstrap as documented: <c>POST /api/pairing/bootstrap</c> hands out
    /// the credential trio, <c>POST /api/auth/refresh</c> rotates the session and
    /// <c>POST /api/auth/logout</c> revokes the refresh token, after which a further refresh is refused.
    /// </summary>
    [Fact]
    public async Task SessionEndpoints_BootstrapRefreshLogout_MatchDocumentedContract()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync($"api-contract-{Guid.NewGuid():N}@example.com");
        string ticket;
        using (var scope = _factory.Services.CreateScope())
        {
            var bootstrapService = scope.ServiceProvider.GetRequiredService<IPairingBootstrapService>();
            ticket = (await bootstrapService.CreateBootstrapTicketAsync(user.Id, isAdmin: true, ct)).Ticket;
        }

        using var client = CreateClient();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var bootstrapResponse = await client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = ticket,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "Vertragstest-Geraet"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);
        var bootstrapBody = await bootstrapResponse.Content.ReadFromJsonAsync<PairingBootstrapResponse>(JsonOptions, ct);
        Assert.NotNull(bootstrapBody);
        var payload = JsonSerializer.Deserialize<PairingBootstrapPayload>(
            PairingCryptoHelper.DecryptToken(clientKey, bootstrapBody!.ServerPublicKey, bootstrapBody.EncryptedPayload),
            JsonOptions);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.DeviceToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));

        var refreshed = await PostWithGateKeyAsync<RefreshTokenResponse>(
            client, "/api/auth/refresh", payload.DeviceToken, payload.RefreshToken, HttpStatusCode.OK, ct);
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed!.Token));
        Assert.NotEqual(payload.RefreshToken, refreshed.RefreshToken);

        await PostWithGateKeyAsync<object>(
            client, "/api/auth/logout", payload.DeviceToken, refreshed.RefreshToken, HttpStatusCode.OK, ct);

        await PostWithGateKeyAsync<object>(
            client, "/api/auth/refresh", payload.DeviceToken, refreshed.RefreshToken, HttpStatusCode.Unauthorized, ct);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> LoginAsync(HttpClient client, string email, CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Add("X-API-Key", MauiGateKey);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthenticationRequest { Email = email, Password = Password },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthorizationToken>(JsonOptions, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token?.token));
        return token!.token;
    }

    private static async Task<long> AddMovieAsync(
        HttpClient client, long playlistId, long movieId, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/playlists/{playlistId}/entries",
            new DtoAddMediaToPlaylistRequest { MediaType = "Movie", MediaId = movieId },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DtoPlaylistAddResult>(JsonOptions, cancellationToken);
        Assert.NotNull(result);
        Assert.NotNull(result!.TopLevelEntry);
        Assert.Equal(0, result.SkippedDuplicateCount);
        return result.TopLevelEntry!.Id;
    }

    private static async Task<TResponse?> PostWithGateKeyAsync<TResponse>(
        HttpClient client,
        string route,
        string gateKey,
        string refreshToken,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Add("X-API-Key", gateKey);
        request.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = refreshToken });
        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(expectedStatusCode, response.StatusCode);
        if (expectedStatusCode != HttpStatusCode.OK || typeof(TResponse) == typeof(object))
            return null;

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors.Select(error => error.Description)));
        return user;
    }

    /// <summary>
    /// Creates a user, a media source the user may read and two movies in it — the minimum the documented
    /// playlist walkthrough needs, since playback only ever selects entries the requesting user has access to.
    /// </summary>
    /// <returns>The user's email address and the ids of the two movies.</returns>
    /// <!-- Tupel-Elemente: Email, FirstMovieId, SecondMovieId -->
    private async Task<(string Email, long FirstMovieId, long SecondMovieId)> SeedUserWithTwoAccessibleMoviesAsync()
    {
        var user = await CreateUserAsync($"api-contract-{Guid.NewGuid():N}@example.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource
        {
            Name = "Contract Source",
            Path = "/contract",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = user.Id });
        var firstMovie = new Movie
        {
            Name = "Vertragstest-Film 1",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow,
            ReleaseDate = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var secondMovie = new Movie
        {
            Name = "Vertragstest-Film 2",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow,
            ReleaseDate = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.Movies.AddRange(firstMovie, secondMovie);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (user.Email!, firstMovie.Id, secondMovie.Id);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file may still be locked */ }
    }
}
