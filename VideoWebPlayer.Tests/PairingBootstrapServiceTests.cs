using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Security;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingBootstrapServiceTests
{
    [Fact]
    public async Task CreateBootstrapTicket_RegularUser_SucceedsByDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-create-default", ct);
        var service = fixture.CreatePairingBootstrapService();

        var created = await service.CreateBootstrapTicketAsync("user-id", isAdmin: false, ct);

        Assert.True(created.Success);
        Assert.Equal(BootstrapTicketErrorKind.None, created.Error);
        // Langes Ticket: 192 Bit, Base64url ohne Padding.
        Assert.True(created.Ticket.Length >= 32);
        Assert.DoesNotContain('+', created.Ticket);
        Assert.DoesNotContain('/', created.Ticket);
        Assert.Equal(8, created.ShortCode.Length);
        Assert.True(created.ExpiresAtUtc > created.CreatedAtUtc);
        Assert.True(created.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(6));

        var stored = Assert.Single(await fixture.Db.PairingCodes.ToListAsync(ct));
        Assert.Equal(PairingCodeKind.BootstrapTicket, stored.Kind);
        Assert.Equal("user-id", stored.CreatedByUserId);
        Assert.Equal(HashHelper.Sha256Hex(created.Ticket), stored.TicketHash);
        Assert.Equal(HashHelper.Sha256Hex(created.ShortCode), stored.CodeHash);
        // Klartext darf niemals gespeichert werden.
        Assert.DoesNotContain(created.Ticket, stored.TicketHash ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain(created.ShortCode, stored.CodeHash, StringComparison.Ordinal);
        Assert.Null(stored.ConsumedAtUtc);
    }

    [Fact]
    public async Task CreateBootstrapTicket_AdminOnly_RejectsRegularUser()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-admin-only", ct);
        var settings = new Dictionary<string, string?> { ["Pairing:BootstrapAdminOnly"] = "true" };
        var service = fixture.CreatePairingBootstrapService(settings: settings);

        var rejected = await service.CreateBootstrapTicketAsync("user-id", isAdmin: false, ct);
        var allowed = await service.CreateBootstrapTicketAsync("admin-id", isAdmin: true, ct);

        Assert.Equal(BootstrapTicketErrorKind.Forbidden, rejected.Error);
        Assert.False(rejected.Success);
        Assert.True(allowed.Success);
        Assert.Single(await fixture.Db.PairingCodes.ToListAsync(ct));
    }

    [Fact]
    public async Task CreateBootstrapTicket_RateLimit_IsEnforcedPerUser()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-rate-limit", ct);
        var settings = new Dictionary<string, string?> { ["Pairing:BootstrapMaxTicketsPerHour"] = "2" };
        var service = fixture.CreatePairingBootstrapService(settings: settings);

        Assert.True((await service.CreateBootstrapTicketAsync("user-id", false, ct)).Success);
        Assert.True((await service.CreateBootstrapTicketAsync("user-id", false, ct)).Success);
        var limited = await service.CreateBootstrapTicketAsync("user-id", false, ct);
        // Anderer Benutzer hat ein eigenes Kontingent.
        var otherUser = await service.CreateBootstrapTicketAsync("other-user", false, ct);

        Assert.Equal(BootstrapTicketErrorKind.RateLimited, limited.Error);
        Assert.True(otherUser.Success);
        Assert.Equal(3, await fixture.Db.PairingCodes.CountAsync(ct));
    }

    [Fact]
    public async Task BootstrapAsync_LongTicket_ReturnsEncryptedTrio()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-flow-ticket", ct);
        var service = fixture.CreatePairingBootstrapService();
        var user = await fixture.CreateUserAsync("bootstrap@test.com");
        var created = await service.CreateBootstrapTicketAsync(user.Id, false, ct);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "Test-TV"
        }, ct);

        Assert.True(result.Success);
        var payload = DecryptPayload(clientKey, result);
        AssertTrio(payload, user);
        await AssertRefreshTokenRotatesAsync(fixture, payload, ct);
        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Equal("Test-TV", device.Name);
        Assert.Equal(user.Id, device.CreatedByUserId);
        Assert.Equal(HashHelper.Sha256Hex(payload.DeviceToken), device.TokenHash);
    }

    [Fact]
    public async Task BootstrapAsync_ShortCodeAlias_ResolvesSameTicket()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-flow-shortcode", ct);
        var service = fixture.CreatePairingBootstrapService();
        var user = await fixture.CreateUserAsync("bootstrap-short@test.com");
        var created = await service.CreateBootstrapTicketAsync(user.Id, false, ct);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = created.ShortCode,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.True(result.Success);
        var payload = DecryptPayload(clientKey, result);
        AssertTrio(payload, user);
    }

    [Fact]
    public async Task BootstrapAsync_Ticket_IsSingleUse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-single-use", ct);
        var service = fixture.CreatePairingBootstrapService();
        var user = await fixture.CreateUserAsync("bootstrap-single@test.com");
        var created = await service.CreateBootstrapTicketAsync(user.Id, false, ct);
        var request = new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo())
        };

        var first = await service.BootstrapAsync(request, ct);
        var second = await service.BootstrapAsync(request, ct);

        Assert.True(first.Success);
        Assert.Equal(BootstrapTicketErrorKind.InvalidTicket, second.Error);
    }

    [Fact]
    public async Task BootstrapAsync_ExpiredTicket_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-expired", ct);
        var service = fixture.CreatePairingBootstrapService();
        var user = await fixture.CreateUserAsync("bootstrap-expired@test.com");
        var created = await service.CreateBootstrapTicketAsync(user.Id, false, ct);
        var stored = await fixture.Db.PairingCodes.SingleAsync(ct);
        stored.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync(ct);

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.Equal(BootstrapTicketErrorKind.InvalidTicket, result.Error);
    }

    [Fact]
    public async Task BootstrapAsync_UnknownTicket_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-unknown", ct);
        var service = fixture.CreatePairingBootstrapService();

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = "unbekanntes-ticket",
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.Equal(BootstrapTicketErrorKind.InvalidTicket, result.Error);
    }

    [Fact]
    public async Task BootstrapAsync_AdminCode_IsNotRedeemable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-admin-code", ct);
        var service = fixture.CreatePairingBootstrapService();
        var pairing = fixture.CreatePairingService();
        var adminCode = (await pairing.CreatePairingCodeAsync("admin-id", ct)).Code;

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = adminCode,
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.Equal(BootstrapTicketErrorKind.InvalidTicket, result.Error);
    }

    [Theory]
    [InlineData("", "dGVzdA==")]
    [InlineData("ticket", "")]
    [InlineData("ticket", "kein-gueltiges-base64!!!")]
    public async Task BootstrapAsync_InvalidRequest_ReturnsInvalidRequest(string ticket, string clientKey)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync($"bootstrap-invalid-{ticket.Length}-{clientKey.Length}", ct);
        var service = fixture.CreatePairingBootstrapService();

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = ticket,
            ClientPublicKey = clientKey
        }, ct);

        Assert.Equal(BootstrapTicketErrorKind.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task BootstrapAsync_TooLongDeviceName_ReturnsInvalidRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-long-device-name", ct);
        var service = fixture.CreatePairingBootstrapService();
        var user = await fixture.CreateUserAsync("bootstrap-name@test.com");
        var created = await service.CreateBootstrapTicketAsync(user.Id, false, ct);

        var result = await service.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo()),
            DeviceName = new string('x', 201)
        }, ct);

        Assert.Equal(BootstrapTicketErrorKind.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task Exchange_DoesNotConsumeBootstrapShortCode()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("bootstrap-exchange-isolation", ct);
        var bootstrap = fixture.CreatePairingBootstrapService();
        var pairing = fixture.CreatePairingService();
        var user = await fixture.CreateUserAsync("bootstrap-iso@test.com");
        var created = await bootstrap.CreateBootstrapTicketAsync(user.Id, false, ct);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var clientKeyBase64 = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo());

        // Der Admin-Exchange darf den Bootstrap-Kurzcode nicht einloesen.
        var exchange = await pairing.ExchangeAsync(new PairingExchangeRequest
        {
            Code = created.ShortCode,
            ClientPublicKey = clientKeyBase64
        }, ct);
        Assert.False(exchange.Success);

        // Das Ticket bleibt danach weiterhin einloesbar.
        var result = await bootstrap.BootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = created.Ticket,
            ClientPublicKey = clientKeyBase64
        }, ct);
        Assert.True(result.Success);
    }

    private static PairingBootstrapPayload DecryptPayload(ECDiffieHellman clientKey, PairingBootstrapResult result)
    {
        var json = PairingCryptoHelper.DecryptToken(clientKey, result.ServerPublicKey!, result.EncryptedPayload!);
        var payload = JsonSerializer.Deserialize<PairingBootstrapPayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        return payload!;
    }

    private static void AssertTrio(PairingBootstrapPayload payload, ApplicationUser user)
    {
        Assert.False(string.IsNullOrWhiteSpace(payload.DeviceToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.True(payload.Expires > DateTime.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token);
        Assert.Equal(user.Id, jwt.Subject);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    private static async Task AssertRefreshTokenRotatesAsync(PairingTestDb fixture, PairingBootstrapPayload payload, CancellationToken ct)
    {
        var refreshTokens = fixture.CreateRefreshTokenService();
        var rotated = await refreshTokens.RotateAsync(payload.RefreshToken, ct);
        Assert.True(rotated.Success);
        Assert.False(string.IsNullOrWhiteSpace(rotated.NewToken));
    }
}
