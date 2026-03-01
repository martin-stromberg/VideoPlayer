using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Maui.ViewModels;

namespace VideoWebPlayer.Maui;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _viewModel;

    public HomePage()
    {
        InitializeComponent();
        _viewModel = new HomePageViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Lade Daten nur wenn nicht im Offline-Modus
        if (!_viewModel.IsOfflineMode)
        {
            try
            {
                await _viewModel.LoadDataAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Fehler", $"Daten konnten nicht geladen werden: {ex.Message}", "OK");
            }
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
}
