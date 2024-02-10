using Mediathek.Models.TVShows;
using Mediathek.ViewModels.MediaLists.Details;
using System.ComponentModel;

namespace MediaPlayer.Views.MediaLists.Cards
{
    [QueryProperty(nameof(Collection), "Collection")]
    [QueryProperty(nameof(Show), "Show")]
    [QueryProperty(nameof(Season), "Season")]
    [QueryProperty(nameof(Episode), "Episode")]
    public partial class TVShowCardPage: ContentPage
    {

        public TVShowCardPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<TVShowDetailsViewModel>();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TVShowDetailsViewModel.SelectedEpisode):
                    if (ViewModel.SelectedEpisode != null)
                    {
                        itemList.ScrollTo(ViewModel.SelectedEpisode);
                        ViewModel.SelectedEpisode = null;
                    }
                    break;
            }
        }

        private TVShowCollection collection;

        public TVShowCollection Collection
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

        private TVShowEpisode episode;

        public TVShowEpisode Episode
        {
            get
            {
                return episode;
            }
            set
            {
                episode = value;
                OnPropertyChanged();
            }
        }

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

        public TVShowDetailsViewModel ViewModel { get; private set; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.SetParent(Collection, Show, Season, Episode);
            ViewModel.OnAppeared();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ViewModel.OnDisappeared(true);
        }

    }
}