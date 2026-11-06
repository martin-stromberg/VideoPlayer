using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="ActorBackfillWorker"/>: a shutdown cancellation must not be logged
/// as an error and must stop the second backfill, while regular failures of the first
/// backfill are logged and do not prevent the second backfill.
/// </summary>
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class ActorBackfillWorkerTests
{
    [Fact]
    public async Task CancelledToken_PropagatesWithoutErrorLogsAndSkipsAgeRatingBackfill()
    {
        await using var testDb = await CreateDbAsync("actor-backfill-cancel");
        var workerLogger = new ListLogger<ActorBackfillWorker>(new ConcurrentQueue<string>());
        var classifierLogger = new ListLogger<MediaSourceClassifier>(new ConcurrentQueue<string>());
        using var provider = BuildProvider(testDb.Context, classifierLogger);
        var worker = new TestableActorBackfillWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), workerLogger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.RunAsync(new CancellationToken(canceled: true)));

        Assert.DoesNotContain(workerLogger.Entries, e => e.Level == LogLevel.Error);
        Assert.DoesNotContain(
            classifierLogger.Entries,
            e => e.Message.Contains("Altersfreigaben", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BackfillFailure_IsLoggedAndAgeRatingBackfillStillRuns()
    {
        await using var testDb = await CreateDbAsync("actor-backfill-error");
        await testDb.Context.DisposeAsync(); // both backfills fail with a non-cancellation error
        var workerLogger = new ListLogger<ActorBackfillWorker>(new ConcurrentQueue<string>());
        var classifierLogger = new ListLogger<MediaSourceClassifier>(new ConcurrentQueue<string>());
        using var provider = BuildProvider(testDb.Context, classifierLogger);
        var worker = new TestableActorBackfillWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), workerLogger);
        using var cts = new CancellationTokenSource();

        var run = worker.RunAsync(cts.Token);
        Assert.True(
            await WaitUntilAsync(() => workerLogger.Entries.Count(e => e.Level == LogLevel.Error) >= 2),
            "worker did not log both backfill failures");
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Contains(
            workerLogger.Entries,
            e => e.Level == LogLevel.Error && e.Message.Contains("Schauspieler", StringComparison.Ordinal));
        Assert.Contains(
            workerLogger.Entries,
            e => e.Level == LogLevel.Error && e.Message.Contains("Altersfreigaben", StringComparison.Ordinal));
        Assert.Contains(
            classifierLogger.Entries,
            e => e.Message.Contains("Altersfreigaben", StringComparison.Ordinal));
    }

    private static async Task<TestDb> CreateDbAsync(string name)
    {
        var connectionString = $"Data Source=file:{name}?mode=memory&cache=shared";
        var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync();
        return new TestDb(db, keeper);
    }

    private static ServiceProvider BuildProvider(
        ApplicationDbContext db, ListLogger<MediaSourceClassifier> classifierLogger)
    {
        var classifier = new MediaSourceClassifier(
            db,
            new BackfillSftpMediaSourceReader(new()),
            null!,
            new EventManager(),
            null!,
            classifierLogger,
            new HttpClient());

        return new ServiceCollection()
            .AddSingleton(classifier)
            .BuildServiceProvider();
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(25);
        }

        return condition();
    }

    private sealed class TestableActorBackfillWorker : ActorBackfillWorker
    {
        public TestableActorBackfillWorker(IServiceScopeFactory scopeFactory, ILogger<ActorBackfillWorker> logger)
            : base(scopeFactory, logger)
        {
        }

        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }

    private sealed class TestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _keeper;

        public TestDb(ApplicationDbContext context, SqliteConnection keeper)
        {
            Context = context;
            _keeper = keeper;
        }

        public ApplicationDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _keeper.DisposeAsync();
        }
    }
}
