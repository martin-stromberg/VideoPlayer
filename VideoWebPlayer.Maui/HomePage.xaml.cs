using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Maui.ViewModels;

namespace VideoWebPlayer.Maui;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _viewModel;
    private readonly Services.SignalRService? _signalRService;

    public HomePage()
    {
        InitializeComponent();
        _viewModel = new HomePageViewModel();
        BindingContext = _viewModel;
        
        // Registriere SignalR Event-Handler
        _signalRService = App.ServiceProvider?.GetService<Services.SignalRService>();
        if (_signalRService != null)
        {
            _signalRService.ContinueWatchingUpdated += OnContinueWatchingUpdated;
            _signalRService.FavoritesChanged += OnFavoritesChanged;
            _signalRService.NewVideosScanned += OnNewVideosScanned;
            
            System.Diagnostics.Debug.WriteLine("[HomePage] SignalR event handlers registered");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

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
            await DisplayAlertAsync("Fehler", $"Daten konnten nicht geladen werden: {ex.Message}", "OK");
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
    
    private async void OnContinueWatchingUpdated(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[HomePage] SignalR: Continue-Watching updated - refreshing list");
        
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
    
    private async void OnFavoritesChanged(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[HomePage] SignalR: Favorites changed - refreshing list");
        
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await _viewModel.RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error refreshing favorites: {ex.Message}");
            }
        });
    }
    
    private async void OnNewVideosScanned(object? sender, Services.NewVideosScannedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[HomePage] SignalR: New videos scanned (Source {e.SourceId}, Count {e.Count}) - refreshing recent entries");
        
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await _viewModel.RefreshDataAsync();
                
                // Optional: Zeige Toast-Benachrichtigung
                // await DisplayAlert("Neue Videos", $"{e.Count} neue Videos wurden gefunden!", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] Error refreshing recent entries: {ex.Message}");
            }
        });
    }
}
