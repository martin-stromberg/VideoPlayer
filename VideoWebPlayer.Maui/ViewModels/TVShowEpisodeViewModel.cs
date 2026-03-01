using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoWebPlayer.Maui.ViewModels;

public class TVShowEpisodeViewModel : INotifyPropertyChanged
{
    private string? _title;
    private string? _plot;
    private string? _imageUrl;
    private ImageSource? _imageSource;
    private long _episodeId;
    private int _episodeNumber;
    private int _seasonNumber;

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

    public string? ImageUrl
    {
        get => _imageUrl;
        set
        {
            if (_imageUrl != value)
            {
                _imageUrl = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageSource? ImageSource
    {
        get => _imageSource;
        set
        {
            if (_imageSource != value)
            {
                _imageSource = value;
                OnPropertyChanged();
            }
        }
    }

    public long EpisodeId
    {
        get => _episodeId;
        set
        {
            if (_episodeId != value)
            {
                _episodeId = value;
                OnPropertyChanged();
            }
        }
    }

    public int EpisodeNumber
    {
        get => _episodeNumber;
        set
        {
            if (_episodeNumber != value)
            {
                _episodeNumber = value;
                OnPropertyChanged();
            }
        }
    }

    public int SeasonNumber
    {
        get => _seasonNumber;
        set
        {
            if (_seasonNumber != value)
            {
                _seasonNumber = value;
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
