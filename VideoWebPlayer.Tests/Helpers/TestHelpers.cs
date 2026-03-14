using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

public static class TestHelpers
{
    public static async Task WaitForMessageAsync(
        ConcurrentQueue<string> messages,
        string expected,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (messages.Any(message => message.Contains(expected)))
            {
                return;
            }

            await Task.Delay(10);
        }

        var errorMessage = messages.FirstOrDefault(message => message.Contains("Fehler im MediaSourceScanService"));
        var joinedMessages = string.Join(Environment.NewLine, messages);
        var failureMessage = errorMessage ?? $"Erwartete Logzeile nicht gefunden: '{expected}'.{Environment.NewLine}{joinedMessages}";
        Assert.True(messages.Any(message => message.Contains(expected)), failureMessage);
    }

    public static async Task DumpDatabaseStateAsync(
            IServiceProvider serviceProvider,
            ConcurrentQueue<string> messages)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var collections = await db.MediaCollections
                .OrderBy(c => c.Path)
                .Select(c => c.Path)
                .ToListAsync();
            messages.Enqueue("DB DUMP: MediaCollections:");
            foreach (var c in collections)
                messages.Enqueue($"  {c}");

            var items = await db.MediaItems
                .OrderBy(i => i.Path)
                .Select(i => i.Path)
                .ToListAsync();
            messages.Enqueue("DB DUMP: MediaItems:");
            foreach (var i in items)
                messages.Enqueue($"  {i}");

            var episodes = await db.TVShowEpisodes
                .OrderBy(e => e.TVShowSeasonId).ThenBy(e => e.Number)
                .Select(e => new { e.Id, e.TVShowSeasonId, e.Number, e.Name })
                .ToListAsync();
            messages.Enqueue($"DB DUMP: TVShowEpisodes (count={episodes.Count}):");
            foreach (var e in episodes)
                messages.Enqueue($"  Id={e.Id} SeasonId={e.TVShowSeasonId} Num={e.Number} Name={e.Name}");
        }
        catch (Exception ex)
        {
            messages.Enqueue($"DB DUMP FAILED: {ex}");
        }
    }

    public static async Task WaitForMediaCollectionAsync(
        IServiceProvider serviceProvider,
        string expectedPath,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.MediaCollections.AnyAsync(c => c.Path == expectedPath))
                {
                    return;
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
            }

            await Task.Delay(10);
        }

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Contains(db.MediaCollections, c => c.Path == expectedPath);
        }
    }

    public static async Task WaitForMediaItemAsync(
        IServiceProvider serviceProvider,
        string expectedPath,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.MediaItems.AnyAsync(item => item.Path == expectedPath))
                {
                    return;
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
            }

            await Task.Delay(10);
        }

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Contains(db.MediaItems, item => item.Path == expectedPath);
        }
    }

    public static async Task WaitForMediaItemClassifiedAsync(
        IServiceProvider serviceProvider,
        string expectedPath,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.MediaItems.AnyAsync(item => item.Path == expectedPath && item.ClassifiedAt != null))
                {
                    return;
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
            }

            await Task.Delay(10);
        }

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Contains(db.MediaItems, item => item.Path == expectedPath && item.ClassifiedAt != null);
        }
    }

    public static async Task WaitForTvShowEpisodeCountAsync(
        IServiceProvider serviceProvider,
        int expectedCount,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.TVShowEpisodes.CountAsync() == expectedCount)
                {
                    return;
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
            }

            await Task.Delay(10);
        }

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(expectedCount, await db.TVShowEpisodes.CountAsync());
        }
    }

    public static async Task WaitForMediaItemCountAsync(
        IServiceProvider serviceProvider,
        int expectedCount,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.MediaItems.CountAsync() == expectedCount)
                {
                    return;
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
            }

            await Task.Delay(10);
        }

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(expectedCount, await db.MediaItems.CountAsync());
        }
    }

    public static async Task WaitForMessageCountAsync(
        ConcurrentQueue<string> messages,
        string expected,
        int expectedCount,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (messages.Count(message => message.Contains(expected)) >= expectedCount)
            {
                return;
            }

            await Task.Delay(10);
        }

        var joinedMessages = string.Join(Environment.NewLine, messages);
        Assert.True(messages.Count(message => message.Contains(expected)) >= expectedCount,
            $"Erwartete Logzeile '{expected}' mindestens {expectedCount} mal.{Environment.NewLine}{joinedMessages}");
    }
}
