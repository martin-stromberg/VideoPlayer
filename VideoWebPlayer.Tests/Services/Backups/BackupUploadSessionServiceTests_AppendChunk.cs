using VideoWebPlayer.Services.Backups;
using Xunit;
using static VideoWebPlayer.Tests.Helpers.BackupUploadSessionServiceTestSupport;

namespace VideoWebPlayer.Tests.Services.Backups;

public sealed class BackupUploadSessionServiceTests_AppendChunk
{
    [Fact]
    public async Task AppendChunk_WritesAtOffsetAndTracksReceivedBytes()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            var first = await session.AppendChunkAsync(0, new MemoryStream(new byte[] { 1, 2, 3, 4 }), 4, TestContext.Current.CancellationToken);
            Assert.Equal(BackupUploadAppendStatus.Written, first.Status);
            Assert.Equal(4, session.ReceivedBytes);

            var second = await session.AppendChunkAsync(4, new MemoryStream(new byte[] { 5, 6, 7, 8, 9, 10 }), 6, TestContext.Current.CancellationToken);
            Assert.Equal(BackupUploadAppendStatus.Written, second.Status);
            Assert.Equal(10, session.ReceivedBytes);

            await session.Stream.FlushAsync(TestContext.Current.CancellationToken);
            session.Stream.Position = 0;
            var bytes = new byte[session.Stream.Length];
            await session.Stream.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, bytes);
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }

    [Fact]
    public async Task AppendChunk_WithMismatchedOffset_RequiresResume()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            var result = await session.AppendChunkAsync(5, new MemoryStream(new byte[] { 1 }), 1, TestContext.Current.CancellationToken);

            Assert.Equal(BackupUploadAppendStatus.ResumeRequired, result.Status);
            Assert.Equal(0, result.Offset);
            Assert.Equal(0, session.ReceivedBytes);
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }

    [Fact]
    public async Task AppendChunk_WithOverflowBeyondTotalLength_IsRejected()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);
        var session = (await service.BeginSessionAsync("test.bak", 10, TestContext.Current.CancellationToken)).Session!;

        try
        {
            var result = await session.AppendChunkAsync(0, new MemoryStream(new byte[11]), 11, TestContext.Current.CancellationToken);

            Assert.Equal(BackupUploadAppendStatus.Rejected, result.Status);
            Assert.NotNull(result.Error);
            Assert.Equal(0, session.ReceivedBytes);
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }
}
