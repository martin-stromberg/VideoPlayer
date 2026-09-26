using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// The pairing calls of the client library (A5): redeeming a pairing code and redeeming a QR bootstrap
/// ticket, each in the accepted and in the refused case.
/// </summary>
public sealed class VideoWebPlayerClientTests_Pairing : DeviceClientTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PairingExchangeAsync_WithValidCode_ReturnsDecryptableDeviceToken()
    {
        var code = await CreatePairingCodeAsync();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await Client.PairingExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "Kopplungs-Testgerät"
        });

        var deviceToken = PairingCryptoHelper.DecryptToken(clientKey, response.ServerPublicKey, response.EncryptedToken);
        Assert.False(string.IsNullOrWhiteSpace(deviceToken));
    }

    [Fact]
    public async Task PairingExchangeAsync_WithInvalidCode_ThrowsUnauthorized()
    {
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client.PairingExchangeAsync(new PairingExchangeRequest
        {
            Code = "UNGUELTIG",
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task PairingBootstrapAsync_WithValidTicket_ReturnsDeviceTokenSessionAndRefreshToken()
    {
        var user = await CreateUserAsync($"bootstrap-lib-{Guid.NewGuid():N}@test.com");
        var ticket = await CreateBootstrapTicketAsync(user.Id);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var response = await Client.PairingBootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = ticket,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "Bootstrap-Testgerät"
        });

        var json = PairingCryptoHelper.DecryptToken(clientKey, response.ServerPublicKey, response.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<PairingBootstrapPayload>(json, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.DeviceToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    }

    [Fact]
    public async Task PairingBootstrapAsync_WithUnknownTicket_ThrowsUnauthorized()
    {
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client.PairingBootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = "unbekanntes-ticket",
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    /// <summary>
    /// Creates a one-time pairing code, as an administrator does on the devices page.
    /// </summary>
    /// <returns>The plaintext pairing code.</returns>
    private async Task<string> CreatePairingCodeAsync()
    {
        using var scope = Services.CreateScope();
        var pairing = scope.ServiceProvider.GetRequiredService<IPairingService>();
        return (await pairing.CreatePairingCodeAsync("admin-id")).Code;
    }
}
