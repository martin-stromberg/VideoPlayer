using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using SkiaSharp;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.ViewModels;

public class MovieCollectionDetailsViewModel : INotifyPropertyChanged
{
    private readonly VideoWebPlayerClient? _client;
    private readonly HttpClient _httpClient;
    private readonly string _baseAddress;
    private bool _isLoading;
    private string? _title;
    private string? _plot;
    private string? _genreNames;
    private string? _releaseYear;
    private ImageSource? _collectionBannerSource;
    private ImageSource? _displayBannerSource;
    private Color _displayBannerBackgroundColor = Colors.Transparent;
    private int _selectedMovieIndex = -1;

    public long CollectionId { get; }

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

    public int SelectedMovieIndex
    {
        get => _selectedMovieIndex;
        set
        {
            if (_selectedMovieIndex != value)
            {
                _selectedMovieIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedMovie));
                
                if (_selectedMovieIndex >= 0)
                {
                    _ = LoadMovieBannerAsync();
                }
            }
        }
    }

    public ObservableCollection<MediaItemViewModel> Movies { get; } = new();
    
    public MediaItemViewModel? SelectedMovie => SelectedMovieIndex >= 0 && SelectedMovieIndex < Movies.Count 
        ? Movies[SelectedMovieIndex] 
        : null;

    public bool ShowMoviesList => Movies.Count > 1;

    private readonly Dictionary<long, DtoMovie> _movieDetailsCache = new();

    public MovieCollectionDetailsViewModel(long collectionId)
    {
        CollectionId = collectionId;
        
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

    public async Task LoadDataAsync(long? initialMovieId = null)
    {
        if (_client == null || string.IsNullOrWhiteSpace(_baseAddress))
            return;

        IsLoading = true;

        try
        {
            var collection = await _client.RequestMovieCollectionAsync(CollectionId) as DtoMovieCollection;
            
            if (collection != null)
            {
                Title = collection.Name;

                // Lade Collection Banner
                if (collection.BannerPictureId.HasValue)
                {
                    var collectionBannerUrl = $"{_baseAddress}/api/pictures/{collection.BannerPictureId}";
                    await LoadCollectionBannerAsync(collectionBannerUrl);
                }

                Movies.Clear();
                if (collection.Movies != null)
                {
                    foreach (var movie in collection.Movies.OrderBy(m => m.ReleaseDate ?? DateTime.MinValue))
                    {
                        var movieVm = new MediaItemViewModel
                        {
                            Title = movie.Name,
                            EntryId = movie.Id,
                            ImageSource = "placeholder.png"
                        };

                        if (movie.PosterPictureId.HasValue)
                        {
                            var posterUrl = $"{_baseAddress}/api/pictures/{movie.PosterPictureId}";
                            movieVm.ImageUrl = posterUrl;
                            _ = LoadMoviePosterImageAsync(posterUrl, movieVm);
                        }

                        Movies.Add(movieVm);
                        
                        // Cache movie details
                        _movieDetailsCache[movie.Id] = movie;
                    }
                }

                OnPropertyChanged(nameof(ShowMoviesList));

                // Film auswählen
                if (Movies.Count > 0)
                {
                    if (initialMovieId.HasValue)
                    {
                        var movieIndex = Movies.ToList().FindIndex(m => m.EntryId == initialMovieId.Value);
                        SelectedMovieIndex = movieIndex >= 0 ? movieIndex : 0;
                    }
                    else if (Movies.Count == 1)
                    {
                        // Nur ein Film → automatisch auswählen
                        SelectedMovieIndex = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading movie collection: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<Color> ExtractAverageColorAsync(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var bitmap = SKBitmap.Decode(stream);
            
            if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0)
                return Colors.Transparent;

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

    private async Task LoadCollectionBannerAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl) || _httpClient == null)
                return;

            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            var backgroundColor = await ExtractAverageColorAsync(imageBytes);
            
            var imageSource = new StreamImageSource
            {
                Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
            };

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _collectionBannerSource = imageSource;
                DisplayBannerSource = imageSource;
                DisplayBannerBackgroundColor = backgroundColor;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading collection banner image {imageUrl}: {ex.Message}");
        }
    }

    private async Task LoadMovieBannerAsync()
    {
        if (SelectedMovie == null || !SelectedMovie.EntryId.HasValue)
            return;

        try
        {
            if (!_movieDetailsCache.TryGetValue(SelectedMovie.EntryId.Value, out var movie))
                return;

            // Aktualisiere Plot und Genre
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Plot = movie.Plot;
                GenreNames = movie.GenreNames;
                
                // Setze Erscheinungsjahr
                if (movie.ReleaseDate.HasValue)
                {
                    ReleaseYear = movie.ReleaseDate.Value.Year.ToString();
                }
                else if (movie.PremieredAt.HasValue)
                {
                    ReleaseYear = movie.PremieredAt.Value.Year.ToString();
                }
                else
                {
                    ReleaseYear = null;
                }
            });

            // Versuche Banner, dann Fanart, dann Poster
            long? imageId = movie.BannerPictureId ?? movie.FanartPictureId ?? movie.PosterPictureId;
            
            if (imageId.HasValue)
            {
                var movieImageUrl = $"{_baseAddress}/api/pictures/{imageId}";
                
                if (!string.IsNullOrEmpty(movieImageUrl) && _httpClient != null)
                {
                    var imageBytes = await _httpClient.GetByteArrayAsync(movieImageUrl);
                    var backgroundColor = await ExtractAverageColorAsync(imageBytes);
                    
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
                // Kein Bild vorhanden → Collection Banner anzeigen
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    DisplayBannerSource = _collectionBannerSource;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading movie banner: {ex.Message}");
            // Bei Fehler: Collection Banner anzeigen
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DisplayBannerSource = _collectionBannerSource;
            });
        }
    }

    private async Task LoadMoviePosterImageAsync(string imageUrl, MediaItemViewModel movie)
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
                movie.ImageSource = imageSource;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading movie image {imageUrl}: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
