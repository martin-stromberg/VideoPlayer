using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the live stream settings of <see cref="ProgramSettingsService"/>: defaults, the
/// update/get roundtrip and the validation rules.
/// </summary>
public sealed class ProgramSettingsServiceLiveStreamTests : IAsyncLifetime
{
    private ProgramScheduleTestDb _fixture = null!;
    private ProgramSettingsService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await ProgramScheduleTestDb.CreateAsync($"ls-settings-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        _service = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Getters_DefaultToLiveStreamDefaults()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(TimeSpan.FromSeconds(6), await _service.GetLiveStreamSegmentLengthAsync(ct));
        Assert.Null(await _service.GetLiveStreamSegmentDirectoryAsync(ct));
        Assert.Equal(TimeSpan.FromMinutes(30), await _service.GetLiveStreamSegmentRetentionAsync(ct));
    }

    [Fact]
    public async Task UpdateLiveStreamSettingsAsync_PersistsValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = Path.Combine(Path.GetTempPath(), "vwp-segs");

        await _service.UpdateLiveStreamSettingsAsync(10, dir, 45, ct);

        var setup = await _fixture.Db.Setups.FirstAsync(ct);
        Assert.Equal(10, setup.LiveStreamSegmentLengthSeconds);
        Assert.Equal(Path.GetFullPath(dir), setup.LiveStreamSegmentDirectory);
        Assert.Equal(45, setup.LiveStreamSegmentRetentionMinutes);
        Assert.Equal(TimeSpan.FromSeconds(10), await _service.GetLiveStreamSegmentLengthAsync(ct));
        Assert.Equal(Path.GetFullPath(dir), await _service.GetLiveStreamSegmentDirectoryAsync(ct));
        Assert.Equal(TimeSpan.FromMinutes(45), await _service.GetLiveStreamSegmentRetentionAsync(ct));
    }

    [Fact]
    public async Task UpdateLiveStreamSettingsAsync_EmptyDirectory_ClearsToDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await _service.UpdateLiveStreamSettingsAsync(6, "C:\\segs", 30, ct);
        await _service.UpdateLiveStreamSettingsAsync(6, " ", 30, ct);

        Assert.Null(await _service.GetLiveStreamSegmentDirectoryAsync(ct));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-5, 30)]
    [InlineData(301, 30)]
    [InlineData(6, 0)]
    [InlineData(6, -1)]
    public async Task UpdateLiveStreamSettingsAsync_InvalidValues_Throws(int segmentSeconds, int retentionMinutes)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateLiveStreamSettingsAsync(segmentSeconds, null, retentionMinutes,
                TestContext.Current.CancellationToken));
    }
}
