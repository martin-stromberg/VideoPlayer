using VideoPlayer.Views;

namespace VideoPlayer
{
    public partial class AppShell: Shell
    {

        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("movies", typeof(MoviesPage));
        }

    }
}
