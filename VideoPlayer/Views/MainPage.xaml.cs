using System.ComponentModel;
using VideoPlayer.ViewModels.Global;

namespace VideoPlayer.Views
{
    public partial class MainPage: ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<ApplicationViewModel>();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            DeviceDisplay.KeepScreenOn = true;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e) { }

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
