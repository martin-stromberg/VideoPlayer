
using VideoPlayer.Models.TVShows;
using VideoPlayer.ViewModels.MediaLists.Details;

namespace VideoPlayer.Views.MediaLists.Cards
{
    [QueryProperty(nameof(Show), "Show")]
    public partial class TVShowCardPage: ContentPage
    {

        public TVShowCardPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<TVShowDetailsViewModel>();
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

        public TVShowDetailsViewModel ViewModel { get; private set; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.SetParent(Show);
            ViewModel.OnAppeared();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ViewModel.OnDisappeared(true);
        }

    }
}