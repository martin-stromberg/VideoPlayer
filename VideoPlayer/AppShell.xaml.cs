using VideoPlayer.Service;

namespace VideoPlayer
{
    public partial class AppShell: Shell
    {

        private readonly IApplicationManager _AppInitializer;

        public AppShell(IApplicationManager appInitializer)
        {
            _AppInitializer = appInitializer;
            InitializeComponent();
        }
    }
}
