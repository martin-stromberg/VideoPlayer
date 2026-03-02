using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using SkiaSharp;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.ViewModels;

public class TVShowDetailsViewModel : INotifyPropertyChanged
{
    private readonly VideoWebPlayerClient? _client;
    private readonly HttpClient _httpClient;
    private readonly string _baseAddress;
    private bool _isLoading;
    private string? _title;
    private string? _plot;
    private string? _genreNames;
    private string? _releaseYear;
    private string? _showBannerUrl;
    private ImageSource? _showBannerSource;
    private ImageSource? _displayBannerSource;
    private Color _displayBannerBackgroundColor = Colors.Transparent;
    private int _selectedSeasonIndex = -1;
    private TVShowEpisodeViewModel? _selectedEpisode;
    private bool _isInitialLoad = true;
    private string? _selectedEpisodeName;

    public long TVShowId { get; }

    public TVShowEpisodeViewModel? SelectedEpisode
    {
        get => _selectedEpisode;
        set
        {
            if (_selectedEpisode != value)
            {
                _selectedEpisode = value;
                OnPropertyChanged();
                
                // Wenn Episode ausgewählt, lade Episode-Banner und -Informationen
                if (_selectedEpisode != null && !_isInitialLoad)
                {
                    _ = LoadEpisodeBannerAndInfoAsync(_selectedEpisode);
                }
                else if (_selectedEpisode == null)
                {
                    // Keine Episode ausgewählt → Episode-Name löschen
                    SelectedEpisodeName = null;
                }
            }
        }
    }
    
    public string? SelectedEpisodeName
    {
        get => _selectedEpisodeName;
        set
        {
            if (_selectedEpisodeName != value)
            {
                _selectedEpisodeName = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Plot
    {
        get => _plot;
        set
        {
            if (_plot != value)
            {
                _plot = value;
                OnPropertyChanged();
            }
        }
    }

    public string? GenreNames
    {
        get => _genreNames;
        set
        {
            if (_genreNames != value)
            {
                _genreNames = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ReleaseYear
    {
        get => _releaseYear;
        set
        {
            if (_releaseYear != value)
            {
                _releaseYear = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageSource? DisplayBannerSource
    {
        get => _displayBannerSource;
        set
        {
            if (_displayBannerSource != value)
            {
                _displayBannerSource = value;
                OnPropertyChanged();
            }
        }
    }

    public Color DisplayBannerBackgroundColor
    {
        get => _displayBannerBackgroundColor;
        set
        {
            if (_displayBannerBackgroundColor != value)
            {
                _displayBannerBackgroundColor = value;
                OnPropertyChanged();
            }
        }
    }

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

    public int SelectedSeasonIndex
    {
        get => _selectedSeasonIndex;
        set
        {
            if (_selectedSeasonIndex != value)
            {
                var oldIndex = _selectedSeasonIndex;
                _selectedSeasonIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSeason));
                OnPropertyChanged(nameof(Episodes));
                
                // Wenn nicht der initiale Load und Index ändert sich
                if (!_isInitialLoad && oldIndex != -1)
                {
                    // Wähle automatisch erste Episode der neuen Staffel aus
                    if (Episodes.Count > 0)
                    {
                        SelectedEpisode = Episodes[0];
                    }
                }
            }
        }
    }

    public ObservableCollection<DtoTVShowSeason> Seasons { get; } = new();
    public DtoTVShowSeason? SelectedSeason => SelectedSeasonIndex >= 0 && SelectedSeasonIndex < Seasons.Count 
        ? Seasons[SelectedSeasonIndex] 
        : null;

    public ObservableCollection<TVShowEpisodeViewModel> Episodes => 
        SelectedSeason != null ? GetEpisodesForSeason(SelectedSeason) : new();

    private readonly Dictionary<long, ObservableCollection<TVShowEpisodeViewModel>> _episodeCache = new();

    public TVShowDetailsViewModel(long tvShowId)
    {
        TVShowId = tvShowId;
        
        _client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
        
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

    public async Task LoadDataAsync(long? initialSeasonId = null)
    {
        if (_client == null || string.IsNullOrWhiteSpace(_baseAddress))
            return;

        IsLoading = true;
        _isInitialLoad = true;

        try
        {
            var tvShow = await _client.RequestTVShowAsync(TVShowId) as DtoTVShow;
            
            if (tvShow != null)
            {
                Title = tvShow.Name;
                Plot = tvShow.Plot;
                GenreNames = tvShow.GenreNames;
                
                // Setze Erscheinungsjahr (PremieredAt oder ReleaseDate)
                if (tvShow.PremieredAt.HasValue)
                {
                    ReleaseYear = tvShow.PremieredAt.Value.Year.ToString();
                }
                else if (tvShow.ReleaseDate.HasValue)
                {
                    ReleaseYear = tvShow.ReleaseDate.Value.Year.ToString();
                }

                // Lade Show Banner
                if (tvShow.BannerPictureId.HasValue)
                {
                    _showBannerUrl = $"{_baseAddress}/api/pictures/{tvShow.BannerPictureId}";
                    await LoadShowBannerAsync(_showBannerUrl);
                }

                Seasons.Clear();
                if (tvShow.Seasons != null)
                {
                    foreach (var season in tvShow.Seasons.OrderBy(s => s.Name))
                    {
                        Seasons.Add(season);
                    }
                }

                // Staffel auswählen
                if (Seasons.Count > 0)
                {
                    if (initialSeasonId.HasValue)
                    {
                        // Suche die Staffel mit der angegebenen ID (nicht erste Staffel = manuell)
                        var seasonIndex = Seasons.ToList().FindIndex(s => s.Id == initialSeasonId.Value);
                        if (seasonIndex > 0)
                        {
                            // Nicht die erste Staffel → Banner der Staffel laden
                            _isInitialLoad = false;
                        }
                        SelectedSeasonIndex = seasonIndex >= 0 ? seasonIndex : 0;
                        
                        if (!_isInitialLoad)
                        {
                            await LoadSeasonBannerAsync();
                        }
                    }
                    else
                    {
                        // Wähle die erste Staffel automatisch → Show Banner behalten
                        SelectedSeasonIndex = 0;
                        _isInitialLoad = false; // Nach dem ersten Load ist es nicht mehr initial
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading TV show: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<Color> ExtractFirstPixelColorAsync(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var bitmap = SKBitmap.Decode(stream);
            
            if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0)
                return Colors.Transparent;

            // Berechne die durchschnittliche Farbe aus den ersten paar Pixeln
            // (obere linke Ecke, typischerweise 10x10 Pixel Sample)
            int sampleSize = Math.Min(10, Math.Min(bitmap.Width, bitmap.Height));
            long totalR = 0, totalG = 0, totalB = 0;
            int pixelCount = 0;

            for (int y = 0; y < sampleSize; y++)
            {
                for (int x = 0; x < sampleSize; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    totalR += pixel.Red;
                    totalG += pixel.Green;
                    totalB += pixel.Blue;
                    pixelCount++;
                }
            }

            if (pixelCount > 0)
            {
                byte avgR = (byte)(totalR / pixelCount);
                byte avgG = (byte)(totalG / pixelCount);
                byte avgB = (byte)(totalB / pixelCount);
                
                return Color.FromRgb(avgR, avgG, avgB);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting pixel color: {ex.Message}");
        }
        
        return Colors.Transparent;
    }

    private async Task LoadShowBannerAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl) || _httpClient == null)
                return;

            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            
            // Extrahiere erste Pixel-Farbe
            var backgroundColor = await ExtractFirstPixelColorAsync(imageBytes);
            
            var imageSource = new StreamImageSource
            {
                Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
            };

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _showBannerSource = imageSource;
                DisplayBannerSource = imageSource;
                DisplayBannerBackgroundColor = backgroundColor;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading show banner image {imageUrl}: {ex.Message}");
        }
    }

    private async Task LoadSeasonBannerAsync()
    {
        if (SelectedSeason == null)
            return;

        try
        {
            if (SelectedSeason.BannerPictureId.HasValue)
            {
                var seasonBannerUrl = $"{_baseAddress}/api/pictures/{SelectedSeason.BannerPictureId}";
                
                if (!string.IsNullOrEmpty(seasonBannerUrl) && _httpClient != null)
                {
                    var imageBytes = await _httpClient.GetByteArrayAsync(seasonBannerUrl);
                    
                    // Extrahiere erste Pixel-Farbe
                    var backgroundColor = await ExtractFirstPixelColorAsync(imageBytes);
                    
                    var imageSource = new StreamImageSource
                    {
                        Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
                    };                    

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        DisplayBannerSource = imageSource;
                        DisplayBannerBackgroundColor = backgroundColor;
                    });
                }
            }
            else
            {
                // Kein Season Banner vorhanden → Show Banner anzeigen
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    DisplayBannerSource = _showBannerSource;
                    // Behalte die vorherige Background-Color bei
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading season banner: {ex.Message}");
            // Bei Fehler: Show Banner anzeigen
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DisplayBannerSource = _showBannerSource;
            });
        }
    }
    
    private async Task LoadEpisodeBannerAndInfoAsync(TVShowEpisodeViewModel episode)
    {
        try
        {
            // Aktualisiere Plot und Episode-Name
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Plot = episode.Plot;
                SelectedEpisodeName = episode.EpisodeNumber > 0 
                    ? $"E{episode.EpisodeNumber:D2}: {episode.Title}" 
                    : episode.Title;
            });
            
            // Lade Episode Banner (wenn vorhanden, sonst Season/Show Banner)
            if (!string.IsNullOrEmpty(episode.ImageUrl) && _httpClient != null)
            {
                var imageBytes = await _httpClient.GetByteArrayAsync(episode.ImageUrl);
                
                // Extrahiere erste Pixel-Farbe
                var backgroundColor = await ExtractFirstPixelColorAsync(imageBytes);
                
                var imageSource = new StreamImageSource
                {
                    Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
                };

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    DisplayBannerSource = imageSource;
                    DisplayBannerBackgroundColor = backgroundColor;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading episode banner: {ex.Message}");
        }
    }

    private ObservableCollection<TVShowEpisodeViewModel> GetEpisodesForSeason(DtoTVShowSeason season)
    {
        if (_episodeCache.TryGetValue(season.Id, out var cached))
        {
            return cached;
        }

        var episodes = new ObservableCollection<TVShowEpisodeViewModel>();
        
        if (season.Episodes != null)
        {
            foreach (var episode in season.Episodes.OrderBy(e => e.Number))
            {
                var episodeVm = new TVShowEpisodeViewModel
                {
                    Title = episode.Name,
                    Plot = episode.Plot,
                    EpisodeId = episode.Id,
                    EpisodeNumber = episode.Number,
                    ImageSource = "placeholder.png"
                };

                if (episode.PosterPictureId.HasValue)
                {
                    var thumbUrl = $"{_baseAddress}/api/pictures/{episode.PosterPictureId}";
                    episodeVm.ImageUrl = thumbUrl;
                    _ = LoadEpisodeImageAsync(thumbUrl, episodeVm);
                }

                episodes.Add(episodeVm);
            }
        }

        _episodeCache[season.Id] = episodes;
        return episodes;
    }

    private async Task LoadEpisodeImageAsync(string imageUrl, TVShowEpisodeViewModel episode)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl) || _httpClient == null)
                return;

            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            var imageSource = new StreamImageSource
            {
                Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
            };

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                episode.ImageSource = imageSource;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading episode image {imageUrl}: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
