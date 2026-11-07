using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Test fake of <see cref="IProgramScheduleService"/>: <see cref="ExecutePassAsync"/> returns
/// queued <see cref="ProgramSchedulePassSummary"/> results (default when the queue runs dry),
/// counts its calls and appends a marker to a shared order log so tests can prove the call
/// happens inside the background-processing-gate lease.
/// </summary>
internal sealed class FakeProgramScheduleService : IProgramScheduleService
{
    private readonly IList<string> _orderLog;
    private readonly Queue<ProgramSchedulePassSummary> _passes = new();
    private int _passCalls;

    public FakeProgramScheduleService(IList<string> orderLog) => _orderLog = orderLog;

    /// <summary>How many passes the worker ran.</summary>
    public int PassCalls => _passCalls;

    /// <summary>Queues the result the next pass returns.</summary>
    public void EnqueuePass(ProgramSchedulePassSummary pass) => _passes.Enqueue(pass);

    /// <inheritdoc />
    public Task<ProgramSchedulePassSummary> ExecutePassAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        lock (_orderLog)
            _orderLog.Add("pass");
        Interlocked.Increment(ref _passCalls);
        return Task.FromResult(_passes.Count > 0 ? _passes.Dequeue() : default);
    }

    /// <inheritdoc />
    public Task<ProgramScheduleRunSummary> EnsureCoverageAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(default(ProgramScheduleRunSummary));

    /// <inheritdoc />
    public Task<ProgramScheduleReconcileSummary> ReconcileAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(default(ProgramScheduleReconcileSummary));

    /// <inheritdoc />
    public Task<List<ProgramSlot>> GetScheduleAsync(long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ProgramSlot>());

    /// <inheritdoc />
    public Task<int> DeleteDisplacedSlotsAsync(string userId, long channelId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    /// <inheritdoc />
    public Task<int> PurgeExpiredRulesAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    /// <inheritdoc />
    public Task<int> CleanupSelfDisplacedSlotsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    /// <inheritdoc />
    public Task<ProgramScheduleResetResult> ResetScheduleAsync(string userId, long channelId, CancellationToken cancellationToken = default)
        => Task.FromResult(default(ProgramScheduleResetResult));

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>> GetRuleDiagnosesAsync(string userId, long channelId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>(new Dictionary<long, ProgramRuleDiagnosis>());
}
