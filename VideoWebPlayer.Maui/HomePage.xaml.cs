using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services.Events;

namespace VideoWebPlayer.Maui;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _viewModel;
    private readonly ISubscribeNotificationEvent? _eventSubscriber;

    public HomePage()
    {
        InitializeComponent();
        _viewModel = new HomePageViewModel();
        BindingContext = _viewModel;

        // Subscribe to notification events
        _eventSubscriber = App.ServiceProvider?.GetService<ISubscribeNotificationEvent>();
        if (_eventSubscriber != null)
        {
            _eventSubscriber.Subscribe<ContinueWatchingUpdatedEvent>(OnContinueWatchingUpdated);
            _eventSubscriber.Subscribe<FavoritesChangedEvent>(OnFavoritesChanged);
            _eventSubscriber.Subscribe<NewVideosScannedEvent>(OnNewVideosScanned);
            _eventSubscriber.Subscribe<DownloadCompletedEvent>(OnDownloadCompleted);
            _eventSubscriber.Subscribe<DownloadDeletedEvent>(OnDownloadDeleted);

            System.Diagnostics.Debug.WriteLine("[HomePage] Subscribed to notification events");
        }

        // Platform-specific UI adjustments are applied in OnAppearing because Shell.Current
        // may not be available yet in the constructor.
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Ensure Shell nav bar is visible so the flyout (hamburger) is shown on iOS/Android
        try
        {
            if (Shell.Current != null)
            {
                // Show the shell navigation bar for this page
                Shell.SetNavBarIsVisible(this, true);

                // Ensure a flyout icon exists so iOS shows the hamburger in the nav bar
                if (Shell.Current.FlyoutIcon == null)
                {
                    // use a bundled icon if available
                    try { Shell.Current.FlyoutIcon = "dotnet_bot.png"; } catch { }
                }
            }
        }
        catch { }

        // Adjust UI for platform: show hamburger on mobile
        try
        {
            var hb = this.FindByName<Button>("HamburgerButton");
            var sb = this.FindByName<Button>("SettingsButton");
            if (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS)
            {
                hb?.SetValue(VisualElement.IsVisibleProperty, true);
                sb?.SetValue(VisualElement.IsVisibleProperty, false);
            }
            else
            {
                hb?.SetValue(VisualElement.IsVisibleProperty, false);
                sb?.SetValue(VisualElement.IsVisibleProperty, true);
            }

            // If we are hosted inside Shell with a flyout, hide the page-level buttons
            try
            {
                if (Shell.Current != null && Shell.Current.FlyoutBehavior != FlyoutBehavior.Disabled)
                {
                    hb?.SetValue(VisualElement.IsVisibleProperty, false);
                    sb?.SetValue(VisualElement.IsVisibleProperty, false);
                }
            }
            catch { }
        }
        catch { }

        try
        {
            // Debug: Zeige Anzahl der Downloads in DB
            var allDownloads = await Services.DownloadManager.Instance.GetAllDownloadsAsync();
            System.Diagnostics.Debug.WriteLine($"[HomePage] Database has {allDownloads.Count} completed downloads");
            
            foreach (var download in allDownloads)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] - {download.Title} ({download.VideoType}) - Status: {download.Status}");
            }

            // Lade Downloads immer (unabhängig vom Modus)
            // Andere Daten nur wenn Online
            await _viewModel.LoadDataAsync();
            
            // Debug: Zeige Anzahl der Items im ViewModel
            System.Diagnostics.Debug.WriteLine($"[HomePage] ViewModel Downloads.Items.Count = {_viewModel.Downloads.Items.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Error in OnAppearing: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[HomePage] Stack trace: {ex.StackTrace}");
            await DisplayAlert("Fehler", $"Daten konnten nicht geladen werden: {ex.Message}", "OK");
        }
    }

    private async void OnReconnectClicked(object sender, EventArgs e)
    {
        try
        {
            var success = await _viewModel.TryReconnectAsync();
            
            if (success)
            {
                await DisplayAlert("Verbindung wiederhergestellt", "Die Verbindung zum Server wurde erfolgreich wiederhergestellt.", "OK");
            }
            else
            {
                await DisplayAlert("Verbindung fehlgeschlagen", "Die Verbindung zum Server konnte nicht hergestellt werden. Bitte überprüfen Sie Ihre Internetverbindung und die Server-Einstellungen.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Fehler beim Verbindungsversuch: {ex.Message}", "OK");
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
			System.Diagnostics.Debug.WriteLine($"[HomePage] Error opening settings: {ex.Message}");
		}
	}
    
    private async void OnContinueWatchingUpdated(ContinueWatchingUpdatedEvent e)
    {
        System.Diagnostics.Debug.WriteLine("[HomePage] Event: Continue-Watching updated - refreshing list");
        
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await _viewModel.RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error refreshing continue watching: {ex.Message}");
            }
        });
    }
    
    private async void OnFavoritesChanged(FavoritesChangedEvent e)
    {
        System.Diagnostics.Debug.WriteLine("[HomePage] Event: Favorites changed - refreshing list");
        
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
				await _viewModel.RefreshFavoritesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error refreshing favorites: {ex.Message}");
            }
        });
    }
    
    private async void OnNewVideosScanned(NewVideosScannedEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[HomePage] Event: New videos scanned (Source {e.SourceId}, Count {e.Count}) - refreshing recent entries");
        
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await _viewModel.RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error refreshing recent entries: {ex.Message}");
            }
        });
    }

    private async void OnDownloadCompleted(DownloadCompletedEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[HomePage] Event: Download completed - {e.Download.Title}");
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                // Füge das neue Download zur Liste hinzu
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
                
                _viewModel.Downloads.Items.Insert(0, mediaItem); // Füge am Anfang ein
                System.Diagnostics.Debug.WriteLine($"[HomePage] Added new download to list: {e.Download.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error adding download: {ex.Message}");
            }
        });
    }

    private async void OnDownloadDeleted(DownloadDeletedEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[HomePage] Event: Download deleted - {e.Title}");
        
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                // Finde und entferne das Item aus der Downloads-Collection
                var itemToRemove = _viewModel.Downloads.Items.FirstOrDefault(item =>
                    item.EntryId == e.VideoId &&
                    item.MediaType == (e.VideoType.Equals(Models.MediaTypes.Movie, StringComparison.OrdinalIgnoreCase)
                        ? Models.MediaTypes.Movie
                        : Models.MediaTypes.Episode));

                if (itemToRemove != null)
                {
                    _viewModel.Downloads.Items.Remove(itemToRemove);
                    System.Diagnostics.Debug.WriteLine($"[HomePage] Removed download from list: {e.Title}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error removing download: {ex.Message}");
            }
        });
    }

    private void OnHamburgerClicked(object sender, EventArgs e)
    {

    }
}
