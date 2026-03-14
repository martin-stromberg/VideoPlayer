using VideoWebPlayer.Maui.ViewModels;

namespace VideoWebPlayer.Maui;

public partial class SourceOverviewPage : ContentPage
{
    private readonly SourceOverviewViewModel _viewModel;
    private const int ItemWidth = 150;
    private int _currentSpan = 4;
    private bool _isLoadingInProgress = false;

    public SourceOverviewPage(long sourceId, string sourceName)
    {
        InitializeComponent();
        _viewModel = new SourceOverviewViewModel(sourceId, sourceName);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        CalculateAndSetSpan();

        try
        {
            await _viewModel.LoadGenresAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Genres konnten nicht geladen werden: {ex.Message}", "OK");
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        
        // Span wird bei Größenänderung angepasst (z.B. Maximieren, Rotation)
        CalculateAndSetSpan();
    }

    private void CalculateAndSetSpan()
    {
        var availableWidth = Width - 200 - 20;
        
        if (availableWidth > 0 && ItemsCollectionView?.ItemsLayout is GridItemsLayout gridLayout)
        {
            var columns = Math.Max(2, (int)(availableWidth / (ItemWidth + 15)));
            
            if (columns != _currentSpan)
            {
                gridLayout.Span = columns;
                _currentSpan = columns;
            }
        }
    }

    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (_isLoadingInProgress || _viewModel.IsLoadingMore || _viewModel.IsLoading)
            return;

        var scrollView = sender as ScrollView;
        if (scrollView == null)
            return;

        // Berechne ob wir nahe am Ende sind (70% gescrollt)
        var scrollingSpace = scrollView.ContentSize.Height - scrollView.Height;
        if (scrollingSpace <= 0)
            return;

        var threshold = 0.7; // 70% gescrollt
        if (e.ScrollY >= scrollingSpace * threshold)
        {
            await LoadMultiplePagesAsync();
            OnScrolled(sender, e);
        }
    }

    private async Task LoadMultiplePagesAsync()
    {
        if (_isLoadingInProgress)
            return;

        _isLoadingInProgress = true;
        
        try
        {
            // Lade 3 Seiten hintereinander ohne zu prüfen
            // Das sollte genug sein um auch schnelles Scrollen abzudecken
            for (int i = 0; i < 3; i++)
            {
                await _viewModel.LoadMoreItemsAsync();
                
                // Sehr kurze Pause zwischen den Loads
                await Task.Delay(100);
            }
        }
        finally
        {
            // Kürzere Verzögerung vor erneutem Triggern
            await Task.Delay(200);
            _isLoadingInProgress = false;            
        }
    }

    private async void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MediaItemViewModel selectedItem)
        {
            Page? detailPage = null;

            if (selectedItem.MediaType == "show" && selectedItem.EntryId.HasValue)
            {
                detailPage = new TVShowDetailsPage(selectedItem.EntryId.Value);
            }
            else if (selectedItem.MediaType == "collection" && selectedItem.EntryId.HasValue)
            {
                detailPage = new MovieCollectionDetailsPage(selectedItem.EntryId.Value);
            }

            if (detailPage != null)
            {
                await Navigation.PushAsync(detailPage);
            }

            if (sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }
        }
    }
}
