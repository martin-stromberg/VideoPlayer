using Mediathek.ViewModels.Management;

namespace MediaPlayer.Views
{
    public partial class ManagementPage: ContentPage
    {

        public ManagementPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<ManagementViewModel>();
        }

        public ManagementViewModel ViewModel { get; }

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

        private void NavigationButtonClicked(object sender, EventArgs e)
        {
            ViewModel.ChangeView((sender as Button)?.BindingContext as BaseManagementContentViewModel);
        }

    }
}