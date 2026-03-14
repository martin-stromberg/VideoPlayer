using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoWebPlayer.Maui.ViewModels;

public class MediaSourceViewModel : INotifyPropertyChanged
{
    private long _id;
    private string? _name;
	private string? _icon;
	private long? _iconPictureId;
	private ImageSource? _iconImageSource;

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

	public string? Icon
	{
		get => _icon;
		set
		{
			if (_icon != value)
			{
				_icon = value;
				OnPropertyChanged();
			}
		}
	}

	public long? IconPictureId
	{
		get => _iconPictureId;
		set
		{
			if (_iconPictureId != value)
			{
				_iconPictureId = value;
				OnPropertyChanged();
			}
		}
	}

	public ImageSource? IconImageSource
	{
		get => _iconImageSource;
		set
		{
			if (_iconImageSource != value)
			{
				_iconImageSource = value;
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
