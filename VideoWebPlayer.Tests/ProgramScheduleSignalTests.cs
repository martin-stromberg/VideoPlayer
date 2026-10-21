using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="ProgramScheduleSignal"/>: coalescing wake-up for the schedule worker,
/// including scan suppression via <see cref="IProgramScheduleSignal.BeginScan"/>.
/// </summary>
public sealed class ProgramScheduleSignalTests
{
    [Fact]
    public async Task Notify_WakesWaiter()
    {
        var signal = new ProgramScheduleSignal();
        signal.NotifyScheduleChanged();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.True(await Task.WhenAny(wait, Task.Delay(2000, TestContext.Current.CancellationToken)) == wait);
    }

    [Fact]
    public async Task Notify_IsCoalesced()
    {
        var signal = new ProgramScheduleSignal();
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
    }
}
