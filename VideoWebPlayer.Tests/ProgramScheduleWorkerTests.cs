using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramScheduleWorkerTests
{
    [Fact]
    public async Task Worker_RunsEnsureCoverage_OnStartup()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=file:schedule-worker-{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<ProgramRuleService>();
        services.AddScoped<IProgramRuleService>(sp => sp.GetRequiredService<ProgramRuleService>());
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<ILogger<ProgramScheduleService>>(NullLogger<ProgramScheduleService>.Instance);
        services.AddSingleton<ILogger<ProgramScheduleWorker>>(NullLogger<ProgramScheduleWorker>.Instance);
        services.AddScoped<IProgramScheduleService, ProgramScheduleService>();
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync(ct);
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var channel = await db.TVChannels.FirstOrDefaultAsync(ct)
                ?? db.TVChannels.Add(new TVChannel { Name = "TV-Sender" }).Entity;
            await db.SaveChangesAsync(ct);
            var source = new MediaSource { Name = "Q", Path = "/q", Host = "h", Port = 1 };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync(ct);
            var collection = new MediaCollection { Name = "C", MediaSourceId = source.Id, Path = "/c", CreatedAt = DateTime.UtcNow };
            db.MediaCollections.Add(collection);
            await db.SaveChangesAsync(ct);
            var item = new MediaItem { Name = "f.mkv", Path = "/c/f.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(30) };
            db.MediaItems.Add(item);
            var movie = new Movie { Name = "M", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
            db.Movies.Add(movie);
            await db.SaveChangesAsync(ct);
            db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
            db.ProgramRules.Add(new ProgramRule
            {
                TVChannelId = channel.Id,
                Name = "Füller",
                RuleType = ProgramRuleType.Filler,
                Recurrence = ProgramRuleRecurrence.Daily,
                TimeOfDay = new TimeOnly(0, 0),
                EpisodeCount = 1,
                MaterialType = ProgramRuleMaterialType.Movie,
                MaterialId = movie.Id,
                IsActive = true
            });
            await db.SaveChangesAsync(ct);
        }

        var worker = new ProgramScheduleWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProgramScheduleWorker>.Instance,
            initialDelay: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await worker.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        var created = 0;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = provider.CreateScope();
            created = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProgramSlots.CountAsync(ct);
            if (created > 0)
                break;
            await Task.Delay(200, ct);
        }

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        Assert.True(created > 0, "Worker hat keine Sendeplätze erzeugt.");
    }
}
