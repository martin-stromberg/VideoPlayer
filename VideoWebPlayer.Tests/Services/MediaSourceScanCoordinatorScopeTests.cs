using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Prüft das Wecken der Wecksignale (gebündelt am Laufende) und das Warten auf die Gate-Lease
/// während eines Restores, für <see cref="MediaSourceScanCoordinator.RunFullScanAsync"/>.
/// </summary>
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceScanCoordinatorScopeTests : IDisposable
{
    private readonly string _rootDir;

    public MediaSourceScanCoordinatorScopeTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-coordinator-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task RunFullScanAsync_DefersScheduleWake_UntilScanEnds()
    {
        var ct = TestContext.Current.CancellationToken;

        var connectionString = $"Data Source=file:coordinator-scope-defer-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        // Der Classifier tritt am Laufende (ClassifyMediaItemsAsync bzw. ClassifyMediaCollectionsAsync)
        // immer in die Schreibsperre ein; das gibt einen deterministischen Haltepunkt, während der
        // Schedule-Scope des Koordinators noch offen ist.
        var gatedWriteCoordinator = new GatedMetadataWriteCoordinator();
        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(
            connectionString, new ConcurrentQueue<string>(), writeCoordinator: gatedWriteCoordinator);
        await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: false, ct);

        var scheduleSignal = serviceProvider.GetRequiredService<IProgramScheduleSignal>();

        var wakes = 0;
        using var wakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var wakeCounter = Task.Run(async () =>
        {
            while (!wakeCts.IsCancellationRequested)
            {
                try
                {
                    await scheduleSignal.WaitAsync(wakeCts.Token);
                    Interlocked.Increment(ref wakes);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);

        gatedWriteCoordinator.Hold();

        using var scope = serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
        var runTask = coordinator.RunFullScanAsync("Automatischer Scanprozess", 64, ct);

        try
        {
            await gatedWriteCoordinator.WaitUntilEnteredAsync(ct);

            // Simuliert einen Commit mit Backfill-Markern, der während des laufenden Scans eintrifft.
            scheduleSignal.NotifyScheduleChanged();

            // Gnadenfrist, während der Schedule-Scope noch offen ist: ein nicht gebündeltes
            // Wecksignal wäre längst angekommen.
            await Task.Delay(400, ct);
            Assert.Equal(0, Volatile.Read(ref wakes));

            gatedWriteCoordinator.Release();
            await runTask;

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (Volatile.Read(ref wakes) == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(50, ct);
            await Task.Delay(400, ct);

            Assert.Equal(1, Volatile.Read(ref wakes));
        }
        finally
        {
            gatedWriteCoordinator.Release();
            await wakeCts.CancelAsync();
            try { await wakeCounter; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task RunFullScanAsync_WaitsForRestoreLease_ThenRuns()
    {
        var ct = TestContext.Current.CancellationToken;

        var connectionString = $"Data Source=file:coordinator-scope-restore-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var gate = new BackgroundProcessingGate();
        var serviceProvider = MediaSourceScanCoordinatorTestServices.Build(connectionString, new ConcurrentQueue<string>(), gate: gate);
        await MediaSourceScanCoordinatorTestServices.SeedAsync(serviceProvider, _rootDir, genresChanged: false, ct);

        var restoreLease = await gate.PauseForRestoreAsync(ct);

        using var scope = serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
        var runTask = coordinator.RunFullScanAsync("Manueller Komplettscan", null, ct);

        await Task.Delay(100, ct);
        Assert.False(runTask.IsCompleted, "Der Lauf hätte auf das Ende des Restores warten müssen.");

        await restoreLease.DisposeAsync();

        var result = await runTask;
        Assert.NotNull(result);
    }
}
