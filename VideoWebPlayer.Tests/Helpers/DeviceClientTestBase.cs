using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared setup for the tests that drive <see cref="VideoWebPlayerClient"/> as a paired device against a
/// running application: the pairing test host (<see cref="PairingWebApplicationFactory"/>), a client whose
/// requests are recorded, and the QR bootstrap performed through the library itself.
/// </summary>
public abstract class DeviceClientTestBase : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    /// <summary>
    /// Creates the test host with the shared pairing test settings (temp SQLite file, generated JWT key,
    /// the three API gate keys).
    /// </summary>
    protected DeviceClientTestBase()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-device-client");
        _factory = PairingWebApplicationFactory.Create(_dbPath, builder =>
            builder.UseSetting("AutoUpdate:HostedServicesEnabled", "false"));
    }

    /// <summary>The client under test, talking to the in-process application.</summary>
    protected VideoWebPlayerClient Client { get; private set; } = null!;

    /// <summary>Every request the client sent, in the order they left the client.</summary>
    protected RecordingHttpHandler Requests { get; private set; } = null!;

    /// <summary>The application's service provider, for seeding and for checking server state.</summary>
    protected IServiceProvider Services => _factory.Services;

    /// <inheritdoc />
    public ValueTask InitializeAsync()
    {
        Requests = new RecordingHttpHandler();
        var httpClient = _factory.CreateDefaultClient(Requests);
        Client = new VideoWebPlayerClient(httpClient, NullLogger<VideoWebPlayerClient>.Instance);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file may still be locked */ }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a plain HTTP client against the in-process application, for tests that need a second,
    /// independent client instance next to <see cref="Client"/>.
    /// </summary>
    /// <returns>The new HTTP client.</returns>
    protected HttpClient CreateHttpClient() => _factory.CreateClient();

    /// <summary>
    /// Creates a confirmed user.
    /// </summary>
    /// <param name="email">The user's email address, also used as user name.</param>
    /// <returns>The created user.</returns>
    protected async Task<ApplicationUser> CreateUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>
    /// Creates a bootstrap ticket for the given user, as the profile page's QR code carries it.
    /// </summary>
    /// <param name="userId">The id of the user the ticket belongs to.</param>
    /// <returns>The ticket secret.</returns>
    protected async Task<string> CreateBootstrapTicketAsync(string userId)
    {
        using var scope = Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IPairingBootstrapService>();
        var created = await bootstrap.CreateBootstrapTicketAsync(userId, isAdmin: false);
        Assert.True(created.Success);
        return created.Ticket;
    }

    /// <summary>
    /// Runs the full QR bootstrap through <see cref="VideoWebPlayerClient.PairingBootstrapAsync"/>,
    /// decrypts the answer with the client's own ECDH key and applies device token, refresh token and
    /// bearer token to <see cref="Client"/> — the state a paired device is in.
    /// </summary>
    /// <param name="ticket">The bootstrap ticket to redeem.</param>
    /// <param name="deviceName">The display name to register the device under.</param>
    /// <returns>The decrypted bootstrap payload.</returns>
    protected async Task<PairingBootstrapPayload> PairDeviceAsync(string ticket, string deviceName = "Testgerät")
    {
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var response = await Client.PairingBootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = ticket,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = deviceName
        });

        var json = PairingCryptoHelper.DecryptToken(clientKey, response.ServerPublicKey, response.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<PairingBootstrapPayload>(json, JsonOptions);
        Assert.NotNull(payload);

        Client.DeviceToken = payload!.DeviceToken;
        Client.DeviceRefreshToken = payload.RefreshToken;
        Client.SetAuthorizationToken(new AuthorizationToken { token = payload.Token, expires = payload.Expires });
        return payload;
    }

    /// <summary>
    /// Creates a user, pairs a device for them via the QR bootstrap and returns both.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <returns>The created user and the decrypted bootstrap payload.</returns>
    /// <!-- Tupel-Elemente: User, Payload -->
    protected async Task<(ApplicationUser User, PairingBootstrapPayload Payload)> CreateUserAndPairDeviceAsync(string email)
    {
        var user = await CreateUserAsync(email);
        var ticket = await CreateBootstrapTicketAsync(user.Id);
        var payload = await PairDeviceAsync(ticket);
        return (user, payload);
    }

    /// <summary>
    /// Adds a media source the given user may read plus two movies in it, the minimum a playlist with
    /// playable entries needs.
    /// </summary>
    /// <param name="userId">The id of the user to grant read access to.</param>
    /// <returns>The ids of the two created movies.</returns>
    /// <!-- Tupel-Elemente: FirstMovieId, SecondMovieId -->
    protected async Task<(long FirstMovieId, long SecondMovieId)> SeedTwoAccessibleMoviesAsync(string userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource { Name = $"Geräte-Quelle {Guid.NewGuid()}", Path = "/device", Host = "localhost", Port = 22, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = userId });

        var first = new Movie { Name = "Geräte-Film 1", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow, ReleaseDate = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var second = new Movie { Name = "Geräte-Film 2", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow, ReleaseDate = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        db.Movies.AddRange(first, second);
        await db.SaveChangesAsync();

        return (first.Id, second.Id);
    }

    /// <summary>
    /// Creates a playlist holding the two given movies through the client library.
    /// </summary>
    /// <param name="name">The name of the playlist to create.</param>
    /// <param name="firstMovieId">The id of the first movie to add.</param>
    /// <param name="secondMovieId">The id of the second movie to add.</param>
    /// <returns>The playlist id and the ids of the two created entries.</returns>
    /// <!-- Tupel-Elemente: PlaylistId, FirstEntryId, SecondEntryId -->
    protected async Task<(long PlaylistId, long FirstEntryId, long SecondEntryId)> CreatePlaylistWithTwoMoviesAsync(
        string name, long firstMovieId, long secondMovieId)
    {
        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest { Name = name, SortMode = "ByReleaseDate" });
        var first = await Client.AddMediaToPlaylistAsync(playlist.Id, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = firstMovieId });
        var second = await Client.AddMediaToPlaylistAsync(playlist.Id, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = secondMovieId });
        Assert.NotNull(first.TopLevelEntry);
        Assert.NotNull(second.TopLevelEntry);
        return (playlist.Id, first.TopLevelEntry!.Id, second.TopLevelEntry!.Id);
    }

    /// <summary>
    /// Replaces the bearer token with an unusable one, so the next request is answered with 401 exactly
    /// as it would be after the session expired.
    /// </summary>
    protected void ExpireSession()
        => Client.SetAuthorizationToken(new AuthorizationToken { token = "abgelaufene-sitzung", expires = DateTime.UtcNow.AddHours(-1) });

    /// <summary>
    /// Revokes the paired device on the server, the way an administrator does on the devices page.
    /// </summary>
    /// <param name="deviceToken">The plaintext device token to revoke.</param>
    protected async Task RevokeDeviceAsync(string deviceToken)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hash = VideoWebPlayer.Services.Security.HashHelper.Sha256Hex(deviceToken);
        var device = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstAsync(db.PairedDevices, d => d.TokenHash == hash);

        var deviceTokens = scope.ServiceProvider.GetRequiredService<IDeviceTokenService>();
        Assert.True(await deviceTokens.RevokeAsync(device.Id));
    }
}

/// <summary>
/// Records method, path and the <c>X-API-Key</c> header of every request the client sends, so tests can
/// show which calls really left the library (e.g. how often the session was renewed).
/// </summary>
public sealed class RecordingHttpHandler : DelegatingHandler
{
    private readonly ConcurrentQueue<(string Method, string Path, string? ApiKey)> _entries = new();

    // All recorded requests, oldest first (Tupel-Elemente: Method, Path, ApiKey).
    public IReadOnlyList<(string Method, string Path, string? ApiKey)> Entries => _entries.ToArray();

    /// <summary>
    /// Counts the recorded requests to a path.
    /// </summary>
    /// <param name="path">The absolute request path to count, e.g. <c>/api/auth/refresh</c>.</param>
    /// <returns>The number of requests sent to that path.</returns>
    public int CountTo(string path)
        => _entries.Count(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = request.Headers.TryGetValues("X-API-Key", out var values) ? string.Join(",", values) : null;
        _entries.Enqueue((request.Method.Method, request.RequestUri?.AbsolutePath ?? string.Empty, apiKey));
        return base.SendAsync(request, cancellationToken);
    }
}
