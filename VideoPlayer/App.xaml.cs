using System.Diagnostics;

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
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            { CurrentDomain_UnhandledException(sender, e); };
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine(e.ExceptionObject?.ToString());
        }

        public IServiceProvider ServiceProvider { get; }

    }
}
