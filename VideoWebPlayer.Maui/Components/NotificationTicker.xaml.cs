using Microsoft.Maui.Controls;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Services.Events;
using Microsoft.Maui.Layouts;
using System.Threading;

namespace VideoWebPlayer.Maui.Components;

public partial class NotificationTicker : ContentView
{
    private readonly ISubscribeNotificationEvent? _eventSubscriber;

    private const double ScrollSpeedPxPerSec = 200.0;
    private const double StartGapPx = 20.0;

    private readonly object _startChainLock = new();
    private Task _startChain = Task.CompletedTask;

    private int _pendingMessages;
    private int _activeMessages;

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(NotificationTicker), string.Empty);

    public static readonly BindableProperty HasMessageProperty =
        BindableProperty.Create(nameof(HasMessage), typeof(bool), typeof(NotificationTicker), false);

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool HasMessage
    {
        get => (bool)GetValue(HasMessageProperty);
        set => SetValue(HasMessageProperty, value);
    }

    public NotificationTicker()
    {
        InitializeComponent();

        // Subscribe to download events
        _eventSubscriber = App.ServiceProvider?.GetService<ISubscribeNotificationEvent>();
        if (_eventSubscriber != null)
        {
            _eventSubscriber.Subscribe<DownloadCompletedEvent>(OnDownloadCompleted);
            _eventSubscriber.Subscribe<DownloadDeletedEvent>(OnDownloadDeleted);
            _eventSubscriber.Subscribe<ContinueWatchingUpdatedEvent>(OnContinueWatchingUpdated);
            _eventSubscriber.Subscribe<NewVideosScannedEvent>(OnNewVideosScanned);

            System.Diagnostics.Debug.WriteLine("[NotificationTicker] Subscribed to notification events");
        }

        // Subscribe to download progress (nur einmal!)
        DownloadQueue.Instance.DownloadProgress -= OnDownloadProgress; // Entferne zuerst existierende Handler
        DownloadQueue.Instance.DownloadProgress += OnDownloadProgress;
        System.Diagnostics.Debug.WriteLine("[NotificationTicker] Subscribed to download progress events");
    }

    private void OnDownloadCompleted(DownloadCompletedEvent e)
    {
        var message = $"✅ Download abgeschlossen: {e.Download.Title}";
        QueueMessage(message);
    }

    private void OnDownloadDeleted(DownloadDeletedEvent e)
    {
        var message = $"🗑️ Download gelöscht: {e.Title}";
        QueueMessage(message);
    }

    private void OnContinueWatchingUpdated(ContinueWatchingUpdatedEvent e)
    {
        var message = "📺 Weiterschauen aktualisiert";
        QueueMessage(message);
    }

    private void OnNewVideosScanned(NewVideosScannedEvent e)
    {
        if (e.Count > 0)
        {
            var message = $"🎬 {e.Count} neue Video(s) gefunden";
            QueueMessage(message);
        }
    }

    private int _lastReportedPercent = -1;
    private DateTime _lastProgressTime = DateTime.MinValue;
    private const int ProgressThrottleMs = 5000; // Nur alle 5000ms updaten

    private void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        // Throttle: Nur alle 500ms eine Nachricht
        var now = DateTime.UtcNow;
        if ((now - _lastProgressTime).TotalMilliseconds < ProgressThrottleMs)
            return;

        // Nur bei Ganzzahl-Änderung
        var wholePercent = (int)Math.Floor(e.ProgressPercent);
        
        if (wholePercent != _lastReportedPercent)
        {
            _lastReportedPercent = wholePercent;
            _lastProgressTime = now;
            var message = $"⬇️ {wholePercent}% - {GetFileSizeString(e.DownloadedBytes)} / {GetFileSizeString(e.TotalBytes)}";
            QueueMessage(message);
        }
    }

    private static string GetFileSizeString(long bytes)
    {
        const long MB = 1024 * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB)
            return $"{(double)bytes / GB:F1} GB";
        if (bytes >= MB)
            return $"{(double)bytes / MB:F1} MB";
        
        return $"{bytes / 1024} KB";
    }

    private void QueueMessage(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationTicker] Queuing message: {message}");

        Interlocked.Increment(ref _pendingMessages);
        MainThread.BeginInvokeOnMainThread(() => HasMessage = true);

        // Start-Reihenfolge: nächste Meldung darf starten, wenn das Ende der vorherigen
        // Meldung nicht mehr über den rechten Rand hinaus ragt.
        lock (_startChainLock)
        {
            _startChain = _startChain.ContinueWith(_ => StartMessageAsync(message), TaskScheduler.Default).Unwrap();
        }
    }

    private async Task StartMessageAsync(string message)
    {
        try
        {
            double labelWidth = 0;
            double containerWidth = 0;
            double labelHeight = 0;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var label = new Label
                {
                    Text = message,
                    FontSize = 16,
                    TextColor = Color.FromArgb("#FFDDAA"),
                    FontFamily = "OpenSans",
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Start,
                };

                // Schatten
                label.Shadow = new Shadow
                {
                    Brush = Colors.Black,
                    Offset = new Point(2, 2),
                    Radius = 3,
                    Opacity = 0.5f
                };

                TickerContainer.Children.Add(label);

                containerWidth = ClipContainer.Width;
                if (containerWidth <= 0)
                    containerWidth = Width;
                if (containerWidth <= 0)
                    containerWidth = Application.Current?.MainPage?.Width ?? 1000;

                // Measure
                var measure = label.Measure(double.PositiveInfinity, double.PositiveInfinity);
                labelWidth = measure.Width;
                labelHeight = measure.Height;

                if (labelWidth <= 0)
                    labelWidth = message.Length * 10;
                if (labelHeight <= 0)
                    labelHeight = 20;

                var containerHeight = ClipContainer.Height;
                if (containerHeight <= 0)
                    containerHeight = Height;
                if (containerHeight <= 0)
                    containerHeight = 47;

                var y = Math.Max(0, (containerHeight - labelHeight) / 2);

                // Start: rechts außerhalb (linke Kante am rechten Rand)
                AbsoluteLayout.SetLayoutBounds(label, new Rect(containerWidth, y, labelWidth, labelHeight));
                AbsoluteLayout.SetLayoutFlags(label, AbsoluteLayoutFlags.None);

                Interlocked.Decrement(ref _pendingMessages);
                Interlocked.Increment(ref _activeMessages);
                HasMessage = true;

                // TranslateTo arbeitet auf TranslationX relativ zur Layout-Position.
                // Da das Label initial bei X = containerWidth liegt, muss die Translation so groß sein,
                // dass die linke Kante bis komplett links raus wandert.
                var endX = -(containerWidth + labelWidth + StartGapPx);
                var distance = containerWidth + labelWidth + StartGapPx;
                var duration = (uint)(distance / ScrollSpeedPxPerSec * 1000);
                duration = Math.Max(3000, Math.Min(duration, 15000));

                _ = label.TranslateTo(endX, 0, duration, Easing.Linear)
                    .ContinueWith(async _ =>
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            TickerContainer.Children.Remove(label);
                            var remaining = Interlocked.Decrement(ref _activeMessages);
                            if (remaining <= 0 && Volatile.Read(ref _pendingMessages) <= 0)
                                HasMessage = false;
                        });
                    });
            });

            // Startregel: nächste Meldung darf erst starten, wenn das Ende dieser Meldung
            // nicht mehr rechts übersteht => nachdem sie um ihre Breite nach links gewandert ist.
            var startDelayMs = (int)Math.Ceiling(((labelWidth + StartGapPx) / ScrollSpeedPxPerSec) * 1000);
            if (startDelayMs < 0)
                startDelayMs = 0;

            await Task.Delay(startDelayMs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationTicker] Error starting message: {ex.Message}");
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        System.Diagnostics.Debug.WriteLine($"[NotificationTicker] Size allocated: {width}x{height}");
    }
}
