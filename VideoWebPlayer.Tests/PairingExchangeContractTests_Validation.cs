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

public sealed class PairingExchangeContractTests_Validation : PairingExchangeContractTestBase
{
    [Fact]
    public async Task Exchange_Returns400OnMissingOrEmptyCode()
    {
        var remoteIp = IPAddress.Parse("198.51.100.23");
        try
        {
            using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var clientKeyBase64 = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo());

            for (var i = 0; i < 3; i++)
            {
                var missingContext = await SendExchangeAsync(
                    JsonSerializer.Serialize(new { clientPublicKey = clientKeyBase64 }),
                    remoteIp);
                Assert.Equal(StatusCodes.Status400BadRequest, missingContext.Response.StatusCode);

                var emptyContext = await SendExchangeAsync(
                    JsonSerializer.Serialize(new { code = "", clientPublicKey = clientKeyBase64 }),
                    remoteIp);
                Assert.Equal(StatusCodes.Status400BadRequest, emptyContext.Response.StatusCode);
            }

            var invalidCodeContext = await SendExchangeAsync(
                JsonSerializer.Serialize(new { code = "ZZZZZZZZ", clientPublicKey = clientKeyBase64 }),
                remoteIp);
            Assert.Equal(StatusCodes.Status401Unauthorized, invalidCodeContext.Response.StatusCode);
        }
        finally
        {
            // Der 401-Aufruf zaehlt einen Fehlversuch auf die IP; den Cache-Eintrag
            // aufraeumen, damit andere Tests nicht in eine vorgezogene Sperre laufen.
            UnblockIp(remoteIp.ToString());
        }
    }

    [Fact]
    public async Task Exchange_Returns400OnDeviceNameTooLong()
    {
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            "/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = "ZZZZZZZZ",
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
                DeviceName = new string('x', 201)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Exchange_Returns400OnNonP256ClientPublicKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = await CreatePairingCodeAsync();
        using var wrongCurveKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var rejected = await client.PostAsJsonAsync(
            "/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = code,
                ClientPublicKey = Convert.ToBase64String(wrongCurveKey.ExportSubjectPublicKeyInfo())
            },
            ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var accepted = await client.PostAsJsonAsync(
            "/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = code,
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
            },
            ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }
}
