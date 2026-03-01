using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoWebPlayer.Maui.ViewModels;

public class MediaCarouselViewModel : INotifyPropertyChanged
{
    private string? _title;
    private bool _isLoading;

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

    public bool HasItems => Items.Count > 0;

    public ObservableCollection<MediaItemViewModel> Items { get; }

    public MediaCarouselViewModel()
    {
        Items = new ObservableCollection<MediaItemViewModel>();
        Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasItems));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
