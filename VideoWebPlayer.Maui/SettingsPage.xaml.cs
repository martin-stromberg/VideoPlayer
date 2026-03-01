using Microsoft.Maui.Controls;
using VideoWebPlayer.Maui.Services;

namespace VideoWebPlayer.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly ISettingsService _settings;

    public SettingsPage(ISettingsService? settings)
    {
        InitializeComponent();
        _settings = settings ?? new Services.SettingsService();

        if (!string.IsNullOrWhiteSpace(_settings.ServerAddress))
        {
            ServerEntry.Text = _settings.ServerAddress;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var addr = ServerEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(addr))
        {
            StatusLabel.Text = "Bitte eine gültige Adresse eingeben.";
            return;
        }
        _settings.SetServerAddress(addr!);
        // close modal
        if (Navigation.ModalStack.Count > 0)
        {
            await Navigation.PopModalAsync();
        }
    }

    private async void OnDiscoverClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = "Suche im Netzwerk (mDNS)...";
        ServerEntry.Text = string.Empty;
        var results = new List<string>();
        try
        {
            var mdns = await _settings.DiscoverServersAsync(2500);
            results.AddRange(mdns);
            StatusLabel.Text = $"mDNS: {mdns.Count} gefunden.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "mDNS-Fehler: " + ex.Message;
        }

        StatusLabel.Text += " Suche per UDP...";
        try
        {
            var udp = await _settings.DiscoverServersUdpAsync(5001, 2000);
            results.AddRange(udp);
            StatusLabel.Text += $" UDP: {udp.Count} gefunden.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text += " UDP-Fehler: " + ex.Message;
        }

        if (results.Count == 0)
        {
            StatusLabel.Text += " Kein Server gefunden.";
            return;
        }

        // Zeige Auswahl-Dialog
        string? selected = null;
        if (results.Count == 1)
        {
            selected = results[0];
        }
        else
        {
            selected = await DisplayActionSheetAsync("Server auswählen", "Abbrechen", null, results.ToArray());
            if (selected == "Abbrechen" || string.IsNullOrWhiteSpace(selected))
                return;
        }
        ServerEntry.Text = selected;
        StatusLabel.Text = $"Vorschlag: {selected}";
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        // Exit the application - user chose to cancel startup
        System.Environment.Exit(0);
    }
}
