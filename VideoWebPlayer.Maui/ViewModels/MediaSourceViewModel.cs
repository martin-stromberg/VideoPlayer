using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoWebPlayer.Maui.ViewModels;

public class MediaSourceViewModel : INotifyPropertyChanged
{
    private long _id;
    private string? _name;

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

    public string? Name
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
