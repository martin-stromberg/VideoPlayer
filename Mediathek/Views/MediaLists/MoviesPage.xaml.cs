using Mediathek.Models.Movies;
using Mediathek.ViewModels.MediaLists;

namespace Mediathek.Views.MediaLists
{
    [QueryProperty(nameof(Collection), "Collection")]
    public partial class MoviesPage: ContentPage
    {

        public MoviesPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<MoviesListViewModel>();
        }

        public MoviesListViewModel ViewModel { get; }

        private MovieCollection collection;

        public MovieCollection Collection
        {
            get
            {
                return collection;
            }
            set
            {
                collection = value;
                OnPropertyChanged();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.SetParent(Collection);
            ViewModel.OnAppeared();
        }

    }
}