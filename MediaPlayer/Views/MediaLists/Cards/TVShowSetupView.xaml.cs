
using Mediathek.Models.TVShows;
using Mediathek.ViewModels.MediaLists.Details;

namespace MediaPlayer.Views.MediaLists.Cards
{
    public partial class TVShowSetupView: ContentView
    {

        public TVShowSetupView()
        {
            InitializeComponent();
        }

        public TVShowDetailsViewModel ViewModel
        {
            get
            {
                return BindingContext as TVShowDetailsViewModel;
            }
        }

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e) { }

        private void AddShowButtonClicked(object sender, EventArgs e)
        {
            Action_AddShow.IsVisible = false;
            ViewModel.LoadUnassignedShows();
        }

        private void AddSelectedShowClicked(object sender, EventArgs e)
        {
            ViewModel.AssignShowToCollection(Picker_AddShow.SelectedItem as TVShowName);
        }

        private void CancelSelectedShowClicked(object sender, EventArgs e)
        {
            Action_AddShow.IsVisible = true;
            ViewModel.ClearUnassignedShows();
        }

    }
}