# Bestandsaufnahme: Logik und Service

## `PlaylistService`
Datei: `VideoWebPlayer\Services\PlaylistService.cs`

### Öffentliche Methoden (gemäß IPlaylistService)

| Methode | Sichtbarkeit | Rückgabewert | Kurzbeschreibung |
|---------|-------------|--------------|------------------|
| `GetPlaylistsAsync` | public | `Task<DtoPlaylist[]>` | Ruft alle Playlists eines Benutzers ab, sortiert nach CreatedAt und Id |
| `GetPlaylistAsync` | public | `Task<DtoPlaylist?>` | Ruft eine einzelne Playlist ab (mit Ownership-Check) |
| `CreatePlaylistAsync` | public | `Task<DtoPlaylist>` | Erstellt eine neue Playlist mit Name, Description und SortMode |
| `UpdatePlaylistAsync` | public | `Task<DtoPlaylist>` | Aktualisiert Playlist (Name, Description, SortMode) |
| `DeletePlaylistAsync` | public | `Task` | Löscht eine Playlist |
| **`AddMediaToPlaylistAsync`** | public | `Task<DtoPlaylistEntry>` | **Kritische Methode für diese Anforderung** — siehe Details unten |
| `RemoveMediaFromPlaylistAsync` | public | `Task` | Entfernt einen einzelnen Eintrag |
| `GetPlaylistEntriesAsync` | public | `Task<DtoPlaylistEntry[]>` | Ruft alle Einträge einer Playlist ab, entfernt Waisenkinder |

### Private Hilfsmethoden

| Methode | Kurzbeschreibung |
|---------|------------------|
| `ParseMediaType` (Zeile 388–394) | Parst einen String case-insensitiv zu `MediaType` Enum mit Validierung |
| `GetCascadeMediaIdsAsync` (Zeile 396–402) | Ruft alle Kinder für einen Medientyp ab (z.B. Episoden für Serie) |
| `GetMediaTitleAsync` | Ruft den Titel für ein Medium ab |
| `GetMediaTitlesAsync` | Ruft Titel für mehrere Medien ab (batch) |
| `ToDto` | Konvertiert Entities zu DTOs |
| `GetOwnedPlaylistAsync` | Prüft Ownership und wirft Exception bei Zugriffsverletzung |

## Detailanalyse: `AddMediaToPlaylistAsync` (Zeile 141–206)

### Aktuelle Implementierung (Probleme)

**Zeile 145:** MediaType-Validierung
```csharp
var parsedMediaType = ParseMediaType(mediaType);
```
- ✅ Validiert case-insensitiv
- ❌ Aber der normalisierte Enum-Wert wird nicht verwendet für Speicherung

**Zeile 154–160:** Duplikat-Check laden
```csharp
var existingKeys = (await _db.PlaylistEntries
    .AsNoTracking()
    .Where(e => e.PlaylistId == playlistId)
    .Select(e => new { e.MediaType, e.MediaId })
    .ToListAsync(cancellationToken))
    .Select(e => (e.MediaType, e.MediaId))
    .ToHashSet();
```
- Lädt alle bestehenden Einträge als HashSet von `(MediaType: string, MediaId: long)` Tuples
- Hinweis: MediaType ist hier der **rohe String** aus der DB (nicht normalisiert)

**Zeile 162–163:** Top-Level-Duplikat-Check — **PROBLEM 1**
```csharp
if (existingKeys.Contains((mediaType, mediaId)))
    throw new InvalidOperationException("Medieninhalt bereits in dieser Playlist vorhanden.");
```
- ❌ Wirft Exception, führt zu HTTP 409 (via Controller MapInvalidOperationException)
- ❌ Verwendet den rohen Request-`mediaType`, nicht den normalisierten Enum-Wert
- Benutzer erhält Fehlermeldung, nicht Info-Meldung
- Ganze Operation wird abgebrochen

**Zeile 165–169:** Kaskaden-Einträge verarbeiten
```csharp
var cascadeEntries = await GetCascadeMediaIdsAsync(parsedMediaType, mediaId, cancellationToken);
var newCascadeEntries = cascadeEntries
    .Select(e => (MediaType: e.MediaType.ToString(), e.MediaId))
    .Where(e => !existingKeys.Contains(e))
    .ToList();
```
- ✅ Kaskaden-Duplikate werden gefiltert
- ❌ Aber: Der Filter nutzt `e.MediaType.ToString()` (kanonischer Wert), während `existingKeys` rohe Strings enthält
  - Dies ist eigentlich ein Bug-Fix: Neue Cascade-Einträge werden normalisiert!
  - Aber existierende Duplikate in der DB könnten nicht normalisiert sein, daher könnte es zu False-Negatives kommen
- ❌ **PROBLEM 2:** Kaskaden-Duplikate werden still übersprungen ohne Zähler oder Rückmeldung

**Zeile 182:** MediaType-Speicherung — **PROBLEM 3 (Normalisierung)**
```csharp
var topLevelEntry = new PlaylistEntry
{
    PlaylistId = playlistId,
    MediaType = mediaType,  // ❌ ROHER STRING, NICHT NORMALISIERT!
    MediaId = mediaId,
    ParentMediaType = null,
    ParentMediaId = null,
    AddedAt = now
};
```
- ❌ Speichert den rohen Request-String `mediaType`, nicht `parsedMediaType.ToString()`
- Beispiel: "Movie" und "movie" führen zu zwei separaten DB-Einträgen mit derselben ID
- Die Duplikat-Prüfung ist dann case-sensitiv und unreliabel

**Zeile 205:** Rückgabe — **PROBLEM 4 (Keine Rückmeldung)**
```csharp
return ToDto(topLevelEntry, mediaTitle, null);
```
- ❌ Gibt nur den Top-Level-Eintrag zurück
- ❌ Keine Information über:
  - Wie viele Einträge insgesamt hinzugefügt wurden (Top-Level + Cascade)
  - Wie viele übersprungen wurden (Duplikate)
  - Benutzerdefinierte Meldung für UI-Feedback

### Abonnierte Events
- Keine direkten Event-Subscriptions in AddMediaToPlaylistAsync

### Publizierte Events
- Keine Events werden nach AddMediaToPlaylistAsync publiziert (direkt in der Methode)
- Im Gegensatz zu CreatePlaylistAsync (Zeile 99), UpdatePlaylistAsync (Zeile 125), DeletePlaylistAsync (Zeile 137)

### MediaTypeHandler
**Zeile 331–380:** Statische Zuordnung von `MediaType` Enum-Wert zu Handler-Funktionen
- Definiert für jeden Medientyp, wie Titel geladen werden (`LoadTitlesAsync`)
- Und optional: wie Kaskaden-Kinder geladen werden (`LoadCascadeChildrenAsync`)
- **TVShowSeason** und **TVShow** und **MovieCollection** haben Cascade-Handler
- **Movie** und **TVShowEpisode** haben keine Cascade-Handler (sind Leaf-Nodes)

Die Handler arbeiten auf der Basis des **kanonischen Enum-Wertes**, nicht auf Strings.
