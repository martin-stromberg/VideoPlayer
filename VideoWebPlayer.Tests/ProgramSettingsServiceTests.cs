using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramSettingsServiceTests
{
    [Fact]
    public async Task GetMdnsAdvertisementEnabledAsync_DefaultsToTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-mdns-default", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        Assert.True(await service.GetMdnsAdvertisementEnabledAsync(ct));
    }

    [Fact]
    public async Task UpdateGeneralSettingsAsync_PersistsMdnsAdvertisementEnabled()
    {
        // Der mDNS-Schalter gehört zum selben Speichern-Vorgang wie die übrigen
        // Einstellungen der Seite (ein SaveChangesAsync) — er darf kein eigener,
        // separat fehlschlagbarer Commit sein.
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-mdns-general", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateGeneralSettingsAsync("Titel", 60, 7, 30, mdnsAdvertisementEnabled: false, ct);

        var setup = await fixture.Db.Setups.AsNoTracking().SingleAsync(ct);
        Assert.False(setup.MdnsAdvertisementEnabled);
        Assert.Equal("Titel", setup.ApplicationTitle);
        Assert.False(await service.GetMdnsAdvertisementEnabledAsync(ct));

        await service.UpdateGeneralSettingsAsync("Titel", 60, 7, 30, mdnsAdvertisementEnabled: true, ct);
        Assert.True((await fixture.Db.Setups.AsNoTracking().SingleAsync(ct)).MdnsAdvertisementEnabled);
    }

    [Fact]
    public async Task UpdateMdnsAdvertisementEnabledAsync_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-mdns-persist", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateMdnsAdvertisementEnabledAsync(false, ct);
        Assert.False(await service.GetMdnsAdvertisementEnabledAsync(ct));
        Assert.False((await fixture.Db.Setups.AsNoTracking().SingleAsync(ct)).MdnsAdvertisementEnabled);

        await service.UpdateMdnsAdvertisementEnabledAsync(true, ct);
        Assert.True(await service.GetMdnsAdvertisementEnabledAsync(ct));
        Assert.True((await fixture.Db.Setups.AsNoTracking().SingleAsync(ct)).MdnsAdvertisementEnabled);
    }
}
