
namespace VideoPlayer.Views
{
    public partial class MoviesPage: ContentPage
    {

        public MoviesPage()
        {
            InitializeComponent();
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("movies");
        }

    }
}