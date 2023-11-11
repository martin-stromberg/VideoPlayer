

using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.Views.MediaLists
{

    public partial class MediaListItemView: ContentView
    {

        public MediaListItemView()
        {
            InitializeComponent();
        }

        protected BaseMediaListItemViewModel ViewModel => BindingContext as BaseMediaListItemViewModel;

        private void ListItemTapped(object sender, TappedEventArgs e)
        {
            ViewModel.OpenDetails();
        }

        private void ListItemPlaybackTapped(object sender, TappedEventArgs e)
        {
            ViewModel.StartPlayback.Execute(true);
        }

    }
}