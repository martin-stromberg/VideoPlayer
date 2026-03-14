using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoWebPlayer.Maui.ViewModels;

public enum CarouselKind
{
    ContinueWatching,
    Favorites,
    RecentEntries,
    Downloads,
    Other
}

public class MediaCarouselViewModel : INotifyPropertyChanged
{
    private string? _title;
    private bool _isLoading;
    private readonly List<MediaItemViewModel> _updatedItems = new();

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

    /// <summary>
    /// Typ des Karussells, bestimmt Verhalten wie LongPress verarbeitet wird.
    /// </summary>
    public CarouselKind Kind { get; set; } = CarouselKind.Other;

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

    /// <summary>
    /// Wird ausgelöst, wenn ein LongPress auf ein Element verarbeitet werden soll.
    /// Der Empfänger erhält das betroffene MediaItemViewModel.
    /// </summary>
    public class MediaItemPressedEventArgs : EventArgs
    {
        public MediaItemViewModel Item { get; }
        public MediaItemPressedEventArgs(MediaItemViewModel item) => Item = item;
    }

    public event EventHandler<MediaItemPressedEventArgs>? ItemPressed;

    /// <summary>
    /// Führt ein LongPress für das übergebene Element aus. Das ViewModel entscheidet,
    /// ob das Event verarbeitet werden soll; falls ja, wird ItemPressed ausgelöst.
    /// </summary>
    public void ExecuteLongPress(MediaItemViewModel item)
    {
        if (item == null)
            return;

        // Erlaubt nur für bestimmte Carousel-Kindertypen
        var allow = Kind == CarouselKind.Favorites || Kind == CarouselKind.ContinueWatching || Kind == CarouselKind.Downloads;

        if (allow)
        {
            ItemPressed?.Invoke(this, new MediaItemPressedEventArgs(item));
        }
    }

    /// <summary>
    /// Fügt ein Element an der angegebenen Position ein. Falls das Element bereits vorhanden ist
    /// und sich bereits an dieser Position befindet, passiert nichts. Befindet sich das vorhandene
    /// Element an einer anderen Position, wird es entfernt und an der neuen Position eingefügt.
    /// Wenn keine Position angegeben wird, wird das Element an Position 0 eingefügt.
    /// Die Erkennung eines vorhandenen Elements erfolgt primär über EntryId und MediaType, als
    /// Fallback über Title bzw. Referenzvergleich.
    /// </summary>
    public void AddItem(MediaItemViewModel item, int? position = null)
    {
        if (item == null)
            return;

        var insertPos = position ?? 0;
        if (insertPos < 0)
            insertPos = 0;

        if (insertPos > Items.Count)
            insertPos = Items.Count;

        int existingIndex = -1;
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i];
            // match by EntryId + MediaType when available
            if (it.EntryId.HasValue && item.EntryId.HasValue && it.EntryId.Value == item.EntryId.Value)
            {
                if (string.Equals(it.MediaType, item.MediaType, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            // fallback: same title
            if (!string.IsNullOrEmpty(it.Title) && !string.IsNullOrEmpty(item.Title) && string.Equals(it.Title, item.Title, StringComparison.Ordinal))
            {
                existingIndex = i;
                break;
            }

            // final fallback: reference equality
            if (ReferenceEquals(it, item))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            if (existingIndex == insertPos)
            {
                if (Items[existingIndex].IsFromCache)
                {
                    // existing entry is a cache placeholder – replace it with the live item
                    Items[existingIndex] = item;
                    _updatedItems.Add(item);
                }
                else
                {
                    // already at desired position – mark as updated and return
                    _updatedItems.Add(Items[existingIndex]);
                }
                return;
            }

            // remove existing; adjust insert position if necessary
            Items.RemoveAt(existingIndex);
            if (existingIndex < insertPos)
            {
                insertPos = Math.Max(0, insertPos - 1);
            }
        }

        // ensure final bounds
        if (insertPos > Items.Count)
            insertPos = Items.Count;

        Items.Insert(insertPos, item);
        _updatedItems.Add(Items[insertPos]);
    }

    /// <summary>
    /// Beginnt eine Aktualisierungssequenz. Setzt die interne Liste der aktualisierten Elemente zurück.
    /// </summary>
    public void BeginUpdate()
    {
        _updatedItems.Clear();
    }

    /// <summary>
    /// Beendet die Aktualisierungssequenz. Entfernt alle Elemente aus Items, die seit dem letzten
    /// BeginUpdate()-Aufruf nicht über AddItem hinzugefügt oder aktualisiert wurden.
    /// </summary>
    public void EndUpdate()
    {
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!_updatedItems.Contains(Items[i]))
                Items.RemoveAt(i);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
