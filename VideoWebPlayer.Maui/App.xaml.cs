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
            // Registriere Handler für abgelaufene Tokens
            var client = services.GetService<VideoWebPlayer.Client.VideoWebPlayerClient>();
            if (client != null)
            {
                client.UnauthorizedReceived += OnUnauthorizedReceived;
            }
        }
        
        private async void OnUnauthorizedReceived(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Token expired - attempting re-authentication");
            
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var authService = ServiceProvider?.GetService<Services.IAuthService>();
                    var settingsService = ServiceProvider?.GetService<Services.ISettingsService>();
                    
                    if (authService == null || settingsService == null)
                        return;
                    
                    // Versuche Auto-Login mit gespeicherten Credentials
                    if (authService.HasCredentials())
                    {
                        var credentials = authService.GetCredentials();
                        var serverAddress = settingsService.ServerAddress;
                        
                        if (!string.IsNullOrWhiteSpace(serverAddress))
                        {
                            Preferences.Default.Set("ServerAddress", serverAddress);
                        }
                        
                        var success = await authService.LoginAsync(credentials.username, credentials.password);
                        
                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("Re-authentication successful");
                            return; // Erfolgreich re-authenticated
                        }
                    }
                    
                    // Re-Authentication fehlgeschlagen -> Offline-Modus
                    System.Diagnostics.Debug.WriteLine("Re-authentication failed - switching to offline mode");
                    await SwitchToOfflineModeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during re-authentication: {ex.Message}");
                    await SwitchToOfflineModeAsync();
                }
            });
        }
        
        private async Task SwitchToOfflineModeAsync()
        {
            try
            {
                var homePage = ServiceProvider?.GetService<HomePage>() ?? new HomePage();
                
                if (homePage.BindingContext is ViewModels.HomePageViewModel homeViewModel)
                {
                    homeViewModel.SetOfflineMode(true);
                }
                
                if (Current?.Windows.Count > 0)
                {
                    Current.Windows[0].Page = new NavigationPage(homePage);
                }
                
                // Zeige Benachrichtigung
                if (Current?.MainPage != null)
                {
                    await Current.MainPage.DisplayAlert(
                        "Offline-Modus", 
                        "Die Sitzung ist abgelaufen und die erneute Anmeldung ist fehlgeschlagen. Die App läuft jetzt im Offline-Modus.", 
                        "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching to offline mode: {ex.Message}");
            }
        }
    }
}
