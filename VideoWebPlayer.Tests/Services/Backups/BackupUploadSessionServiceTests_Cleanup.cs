using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;
using Xunit;
using static VideoWebPlayer.Tests.Helpers.BackupUploadSessionServiceTestSupport;

namespace VideoWebPlayer.Tests.Services.Backups;

public sealed class BackupUploadSessionServiceTests_Cleanup
{
    [Fact]
    public async Task GetSession_WhenSessionExpired_RemovesSessionAndTempFile()
    {
        using var provider = CreateProvider();
        var time = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1));
        var service = CreateService(provider, time);
        var expired = (await service.BeginSessionAsync("old.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            time.Advance(TimeSpan.FromHours(25));

            Assert.Null(service.GetSession(expired.Id));
            Assert.False(File.Exists(expired.TempPath));
        }
        finally
        {
            await DisposeSessionAsync(expired);
        }
    }

    [Fact]
    public async Task BeginSessionAsync_WhenOrphanedTempFileIsOld_DeletesOrphan()
    {
        using var provider = CreateProvider();
        var service = CreateService(provider);
        var orphanPath = Path.Combine(Path.GetTempPath(), $"vwp-backup-upload-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(orphanPath, "orphan", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(orphanPath, DateTime.UtcNow.AddHours(-25));

        BackupUploadSession? fresh = null;
        try
        {
            fresh = (await service.BeginSessionAsync("fresh.bak", 10, TestContext.Current.CancellationToken)).Session!;

            Assert.False(File.Exists(orphanPath));
            Assert.True(File.Exists(fresh.TempPath));
            Assert.Same(fresh, service.GetSession(fresh.Id));
        }
        finally
        {
            await DisposeSessionAsync(fresh);
            if (File.Exists(orphanPath))
                File.Delete(orphanPath);
        }
    }

    [Fact]
    public async Task GetSession_WithinCleanupInterval_SkipsTempFileScan()
    {
        using var provider = CreateProvider();
        var time = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        var service = CreateService(provider, time);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;
        var orphanPath = Path.Combine(Path.GetTempPath(), $"vwp-backup-upload-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(orphanPath, "orphan", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(orphanPath, DateTime.UtcNow.AddHours(-25));

        try
        {
            Assert.Null(service.GetSession(Guid.NewGuid()));
            Assert.True(File.Exists(orphanPath));

            time.Advance(BackupUploadSessionService.CleanupInterval.Add(TimeSpan.FromMinutes(1)));

            Assert.Null(service.GetSession(Guid.NewGuid()));
            Assert.False(File.Exists(orphanPath));
        }
        finally
        {
            await DisposeSessionAsync(session);
            if (File.Exists(orphanPath))
                File.Delete(orphanPath);
        }
    }
}
