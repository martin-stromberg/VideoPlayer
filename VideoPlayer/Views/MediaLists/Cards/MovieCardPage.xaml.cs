using VideoPlayer.Models.Movies;
using VideoPlayer.ViewModels.MediaLists.Details;

namespace VideoPlayer.Views.MediaLists.Cards;

[QueryProperty(nameof(Movie), "Movie")]
[QueryProperty(nameof(MovieCollection), "Collection")]
public partial class MovieCardPage : ContentPage
{
	public MovieCardPage()
	{
		InitializeComponent();
        BindingContext = ViewModel = App.GetService<MovieDetailsViewModel>();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch(e.PropertyName)
        {
            case nameof(MovieDetailsViewModel.IsVideoInFullscreen):
                UpdateFullscreenMode();
                break;
        }
    }

    private void UpdateFullscreenMode()
    {
        ContentGrid.SetRowSpan(VideoPlayer, ViewModel.IsVideoInFullscreen ? 2 : 1);
    }

    private Movie movie;

    public Movie Movie
    {
        get
        {
            return movie;
        }
        set
        {
            movie = value;
            OnPropertyChanged();
        }
    }

    private MovieCollection movieCollection;

    public MovieCollection MovieCollection
    {
        get
        {
            return movieCollection;
        }
        set
        {
            movieCollection = value;
            OnPropertyChanged();
        }
    }

    public MovieDetailsViewModel ViewModel { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.SetParent(MovieCollection, Movie);
        ViewModel.OnAppeared();
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel.OnDisappeared(true);
    }
}