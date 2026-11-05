using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="ProgramScheduleSignal"/>: coalescing wake-up for the schedule worker,
/// including scan suppression via <see cref="IProgramScheduleSignal.BeginScan"/> and the batching
/// window for scope-free <see cref="IProgramScheduleSignal.NotifyScheduleChanged"/> reports.
/// </summary>
public sealed class ProgramScheduleSignalTests
{
    private static ProgramScheduleSignal CreateImmediateSignal()
        => new(Options.Create(new ProgramScheduleOptions { ScheduleChangedDebounceSeconds = 0 }));

    private static ProgramScheduleSignal CreateBatchedSignal(ControllableTimeProvider timeProvider, int debounceSeconds = 5)
        => new(Options.Create(new ProgramScheduleOptions { ScheduleChangedDebounceSeconds = debounceSeconds }), timeProvider);

    [Fact]
    public async Task Notify_WakesWaiter()
    {
        var signal = CreateImmediateSignal();
        signal.NotifyScheduleChanged();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, TestContext.Current.CancellationToken)) == wait);
    }

    [Fact]
    public async Task Notify_IsCoalesced()
    {
        var signal = CreateImmediateSignal();
        signal.NotifyScheduleChanged();
        signal.NotifyScheduleChanged();
        await signal.WaitAsync(TestContext.Current.CancellationToken);
        var second = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(200, TestContext.Current.CancellationToken)) == second, "Coalesced signals must not wake twice.");
    }

    [Fact]
    public async Task Notify_DuringScan_IsSuppressed_UntilScanEnds()
    {
        var signal = new ProgramScheduleSignal();
        Task wait;
        using (var scope = signal.BeginScan())
        {
            signal.NotifyScheduleChanged();
            wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();
            Assert.False(await Task.WhenAny(wait, Task.Delay(200, TestContext.Current.CancellationToken)) == wait, "Notification during a scan must be suppressed.");
        }
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, TestContext.Current.CancellationToken)) == wait, "End of scan must wake the pending waiter.");
        var second = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(200, TestContext.Current.CancellationToken)) == second, "Suppressed notifications of one scan must coalesce into a single wake.");
    }

    [Fact]
    public async Task ScanEnd_WithoutSuppressedNotification_DoesNotWake()
    {
        var signal = new ProgramScheduleSignal();
        using (signal.BeginScan())
        {
        }

        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.False(await Task.WhenAny(wait, Task.Delay(300, TestContext.Current.CancellationToken)) == wait,
            "A scan that suppressed no notification must not wake the worker on dispose.");
    }

    [Fact]
    public async Task Notify_OutsideScan_WakesAfterDebounceWindow()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        var wait = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(wait, Task.Delay(200, ct)) == wait,
            "A scope-free notification must not wake the worker before the batching window ends.");

        timeProvider.TickTimers();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, ct)) == wait,
            "The batched flush must wake the worker once the window elapsed.");
    }

    [Fact]
    public async Task Notify_BurstWithinWindow_WakesOnce()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        signal.NotifyScheduleChanged();
        signal.NotifyScheduleChanged();
        timeProvider.TickTimers();

        await signal.WaitAsync(ct);
        var second = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(200, ct)) == second,
            "A burst inside one window must coalesce into a single wake.");
    }

    [Fact]
    public async Task Notify_AfterWindowDelivery_StartsNewWindow()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        timeProvider.TickTimers();
        await signal.WaitAsync(ct);

        signal.NotifyScheduleChanged();
        var second = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(200, ct)) == second,
            "A notification after a delivered batch opens a new window instead of waking at once.");

        timeProvider.TickTimers();
        Assert.True(await Task.WhenAny(second, Task.Delay(2000, ct)) == second,
            "The new window must deliver the second notification at its end.");
    }

    [Fact]
    public async Task Notify_BatchedWake_AlsoFeedsLiveChannel()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        timeProvider.TickTimers();

        var liveWait = signal.WaitLiveAsync(ct).AsTask();
        Assert.True(await Task.WhenAny(liveWait, Task.Delay(2000, ct)) == liveWait,
            "The batched delivery must feed the live channel as well.");
        var second = signal.WaitLiveAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(200, ct)) == second,
            "The batched delivery must wake the live channel exactly once.");
    }

    [Fact]
    public async Task NotifyScheduleChangedNow_WakesImmediately()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChangedNow();
        var wait = signal.WaitAsync(ct).AsTask();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, ct)) == wait,
            "The immediate path must wake the worker without a timer tick.");
    }

    [Fact]
    public async Task NotifyScheduleChangedNow_DuringScan_IsSuppressed()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        Task wait;
        using (signal.BeginScan())
        {
            signal.NotifyScheduleChangedNow();
            wait = signal.WaitAsync(ct).AsTask();
            Assert.False(await Task.WhenAny(wait, Task.Delay(200, ct)) == wait,
                "The immediate path must stay suppressed while a scan is open.");
        }

        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, ct)) == wait,
            "The scan's end must wake the waiter once.");
        var second = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(200, ct)) == second,
            "The suppressed immediate report must coalesce into a single wake.");
    }

    [Fact]
    public async Task NotifyScheduleChangedNow_ConsumesPendingBatch()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        signal.NotifyScheduleChangedNow();
        await signal.WaitAsync(ct);

        timeProvider.TickTimers();
        var second = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(300, ct)) == second,
            "A batch consumed by the immediate path must not wake again when its window ends.");
    }

    [Fact]
    public async Task PendingBatch_FlushedDuringScan_IsSuppressedUntilScanEnds()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        var scope = signal.BeginScan();
        timeProvider.TickTimers();
        await Task.Delay(200, ct);

        var wait = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(wait, Task.Delay(200, ct)) == wait,
            "A batch flushing into an open scan must not wake while the scan runs.");

        scope.Dispose();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, ct)) == wait,
            "The scan's end must wake the waiter once.");
    }

    [Fact]
    public async Task EndScan_Wake_ConsumesPendingBatch_LateFlushDoesNotWake()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        using (signal.BeginScan())
        {
            signal.NotifyScheduleChanged();
        }

        var wait = signal.WaitAsync(ct).AsTask();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, ct)) == wait,
            "The scan's end must wake for the suppressed notification.");

        timeProvider.TickTimers();
        var second = signal.WaitAsync(ct).AsTask();
        Assert.False(await Task.WhenAny(second, Task.Delay(300, ct)) == second,
            "The late-running flush must not write a second wake after the batch was consumed.");
    }

    [Fact]
    public async Task DebounceDisabled_DeliversImmediately()
    {
        var timeProvider = new ControllableTimeProvider();
        var signal = CreateBatchedSignal(timeProvider, debounceSeconds: 0);
        var ct = TestContext.Current.CancellationToken;

        signal.NotifyScheduleChanged();
        var wait = signal.WaitAsync(ct).AsTask();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, ct)) == wait,
            "ScheduleChangedDebounceSeconds = 0 must restore immediate delivery without a timer tick.");
    }
}
