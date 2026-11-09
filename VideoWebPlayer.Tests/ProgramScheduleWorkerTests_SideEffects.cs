using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Side effects of a <see cref="ProgramScheduleWorker"/> iteration, proven with fakes: the
/// live-schedule notification when a pass materialized changes, the
/// <see cref="IBackgroundProcessingGate"/> lease wrapping the pass, and the per-iteration
/// re-read of the configured generation interval.
/// </summary>
public sealed class ProgramScheduleWorkerTests_SideEffects
{
    private static ProgramSchedulePassSummary Pass(int displaced = 0, int reconcileCreated = 0, int coverageCreated = 0)
        => new(0, 0,
            new ProgramScheduleReconcileSummary(displaced, reconcileCreated, 0, 0, 0, 0, 0, 0L, 0),
            new ProgramScheduleRunSummary(coverageCreated, 0, 0, 0, 0, 0, 0L, 0));

    private static async Task<ServiceProvider> BuildProviderAsync(
        string dbName, CancellationToken ct, IList<string> orderLog,
        FakeProgramScheduleService service, FakeBackgroundProcessingGate gate,
        int intervalMinutes = 60)
    {
        var connectionString = $"Data Source=file:schedule-worker-fx-{dbName}-{Guid.NewGuid():N}?mode=memory&cache=shared";
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        var services = new ServiceCollection();
        // The keeper connection keeps the shared-cache in-memory database alive: registered as
        // singleton it stays referenced (an unreferenced connection could be finalized by the GC
        // mid-test, which destroys the database under the worker's feet) and is closed when the
        // caller disposes the provider.
        services.AddSingleton(connection);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton<ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<ILogger<ProgramScheduleWorker>>(NullLogger<ProgramScheduleWorker>.Instance);
        services.AddScoped<ProgramSettingsService>();
        services.AddScoped<IProgramScheduleService>(_ => service);
        services.AddScoped<IBackgroundProcessingGate>(_ => gate);
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        db.Setups.Add(new Setup
        {
            TvSenderEnabled = true,
            ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted,
            ProgramScheduleIntervalMinutes = intervalMinutes
        });
        await db.SaveChangesAsync(ct);
        return provider;
    }

    private static async Task WaitForPassesAsync(FakeProgramScheduleService service, int count, string what, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (service.PassCalls < count && DateTime.UtcNow < deadline)
            await Task.Delay(50, ct);
        Assert.True(service.PassCalls >= count, $"Timeout: {what} (Pässe: {service.PassCalls})");
    }

    // A pass increments the call counter before the worker reaches its interval sleep — the
    // advance of the fake clock must wait until the timer is actually armed, otherwise it moves
    // the clock past nothing and the next pass never fires.
    private static async Task WaitForTimerAsync(ScheduleTestTimeProvider time, string what, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (time.PendingTimerCount == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10, ct);
        Assert.True(time.PendingTimerCount > 0, $"Timeout: {what} — kein Timer scharf");
    }

    private static async Task<(CancellationTokenSource Cts, ProgramScheduleWorker Worker)> StartWorkerAsync(
        ServiceProvider provider, FakeScheduleSignal signal, ScheduleTestTimeProvider time, CancellationToken ct)
    {
        var worker = new ProgramScheduleWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProgramScheduleWorker>.Instance,
            signal,
            initialDelay: TimeSpan.Zero,
            timeProvider: time);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await worker.StartAsync(cts.Token);
        return (cts, worker);
    }

    private static async Task StopWorkerAsync(CancellationTokenSource cts, ProgramScheduleWorker worker)
    {
        await cts.CancelAsync();
        // Bounded wait instead of StopAsync(CancellationToken.None): that overload awaits the
        // worker task with no timeout, so a worker that misses its cancellation (stuck await,
        // starved continuation) hangs the whole test run silently — the observed CI symptom.
        // WaitAsync fails the test after 15 s and surfaces the stuck worker instead; a faulted
        // ExecuteTask rethrows the worker's real exception here.
        var executeTask = worker.ExecuteTask
            ?? throw new InvalidOperationException("Der Worker wurde nicht gestartet.");
        await executeTask.WaitAsync(TimeSpan.FromSeconds(15));
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();
    }

    // Materialisierte Änderungen müssen den Live-Stream sofort wecken — nicht erst beim
    // nächsten Intervall. Ohne die Assertion bliebe ein fehlender NotifyLiveScheduleChanged-
    // Aufruf unsichtbar.
    [Fact]
    public async Task Worker_PassWithChanges_NotifiesLiveScheduleChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var orderLog = new List<string>();
        var service = new FakeProgramScheduleService(orderLog);
        service.EnqueuePass(Pass(displaced: 1));
        var gate = new FakeBackgroundProcessingGate(orderLog);
        var signal = new FakeScheduleSignal();
        var time = new ScheduleTestTimeProvider(DateTimeOffset.UtcNow);
        await using var provider = await BuildProviderAsync("notify", ct, orderLog, service, gate);

        var (cts, worker) = await StartWorkerAsync(provider, signal, time, ct);
        await WaitForPassesAsync(service, 1, "erster Pass", ct);
        await StopWorkerAsync(cts, worker);

        Assert.True(signal.LiveNotifications >= 1,
            $"NotifyLiveScheduleChanged nicht gefeuert (Pässe: {service.PassCalls})");
    }

    // Gegenprobe: ein Pass ohne materialisierte Änderungen darf den Live-Stream nicht wecken —
    // ein „immer feuern" würde die obige Assertion wirkungslos machen.
    [Fact]
    public async Task Worker_PassWithoutChanges_DoesNotNotifyLiveScheduleChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var orderLog = new List<string>();
        var service = new FakeProgramScheduleService(orderLog);
        service.EnqueuePass(Pass());
        var gate = new FakeBackgroundProcessingGate(orderLog);
        var signal = new FakeScheduleSignal();
        var time = new ScheduleTestTimeProvider(DateTimeOffset.UtcNow);
        await using var provider = await BuildProviderAsync("quiet", ct, orderLog, service, gate);

        var (cts, worker) = await StartWorkerAsync(provider, signal, time, ct);
        await WaitForPassesAsync(service, 1, "erster Pass", ct);
        await StopWorkerAsync(cts, worker);

        Assert.Equal(0, signal.LiveNotifications);
    }

    // Der Pass läuft innerhalb des Gate-Lease: ein Backup-Restore darf niemals über eine
    // laufende Sendeplan-Materialisierung fahren.
    [Fact]
    public async Task Worker_Run_WrapsPassInGateLease()
    {
        var ct = TestContext.Current.CancellationToken;
        var orderLog = new List<string>();
        var service = new FakeProgramScheduleService(orderLog);
        service.EnqueuePass(Pass());
        var gate = new FakeBackgroundProcessingGate(orderLog);
        var signal = new FakeScheduleSignal();
        var time = new ScheduleTestTimeProvider(DateTimeOffset.UtcNow);
        await using var provider = await BuildProviderAsync("gate", ct, orderLog, service, gate);

        var (cts, worker) = await StartWorkerAsync(provider, signal, time, ct);
        await WaitForPassesAsync(service, 1, "erster Pass", ct);
        await StopWorkerAsync(cts, worker);

        var sequence = orderLog.Where(e => e.StartsWith("gate") || e == "pass").ToList();
        Assert.Equal(new[] { "gate:enter", "pass", "gate:exit" }, sequence);
        Assert.Contains("Sendeplan", gate.OperationNames);
    }

    // Das Intervall wird je Iteration neu aus den Einstellungen gelesen: eine Änderung wirkt
    // ohne Worker-Neustart. Mit dem steuerbaren Zeitgeber bewiesen: Pass 2 bricht den
    // 60-Minuten-Schlaf nicht ab, aber der dritte Pass folgt schon nach der neuen 1-Minute-
    // Frist — ein zwischengespeichertes Intervall ließe ihn 60 Minuten warten.
    [Fact]
    public async Task Worker_RereadsIntervalEachIteration()
    {
        var ct = TestContext.Current.CancellationToken;
        var orderLog = new List<string>();
        var service = new FakeProgramScheduleService(orderLog);
        var gate = new FakeBackgroundProcessingGate(orderLog);
        var signal = new FakeScheduleSignal();
        var time = new ScheduleTestTimeProvider(DateTimeOffset.UtcNow);
        await using var provider = await BuildProviderAsync("interval", ct, orderLog, service, gate, intervalMinutes: 60);

        var (cts, worker) = await StartWorkerAsync(provider, signal, time, ct);
        await WaitForPassesAsync(service, 1, "erster Pass", ct);
        // PassCalls hochgezählt heißt nicht "Intervall-Schlaf scharf": erst wenn der Timer im
        // Zeitgeber steht, trifft Advance den Schlaf wirklich.
        await WaitForTimerAsync(time, "Intervall-Schlaf nach Pass 1", ct);

        // Intervall im laufenden Betrieb ändern, dann den alten Schlaf zu Ende fahren.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setup = await db.Setups.FirstAsync(ct);
            setup.ProgramScheduleIntervalMinutes = 1;
            await db.SaveChangesAsync(ct);
        }
        time.Advance(TimeSpan.FromMinutes(60));
        await WaitForPassesAsync(service, 2, "zweiter Pass", ct);
        await WaitForTimerAsync(time, "Intervall-Schlaf nach Pass 2", ct);

        // Neue Frist wirkt sofort: der dritte Pass folgt nach 1 Minute, nicht nach weiteren 60.
        time.Advance(TimeSpan.FromMinutes(1));
        await WaitForPassesAsync(service, 3, "dritter Pass", ct);
        await StopWorkerAsync(cts, worker);

        Assert.True(service.PassCalls >= 3);
    }
}
