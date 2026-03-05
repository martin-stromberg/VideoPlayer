using Microsoft.Extensions.Configuration;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.ViewModels;

namespace VideoWebPlayer.Maui
{
    public partial class MainPage : ContentPage
    {
        private readonly ISettingsService _settings;
        private readonly IConnectionService _connection;
        private readonly IAuthService _auth;
        private readonly IServiceProvider _services;
        private bool _started;

        public MainPage(ISettingsService? settings, IConnectionService? connection, IAuthService? auth, IServiceProvider services)
        {
            InitializeComponent();
            _settings = settings ?? new Services.SettingsService();
            _connection = connection ?? new Services.ConnectionService();
            _auth = auth ?? new Services.AuthService();
            _services = services;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!_started)
            {
                _started = true;
                _ = RunStartupWorkflowAsync();
            }
        }

        private async Task RunStartupWorkflowAsync()
        {
            var nav = this.Navigation;

            try
            {
                while (true)
                {
                    // Ensure server address
                    while (!_settings.HasServerAddress())
                    {
                        var settingsPage = _services.GetService(typeof(ServerSetupPage)) as ServerSetupPage ?? new ServerSetupPage(_settings);
                        await nav.PushModalAsync(new NavigationPage(settingsPage));
                        if (!_settings.HasServerAddress())
                        {
                            // user exited
                            return;
                        }
                    }

                    // Try connecting
                    var loading = new LoadingPage("Verbinde mit Server...");
                    await nav.PushModalAsync(new NavigationPage(loading));
                    bool connected = false;
                    try
                    {
                        connected = await _connection.TryConnectAsync(_settings.ServerAddress ?? string.Empty);
                    }
                    finally
                    {
                        await nav.PopModalAsync();
                    }

                    if (!connected)
                    {
                        // Offer offline mode
                        bool continueOffline = await DisplayAlertAsync(
                            "Keine Verbindung", 
                            "Der Server ist nicht erreichbar. Möchten Sie im Offline-Modus fortfahren?", 
                            "Offline fortfahren", 
                            "Einstellungen öffnen");
                        
                        if (continueOffline)
                        {
                            // Start in offline mode
                            var offlineHome = _services.GetService(typeof(HomePage)) as HomePage ?? new HomePage();
                            if (offlineHome.BindingContext is HomePageViewModel homeViewModel)
                            {
                                homeViewModel.SetOfflineMode(true);
                            }
                            
                            if (Application.Current?.Windows.Count > 0)
                            {
                                Application.Current.Windows[0].Page = new NavigationPage(offlineHome);
                            }
                            break;
                        }
                        else
                        {
                            // show settings again to allow correction
                            var settingsPage = _services.GetService(typeof(ServerSetupPage)) as ServerSetupPage ?? new ServerSetupPage(_settings);
                            await nav.PushModalAsync(new NavigationPage(settingsPage));
                            // loop continues
                            continue;
                        }
                    }

                    // Connected. Ensure user is authenticated
                    while (!_auth.HasCredentials())
                    {
                        var loginPage = _services.GetService(typeof(LoginPage)) as LoginPage ?? new LoginPage(_auth);
                        await nav.PushModalAsync(new NavigationPage(loginPage));
                        if (!_auth.HasCredentials())
                        {
                            // user cancelled login
                            return;
                        }
                    }

                    // success -> navigate to HomePage using Window
                    var home = _services.GetService(typeof(HomePage)) as Page ?? new HomePage();
                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new NavigationPage(home);
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                // show error so it's visible during development
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await this.DisplayAlertAsync("Fehler", ex.Message, "OK");
                    }
                    catch { }
                });
            }
        }
    }
}
