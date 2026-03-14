using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Maui.Services;
using CommunityToolkit.Maui.Views;
using System.ComponentModel;

namespace VideoWebPlayer.Maui.Components;

public partial class MediaBannerPlayer : ContentView
{
    private VideoRequest? _currentVideoRequest;
    private System.Timers.Timer? _positionUpdateTimer;
    private readonly SemaphoreSlim _playbackProgressLock = new(1, 1);
    private double _lastReportedPositionSeconds = -1;
    private double _lastReportedDurationSeconds = -1;

    public MediaBannerPlayer()
    {
        // InitializeComponent() wird von ContentView nicht automatisch generiert
        // wenn die XAML nicht richtig verknüpft ist
        // Stattdessen können wir die Komponente manuell initialisieren
        try
        {
            InitializeComponent();
        }
        catch
        {
            // Falls InitializeComponent() nicht vorhanden ist, initialisiere XAML manuell
            this.Content = new Grid();
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] OnBindingContextChanged called, BindingContext type: {BindingContext?.GetType().Name ?? "null"}");

        // Unsubscribe von altem Context
        if (BindingContext is INotifyPropertyChanged oldContext)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Unsubscribing from old context");
            oldContext.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe auf neuem Context
        if (BindingContext is INotifyPropertyChanged newContext)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Subscribing to new context");
            newContext.PropertyChanged += OnViewModelPropertyChanged;
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Successfully subscribed to PropertyChanged");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] BindingContext does not implement INotifyPropertyChanged");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] PropertyChanged event received: {e.PropertyName} from {sender?.GetType().Name}");
        
        // Wenn sich SelectedMovie, SelectedMovieIndex oder SelectedEpisode ändert, stoppe die Wiedergabe
        if (e.PropertyName == "SelectedMovie" || 
            e.PropertyName == "SelectedMovieIndex" ||
            e.PropertyName == "SelectedEpisode" ||
            e.PropertyName == "SelectedEpisodeIndex")
        {
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Selection changed ({e.PropertyName}), stopping video playback");
            StopVideoPlayback();
        }
    }

    private void StopVideoPlayback()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            System.Diagnostics.Debug.WriteLine("[MediaBannerPlayer] Stopping video playback due to selection change");

            // Stoppe Timer
            _positionUpdateTimer?.Stop();

            // Speichere Position falls vorhanden
            if (VideoPlayer?.Source != null && _currentVideoRequest != null)
            {
                _ = Task.Run(async () => await SaveCurrentPositionAsync());
            }

            // Stoppe Video
            try
            {
                VideoPlayer.Pause();
                VideoPlayer.Source = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Error stopping video: {ex.Message}");
            }

            // Cleanup
            if (_currentVideoRequest != null)
            {
                _currentVideoRequest.SourceAvailable -= OnVideoSourceAvailable;
                _currentVideoRequest = null;
            }

            _lastReportedPositionSeconds = -1;
            _lastReportedDurationSeconds = -1;

            // Zeige Banner und Play-Button wieder an
            VideoPlayer.IsVisible = false;
            PlayButton.IsVisible = true;
            BannerImage.IsVisible = true;

            if (ErrorLabel != null)
            {
                ErrorLabel.IsVisible = false;
            }
        });
    }

    private async Task SaveCurrentPositionAsync()
    {
        try
        {
            if (VideoPlayer == null || _currentVideoRequest == null) return;

            var position = VideoPlayer.Position.TotalSeconds;
            var duration = VideoPlayer.Duration.TotalSeconds;

            if (position > 0 && duration > 0)
            {
                // beim Stop/Wechsel immer speichern, unabhängig vom Cache
                await DownloadManager.Instance.UpdatePlaybackPositionAsync(
                    _currentVideoRequest.VideoId,
                    _currentVideoRequest.VideoType,
                    position,
                    duration);

                System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Position saved: {position:F1}s / {duration:F1}s");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Error saving position: {ex.Message}");
        }
    }

    private async void OnPlayTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext == null) 
        {
            System.Diagnostics.Debug.WriteLine("[MediaBannerPlayer] BindingContext is null");
            return;
        }

        // Hole ViewModel vom Binding Context
        if (BindingContext is not IMediaBannerViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] BindingContext is not IMediaBannerViewModel, type: {BindingContext?.GetType().Name}");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[MediaBannerPlayer] Calling GetVideoInfoForPlaybackAsync...");
        
        // Hole Video-Info vom ViewModel
        var videoInfo = await viewModel.GetVideoInfoForPlaybackAsync();
        
        System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] GetVideoInfoForPlaybackAsync returned: {videoInfo?.ToString() ?? "null"}");
        
        if (videoInfo == null)
        {
            System.Diagnostics.Debug.WriteLine("[MediaBannerPlayer] videoInfo is null!");
            await Application.Current!.MainPage!.DisplayAlert(
                "Keine Auswahl", 
                "Bitte wählen Sie ein Medium aus.", 
                "OK");
            return;
        }

        await PlayVideoAsync(videoInfo.Value.VideoId, videoInfo.Value.VideoType, videoInfo.Value.Title);
    }

    private async Task PlayVideoAsync(long videoId, string videoType, string title)
    {
        try
        {
            // Cleanup vorheriges Request
            if (_currentVideoRequest != null)
            {
                _currentVideoRequest.SourceAvailable -= OnVideoSourceAvailable;
            }

            _currentVideoRequest = await DownloadManager.Instance.RequestVideoAsync(
                videoId,
                videoType,
                title);

            _currentVideoRequest.SourceAvailable += OnVideoSourceAvailable;

            System.Diagnostics.Debug.WriteLine("[MediaBannerPlayer] Video request created, waiting for source...");
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Fehler", $"Video konnte nicht geladen werden: {ex.Message}", "OK");
        }
    }

    private void OnVideoSourceAvailable(object? sender, VideoSourceInfo source)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (source == null) return;

            System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Video source available");

            if (source.SourcePath != null)
            {
                if (source.SourceType == VideoSourceType.LocalFile)
                {
                    VideoPlayer.Source = MediaSource.FromFile(source.SourcePath);
                }
                else
                {
                    VideoPlayer.Source = MediaSource.FromUri(source.SourcePath);
                }
            }
        });
    }

    private void OnMediaStateChanged(object? sender, CommunityToolkit.Maui.Core.MediaStateChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Media state changed");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Wenn Video abgespielt wird
            if (VideoPlayer?.Source != null)
            {
                PlayButton.IsVisible = false;
                BannerImage.IsVisible = false;
                VideoPlayer.IsVisible = true;

                if (ErrorLabel != null)
                {
                    ErrorLabel.IsVisible = false;
                }

                StartPositionUpdateTimer();
            }
        });
    }

    private void OnMediaFailed(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MediaBannerPlayer] Media failed");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Verstecke Video Player und zeige Banner + Play-Button
            VideoPlayer.IsVisible = false;
            VideoPlayer.Source = null;

            PlayButton.IsVisible = true;
            BannerImage.IsVisible = true;

            if (ErrorLabel != null)
            {
                ErrorLabel.Text = $"⚠ Wiedergabe fehlgeschlagen";
                ErrorLabel.IsVisible = true;
            }

            _positionUpdateTimer?.Stop();
        });
    }

    private void StartPositionUpdateTimer()
    {
        _positionUpdateTimer?.Stop();
        _positionUpdateTimer = new System.Timers.Timer(5000);
        _positionUpdateTimer.Elapsed += async (s, e) => await UpdatePlaybackPositionAsync();
        _positionUpdateTimer.Start();

        System.Diagnostics.Debug.WriteLine("[MediaBannerPlayer] Position update timer started");
    }

    private async Task UpdatePlaybackPositionAsync()
    {
        try
        {
            if (VideoPlayer == null || _currentVideoRequest == null) return;

            // Timer kann re-entrant sein
            if (!await _playbackProgressLock.WaitAsync(0))
                return;

            try
            {
                var position = VideoPlayer.Position.TotalSeconds;
                var duration = VideoPlayer.Duration.TotalSeconds;

                if (position <= 0 || duration <= 0)
                    return;

                // Wenn die Position sich nicht geändert hat (z.B. Pause), kein erneutes Senden.
                // Kleine Toleranz, damit Rundungen/Timer-Jitter nicht triggern.
                const double epsilonSeconds = 0.5;
                if (Math.Abs(position - _lastReportedPositionSeconds) < epsilonSeconds &&
                    Math.Abs(duration - _lastReportedDurationSeconds) < epsilonSeconds)
                {
                    return;
                }

                await DownloadManager.Instance.UpdatePlaybackPositionAsync(
                    _currentVideoRequest.VideoId,
                    _currentVideoRequest.VideoType,
                    position,
                    duration);

                _lastReportedPositionSeconds = position;
                _lastReportedDurationSeconds = duration;
            }
            finally
            {
                _playbackProgressLock.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating position: {ex.Message}");
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler == null)
        {
            _positionUpdateTimer?.Stop();
            _positionUpdateTimer?.Dispose();
            _positionUpdateTimer = null;

            // Unsubscribe von PropertyChanged
            if (BindingContext is INotifyPropertyChanged context)
            {
                context.PropertyChanged -= OnViewModelPropertyChanged;
            }
        }
    }
}

/// <summary>
/// Interface für ViewModels die mit MediaBannerPlayer kompatibel sind
/// </summary>
public interface IMediaBannerViewModel
{
    /// <summary>
    /// Gibt die Video-Informationen für die Wiedergabe zurück
    /// </summary>
    Task<(long VideoId, string VideoType, string Title)?> GetVideoInfoForPlaybackAsync();

    /// <summary>
    /// Wird aufgerufen wenn der Play-Button sichtbar sein soll
    /// </summary>
    bool ShouldShowPlayButton { get; }
}
