← [Zurück zur Übersicht](index.md)

# Weiterschauen — Business Rules

## Regel: Episode-Reihenfolge wird nach Episodennummer bestimmt

**Beschreibung:** Die Reihenfolge der Episoden innerhalb einer Staffel wird ausschließlich durch die Episodennummer (`TVShowEpisode.Number`) bestimmt. Datumsangaben (`ReleaseDate`, `PremieredAt`) beeinflussen die Reihenfolge nicht.

**Bedingungen:**
- Episodennummern sind positive Ganzzahlen
- Episodennummern können Lücken aufweisen (z. B. 1, 2, 4 — Episode 3 fehlt)
- Alle Episoden innerhalb einer Staffel sollten eindeutige Nummern haben

**Verhalten:**
- Die nächste Episode ist immer die mit der kleinsten Nummer größer als der aktuellen Episodennummer
- Episodennummern von NULL oder Null-ähnlichen Werten werden nicht berücksichtigt
- Datumsfelder können NULL sein ohne Auswirkung auf die Reihenfolge

**Umsetzung:** `ContinueWatchingService.GetNextEpisodeAsync()` (Zeilen 354–358):
```csharp
var next = await _db.TVShowEpisodes.AsNoTracking()
    .Where(e => e.TVShowSeasonId == current.TVShowSeasonId && e.Number > current.Number)
    .OrderBy(e => e.Number)
    .Select(e => e.Id)
    .FirstOrDefaultAsync(ct);
```

**Begründung:** Episodennummern sind die zuverlässigste Quelle für die Episode-Reihenfolge. Datumsfelder können NULL oder fehlerhaft sein; die Episodennummer ist hingegen strukturell garantiert.

---

## Regel: Staffelübergang geschieht nach lexikographischer Sortierung der Staffel-Namen

**Beschreibung:** Wenn alle Episoden einer Staffel angesehen wurden, erfolgt der Übergang zur nächsten Staffel. Die Reihenfolge der Staffeln wird nach deren Namen (`TVShowSeason.Name`) lexikographisch (alphabetisch) sortiert.

**Bedingungen:**
- Die aktuelle Staffel ist bekannt (`TVShowSeasonId`)
- Die Serie existiert (über `TVShowId`)
- Es können mehrere Staffeln in beliebiger Reihenfolge existieren (z. B. "Staffel 01", "Staffel 02", "Season 1", "Spezials")

**Verhalten:**
- Alle Staffeln der Serie werden geladen und nach `Name` sortiert
- Die aktuelle Staffel wird in dieser sortierten Liste gesucht
- Die nächste Staffel in der Liste ist der Zielübergang
- Falls keine nächste Staffel existiert: `null` zurückgeben (Serie abgeschlossen)

**Umsetzung:** `ContinueWatchingService.GetNextEpisodeAsync()` (Zeilen 362–368):
```csharp
var nextSeason = (await _db.TVShowSeasons.Where(s => s.TVShowId == season.TVShowId)
    .OrderBy(s => s.Name)
    .ToListAsync(ct))
    .SkipWhile(s => s.Id != season.Id)
    .SkipWhile(s => s.Id == season.Id)
    .FirstOrDefault();
```

**Begründung:** Staffel-Namen sind nicht immer numerisch sortierbar (z. B. "Staffel 01" vs. "Season 1" vs. "Spezials"). Lexikographische Sortierung ist zuverlässig und deterministisch. Diese Regel folgt dem Muster, das auch in `GetNextMovieAsync()` für Filmsammlungen verwendet wird.

---

## Regel: Nur eine Episode pro Serie in der "Weiterschauen"-Liste

**Beschreibung:** Ein Benutzer kann pro Serie nur eine Episode in der "Weiterschauen"-Liste haben. Wenn eine neue Episode hinzugefügt wird, werden alle anderen Episoden derselben Serie aus der Liste entfernt.

**Bedingungen:**
- Benutzer hat eine Episode einer Serie zu Ende angesehen
- Die nächste Episode existiert
- Es können bereits andere Episoden derselben Serie in der "Weiterschauen"-Liste sein (von früheren Interaktionen)

**Verhalten:**
- Query: Finde alle `ContinueWatchingEntry` des Benutzers, deren Episode zu derselben `TVShow` gehört, aber eine unterschiedliche `TVShowEpisodeId` hat
- Alle diese Einträge werden gelöscht
- Die neue Episode wird eingefügt oder aktualisiert
- Signalisiere eine Änderung der "Weiterschauen"-Liste (SignalR)

**Umsetzung:** `ContinueWatchingService.RemoveExistingTVShowEntry()` (Zeilen 261–289):
```csharp
private async Task RemoveExistingTVShowEntry(string userId, long? nextEpisodeId, CancellationToken ct)
{
    // ... TVShow-ID ermitteln über Episode → Season → Show ...
    var obsoleteEpisodeEntries = await (
        from cw in _db.ContinueWatchingEntries
        join e in _db.TVShowEpisodes on cw.TVShowEpisodeId equals e.Id
        join s in _db.TVShowSeasons on e.TVShowSeasonId equals s.Id
        where cw.UserId == userId
              && cw.TVShowEpisodeId != null
              && cw.TVShowEpisodeId != nextEpisodeId.Value
              && s.TVShowId == showId  // Gleiche Serie
        select cw
    ).ToListAsync(ct);
    
    if (obsoleteEpisodeEntries.Count > 0)
        _db.ContinueWatchingEntries.RemoveRange(obsoleteEpisodeEntries);
}
```

**Begründung:** Dies verhindert Verwirrung durch mehrere Episoden derselben Serie in der Liste und erzeugt eine klare, nicht redundante Übersicht. Die Liste konzentriert sich auf "was kommt als nächstes", nicht auf "alles, was ich angesehen habe".

---

## Regel: Nur ein Film pro Filmsammlung in der "Weiterschauen"-Liste

**Beschreibung:** Ein Benutzer kann pro Filmsammlung (z. B. eine Filmreihe wie "Marvel") nur einen Film in der "Weiterschauen"-Liste haben. Analog zur Serie-Regel werden alle anderen Filme derselben Sammlung entfernt, wenn eine neue Film hinzugefügt wird.

**Bedingungen:**
- Benutzer hat einen Film zu Ende angesehen
- Der nächste Film existiert (in der gleichen Sammlung)
- Es können bereits andere Filme derselben Sammlung in der Liste sein

**Verhalten:**
- Query: Finde alle `ContinueWatchingEntry` des Benutzers, deren Film zu derselben `MovieCollection` gehört, aber eine unterschiedliche `MovieId` hat
- Alle diese Einträge werden gelöscht
- Der neue Film wird eingefügt oder aktualisiert
- Signalisiere eine Änderung

**Umsetzung:** `ContinueWatchingService.RemoveExtsingMovieCollectionEntry()` (Zeilen 291–318):
```csharp
private async Task RemoveExtsingMovieCollectionEntry(string userId, long? nextMovieId, CancellationToken ct)
{
    // ... MovieCollectionId ermitteln über Film ...
    var obsoleteMovieEntries = await (
        from cw in _db.ContinueWatchingEntries
        join m in _db.Movies on cw.MovieId equals m.Id
        where cw.UserId == userId
              && cw.MovieId != null
              && cw.MovieId != nextMovieId.Value
              && m.MovieCollectionId == collectionId.Value  // Gleiche Sammlung
        select cw
    ).ToListAsync(ct);
    
    if (obsoleteMovieEntries.Count > 0)
        _db.ContinueWatchingEntries.RemoveRange(obsoleteMovieEntries);
}
```

**Begründung:** Analoge Begründung wie bei Serien — Klarheit und Vermeidung von Redundanzen.

---

## Regel: Serienwechsel ist nicht möglich über nächste Episode

**Beschreibung:** Wenn ein Benutzer die letzte Episode der letzten Staffel einer Serie zu Ende schaut, wird keine nächste Episode vorgeschlagen. Die Serie gilt dann als abgeschlossen.

**Bedingungen:**
- Benutzer schaut die letzte Episode einer Serie
- Es gibt keine weitere Staffel nach der aktuellen
- Oder die nächste Staffel hat keine Episoden

**Verhalten:**
- `GetNextEpisodeAsync()` gibt `null` zurück
- `ProcessBufferedEntryAsync()` entfernt die letzte Episode aus der "Weiterschauen"-Liste
- Kein neuer Eintrag wird hinzugefügt
- Die Anwendung zeigt optional "Serie abgeschlossen" oder dergleichen

**Umsetzung:** `ContinueWatchingService.GetNextEpisodeAsync()` (Zeilen 362–376):
```csharp
if (nextSeason is null) return null;  // Keine Staffel vorhanden
next = await _db.TVShowEpisodes.AsNoTracking()
    .Where(e => e.TVShowSeasonId == nextSeason.Id)
    .OrderBy(e => e.Number)
    .Select(e => e.Id)
    .FirstOrDefaultAsync(ct);
if (next == 0) return null;  // Staffel hat keine Episoden
```

**Begründung:** Dies ist das erwartete Verhalten für Serienenenden. Ein Benutzer erwartet nach der letzten Episode einen Abschluss oder eine Bestätigung, nicht eine zufällige nächste Serie.

---

## Regel: Skalierung der Episode-Position nach Dauer

**Beschreibung:** Die Position einer Episode wird in Sekunden (`TimeSpan`) gespeichert. Die Dauer wird ebenfalls in Sekunden gespeichert. Eine Episode wird als "zu Ende angesehen" betrachtet, wenn weniger als 30 Sekunden verbleiben.

**Bedingungen:**
- Benutzer pausiert oder beendet die Wiedergabe
- Position und Dauer sind in Sekunden verfügbar

**Verhalten:**
- Berechnung: `duration - position <= 30 Sekunden`
- Falls wahr: Episode als zu Ende erkannt, nächste Episode ermitteln
- Falls falsch: Position speichern, keine Aktion

**Umsetzung:** `ContinueWatchingService.ProcessBufferedEntryAsync()` (Zeilen 157–159):
```csharp
private static readonly TimeSpan EndThreshold = TimeSpan.FromSeconds(30);
// ...
if (duration - position <= EndThreshold)
{
    // Markiert als abgeschlossen
}
```

**Begründung:** Eine 30-Sekunden-Toleranz berücksichtigt Credits und Outro-Musik, ohne dass der Benutzer bis zum absoluten Ende der Datei schauen muss. Dies ist ein Standard in Streaming-Anwendungen.

---

## Regel: Mindestposition für Puffererkennung

**Beschreibung:** Positionen, die kleiner als 5 Sekunden sind, werden nicht gepuffert. Dies filtert zufällige oder sehr kurze Zuschauer-Interaktionen heraus.

**Bedingungen:**
- Benutzer drückt Play oder öffnet eine Episode
- Position wird gemeldet

**Verhalten:**
- Falls `position < 5 Sekunden`: Eintrag wird ignoriert, nicht gepuffert
- Falls `position >= 5 Sekunden`: Eintrag wird gepuffert und später verarbeitet

**Umsetzung:** `ContinueWatchingService.ReportProgressAsync()` (Zeilen 133–135):
```csharp
private static readonly TimeSpan MinStart = TimeSpan.FromSeconds(5);
// ...
if (position < MinStart) return Task.CompletedTask;
```

**Begründung:** Verhindert Puffer-Überlauf durch kurze Testabrufe oder Ladezeit-Spitzen.

---

## Regel: Eindeutigkeit durch (UserId, MediaId, PlaylistId)

**Beschreibung:** Ein Benutzer kann pro Media (Film oder Episode) pro Playlist nur einen Eintrag in der Weiterschauen-Liste haben. Zusätzlich kann ein Benutzer pro Media eine Variante ohne Playlist-Bezug (PlaylistId = NULL) haben. Kombinationen unterschiedlicher Playlists oder Playlist vs. Non-Playlist sind jedoch unabhängige Einträge.

**Bedingungen:**
- Ein Video wird aus einer Playlist heraus gestartet (PlaylistId gesetzt) oder außerhalb einer Playlist (PlaylistId = NULL)
- Benutzer hat bereits einen Eintrag für dieses Video in derselben Playlist-Kombination

**Verhalten:**
- Das System sucht den vorhandenen Eintrag mit `(UserId, MovieId OR TVShowEpisodeId, PlaylistId)`
- Falls vorhanden: Position wird aktualisiert (Upsert-Verhalten), kein neuer Eintrag wird erstellt
- Falls nicht vorhanden: Ein neuer Eintrag wird erstellt
- Sind verschiedene Playlist-Varianten vorhanden, werden diese als separate Einträge behandelt

**Umsetzung:** `ContinueWatchingService.UpsertAsync()`:
```csharp
var entry = await _db.ContinueWatchingEntries
    .FirstOrDefaultAsync(x => x.UserId == userId 
                           && x.MovieId == nextMovieId 
                           && x.TVShowEpisodeId == nextEpisodeId 
                           && x.PlaylistId == playlistId, ct);

if (entry == null)
{
    // Neuer Eintrag wird erstellt mit dieser PlaylistId
}
else
{
    // Position wird aktualisiert
}
```

**Begründung:** Diese Regel ermöglicht es, dasselbe Video mehrfach in der Weiterschauen-Liste zu haben — einmal pro Playlist plus optional eine Non-Playlist-Variante — und jede Variante individuell zu verwalten (eigener Fortschritt, eigene Ausblendung, eigenes Überspringen).

---

## Regel: Gesehen-Markierung ist playlist-übergreifend

**Beschreibung:** Wenn ein Benutzer ein Video zu Ende schaut (Fortschritt nahe am Ende), werden ALLE Varianten dieses Videos aus der Weiterschauen-Liste entfernt — unabhängig davon, mit welcher Playlist oder ohne Playlist sie verknüpft sind.

**Bedingungen:**
- Benutzer schaut ein Video bis zum Ende (weniger als 30 Sekunden verbleibend)
- Es können mehrere Varianten des Videos in der Liste sein (verschiedene Playlists, mit/ohne Playlist)

**Verhalten:**
- Das System markiert das Video global als „gesehen" (registriert in der `WatchedEntry`-Tabelle)
- Es entfernt alle `ContinueWatchingEntry` für `(UserId, MovieId OR TVShowEpisodeId)` **ohne PlaylistId-Filter**
- Dies geschieht **vor** der Ermittlung des nächsten Videos
- Anschließend wird die nächste Episode/nächster Film mit der **aktuellen** PlaylistId eingefügt (falls vorhanden)

**Umsetzung:** `ContinueWatchingService.ProcessBufferedEntryAsync()` (Zeilen 265–275):
```csharp
// Gesehen-Markierung ist playlist-uebergreifend: ALLE Varianten dieses Videos werden
// entfernt, unabhaengig von ihrer PlaylistId.
var existingEntries = await _db.ContinueWatchingEntries
    .Where(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId)
    .ToListAsync(ct);

if (existingEntries.Count > 0)
{
    _db.ContinueWatchingEntries.RemoveRange(existingEntries);
    await _db.SaveChangesAsync(ct);
}
```

**Begründung:** Die Gesehen-Markierung ist eine globale, benutzerweite Information („dieses Video habe ich gesehen"). Sie sollte nicht playlist-spezifisch sein. Dies verhindert auch Verwirrung: Wenn ein Benutzer das gleiche Video in zwei verschiedenen Playlists zu Ende schaut, würde erwartet, dass es überall als gesehen markiert wird, nicht nur in einer Playlist.

---

## Regel: Ausblenden und Überspringen sind playlist-spezifisch

**Beschreibung:** Die manuellen Aktionen „Ausblenden" und „Überspringen" wirken nur auf die jeweilige Playlist-Variante eines Eintrags.

**Bedingungen:**
- Benutzer klickt „Ausblenden" oder „Überspringen" auf einem Eintrag in der Weiterschauen-Liste
- Der Eintrag hat optional eine PlaylistId

**Verhalten (Ausblenden):**
- Das System sucht den Eintrag mit `(UserId, MovieId OR TVShowEpisodeId, PlaylistId)`
- Falls vorhanden: Dieser eine Eintrag wird gelöscht
- Alle anderen Varianten (mit anderen Playlists oder ohne) bleiben erhalten

**Verhalten (Überspringen):**
- Das System sucht den Eintrag mit `(UserId, MovieId OR TVShowEpisodeId, PlaylistId)`
- Falls vorhanden: Dieser Eintrag wird gelöscht
- Beim Ermitteln der nächsten Media (Episode oder Film) wird aber die **aktuelle Playlist-ID beibehalten**
- Der Eintrag für die nächste Media wird mit derselben PlaylistId erstellt
- Alle anderen Varianten des alten Videos bleiben erhalten

**Umsetzung:** `ContinueWatchingService.HideAsync()` und `SkipAsync()` verwenden beide die PlaylistId in der Eindeutigkeitsabfrage.

**Begründung:** Dies ermöglicht eine granulare Verwaltung: Ein Benutzer kann z. B. ein Video in Playlist A ausblenden, es aber weiterhin in Playlist B und als Non-Playlist-Variante in der Weiterschauen-Liste haben. Dies ist nützlich, wenn der Benutzer den Titel „dort" fortsetzen, aber „hier" nicht mehr sehen möchte.
