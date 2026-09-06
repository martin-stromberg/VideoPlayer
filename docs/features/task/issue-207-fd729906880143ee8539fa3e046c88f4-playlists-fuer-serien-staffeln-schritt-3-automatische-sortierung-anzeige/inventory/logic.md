# Logik und Methoden

## PlaylistService

Datei: `VideoWebPlayer/Services/PlaylistService.cs`

### Fehlerhafte Kernmethode

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsEntryAccessible` (Zeilen 660–671) | `private static` | **FEHLERHAFTE IMPLEMENTIERUNG:** Prüft, ob ein Playlist-Eintrag zugänglich ist. Gibt hardcodiert `false` für alle Typen außer `MovieCollection` und `TVShow` zurück. Ignoriert komplett den regulären Quellenzugriff (`MediaSourceUsers`). |

**Aktuelle Logik (FALSCH):**
```csharp
private static bool IsEntryAccessible(PlaylistEntry entry, HashSet<long> unlockedMovieCollectionIds, HashSet<long> unlockedTVShowIds)
{
    if (!TryParseKnownMediaType(entry.MediaType, out var parsedType))
        return false;

    return parsedType switch
    {
        MediaType.MovieCollection => unlockedMovieCollectionIds.Contains(entry.MediaId),
        MediaType.TVShow => unlockedTVShowIds.Contains(entry.MediaId),
        _ => false  // ← FEHLER: Movie, TVShowEpisode, TVShowSeason geben immer false zurück
    };
}
```

### Methoden, die IsEntryAccessible nutzen

| Methode | Sichtbarkeit | Kurzbeschreibung | Nutzt IsEntryAccessible |
|---------|-------------|------------------|------------------------|
| `BuildEntryDtosAsync` (Zeilen 580–603) | `private async` | Zentrale Kernmethode, die DTOs für Playlist-Einträge erstellt. Lädt Titel, Bilder und Freischaltungs-Status in Bulk. Die Zugriffsvalidierung erfolgt hier über `IsEntryAccessible`. | **JA** (Zeile 600) |
| `GetPlaylistEntriesAsync` (Zeilen 180–186) | `public async` | Ruft alle Einträge einer Playlist ab. Nutzt `BuildEntryDtosAsync` über `LoadValidPlaylistEntriesAsync`. | Indirekt via `BuildEntryDtosAsync` |
| `GetPlaylistEntriesPagedAsync` (Zeilen 674–701) | `public async` | Ruft paginierte Einträge mit Sortierung ab. Nutzt `BuildEntryDtosAsync` auf die Seite. | Indirekt via `BuildEntryDtosAsync` |
| `BuildAddResultAsync` (Zeilen 507–529) | `private async` | Erstellt das Ergebnis-DTO nach dem Hinzufügen von Medien. Nutzt `BuildEntryDtosAsync`. | Indirekt via `BuildEntryDtosAsync` |

### Unterstützende Methoden für Bulk-Loading

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadUnlockedMediaIdsAsync` (Zeilen 643–649) | `private async` | Lädt in zwei Queries die freigeschalteten `MovieCollection`- und `TVShow`-IDs für einen Benutzer via `IUnlockedMediaService`. Gibt ein Tuple von HashSets zurück. |
| `LoadTitlesForMediaRefsAsync` (Zeilen 754–766) | `private async` | Lädt Titel gruppiert nach Medientyp. Wird für Einträge und ihre Parents genutzt. |
| `LoadPictureIdsForMediaRefsAsync` (Zeilen 614–632) | `private async` | Lädt aufgelöste Bild-IDs (Poster, Fallback auf Banner/Fanart) gruppiert nach Medientyp. |
| `GetParentMediaRefs` (Zeile 774) | `private static` | Projiziert Parent-Mediarefeenzen aus Einträgen. |
| `GetParentTitle` (Zeilen 779–787) | `private static` | Ruft Parent-Titel aus geladenen Daten ab. |

## ItemsController

Datei: `VideoWebPlayer/Controllers/ItemsController.cs`

### Referenzimplementierung für Freischaltungsprüfung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureAccessAsync` (Zeilen 358–370) | `private async` | **KORREKTE IMPLEMENTIERUNG:** Prüft, ob der aktuelle Benutzer Zugriff auf eine Media hat. Führt die Regel `hasSourceAccess OR isUnlocked` aus. |
| `IsUnlockedAsync` (Zeilen 372–387) | `private async` | **KORREKTE HIERARCHIE-AUFLÖSUNG:** Prüft, ob eine Media freigeschaltet ist, mit Fallback-Hierarchie: Movie → MovieCollection, TVShowEpisode → TVShowSeason → TVShow, sonst direkt. |

**Korrekte Logik in EnsureAccessAsync:**
```csharp
private async Task EnsureAccessAsync(MediaBaseEntry entry)
{
    var source = await _db.MediaSources
        .AsNoTracking()
        .FirstOrDefaultAsync(ms => ms.Id == entry.MediaSourceId);
    if (source is null)
        throw new RecordNotFoundException("Medienquelle nicht gefunden");

    var hasSourceAccess = await _db.MediaSourceUsers.AnyAsync(u => u.UserId == CurrentUser.Id && u.MediaSourceId == source.Id);
    var isUnlocked = await IsUnlockedAsync(entry);
    if (!hasSourceAccess && !isUnlocked)  // ← KORREKTE REGEL: hasSourceAccess OR isUnlocked
        throw new UnauthorizedAccessException("Fehlende Berechtigung fuer Medienquelle");
}
```

**Korrekte Hierarchie-Auflösung in IsUnlockedAsync:**
```csharp
private async Task<bool> IsUnlockedAsync(MediaBaseEntry entry)
{
    // Movie: Prüfe seine MovieCollection
    if (entry is Movie movie)
        return await _db.UnlockedMediaEntries.AsNoTracking().AnyAsync(u => u.UserId == CurrentUser.Id && u.MovieCollectionId == movie.MovieCollectionId);

    // TVShowEpisode: Prüfe seine TVShow (über Season)
    if (entry is TVShowEpisode episode)
    {
        var showId = await _db.TVShowSeasons.AsNoTracking()
            .Where(s => s.Id == episode.TVShowSeasonId)
            .Select(s => (long?)s.TVShowId)
            .FirstOrDefaultAsync();
        return await _db.UnlockedMediaEntries.AsNoTracking().AnyAsync(u => u.UserId == CurrentUser.Id && u.TVShowId == showId);
    }

    // TVShowSeason oder TVShow: Direkt prüfen
    return await _db.UnlockedMediaEntries.AsNoTracking().AnyAsync(u => u.UserId == CurrentUser.Id && (u.MovieCollectionId == entry.Id || u.TVShowId == entry.Id));
}
```

### Weitere Methoden in ItemsController mit korrekter Logik

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `Get` (Endpunkt) | Zeile 117–226 | Items-Discovery-Endpunkt. Zeigt die korrekte Logik: `Where(m => mediaSourceIds.Contains(m.MediaSourceId) \|\| unlockedMovieCollectionIds.Contains(m.Id))` (Zeile 158, 191). |
