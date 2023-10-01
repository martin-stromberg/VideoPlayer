
using IssueTest.ViewModels;

namespace IssueTest
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = new MainFormViewModel();
        }

        internal MainFormViewModel ViewModel { get; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Task.Run(() => {
                ViewModel.StartAsync();
            });
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            ViewModel.ProcessClick();
        }
    }

}
