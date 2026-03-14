using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaSharp;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.ViewModels;

public class SourceOverviewViewModel : INotifyPropertyChanged
{
    private readonly VideoWebPlayerClient? _client;
    private bool _isLoading;
    private bool _isLoadingMore;
    private GenreViewModel? _selectedGenre;
    private int _currentPage = 0;
    private const int PageSize = 20;
    private bool _hasMoreItems = true;

    public long SourceId { get; }
    public string SourceName { get; }

    public ObservableCollection<GenreViewModel> Genres { get; } = new();
    public ObservableCollection<MediaItemViewModel> Items { get; } = new();

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

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set
        {
            if (_isLoadingMore != value)
            {
                _isLoadingMore = value;
                OnPropertyChanged();
            }
        }
    }

    public GenreViewModel? SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (_selectedGenre != value)
            {
                _selectedGenre = value;
                OnPropertyChanged();
                _ = LoadItemsForGenreAsync();
            }
        }
    }

    public SourceOverviewViewModel(long sourceId, string sourceName)
    {
        SourceId = sourceId;
        SourceName = sourceName;

        _client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
    }

    public async Task LoadGenresAsync()
    {
        if (_client == null)
            return;

        IsLoading = true;

        try
        {
            var sourceGenres = await _client.RequestSourceGenresAsync(SourceId);

            Genres.Clear();
            
            // Füge "Alle" als ersten Eintrag hinzu (mit ID = 0)
            Genres.Add(new GenreViewModel
            {
                Id = 0,
                Name = "Alle"
            });
            
            if (sourceGenres?.Genres != null)
            {
                foreach (var genre in sourceGenres.Genres)
                {
                    Genres.Add(new GenreViewModel
                    {
                        Id = genre.Id,
                        Name = genre.Name
                    });
                }
            }

            // Wähle "Alle" automatisch als erstes Genre
            if (Genres.Count > 0)
            {
                SelectedGenre = Genres[0];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading genres: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadItemsForGenreAsync()
    {
        if (SelectedGenre == null || _client == null)
            return;

        IsLoading = true;
        _currentPage = 0;
        _hasMoreItems = true;

        try
        {
            Items.Clear();
            
            // Lade direkt 3 Seiten nacheinander
            for (int i = 0; i < 3 && _hasMoreItems; i++)
            {
                await LoadMoreItemsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading items: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadMoreItemsAsync()
    {
        if (!_hasMoreItems || IsLoadingMore || SelectedGenre == null)
            return;

        IsLoadingMore = true;

        try
        {
            // Wenn "Alle" ausgewählt ist (ID = 0), lade ohne Genre-Filter
            var genreId = SelectedGenre.Id == 0 ? 0 : SelectedGenre.Id;
            
            var items = await _client.RequestSourceItems(SourceId, _currentPage, PageSize, "", genreId);

            if (items == null || items.Count == 0)
            {
                _hasMoreItems = false;
                return;
            }

            foreach (var item in items)
            {
                var mediaItem = new MediaItemViewModel
                {
                    Title = item.Title,
                    ImageSource = "placeholder.png",
                    EntryId = item.Id,
                    MediaType = item.Type == "Movie" ? "collection" : "show"
                };

                if (item.PictureId.HasValue)
                {
                    mediaItem.PosterPictureId = item.PictureId;
                    _ = LoadItemImageAsync(item.PictureId.Value, mediaItem);
                }

                // Füge Items direkt hinzu (ObservableCollection ist bereits thread-safe)
                Items.Add(mediaItem);
            }

            _currentPage++;

            if (items.Count < PageSize)
            {
                _hasMoreItems = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading more items: {ex.Message}");
            _hasMoreItems = false;
        }
        finally
        {
            IsLoadingMore = false;
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

    private async Task LoadItemImageAsync(long pictureId, MediaItemViewModel mediaItem)
    {
        try
        {
            if (_client == null)
                return;

            var imageBytes = await _client.GetPictureAsync(pictureId);
            if (imageBytes == null || imageBytes.Length == 0)
                return;

            // Extrahiere Hintergrundfarbe
            var backgroundColor = await ExtractAverageColorAsync(imageBytes);

            var imageSource = new StreamImageSource
            {
                Stream = (token) => Task.FromResult((Stream)new MemoryStream(imageBytes))
            };

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                mediaItem.ImageSource = imageSource;
                mediaItem.BackgroundColor = backgroundColor;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading item image for picture {pictureId}: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GenreViewModel : INotifyPropertyChanged
{
    private long _id;
    private string _name = string.Empty;

    public long Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
