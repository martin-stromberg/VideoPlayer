namespace VideoPlayer
{
    public partial class App: Application
    {

        public static T GetService<T>()
        {
            return ((App)App.Current).ServiceProvider.GetService<T>();
        }

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            ServiceProvider = serviceProvider;
            MainPage = new AppShell();
        }

        public IServiceProvider ServiceProvider { get; }

    }
}
