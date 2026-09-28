using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingExchangeContractTests_Flow : PairingExchangeContractTestBase
{
    [Fact]
    public async Task Exchange_RequiresNoAuth_Returns200OnValidCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = await CreatePairingCodeAsync();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            "/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = code,
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
                DeviceName = "Contract-Geraet"
            },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PairingExchangeResponse>(cancellationToken: ct);
        Assert.False(string.IsNullOrWhiteSpace(payload?.ServerPublicKey));
        Assert.False(string.IsNullOrWhiteSpace(payload?.EncryptedToken));
    }

    [Fact]
    public async Task Exchange_Returns401OnInvalidCode()
    {
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            "/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = "ZZZZZZZZ",
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Exchange_Returns429AfterRepeatedFailures()
    {
        var remoteIp = IPAddress.Parse("203.0.113.77");
        try
        {
            using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var clientKeyBase64 = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo());
            var rawJson = JsonSerializer.Serialize(new { code = "ZZZZZZZZ", clientPublicKey = clientKeyBase64 });

            for (var i = 0; i < 5; i++)
            {
                var failedContext = await SendExchangeAsync(rawJson, remoteIp);
                Assert.Equal(StatusCodes.Status401Unauthorized, failedContext.Response.StatusCode);
            }

            var blockedContext = await SendExchangeAsync(rawJson, remoteIp);
            Assert.Equal(StatusCodes.Status429TooManyRequests, blockedContext.Response.StatusCode);
        }
        finally
        {
            UnblockIp(remoteIp.ToString());
        }
    }

    [Fact]
    public async Task Exchange_ResponseDecryptsToWorkingLoginToken()
    {
        var ct = TestContext.Current.CancellationToken;
        const string password = "PairingContract123!";
        var email = await CreateUserAsync(password);
        var code = await CreatePairingCodeAsync();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var exchangeResponse = await client.PostAsJsonAsync(
            "/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = code,
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
                DeviceName = "Contract-Geraet"
            },
            ct);
        Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
        var exchangePayload = await exchangeResponse.Content.ReadFromJsonAsync<PairingExchangeResponse>(cancellationToken: ct);
        var deviceToken = PairingCryptoHelper.DecryptToken(clientKey, exchangePayload!.ServerPublicKey, exchangePayload.EncryptedToken);

        client.DefaultRequestHeaders.Add("X-API-Key", deviceToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthenticationRequest { Email = email, Password = password },
            ct);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthorizationToken>(cancellationToken: ct);
        Assert.False(string.IsNullOrWhiteSpace(token?.token));
    }
}
