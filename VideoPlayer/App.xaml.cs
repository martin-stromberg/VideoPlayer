using VideoPlayer.Properties;
using VideoPlayer.Service;
using VideoPlayer.Service.ErrorHandling;

namespace VideoPlayer
{
    public partial class App: Application
    {
        private readonly IErrorLogManager _errorLogManager;

        public App(IApplicationManager appInitialize)
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            MainPage = new AppShell(appInitialize);
            _errorLogManager = appInitialize.ResolveService<IErrorLogManager>();
        }
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
                _errorLogManager.WriteError(e.ExceptionObject as Exception);
        }

        protected override void OnSleep()
        {
            base.OnSleep();
        }

    }
}
