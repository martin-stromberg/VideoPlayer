using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the administrator reset methods of <see cref="MediaDurationProbeService"/>
/// (<see cref="MediaDurationProbeService.ResetFailedProbeAsync"/> and
/// <see cref="MediaDurationProbeService.ResetFailedProbesAsync"/>): clearing the probe state of
/// permanently failed items so <c>ProbePendingAsync</c> picks them up again — against real
/// SQLite via the <see cref="ProgramScheduleTestDb"/> fixture.
/// </summary>
public sealed class MediaDurationProbeServiceTests_Reset
{
    private static MediaDurationProbeService CreateService(ProgramScheduleTestDb fixture)
        => new(
            fixture.Db,
            new StubMediaSourceReader(() => new MemoryStream([1, 2, 3])),
            new FixedDurationProbe(TimeSpan.FromMinutes(60)),
            new PresentLocator(),
            Options.Create(new MediaProbeOptions()),
            NullLogger<MediaDurationProbeService>.Instance);

    [Fact]
    public async Task ResetFailedProbeAsync_FailedItem_ClearsProbeStateAndBecomesPending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-single", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;
        var failed = await fixture.SeedProbeItemAsync("defekt.mkv", null, DateTime.UtcNow.AddDays(-1), maxAttempts, ct);
        var service = CreateService(fixture);

        var before = await service.GetProgressAsync(admin.Id, 10, cancellationToken: ct);
        Assert.Equal(1, before.FailedCount);
        Assert.Equal(0, before.PendingCount);

        Assert.True(await service.ResetFailedProbeAsync(admin.Id, failed.Id, ct));

        await fixture.Db.Entry(failed).ReloadAsync(ct);
        Assert.Null(failed.DurationProbedAt);
        Assert.Equal(0, failed.DurationProbeAttempts);

        var after = await service.GetProgressAsync(admin.Id, 10, cancellationToken: ct);
        Assert.Equal(0, after.FailedCount);
        Assert.Equal(1, after.PendingCount);
        var pending = Assert.Single(after.PendingItems);
        Assert.Equal(failed.Id, pending.MediaItemId);
        Assert.Empty(after.FailedItems);
    }

    [Fact]
    public async Task ResetFailedProbeAsync_ProbedItem_ReturnsFalseAndKeepsDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-probed", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;
        var probed = await fixture.SeedProbeItemAsync(
            "fertig.mkv", TimeSpan.FromMinutes(90), DateTime.UtcNow, maxAttempts, ct);
        var service = CreateService(fixture);

        Assert.False(await service.ResetFailedProbeAsync(admin.Id, probed.Id, ct));

        await fixture.Db.Entry(probed).ReloadAsync(ct);
        Assert.Equal(TimeSpan.FromMinutes(90), probed.Duration);
        Assert.Equal(maxAttempts, probed.DurationProbeAttempts);
        Assert.NotNull(probed.DurationProbedAt);
    }

    [Fact]
    public async Task ResetFailedProbeAsync_PendingOrUnknownItem_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-pending", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var pending = await fixture.SeedProbeItemAsync(
            "wartet.mkv", null, DateTime.UtcNow.AddMinutes(-10), 2, ct);
        var service = CreateService(fixture);

        Assert.False(await service.ResetFailedProbeAsync(admin.Id, pending.Id, ct));
        Assert.False(await service.ResetFailedProbeAsync(admin.Id, pending.Id + 9999, ct));

        await fixture.Db.Entry(pending).ReloadAsync(ct);
        Assert.Equal(2, pending.DurationProbeAttempts);
        Assert.NotNull(pending.DurationProbedAt);
    }

    [Fact]
    public async Task ResetFailedProbeAsync_NonVideoItem_ReturnsFalseAndKeepsState()
    {
        // The single reset must address the same failed set as the counters and the bulk
        // reset — video items only. A non-video file that accumulated attempts (e.g. a
        // legacy row) never appears in the failed list and must not be resettable either.
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-nonvideo", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;
        var probedAt = DateTime.UtcNow.AddDays(-1);
        var nonVideo = await fixture.SeedProbeItemAsync("notizen.nfo", null, probedAt, maxAttempts, ct);
        var service = CreateService(fixture);

        Assert.False(await service.ResetFailedProbeAsync(admin.Id, nonVideo.Id, ct));

        await fixture.Db.Entry(nonVideo).ReloadAsync(ct);
        Assert.Equal(maxAttempts, nonVideo.DurationProbeAttempts);
        Assert.Equal(probedAt, nonVideo.DurationProbedAt);
    }

    [Fact]
    public async Task ResetFailedProbeAsync_NonAdmin_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-auth", ct);
        var viewer = await fixture.AddUserAsync("viewer@test.com");
        var failed = await fixture.SeedProbeItemAsync(
            "defekt.mkv", null, DateTime.UtcNow.AddDays(-1),
            new MediaProbeOptions().EffectiveMaxAttempts, ct);
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.ResetFailedProbeAsync(viewer.Id, failed.Id, ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ResetFailedProbeAsync("no-such-user", failed.Id, ct));
    }

    [Fact]
    public async Task ResetFailedProbesAsync_ResetsAllFailedItemsBeyondDetailCap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-all-cap", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        for (var i = 0; i < 30; i++)
            await fixture.SeedProbeItemAsync($"defekt-{i}.mkv", null, now.AddMinutes(-i), maxAttempts, ct);
        var service = CreateService(fixture);

        var before = await service.GetProgressAsync(admin.Id, 10, cancellationToken: ct);
        Assert.Equal(30, before.FailedCount);
        Assert.Equal(25, before.FailedItems.Count);

        Assert.Equal(30, await service.ResetFailedProbesAsync(admin.Id, ct));

        var after = await service.GetProgressAsync(admin.Id, 10, cancellationToken: ct);
        Assert.Equal(0, after.FailedCount);
        Assert.Equal(30, after.PendingCount);
    }

    [Fact]
    public async Task ResetFailedProbesAsync_LeavesProbedAndPendingUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-all-untouched", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var now = DateTime.UtcNow;
        var maxAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

        var probed = await fixture.SeedProbeItemAsync("fertig.mkv", TimeSpan.FromMinutes(90), now, 0, ct);
        var pending = await fixture.SeedProbeItemAsync("wartet.mkv", null, now.AddMinutes(-10), 2, ct);
        var failed = await fixture.SeedProbeItemAsync("defekt.mkv", null, now.AddDays(-1), maxAttempts, ct);
        var service = CreateService(fixture);

        Assert.Equal(1, await service.ResetFailedProbesAsync(admin.Id, ct));

        fixture.Db.ChangeTracker.Clear();
        var probedRow = await fixture.Db.MediaItems.SingleAsync(mi => mi.Id == probed.Id, ct);
        Assert.Equal(TimeSpan.FromMinutes(90), probedRow.Duration);
        var pendingRow = await fixture.Db.MediaItems.SingleAsync(mi => mi.Id == pending.Id, ct);
        Assert.Equal(2, pendingRow.DurationProbeAttempts);
        Assert.Equal(pending.DurationProbedAt, pendingRow.DurationProbedAt);
        var failedRow = await fixture.Db.MediaItems.SingleAsync(mi => mi.Id == failed.Id, ct);
        Assert.Null(failedRow.DurationProbedAt);
        Assert.Equal(0, failedRow.DurationProbeAttempts);
    }

    [Fact]
    public async Task ResetFailedProbesAsync_NonAdmin_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-all-auth", ct);
        var viewer = await fixture.AddUserAsync("viewer@test.com");
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.ResetFailedProbesAsync(viewer.Id, ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ResetFailedProbesAsync("no-such-user", ct));
    }

    [Fact]
    public async Task ProbePendingAsync_PicksUpResetItem()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("probe-reset-pickup", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var failed = await fixture.SeedProbeItemAsync(
            "defekt.mkv", null, DateTime.UtcNow.AddDays(-1),
            new MediaProbeOptions().EffectiveMaxAttempts, ct);
        var service = CreateService(fixture);

        Assert.Equal(0, await service.ProbePendingAsync(10, ct));
        Assert.True(await service.ResetFailedProbeAsync(admin.Id, failed.Id, ct));
        Assert.Equal(1, await service.ProbePendingAsync(10, ct));

        fixture.Db.ChangeTracker.Clear();
        var reloaded = await fixture.Db.MediaItems.SingleAsync(mi => mi.Id == failed.Id, ct);
        Assert.Equal(TimeSpan.FromMinutes(60), reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);
    }

}
