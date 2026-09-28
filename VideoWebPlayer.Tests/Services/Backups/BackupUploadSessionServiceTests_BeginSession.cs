using VideoWebPlayer.Services.Backups;
using Xunit;
using static VideoWebPlayer.Tests.Helpers.BackupUploadSessionServiceTestSupport;

namespace VideoWebPlayer.Tests.Services.Backups;

public sealed class BackupUploadSessionServiceTests_BeginSession
{
    [Fact]
    public async Task BeginSessionAsync_WithValidInput_CreatesSessionAndTempFile()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var session = (await service.BeginSessionAsync("test.bak", 100, TestContext.Current.CancellationToken)).Session;

        try
        {
            Assert.NotNull(session);
            Assert.True(File.Exists(session.TempPath));
            Assert.StartsWith(tempDir.Path, session.TempPath);
            Assert.Equal("test.bak", session.FileName);
            Assert.Equal(100, session.TotalLength);
            Assert.Equal(0, session.ReceivedBytes);
        }
        finally
        {
            await DisposeSessionAsync(session);
        }
    }

    [Fact]
    public async Task BeginSessionAsync_WithEmptyFileName_Fails()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var result = await service.BeginSessionAsync("", 100, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task BeginSessionAsync_WithPathInFileName_Fails()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var result = await service.BeginSessionAsync($"sub{Path.DirectorySeparatorChar}dir.bak", 100, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task BeginSessionAsync_WithZeroLength_Fails()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var result = await service.BeginSessionAsync("a.bak", 0, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task BeginSessionAsync_WithNegativeLength_Fails()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var result = await service.BeginSessionAsync("a.bak", -5, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task BeginSessionAsync_WithLengthAboveMaxUploadSize_Fails()
    {
        using var provider = CreateProvider(maxUploadSizeBytes: 1024);
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var result = await service.BeginSessionAsync("big.bak", 2048, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.ExceedsUploadLimit);
        Assert.Null(result.Session);
        Assert.Equal("Die Datei überschreitet das Upload-Limit von 1 KB.", result.Error);
    }

    [Fact]
    public async Task BeginSessionAsync_WithLengthAboveMaxUploadSize_FormatsGigabyteLimit()
    {
        using var provider = CreateProvider(maxUploadSizeBytes: 5L * 1024 * 1024 * 1024);
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var result = await service.BeginSessionAsync("big.bak", 6L * 1024 * 1024 * 1024, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.ExceedsUploadLimit);
        Assert.Equal("Die Datei überschreitet das Upload-Limit von 5 GB.", result.Error);
        Assert.DoesNotContain("5368709120", result.Error);
    }

    [Fact]
    public async Task BeginSessionAsync_WithSameFileName_CreatesDistinctSessions()
    {
        using var provider = CreateProvider();
        using var tempDir = TempDirectory.Create();
        var service = CreateService(provider, tempDir.Path);

        var first = (await service.BeginSessionAsync("same.bak", 10, TestContext.Current.CancellationToken)).Session!;
        BackupUploadSession? second = null;
        try
        {
            second = (await service.BeginSessionAsync("same.bak", 10, TestContext.Current.CancellationToken)).Session!;

            Assert.NotEqual(first.Id, second.Id);
            Assert.NotEqual(first.TempPath, second.TempPath);
            Assert.True(File.Exists(first.TempPath));
            Assert.True(File.Exists(second.TempPath));
        }
        finally
        {
            await DisposeSessionAsync(first);
            await DisposeSessionAsync(second);
        }
    }
}
