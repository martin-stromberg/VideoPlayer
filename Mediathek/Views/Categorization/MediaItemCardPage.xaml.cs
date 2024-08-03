using Mediathek;
using Mediathek.Models.MediaItems;
using Mediathek.ViewModels.Categorization;

namespace Mediathek.Views.Categorization
{
    [QueryProperty(nameof(Item), "Item")]
    public partial class MediaItemCardPage: ContentPage
    {

        public MediaItemCardPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<MediaItemDetailsViewModel>();
        }

        public MediaItem Item
        {
            get
            {
                return ViewModel.Item;
            }
            set
            {
                ViewModel.Item = value;
            }
        }

        public MediaItemDetailsViewModel ViewModel { get; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel.OnAppeared();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ViewModel.OnDisappeared(true);
        }

    }
}