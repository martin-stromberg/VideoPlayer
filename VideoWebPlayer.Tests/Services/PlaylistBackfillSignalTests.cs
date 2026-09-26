using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="PlaylistBackfillSignal"/>: the wake-up line that replaces the old polling loop.
/// </summary>
public class PlaylistBackfillSignalTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(150);

    [Fact]
    public async Task NotifyOutsideAScan_WakesTheWaiter()
    {
        var signal = new PlaylistBackfillSignal();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();

        signal.NotifyMarkersWritten();

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task NoSignal_NothingWakes()
    {
        var signal = new PlaylistBackfillSignal();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();

        Assert.NotSame(wait, await Task.WhenAny(wait, Task.Delay(Short, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task NotifyDuringAScan_WakesOnlyWhenTheScanEnds()
    {
        var signal = new PlaylistBackfillSignal();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();

        var scan = signal.BeginScan();
        for (var i = 0; i < 100; i++)
            signal.NotifyMarkersWritten(); // a scan saves hundreds of times
        Assert.NotSame(wait, await Task.WhenAny(wait, Task.Delay(Short, TestContext.Current.CancellationToken)));

        scan.Dispose();

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ScanEnd_WakesEvenWithoutAnyMarker()
    {
        var signal = new PlaylistBackfillSignal();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();

        signal.BeginScan().Dispose();

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task OverlappingScans_WakeOnlyWhenTheLastOneEnds()
    {
        var signal = new PlaylistBackfillSignal();
        var wait = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();

        var first = signal.BeginScan();
        var second = signal.BeginScan();
        first.Dispose();
        Assert.NotSame(wait, await Task.WhenAny(wait, Task.Delay(Short, TestContext.Current.CancellationToken)));

        second.Dispose();
        second.Dispose(); // disposing twice must not end another scan

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task SeveralSignals_AreCoalescedIntoOne()
    {
        var signal = new PlaylistBackfillSignal();
        signal.NotifyMarkersWritten();
        signal.NotifyMarkersWritten();
        signal.NotifyMarkersWritten();

        await signal.WaitAsync(TestContext.Current.CancellationToken);
        var second = signal.WaitAsync(TestContext.Current.CancellationToken).AsTask();

        Assert.NotSame(second, await Task.WhenAny(second, Task.Delay(Short, TestContext.Current.CancellationToken)));
    }
}
