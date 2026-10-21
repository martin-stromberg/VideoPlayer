using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="MediaDurationProbeService.GetProgressAsync"/>: the aggregated probe
/// progress for the administration view (counters over the probe states, last activity,
/// hourly rate and the rolling list of recently probed items), against real SQLite.
/// </summary>
public sealed class MediaDurationProbeServiceTests_Progress
{
    private static MediaDurationProbeService CreateService(ProgramScheduleTestDb fixture)
        => new(
            fixture.Db,
            new StubMediaSourceReader(),
            new NullProbe(),
            new PresentLocator(),
            Options.Create(new MediaProbeOptions()),
            NullLogger<MediaDurationProbeService>.Instance);

    private static async Task<MediaItem> SeedItemAsync(
        ProgramScheduleTestDb fixture, string name,
        TimeSpan? duration, DateTime? probedAt, int attempts, CancellationToken ct)
    {
        var item = await fixture.CreateMediaItemAsync();
        item.Name = name;
        item.Duration = duration;
        item.DurationProbedAt = probedAt;
        item.DurationProbeAttempts = attempts;
        await fixture.Db.SaveChangesAsync(ct);
        return item;
    }

    [Fact]
    public async Task GetProgressAsync_CountsStatesSeparately()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-counts", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        await SeedItemAsync(fixture, "fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        await SeedItemAsync(fixture, "neu.mkv", null, null, 0, ct);
        await SeedItemAsync(fixture, "retry.mkv", null, now.AddMinutes(-10), 2, ct);
        await SeedItemAsync(fixture, "defekt.mkv", null, now.AddDays(-1), maxAttempts, ct);
        await SeedItemAsync(fixture, "notizen.nfo", null, null, 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, ct);

        Assert.Equal(4, progress.TotalCount);
        Assert.Equal(1, progress.ProbedCount);
        Assert.Equal(2, progress.PendingCount);
        Assert.Equal(1, progress.FailedCount);
    }

    [Fact]
    public async Task GetProgressAsync_RecentItems_OrderedNewestFirstAndLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-recent", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;

        await SeedItemAsync(fixture, "alt.mkv", TimeSpan.FromMinutes(10), now.AddMinutes(-30), 0, ct);
        await SeedItemAsync(fixture, "mitte.mkv", TimeSpan.FromMinutes(20), now.AddMinutes(-20), 0, ct);
        await SeedItemAsync(fixture, "neu.mkv", TimeSpan.FromMinutes(30), now.AddMinutes(-10), 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 2, ct);

        Assert.Equal(2, progress.RecentItems.Count);
        Assert.Equal("neu.mkv", progress.RecentItems[0].Name);
        Assert.Equal("mitte.mkv", progress.RecentItems[1].Name);
        Assert.Equal(TimeSpan.FromMinutes(30), progress.RecentItems[0].Duration);
        Assert.Equal("Testquelle", progress.RecentItems[0].SourceName);
    }

    [Fact]
    public async Task GetProgressAsync_ReportsLastActivityAndHourlyRate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-activity", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;

        var recent = await SeedItemAsync(fixture, "frisch.mkv", TimeSpan.FromMinutes(5), now.AddMinutes(-30), 0, ct);
        await SeedItemAsync(fixture, "lange-her.mkv", TimeSpan.FromMinutes(5), now.AddHours(-2), 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, ct);

        Assert.Equal(recent.DurationProbedAt, progress.LastActivityAt);
        Assert.Equal(1, progress.ProbedLastHour);
    }

    [Fact]
    public async Task GetProgressAsync_EmptyLibrary_ReturnsZeros()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-empty", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, ct);

        Assert.Equal(0, progress.TotalCount);
        Assert.Equal(0, progress.ProbedCount);
        Assert.Equal(0, progress.PendingCount);
        Assert.Equal(0, progress.FailedCount);
        Assert.Null(progress.LastActivityAt);
        Assert.Equal(0, progress.ProbedLastHour);
        Assert.Empty(progress.RecentItems);
    }

    [Fact]
    public async Task GetProgressAsync_NonAdmin_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-auth", ct);
        var viewer = await fixture.AddUserAsync("viewer@test.com");
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.GetProgressAsync(viewer.Id, 10, ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetProgressAsync("no-such-user", 10, ct));
    }

    private sealed class NullProbe : IMediaDurationProbe
    {
        public Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.Zero));

        public Task<MediaDurationProbeResult> ProbeUriAsync(string uri, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.Zero));

        public Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.Zero));
    }

    private sealed class PresentLocator : IFfprobeLocator
    {
        public string? Resolve() => "ffprobe";
    }

    private sealed class StubMediaSourceReader : IMediaSourceReader
    {
        public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source) => Enumerable.Empty<MediaEntry>();
        public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection) => Enumerable.Empty<MediaEntry>();
        public Task<bool> FileExistsAsync(MediaCollection collection, string fileName) => Task.FromResult(false);
        public Task<string?> ReadFileAsync(MediaCollection collection, string fileName) => Task.FromResult<string?>(null);
        public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName) => Task.FromResult<Stream?>(null);
        public Stream? OpenFileStream(MediaCollection collection, string fileName) => null;
    }
}
