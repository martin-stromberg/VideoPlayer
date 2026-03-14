# Episode Selection & Smart Play Button - Technical Documentation

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Entwickler  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2024

## Feature Overview

Intelligente Episode-Auswahl auf der TV-Show-Details-Seite mit kontextabhängigem Play-Button.

## Implementierte Features

### 1. Episode Selection
- **SelectedEpisode Property** im ViewModel
- Two-way binding mit CollectionView
- Automatische Aktualisierung von Banner und Plot

### 2. Smart Play Button

Der Play-Button passt sich intelligent an die Situation an:

#### Beim ersten Laden (Initial Load):
- ✅ Zeige Serien-Banner und Serien-Plot
- ✅ Erste Staffel automatisch gewählt
- ✅ KEINE Episode vorausgewählt
- ✅ Play-Button → Spielt erste Episode

#### Bei Staffelwechsel:
- ✅ Erste Episode wird automatisch ausgewählt
- ✅ Episode-Banner wird geladen
- ✅ Episode-Plot wird angezeigt
- ✅ Play-Button → Spielt diese Episode

#### Bei Episode-Auswahl:
- ✅ Episode-spezifisches Banner
- ✅ Episode-spezifischer Plot
- ✅ Play-Button → Spielt ausgewählte Episode

## Code-Struktur

### ViewModel (TVShowDetailsViewModel.cs)

```csharp
// Neue Properties:
public TVShowEpisodeViewModel? SelectedEpisode { get; set; }

// Neue Methoden:
private async Task LoadEpisodeBannerAndInfoAsync(TVShowEpisodeViewModel episode)
{
    // Lädt Episode-Banner und aktualisiert Plot
}

// Angepasste Logik:
public int SelectedSeasonIndex
{
    set
    {
        if (!_isInitialLoad && oldIndex != -1)
        {
            // Auto-select first episode on season change
            if (Episodes.Count > 0)
            {
                SelectedEpisode = Episodes[0];
            }
        }
    }
}
```

### XAML (TVShowDetailsPage.xaml)

```xaml
<CollectionView ItemsSource="{Binding Episodes}" 
                SelectionMode="Single"
                SelectedItem="{Binding SelectedEpisode}"
                SelectionChanged="OnEpisodeSelectionChanged">
```

### Code-Behind (TVShowDetailsPage.xaml.cs)

```csharp
private async void OnPlayTapped(object? sender, EventArgs e)
{
    // Smart fallback: Selected episode or first episode
    var episodeToPlay = _viewModel.SelectedEpisode 
                     ?? _viewModel.Episodes.FirstOrDefault();
    
    if (episodeToPlay == null)
    {
        await DisplayAlert("Keine Episode", "...", "OK");
        return;
    }

    await PlayEpisodeAsync(episodeToPlay);
}
```

## User Flow

```
┌─────────────────────────────────────────┐
│ App-Start: Serie laden                  │
│ ✓ Serie-Banner + Serie-Plot             │
│ ✓ Staffel 1 automatisch gewählt         │
│ ✗ Keine Episode ausgewählt              │
└─────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│ User klickt Play-Button                 │
│ → Spielt Episode 1 (Staffel 1)          │
└─────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│ User klickt Episode 3                   │
│ ✓ Episode 3 wird ausgewählt             │
│ ✓ Banner wechselt zu Episode 3          │
│ ✓ Plot zeigt Episode 3 Beschreibung     │
└─────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│ User klickt Play-Button                 │
│ → Spielt Episode 3 (Staffel 1)          │
└─────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│ User wechselt zu Staffel 2              │
│ ✓ Episode 1 (S2) automatisch gewählt    │
│ ✓ Banner + Plot aktualisiert            │
└─────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│ User klickt Play-Button                 │
│ → Spielt Episode 1 (Staffel 2)          │
└─────────────────────────────────────────┘
```

## Technical Details

### State Management
- `_isInitialLoad` flag verhindert auto-selection beim ersten Laden
- `SelectedEpisode` null beim Start → Serie-Informationen sichtbar
- `SelectedEpisode` gesetzt → Episode-Informationen sichtbar

### Event Flow
1. User wählt Episode → `SelectedEpisode` Property ändert sich
2. Property Setter triggert `LoadEpisodeBannerAndInfoAsync()`
3. Banner und Plot werden asynchron geladen und aktualisiert
4. UI reagiert auf PropertyChanged Events

### Performance
- Banner werden asynchron geladen
- Episode-Cache verhindert redundante Datenbank-Abfragen
- Main-Thread wird nicht blockiert

## Testing Checklist

- [x] App-Start zeigt Serie-Banner
- [x] Play-Button spielt erste Episode (keine Auswahl)
- [x] Episode-Auswahl aktualisiert Banner
- [x] Episode-Auswahl aktualisiert Plot
- [x] Staffelwechsel wählt erste Episode aus
- [x] Play-Button spielt ausgewählte Episode
- [x] Zurück zur Serie zeigt wieder Serie-Banner

## Known Issues

- MediaElement Package-Kompatibilitätsproblem mit .NET 10
- Siehe: [MediaElement Error Handling](./TECH_MediaElement_Error_Handling.md)

## Future Enhancements

- [ ] "Weiter schauen" Button (nächste Episode nach aktueller)
- [ ] Keyboard-Navigation für Episode-Auswahl
- [ ] Swipe-Gesten für schnellen Episodenwechsel
- [ ] Episode-Preview beim Hover

## Related Documentation

- [MediaElement Error Handling](./TECH_MediaElement_Error_Handling.md)
- [Video Playback](./TECH_Video_Playback.md)

---

**Implementiert in**: v2.0  
**Plattformen**: iOS, Windows
