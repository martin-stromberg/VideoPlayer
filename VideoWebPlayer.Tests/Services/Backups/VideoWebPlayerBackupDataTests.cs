using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Ensures that database backups from earlier schema versions can still be restored.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutUnlockedMediaAndEndThreshold_RestoresSuccessfully()
    {
        // Prepare a current database with a valid admin user and setup.
        var connectionString = "Data Source=file:backuptest?mode=memory&cache=shared";
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@test.de",
            NormalizedEmail = "ADMIN@TEST.DE",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Sources = string.Empty,
            IsAdmin = true
        });

        db.Setups.Add(new Setup
        {
            DataVersion = 1,
            GenresChanged = false,
            ContinueWatchingEndThresholdSeconds = 42
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var environment = new FakeWebHostEnvironment();
        var logger = NullLogger<VideoWebPlayerBackupData>.Instance;
        var factory = new VideoWebPlayerBackupDataFactory(new ServiceCollection().BuildServiceProvider(), environment, logger)
        {
            UserId = userId
        };

        var backup = new VideoWebPlayerBackupData(
            "test",
            "VideoWebPlayer:Database",
            db,
            environment,
            logger,
            factory);

        // Create a backup of the current schema.
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, TestContext.Current.CancellationToken);
        currentStream.Position = 0;

        // Simulate an old backup that does not yet contain the UnlockedMediaEntries
        // table and the Setups.ContinueWatchingEndThresholdSeconds column.
        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        using var legacyStream = new MemoryStream();
        var setupsEntryName = string.Empty;

        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            var indexEntry = originalArchive.GetEntry("index.json")!;
            JsonNode? indexNode;
            using (var indexStream = indexEntry.Open())
            {
                indexNode = await JsonNode.ParseAsync(indexStream, cancellationToken: TestContext.Current.CancellationToken);
            }

            var tables = indexNode!["tables"]!.AsArray();
            var unlockedTable = tables.FirstOrDefault(t =>
                string.Equals(t!["name"]!.GetValue<string>(), "UnlockedMediaEntries", StringComparison.OrdinalIgnoreCase));
            if (unlockedTable is not null)
            {
                var unlockedIndex = tables.IndexOf(unlockedTable);
                if (unlockedIndex >= 0)
                    tables.RemoveAt(unlockedIndex);
            }

            var setupsTable = tables.FirstOrDefault(t =>
                string.Equals(t!["name"]!.GetValue<string>(), "Setups", StringComparison.OrdinalIgnoreCase));
            setupsEntryName = setupsTable!["entryName"]!.GetValue<string>();

            // Build an old-style Setups table without ContinueWatchingEndThresholdSeconds.
            setupsTable["columns"] = new JsonArray
            {
                "Id",
                "DataVersion",
                "GenresChanged",
                "ApplicationTitle",
                "ScanProcessIntervalMinutes",
                "MediaCollectionScanIntervalDays"
            };

            var newIndexEntry = legacyArchive.CreateEntry("index.json");
            using (var newIndexStream = newIndexEntry.Open())
            {
                await using var writer = new Utf8JsonWriter(newIndexStream, new JsonWriterOptions { Indented = true });
                indexNode!.WriteTo(writer, JsonOptions);
                await writer.FlushAsync(TestContext.Current.CancellationToken);
            }

            foreach (var entry in originalArchive.Entries)
            {
                if (entry.FullName == "index.json")
                    continue;

                if (unlockedTable is not null
                    && string.Equals(entry.FullName, unlockedTable["entryName"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(entry.FullName, setupsEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    var legacySetupsData = new JsonObject
                    {
                        ["rows"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["Id"] = 1,
                                ["DataVersion"] = 1,
                                ["GenresChanged"] = false,
                                ["ApplicationTitle"] = "Martins Videosammlung",
                                ["ScanProcessIntervalMinutes"] = 60,
                                ["MediaCollectionScanIntervalDays"] = 7
                            }
                        }
                    };

                    var newSetupsEntry = legacyArchive.CreateEntry(entry.FullName);
                    using (var newSetupsStream = newSetupsEntry.Open())
                    {
                        await using var writer = new Utf8JsonWriter(newSetupsStream, new JsonWriterOptions { Indented = true });
                        legacySetupsData.WriteTo(writer, JsonOptions);
                        await writer.FlushAsync(TestContext.Current.CancellationToken);
                    }
                }
                else
                {
                    var newEntry = legacyArchive.CreateEntry(entry.FullName);
                    using (var sourceStream = entry.Open())
                    using (var destinationStream = newEntry.Open())
                    {
                        await sourceStream.CopyToAsync(destinationStream, TestContext.Current.CancellationToken);
                    }
                }
            }
        }

        legacyStream.Position = 0;

        // Verify the simulated legacy backup really lacks the new optional data.
        using (var checkArchive = new ZipArchive(legacyStream, ZipArchiveMode.Read, true))
        {
            var checkEntry = checkArchive.GetEntry(setupsEntryName)!;
            using var checkStream = checkEntry.Open();
            var checkText = await new StreamReader(checkStream).ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("ContinueWatchingEndThresholdSeconds", checkText);
        }

        legacyStream.Position = 0;

        // This must not throw even though the backup lacks the new table and column.
        var exception = await Record.ExceptionAsync(async () =>
            await backup.ReadFromAsync(legacyStream, TestContext.Current.CancellationToken));

        Assert.Null(exception);
        Assert.False(await db.UnlockedMediaEntries.AnyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(userId, (await db.Users.FirstAsync(TestContext.Current.CancellationToken)).Id);
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
