using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaSharp;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Maui.Components;
using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Maui.Services;

namespace VideoWebPlayer.Maui.ViewModels;

public class TVShowDetailsViewModel : INotifyPropertyChanged, IMediaBannerViewModel
{
    private readonly VideoWebPlayerClient? _client;
    private bool _isLoading;
    private string? _title;
    private string? _plot;
    private string? _genreNames;
    private string? _releaseYear;
    
    private ImageSource? _showBannerSource;
    private ImageSource? _displayBannerSource;
    private Color _displayBannerBackgroundColor = Colors.Transparent;
    private int _selectedSeasonIndex = -1;
    private int _selectedEpisodeIndex = -1;
    private TVShowEpisodeViewModel? _selectedEpisode;
    private bool _isInitialLoad = true;
    private string? _selectedEpisodeName;
    private DtoTVShow? _tvShow;
    private bool _isShowFavorite;
    private bool _isSelectedSeasonFavorite;
	private bool _isSelectionFavorite;

    public long TVShowId { get; }

	public int SelectedSeasonIndex
	{
		get => _selectedSeasonIndex;
		set
		{
			if (_selectedSeasonIndex != value)
			{
				_selectedSeasonIndex = value;
				OnPropertyChanged();

				UpdateSelectedSeasonFavorite();
				UpdateSelectionFavorite();

				// Wenn Staffel wechselt: Episode-Liste aktualisieren und automatisch Episode 1 auswählen
				SelectedEpisodeIndex = -1;
				OnPropertyChanged(nameof(Episodes));
				if (Episodes.Count > 0)
					SelectedEpisodeIndex = 0;

				// Lade Staffel-Banner wenn nicht die erste Staffel ist
				if (!_isInitialLoad && value > 0)
				{
					_ = LoadSeasonBannerAsync();
				}
				else if (value == 0)
				{
					// Erste Staffel: zeige Show-Banner
					_ = MainThread.InvokeOnMainThreadAsync(() =>
					{
						DisplayBannerSource = _showBannerSource;
					});
				}
			}
		}
	}

    public int SelectedEpisodeIndex
    {
        get => _selectedEpisodeIndex;
        set
        {
            if (_selectedEpisodeIndex != value)
            {
                _selectedEpisodeIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedEpisode));
                
                // Wenn Episode ausgewählt, lade Episode-Banner und -Informationen
                if (SelectedEpisode != null && !_isInitialLoad)
                {
                    _ = LoadEpisodeBannerAndInfoAsync(SelectedEpisode);
                }

				UpdateSelectionFavorite();
            }
        }
    }

    public TVShowEpisodeViewModel? SelectedEpisode
    {
        get => _selectedEpisodeIndex >= 0 && _selectedEpisodeIndex < Episodes.Count
            ? Episodes[_selectedEpisodeIndex]
            : null;
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

    public ObservableCollection<DtoTVShowSeason> Seasons { get; } = new();
    
    public DtoTVShowSeason? SelectedSeason => SelectedSeasonIndex >= 0 && SelectedSeasonIndex < Seasons.Count 
        ? Seasons[SelectedSeasonIndex] 
        : null;

    public bool IsShowFavorite
    {
        get => _isShowFavorite;
        set
        {
            if (_isShowFavorite != value)
            {
                _isShowFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFavoriteStarText));
				UpdateSelectionFavorite();
            }
        }
    }

    public string ShowFavoriteStarText => IsShowFavorite ? "★" : "☆";

	public bool IsSelectionFavorite
	{
		get => _isSelectionFavorite;
		private set
		{
			if (_isSelectionFavorite != value)
			{
				_isSelectionFavorite = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(SelectionFavoriteStarText));
			}
		}
	}

	public string SelectionFavoriteStarText => IsSelectionFavorite ? "★" : "☆";

    public bool IsSelectedSeasonFavorite
    {
        get => _isSelectedSeasonFavorite;
        set
        {
            if (_isSelectedSeasonFavorite != value)
            {
                _isSelectedSeasonFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSeasonFavoriteStarText));
				UpdateSelectionFavorite();
            }
        }
    }

    public string SelectedSeasonFavoriteStarText => IsSelectedSeasonFavorite ? "★" : "☆";

	private enum FavoriteTarget
	{
		Episode,
		Season,
		Show
	}

	private FavoriteTarget GetSelectionFavoriteTarget()
	{
		var episode = SelectedEpisode;
		if (episode == null)
			return FavoriteTarget.Show;

		if (episode.EpisodeNumber == 1)
		{
			if (TryParseSeasonNumber(SelectedSeason?.Name) == 1)
				return FavoriteTarget.Show;

			return FavoriteTarget.Season;
		}

		return FavoriteTarget.Episode;
	}

	private static int? TryParseSeasonNumber(string? seasonName)
	{
		if (string.IsNullOrWhiteSpace(seasonName))
			return null;

		var match = System.Text.RegularExpressions.Regex.Match(seasonName, @"\d+");
		return match.Success && int.TryParse(match.Value, out var n) ? n : null;
	}

	private void UpdateSelectionFavorite()
	{
		var target = GetSelectionFavoriteTarget();
		IsSelectionFavorite = target switch
		{
			FavoriteTarget.Episode => SelectedEpisode?.IsFavorite ?? false,
			FavoriteTarget.Season => SelectedSeason?.IsFavorite ?? false,
			FavoriteTarget.Show => IsShowFavorite,
			_ => false
		};
	}

    public ObservableCollection<TVShowEpisodeViewModel> Episodes => 
        SelectedSeason != null ? GetEpisodesForSeason(SelectedSeason) : new();

    private readonly Dictionary<long, ObservableCollection<TVShowEpisodeViewModel>> _episodeCache = new();
    private HashSet<long> _downloadedEpisodeIds = new();

    public TVShowDetailsViewModel(long tvShowId)
    {
        TVShowId = tvShowId;

        _client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
    }

    public async Task LoadDataAsync(long? initialSeasonId = null, long? initialEpisodeId = null)
    {
        if (_client == null)
            return;

        IsLoading = true;
        _isInitialLoad = true;

        try
        {
            var tvShow = await _client.RequestTVShowAsync(TVShowId) as DtoTVShow;
            
            if (tvShow != null)
            {
                _tvShow = tvShow;
                Title = tvShow.Name;
                Plot = tvShow.Plot;
                GenreNames = tvShow.GenreNames;
                IsShowFavorite = tvShow.IsFavorite;
                
                // Setze Erscheinungsjahr (PremieredAt oder ReleaseDate)
                if (tvShow.PremieredAt.HasValue)
                {
                    ReleaseYear = tvShow.PremieredAt.Value.Year.ToString();
                }
                else if (tvShow.ReleaseDate.HasValue)
                {
                    ReleaseYear = tvShow.ReleaseDate.Value.Year.ToString();
                }

                // Lade Show Banner über API-Client
                if (tvShow.BannerPictureId.HasValue)
                {
                    await LoadShowBannerAsync(tvShow.BannerPictureId.Value);
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

                    UpdateSelectedSeasonFavorite();

                    // Episode auswählen (falls angegeben)
                    if (initialEpisodeId.HasValue && Episodes.Count > 0)
                    {
                        var episodeIndex = Episodes.ToList().FindIndex(e => e.EpisodeId == initialEpisodeId.Value);
                        if (episodeIndex >= 0)
                        {
                            SelectedEpisodeIndex = episodeIndex;
                            System.Diagnostics.Debug.WriteLine($"[TVShowDetailsViewModel] Episode {initialEpisodeId} loaded at index {episodeIndex}");
                        }
                    }
                    else if (Episodes.Count > 0)
                    {
                        // Wähle erste Episode
                        SelectedEpisodeIndex = 0;
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

        await RefreshDownloadStatusAsync();
    }

    private void UpdateSelectedSeasonFavorite()
    {
        IsSelectedSeasonFavorite = SelectedSeason?.IsFavorite ?? false;
    }

    public async Task ToggleShowFavoriteAsync()
    {
        if (_client == null)
            return;

        try
        {
            var isFav = await _client.ToggleFavorite(new DtoTVShow { Id = TVShowId, Name = Title ?? string.Empty });
            IsShowFavorite = isFav;
            if (_tvShow != null)
                _tvShow.IsFavorite = isFav;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TVShowDetailsViewModel] Error toggling show favorite: {ex.Message}");
        }
    }

	public async Task ToggleSelectionFavoriteAsync()
	{
		var target = GetSelectionFavoriteTarget();
		try
		{
			switch (target)
			{
				case FavoriteTarget.Episode:
					if (SelectedEpisode != null)
						await ToggleEpisodeFavoriteAsync(SelectedEpisode);
					break;
				case FavoriteTarget.Season:
					await ToggleSelectedSeasonFavoriteAsync();
					break;
				case FavoriteTarget.Show:
					await ToggleShowFavoriteAsync();
					break;
			}
		}
		finally
		{
			UpdateSelectionFavorite();
		}
	}

    public async Task ToggleSelectedSeasonFavoriteAsync()
    {
        if (_client == null || SelectedSeason == null)
            return;

        try
        {
            var seasonId = SelectedSeason.Id;
            var isFav = await _client.ToggleFavorite(new DtoTVShowSeason { Id = seasonId, Name = SelectedSeason.Name });
            SelectedSeason.IsFavorite = isFav;
            IsSelectedSeasonFavorite = isFav;
			UpdateSelectionFavorite();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TVShowDetailsViewModel] Error toggling season favorite: {ex.Message}");
        }
    }

    public async Task ToggleEpisodeFavoriteAsync(TVShowEpisodeViewModel episode)
    {
        if (_client == null)
            return;

        try
        {
            var isFav = await _client.ToggleFavorite(new DtoTVShowEpisode { Id = episode.EpisodeId, Name = episode.Title ?? string.Empty });
            episode.IsFavorite = isFav;
			if (ReferenceEquals(episode, SelectedEpisode))
				UpdateSelectionFavorite();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TVShowDetailsViewModel] Error toggling episode favorite: {ex.Message}");
        }
    }

    public async Task RefreshDownloadStatusAsync()
    {
        try
        {
            var downloads = await DownloadManager.Instance.GetAllDownloadsAsync();
            _downloadedEpisodeIds = downloads
                .Where(d => string.Equals(DownloadManager.NormalizeVideoType(d.VideoType), MediaTypes.Episode, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.VideoId)
                .ToHashSet();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var list in _episodeCache.Values)
                {
                    foreach (var ep in list)
                    {
                        ep.IsDownloaded = _downloadedEpisodeIds.Contains(ep.EpisodeId);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TVShowDetailsViewModel] Error refreshing download status: {ex.Message}");
        }
    }

    public TVShowEpisodeViewModel? FindEpisode(long episodeId)
    {
        foreach (var list in _episodeCache.Values)
        {
            var match = list.FirstOrDefault(e => e.EpisodeId == episodeId);
            if (match != null)
                return match;
        }

        return Episodes.FirstOrDefault(e => e.EpisodeId == episodeId);
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

    private async Task LoadShowBannerAsync(long pictureId)
    {
        try
        {
            if (_client == null)
                return;

            var imageBytes = await _client.GetPictureAsync(pictureId);
            if (imageBytes == null || imageBytes.Length == 0)
                return;

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
            System.Diagnostics.Debug.WriteLine($"Error loading show banner image {pictureId}: {ex.Message}");
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
                if (_client == null)
                    return;

                var imageBytes = await _client.GetPictureAsync(SelectedSeason.BannerPictureId.Value);
                if (imageBytes != null && imageBytes.Length > 0)
                {
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
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() => DisplayBannerSource = _showBannerSource);
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
            if (episode.PosterPictureId.HasValue && _client != null)
            {
                var imageBytes = await _client.GetPictureAsync(episode.PosterPictureId.Value);
                if (imageBytes != null && imageBytes.Length > 0)
                {
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
                    SeasonNumber = season.Number,
                    IsFavorite = episode.IsFavorite,
                    ImageSource = "placeholder.png"
                };

                if (episode.PosterPictureId.HasValue)
                {
                    episodeVm.PosterPictureId = episode.PosterPictureId;
                    _ = LoadEpisodeImageAsync(episode.PosterPictureId.Value, episodeVm);
                }

                episodes.Add(episodeVm);
            }
        }

        _episodeCache[season.Id] = episodes;
        return episodes;
    }

    private async Task LoadEpisodeImageAsync(long pictureId, TVShowEpisodeViewModel episode)
    {
        try
        {
            if (_client == null)
                return;

            var imageBytes = await _client.GetPictureAsync(pictureId);
            if (imageBytes == null || imageBytes.Length == 0)
                return;

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
            System.Diagnostics.Debug.WriteLine($"Error loading episode image for picture {pictureId}: {ex.Message}");
        }
    }

    public bool ShouldShowPlayButton => true;
    
    public async Task<(long VideoId, string VideoType, string Title)?> GetVideoInfoForPlaybackAsync()
    {
        // Spiele ausgewählte Episode oder erste Episode
        var episode = SelectedEpisode ?? Episodes.FirstOrDefault();
        
        if (episode == null)
            return null;

        return (episode.EpisodeId, Models.MediaTypes.Episode, episode.Title ?? "Unknown Episode");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
