using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Security;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingServiceTests_Exchange
{
    [Fact]
    public async Task ExchangeAsync_ValidRequest_ReturnsDecryptableTokenAndPersistsDevice()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-valid", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync("admin-user-id", ct)).Code;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "Wohnzimmer-TV"
        }, ct);

        Assert.True(result.Success);
        Assert.Equal(PairingExchangeErrorKind.None, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.ServerPublicKey));
        Assert.False(string.IsNullOrWhiteSpace(result.EncryptedToken));

        var token = PairingCryptoHelper.DecryptToken(clientKey, result.ServerPublicKey!, result.EncryptedToken!);
        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Equal("Wohnzimmer-TV", device.Name);
        Assert.Equal(HashHelper.Sha256Hex(token), device.TokenHash);
        Assert.Equal("admin-user-id", device.CreatedByUserId);

        var pairingCode = await fixture.Db.PairingCodes.AsNoTracking().SingleAsync(ct);
        Assert.NotNull(pairingCode.ConsumedAtUtc);
    }

    [Fact]
    public async Task ExchangeAsync_UnknownCode_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-unknown", ct);
        var service = fixture.CreatePairingService();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = "XXXXXXXX",
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidCode, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
    }

    [Fact]
    public async Task ExchangeAsync_ExpiredCode_Fails()
    {
        // Regressionstest zu review-code.md: Die Ablaufbedingung ist Teil des atomaren
        // ExecuteUpdate-Verbrauchs (ExpiresAtUtc > now), sodass ein Code, der zwischen
        // Lese- und Update-Zugriff ablaeuft, nicht mehr konsumiert wird. Das reine
        // Zeitfenster ist nicht deterministisch reproduzierbar; geprueft wird die
        // fachliche Invariante, dass ein abgelaufener Code weder konsumiert wird noch
        // ein Geraet erzeugt.
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-expired", ct);
        var service = fixture.CreatePairingService();
        var code = "EXPIRED1";
        fixture.Db.PairingCodes.Add(new PairingCode
        {
            CodeHash = HashHelper.Sha256Hex(code),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        await fixture.Db.SaveChangesAsync(ct);
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidCode, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
        var pairingCode = await fixture.Db.PairingCodes.AsNoTracking().SingleAsync(ct);
        Assert.Null(pairingCode.ConsumedAtUtc);
    }

    [Fact]
    public async Task ExchangeAsync_ConsumedCode_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-consumed", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var request = new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        };

        var first = await service.ExchangeAsync(request, ct);
        var second = await service.ExchangeAsync(request, ct);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidCode, second.Error);
        Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
    }

    [Fact]
    public async Task ExchangeAsync_ConcurrentConsume_OnlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-race", ct, ";Default Timeout=30");
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;

        async Task<PairingExchangeResult> ExchangeInScope()
        {
            var scope = fixture.CreateScope();
            try
            {
                var scopedDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var scopedService = fixture.CreatePairingService(scopedDb);
                using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
                return await scopedService.ExchangeAsync(new PairingExchangeRequest
                {
                    Code = code,
                    ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
                }, ct);
            }
            finally
            {
                scope.Dispose();
            }
        }

        var results = await Task.WhenAll(ExchangeInScope(), ExchangeInScope());

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success && r.Error == PairingExchangeErrorKind.InvalidCode));
        Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
    }

    [Fact]
    public async Task ExchangeAsync_InvalidPublicKey_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-badkey", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-key"))
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidRequest, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Null((await fixture.Db.PairingCodes.AsNoTracking().SingleAsync(ct)).ConsumedAtUtc);
    }

    [Fact]
    public async Task ExchangeAsync_NonP256PublicKey_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-wrongcurve", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidRequest, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Null((await fixture.Db.PairingCodes.AsNoTracking().SingleAsync(ct)).ConsumedAtUtc);
    }

    [Fact]
    public async Task ExchangeAsync_ExplicitParametersPublicKey_Fails()
    {
        // Regressionstest zu review-code.md: Ein SPKI-Blob mit explizit codierten
        // Kurvenparametern (SpecifiedECDomain statt benannter Kurve) kann je nach
        // Plattform beim Import oder erst beim ExportParameters-Aufruf eine
        // Cryptographic-/PlatformNotSupportedException ausloesen. Beide Pfade
        // muessen kontrolliert als InvalidRequest (HTTP 400) enden, nicht als
        // unbehandelte Exception (HTTP 500).
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-explicitcurve", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = BuildExplicitParametersP256Spki()
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidRequest, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Null((await fixture.Db.PairingCodes.AsNoTracking().SingleAsync(ct)).ConsumedAtUtc);
    }

    [Fact]
    public async Task ExchangeAsync_EmptyCode_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-emptycode", ct);
        var service = fixture.CreatePairingService();
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = "",
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo())
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidRequest, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
    }

    [Fact]
    public async Task ExchangeAsync_DeviceNameTooLong_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-longname", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = new string('x', 201)
        }, ct);

        Assert.False(result.Success);
        Assert.Equal(PairingExchangeErrorKind.InvalidRequest, result.Error);
        Assert.Empty(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Null((await fixture.Db.PairingCodes.AsNoTracking().SingleAsync(ct)).ConsumedAtUtc);
    }

    [Fact]
    public async Task ExchangeAsync_DeviceNameWithPaddingWithinTrimmedLimit_Succeeds()
    {
        // Regressionstest zu review-code.md: Die Laengenpruefung lief auf dem
        // ungetrimmten String, waehrend RenameAsync nach dem Trim prueft. Ein Name
        // wie "  " + 199 Zeichen wurde beim Exchange mit 400 abgelehnt, beim
        // Umbenennen aber akzeptiert. Jetzt wird vor der Pruefung getrimmt.
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-exchange-paddedname", ct);
        var service = fixture.CreatePairingService();
        var code = (await service.CreatePairingCodeAsync(null, ct)).Code;
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var expectedName = new string('x', 199);

        var result = await service.ExchangeAsync(new PairingExchangeRequest
        {
            Code = code,
            ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
            DeviceName = "  " + expectedName
        }, ct);

        Assert.True(result.Success);
        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Equal(expectedName, device.Name);
    }

    private static string BuildExplicitParametersP256Spki()
    {
        var prime = Convert.FromHexString("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF");
        var a = Convert.FromHexString("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFC");
        var b = Convert.FromHexString("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B");
        var generator = Convert.FromHexString(
            "04" +
            "6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296" +
            "4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5");
        var order = Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");

        var fieldId = DerSeq(DerOid([0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x01, 0x01]), DerInt(prime));
        var curve = DerSeq(DerOctet(a), DerOctet(b));
        var specifiedDomain = DerSeq(DerInt([1]), fieldId, curve, DerOctet(generator), DerInt(order), DerInt([1]));
        var algorithm = DerSeq(DerOid([0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01]), specifiedDomain);
        return Convert.ToBase64String(DerSeq(algorithm, DerBitString(generator)));
    }

    private static byte[] DerSeq(params byte[][] parts) => DerTlv(0x30, Concat(parts));

    private static byte[] DerInt(byte[] unsignedBigEndian)
    {
        var value = unsignedBigEndian;
        if (value.Length > 0 && (value[0] & 0x80) != 0)
            value = [0x00, .. value];
        return DerTlv(0x02, value);
    }

    private static byte[] DerOctet(byte[] value) => DerTlv(0x04, value);

    private static byte[] DerOid(byte[] content) => DerTlv(0x06, content);

    private static byte[] DerBitString(byte[] data) => DerTlv(0x03, [0x00, .. data]);

    private static byte[] DerTlv(byte tag, byte[] content)
    {
        byte[] length = content.Length switch
        {
            < 0x80 => [(byte)content.Length],
            < 0x100 => [0x81, (byte)content.Length],
            _ => [0x82, (byte)(content.Length >> 8), (byte)content.Length]
        };
        return [tag, .. length, .. content];
    }

    private static byte[] Concat(byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }
}
