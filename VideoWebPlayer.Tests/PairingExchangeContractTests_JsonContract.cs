using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingExchangeContractTests_JsonContract : PairingExchangeContractTestBase
{
    [Fact]
    public async Task Exchange_UsesBindingJsonFieldNames()
    {
        var code = await CreatePairingCodeAsync();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var clientKeyBase64 = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo());

        var context = await SendExchangeAsync(
            $$"""{"code":"{{code}}","clientPublicKey":"{{clientKeyBase64}}","deviceName":"Json-Vertrag-Geraet"}""");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var document = JsonDocument.Parse(await ReadBodyAsync(context));
        var propertyNames = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "serverPublicKey", "encryptedToken" }, propertyNames);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("serverPublicKey").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("encryptedToken").GetString()));
    }
}
