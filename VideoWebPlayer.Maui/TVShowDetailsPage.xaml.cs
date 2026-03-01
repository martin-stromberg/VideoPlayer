using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;

namespace VideoWebPlayer.Maui;

public partial class TVShowDetailsPage : ContentPage
{
    private readonly TVShowDetailsViewModel _viewModel;
    private readonly long? _initialSeasonId;
    private VideoRequest? _currentVideoRequest;
    private TimeSpan _lastPosition;

    public TVShowDetailsPage(long tvShowId, long? seasonId = null)
    {
        InitializeComponent();
        _initialSeasonId = seasonId;
        _viewModel = new TVShowDetailsViewModel(tvShowId);
        BindingContext = _viewModel;
        
        // Registriere Download-Completed Event
        DownloadQueue.Instance.DownloadCompleted += OnDownloadCompleted;
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
        
        // Cleanup
        if (_currentVideoRequest != null)
        {
            _currentVideoRequest.SourceAvailable -= OnVideoSourceAvailable;
        }
        
        DownloadQueue.Instance.DownloadCompleted -= OnDownloadCompleted;
        
        // Stoppe Video Player
        VideoPlayer.Stop();
    }

    private async void OnPlayTapped(object? sender, EventArgs e)
    {
        // Spiele erste Episode der ausgewählten Staffel
        var firstEpisode = _viewModel.Episodes.FirstOrDefault();
        if (firstEpisode == null)
        {
            await DisplayAlert("Keine Episode", "Keine Episode zum Abspielen verfügbar.", "OK");
            return;
        }

        await PlayEpisodeAsync(firstEpisode);
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
                "Episode",
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

                // Setze Position zurück wenn vorhanden
                if (_lastPosition > TimeSpan.Zero)
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

    private async void OnDownloadEpisodeTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is TVShowEpisodeViewModel episode)
        {
            try
            {
                // Prüfe ob bereits heruntergeladen
                var existing = await DownloadManager.Instance.GetDownloadAsync(episode.EpisodeId, "Episode");
                
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
                    VideoType = "Episode",
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
}
