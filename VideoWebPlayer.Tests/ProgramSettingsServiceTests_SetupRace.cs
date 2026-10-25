using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression tests for Issue #279: two writers racing to create the single Setups row on an
/// empty table must converge on one row. Before the fix both could pass their existence check,
/// insert two rows and leave readers to answer the <see cref="Setup.TvSenderEnabled"/> check
/// from whichever duplicate came first — the bimodal 404 source on program-guide requests under
/// CI parallel load. Tests run against real migrated SQLite (the unique index comes from
/// <c>AddSetupsSingleRowIndex</c>, not from <c>EnsureCreated</c>).
/// </summary>
public sealed class ProgramSettingsServiceTests_SetupRace
{
    /// <summary>
    /// Deterministic interleaving: the loser is pinned inside its INSERT while the winner
    /// completes a full get-or-create — the loser's save then hits the single-row index and must
    /// adopt the winner's row instead of surfacing a second Setup row.
    /// </summary>
    [Fact]
    public async Task GetOrCreateSetupAsync_LosesCreateRace_ReturnsWinnersRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = $"file:setup-race-loser-{Guid.NewGuid():N}?mode=memory&cache=shared";
        // Keeps the shared-cache database alive between the contexts' connection opens.
        await using var anchor = new SqliteConnection($"Data Source={dbName}");
        await anchor.OpenAsync(ct);

        var holdInsert = new HoldSetupsInsertInterceptor();
        await using var dbLoser = new ApplicationDbContext(Options(dbName, holdInsert), new EventManager());
        await using var dbWinner = new ApplicationDbContext(Options(dbName, null), new EventManager());
        await dbWinner.Database.MigrateAsync(ct);

        // Task.Run detaches the loser from the test's single-threaded synchronization context —
        // its blocked INSERT may hold a thread-pool thread, but never the thread the test awaits on.
        var loserTask = Task.Run(() => new ProgramSettingsService(dbLoser, NullLogger<ProgramSettingsService>.Instance)
            .GetOrCreateSetupAsync(ct));
        // The loser has already read an empty table and now sits inside its blocked INSERT.
        await holdInsert.InsertAttempted.Task;

        var winnerRow = await new ProgramSettingsService(dbWinner, NullLogger<ProgramSettingsService>.Instance)
            .GetOrCreateSetupAsync(ct);
        holdInsert.Release();
        var loserRow = await loserTask;

        Assert.Equal(winnerRow.Id, loserRow.Id);
        Assert.Equal(1, await dbWinner.Setups.AsNoTracking().CountAsync(ct));
    }

    [Fact]
    public async Task GetOrCreateSetupAsync_ConcurrentCreators_ConvergeToSingleRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = $"file:setup-race-burst-{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var anchor = new SqliteConnection($"Data Source={dbName}");
        await anchor.OpenAsync(ct);

        await using (var db = new ApplicationDbContext(Options(dbName, null), new EventManager()))
            await db.Database.MigrateAsync(ct);

        var rows = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var db = new ApplicationDbContext(Options(dbName, null), new EventManager());
            return await new ProgramSettingsService(db, NullLogger<ProgramSettingsService>.Instance)
                .GetOrCreateSetupAsync(ct);
        })));

        await using var check = new ApplicationDbContext(Options(dbName, null), new EventManager());
        Assert.Equal(1, await check.Setups.AsNoTracking().CountAsync(ct));
        Assert.All(rows, row => Assert.Equal(rows[0].Id, row.Id));
    }

    private static DbContextOptions<ApplicationDbContext> Options(string dbName, DbCommandInterceptor? interceptor)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbName}");
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    /// <summary>
    /// Holds the first <c>INSERT INTO "Setups"</c> the intercepted context issues until released —
    /// pinning the caller in the exact window between "checked the table is empty" and
    /// "committed the new row" that produced the duplicate rows of Issue #279.
    /// </summary>
    private sealed class HoldSetupsInsertInterceptor : DbCommandInterceptor
    {
        private readonly ManualResetEventSlim _release = new();
        private int _held;

        public TaskCompletionSource InsertAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.Set();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Hold(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Hold(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Hold(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Hold(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO \"Setups\"")
                && Interlocked.CompareExchange(ref _held, 1, 0) == 0)
            {
                InsertAttempted.TrySetResult();
                _release.Wait();
            }
        }
    }
}
