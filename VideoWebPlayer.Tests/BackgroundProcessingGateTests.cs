using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class BackgroundProcessingGateTests
{
    [Fact]
    public async Task PauseForRestoreAsync_WaitsForActiveOperationAndBlocksNewOperations()
    {
        var gate = new BackgroundProcessingGate();
        await using var operation = await gate.EnterOperationAsync("scan", TestContext.Current.CancellationToken);

        var restoreTask = gate.PauseForRestoreAsync(TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(restoreTask.IsCompleted);
        Assert.True(gate.IsPausedForRestore);
        Assert.Equal(1, gate.ActiveOperationCount);

        var blockedOperationTask = gate.EnterOperationAsync("blocked", TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(blockedOperationTask.IsCompleted);

        await operation.DisposeAsync();
        var restoreLease = await restoreTask;
        Assert.Equal(0, gate.ActiveOperationCount);

        await restoreLease.DisposeAsync();
        await using var blockedOperation = await blockedOperationTask;

        Assert.False(gate.IsPausedForRestore);
        Assert.Equal(1, gate.ActiveOperationCount);
    }

    [Fact]
    public async Task PauseForRestoreAsync_RejectsParallelRestoreUntilLeaseIsDisposed()
    {
        var gate = new BackgroundProcessingGate();
        await using var restoreLease = await gate.PauseForRestoreAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.PauseForRestoreAsync(TestContext.Current.CancellationToken));

        Assert.True(gate.IsPausedForRestore);

        await restoreLease.DisposeAsync();
        await using var secondRestoreLease = await gate.PauseForRestoreAsync(TestContext.Current.CancellationToken);

        Assert.True(gate.IsPausedForRestore);
    }

    [Fact]
    public async Task PauseForRestoreAsync_CancellationWhileWaitingResumesOperations()
    {
        var gate = new BackgroundProcessingGate();
        await using var operation = await gate.EnterOperationAsync("scan", TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();

        var restoreTask = gate.PauseForRestoreAsync(cts.Token);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => restoreTask);

        Assert.False(gate.IsPausedForRestore);
        await using var newOperation = await gate.EnterOperationAsync("new", TestContext.Current.CancellationToken);
        Assert.Equal(2, gate.ActiveOperationCount);
    }
}
