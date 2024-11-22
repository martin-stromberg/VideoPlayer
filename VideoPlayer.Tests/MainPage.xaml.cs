namespace VideoPlayer.Tests
{
    public partial class MainPage: ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            TestContainer.OnAppearing();
        }

    }

}
