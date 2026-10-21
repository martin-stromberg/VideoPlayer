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

        await service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate("Titel", 60, 7, 30, MdnsAdvertisementEnabled: false, DiscoveryPublicBaseUrl: null), ct);

        var setup = await fixture.Db.Setups.AsNoTracking().SingleAsync(ct);
        Assert.False(setup.MdnsAdvertisementEnabled);
        Assert.Equal("Titel", setup.ApplicationTitle);
        Assert.False(await service.GetMdnsAdvertisementEnabledAsync(ct));

        await service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate("Titel", 60, 7, 30, MdnsAdvertisementEnabled: true, DiscoveryPublicBaseUrl: null), ct);
        Assert.True((await fixture.Db.Setups.AsNoTracking().SingleAsync(ct)).MdnsAdvertisementEnabled);
    }

    [Fact]
    public async Task GetDiscoveryPublicBaseUrlAsync_DefaultsToNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-discovery-default", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        Assert.Null(await service.GetDiscoveryPublicBaseUrlAsync(ct));
    }

    [Fact]
    public async Task UpdateGeneralSettingsAsync_PersistsNormalizedDiscoveryPublicBaseUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-discovery-general", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate(
                "Titel", 60, 7, 30, MdnsAdvertisementEnabled: true,
                DiscoveryPublicBaseUrl: "  https://videos.example.com/videoplayer/  "), ct);

        var setup = await fixture.Db.Setups.AsNoTracking().SingleAsync(ct);
        Assert.Equal("https://videos.example.com/videoplayer/", setup.DiscoveryPublicBaseUrl);
        Assert.Equal("Titel", setup.ApplicationTitle);
        Assert.True(setup.MdnsAdvertisementEnabled);
        Assert.Equal("https://videos.example.com/videoplayer/", await service.GetDiscoveryPublicBaseUrlAsync(ct));
    }

    [Fact]
    public async Task UpdateGeneralSettingsAsync_ClearsDiscoveryPublicBaseUrlOnEmptyInput()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-discovery-clear", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate(
                "Titel", 60, 7, 30, MdnsAdvertisementEnabled: true,
                DiscoveryPublicBaseUrl: "https://videos.example.com/videoplayer/"), ct);

        // Leeren Wert speichern löscht den Admin-Override.
        await service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate("Titel", 60, 7, 30, MdnsAdvertisementEnabled: true, DiscoveryPublicBaseUrl: " "), ct);

        Assert.Null((await fixture.Db.Setups.AsNoTracking().SingleAsync(ct)).DiscoveryPublicBaseUrl);
        Assert.Null(await service.GetDiscoveryPublicBaseUrlAsync(ct));
    }

    [Theory]
    [InlineData("notaurl")]
    [InlineData("/relativ")]
    [InlineData("ftp://x")]
    public async Task UpdateGeneralSettingsAsync_RejectsInvalidDiscoveryPublicBaseUrl(string invalidUrl)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-discovery-invalid", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate(
                "Titel", 60, 7, 30, MdnsAdvertisementEnabled: true,
                DiscoveryPublicBaseUrl: "https://videos.example.com/"), ct);

        await Assert.ThrowsAsync<DiscoveryUrlValidationException>(() => service.UpdateGeneralSettingsAsync(
            new GeneralSettingsUpdate(
                "Titel", 60, 7, 30, MdnsAdvertisementEnabled: true,
                DiscoveryPublicBaseUrl: invalidUrl), ct));

        // Atomar: die ungültige Eingabe schreibt nichts, der bisherige Wert bleibt bestehen.
        var setup = await fixture.Db.Setups.AsNoTracking().SingleAsync(ct);
        Assert.Equal("https://videos.example.com/", setup.DiscoveryPublicBaseUrl);
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

public sealed class ProgramSettingsServiceHolidayFederalStateTests
{
    [Fact]
    public async Task GetHolidayFederalStateAsync_DefaultsToNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-holiday-default", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        Assert.Null(await service.GetHolidayFederalStateAsync(ct));
    }

    [Fact]
    public async Task UpdateHolidayFederalStateAsync_PersistsCodeAndNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-holiday-update", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateHolidayFederalStateAsync("DE-NW", ct);
        Assert.Equal("DE-NW", (await fixture.Db.Setups.SingleAsync(ct)).HolidayFederalState);

        await service.UpdateHolidayFederalStateAsync(null, ct);
        Assert.Null((await fixture.Db.Setups.SingleAsync(ct)).HolidayFederalState);
    }

    [Fact]
    public async Task UpdateHolidayFederalStateAsync_UnknownCode_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-holiday-invalid", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateHolidayFederalStateAsync("XX-YY", ct));
    }
}

public sealed class ProgramSettingsServiceScheduleTests
{
    [Fact]
    public async Task ProgramScheduleSettings_DefaultTo48HoursAnd15Minutes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-schedule-default", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        Assert.Equal(TimeSpan.FromHours(48), await service.GetProgramScheduleHorizonAsync(ct));
        Assert.Equal(TimeSpan.FromMinutes(15), await service.GetProgramScheduleIntervalAsync(ct));
    }

    [Fact]
    public async Task UpdateProgramScheduleSettings_PersistsAndValidates()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("program-settings-schedule-update", ct);
        var service = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

        await service.UpdateProgramScheduleSettingsAsync(72, 30, ct);

        var setup = await fixture.Db.Setups.AsNoTracking().SingleAsync(ct);
        Assert.Equal(72, setup.ProgramScheduleHorizonHours);
        Assert.Equal(30, setup.ProgramScheduleIntervalMinutes);
        Assert.Equal(TimeSpan.FromHours(72), await service.GetProgramScheduleHorizonAsync(ct));
        Assert.Equal(TimeSpan.FromMinutes(30), await service.GetProgramScheduleIntervalAsync(ct));

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateProgramScheduleSettingsAsync(0, 15, ct));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateProgramScheduleSettingsAsync(48, 0, ct));
    }
}
