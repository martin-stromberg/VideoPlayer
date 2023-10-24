
using VideoPlayer.ViewModels.MediaLists;

namespace VideoPlayer.Views.MediaLists
{
    public partial class MoviesPage: ContentPage
    {

        public MoviesPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<MoviesListViewModel>();
        }

        public MoviesListViewModel ViewModel { get; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.OnAppeared();
        }

    }
}