using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
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
/// HTTP-level tests for the public bootstrap endpoint and the refresh/logout endpoints.
/// </summary>
public sealed class PairingBootstrapEndpointsTests : IAsyncLifetime
{
    private const string MauiGateKey = "test-maui-api-token";
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private HttpClient _client = null!;

    public PairingBootstrapEndpointsTests()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-bootstrap-endpoints");
        _factory = PairingWebApplicationFactory.Create(_dbPath, builder =>
        {
            builder.UseSetting("AutoUpdate:HostedServicesEnabled", "false");
        });
    }

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file may still be locked */ }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Bootstrap_HappyPath_ReturnsEncryptedTrio_AndRefreshWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync("bootstrap-http@test.com");
        var created = await CreateTicketAsync(user.Id);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await _client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "HTTP-Geraet"
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PairingBootstrapResponse>(ct);
        Assert.NotNull(body);
        var json = PairingCryptoHelper.DecryptToken(clientKey, body!.ServerPublicKey, body.EncryptedPayload);
        var payload = System.Text.Json.JsonSerializer.Deserialize<PairingBootstrapPayload>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.DeviceToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token);
        Assert.Equal(user.Id, jwt.Subject);

        // Device-Token funktioniert als Gate-Key, Refresh-Token rotiert die Session.
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("X-API-Key", payload.DeviceToken);
        refreshRequest.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = payload.RefreshToken });
        var refreshResponse = await _client.SendAsync(refreshRequest, ct);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<RefreshTokenResponse>(ct);
        Assert.False(string.IsNullOrWhiteSpace(refreshed?.Token));
        Assert.False(string.IsNullOrWhiteSpace(refreshed?.RefreshToken));
        Assert.NotEqual(payload.RefreshToken, refreshed!.RefreshToken);
    }

    [Fact]
    public async Task Bootstrap_ShortCodeAlias_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync("bootstrap-alias@test.com");
        var created = await CreateTicketAsync(user.Id);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await _client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = created.ShortCode,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_UnknownTicket_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await _client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = "unbekanntes-ticket",
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var message = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Ungültiges oder abgelaufenes Pairing-Ticket.", message);
    }

    [Fact]
    public async Task Bootstrap_InvalidRequest_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = "",
            ClientPublicKey = ""
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Ungültiger Bootstrap-Request.", message);
    }

    [Fact]
    public async Task Bootstrap_InvalidClientKey_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync("bootstrap-badkey@test.com");
        var created = await CreateTicketAsync(user.Id);

        var response = await _client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = "kein-gueltiges-base64!!!"
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Das Ticket bleibt bei einem Formatfehler einloesbar.
        var stored = await GetPairingCodeAsync(created.Ticket);
        Assert.Null(stored!.ConsumedAtUtc);
    }

    [Fact]
    public async Task Bootstrap_ConsumedTicket_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync("bootstrap-consumed@test.com");
        var created = await CreateTicketAsync(user.Id);
        var request = new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo())
        };

        var first = await _client.PostAsJsonAsync("/api/pairing/bootstrap", request, ct);
        var second = await _client.PostAsJsonAsync("/api/pairing/bootstrap", request, ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Exchange_AdminCode_StillWorksUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var pairing = scope.ServiceProvider.GetRequiredService<IPairingService>();
        var adminCode = (await pairing.CreatePairingCodeAsync("admin-id", ct)).Code;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await _client.PostAsJsonAsync("/api/pairing/exchange", new PairingExchangeRequest
        {
            Code = adminCode,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "Admin-Geraet"
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PairingExchangeResponse>(ct);
        var deviceToken = PairingCryptoHelper.DecryptToken(clientKey, body!.ServerPublicKey, body.EncryptedToken);
        Assert.False(string.IsNullOrWhiteSpace(deviceToken));
    }

    [Fact]
    public async Task BootstrapTicket_IsNotRedeemableViaExchange()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync("bootstrap-exchange@test.com");
        var created = await CreateTicketAsync(user.Id);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await _client.PostAsJsonAsync("/api/pairing/exchange", new PairingExchangeRequest
        {
            Code = created.ShortCode,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("X-API-Key", MauiGateKey);
        request.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = "unbekannt" });
        var response = await _client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutGateKey_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = "egal" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_EmptyRequest_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("X-API-Key", MauiGateKey);
        request.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = "" });
        var response = await _client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await CreateUserAsync("bootstrap-logout@test.com");
        var created = await CreateTicketAsync(user.Id);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var bootstrapResponse = await _client.PostAsJsonAsync("/api/pairing/bootstrap", new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);
        var body = await bootstrapResponse.Content.ReadFromJsonAsync<PairingBootstrapResponse>(ct);
        var json = PairingCryptoHelper.DecryptToken(clientKey, body!.ServerPublicKey, body.EncryptedPayload);
        var payload = System.Text.Json.JsonSerializer.Deserialize<PairingBootstrapPayload>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("X-API-Key", payload.DeviceToken);
        logoutRequest.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = payload.RefreshToken });
        var logoutResponse = await _client.SendAsync(logoutRequest, ct);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("X-API-Key", payload.DeviceToken);
        refreshRequest.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = payload.RefreshToken });
        var refreshResponse = await _client.SendAsync(refreshRequest, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsAdmin = false
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        return user;
    }

    private async Task<CreatedBootstrapTicket> CreateTicketAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IPairingBootstrapService>();
        var created = await bootstrap.CreateBootstrapTicketAsync(userId, isAdmin: false);
        Assert.True(created.Success);
        return created;
    }

    private async Task<PairingCode?> GetPairingCodeAsync(string ticket)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hash = VideoWebPlayer.Services.Security.HashHelper.Sha256Hex(ticket);
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.PairingCodes, c => c.TicketHash == hash || c.CodeHash == hash);
    }
}
