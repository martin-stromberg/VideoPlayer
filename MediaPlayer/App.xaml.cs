using System.Diagnostics;

namespace MediaPlayer
{
    public partial class App: Application
    {

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            ServiceProvider = serviceProvider;
            MainPage = new AppShell();
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            { CurrentDomain_UnhandledException(sender, e); };
        }

        public static T GetService<T>() => ((App)Current).ServiceProvider.GetService<T>();

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine(e.ExceptionObject?.ToString());
        }

        public IServiceProvider ServiceProvider { get; }

    }
}
