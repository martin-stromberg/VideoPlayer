# Logik-Klassen

## `VideoPlayer` (Razor-Komponente)
Datei: `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplyPlaylistNavigationResultAsync` | private | Wendet das Ergebnis einer Next/Previous/Advance-Navigation an; setzt `playlistEndReached = true` wenn `result is null` |
| `OnNextPlaylistEntryAsync` | private | Ruft `GetNextPlaylistEntryAsync` auf und leitet an `ApplyPlaylistNavigationResultAsync` |
| `OnPreviousPlaylistEntryAsync` | private | Ruft `GetPreviousPlaylistEntryAsync` auf und leitet an `ApplyPlaylistNavigationResultAsync` |
| `OnMediaEndAsync` | private | Ruft `AdvancePlaylistAsync` auf für automatisches Weiterschalten |
| `OnRestartPlaylistAsync` | private | Startet Playlist neu von Anfang |
| `ApplyPlaylistContext` | private | Setzt lokalen Playlist-Kontext (PlaylistId, CurrentEntryId, TotalCount, Name) |
| `ClearPlaylistContext` | private | Löscht lokalen Playlist-Kontext |
| `RunPlaylistNavigationActionAsync` | private | Fehlerbehandlungs-Wrapper für Navigationsaktionen; setzt `playlistNavigationErrorMessage` bei Fehler |

**Felder (Zustand):**
- `playlistEndReached` (bool, private): Wird auf `true` gesetzt, wenn keine weitere Navigation möglich ist
- `playlistNavigationErrorMessage` (string?, private): Wird gesetzt bei Fehler in einer Navigation
- `currentPlaylistId` (long?, private): Aktuelle Playlist-ID
- `currentPlaylistEntryId` (long?, private): Aktueller Eintrag
- `playlistTotalCount` (int?, private): Gesamtzahl Einträge in Playlist
- `currentPlaylistPosition` (int, private): 0-basierte Position in Playlist

**Status der Anforderung:**
- `playlistEndReached` wird bei `result is null` immer auf `true` gesetzt (Zeile 313), **unabhängig von Navigationsrichtung**
  - **PROBLEM:** Für "Vorheriger" am Anfang sollte keine "Ende erreicht"-Meldung gezeigt werden
  - Muss Navigationsrichtung unterscheiden können
- `playlistNavigationErrorMessage` ist vorhanden und wird korrekt gesetzt (Zeile 290)
- Keine separate Behandlung für `playlistBeginningReached`
- `ApplyPlaylistNavigationResultAsync` erhält keine Navigationsrichtungs-Information

---

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartPlaylistAsync` | public async | Startet Wiedergabe; ruft `ResolveExplicitStartEntry` oder `ResolveFirstPlayableEntry` auf |
| `ResolveExplicitStartEntry` | private static | Validiert angeforderten Eintrag; prüft nur Zugehörigkeit zur Playlist und Zugriffsrecht |
| `ResolveFirstPlayableEntry` | private static | Findet ersten abspielbaren und zugänglichen Eintrag |
| `FindAdjacentPlayableEntryAsync` | private async | Findet nächsten/vorherigen abspielbaren und zugänglichen Eintrag |
| `GetNextPlaylistEntryAsync` | public async | API für nächsten Eintrag |
| `GetPreviousPlaylistEntryAsync` | public async | API für vorherigen Eintrag |
| `AdvancePlaylistAsync` | public async | Ruft intern `GetNextPlaylistEntryAsync` auf |

**Status der Anforderung:**

- `ResolveExplicitStartEntry` (Zeile 924-934):
  - **PROBLEM:** Prüft NICHT `PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType)`
  - Aktuell: Prüft nur `KeyNotFoundException` (Eintrag gehört zur Playlist) und `PlaylistAccessDeniedException` (Zugriff)
  - **FEHLEND:** Validierung, ob der Eintrag tatsächlich abspielbar ist (nicht TVShow/TVShowSeason/MovieCollection)
  - Müsste zusätzlich werfen: `InvalidOperationException` wenn nicht abspielbar
  
- `ResolveFirstPlayableEntry` (Zeile 943-952):
  - Korrekt: Prüft `PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType)` UND `isAccessible`
  
- `FindAdjacentPlayableEntryAsync` (Zeile 970-998):
  - Korrekt: Prüft beide Bedingungen beim Filtern von Kandidaten (Zeilen 990-993)

---

## `PlaylistEntriesList` (Razor-Komponente)
Datei: `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsPlayableEntry` | private static | Prüft, ob Eintrag abspielbar ist (Movie oder TVShowEpisode) |
| `OnPlayEntry` | Parameter (EventCallback) | Wird aufgerufen bei Doppelklick oder "Abspielen"-Button |
| `RunEntryActionAsync` | private | Fehlerbehandlungs-Wrapper für Entry-Operationen |

**Status der Anforderung:**

- `IsPlayableEntry` (Zeile 306-307):
  - Aktuell: `PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType)`
  - **PROBLEM:** Prüft NICHT `entry.IsAccessible`
  - **FEHLEND:** Muss auch `entry.IsAccessible` validieren
  
- "Abspielen"-Button (Zeile 68-71):
  - Korrekt: Button wird nur gerendert wenn `IsPlayableEntry(entry)` wahr ist
  - Aber: `IsPlayableEntry` ist unvollständig (siehe oben)
  
- @ondblclick (Zeile 59):
  - Aktuell: `@ondblclick="() => OnPlayEntry.InvokeAsync(entry)"` – IMMER aktiv
  - **PROBLEM:** Auch auf nicht-abspielbaren Zeilen (TVShow/Staffel/Sammlung) aktiv
  - **PROBLEM:** Auch auf nicht-zugänglichen Zeilen aktiv
  - **FEHLEND:** Conditional rendering oder disabled-Logik analog zum Button

---

## `PlaylistDetail` (Razor-Komponente)
Datei: `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartPlaybackAsync` | private async | Startet Wiedergabe beim Klick auf "Abspielen" oder initialen Entry-Load |
| `OnPlaylistEntryIdChanged` | private | Callback von VideoPlayer bei Navigation (Next/Previous/Advance) |
| `LoadPlaylistAsync` | private async | Lädt Playlist-Metadaten; setzt `loadError` bei Fehler |

**Felder (Zustand):**
- `loadError` (string?, private): Fehler beim Laden von Playlist-Metadaten
- `playbackStart` (DtoPlaylistPlaybackStart?, private): Aktuelle Wiedergabe-Session
- `isLoading` (bool, private): Zeigt "Lade Daten..." an
- `sortModeStatusMessage` (string?, private): Fehler bei Sortiermodus-Änderung
- Kein separates `playbackError` Feld

**Status der Anforderung:**

- `StartPlaybackAsync` (Zeile 150-165):
  - Aktuell: Fehler werden in `loadError` gespeichert (Zeile 163)
  - **PROBLEM:** `loadError` ist für Fehler beim LADEN der Playlist-Metadaten gedacht, nicht für Fehler beim Starten der Wiedergabe
  - **FEHLEND:** Separates Feld `playbackError` für Wiedergabe-Fehler
  
- Fehler-Rendering (Zeile 23-25):
  - Zeigt `loadError` in einer Alert-Box
  - **PROBLEM:** Wiedergabe-Fehler (403 bei gesperrtem Eintrag) werden hier als generischer "Lade"-Fehler angezeigt
  - **FEHLEND:** Separate Fehler-UI innerhalb der Playlist-Details für Wiedergabe-Fehler
  
- `OnPlaylistEntryIdChanged` (Zeile 181-191):
  - Korrekt: Updated `playbackStart` mit neuen Stream-Informationen
  - Korrekt: Navigiert zur URL mit aktualisiertem `entryId`
