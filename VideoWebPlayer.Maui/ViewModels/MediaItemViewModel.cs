using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;

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

    public ICommand DeleteDownloadCommand { get; }

    public MediaItemViewModel()
    {
        DeleteDownloadCommand = new Command(async () => await OnDeleteDownloadAsync());
    }

    private async Task OnDeleteDownloadAsync()
    {
        if (Application.Current?.MainPage == null)
            return;

        // Zeige Bestätigungsdialog
        var result = await Application.Current.MainPage.DisplayAlert(
            "Download löschen?",
            $"'{Title}' und die lokalen Dateien werden gelöscht.",
            "Löschen",
            "Abbrechen");

        if (result)
        {
            try
            {
                // Lösche die Download-Datei
                await DownloadManager.Instance.DeleteDownloadAsync(
                    EntryId ?? 0,
                    MediaType ?? "movie");

                System.Diagnostics.Debug.WriteLine($"[MediaItemViewModel] Deleted: {Title}");
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert(
                    "Fehler",
                    $"Download konnte nicht gelöscht werden: {ex.Message}",
                    "OK");
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
