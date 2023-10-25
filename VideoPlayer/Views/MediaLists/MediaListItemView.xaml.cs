
using VideoPlayer.ViewModels.MediaLists;

namespace VideoPlayer.Views.MediaLists
{
    public partial class MediaListItemView: ContentView
    {

        public MediaListItemView()
        {
            InitializeComponent();
        }

        protected MediaListItemViewModel ViewModel => BindingContext as MediaListItemViewModel;

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            ViewModel.OpenDetails();
        }

    }
}