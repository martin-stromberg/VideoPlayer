using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;

namespace VideoWebPlayer.Maui;

public partial class MovieCollectionDetailsPage : ContentPage
{
    private readonly MovieCollectionDetailsViewModel _viewModel;

    public MovieCollectionDetailsPage(long collectionId)
    {
        InitializeComponent();
        _viewModel = new MovieCollectionDetailsViewModel(collectionId);
        BindingContext = _viewModel;
        
        // Registriere Download-Completed Event
        DownloadQueue.Instance.DownloadCompleted += OnDownloadCompleted;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.LoadDataAsync();
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

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (Navigation.NavigationStack.Count > 1)
			await Navigation.PopAsync();
	}

    private void OnMovieSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView cv && e.CurrentSelection.Count > 0)
        {
            var selectedMovie = e.CurrentSelection[0] as MediaItemViewModel;
            if (selectedMovie != null)
            {
                // Finde den Index des ausgewählten Films
                var index = _viewModel.Movies.IndexOf(selectedMovie);
                if (index >= 0)
                {
                    // Setze SelectedMovieIndex - das triggert PropertyChanged!
                    _viewModel.SelectedMovieIndex = index;
                    System.Diagnostics.Debug.WriteLine($"[MovieCollectionDetailsPage] Movie selected at index {index}: {selectedMovie.Title}");
                }
            }
        }
    }

    private async void OnDownloadMovieTapped(object? sender, EventArgs e)
    {
        var movie = _viewModel.SelectedMovie;
        if (movie == null || !movie.EntryId.HasValue)
        {
            await DisplayAlert("Keine Auswahl", "Bitte wählen Sie einen Film aus.", "OK");
            return;
        }

        try
        {
            var videoRequest = await DownloadManager.Instance.RequestVideoAsync(
                movie.EntryId.Value,
                Models.MediaTypes.Movie,
                movie.Title ?? "Unknown Movie");

            await DownloadManager.Instance.QueueDownloadAsync(videoRequest, DownloadRetentionType.Cache);

            await DisplayAlert("Download gestartet", 
                $"'{movie.Title}' wird heruntergeladen und 7 Tage lang gespeichert.", 
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
            var title = _viewModel.SelectedMovie?.Title ?? "Unknown";
            await DisplayAlert("Download abgeschlossen",
                $"'{title}' wurde erfolgreich heruntergeladen.",
                "OK");
        });
    }
}
