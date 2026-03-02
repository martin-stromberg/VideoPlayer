using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.ViewModels;

public class HomePageViewModel : INotifyPropertyChanged
{
    private readonly VideoWebPlayerClient? _client;
    private readonly HttpClient _httpClient;
    private readonly string _baseAddress;
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
        
        // Erstelle einen HttpClient mit Bearer Token für Bild-Requests
        _httpClient = new HttpClient();
        var token = _client?.AuthorizationToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        _baseAddress = Preferences.Default.Get("ServerAddress", string.Empty);
        if (!string.IsNullOrWhiteSpace(_baseAddress))
        {
            if (!_baseAddress.StartsWith("http://") && !_baseAddress.StartsWith("https://"))
            {
                _baseAddress = $"http://{_baseAddress}";
            }
            _baseAddress = _baseAddress.TrimEnd('/');
        }
    }

    public async Task LoadDataAsync()
    {
        System.Diagnostics.Debug.WriteLine("LoadDataAsync started");
        
        // Lade Downloads immer (auch ohne Server)
        await LoadDownloadsAsync();
        
        System.Diagnostics.Debug.WriteLine($"Downloads loaded. Count: {Downloads.Items.Count}");
        
        if (_client == null || string.IsNullOrWhiteSpace(_baseAddress))
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

        IsLoading = true;

        try
        {
            // Lösche bestehende Items vor dem Neuladen
            ContinueWatching.Items.Clear();
            Favorites.Items.Clear();
            RecentEntries.Items.Clear();
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

    public async Task RefreshDataAsync()
    {
        _isLoaded = false;
        await LoadDataAsync();
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
        if (_client == null || string.IsNullOrWhiteSpace(_baseAddress))
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
            ContinueWatching.IsLoading = true;
            var items = await _client.RequestContinueWatchingAsync();
            
            foreach (var item in items.Take(10))
            {
                if (item?.Entry == null) continue;
                
                var posterUrl = item.PosterPictureId.HasValue 
                    ? $"{_baseAddress}/api/pictures/{item.PosterPictureId}"
                    : null;

                // Bestimme den MediaType basierend auf dem Entry-Typ
                string? mediaType = null;
                if (item.Entry is DtoTVShowEpisode)
                {
                    mediaType = "episode";
                    // Für Episoden navigieren wir zur Show
                    var episode = item.Entry as DtoTVShowEpisode;
                    if (episode?.Season?.Show != null)
                    {
                        item.Entry = episode.Season.Show;
                        mediaType = "show";
                    }
                }
                else if (item.Entry is DtoMovie movie)
                {
                    mediaType = "movie";
                    // Für Filme navigieren wir zur Collection
                    if (movie.Collection != null)
                    {
                        item.Entry = movie.Collection;
                        mediaType = "collection";
                    }
                }

                var mediaItem = new MediaItemViewModel
                {
                    Title = item.Title ?? item.Entry.Name,
                    ImageUrl = posterUrl,
                    ImageSource = "placeholder.png",
                    EntryId = item.Entry.Id,
                    MediaType = mediaType
                };

                // Lade Bild asynchron
                if (!string.IsNullOrEmpty(posterUrl))
                {
                    _ = LoadImageAsync(posterUrl, mediaItem);
                }

                ContinueWatching.Items.Add(mediaItem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading continue watching: {ex.Message}");
        }
        finally
        {
            ContinueWatching.IsLoading = false;
        }
    }

    private async Task LoadFavoritesAsync()
    {
        if (_client == null) return;
        
        try
        {
            Favorites.IsLoading = true;
            var items = await _client.RequestFavoritesAsync();
            
            foreach (var item in items.Take(10))
            {
                if (item?.Entry == null) continue;
                
                var posterUrl = item.Entry.PosterPictureId.HasValue 
                    ? $"{_baseAddress}/api/pictures/{item.Entry.PosterPictureId}"
                    : null;

                // Bestimme den MediaType und navigiere zum richtigen Entry
                string? mediaType = null;
                var targetEntry = item.Entry;
                
                if (item.Entry is DtoTVShow)
                {
                    mediaType = "show";
                }
                else if (item.Entry is DtoMovieCollection)
                {
                    mediaType = "collection";
                }
                else if (item.Entry is DtoMovie movie)
                {
                    // Für Filme navigieren wir zur Collection
                    if (movie.Collection != null)
                    {
                        targetEntry = movie.Collection;
                        mediaType = "collection";
                        
                        // Verwende Collection Poster wenn vorhanden
                        if (movie.Collection.PosterPictureId.HasValue)
                        {
                            posterUrl = $"{_baseAddress}/api/pictures/{movie.Collection.PosterPictureId}";
                        }
                    }
                    else
                    {
                        mediaType = "movie";
                    }
                }

                var mediaItem = new MediaItemViewModel
                {
                    Title = targetEntry.Name,
                    ImageUrl = posterUrl,
                    ImageSource = "placeholder.png",
                    EntryId = targetEntry.Id,
                    MediaType = mediaType
                };

                // Lade Bild asynchron
                if (!string.IsNullOrEmpty(posterUrl))
                {
                    _ = LoadImageAsync(posterUrl, mediaItem);
                }

                Favorites.Items.Add(mediaItem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
        }
        finally
        {
            Favorites.IsLoading = false;
        }
    }

    private async Task LoadRecentEntriesAsync()
    {
        if (_client == null) return;
        
        try
        {
            RecentEntries.IsLoading = true;
            var items = await _client.RequestRecentEntriesAsync();
            
            foreach (var item in items.Take(10))
            {
                if (item?.Entry == null) continue;
                
                var posterUrl = item.Entry.PosterPictureId.HasValue 
                    ? $"{_baseAddress}/api/pictures/{item.Entry.PosterPictureId}"
                    : null;

                // Bestimme den MediaType
                string? mediaType = null;
                if (item.Entry is DtoTVShow)
                {
                    mediaType = "show";
                }
                else if (item.Entry is DtoMovieCollection)
                {
                    mediaType = "collection";
                }

                var mediaItem = new MediaItemViewModel
                {
                    Title = item.Entry.Name,
                    ImageUrl = posterUrl,
                    ImageSource = "placeholder.png",
                    EntryId = item.Entry.Id,
                    MediaType = mediaType
                };

                // Lade Bild asynchron
                if (!string.IsNullOrEmpty(posterUrl))
                {
                    _ = LoadImageAsync(posterUrl, mediaItem);
                }

                RecentEntries.Items.Add(mediaItem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading recent entries: {ex.Message}");
        }
        finally
        {
            RecentEntries.IsLoading = false;
        }
    }

    private async Task LoadDownloadsAsync()
    {
        try
        {
            Downloads.IsLoading = true;
            
            // WICHTIG: Erst auf Main Thread clearen
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Downloads.Items.Clear();
            });
            
            // Lade Downloads aus lokaler Datenbank (kein Server-Zugriff)
            var downloadedVideos = await Services.DownloadManager.Instance.GetAllDownloadsAsync();
            
            System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Found {downloadedVideos.Count} downloaded videos in database");
            
            foreach (var download in downloadedVideos.Take(10))
            {
                System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Processing download: {download.Title} ({download.VideoType}) - VideoId: {download.VideoId}");
                
                // Erstelle MediaItem auf Main Thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var mediaItem = new MediaItemViewModel
                    {
                        Title = download.Title,
                        // Verwende SolidColorBrush als Dummy-Image
                        ImageSource = ImageSource.FromFile("dotnet_bot.png"), // Default MAUI icon als Fallback
                        EntryId = download.VideoId,
                        MediaType = download.VideoType.Equals(Models.MediaTypes.Movie, StringComparison.OrdinalIgnoreCase) ? "movie" : "episode"
                    };
                    
                    Downloads.Items.Add(mediaItem);
                    System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Added to Items. New count: {Downloads.Items.Count}");
                });
                
                // Lade Bild asynchron NACH dem Hinzufügen
                if (!string.IsNullOrEmpty(download.LocalPosterImagePath) && File.Exists(download.LocalPosterImagePath))
                {
                    _ = LoadLocalImageAsync(download.LocalPosterImagePath, Downloads.Items[Downloads.Items.Count - 1]);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[LoadDownloadsAsync] Final Downloads.Items.Count: {Downloads.Items.Count}");
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
            var sources = await _client.RequestSourcesAsync();
            
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    var sourceItem = new MediaSourceViewModel
                    {
                        Id = source.Id,
                        Name = source.Name
                    };
                    
                    Sources.Add(sourceItem);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading sources: {ex.Message}");
        }
    }

    private async Task LoadImageAsync(string imageUrl, MediaItemViewModel mediaItem)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl) || _httpClient == null)
                return;

            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
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
            System.Diagnostics.Debug.WriteLine($"Error loading image {imageUrl}: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
