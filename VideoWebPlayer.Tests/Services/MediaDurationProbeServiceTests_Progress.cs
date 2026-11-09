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
            new FixedDurationProbe(TimeSpan.Zero),
            new PresentLocator(),
            Options.Create(new MediaProbeOptions()),
            NullLogger<MediaDurationProbeService>.Instance);

    [Fact]
    public async Task GetProgressAsync_CountsStatesSeparately()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-counts", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        await fixture.SeedProbeItemAsync("fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        await fixture.SeedProbeItemAsync("neu.mkv", null, null, 0, ct);
        await fixture.SeedProbeItemAsync("retry.mkv", null, now.AddMinutes(-10), 2, ct);
        await fixture.SeedProbeItemAsync("defekt.mkv", null, now.AddDays(-1), maxAttempts, ct);
        await fixture.SeedProbeItemAsync("notizen.nfo", null, null, 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, cancellationToken: ct);

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

        await fixture.SeedProbeItemAsync("alt.mkv", TimeSpan.FromMinutes(10), now.AddMinutes(-30), 0, ct);
        await fixture.SeedProbeItemAsync("mitte.mkv", TimeSpan.FromMinutes(20), now.AddMinutes(-20), 0, ct);
        await fixture.SeedProbeItemAsync("neu.mkv", TimeSpan.FromMinutes(30), now.AddMinutes(-10), 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 2, cancellationToken: ct);

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

        var recent = await fixture.SeedProbeItemAsync("frisch.mkv", TimeSpan.FromMinutes(5), now.AddMinutes(-30), 0, ct);
        await fixture.SeedProbeItemAsync("lange-her.mkv", TimeSpan.FromMinutes(5), now.AddHours(-2), 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, cancellationToken: ct);

        Assert.Equal(recent.DurationProbedAt, progress.LastActivityAt);
        Assert.Equal(1, progress.ProbedLastHour);
    }

    [Fact]
    public async Task GetProgressAsync_PendingItems_ListsUnprobedAndRetrying()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-pending", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        await fixture.SeedProbeItemAsync("fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        await fixture.SeedProbeItemAsync("nie-probiert.mkv", null, null, 0, ct);
        var retried = await fixture.SeedProbeItemAsync("retry.mkv", null, now.AddMinutes(-10), 2, ct);
        await fixture.SeedProbeItemAsync("defekt.mkv", null, now.AddDays(-1), maxAttempts, ct);
        await fixture.SeedProbeItemAsync("notizen.nfo", null, null, 0, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, cancellationToken: ct);

        Assert.Equal(2, progress.PendingItems.Count);
        Assert.Equal("retry.mkv", progress.PendingItems[0].Name);
        Assert.Equal("Testquelle", progress.PendingItems[0].SourceName);
        Assert.Equal(2, progress.PendingItems[0].Attempts);
        Assert.Equal(retried.DurationProbedAt, progress.PendingItems[0].LastAttemptAt);
        // Never-probed items sort last (DurationProbedAt null) and report no attempt time.
        Assert.Equal("nie-probiert.mkv", progress.PendingItems[1].Name);
        Assert.Equal(0, progress.PendingItems[1].Attempts);
        Assert.Null(progress.PendingItems[1].LastAttemptAt);
    }

    [Fact]
    public async Task GetProgressAsync_FailedItems_ListsExhaustedAttemptsNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-failed", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        await fixture.SeedProbeItemAsync("fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        await fixture.SeedProbeItemAsync("wartet.mkv", null, null, 0, ct);
        await fixture.SeedProbeItemAsync("defekt-alt.mkv", null, now.AddDays(-2), maxAttempts, ct);
        await fixture.SeedProbeItemAsync("defekt-neu.mkv", null, now.AddHours(-3), maxAttempts + 2, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, cancellationToken: ct);

        Assert.Equal(2, progress.FailedItems.Count);
        Assert.Equal("defekt-neu.mkv", progress.FailedItems[0].Name);
        Assert.Equal(maxAttempts + 2, progress.FailedItems[0].Attempts);
        Assert.Equal("defekt-alt.mkv", progress.FailedItems[1].Name);
        Assert.All(progress.FailedItems, item =>
        {
            Assert.Equal("Testquelle", item.SourceName);
            Assert.NotNull(item.LastAttemptAt);
        });
    }

    [Fact]
    public async Task GetProgressAsync_DetailItems_RespectLimit_CountersStayFull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-limit", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        for (var i = 0; i < 4; i++)
        {
            await fixture.SeedProbeItemAsync($"wartet-{i}.mkv", null, null, 0, ct);
            await fixture.SeedProbeItemAsync($"defekt-{i}.mkv", null, now.AddMinutes(-i), maxAttempts, ct);
        }

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, detailItemCount: 2, cancellationToken: ct);

        Assert.Equal(4, progress.PendingCount);
        Assert.Equal(4, progress.FailedCount);
        Assert.Equal(2, progress.PendingItems.Count);
        Assert.Equal(2, progress.FailedItems.Count);
    }

    [Fact]
    public async Task GetProgressAsync_DetailItems_CarryMediaItemId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-itemid", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        var pending = await fixture.SeedProbeItemAsync("wartet.mkv", null, null, 0, ct);
        var failed = await fixture.SeedProbeItemAsync("defekt.mkv", null, now.AddDays(-1), maxAttempts, ct);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, cancellationToken: ct);

        Assert.Equal(pending.Id, Assert.Single(progress.PendingItems).MediaItemId);
        Assert.Equal(failed.Id, Assert.Single(progress.FailedItems).MediaItemId);
    }

    [Fact]
    public async Task GetProgressAsync_EmptyLibrary_ReturnsZeros()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-progress-empty", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);

        var progress = await CreateService(fixture).GetProgressAsync(admin.Id, 10, cancellationToken: ct);

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
            () => service.GetProgressAsync(viewer.Id, 10, cancellationToken: ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetProgressAsync("no-such-user", 10, cancellationToken: ct));
    }

}
