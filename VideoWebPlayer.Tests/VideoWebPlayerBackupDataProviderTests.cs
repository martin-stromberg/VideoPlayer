using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task RestoreAsync_ReportsDataSetAndRecordProgress()
    {
        using var temp = new TempDirectory();
        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        sourceDb.MediaSources.AddRange(
            new MediaSource { Id = 101, Name = "Source A", Path = "/source-a", Host = "localhost", Port = 22 },
            new MediaSource { Id = 102, Name = "Source B", Path = "/source-b", Host = "localhost", Port = 22 });
        await sourceDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var progress = new CapturingProgress();

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            exported.Index,
            new BackupRestoreContext(null, exported.OpenAsync, progress),
            TestContext.Current.CancellationToken);

        Assert.Contains(progress.Values, x =>
            x.DataSetName == "MediaSources"
            && x.DataSetNumber > 0
            && x.DataSetTotal >= x.DataSetNumber
            && x.RecordNumber == 2
            && x.RecordTotal == 2);
    }

    [Fact]
    public async Task RestoreAsync_AcceptsLegacyPayloadWithoutUpdateSettings()
    {
        using var temp = new TempDirectory();
        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        sourceDb.Setups.Add(new Setup
        {
            Id = 1,
            ApplicationTitle = "Legacy Title",
            ScanProcessIntervalMinutes = 15,
            MediaCollectionScanIntervalDays = 3
        });
        sourceDb.MediaSources.Add(new MediaSource { Id = 12, Name = "Legacy", Path = "/legacy", Host = "localhost", Port = 22 });
        await sourceDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, temp.Path),
            TestContext.Current.CancellationToken);

        var legacyIndex = CreateLegacyIndexWithoutUpdateSettings(exported.Index);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            legacyIndex,
            new BackupRestoreContext(null, (entryName, token) => OpenLegacyPayloadAsync(exported, entryName, token)),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        Assert.True(await targetDb.MediaSources.AnyAsync(x => x.Id == 12, TestContext.Current.CancellationToken));
        var setup = await targetDb.Setups.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(15, setup.ScanProcessIntervalMinutes);
        Assert.Equal("Martins Videosammlung", setup.ApplicationTitle);
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
    public async Task RestoreAsync_MapsRestoredUserConflictingWithExecutingAdminUserName()
    {
        using var temp = new TempDirectory();
        await using var payloadConnection = new SqliteConnection("Data Source=:memory:");
        await payloadConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var payloadDb = CreateDb(payloadConnection);
        await payloadDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        payloadDb.Users.Add(new ApplicationUser
        {
            Id = "backup-user",
            UserName = "admin@example.test",
            NormalizedUserName = "ADMIN@EXAMPLE.TEST",
            Email = "admin@example.test",
            NormalizedEmail = "ADMIN@EXAMPLE.TEST",
            Sources = "[101]",
            IsAdmin = false
        });
        payloadDb.MediaSources.Add(new MediaSource { Id = 101, Name = "Source", Path = "/source", Host = "localhost", Port = 22 });
        payloadDb.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = 101, UserId = "backup-user" });
        await payloadDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var payload = await ExportProviderPayloadAsync(
            CreateProvider(payloadDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        targetDb.Users.Add(new ApplicationUser
        {
            Id = "admin",
            UserName = "admin@example.test",
            NormalizedUserName = "ADMIN@EXAMPLE.TEST",
            Email = "admin@example.test",
            NormalizedEmail = "ADMIN@EXAMPLE.TEST",
            IsAdmin = true,
            Sources = "[1]"
        });
        await targetDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            payload.Index,
            new BackupRestoreContext("admin", payload.OpenAsync),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        var admin = await targetDb.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("admin", admin.Id);
        Assert.True(admin.IsAdmin);
        Assert.Equal("[101]", admin.Sources);
        var mediaSourceUser = await targetDb.MediaSourceUsers.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("admin", mediaSourceUser.UserId);
        Assert.Equal(101, mediaSourceUser.MediaSourceId);
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

    [Fact]
    public async Task ExportAsync_ExcludesGeneratedBackgroundPictures()
    {
        using var temp = new TempDirectory();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var (userPicture, generatedPicture, episode) = await SeedGeneratedBackgroundScenarioAsync(db);

        var provider = CreateProvider(db, temp.Path);
        await using var exported = await ExportProviderPayloadAsync(provider, TestContext.Current.CancellationToken);

        var pictureIds = await ReadRowLongPropertyAsync(exported, "entities/Pictures.json", "Id");
        Assert.Contains(userPicture.Id, pictureIds);
        Assert.DoesNotContain(generatedPicture.Id, pictureIds);

        var episodeRow = await ReadSingleRowAsync(exported, "entities/TVShowEpisodes.json", "Id", episode.Id);
        Assert.Equal(JsonValueKind.Null, episodeRow.GetProperty("GeneratedBackgroundPictureId").ValueKind);
        Assert.True(GetBoolValue(episodeRow.GetProperty("BackgroundImageRequiresUpdate")));
    }

    [Fact]
    public async Task RestoreAsync_DoesNotViolateForeignKeys_WhenGeneratedBackgroundImageWasExcluded()
    {
        using var temp = new TempDirectory();
        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var (_, _, episode) = await SeedGeneratedBackgroundScenarioAsync(sourceDb);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, temp.Path),
            TestContext.Current.CancellationToken);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            exported.Index,
            new BackupRestoreContext(null, exported.OpenAsync),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        var restoredEpisode = await targetDb.TVShowEpisodes.SingleAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
        Assert.Null(restoredEpisode.GeneratedBackgroundPictureId);
        Assert.True(restoredEpisode.BackgroundImageRequiresUpdate);
        Assert.False(await targetDb.Pictures.AnyAsync(p => p.IsGeneratedBackground, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestoreAsync_AcceptsLegacyPayloadWithoutEpisodeBackgroundImageColumns()
    {
        using var temp = new TempDirectory();
        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var (userPicture, _, episode) = await SeedGeneratedBackgroundScenarioAsync(sourceDb);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, temp.Path),
            TestContext.Current.CancellationToken);

        var legacyIndex = CreateLegacyIndexWithoutEpisodeBackgroundImageColumns(exported.Index);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            legacyIndex,
            new BackupRestoreContext(
                null,
                (entryName, token) => OpenLegacyPayloadWithoutEpisodeBackgroundImageColumnsAsync(exported, entryName, token)),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        var restoredEpisode = await targetDb.TVShowEpisodes.SingleAsync(e => e.Id == episode.Id, TestContext.Current.CancellationToken);
        Assert.Null(restoredEpisode.GeneratedBackgroundPictureId);
        Assert.False(restoredEpisode.BackgroundImageRequiresUpdate);
        Assert.Null(restoredEpisode.BackgroundImageGeneratedAt);

        var restoredPicture = await targetDb.Pictures.SingleAsync(p => p.Id == userPicture.Id, TestContext.Current.CancellationToken);
        Assert.False(restoredPicture.IsGeneratedBackground);
        Assert.Null(restoredPicture.EpisodeId);
    }

    [Fact]
    public async Task RestoreAsync_AcceptsLegacyPayloadWithoutManualMetadataColumns()
    {
        using var temp = new TempDirectory();
        await using var sourceConnection = new SqliteConnection("Data Source=:memory:");
        await sourceConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var sourceDb = CreateDb(sourceConnection);
        await sourceDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var mediaSource = new MediaSource { Id = 201, Name = "Source", Path = "/source", Host = "localhost", Port = 22 };
        var collection = new MediaCollection { Id = 202, Name = "Collection", Path = "/source/show", MediaSourceId = mediaSource.Id, CreatedAt = DateTime.UtcNow };
        var movieCollection = new MovieCollection { Id = 203, Name = "Movies", MediaSourceId = mediaSource.Id, CollectionId = collection.Id, CreatedAt = DateTime.UtcNow, IsManuallyEdited = true };
        var movie = new Movie { Id = 204, Name = "Movie", MediaSourceId = mediaSource.Id, CollectionId = collection.Id, MovieCollectionId = movieCollection.Id, CreatedAt = DateTime.UtcNow, IsManuallyEdited = true };
        var show = new TVShow { Id = 205, Name = "Show", MediaSourceId = mediaSource.Id, CollectionId = collection.Id, CreatedAt = DateTime.UtcNow, IsManuallyEdited = true };
        var season = new TVShowSeason { Id = 206, Name = "Season", TVShowId = show.Id, MediaSourceId = mediaSource.Id, CollectionId = collection.Id, CreatedAt = DateTime.UtcNow, IsManuallyEdited = true };
        var episode = new TVShowEpisode { Id = 207, Name = "Episode", TVShowSeasonId = season.Id, MediaSourceId = mediaSource.Id, CollectionId = collection.Id, CreatedAt = DateTime.UtcNow, IsManuallyEdited = true };

        sourceDb.MediaSources.Add(mediaSource);
        sourceDb.MediaCollections.Add(collection);
        sourceDb.MovieCollections.Add(movieCollection);
        sourceDb.Movies.Add(movie);
        sourceDb.TVShows.Add(show);
        sourceDb.TVShowSeasons.Add(season);
        sourceDb.TVShowEpisodes.Add(episode);
        await sourceDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var exported = await ExportProviderPayloadAsync(
            CreateProvider(sourceDb, temp.Path),
            TestContext.Current.CancellationToken);

        var legacyIndex = CreateLegacyIndexWithoutManualMetadataColumns(exported.Index);

        await using var targetConnection = new SqliteConnection("Data Source=:memory:");
        await targetConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var targetDb = CreateDb(targetConnection);
        await targetDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await CreateProvider(targetDb, temp.Path).RestoreAsync(
            legacyIndex,
            new BackupRestoreContext(
                null,
                (entryName, token) => OpenLegacyPayloadWithoutManualMetadataColumnsAsync(exported, entryName, token)),
            TestContext.Current.CancellationToken);

        targetDb.ChangeTracker.Clear();
        Assert.False((await targetDb.MovieCollections.SingleAsync(x => x.Id == movieCollection.Id, TestContext.Current.CancellationToken)).IsManuallyEdited);
        Assert.False((await targetDb.Movies.SingleAsync(x => x.Id == movie.Id, TestContext.Current.CancellationToken)).IsManuallyEdited);
        Assert.False((await targetDb.TVShows.SingleAsync(x => x.Id == show.Id, TestContext.Current.CancellationToken)).IsManuallyEdited);
        Assert.False((await targetDb.TVShowSeasons.SingleAsync(x => x.Id == season.Id, TestContext.Current.CancellationToken)).IsManuallyEdited);
        Assert.False((await targetDb.TVShowEpisodes.SingleAsync(x => x.Id == episode.Id, TestContext.Current.CancellationToken)).IsManuallyEdited);
    }

    private static readonly (string Table, string Column)[] EpisodeBackgroundImageLegacyColumns =
    {
        (nameof(ApplicationDbContext.TVShowEpisodes), nameof(TVShowEpisode.GeneratedBackgroundPictureId)),
        (nameof(ApplicationDbContext.TVShowEpisodes), nameof(TVShowEpisode.BackgroundImageRequiresUpdate)),
        (nameof(ApplicationDbContext.TVShowEpisodes), nameof(TVShowEpisode.BackgroundImageGeneratedAt)),
        (nameof(ApplicationDbContext.Pictures), nameof(Picture.IsGeneratedBackground)),
        (nameof(ApplicationDbContext.Pictures), nameof(Picture.EpisodeId))
    };

    private static readonly (string Table, string Column)[] ManualMetadataLegacyColumns =
    {
        (nameof(ApplicationDbContext.MovieCollections), nameof(MovieCollection.IsManuallyEdited)),
        (nameof(ApplicationDbContext.Movies), nameof(Movie.IsManuallyEdited)),
        (nameof(ApplicationDbContext.TVShowEpisodes), nameof(TVShowEpisode.IsManuallyEdited)),
        (nameof(ApplicationDbContext.TVShows), nameof(TVShow.IsManuallyEdited)),
        (nameof(ApplicationDbContext.TVShowSeasons), nameof(TVShowSeason.IsManuallyEdited))
    };

    private static MemoryStream CreateLegacyIndexWithoutEpisodeBackgroundImageColumns(MemoryStream index)
    {
        index.Position = 0;
        var root = JsonNode.Parse(index)!;
        var tables = root["tables"]!.AsArray();

        foreach (var table in tables)
        {
            var tableName = table!["name"]?.GetValue<string>();
            var columns = table["columns"]!.AsArray();
            for (var columnIndex = columns.Count - 1; columnIndex >= 0; columnIndex--)
            {
                var columnName = columns[columnIndex]?.GetValue<string>();
                if (EpisodeBackgroundImageLegacyColumns.Any(x =>
                        string.Equals(x.Table, tableName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.Column, columnName, StringComparison.OrdinalIgnoreCase)))
                {
                    columns.RemoveAt(columnIndex);
                }
            }
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    private static MemoryStream CreateLegacyIndexWithoutManualMetadataColumns(MemoryStream index)
    {
        index.Position = 0;
        var root = JsonNode.Parse(index)!;
        RemoveColumnsFromIndex(root, ManualMetadataLegacyColumns);
        return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    private static async Task<Stream> OpenLegacyPayloadWithoutEpisodeBackgroundImageColumnsAsync(
        ExportedPayload exported,
        string entryName,
        CancellationToken cancellationToken)
    {
        var stream = await exported.OpenAsync(entryName, cancellationToken);
        var columnsToRemove = EpisodeBackgroundImageLegacyColumns
            .Where(x => entryName.Contains(x.Table, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Column)
            .ToList();
        if (columnsToRemove.Count == 0)
            return stream;

        await using (stream)
        {
            var root = JsonNode.Parse(stream)!;
            foreach (var row in root["rows"]!.AsArray())
            {
                foreach (var column in columnsToRemove)
                    row!.AsObject().Remove(column);
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        }
    }

    private static async Task<Stream> OpenLegacyPayloadWithoutManualMetadataColumnsAsync(
        ExportedPayload exported,
        string entryName,
        CancellationToken cancellationToken)
    {
        var stream = await exported.OpenAsync(entryName, cancellationToken);
        return await RemoveColumnsFromPayloadAsync(stream, entryName, ManualMetadataLegacyColumns);
    }

    private static void RemoveColumnsFromIndex(JsonNode root, IReadOnlyCollection<(string Table, string Column)> columnsToRemove)
    {
        var tables = root["tables"]!.AsArray();
        foreach (var table in tables)
        {
            var tableName = table!["name"]?.GetValue<string>();
            var columns = table["columns"]!.AsArray();
            for (var columnIndex = columns.Count - 1; columnIndex >= 0; columnIndex--)
            {
                var columnName = columns[columnIndex]?.GetValue<string>();
                if (columnsToRemove.Any(x =>
                        string.Equals(x.Table, tableName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.Column, columnName, StringComparison.OrdinalIgnoreCase)))
                {
                    columns.RemoveAt(columnIndex);
                }
            }
        }
    }

    private static async Task<Stream> RemoveColumnsFromPayloadAsync(
        Stream stream,
        string entryName,
        IReadOnlyCollection<(string Table, string Column)> columnsToRemove)
    {
        var payloadColumnsToRemove = columnsToRemove
            .Where(x => entryName.Contains(x.Table, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Column)
            .ToList();
        if (payloadColumnsToRemove.Count == 0)
            return stream;

        await using (stream)
        {
            var root = JsonNode.Parse(stream)!;
            foreach (var row in root["rows"]!.AsArray())
            {
                foreach (var column in payloadColumnsToRemove)
                    row!.AsObject().Remove(column);
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        }
    }

    private static async Task<(Picture UserPicture, Picture GeneratedPicture, TVShowEpisode Episode)> SeedGeneratedBackgroundScenarioAsync(ApplicationDbContext db)
    {
        var source = new MediaSource { Name = "Source", Path = "/source", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collection = new MediaCollection { Name = "Collection", Path = "/source/collection", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mediaItem = new MediaItem { Name = "fanart.jpg", Path = "/source/collection/fanart.jpg", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var userPicture = new Picture { MediaItemId = mediaItem.Id, Type = "fanart", Data = new byte[] { 1 }, ContentType = "image/jpeg", IsGeneratedBackground = false };
        db.Pictures.Add(userPicture);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var generatedPicture = new Picture { MediaItemId = mediaItem.Id, Type = "background", Data = new byte[] { 2 }, ContentType = "image/jpeg", IsGeneratedBackground = true };
        db.Pictures.Add(generatedPicture);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        generatedPicture.EpisodeId = null;

        var show = new TVShow { Name = "Show", MediaSourceId = source.Id, CollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var season = new TVShowSeason { Name = "Staffel 01", TVShowId = show.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var episode = new TVShowEpisode
        {
            Name = "Episode",
            Number = 1,
            TVShowSeasonId = season.Id,
            CreatedAt = DateTime.UtcNow,
            GeneratedBackgroundPictureId = generatedPicture.Id,
            BackgroundImageRequiresUpdate = false,
            BackgroundImageGeneratedAt = DateTime.UtcNow
        };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        generatedPicture.EpisodeId = episode.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (userPicture, generatedPicture, episode);
    }

    private static async Task<List<long>> ReadRowLongPropertyAsync(ExportedPayload exported, string entryName, string propertyName)
    {
        await using var stream = await exported.OpenAsync(entryName, TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty("rows").EnumerateArray()
            .Select(row => row.GetProperty(propertyName).GetInt64())
            .ToList();
    }

    private static async Task<JsonElement> ReadSingleRowAsync(ExportedPayload exported, string entryName, string keyProperty, long keyValue)
    {
        await using var stream = await exported.OpenAsync(entryName, TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty(keyProperty).GetInt64() == keyValue)
            .Clone();
    }

    private static bool GetBoolValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => element.GetInt64() != 0,
        _ => throw new InvalidOperationException($"Unerwarteter Werttyp: {element.ValueKind}")
    };

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

    private static MemoryStream CreateLegacyIndexWithoutUpdateSettings(MemoryStream index)
    {
        index.Position = 0;
        var root = JsonNode.Parse(index)!;
        var tables = root["tables"]!.AsArray();

        for (var i = tables.Count - 1; i >= 0; i--)
        {
            var table = tables[i]!;
            var tableName = table["name"]?.GetValue<string>();
            if (string.Equals(tableName, "UpdateSettings", StringComparison.OrdinalIgnoreCase))
            {
                tables.RemoveAt(i);
                continue;
            }

            if (string.Equals(tableName, "Setups", StringComparison.OrdinalIgnoreCase))
            {
                var columns = table["columns"]!.AsArray();
                for (var columnIndex = columns.Count - 1; columnIndex >= 0; columnIndex--)
                {
                    if (string.Equals(columns[columnIndex]?.GetValue<string>(), "ApplicationTitle", StringComparison.OrdinalIgnoreCase))
                        columns.RemoveAt(columnIndex);
                }
            }
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }

    private static async Task<Stream> OpenLegacyPayloadAsync(ExportedPayload exported, string entryName, CancellationToken cancellationToken)
    {
        var stream = await exported.OpenAsync(entryName, cancellationToken);
        if (!entryName.Contains("Setups", StringComparison.OrdinalIgnoreCase))
            return stream;

        await using (stream)
        {
            var root = JsonNode.Parse(stream)!;
            foreach (var row in root["rows"]!.AsArray())
                row!.AsObject().Remove("ApplicationTitle");

            return new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        }
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

    private sealed class CapturingProgress : IProgress<BackupRestoreProgress>
    {
        public List<BackupRestoreProgress> Values { get; } = new();

        public void Report(BackupRestoreProgress value)
        {
            Values.Add(value);
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
