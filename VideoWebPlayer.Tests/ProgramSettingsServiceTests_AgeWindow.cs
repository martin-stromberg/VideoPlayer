using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the age-window settings of <see cref="ProgramSettingsService"/>: the delivered
/// defaults, the get/update roundtrip, the read-side normalization of corrupted values, the
/// ordering/night-end validation and the enum validation of the unknown-rating mode.
/// </summary>
public sealed class ProgramSettingsServiceTests_AgeWindow : IAsyncLifetime
{
    private ProgramScheduleTestDb _fixture = null!;
    private ProgramSettingsService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await ProgramScheduleTestDb.CreateAsync($"agewindow-settings-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        _service = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetProgramScheduleAgeWindows_DefaultsToDeliveredSchema()
    {
        var windows = await _service.GetProgramScheduleAgeWindowsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new TimeOnly(20, 0), windows.Fsk12Earliest);
        Assert.Equal(new TimeOnly(22, 0), windows.Fsk16Earliest);
        Assert.Equal(new TimeOnly(23, 0), windows.Fsk18Earliest);
        Assert.Equal(new TimeOnly(6, 0), windows.NightEnd);
    }

    [Fact]
    public async Task GetProgramScheduleAgeWindows_InvalidStoredValues_NormalizeToDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await _fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleAgeWindow18Local = new TimeOnly(18, 0); // breaks Fsk16 <= Fsk18
        await _fixture.Db.SaveChangesAsync(ct);

        Assert.Equal(ProgramScheduleAgeWindowSettings.Default, await _service.GetProgramScheduleAgeWindowsAsync(ct));
    }

    [Fact]
    public async Task UpdateProgramScheduleAgeWindows_Roundtrip()
    {
        var ct = TestContext.Current.CancellationToken;
        var custom = new ProgramScheduleAgeWindowSettings(new TimeOnly(19, 30), new TimeOnly(21, 0), new TimeOnly(22, 30), new TimeOnly(5, 0));

        await _service.UpdateProgramScheduleAgeWindowsAsync(custom, ct);

        Assert.Equal(custom, await _service.GetProgramScheduleAgeWindowsAsync(ct));
        var setup = await _fixture.Db.Setups.FirstAsync(ct);
        Assert.Equal(new TimeOnly(19, 30), setup.ProgramScheduleAgeWindow12Local);
        Assert.Equal(new TimeOnly(21, 0), setup.ProgramScheduleAgeWindow16Local);
        Assert.Equal(new TimeOnly(22, 30), setup.ProgramScheduleAgeWindow18Local);
        Assert.Equal(new TimeOnly(5, 0), setup.ProgramScheduleAgeWindowNightEndLocal);
    }

    [Theory]
    [InlineData(21, 20, 23, 6)]   // Fsk12 > Fsk16
    [InlineData(20, 22, 21, 6)]   // Fsk16 > Fsk18
    [InlineData(20, 22, 23, 21)]  // NightEnd not before the first window
    [InlineData(20, 22, 23, 20)]  // NightEnd equal to the first window
    public async Task UpdateProgramScheduleAgeWindows_Invalid_ThrowsAndKeepsDefaults(int h12, int h16, int h18, int end)
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateProgramScheduleAgeWindowsAsync(
            new ProgramScheduleAgeWindowSettings(new TimeOnly(h12, 0), new TimeOnly(h16, 0), new TimeOnly(h18, 0), new TimeOnly(end, 0)), ct));

        Assert.Equal(ProgramScheduleAgeWindowSettings.Default, await _service.GetProgramScheduleAgeWindowsAsync(ct));
    }

    [Fact]
    public async Task GetProgramScheduleUnknownRatingMode_SeededFixtureIsUnrestricted()
    {
        // The test fixture seeds Unrestricted so ratings-less seed data stays schedulable —
        // the productive Setup default is RestrictToNightWindow.
        Assert.Equal(ProgramScheduleUnknownRatingMode.Unrestricted,
            await _service.GetProgramScheduleUnknownRatingModeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetProgramScheduleUnknownRatingMode_InvalidStoredValue_NormalizesToRestrictive()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await _fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleUnknownRatingMode = (ProgramScheduleUnknownRatingMode)7;
        await _fixture.Db.SaveChangesAsync(ct);

        Assert.Equal(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, await _service.GetProgramScheduleUnknownRatingModeAsync(ct));
    }

    [Fact]
    public async Task UpdateProgramScheduleUnknownRatingMode_Roundtrip()
    {
        var ct = TestContext.Current.CancellationToken;

        await _service.UpdateProgramScheduleUnknownRatingModeAsync(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);
        Assert.Equal(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, await _service.GetProgramScheduleUnknownRatingModeAsync(ct));

        await _service.UpdateProgramScheduleUnknownRatingModeAsync(ProgramScheduleUnknownRatingMode.Unrestricted, ct);
        Assert.Equal(ProgramScheduleUnknownRatingMode.Unrestricted, await _service.GetProgramScheduleUnknownRatingModeAsync(ct));
        Assert.Equal(ProgramScheduleUnknownRatingMode.Unrestricted, (await _fixture.Db.Setups.FirstAsync(ct)).ProgramScheduleUnknownRatingMode);
    }

    [Fact]
    public async Task UpdateProgramScheduleUnknownRatingMode_InvalidValue_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateProgramScheduleUnknownRatingModeAsync(
            (ProgramScheduleUnknownRatingMode)42, TestContext.Current.CancellationToken));
    }
}
