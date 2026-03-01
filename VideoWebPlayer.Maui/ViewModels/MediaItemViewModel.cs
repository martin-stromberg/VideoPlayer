using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoWebPlayer.Maui.ViewModels;

public class MediaItemViewModel : INotifyPropertyChanged
{
    private string? _title;
    private string? _imageUrl;
    private ImageSource? _imageSource;
    private long? _entryId;
    private string? _mediaType;
    private Color _backgroundColor = Colors.Transparent;

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

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                OnPropertyChanged();
            }
        }
    }

    public long? EntryId
    {
        get => _entryId;
        set
        {
            if (_entryId != value)
            {
                _entryId = value;
                OnPropertyChanged();
            }
        }
    }

    public string? MediaType
    {
        get => _mediaType;
        set
        {
            if (_mediaType != value)
            {
                _mediaType = value;
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
