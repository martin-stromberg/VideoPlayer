using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace VideoWebPlayer.Maui
{
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; set; }
        private bool _startupInitialized;

        public App()
        {
            InitializeComponent();
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_startupInitialized)
                    return;
                var waited = 0;
                while (ServiceProvider is null && waited < 5000)
                {
                    await Task.Delay(50);
                    waited += 50;
                }
                if (ServiceProvider is not null && !_startupInitialized)
                {
                    _startupInitialized = true;
                    try
                    {
                        InitializeAfterServices(ServiceProvider);
                    }
                    catch { }
                }
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Always return a Window with a ContentPage to satisfy MAUI startup requirements
            // Do NOT set Application.Current.MainPage anywhere else in the app
            var main = ServiceProvider?.GetService<MainPage>() ?? Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<MainPage>(ServiceProvider!);
            var window = new Window(new NavigationPage(main))
            {
                // Titelleiste entfernen
                Title = string.Empty
            };
            
            // Plattformübergreifend: TitleBar auf null setzen
            window.Created += (s, e) =>
            {
                if (s is Window w)
                {
                    w.TitleBar = null;
                }
            };
            
            return window;
        }

        public void InitializeAfterServices(IServiceProvider services)
        {
            // Do NOT set Application.Current.MainPage here!
            // All navigation must use the returned Window/Page
        }
    }
}
