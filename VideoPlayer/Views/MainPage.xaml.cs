using VideoPlayer.ViewModels.Global;

namespace VideoPlayer.Views
{
    public partial class MainPage: ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<ApplicationViewModel>();
        }

        public ApplicationViewModel ViewModel { get; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ViewModel?.OnAppeared();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ViewModel?.OnDisappeared(true);
        }

    }

}
