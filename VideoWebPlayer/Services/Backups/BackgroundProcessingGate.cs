namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// In-process implementation of the background processing gate.
/// </summary>
public sealed class BackgroundProcessingGate : IBackgroundProcessingGate
{
    private readonly object _sync = new();
    private TaskCompletionSource _resumeSignal = NewSignal(completed: true);
    private TaskCompletionSource _idleSignal = NewSignal(completed: true);
    private bool _paused;
    private bool _restoreInProgress;
    private int _activeOperations;

    /// <inheritdoc />
    public bool IsPausedForRestore
    {
        get
        {
            lock (_sync)
                return _paused;
        }
    }

    /// <inheritdoc />
    public int ActiveOperationCount
    {
        get
        {
            lock (_sync)
                return _activeOperations;
        }
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> EnterOperationAsync(string name, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                if (!_paused)
                {
                    _activeOperations++;
                    if (_activeOperations == 1)
                        _idleSignal = NewSignal(completed: false);
                    return new OperationLease(this);
                }

                waitTask = _resumeSignal.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> PauseForRestoreAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (_sync)
        {
            if (_restoreInProgress)
                throw new InvalidOperationException("Es läuft bereits ein Restore.");

            _restoreInProgress = true;
            _paused = true;
            _resumeSignal = NewSignal(completed: false);

            waitTask = _idleSignal.Task;
        }

        try
        {
            await waitTask.WaitAsync(cancellationToken);
            return new RestoreLease(this);
        }
        catch
        {
            CancelRestorePause();
            throw;
        }
    }

    private void LeaveOperation()
    {
        lock (_sync)
        {
            _activeOperations--;
            if (_activeOperations <= 0)
            {
                _activeOperations = 0;
                _idleSignal.TrySetResult();
            }
        }
    }

    private void ResumeOperations()
    {
        lock (_sync)
        {
            _restoreInProgress = false;
            _paused = false;
            _resumeSignal.TrySetResult();
        }
    }

    private void CancelRestorePause()
    {
        lock (_sync)
        {
            _restoreInProgress = false;
            _paused = false;
            _resumeSignal.TrySetResult();
        }
    }

    private static TaskCompletionSource NewSignal(bool completed)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (completed)
            source.SetResult();
        return source;
    }

    private sealed class OperationLease : IAsyncDisposable
    {
        private readonly BackgroundProcessingGate _gate;
        private int _disposed;

        public OperationLease(BackgroundProcessingGate gate)
        {
            _gate = gate;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _gate.LeaveOperation();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RestoreLease : IAsyncDisposable
    {
        private readonly BackgroundProcessingGate _gate;
        private int _disposed;

        public RestoreLease(BackgroundProcessingGate gate)
        {
            _gate = gate;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _gate.ResumeOperations();
            return ValueTask.CompletedTask;
        }
    }
}
