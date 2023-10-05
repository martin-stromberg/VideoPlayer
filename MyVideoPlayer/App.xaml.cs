using MyVideoPlayer.Helper.LibraryScan;

namespace MyVideoPlayer
{
    public partial class App : Application
    {
        private readonly ILibraryCollector libraryCollector;

        public App(IServiceProvider serviceProvider, ILibraryCollector libraryCollector)
        {
            InitializeComponent();

            MainPage = new AppShell();
            ServiceProvider = serviceProvider;
            this.libraryCollector = libraryCollector;
        }

        public IServiceProvider ServiceProvider { get; }

        public static T GetService<T>()
        {
            return ((App)App.Current).ServiceProvider.GetService<T>();
        }
    }
}
