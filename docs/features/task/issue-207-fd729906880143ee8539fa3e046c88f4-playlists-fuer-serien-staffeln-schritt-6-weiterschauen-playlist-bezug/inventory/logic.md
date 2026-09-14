# Logik- und Komponentenmethoden

## `PlaylistDetail.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`

Parent-Komponente für die Playlist-Wiedergabe mit State und Callbacks.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnPlaylistEntryIdChanged(PlaylistEntryPlaybackInfo info)` | private | **KRITISCHER FEHLER:** Aktualisiert `playbackStart` bei einem Entry-Wechsel, aktualisiert aber NICHT `playbackStart.StartPositionSeconds`. Dies ist die Hauptursache der Regression. Wird aufgerufen via `CurrentPlaylistEntryIdChanged` Callback vom VideoPlayer. |
| `StartPlaybackAsync(long? entryId)` | private | Ruft `PlaylistClient.StartPlaylistAsync(Id, entryId)` auf und erhält ein `DtoPlaylistPlaybackStart` mit korrekter `StartPositionSeconds`. Diese wird korrekt an `VideoPlayer` übergeben. |
| `OnInitializedAsync()` | protected override | Lädt Playlist und startet Wiedergabe, falls `EntryId` Query-Parameter vorhanden ist. |

**Befund:** Die `OnPlaylistEntryIdChanged` Methode (Zeilen 202-212) hat diese kritische Zeile nicht:
```csharp
playbackStart.StartPositionSeconds = ???;  // FEHLT!
```

Sie aktualisiert stattdessen:
- `playbackStart.CurrentEntryId`
- `playbackStart.MediaType`
- `playbackStart.MediaId`
- `playbackStart.StreamUrl`

## `VideoPlayer.razor`
Datei: `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor`

Abspieler-Komponente mit Playlist-Navigationslogik.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplyPlaylistNavigationResultAsync(DtoPlaylistNavigationResult? result, bool isForward)` | private | Wird nach Next/Previous/Advance aufgerufen. Aktualisiert `currentPlaylistEntryId`, `currentPlaylistPosition` und ruft `ApplyEntryStreamInfo` auf. |
| `ApplyEntryStreamInfo(string playerMediaType, long mediaId, string entryMediaType)` | private | **REGRESSION-PUNKT:** Setzt explizit `StartPositionSeconds = null` (Zeile 351). Dies ist notwendig, um zu vermeiden, dass die alte Position erneut angewendet wird, aber es passiert KEINE Übergabe der neuen Position an den Parent. |
| `OnMediaEndAsync()` | private | Ruft `AdvancePlaylistAsync` auf und delegiert an `ApplyPlaylistNavigationResultAsync`. |
| `OnNextPlaylistEntryAsync()` | private | Ruft `GetNextPlaylistEntryAsync` auf und delegiert an `ApplyPlaylistNavigationResultAsync`. |
| `OnPreviousPlaylistEntryAsync()` | private | Ruft `GetPreviousPlaylistEntryAsync` auf und delegiert an `ApplyPlaylistNavigationResultAsync`. |
| `OnRestartPlaylistAsync()` | private | Ruft `StartPlaylistAsync` auf, wendet Kontext und Stream-Info an und ruft `NotifyPlaylistEntryChangedAsync` auf. |
| `NotifyPlaylistEntryChangedAsync(long entryId)` | private | Benachrichtigt Parent via `CurrentPlaylistEntryIdChanged` Callback mit `PlaylistEntryPlaybackInfo`. Das Record hat KEIN `StartPositionSeconds` Feld. |
| `OnAfterRenderAsync(bool firstRender)` | protected override | **REGRESSION-PUNKT:** Wendet `StartPositionSeconds` an, wenn sich die `StreamUrl` ändert (Zeilen 167-171). Setzt `_startApplied = false` zurück, wenn `StreamUrl` wechselt, aber die neue `StartPositionSeconds` wird vom Parent nicht aktualisiert. |

**Befund zur Regression:**
- Zeile 351: `StartPositionSeconds = null;` ist korrekt notwendig
- Zeile 167-171: `OnAfterRenderAsync` wendet alte `StartPositionSeconds` an, weil der Parent diese nicht aktualisiert hat
- Zeile 295-298: `NotifyPlaylistEntryChangedAsync` hat kein `StartPositionSeconds` Feld in `PlaylistEntryPlaybackInfo`

**Abonnierte Events:**
- `OnMediaEndAsync` → ruft `AdvancePlaylistAsync` auf
- `OnNextPlaylistEntryAsync()` → Klick auf "Nächster" Button
- `OnPreviousPlaylistEntryAsync()` → Klick auf "Vorheriger" Button
- `OnRestartPlaylistAsync()` → Klick auf "Neu starten" Button

**Publizierte Events:**
- `CurrentPlaylistEntryIdChanged` → Parameter Event an Parent (`PlaylistDetail`) mit `PlaylistEntryPlaybackInfo`

## `IPlaylistApiClient`
Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs`

Interface für Playlist-API-Operationen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartPlaylistAsync(long playlistId, long? entryId)` | playlistId, entryId (optional) | `Task<DtoPlaylistPlaybackStart>` | Initial-Start oder Neustart: liefert die Startposition |
| `GetNextPlaylistEntryAsync(long playlistId, long currentEntryId)` | playlistId, currentEntryId | `Task<DtoPlaylistNavigationResult?>` | Navigation: nächster Eintrag (KEINE StartPositionSeconds!) |
| `GetPreviousPlaylistEntryAsync(long playlistId, long currentEntryId)` | playlistId, currentEntryId | `Task<DtoPlaylistNavigationResult?>` | Navigation: vorheriger Eintrag (KEINE StartPositionSeconds!) |
| `AdvancePlaylistAsync(long playlistId, long currentEntryId)` | playlistId, currentEntryId | `Task<DtoPlaylistNavigationResult?>` | Auto-Advance bei Medienende (KEINE StartPositionSeconds!) |

**Kritischer Befund:** Alle drei Navigation-Methoden (Next/Previous/Advance) liefern `DtoPlaylistNavigationResult` **ohne** `StartPositionSeconds`. Das ist ein Designproblem, das die Implementierung von Szenario A verhindert.
