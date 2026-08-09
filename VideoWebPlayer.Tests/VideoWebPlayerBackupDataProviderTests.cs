using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class VideoWebPlayerBackupDataProviderTests
{
    [Fact]
    public async Task RestoreAsync_RejectsIncompletePayloadBeforeDeletingData()
    {
        using var temp = new TempDirectory();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.MediaSources.Add(new MediaSource { Name = "Existing", Path = "/existing", Host = "localhost", Port = 22 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = CreateProvider(db, temp.Path);
        await using var payload = new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "providerId": "VideoWebPlayer.ApplicationDbContext",
              "schemaVersion": 1,
              "createdAtUtc": "2026-08-09T12:00:00Z",
              "tables": [
                { "name": "AspNetUsers", "schema": null, "columns": [], "entryName": "entities/AspNetUsers.json" }
              ],
              "files": []
            }
            """));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => provider.RestoreAsync(payload, new BackupRestoreContext("admin"), TestContext.Current.CancellationToken));

        Assert.Equal(1, await db.MediaSources.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExportRestoreAsync_RestoresRelatedRowsWithSqliteForeignKeys()
    {
        using var temp = new TempDirectory();
        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var source = new MediaSource { Id = 11, Name = "Source", Path = "/source", Host = "localhost", Port = 22 };
        var collection = new MediaCollection { Id = 22, Name = "Movies", Path = "/source/movies", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        var item = new MediaItem { Id = 33, Name = "Movie.mkv", Path = "/source/movies/Movie.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        sourceDb.MediaSources.Add(source);
        sourceDb.MediaCollections.Add(collection);
        sourceDb.MediaItems.Add(item);
        await sourceDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        targetDb.MediaSources.Add(new MediaSource { Name = "Old", Path = "/old", Host = "localhost", Port = 22 });
        await targetDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            exported.Index,
            new BackupRestoreContext(null, exported.OpenAsync),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        Assert.False(await targetDb.MediaSources.AnyAsync(x => x.Name == "Old", TestContext.Current.CancellationToken));
        Assert.True(await targetDb.MediaSources.AnyAsync(x => x.Id == 11, TestContext.Current.CancellationToken));
        Assert.True(await targetDb.MediaCollections.AnyAsync(x => x.Id == 22 && x.MediaSourceId == 11, TestContext.Current.CancellationToken));
        Assert.True(await targetDb.MediaItems.AnyAsync(x => x.Id == 33 && x.MediaCollectionId == 22, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestoreAsync_PreservesExecutingAdminWhenBackupDoesNotContainUser()
    {
        using var temp = new TempDirectory();
        await using var payloadConnection = new SqliteConnection("Data Source=:memory:");
        await payloadConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var payloadDb = CreateDb(payloadConnection);
        await payloadDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await using var payload = await ExportProviderPayloadAsync(
            CreateProvider(payloadDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        targetDb.Users.Add(new ApplicationUser { Id = "admin", UserName = "admin@example.test", IsAdmin = true, Sources = "[1]" });
        await targetDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            payload.Index,
            new BackupRestoreContext("admin", payload.OpenAsync),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        var admin = await targetDb.Users.SingleAsync(x => x.Id == "admin", TestContext.Current.CancellationToken);
        Assert.True(admin.IsAdmin);
        Assert.Equal(string.Empty, admin.Sources);
    }

    [Fact]
    public async Task RestoreAsync_ForcesExecutingAdminFlagWhenBackupContainsUser()
    {
        using var temp = new TempDirectory();
        await using var payloadConnection = new SqliteConnection("Data Source=:memory:");
        await payloadConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var payloadDb = CreateDb(payloadConnection);
        await payloadDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        payloadDb.Users.Add(new ApplicationUser { Id = "admin", UserName = "backup@example.test", IsAdmin = false, Sources = "[2]" });
        await payloadDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var payload = await ExportProviderPayloadAsync(
            CreateProvider(payloadDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            payload.Index,
            new BackupRestoreContext("admin", payload.OpenAsync),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        var admin = await targetDb.Users.SingleAsync(x => x.Id == "admin", TestContext.Current.CancellationToken);
        Assert.Equal("backup@example.test", admin.UserName);
        Assert.True(admin.IsAdmin);
    }

    [Fact]
    public async Task RestoreAsync_RemovesMediaSourceUsersAddedAfterBackup()
    {
        using var temp = new TempDirectory();
        await using var payloadConnection = new SqliteConnection("Data Source=:memory:");
        await payloadConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var payloadDb = CreateDb(payloadConnection);
        await payloadDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        payloadDb.Users.AddRange(
            new ApplicationUser { Id = "user-1", UserName = "one@example.test" },
            new ApplicationUser { Id = "user-2", UserName = "two@example.test" });
        payloadDb.MediaSources.Add(new MediaSource { Id = 101, Name = "Source", Path = "/source", Host = "localhost", Port = 22 });
        payloadDb.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = 101, UserId = "user-1" });
        await payloadDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var payload = await ExportProviderPayloadAsync(
            CreateProvider(payloadDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        targetDb.Users.AddRange(
            new ApplicationUser { Id = "user-1", UserName = "one@example.test" },
            new ApplicationUser { Id = "user-2", UserName = "two@example.test" });
        targetDb.MediaSources.Add(new MediaSource { Id = 101, Name = "Source", Path = "/source", Host = "localhost", Port = 22 });
        targetDb.MediaSourceUsers.AddRange(
            new MediaSourceUser { MediaSourceId = 101, UserId = "user-1" },
            new MediaSourceUser { MediaSourceId = 101, UserId = "user-2" });
        await targetDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            payload.Index,
            new BackupRestoreContext(null, payload.OpenAsync),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        var restoredUsers = await targetDb.MediaSourceUsers
            .Where(x => x.MediaSourceId == 101)
            .Select(x => x.UserId)
            .OrderBy(x => x)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "user-1" }, restoredUsers);
    }

    [Fact]
    public async Task ExportAsync_WritesGenreIconsAsFileEntryReferences()
    {
        using var temp = new TempDirectory();
        var iconDirectory = Path.Combine(temp.Path, "wwwroot", "images", "genres");
        Directory.CreateDirectory(iconDirectory);
        var iconBytes = Encoding.UTF8.GetBytes("icon-data");
        await File.WriteAllBytesAsync(Path.Combine(iconDirectory, "action.png"), iconBytes, TestContext.Current.CancellationToken);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await using var exported = new MemoryStream();
        var context = new BackupExportContext(BackupGeneration.Manual, DateTimeOffset.UtcNow);
        await CreateProvider(db, temp.Path).ExportAsync(exported, context, TestContext.Current.CancellationToken);

        var json = Encoding.UTF8.GetString(exported.ToArray());
        Assert.Contains("\"entryName\"", json);
        Assert.Contains("\"files/action.png\"", json);
        Assert.Contains("\"entities/", json);
        Assert.DoesNotContain("base64Content", json, StringComparison.OrdinalIgnoreCase);
        var fileAttachment = Assert.Single(context.FileAttachments, x => x.EntryName.StartsWith("files/", StringComparison.Ordinal));
        Assert.Equal("files/action.png", fileAttachment.EntryName);

        await using var attachment = new MemoryStream();
        await fileAttachment.WriteAsync(attachment, TestContext.Current.CancellationToken);
        Assert.Equal(iconBytes, attachment.ToArray());
    }

    [Fact]
    public async Task RestoreAsync_RestoresGenreIconsFromPayloadEntries()
    {
        using var sourceTemp = new TempDirectory();
        var sourceIconDirectory = Path.Combine(sourceTemp.Path, "wwwroot", "images", "genres");
        Directory.CreateDirectory(sourceIconDirectory);
        var iconBytes = Encoding.UTF8.GetBytes("restored-icon");
        await File.WriteAllBytesAsync(Path.Combine(sourceIconDirectory, "action.png"), iconBytes, TestContext.Current.CancellationToken);

        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, sourceTemp.Path),
            TestContext.Current.CancellationToken);

        using var targetTemp = new TempDirectory();
        var targetIconDirectory = Path.Combine(targetTemp.Path, "wwwroot", "images", "genres");
        Directory.CreateDirectory(targetIconDirectory);
        await File.WriteAllTextAsync(Path.Combine(targetIconDirectory, "old.png"), "old", TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, targetTemp.Path).RestoreAsync(
            exported.Index,
            new BackupRestoreContext(
                null,
                exported.OpenAsync),
            TestContext.Current.CancellationToken);

        Assert.Equal(iconBytes, await File.ReadAllBytesAsync(Path.Combine(targetIconDirectory, "action.png"), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(targetIconDirectory, "old.png")));
    }

    [Fact]
    public async Task RestoreAsync_KeepsExistingGenreIconsWhenPayloadEntryCannotBeOpened()
    {
        using var sourceTemp = new TempDirectory();
        var sourceIconDirectory = Path.Combine(sourceTemp.Path, "wwwroot", "images", "genres");
        Directory.CreateDirectory(sourceIconDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceIconDirectory, "action.png"), "new", TestContext.Current.CancellationToken);

        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, sourceTemp.Path),
            TestContext.Current.CancellationToken);

        using var targetTemp = new TempDirectory();
        var targetIconDirectory = Path.Combine(targetTemp.Path, "wwwroot", "images", "genres");
        Directory.CreateDirectory(targetIconDirectory);
        var existingPath = Path.Combine(targetIconDirectory, "old.png");
        await File.WriteAllTextAsync(existingPath, "old", TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        targetDb.MediaSources.Add(new MediaSource { Name = "Existing", Path = "/existing", Host = "localhost", Port = 22 });
        await targetDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => CreateProvider(targetDb, targetTemp.Path).RestoreAsync(
                exported.Index,
                new BackupRestoreContext(
                    null,
                    (entryName, _) =>
                    {
                        if (entryName.StartsWith("files/", StringComparison.Ordinal))
                            throw new FileNotFoundException(entryName);

                        return exported.OpenAsync(entryName, TestContext.Current.CancellationToken);
                    }),
                TestContext.Current.CancellationToken));

        Assert.True(File.Exists(existingPath));
        Assert.Equal("old", await File.ReadAllTextAsync(existingPath, TestContext.Current.CancellationToken));
        Assert.Equal(1, await targetDb.MediaSources.CountAsync(TestContext.Current.CancellationToken));
    }

    private static ApplicationDbContext CreateDb(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ApplicationDbContext(options, new EventManager());
    }

    private static VideoWebPlayerBackupDataProvider CreateProvider(ApplicationDbContext db, string root)
        => new(
            db,
            new TestWebHostEnvironment(root),
            NullLogger<VideoWebPlayerBackupDataProvider>.Instance);

    private static async Task<Dictionary<string, byte[]>> MaterializeAttachmentsAsync(
        BackupExportContext context,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var attachment in context.FileAttachments)
        {
            await using var stream = new MemoryStream();
            await attachment.WriteAsync(stream, cancellationToken);
            result[attachment.EntryName] = stream.ToArray();
        }

        return result;
    }

    private static async Task<ExportedPayload> ExportProviderPayloadAsync(
        VideoWebPlayerBackupDataProvider provider,
        CancellationToken cancellationToken)
    {
        var index = new MemoryStream();
        var context = new BackupExportContext(BackupGeneration.Manual, DateTimeOffset.UtcNow);
        await provider.ExportAsync(index, context, cancellationToken);
        index.Position = 0;
        var attachments = await MaterializeAttachmentsAsync(context, cancellationToken);
        return new ExportedPayload(index, attachments);
    }

    private sealed class ExportedPayload : IAsyncDisposable
    {
        private readonly Dictionary<string, byte[]> _attachments;

        public ExportedPayload(MemoryStream index, Dictionary<string, byte[]> attachments)
        {
            Index = index;
            _attachments = attachments;
        }

        public MemoryStream Index { get; }

        public Task<Stream> OpenAsync(string entryName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new MemoryStream(_attachments[entryName]));
        }

        public ValueTask DisposeAsync()
        {
            Index.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = Path.Combine(root, "wwwroot");
            Directory.CreateDirectory(WebRootPath);
            ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
            WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = Environments.Development;
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vwp-backup-tests-{Guid.NewGuid():N}");
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
