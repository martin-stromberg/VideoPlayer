using Mediathek.ViewModels.MediaLists.MediaListItem;

namespace Mediathek.Views.MediaLists.ListItems
{
    public partial class MediaListItemBoxView: ContentView
    {

        public MediaListItemBoxView()
        {
            InitializeComponent();
        }

        protected BaseMediaListItemViewModel ViewModel => BindingContext as BaseMediaListItemViewModel;

        private void ListItemTapped(object sender, TappedEventArgs e)
        {
            ViewModel.OpenDetails();
        }

        private void ListItemDetailTapped(object sender, TappedEventArgs e)
        {
            ViewModel.OpenCategory();
        }

    }
}