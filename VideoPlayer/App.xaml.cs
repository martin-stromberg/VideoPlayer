using VideoPlayer.Service;

namespace VideoPlayer
{
    public partial class App: Application
    {

        public App(IApplicationManager appInitialize)
        {
            InitializeComponent();

            MainPage = new AppShell(appInitialize);
        }

    }
}
