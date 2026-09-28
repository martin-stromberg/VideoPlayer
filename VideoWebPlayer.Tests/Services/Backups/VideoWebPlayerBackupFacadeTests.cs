using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

public sealed class VideoWebPlayerBackupFacadeTests : IAsyncDisposable
{
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs = [];

    [Fact]
    public async Task ImportUploadFileAsync_ValidFile_MovesToStorageAndRecordsHistory()
    {
        var storageDir = CreateTempDirectory();
        using var provider = CreateProvider(storageDir);
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tempFile = await CreateBackupTempFileAsync(withManifest: true);

        var result = await facade.ImportUploadFileAsync(tempFile, "uploaded.bak", "user-1", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(File.Exists(tempFile));
        Assert.True(File.Exists(Path.Combine(storageDir, "uploaded.bak")));

        var row = await db.BackupOperationHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Upload", row.Operation);
        Assert.True(row.Succeeded);
        Assert.Equal("user-1", row.UserId);
        Assert.Equal("uploaded.bak", row.FileName);
    }

    [Fact]
    public async Task ImportUploadFileAsync_ExistingTarget_OverwritesFile()
    {
        var storageDir = CreateTempDirectory();
        var targetPath = Path.Combine(storageDir, "existing.bak");
        await File.WriteAllTextAsync(targetPath, "old-content", TestContext.Current.CancellationToken);

        using var provider = CreateProvider(storageDir);
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
        var tempFile = await CreateBackupTempFileAsync(withManifest: true);

        var result = await facade.ImportUploadFileAsync(tempFile, "existing.bak", "user-1", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(targetPath));
        using (var archive = new ZipArchive(File.OpenRead(targetPath), ZipArchiveMode.Read))
        {
            Assert.NotNull(archive.GetEntry("manifest.json"));
        }
    }

    [Fact]
    public async Task ImportUploadFileAsync_MissingManifest_ReturnsFailureAndKeepsTempFile()
    {
        var storageDir = CreateTempDirectory();
        using var provider = CreateProvider(storageDir);
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
        var tempFile = await CreateBackupTempFileAsync(withManifest: false);

        var result = await facade.ImportUploadFileAsync(tempFile, "invalid.bak", "user-1", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(File.Exists(tempFile));
        Assert.False(File.Exists(Path.Combine(storageDir, "invalid.bak")));
    }

    [Fact]
    public async Task ImportUploadFileAsync_EmptyFile_ReturnsFailureAndKeepsTempFile()
    {
        var storageDir = CreateTempDirectory();
        using var provider = CreateProvider(storageDir);
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
        var tempFile = TrackTempFile(Path.Combine(Path.GetTempPath(), $"vwp-test-upload-{Guid.NewGuid():N}.tmp"));
        await File.WriteAllBytesAsync(tempFile, Array.Empty<byte>(), TestContext.Current.CancellationToken);

        var result = await facade.ImportUploadFileAsync(tempFile, "empty.bak", "user-1", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(File.Exists(tempFile));
        Assert.False(File.Exists(Path.Combine(storageDir, "empty.bak")));
    }

    [Fact]
    public async Task ImportUploadFileAsync_WhenCancelled_ReturnsFailureAndKeepsTempFile()
    {
        var storageDir = CreateTempDirectory();
        using var provider = CreateProvider(storageDir);
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
        var tempFile = await CreateBackupTempFileAsync(withManifest: true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await facade.ImportUploadFileAsync(tempFile, "cancelled.bak", "user-1", cts.Token);

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(Path.Combine(storageDir, "cancelled.bak")));
        Assert.True(File.Exists(tempFile));
    }

    [Fact]
    public async Task ImportUploadFileAsync_NameWithoutBakExtension_AppendsExtension()
    {
        var storageDir = CreateTempDirectory();
        using var provider = CreateProvider(storageDir);
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
        var tempFile = await CreateBackupTempFileAsync(withManifest: true);

        var result = await facade.ImportUploadFileAsync(tempFile, "backup", "user-1", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(Path.Combine(storageDir, "backup.bak")));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
            }
        }

        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }

        await ValueTask.CompletedTask;
    }

    private string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vwp-facade-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private string TrackTempFile(string path)
    {
        _tempFiles.Add(path);
        return path;
    }

    private async Task<string> CreateBackupTempFileAsync(bool withManifest)
    {
        var path = TrackTempFile(Path.Combine(Path.GetTempPath(), $"vwp-test-upload-{Guid.NewGuid():N}.tmp"));
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(withManifest ? "manifest.json" : "data.bin");
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(new byte[] { 123, 125 }, TestContext.Current.CancellationToken);
        }

        return path;
    }

    private static ServiceProvider CreateProvider(string storagePath)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new EventManager());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IBackupDataSource, NoopBackupDataSource>();
        services.AddScoped<VideoWebPlayerBackupDataFactory>();
        services.AddScoped<IBackupOptionsProvider>(_ => new StaticBackupOptionsProvider(storagePath));
        services.AddScoped<BackupSettingsService>();
        services.AddScoped<BackupOperationHistoryService>();
        services.AddScoped<VideoWebPlayerBackupFacade>();
        services.AddSingleton<IBackupService>(new ListingBackupService(storagePath));
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private sealed class StaticBackupOptionsProvider : IBackupOptionsProvider
    {
        private readonly string _storagePath;

        public StaticBackupOptionsProvider(string storagePath)
        {
            _storagePath = storagePath;
        }

        public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new BackupOptions { StoragePath = _storagePath });
    }

    private sealed class ListingBackupService : IBackupService
    {
        private readonly string _storagePath;

        public ListingBackupService(string storagePath)
        {
            _storagePath = storagePath;
        }

        public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BackupDescriptor>>(
                Directory.GetFiles(_storagePath, "*.bak")
                    .Select(path => new BackupDescriptor(
                        Path.GetFileName(path),
                        path,
                        new FileInfo(path).Length,
                        File.GetCreationTimeUtc(path),
                        BackupGeneration.Uploaded,
                        "msTools.Backup.Object",
                        0,
                        true,
                        Array.Empty<string>()))
                    .ToList());

        public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<BackupResult> StoreAsync(string backupName, BackupGeneration generation, IEnumerable<IBackupData> items, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<IBackupData>> RestoreAsync(string backupName, IBackupDataFactory factory, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoopBackupDataSource : IBackupDataSource
    {
        public Task<IReadOnlyList<IBackupData>> GetBackupDataAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IBackupData>>(Array.Empty<IBackupData>());
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "VideoWebPlayer";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
