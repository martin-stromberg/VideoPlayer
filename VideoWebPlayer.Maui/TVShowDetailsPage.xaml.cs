using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;

namespace VideoWebPlayer.Maui;

public partial class TVShowDetailsPage : ContentPage
{
    private readonly TVShowDetailsViewModel _viewModel;
    private readonly long? _initialSeasonId;

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
        DownloadQueue.Instance.DownloadCompleted -= OnDownloadCompleted;
    }

    private void OnEpisodeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView cv && e.CurrentSelection.Count > 0)
        {
            var selectedEpisode = e.CurrentSelection[0] as TVShowEpisodeViewModel;
            if (selectedEpisode != null)
            {
                // Finde den Index der ausgewählten Episode
                var index = _viewModel.Episodes.IndexOf(selectedEpisode);
                if (index >= 0)
                {
                    // Setze SelectedEpisodeIndex - das triggert PropertyChanged!
                    _viewModel.SelectedEpisodeIndex = index;
                    System.Diagnostics.Debug.WriteLine($"[TVShowDetailsPage] Episode selected at index {index}: {selectedEpisode.Title}");
                }
            }
        }
    }

    private async void OnDownloadEpisodeTapped(object? sender, TappedEventArgs e)
    {
        var episode = _viewModel.SelectedEpisode;
        if (episode == null)
        {
            await DisplayAlert("Keine Auswahl", "Bitte wählen Sie eine Episode aus.", "OK");
            return;
        }

        try
        {
            var videoRequest = await DownloadManager.Instance.RequestVideoAsync(
                episode.EpisodeId,
                MediaTypes.Episode,
                episode.Title ?? "Unknown Episode");

            await DownloadManager.Instance.QueueDownloadAsync(videoRequest, DownloadRetentionType.Cache);

            await DisplayAlert("Download gestartet",
                $"'{episode.Title}' wird heruntergeladen und 7 Tage lang gespeichert.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Download konnte nicht gestartet werden: {ex.Message}", "OK");
        }
    }

    private async void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var title = _viewModel.SelectedEpisode?.Title ?? "Unknown";
            await DisplayAlert("Download abgeschlossen",
                $"'{title}' wurde erfolgreich heruntergeladen.",
                "OK");
        });
    }
}
