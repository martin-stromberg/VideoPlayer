using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace VideoWebPlayer.Maui
{
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; set; }
        private bool _startupInitialized;
		private bool _onlineInitialized;

		public static Task SafeDisplayAlertAsync(string title, string message, string cancel)
			=> SafeDisplayAlertAsync(title, message, accept: null, cancel, defaultAcceptResult: true);

		public static Task<bool> SafeDisplayAlertAsync(string title, string message, string accept, string cancel)
			=> SafeDisplayAlertAsync(title, message, accept, cancel, defaultAcceptResult: false);

		private static async Task<bool> SafeDisplayAlertAsync(
			string title,
			string message,
			string? accept,
			string cancel,
			bool defaultAcceptResult)
		{
			try
			{
				return await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					var page = Current?.MainPage;
					if (page is null)
						return defaultAcceptResult;

					// On Windows, showing a dialog before the page is fully attached can throw:
					// "This element does not have a XamlRoot".
					var start = DateTime.UtcNow;
					while ((page.Handler is null || page.Window is null) && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
						await Task.Delay(50);

					try
					{
						if (accept is null)
						{
							await page.DisplayAlert(title, message, cancel);
							return defaultAcceptResult;
						}

						return await page.DisplayAlert(title, message, accept, cancel);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"[App] DisplayAlert failed: {ex.Message}");
						return defaultAcceptResult;
					}
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[App] SafeDisplayAlertAsync failed: {ex.Message}");
				return defaultAcceptResult;
			}
		}

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
            // Use AppShell as the root when available so the Shell flyout is shown on mobile.
            // Fall back to the previous NavigationPage(MainPage) behavior when Shell is not registered.
            object root = ServiceProvider?.GetService<AppShell>() ?? ActivatorUtilities.CreateInstance<AppShell>(ServiceProvider!);
            // Always return a Window with the chosen root element
            var window = new Window((Page)root)
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
			// Ensure this only runs once per app instance.
			if (_startupInitialized)
				return;
			_startupInitialized = true;

            // Configure DownloadManager with event publisher
            var eventPublisher = services.GetService<VideoWebPlayer.Maui.Services.Events.IPublishNotificationEvent>();
            if (eventPublisher != null)
            {
                Services.DownloadManager.Instance.SetEventPublisher(eventPublisher);
                System.Diagnostics.Debug.WriteLine("[App] DownloadManager configured with event publisher");
            }
        }

		public void InitializeAfterStartupWorkflowOnline(IServiceProvider services)
		{
			// This can be triggered after the startup workflow (server setup + connect + login)
			// and may also be called again after a token refresh.
			if (!_onlineInitialized)
			{
				_onlineInitialized = true;
				try
				{
					var coordinator = services.GetService<Services.WatchlistDownloadCoordinatorService>();
					if (coordinator != null)
					{
						_ = coordinator.RunOnStartupAsync();
						System.Diagnostics.Debug.WriteLine("[App] WatchlistDownloadCoordinatorService started");
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[App] WatchlistDownloadCoordinatorService start failed: {ex.Message}");
				}
			}

			// Always attempt to (re)initialize SignalR when we are in online mode.
			_ = InitializeSignalRAsync(services);
		}
        
        private async Task InitializeSignalRAsync(IServiceProvider services)
        {
            try
            {
                var signalRService = services.GetService<Services.SignalRService>();
                var authService = services.GetService<Services.IAuthService>();
                var settingsService = services.GetService<Services.ISettingsService>();
                
                if (signalRService == null || authService == null || settingsService == null)
                    return;

				// Wait until server address + credentials are available.
				// `InitializeAfterServices` can run before the startup workflow finishes (first app launch/login),
				// so we poll for a short time instead of returning early.
				var waitStart = DateTime.UtcNow;
				while ((!authService.HasCredentials() || !settingsService.HasServerAddress())
					&& DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(30))
				{
					await Task.Delay(500);
				}

				if (!authService.HasCredentials() || !settingsService.HasServerAddress())
				{
					System.Diagnostics.Debug.WriteLine("[SignalR] No credentials or server address - skipping");
					return;
				}
                
                var serverAddress = settingsService.ServerAddress;
				var token = settingsService.GetAuthToken() ?? string.Empty;
				if (string.IsNullOrEmpty(token))
				{
					// Token is usually written during/after login - wait a bit.
					var tokenWaitStart = DateTime.UtcNow;
					while (string.IsNullOrEmpty(token) && DateTime.UtcNow - tokenWaitStart < TimeSpan.FromSeconds(30))
					{
						await Task.Delay(500);
						token = settingsService.GetAuthToken() ?? string.Empty;
					}
				}

				if (string.IsNullOrEmpty(token))
				{
					System.Diagnostics.Debug.WriteLine("[SignalR] No auth token - skipping");
					return;
				}
                
                // Verbinde zu SignalR Hub
                await signalRService.ConnectAsync(serverAddress ?? string.Empty, token);
                
                System.Diagnostics.Debug.WriteLine("[SignalR] Initialization completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] Initialization failed: {ex.Message}");
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
								settingsService.SetServerAddress(serverAddress);
							}
                        
                        var (success, error) = await authService.LoginAsync(credentials.username, credentials.password);

                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("Re-authentication successful");
                            // Ensure online-only services (SignalR, coordinator) are running with the refreshed token.
                            InitializeAfterStartupWorkflowOnline(ServiceProvider!);
                            return; // Erfolgreich re-authenticated
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Re-authentication failed: {error}");
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
				await SafeDisplayAlertAsync(
					"Offline-Modus",
					"Die Sitzung ist abgelaufen und die erneute Anmeldung ist fehlgeschlagen. Die App läuft jetzt im Offline-Modus.",
					"OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching to offline mode: {ex.Message}");
            }
        }
    }
}
