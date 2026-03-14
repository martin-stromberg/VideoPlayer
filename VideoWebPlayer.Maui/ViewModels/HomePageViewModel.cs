using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Maui.Services;

namespace VideoWebPlayer.Maui.ViewModels;

public class HomePageViewModel : INotifyPropertyChanged
{
    private readonly VideoWebPlayerClient? _client;
    private bool _isLoading;
    private bool _isLoaded = false;
    private bool _isOfflineMode = false;

    public MediaCarouselViewModel ContinueWatching { get; }
    public MediaCarouselViewModel Favorites { get; }
    public MediaCarouselViewModel RecentEntries { get; }
    public MediaCarouselViewModel Downloads { get; }
    public ObservableCollection<MediaSourceViewModel> Sources { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        set
        {
            if (_isOfflineMode != value)
            {
                _isOfflineMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOnline));
            }
        }
    }

    public bool IsOnline => !IsOfflineMode;

    public HomePageViewModel()
    {
        ContinueWatching = new MediaCarouselViewModel { Title = "Weiterschauen" };
        Favorites = new MediaCarouselViewModel { Title = "Favoriten" };
        RecentEntries = new MediaCarouselViewModel { Title = "Neu im Programm" };
        Downloads = new MediaCarouselViewModel { Title = "Heruntergeladene Videos" };

        _client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
    }

    /// <summary>
    /// Lädt gecachte Einträge für alle Karussells aus der Datenbank und zeigt sie sofort an.
    /// </summary>
    private async Task LoadFromCacheAsync()
    {
        await Task.WhenAll(
            PopulateCarouselFromCacheAsync(ContinueWatching, "ContinueWatching"),
            PopulateCarouselFromCacheAsync(Favorites, "Favorites"),
            PopulateCarouselFromCacheAsync(RecentEntries, "RecentEntries")
        );
    }

    private async Task PopulateCarouselFromCacheAsync(MediaCarouselViewModel carousel, string carouselName)
    {
        try
        {
            var cached = await ElementCacheService.Instance.GetCachedItemsAsync(carouselName);
            if (cached.Count == 0) return;

            var toAdd = cached.Select(c => new MediaItemViewModel
            {
                Title = c.Title,
                ImageUrl = c.ImageUrl,
                ImageSource = "placeholder.png",
                EntryId = c.EntryId,
                MediaType = c.MediaType,
                SeasonId = c.SeasonId,
                EpisodeId = c.EpisodeId,
                PosterPictureId = c.PosterPictureId,
                IsFromCache = true
            }).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                for (int i = 0; i < toAdd.Count; i++)
                    carousel.AddItem(toAdd[i], i);
            });

            // Lade Bilder im Hintergrund nach
            if (_client != null)
            {
                foreach (var item in toAdd.Where(i => i.PosterPictureId.HasValue && i.PosterPictureId > 0))
                    _ = LoadImageAsync(item.PosterPictureId!.Value, item);
            }

            System.Diagnostics.Debug.WriteLine($"[HomePageViewModel] Loaded {toAdd.Count} cached items for '{carouselName}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePageViewModel] Cache load error for '{carouselName}': {ex.Message}");
        }
    }

    public async Task LoadDataAsync()
    {
        System.Diagnostics.Debug.WriteLine("LoadDataAsync started");
        
        // Lade Downloads immer (auch ohne Server)
        await LoadDownloadsAsync();
        
        System.Diagnostics.Debug.WriteLine($"Downloads loaded. Count: {Downloads.Items.Count}");
        
        if (_client == null)
        {
            System.Diagnostics.Debug.WriteLine("No client or base address - switching to offline mode");
            IsOfflineMode = true;
            return;
        }

        // Lade nur beim ersten Aufruf, nicht bei jedem OnAppearing
        if (_isLoaded)
        {
            System.Diagnostics.Debug.WriteLine("Already loaded - skipping");
            return;
        }

        // Zeige gecachte Einträge sofort an, bevor der Server antwortet
        await LoadFromCacheAsync();

        IsLoading = true;

        try
        {
            // Beginne Aktualisierung der Karussells (nicht mehr vorhandene Elemente werden durch EndUpdate entfernt)
            ContinueWatching.BeginUpdate();
            Favorites.BeginUpdate();
            RecentEntries.BeginUpdate();
            Sources.Clear();
            
            await Task.WhenAll(
                LoadContinueWatchingAsync(),
                LoadFavoritesAsync(),
                LoadRecentEntriesAsync(),
                LoadSourcesAsync()
            );
            
            _isLoaded = true;
            IsOfflineMode = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            IsOfflineMode = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RefreshDownloadsAsync()
    {
        // Lade nur Downloads neu, ohne die ganze Seite zu reloaden
        await LoadDownloadsAsync();
    }

    public async Task RefreshDataAsync()
    {
        _isLoaded = false;
        await LoadDataAsync();
    }

	public async Task RefreshFavoritesAsync()
	{
		Favorites.BeginUpdate();
		await LoadFavoritesAsync();
	}
    
    public void RefreshData()
    {
        _isLoaded = false;
    }

    public void SetOfflineMode(bool isOffline)
    {
        IsOfflineMode = isOffline;
    }

    public async Task<bool> TryReconnectAsync()
    {
        if (_client == null)
            return false;

        IsLoading = true;
        
        try
        {
            // Versuche einen einfachen Health Check
            var connected = await _client.HealthCheckAsync();
            
            if (connected)
            {
                // Verbindung erfolgreich - lade Daten
                IsOfflineMode = false;
                _isLoaded = false; // Reset, damit Daten neu geladen werden
                await LoadDataAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reconnect failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
        
        return false;
    }

    private async Task LoadContinueWatchingAsync()
    {
        if (_client == null) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => ContinueWatching.IsLoading = true);

            var items = await _client.RequestContinueWatchingAsync().ConfigureAwait(false);

            var toAdd = new List<MediaItemViewModel>();
            var seen = new HashSet<string>();
            foreach (var item in items.Take(10))
            {
                if (item?.Entry == null) continue;

                // Build a unique key for the entry to avoid duplicates (mediaType|targetId|season|episode)
                var (targetId, mediaType, _, title, seasonId, episodeId) = GetEntryDetails(item.Entry, item.PosterPictureId);
                var key = $"{mediaType ?? ""}|{targetId}|{seasonId?.ToString() ?? "0"}|{episodeId?.ToString() ?? "0"}";
                if (!seen.Add(key))
                {
                    // duplicate entry, skip
                    System.Diagnostics.Debug.WriteLine($"Skipping duplicate continue-watching entry: {key} ({item?.Title ?? title})");
                    continue;
                }

                var mediaItem = CreateMediaItemViewModel(item);
                toAdd.Add(mediaItem);

                if (item.PosterPictureId.HasValue && item.PosterPictureId > 0)
                {
                    _ = LoadImageAsync(item.PosterPictureId.Value, mediaItem);
                }
            }

            List<MediaItemViewModel> continueWatchingSnapshot = null!;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                for (int idx = 0; idx < toAdd.Count; idx++)
                    ContinueWatching.AddItem(toAdd[idx], idx);
                ContinueWatching.EndUpdate();
                continueWatchingSnapshot = ContinueWatching.Items.ToList();
            });

            _ = Task.Run(() => ElementCacheService.Instance.SaveCachedItemsAsync("ContinueWatching", continueWatchingSnapshot));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading continue watching: {ex.Message}");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => ContinueWatching.IsLoading = false);
        }
    }

    private MediaItemViewModel CreateMediaItemViewModel(ContinueWatchingDto item)
    {
        var (targetId, mediaType, posterUrl, title, seasonId, episodeId) = GetEntryDetails(item.Entry, item.PosterPictureId);
        var mediaItem = CreateMediaItemViewModel(item.Title ?? title, posterUrl, targetId, mediaType, seasonId, episodeId);

        if (item.PosterPictureId.HasValue && item.PosterPictureId > 0)
        {
            _ = LoadImageAsync(item.PosterPictureId.Value, mediaItem);
        }

        return mediaItem;
    }

    private async Task LoadFavoritesAsync()
    {
        if (_client == null) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => Favorites.IsLoading = true);

            var items = await _client.RequestFavoritesAsync().ConfigureAwait(false);
            var toAdd = new List<MediaItemViewModel>();

            foreach (var item in items.Take(10))
            {
                if (item?.Entry == null) continue;
                var mediaItem = CreateMediaItemViewModel(item);
                toAdd.Add(mediaItem);
            }

            List<MediaItemViewModel> favoritesSnapshot = null!;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                for (int idx = 0; idx < toAdd.Count; idx++)
                    Favorites.AddItem(toAdd[idx], idx);
                Favorites.EndUpdate();
                favoritesSnapshot = Favorites.Items.ToList();
            });

            _ = Task.Run(() => ElementCacheService.Instance.SaveCachedItemsAsync("Favorites", favoritesSnapshot));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => Favorites.IsLoading = false);
        }
    }

    private MediaItemViewModel CreateMediaItemViewModel(Data.DtoFavoriteEntry item)
    {
        var (targetId, mediaType, posterUrl, title, seasonId, episodeId) = GetEntryDetails(item.Entry, item.Entry.PosterPictureId);
        var mediaItem = CreateMediaItemViewModel(title, posterUrl, targetId, mediaType, seasonId, episodeId);

        if (item.Entry.PosterPictureId.HasValue && item.Entry.PosterPictureId > 0)
        {
            _ = LoadImageAsync(item.Entry.PosterPictureId.Value, mediaItem);
        }

        return mediaItem;
    }

    private async Task LoadRecentEntriesAsync()
    {
        if (_client == null) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => RecentEntries.IsLoading = true);

            var items = await _client.RequestRecentEntriesAsync().ConfigureAwait(false);
            var toAdd = new List<MediaItemViewModel>();

            foreach (var item in items.Take(10))
            {
                if (item?.Entry == null) continue;
                var mediaItem = CreateMediaItemViewModel(item);
                toAdd.Add(mediaItem);
            }

            List<MediaItemViewModel> recentEntriesSnapshot = null!;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                for (int idx = 0; idx < toAdd.Count; idx++)
                    RecentEntries.AddItem(toAdd[idx], idx);
                RecentEntries.EndUpdate();
                recentEntriesSnapshot = RecentEntries.Items.ToList();
            });

            _ = Task.Run(() => ElementCacheService.Instance.SaveCachedItemsAsync("RecentEntries", recentEntriesSnapshot));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading recent entries: {ex.Message}");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => RecentEntries.IsLoading = false);
        }
    }
    /// <summary>
    /// Erstellt ein MediaItemViewModel basierend auf den Details eines DtoRecentEntry-Objekts.
    /// </summary>
    /// <remarks>Falls die Poster-URL nicht null oder leer ist, wird ein asynchroner Bildladevorgang für das MediaItemViewModel gestartet.</remarks>
    /// <param name="item">Die Daten des aktuellen Eintrags, die verwendet werden, um die Details für das MediaItemViewModel zu extrahieren.</param>
    /// <returns>Eine MediaItemViewModel-Instanz, die mit den aus dem bereitgestellten aktuellen Eintrag extrahierten Details gefüllt ist.</returns>
    private MediaItemViewModel CreateMediaItemViewModel(DtoRecentEntry item)
    {
        var (targetId, mediaType, posterUrl, title, seasonId, episodeId) = GetEntryDetails(item.Entry, item.Entry.PosterPictureId);
        var mediaItem = CreateMediaItemViewModel(title, posterUrl, targetId, mediaType, seasonId, episodeId);

        if (item.Entry.PosterPictureId.HasValue && item.Entry.PosterPictureId > 0)
        {
            _ = LoadImageAsync(item.Entry.PosterPictureId.Value, mediaItem);
        }
        return mediaItem;
    }
    /// <summary>
	/// Erstellt ein MediaItemViewModel mit den gegebenen Parametern.
	/// </summary>
	private MediaItemViewModel CreateMediaItemViewModel(string title, string? imageUrl, long entryId, string? mediaType, long? seasonId = null, long? episodeId = null)
    {
        return new MediaItemViewModel
        {
            Title = title,
            ImageUrl = imageUrl,
            ImageSource = "placeholder.png",
            EntryId = entryId,
            MediaType = mediaType,
            SeasonId = seasonId,
            EpisodeId = episodeId
        };
    }

    private async Task LoadDownloadsAsync()
    {
        try
        {
            Downloads.IsLoading = true;

            // Clear and load on background, update UI in a single batch to avoid UI thread stalls
            var downloadedVideos = await Services.DownloadManager.Instance.GetAllDownloadsAsync().ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Found {downloadedVideos.Count} downloaded videos in database");

            var toAdd = new List<MediaItemViewModel>();
            var imageLoadTasks = new List<Task>();

            foreach (var download in downloadedVideos.Take(10))
            {
                System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Preparing download: {download.Title} ({download.VideoType}) - VideoId: {download.VideoId}");

                var mediaItem = new MediaItemViewModel
                {
                    Title = download.Title,
                    ImageSource = ImageSource.FromFile("dotnet_bot.png"),
                    EntryId = download.VideoId,
                    MediaType = download.VideoType.Equals(Models.MediaTypes.Movie, StringComparison.OrdinalIgnoreCase) ? MediaTypes.Movie : MediaTypes.Episode
                };

                toAdd.Add(mediaItem);

                // schedule local image load without blocking UI thread
                if (!string.IsNullOrEmpty(download.LocalPosterImagePath) && File.Exists(download.LocalPosterImagePath))
                {
                    imageLoadTasks.Add(LoadLocalImageAsync(download.LocalPosterImagePath, mediaItem));
                }
                else if (!string.IsNullOrEmpty(download.LocalBannerImagePath) && File.Exists(download.LocalBannerImagePath))
                {
                    imageLoadTasks.Add(LoadLocalImageAsync(download.LocalBannerImagePath, mediaItem));
                }
            }

            // Update UI in one batch
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Downloads.Items.Clear();
                foreach (var mi in toAdd)
                    Downloads.Items.Add(mi);
                System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Added {toAdd.Count} downloads to UI");
            });

            // Run image loads in background (they will update UI per-item when done)
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(imageLoadTasks);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Image load tasks error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Error loading downloads: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Stack trace: {ex.StackTrace}");
        }
        finally
        {
            Downloads.IsLoading = false;
        }
    }
    
    private async Task LoadLocalImageAsync(string imagePath, MediaItemViewModel mediaItem)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var imageSource = new StreamImageSource
            {
                Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
            };
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                mediaItem.ImageSource = imageSource;
            });
            
            System.Diagnostics.Debug.WriteLine($"[LoadLocalImageAsync] Loaded image for: {mediaItem.Title}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadLocalImageAsync] Error loading local image: {ex.Message}");
        }
    }

    private async Task LoadSourcesAsync()
    {
        if (_client == null) return;

        try
        {
            var sources = await _client.RequestSourcesAsync().ConfigureAwait(false);

            if (sources != null)
            {
                var toAdd = new List<MediaSourceViewModel>();
                foreach (var source in sources)
                {
                    var sourceItem = new MediaSourceViewModel
                    {
                        Id = source.Id,
                        Name = source.Name,
                        Icon = "📁",
                        IconPictureId = source.IconPictureId
                    };
                    toAdd.Add(sourceItem);

                    if (sourceItem.IconPictureId.HasValue)
                    {
                        _ = LoadSourceIconAsync(sourceItem.IconPictureId.Value, sourceItem);
                    }
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var s in toAdd)
                        Sources.Add(s);
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading sources: {ex.Message}");
        }
    }

	private async Task LoadSourceIconAsync(long pictureId, MediaSourceViewModel source)
	{
		try
		{
			if (pictureId <= 0 || _client is null)
				return;

			var imageBytes = await _client.GetSourcePictureAsync(pictureId);
			var imageSource = new StreamImageSource
			{
				Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
			};

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				source.IconImageSource = imageSource;
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading source icon {pictureId}: {ex.Message}");
		}
	}

	/// <summary>
	/// Bestimmt das Navigationsziel und den MediaType für einen Entry.
	/// Für Episoden und Seasons wird zur Show navigiert, für Movies zur Collection.
	/// </summary>
	private (long targetId, string? mediaType, string? posterUrl, string title, long? seasonId, long? episodeId) GetEntryDetails(object entry, long? pictureId)
	{
		string? mediaType = null;
		long targetId = 0;
		string title = string.Empty;
		long? seasonId = null;
		long? episodeId = null;
		var posterUrl = pictureId.HasValue ? $"api/pictures/{pictureId}" : null;

		if (entry is DtoTVShow show)
		{
			mediaType = "show";
			targetId = show.Id;
			title = show.Name;
		}
		else if (entry is DtoMovieCollection collection)
		{
			mediaType = "collection";
			targetId = collection.Id;
			title = collection.Name;
		}
		else if (entry is DtoTVShowSeason season)
		{
			if (season.Show != null)
			{
				mediaType = "show";
				targetId = season.Show.Id;
				title = season.Show.Name;
				seasonId = season.Id;
				// Verwende Show-Poster nur als Fallback, wenn die Staffel kein eigenes Poster hat
				if (!posterUrl?.Contains("pictures") == true && season.Show.PosterPictureId.HasValue)
				{
					posterUrl = $"api/pictures/{season.Show.PosterPictureId}";
				}
			}
		}
		else if (entry is DtoTVShowEpisode episode)
		{
			if (episode.Season?.Show != null)
			{
				mediaType = "show";
				targetId = episode.Season.Show.Id;
				title = episode.Season.Show.Name;
				seasonId = episode.Season.Id;
				episodeId = episode.Id;
				// Verwende Show-Poster nur als Fallback, wenn die Episode kein eigenes Poster hat
				if (!posterUrl?.Contains("pictures") == true && episode.Season.Show.PosterPictureId.HasValue)
				{
					posterUrl = $"api/pictures/{episode.Season.Show.PosterPictureId}";
				}
			}
		}
		else if (entry is DtoMovie movie)
		{
			if (movie.Collection != null)
			{
				mediaType = "collection";
				targetId = movie.Collection.Id;
				title = movie.Collection.Name;
				if (movie.Collection.PosterPictureId.HasValue)
				{
					posterUrl = $"api/pictures/{movie.Collection.PosterPictureId}";
				}
			}
			else
			{
				mediaType = "movie";
				targetId = movie.Id;
				title = movie.Name;
			}
		}

		return (targetId, mediaType, posterUrl, title, seasonId, episodeId);
	}

	

	private async Task LoadImageAsync(long pictureId, MediaItemViewModel mediaItem)
    {
        try
        {
            if (pictureId <= 0 || _client is null)
                return;

            var imageBytes = await _client.GetPictureAsync(pictureId);
            var stream = new MemoryStream(imageBytes);
            var imageSource = new StreamImageSource
            {
                Stream = (token) => Task.FromResult((Stream)stream)
            };

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                mediaItem.ImageSource = imageSource;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image {pictureId}: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
