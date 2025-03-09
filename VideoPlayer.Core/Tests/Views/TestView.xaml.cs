
namespace VideoPlayer.Tests.Views
{
    public partial class TestView: ContentView
    {

        public TestView()
        {
            InitializeComponent();
            BindingContext = new TestViewModel(null);
        }

        public async void OnAppearing()
        {
            await Task.Delay(3000);            
            (BindingContext as TestViewModel).Run.Execute(this);
            (BindingContext as TestViewModel).ForceAll = true;
        }

    }
}