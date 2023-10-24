
using VideoPlayer.ViewModels.MediaLists;

namespace VideoPlayer.Views.MediaLists
{
    public partial class TVShowsPage: ContentPage
    {

        public TVShowsPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<TVShowListViewModel>();
        }

        public TVShowListViewModel ViewModel { get; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.OnAppeared();
        }

    }
}