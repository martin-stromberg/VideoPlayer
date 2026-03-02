using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Client;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;

namespace VideoWebPlayer.Maui;

public partial class TVShowDetailsPage : ContentPage
{
    private readonly TVShowDetailsViewModel _viewModel;
    private readonly long? _initialSeasonId;
    private VideoRequest? _currentVideoRequest;
    private TimeSpan _lastPosition;
    private System.Timers.Timer? _positionUpdateTimer;
    private bool _isLocalFile;
    private bool _isDisposed = false;

    public TVShowDetailsPage(long tvShowId, long? seasonId = null)
    {
        InitializeComponent();
        _initialSeasonId = seasonId;
        _viewModel = new TVShowDetailsViewModel(tvShowId);
        BindingContext = _viewModel;
        
        // Registriere Download-Completed Event
        DownloadQueue.Instance.DownloadCompleted += OnDownloadCompleted;
        
        // Position-Update Timer (alle 5 Sekunden)
        _positionUpdateTimer = new System.Timers.Timer(5000);
        _positionUpdateTimer.Elapsed += OnPositionUpdateTimerElapsed;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.LoadDataAsync(_initialSeasonId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Daten konnten nicht geladen werden: {ex.Message}", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        if (_isDisposed) return;
        _isDisposed = true;
        
        // Stoppe Timer sofort
        _positionUpdateTimer?.Stop();
        
        // Stoppe Video Player SOFORT (synchron)
        if (VideoPlayer != null && VideoPlayer.CurrentState != MediaElementState.None)
        {
            VideoPlayer.Pause(); // Pause statt Stop, damit Position erhalten bleibt
            VideoPlayer.Source = null; // Gebe Ressource frei
        }
        
        // Speichere Position asynchron (nicht blockierend)
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveCurrentPosition();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving position on disappear: {ex.Message}");
            }
        });
        
        // Cleanup
        if (_currentVideoRequest != null)
        {
            _currentVideoRequest.SourceAvailable -= OnVideoSourceAvailable;
        }
        
        DownloadQueue.Instance.DownloadCompleted -= OnDownloadCompleted;
        
        // Dispose Timer
        _positionUpdateTimer?.Dispose();
        _positionUpdateTimer = null;
    }

    private async void OnPlayTapped(object? sender, EventArgs e)
    {
        // Spiele ausgewählte Episode oder erste Episode der Staffel
        var episodeToPlay = _viewModel.SelectedEpisode ?? _viewModel.Episodes.FirstOrDefault();
        
        if (episodeToPlay == null)
        {
            await DisplayAlert("Keine Episode", "Keine Episode zum Abspielen verfügbar.", "OK");
            return;
        }

        await PlayEpisodeAsync(episodeToPlay);
    }
    
    private void OnEpisodeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Wenn eine neue Episode ausgewählt wird während Video läuft, stoppe Wiedergabe
        if (VideoPlayer != null && VideoPlayer.CurrentState != MediaElementState.None && VideoPlayer.CurrentState != MediaElementState.Stopped)
        {
            try
            {
                // Speichere Position des aktuellen Videos
                _ = SaveCurrentPosition();
                
                // Stoppe Video Player
                VideoPlayer.Pause();
                VideoPlayer.Source = null;
                
                // Zeige Play Button und Banner wieder
                _ = MainThread.InvokeOnMainThreadAsync(() =>
                {
                    VideoPlayer.IsVisible = false;
                    PlayButton.IsVisible = true;
                    BannerImage.IsVisible = true;
                });
                
                // Stoppe Timer
                _positionUpdateTimer?.Stop();
                
                // Reset Position
                _lastPosition = TimeSpan.Zero;
                
                System.Diagnostics.Debug.WriteLine("Video stopped due to episode selection change");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping video on episode selection: {ex.Message}");
            }
        }
        
        // ViewModel kümmert sich um das Laden der Episode-Infos (Banner, Plot)
    }

    private async Task PlayEpisodeAsync(TVShowEpisodeViewModel episode)
    {
        try
        {
            // Cleanup vorheriges Request
            if (_currentVideoRequest != null)
            {
                _currentVideoRequest.SourceAvailable -= OnVideoSourceAvailable;
            }

            // Neues Request erstellen
            _currentVideoRequest = await DownloadManager.Instance.RequestVideoAsync(
                episode.EpisodeId,
                MediaTypes.Episode,
                episode.Title ?? "Episode"
            );

            // Event registrieren
            _currentVideoRequest.SourceAvailable += OnVideoSourceAvailable;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Video konnte nicht geladen werden: {ex.Message}", "OK");
        }
    }

    private void OnVideoSourceAvailable(object? sender, VideoSourceInfo sourceInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Speichere aktuelle Position wenn Video läuft
                if (VideoPlayer.CurrentState == MediaElementState.Playing)
                {
                    _lastPosition = VideoPlayer.Position;
                }

                // Merke ob lokale Datei
                _isLocalFile = sourceInfo.SourceType == VideoSourceType.LocalFile;

                // Setze neue Quelle
                if (sourceInfo.SourceType == VideoSourceType.LocalFile)
                {
                    VideoPlayer.Source = MediaSource.FromFile(sourceInfo.SourcePath);
                    System.Diagnostics.Debug.WriteLine($"Switched to local file: {sourceInfo.SourcePath}");
                }
                else
                {
                    VideoPlayer.Source = MediaSource.FromUri(sourceInfo.SourcePath);
                    System.Diagnostics.Debug.WriteLine($"Playing stream: {sourceInfo.SourcePath}");
                }

                // Zeige Video Player, verstecke Play Button und Banner
                VideoPlayer.IsVisible = true;
                PlayButton.IsVisible = false;
                BannerImage.IsVisible = false;

                // Setze Resume-Position wenn vorhanden
                if (sourceInfo.ResumePosition.HasValue && sourceInfo.ResumePosition.Value > TimeSpan.Zero)
                {
                    _lastPosition = sourceInfo.ResumePosition.Value;
                    VideoPlayer.SeekTo(sourceInfo.ResumePosition.Value);
                    System.Diagnostics.Debug.WriteLine($"Resuming from position: {sourceInfo.ResumePosition.Value}");
                }
                else if (_lastPosition > TimeSpan.Zero)
                {
                    VideoPlayer.SeekTo(_lastPosition);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting video source: {ex.Message}");
            }
        });
    }

    private void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        // Prüfe ob Download für aktuelles Video
        if (_currentVideoRequest != null &&
            e.VideoId == _currentVideoRequest.VideoId &&
            e.VideoType == _currentVideoRequest.VideoType &&
            e.Success)
        {
            // Triggere SourceAvailable Event erneut für nahtlosen Wechsel
            var sourceInfo = new VideoSourceInfo
            {
                SourcePath = e.LocalFilePath,
                SourceType = VideoSourceType.LocalFile
            };
            
            OnVideoSourceAvailable(this, sourceInfo);
        }
    }

    private void OnMediaStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Media state changed: {e.NewState}");
        
        // Starte/Stoppe Timer basierend auf State
        if (e.NewState == MediaElementState.Playing)
        {
            _positionUpdateTimer?.Start();
            
            // Verstecke Fehlermeldung wenn Video erfolgreich spielt
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ErrorLabel != null)
                {
                    ErrorLabel.IsVisible = false;
                }
            });
        }
        else
        {
            _positionUpdateTimer?.Stop();
            
            // Speichere Position bei Pause oder Stop
            if (e.NewState == MediaElementState.Paused || e.NewState == MediaElementState.Stopped)
            {
                _ = SaveCurrentPosition();
            }
        }
        
        // Wenn Video stoppt, zeige wieder Play Button und Banner
        if (e.NewState == MediaElementState.Stopped)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                VideoPlayer.IsVisible = false;
                PlayButton.IsVisible = true;
                BannerImage.IsVisible = true;
                _lastPosition = TimeSpan.Zero;
            });
        }
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Media failed: {e.ErrorMessage}");
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Verstecke Video Player
                VideoPlayer.IsVisible = false;
                VideoPlayer.Source = null;
                
                // Zeige Play Button und Banner
                PlayButton.IsVisible = true;
                BannerImage.IsVisible = true;
                
                // Zeige Fehlermeldung im Banner
                if (ErrorLabel != null)
                {
                    ErrorLabel.Text = $"⚠ Wiedergabe fehlgeschlagen: {e.ErrorMessage ?? "Unbekannter Fehler"}";
                    ErrorLabel.IsVisible = true;
                }
                
                // Stoppe Timer
                _positionUpdateTimer?.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling media failure: {ex.Message}");
            }
        });
    }

    private async void OnDownloadEpisodeTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is TVShowEpisodeViewModel episode)
        {
            try
            {
                // Prüfe ob bereits heruntergeladen
                var existing = await DownloadManager.Instance.GetDownloadAsync(episode.EpisodeId, MediaTypes.Episode);
                
                if (existing != null && existing.Status == DownloadStatus.Completed)
                {
                    await DisplayAlert("Bereits heruntergeladen", $"'{episode.Title}' wurde bereits heruntergeladen.", "OK");
                    return;
                }
                
                if (existing != null && existing.Status == DownloadStatus.Downloading)
                {
                    await DisplayAlert("Download läuft", $"'{episode.Title}' wird bereits heruntergeladen.", "OK");
                    return;
                }

                // Erstelle Request und füge zur Queue hinzu (Download, nicht Cache)
                var request = new VideoRequest
                {
                    VideoId = episode.EpisodeId,
                    VideoType = MediaTypes.Episode,
                    Title = episode.Title ?? "Episode"
                };
                
                await DownloadManager.Instance.QueueDownloadAsync(request, DownloadRetentionType.Download);
                
                await DisplayAlert("Download gestartet", $"'{episode.Title}' wurde zur Download-Warteschlange hinzugefügt.\nDer Download bleibt 7 Tage gespeichert.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Fehler", $"Download konnte nicht gestartet werden: {ex.Message}", "OK");
            }
        }
    }

    private async void OnPositionUpdateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await SaveCurrentPosition();
    }

    private async Task SaveCurrentPosition()
    {
        if (_currentVideoRequest == null || VideoPlayer.CurrentState == MediaElementState.None)
            return;

        try
        {
            var position = VideoPlayer.Position;
            var duration = VideoPlayer.Duration;
            
            if (position <= TimeSpan.Zero || duration <= TimeSpan.Zero)
                return;

            var positionSeconds = position.TotalSeconds;
            var durationSeconds = duration.TotalSeconds;

            if (_isLocalFile)
            {
                // Lokale Datei: Speichere in DB
                await DownloadManager.Instance.UpdatePlaybackPositionAsync(
                    _currentVideoRequest.VideoId,
                    _currentVideoRequest.VideoType,
                    positionSeconds,
                    durationSeconds
                );
            }
            else
            {
                // Stream: Sende an Server
                await SendPositionToServerAsync(
                    _currentVideoRequest.VideoId,
                    _currentVideoRequest.VideoType,
                    positionSeconds,
                    durationSeconds
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving position: {ex.Message}");
        }
    }

    private async Task SendPositionToServerAsync(long videoId, string videoType, double positionSeconds, double durationSeconds)
    {
        try
        {
            var client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
            if (client == null)
            {
                System.Diagnostics.Debug.WriteLine("VideoWebPlayerClient not available");
                return;
            }

            await client.ReportPlaybackProgressAsync(
                videoType,
                videoId,
                (long)positionSeconds,
                (long)durationSeconds
            );
            
            System.Diagnostics.Debug.WriteLine($"Successfully sent position to server: {positionSeconds}s / {durationSeconds}s for {videoType} {videoId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending position to server: {ex.Message}");
        }
    }
}
