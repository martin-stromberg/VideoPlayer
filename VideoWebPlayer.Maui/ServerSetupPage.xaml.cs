using Microsoft.Maui.Controls;
using VideoWebPlayer.Maui.Services;

namespace VideoWebPlayer.Maui;

public partial class ServerSetupPage : ContentPage
{
	private readonly ISettingsService _settings;
	private readonly Entry _serverEntry;
	private readonly Label _statusLabel;

	public ServerSetupPage(ISettingsService? settings)
	{
		_settings = settings ?? new Services.SettingsService();

		Title = "Server Einstellungen";

		_serverEntry = new Entry { Placeholder = "http://192.168.1.100:5000" };
		_statusLabel = new Label { TextColor = Colors.Gray };

		if (!string.IsNullOrWhiteSpace(_settings.ServerAddress))
			_serverEntry.Text = _settings.ServerAddress;

		Content = new VerticalStackLayout
		{
			Padding = 20,
			Spacing = 15,
			Children =
			{
				new Label { Text = "Bitte geben Sie die Serveradresse ein:" },
				_serverEntry,
				new HorizontalStackLayout
				{
					Spacing = 10,
					Children =
					{
						new Button { Text = "Automatisch suchen", Command = new Command(async () => await DiscoverAsync()) },
						new Button { Text = "Verbindung herstellen", Command = new Command(async () => await SaveAsync()) },
						new Button { Text = "Abbrechen", Command = new Command(Cancel) }
					}
				},
				_statusLabel
			}
		};
	}

	private async Task SaveAsync()
	{
		var addr = _serverEntry.Text?.Trim();
		if (string.IsNullOrWhiteSpace(addr))
		{
			_statusLabel.Text = "Bitte eine gültige Adresse eingeben.";
			return;
		}
		_settings.SetServerAddress(addr);
		// close modal
		if (Navigation.ModalStack.Count > 0)
		{
			await Navigation.PopModalAsync();
		}
	}

	private async Task DiscoverAsync()
	{
		_statusLabel.Text = "Suche im Netzwerk (mDNS)...";
		_serverEntry.Text = string.Empty;
		var results = new List<string>();
		try
		{
			var mdns = await _settings.DiscoverServersAsync(2500);
			results.AddRange(mdns);
			_statusLabel.Text = $"mDNS: {mdns.Count} gefunden.";
		}
		catch (Exception ex)
		{
			_statusLabel.Text = "mDNS-Fehler: " + ex.Message;
		}

		_statusLabel.Text += " Suche per UDP...";
		try
		{
			var udp = await _settings.DiscoverServersUdpAsync(5001, 2000);
			results.AddRange(udp);
			_statusLabel.Text += $" UDP: {udp.Count} gefunden.";
		}
		catch (Exception ex)
		{
			_statusLabel.Text += " UDP-Fehler: " + ex.Message;
		}

		if (results.Count == 0)
		{
			_statusLabel.Text += " Kein Server gefunden.";
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
		_serverEntry.Text = selected;
		_statusLabel.Text = $"Vorschlag: {selected}";
	}

	private static void Cancel()
	{
		// Exit the application - user chose to cancel startup
		System.Environment.Exit(0);
	}
}
