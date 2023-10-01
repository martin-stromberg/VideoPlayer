using MyVideoPlayer.ViewModels;

namespace MyVideoPlayer
{
    public partial class MainPage : ContentPage
    {
        private HomePageViewModel viewModel;
        public MainPage()
        {
            InitializeComponent();            
            BindingContext = viewModel = App.GetService<HomePageViewModel>();
            DeviceDisplay.Current.KeepScreenOn = true;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            viewModel.StartInitializationAsync();
        }

    }

}
