using Mediathek.ViewModels.Global;
using System.ComponentModel;

namespace Mediathek.Views
{
    public partial class MainPage: ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
            DeviceDisplay.KeepScreenOn = true;
            BindingContext = ViewModel = ApplicationViewModel.Empty();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e) { }

        public ApplicationViewModel ViewModel { get; private set; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (ViewModel.IsDummy)
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    ViewModel.Fill(App.GetService<ApplicationViewModel>());
                    ViewModel.OnAppeared();
                });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ViewModel?.OnDisappeared(true);
        }

    }

}
