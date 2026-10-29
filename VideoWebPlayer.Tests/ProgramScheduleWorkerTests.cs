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
        await AssertWorkerCreatesSlotsAsync(ct, tvSenderEnabled: true, expectSlots: true);
    }

    [Fact]
    public async Task Worker_IdlesWithoutSchedule_WhenFeatureNotReleased()
    {
        var ct = TestContext.Current.CancellationToken;
        // Feature-Flag aus: der Worker behaelt seinen Takt, darf aber keinen Sendeplatz
        // materialisieren — Reconcile/EnsureCoverage werden uebersprungen.
        await AssertWorkerCreatesSlotsAsync(ct, tvSenderEnabled: false, expectSlots: false);
    }

    [Fact]
    public async Task Worker_Run_PurgesExpiredRules()
    {
        var ct = TestContext.Current.CancellationToken;
        await AssertWorkerPurgeAsync(ct, tvSenderEnabled: true, expectPurged: true);
    }

    [Fact]
    public async Task Worker_DisabledFeature_SkipsPurge()
    {
        var ct = TestContext.Current.CancellationToken;
        // Feature-Flag aus: auch die Ablauf-Bereinigung pausiert — die Regel bleibt bestehen.
        await AssertWorkerPurgeAsync(ct, tvSenderEnabled: false, expectPurged: false);
    }

    private static async Task AssertWorkerPurgeAsync(CancellationToken ct, bool tvSenderEnabled, bool expectPurged)
    {
        var connectionString = $"Data Source=file:schedule-worker-purge-{Guid.NewGuid():N}?mode=memory&cache=shared";
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

        long ruleId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync(ct);
            var channel = new TVChannel { Name = "TV-Sender" };
            db.TVChannels.Add(channel);
            var source = new MediaSource { Name = "Q", Path = "/q", Host = "h", Port = 1 };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync(ct);
            var movie = new Movie { Name = "M", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
            db.Movies.Add(movie);
            var localYesterday = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
            var rule = new ProgramRule
            {
                TVChannelId = channel.Id,
                Name = "Abgelaufen",
                RuleType = ProgramRuleType.MoviePrimeTime,
                Recurrence = ProgramRuleRecurrence.Daily,
                TimeOfDay = new TimeOnly(20, 0),
                EpisodeCount = 1,
                MaterialType = ProgramRuleMaterialType.Movie,
                MaterialId = movie.Id,
                IsActive = true,
                ValidityMode = ProgramRuleValidityMode.OneTime,
                ValidTo = localYesterday
            };
            db.ProgramRules.Add(rule);
            db.Setups.Add(new Setup { TvSenderEnabled = tvSenderEnabled, ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted });
            await db.SaveChangesAsync(ct);
            ruleId = rule.Id;
        }

        var worker = new ProgramScheduleWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProgramScheduleWorker>.Instance,
            initialDelay: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await worker.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(expectPurged ? 30 : 5);
        var ruleExists = true;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = provider.CreateScope();
            ruleExists = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .ProgramRules.AnyAsync(r => r.Id == ruleId, ct);
            if (!ruleExists)
                break;
            await Task.Delay(200, ct);
        }

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        Assert.Equal(!expectPurged, ruleExists);
    }

    private static async Task AssertWorkerCreatesSlotsAsync(
        CancellationToken ct, bool tvSenderEnabled, bool expectSlots)
    {
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
            // Der Test zielt auf die Worker-Mechanik, nicht auf das Jugendschutzfenster —
            // der freigabelose Film bleibt mit Unrestricted zu jeder Sendezeit planbar.
            db.Setups.Add(new Setup { TvSenderEnabled = tvSenderEnabled, ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted });
            await db.SaveChangesAsync(ct);
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

        var deadline = DateTime.UtcNow.AddSeconds(expectSlots ? 30 : 5);
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

        Assert.Equal(expectSlots, created > 0);
    }

    // End-to-end: ein Worker-Takt bereinigt ein bewiesenes Selbst-Verdraengungs-Zwillingpaar —
    // die verdrängte Kopie verschwindet, der geplante Zwilling und eine echte
    // Verdrängungshistorie bleiben.
    [Fact]
    public async Task Worker_Run_CleansSelfDisplacedDuplicates()
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

        // Historisches Zwillingpaar: das Reconcile lädt es nicht (vollständig ausgestrahlt),
        // die Bereinigung entfernt trotzdem nur die bewiesene Kopie.
        var start = DateTime.UtcNow.AddDays(-3);
        var end = start.AddMinutes(90);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync(ct);
            var channel = await db.TVChannels.FirstOrDefaultAsync(ct)
                ?? db.TVChannels.Add(new TVChannel { Name = "TV-Sender" }).Entity;
            await db.SaveChangesAsync(ct);
            var source = new MediaSource { Name = "Q", Path = "/q", Host = "h", Port = 1 };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync(ct);
            var collection = new MediaCollection { Name = "C", MediaSourceId = source.Id, Path = "/c", CreatedAt = DateTime.UtcNow };
            db.MediaCollections.Add(collection);
            await db.SaveChangesAsync(ct);
            var item = new MediaItem { Name = "f.mkv", Path = "/c/f.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90) };
            db.MediaItems.Add(item);
            var movie = new Movie { Name = "M", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
            db.Movies.Add(movie);
            await db.SaveChangesAsync(ct);
            db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
            db.Setups.Add(new Setup { TvSenderEnabled = true, ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted });
            await db.SaveChangesAsync(ct);
            var rule = new ProgramRule
            {
                TVChannelId = channel.Id, Name = "Primetime", RuleType = ProgramRuleType.MoviePrimeTime,
                Recurrence = ProgramRuleRecurrence.Daily, TimeOfDay = new TimeOnly(20, 0), EpisodeCount = 1,
                MaterialType = ProgramRuleMaterialType.Movie, MaterialId = movie.Id, IsActive = true
            };
            db.ProgramRules.Add(rule);
            await db.SaveChangesAsync(ct);
            ProgramSlot Row(ProgramSlotStatus status) => new()
            {
                TVChannelId = channel.Id, Start = start, End = end, MediaItemId = item.Id, MovieId = movie.Id,
                ProgramRuleId = rule.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = status,
                CreatedAt = DateTime.UtcNow
            };
            db.ProgramSlots.Add(Row(ProgramSlotStatus.Scheduled));
            db.ProgramSlots.Add(Row(ProgramSlotStatus.Displaced)); // bewiesenes Duplikat
            db.ProgramSlots.Add(new ProgramSlot // echte Verdraengungshistorie: kein Zwilling
            {
                TVChannelId = channel.Id, Start = start.AddDays(1), End = end.AddDays(1), MediaItemId = item.Id,
                MovieId = movie.Id, ProgramRuleId = rule.Id, RuleType = ProgramRuleType.MoviePrimeTime,
                Status = ProgramSlotStatus.Displaced, CreatedAt = DateTime.UtcNow
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
        var done = false;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = provider.CreateScope();
            var displaced = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .ProgramSlots.CountAsync(s => s.Status == ProgramSlotStatus.Displaced, ct);
            if (displaced == 1)
            {
                done = true;
                break;
            }
            await Task.Delay(200, ct);
        }
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        Assert.True(done, "Der Worker-Task hat das Duplikat nicht entfernt.");
        using var check = provider.CreateScope();
        var dbCheck = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scheduled = await dbCheck.ProgramSlots.CountAsync(s => s.Status == ProgramSlotStatus.Scheduled, ct);
        Assert.True(scheduled > 0);
    }
}
