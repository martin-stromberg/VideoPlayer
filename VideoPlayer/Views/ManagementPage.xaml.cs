
using VideoPlayer.ViewModels.Management;

namespace VideoPlayer.Views
{
    public partial class ManagementPage: ContentPage
    {

        public ManagementPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<ManagementViewModel>();
        }

        public ManagementViewModel ViewModel { get; }

        private void NavigationButtonClicked(object sender, EventArgs e)
        {
            ViewModel.ChangeView((sender as Button).BindingContext as BaseManagementContentViewModel);
        }

    }
}