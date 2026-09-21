using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;
using Xunit;
using static VideoWebPlayer.Tests.Helpers.BackupUploadSessionServiceTestSupport;

namespace VideoWebPlayer.Tests.Services.Backups;

public sealed class BackupUploadSessionServiceTests_Concurrency
{
    [Fact]
    public async Task AppendChunkAsync_AfterSessionAborted_ReturnsRejectedInsteadOfThrowing()
    {
        using var provider = CreateProvider();
        var service = CreateService(provider);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        await service.AbortSessionAsync(session);

        var result = await session.AppendChunkAsync(0, new MemoryStream(new byte[] { 1 }), 1, TestContext.Current.CancellationToken);

        Assert.Equal(BackupUploadAppendStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task AppendChunkAsync_WhenSessionDisposedWhileWaiting_ReturnsRejectedInsteadOfThrowing()
    {
        using var provider = CreateProvider();
        var service = CreateService(provider);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            await session.WriteLock.WaitAsync(TestContext.Current.CancellationToken);
            var waiting = session.AppendChunkAsync(0, new MemoryStream(new byte[] { 1, 2, 3, 4 }), 4, TestContext.Current.CancellationToken);

            await session.DisposeAsync();
            session.WriteLock.Release();

            var result = await waiting;

            Assert.Equal(BackupUploadAppendStatus.Rejected, result.Status);
        }
        finally
        {
            if (File.Exists(session.TempPath))
                File.Delete(session.TempPath);
        }
    }

    [Fact]
    public async Task GetSession_ExpiredSessionWhoseWriteLockIsHeld_IsNotDisposed()
    {
        using var provider = CreateProvider();
        var time = new IncrementingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1));
        var service = CreateService(provider, time);
        var session = (await service.BeginSessionAsync("active.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            await session.WriteLock.WaitAsync(TestContext.Current.CancellationToken);
            try
            {
                time.Advance(TimeSpan.FromHours(25));

                Assert.Null(service.GetSession(Guid.NewGuid()));
                Assert.True(File.Exists(session.TempPath));
                Assert.Same(session, service.GetSession(session.Id));
            }
            finally
            {
                session.WriteLock.Release();
            }
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }
}
