using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using Xunit;

namespace msTools.Backup.Tests;

public sealed class FileSystemBackupStoreTests
{
    [Fact]
    public async Task SaveBackupAsync_CreatesZipWithManifestAndPayload()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);

        var descriptor = await store.SaveBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "Tests"), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(descriptor.Path));
        using var archive = ZipFile.OpenRead(descriptor.Path);
        Assert.NotNull(archive.GetEntry("manifest.json"));
        Assert.NotNull(archive.GetEntry("index.json"));
    }

    [Fact]
    public async Task SaveBackupAsync_WritesFileAttachmentsUnderFilesAndReferencesThemInManifest()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path, provider: new AttachmentProvider());

        var descriptor = await store.SaveBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "Tests"), TestContext.Current.CancellationToken);

        using var archive = ZipFile.OpenRead(descriptor.Path);
        Assert.NotNull(archive.GetEntry("files/icons/action.png"));

        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
            manifestStream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            TestContext.Current.CancellationToken);

        Assert.NotNull(manifest);
        Assert.Contains("index.json", manifest.PayloadEntries);
        Assert.Contains("files/icons/action.png", manifest.PayloadEntries);
    }

    [Fact]
    public async Task ValidateAsync_RejectsNonZip()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));

        var result = await store.ValidateAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_RejectsZipWithoutManifest()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("index.json");
        }

        stream.Position = 0;
        var result = await store.ValidateAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("manifest.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_RejectsWrongProvider()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            await using (var manifest = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifest, new BackupManifest
                {
                    ProviderId = "Other",
                    AppName = "Tests",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    Generation = BackupGeneration.Manual
                }, cancellationToken: TestContext.Current.CancellationToken);
            }

            var dataEntry = archive.CreateEntry("index.json");
            await using var data = dataEntry.Open();
            await data.WriteAsync(Encoding.UTF8.GetBytes("{}"), TestContext.Current.CancellationToken);
        }

        stream.Position = 0;
        var result = await store.ValidateAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("Providerkennung", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_RejectsPathTraversalEntry()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("../evil.txt");
            var manifestEntry = archive.CreateEntry("manifest.json");
            await using (var manifest = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifest, new BackupManifest
                {
                    ProviderId = TestProvider.Id,
                    AppName = "Tests",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    Generation = BackupGeneration.Manual
                }, cancellationToken: TestContext.Current.CancellationToken);
            }

            archive.CreateEntry("index.json");
        }

        stream.Position = 0;
        var result = await store.ValidateAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("Unsicherer ZIP-Eintrag", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_AcceptsNonSeekableZipStream()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        var created = await store.SaveBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "Tests"), TestContext.Current.CancellationToken);
        var backupBytes = await File.ReadAllBytesAsync(created.Path, TestContext.Current.CancellationToken);
        await using var stream = new NonSeekableReadStream(backupBytes);

        var result = await store.ValidateAsync(stream, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnsafeManifestPayloadEntry()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            await using (var manifest = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifest, new BackupManifest
                {
                    ProviderId = TestProvider.Id,
                    AppName = "Tests",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    Generation = BackupGeneration.Manual,
                    PayloadEntries = new List<string> { "data.json", "files/../evil.txt" }
                }, cancellationToken: TestContext.Current.CancellationToken);
            }

            archive.CreateEntry("index.json");
            archive.CreateEntry("files/../evil.txt");
        }

        stream.Position = 0;
        var result = await store.ValidateAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("Unsicherer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportUploadedBackupAsync_PersistsNonSeekableValidatedStream()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        var created = await store.SaveBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "Tests"), TestContext.Current.CancellationToken);
        var uploadBytes = await File.ReadAllBytesAsync(created.Path, TestContext.Current.CancellationToken);
        await using var upload = new NonSeekableReadStream(uploadBytes);

        var imported = await store.ImportUploadedBackupAsync(upload, "backup.zip", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(imported.Path));
        Assert.Equal(uploadBytes.Length, new FileInfo(imported.Path).Length);
        await using var importedStream = File.OpenRead(imported.Path);
        Assert.True((await store.ValidateAsync(importedStream, TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task ImportUploadedBackupAsync_EnforcesUploadLimitForNonSeekableStream()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path, maxUploadSizeBytes: 8);
        await using var upload = new NonSeekableReadStream(Encoding.UTF8.GetBytes("this is too large"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ImportUploadedBackupAsync(upload, "backup.zip", TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.zip"));
    }

    [Fact]
    public async Task SaveBackupAsync_RemovesTemporaryFileWhenExportFails()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path, provider: new ThrowingProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "Tests"), TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.zip"));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredBackup()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);
        var descriptor = await store.SaveBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "Tests"), TestContext.Current.CancellationToken);

        await store.DeleteAsync(descriptor.FileName, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(descriptor.Path));
        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
    }

    private static FileSystemBackupStore CreateStore(
        string path,
        long maxUploadSizeBytes = 512L * 1024L * 1024L,
        IBackupDataProvider? provider = null)
        => new(
            new StaticOptionsProvider(path, maxUploadSizeBytes),
            provider ?? new TestProvider(),
            TimeProvider.System,
            NullLogger<FileSystemBackupStore>.Instance);

    private sealed class StaticOptionsProvider : IBackupOptionsProvider
    {
        private readonly string _path;
        private readonly long _maxUploadSizeBytes;

        public StaticOptionsProvider(string path, long maxUploadSizeBytes)
        {
            _path = path;
            _maxUploadSizeBytes = maxUploadSizeBytes;
        }

        public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new BackupOptions
            {
                StoragePath = _path,
                MaxUploadSizeBytes = _maxUploadSizeBytes
            });
    }

    private sealed class TestProvider : IBackupDataProvider
    {
        public const string Id = "Test.Provider";

        public string ProviderId => Id;

        public async Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken)
            => await target.WriteAsync(Encoding.UTF8.GetBytes("{\"ok\":true}"), cancellationToken);

        public Task<BackupValidationResult> ValidateAsync(Stream source, BackupValidationContext context, CancellationToken cancellationToken)
            => Task.FromResult(BackupValidationResult.Valid);

        public Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ThrowingProvider : IBackupDataProvider
    {
        public string ProviderId => TestProvider.Id;

        public Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("export failed");

        public Task<BackupValidationResult> ValidateAsync(Stream source, BackupValidationContext context, CancellationToken cancellationToken)
            => Task.FromResult(BackupValidationResult.Valid);

        public Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class AttachmentProvider : IBackupDataProvider
    {
        public string ProviderId => TestProvider.Id;

        public async Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken)
        {
            context.FileAttachments.Add(new BackupFileAttachment(
                "files/icons/action.png",
                async (stream, token) => await stream.WriteAsync(Encoding.UTF8.GetBytes("png"), token)));

            await target.WriteAsync(Encoding.UTF8.GetBytes("{\"ok\":true,\"files\":[{\"relativePath\":\"icons/action.png\",\"entryName\":\"files/icons/action.png\"}]}"), cancellationToken);
        }

        public Task<BackupValidationResult> ValidateAsync(Stream source, BackupValidationContext context, CancellationToken cancellationToken)
            => Task.FromResult(BackupValidationResult.Valid);

        public Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableReadStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"backup-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
