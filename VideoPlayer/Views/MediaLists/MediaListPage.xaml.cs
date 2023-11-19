using VideoPlayer.ViewModels.MediaLists;

namespace VideoPlayer.Views.MediaLists
{
    [QueryProperty(nameof(Category), "Category")]
    public partial class MediaList: ContentPage
    {

        public MediaList()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<MediaItemListViewModel>();
        }

        protected MediaItemListViewModel ViewModel { get; }

        public string Category
        {
            get
            {
                return ViewModel.Category;
            }
            set
            {
                ViewModel.Category = value;
            }
        }

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