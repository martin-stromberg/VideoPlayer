using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;

namespace VideoWebPlayer.Maui;

public partial class TVShowDetailsPage : ContentPage
{
	private readonly TVShowDetailsViewModel _viewModel;
	private readonly long? _initialSeasonId;
	private readonly long? _initialEpisodeId;

	public TVShowDetailsPage(long tvShowId, long? seasonId = null, long? episodeId = null)
	{
		InitializeComponent();
		_initialSeasonId = seasonId;
		_initialEpisodeId = episodeId;
		_viewModel = new TVShowDetailsViewModel(tvShowId);
		BindingContext = _viewModel;
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;

		// Registriere Download-Completed Event
		DownloadQueue.Instance.DownloadCompleted += OnDownloadCompleted;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		try
		{
			await _viewModel.LoadDataAsync(_initialSeasonId, _initialEpisodeId);
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
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

	private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TVShowDetailsViewModel.Episodes))
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(50);
				await _viewModel.RefreshDownloadStatusAsync();
			});
		}
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (Navigation.NavigationStack.Count > 1)
			await Navigation.PopAsync();
	}

	private async void OnToggleShowFavoriteClicked(object? sender, EventArgs e)
	{
		await _viewModel.ToggleSelectionFavoriteAsync();
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
        var episode = (sender as BindableObject)?.BindingContext as TVShowEpisodeViewModel ?? _viewModel.SelectedEpisode;
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
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Download konnte nicht gestartet werden: {ex.Message}", "OK");
        }
    }

	private async void OnRemoveDownloadEpisodeTapped(object? sender, TappedEventArgs e)
	{
		var episode = (sender as BindableObject)?.BindingContext as TVShowEpisodeViewModel;
		if (episode == null)
			return;

		var confirm = await DisplayAlert("Download entfernen", $"Download von '{episode.Title}' wirklich entfernen?", "Entfernen", "Abbrechen");
		if (!confirm)
			return;

		try
		{
			var deleted = await DownloadManager.Instance.DeleteDownloadAsync(episode.EpisodeId, MediaTypes.Episode);
			if (deleted)
			{
				episode.IsDownloaded = false;
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Fehler", $"Download konnte nicht entfernt werden: {ex.Message}", "OK");
		}
	}

    private async void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
			if (!e.Success)
			{
				await DisplayAlert("Download fehlgeschlagen",
					e.ErrorMessage ?? "Unbekannter Fehler",
					"OK");
				return;
			}

			if (!string.Equals(DownloadManager.NormalizeVideoType(e.VideoType), MediaTypes.Episode, StringComparison.OrdinalIgnoreCase))
				return;

			var ep = _viewModel.FindEpisode(e.VideoId);
			if (ep != null)
				ep.IsDownloaded = true;
        });
    }
}
