using Microsoft.Extensions.Configuration;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services.Events;

namespace VideoWebPlayer.Maui
{
    using Microsoft.Maui.Devices;

    public partial class MainPage : ContentPage
    {
        private readonly ISettingsService _settings;
        private readonly IConnectionService _connection;
        private readonly IAuthService _auth;
        private readonly IServiceProvider _services;
        private bool _started;
        private HomePageViewModel _viewModel;
        private readonly VideoWebPlayer.Maui.Services.Events.ISubscribeNotificationEvent? _eventSubscriber;

        public MainPage(ISettingsService? settings, IConnectionService? connection, IAuthService? auth, IServiceProvider services)
        {
            InitializeComponent();
            _settings = settings ?? new Services.SettingsService();
            _connection = connection ?? new Services.ConnectionService();
            _auth = auth ?? new Services.AuthService();
            _services = services;

            // Initialize Home view model and bind UI
            _viewModel = new HomePageViewModel();
            BindingContext = _viewModel;

            // Subscribe to notification events
            _eventSubscriber = App.ServiceProvider?.GetService<VideoWebPlayer.Maui.Services.Events.ISubscribeNotificationEvent>();
            if (_eventSubscriber != null)
            {
                _eventSubscriber.Subscribe<VideoWebPlayer.Maui.Services.Events.ContinueWatchingUpdatedEvent>(OnContinueWatchingUpdated);
                _eventSubscriber.Subscribe<VideoWebPlayer.Maui.Services.Events.FavoritesChangedEvent>(OnFavoritesChanged);
                _eventSubscriber.Subscribe<VideoWebPlayer.Maui.Services.Events.NewVideosScannedEvent>(OnNewVideosScanned);
                _eventSubscriber.Subscribe<VideoWebPlayer.Maui.Services.Events.DownloadCompletedEvent>(OnDownloadCompleted);
                _eventSubscriber.Subscribe<VideoWebPlayer.Maui.Services.Events.DownloadDeletedEvent>(OnDownloadDeleted);
            }
        }

        private async Task ShowHomeAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var hc = this.FindByName<Microsoft.Maui.Controls.View>("HomeChrome");
                    if (hc != null)
                    {
                        hc.IsVisible = true;
                    }

                    // Ensure view model is refreshed and UI updated
                    if (_viewModel != null)
                    {
                        await _viewModel.RefreshDataAsync();
                    }
                    // If Shell is used, navigate to a real HomePage instance so the UI is rendered correctly.
                    try
                    {
                        if (Shell.Current != null)
                        {
                            // Navigate to the registered Shell route for HomePage
                            await Shell.Current.GoToAsync("//home");
                            return;
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPage] ShowHomeAsync error: {ex.Message}");
                }
            });
        }

        // UI event handlers referenced from MainPage.xaml
        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                var settings = App.ServiceProvider?.GetService<Services.ISettingsService>();
                var page = App.ServiceProvider?.GetService<SettingsPage>() ?? new SettingsPage(settings);
                await Navigation.PushAsync(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Settings open error: {ex.Message}");
            }
        }

        private async void OnHamburgerClicked(object sender, EventArgs e)
        {
            try
            {
                if (Shell.Current != null && Shell.Current.FlyoutBehavior != FlyoutBehavior.Disabled)
                {
                    Shell.Current.FlyoutIsPresented = true;
                    return;
                }

                var selection = await DisplayActionSheet("Menü", "Abbrechen", null, "Einstellungen", "Quellenübersicht");
                if (selection == "Einstellungen")
                {
                    var settings = App.ServiceProvider?.GetService<Services.ISettingsService>();
                    var page = App.ServiceProvider?.GetService<SettingsPage>() ?? new SettingsPage(settings);
                    await Navigation.PushAsync(page);
                }
                else if (selection == "Quellenübersicht")
                {
                    var page = App.ServiceProvider?.GetService<SourceOverviewPage>() ?? new SourceOverviewPage(0, "Quellen");
                    await Navigation.PushAsync(page);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Hamburger error: {ex.Message}");
            }
        }

        private async void OnSourceTapped(object sender, TappedEventArgs e)
        {
            if (sender is Border border && border.BindingContext is MediaSourceViewModel source)
            {
                var sourcePage = new SourceOverviewPage(source.Id, source.Name ?? "Quelle");
                await Navigation.PushAsync(sourcePage);
            }
        }

        private async void OnReconnectClicked(object sender, EventArgs e)
        {
            try
            {
                // Use ConnectionService workflow to attempt full connection + login if possible
                var state = await _connection.StartConnectionWorkflowAsync(_viewModel, _services);

                var nav = this.Navigation;

                if (state == ConnectionState.NeedsServerSetup)
                {
                    var settingsPage = _services.GetService(typeof(ServerSetupPage)) as ServerSetupPage ?? new ServerSetupPage(_settings);
                    await nav.PushModalAsync(new NavigationPage(settingsPage));
                    return;
                }

                if (state == ConnectionState.NeedsLogin)
                {
                    var loginPage = _services.GetService(typeof(LoginPage)) as LoginPage ?? new LoginPage(_auth);
                    await nav.PushModalAsync(new NavigationPage(loginPage));
                    return;
                }

                if (state == ConnectionState.Offline)
                {
                    await DisplayAlert("Verbindung fehlgeschlagen", "Der Server ist nicht erreichbar. Bitte überprüfen Sie Ihre Einstellungen oder starten Sie die App im Offline-Modus.", "OK");
                    return;
                }

                // Connected
                (Application.Current as App)?.InitializeAfterStartupWorkflowOnline(_services);
                try
                {
                    await ShowHomeAsync();
                }
                catch { }
                await DisplayAlert("Verbunden", "Die Verbindung zum Server wurde wiederhergestellt.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Fehler", $"Fehler beim Verbindungsversuch: {ex.Message}", "OK");
            }
        }

        // Notification event handlers
        private async void OnContinueWatchingUpdated(ContinueWatchingUpdatedEvent e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await _viewModel.RefreshDataAsync(); } catch { }
            });
        }

        private async void OnFavoritesChanged(FavoritesChangedEvent e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await _viewModel.RefreshFavoritesAsync(); } catch { }
            });
        }

        private async void OnNewVideosScanned(NewVideosScannedEvent e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await _viewModel.RefreshDataAsync(); } catch { }
            });
        }

        private async void OnDownloadCompleted(DownloadCompletedEvent e)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var mediaItem = new MediaItemViewModel
                    {
                        Title = e.Download.Title,
                        ImageSource = !string.IsNullOrEmpty(e.Download.LocalPosterImagePath) && System.IO.File.Exists(e.Download.LocalPosterImagePath)
                            ? ImageSource.FromFile(e.Download.LocalPosterImagePath)
                            : ImageSource.FromFile("dotnet_bot.png"),
                        EntryId = e.Download.VideoId,
                        MediaType = e.Download.VideoType.Equals(Models.MediaTypes.Movie, StringComparison.OrdinalIgnoreCase)
                            ? Models.MediaTypes.Movie
                            : Models.MediaTypes.Episode
                    };
                    _viewModel.Downloads.Items.Insert(0, mediaItem);
                }
                catch { }
            });
        }

        private async void OnDownloadDeleted(DownloadDeletedEvent e)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var itemToRemove = _viewModel.Downloads.Items.FirstOrDefault(item =>
                        item.EntryId == e.VideoId && item.MediaType == (e.VideoType.Equals(Models.MediaTypes.Movie, StringComparison.OrdinalIgnoreCase) ? Models.MediaTypes.Movie : Models.MediaTypes.Episode));
                    if (itemToRemove != null) _viewModel.Downloads.Items.Remove(itemToRemove);
                }
                catch { }
            });
        }

        /// <summary>
        /// Trigger the startup workflow again (used after server settings changed).
        /// </summary>
        public void RetryStartupWorkflow()
        {
            // Allow retry even if _started was set previously (e.g. user cancelled earlier).
            _started = false;
            _ = RunStartupWorkflowAsync();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!_started)
            {
                _started = true;
                _ = RunStartupWorkflowAsync();
            }

            // Always attempt to load data for the Home view model (it will handle offline/no-client)
            _ = _viewModel.LoadDataAsync();
        }

        private async Task RunStartupWorkflowAsync()
        {
            var nav = this.Navigation;

            try
            {
                // Use the ConnectionService to run the connection workflow and update the ViewModel flags
                var state = await _connection.StartConnectionWorkflowAsync(_viewModel, _services);

                if (state == ConnectionState.NeedsServerSetup)
                {
                    var settingsPage = _services.GetService(typeof(ServerSetupPage)) as ServerSetupPage ?? new ServerSetupPage(_settings);
                    await nav.PushModalAsync(new NavigationPage(settingsPage));
                    // allow user to correct settings and retry
                    return;
                }

                if (state == ConnectionState.NeedsLogin)
                {
                    var loginPage = _services.GetService(typeof(LoginPage)) as LoginPage ?? new LoginPage(_auth);
                    await nav.PushModalAsync(new NavigationPage(loginPage));
                    // After login, higher level should trigger retry if needed
                    return;
                }

                if (state == ConnectionState.Offline)
                {
                    // show offline home
                    var offlineHome = _services.GetService(typeof(HomePage)) as HomePage ?? new HomePage();
                    if (offlineHome.BindingContext is HomePageViewModel homeViewModel)
                    {
                        homeViewModel.SetOfflineMode(true);
                    }

                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new NavigationPage(offlineHome);
                    }
                    return;
                }

                // Connected -> initialize online services
                (Application.Current as App)?.InitializeAfterStartupWorkflowOnline(_services);

                // success -> show the Home UI inside this MainPage
                try
                {
                    await ShowHomeAsync();
                }
                catch { }
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
