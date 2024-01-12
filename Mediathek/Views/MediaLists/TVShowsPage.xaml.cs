using Mediathek.Models.TVShows;
using Mediathek.ViewModels.MediaLists;

namespace Mediathek.Views.MediaLists
{
    [QueryProperty(nameof(Show), "Show")]
    [QueryProperty(nameof(Season), "Season")]
    public partial class TVShowsPage: ContentPage
    {

        public TVShowsPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<TVShowListViewModel>();
        }

        public TVShowListViewModel ViewModel { get; }

        private TVShow show;

        public TVShow Show
        {
            get
            {
                return show;
            }
            set
            {
                show = value;
                OnPropertyChanged();
            }
        }

        private TVShowSeason season;

        public TVShowSeason Season
        {
            get
            {
                return season;
            }
            set
            {
                season = value;
                OnPropertyChanged();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.SetParent(Show, Season);
            ViewModel.OnAppeared();
        }

    }
}