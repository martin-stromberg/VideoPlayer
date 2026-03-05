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

		PlaybackCacheDaysEntry.Text = _settings.PlaybackCacheRetentionDays.ToString();
		WatchlistCacheDaysEntry.Text = _settings.WatchlistCacheRetentionDays.ToString();
		DownloadDaysEntry.Text = _settings.DownloadRetentionDays.ToString();
	}

	private async void OnBackClicked(object? sender, EventArgs e)
	{
		if (Navigation.NavigationStack.Count > 1)
			await Navigation.PopAsync();
	}

	private void OnSaveClicked(object? sender, EventArgs e)
	{
		static bool TryParseDays(string? text, out int days)
		{
			days = 0;
			if (string.IsNullOrWhiteSpace(text))
				return false;
			return int.TryParse(text.Trim(), out days);
		}

		if (!TryParseDays(PlaybackCacheDaysEntry.Text, out var playbackDays) ||
			!TryParseDays(WatchlistCacheDaysEntry.Text, out var watchlistDays) ||
			!TryParseDays(DownloadDaysEntry.Text, out var downloadDays))
		{
			StatusLabel.Text = "Bitte gültige Zahlen (Tage) eingeben.";
			return;
		}

		_settings.PlaybackCacheRetentionDays = playbackDays;
		_settings.WatchlistCacheRetentionDays = watchlistDays;
		_settings.DownloadRetentionDays = downloadDays;

		StatusLabel.Text = "Gespeichert.";
	}
}
