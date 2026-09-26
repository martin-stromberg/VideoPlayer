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

**Beschreibung:** Ein Benutzer kann pro Serie nur eine Episode in der "Weiterschauen"-Liste haben.
*Gilt für Einträge **ohne** Playlist-Bezug.* Für Einträge mit `PlaylistId` gilt stattdessen die weitergehende Regel „Genau ein Eintrag je Anwender und Playlist" (siehe unten). Wenn eine neue Episode hinzugefügt wird, werden alle anderen Episoden derselben Serie aus der Liste entfernt.

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
*Gilt für Einträge **ohne** Playlist-Bezug.* Stammt der Eintrag aus einer Playlist, geht es mit dem nächsten Titel dieser Playlist weiter — auch über Seriengrenzen hinweg (siehe unten).

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
// Gesehen-Markierung ist playlist-übergreifend: ALLE Varianten dieses Videos werden
// entfernt, unabhängig von ihrer PlaylistId.
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

---

## Regel: Eindeutige Identifizierung der Weiterschauen-Einträge in der UI durch Entry-ID

**Beschreibung:** Jeder Weiterschauen-Eintrag wird in der Benutzeroberfläche durch seine eindeutige Datenbank-ID (`ContinueWatchingEntry.Id`) identifiziert, nicht durch die Media-ID. Dies ist essentiell, wenn dasselbe Video mehrfach in der Liste erscheint (mit unterschiedlichen Playlists oder ohne).

**Bedingungen:**
- Weiterschauen-Liste wird geladen
- Ein oder mehr Videos erscheinen mehrfach (mit verschiedenen `PlaylistId`-Werten)
- Display-Daten (Titel, Bild, Playlist-Name) müssen eindeutig jeder Variante zugeordnet werden

**Verhalten:**
- Die Eigenschaft `ContinueWatchingDto.Id` wird mit der Datenbank-ID der `ContinueWatchingEntry` befüllt
- In `ContinueWatchingList.razor` werden Display-Data-Dictionaries nach `ContinueWatchingDto.Id` indiziert: `titles[it.Id]`, `images[it.Id]`, `links[it.Id]`, `playlistSubtitles[it.Id]`
- Das Blazor-`@key`-Attribut wird auf `@key="it.Id"` gesetzt, um eindeutige Keys pro Datenbankzeile zu garantieren
- Jeder Eintrag erhält korrekt seine zugehörigen Anzeigedaten, auch wenn mehrere Varianten des gleichen Videos nebeneinander erscheinen

**Umsetzung:** 
- `ContinueWatchingService.GetListAsync()` — Befüllt `ContinueWatchingDto.Id` aus `entry.Id`
- `ContinueWatchingList.razor` — Umindizierung aller Display-Data-Dictionaries auf `it.Id` statt `it.Entry.Id`

**Begründung:** Eine Indizierung nach Media-ID (`Entry.Id`) würde zu Kollisionen führen, wenn das gleiche Video mehrfach mit unterschiedlichen Playlist-Bezügen in der Liste vorhanden ist: mehrere DTOs mit derselben `Entry.Id` würden sich gegenseitig im Dictionary überschreiben, und der Blazor-Renderer würde bei mehreren identischen `@key`-Werten fehlschlagen. Die Entry-ID ist eindeutig pro Datenbankzeile und behebt beide Probleme.

---

## Regel: Konfliktauflösung beim Löschen einer Playlist mit playlist-losem Duplikat

**Beschreibung:** Wenn eine Playlist gelöscht wird und ein oder mehr ihrer Weiterschauen-Einträge ein Duplikat als playlist-loser Eintrag (PlaylistId = NULL) haben, wird das playlist-gebundene Duplikat entfernt statt auf NULL gesetzt. Dies verhindert Unique-Constraint-Verletzungen und bewahrt die Konsistenz der Datenbank.

**Bedingungen:**
- Benutzer löscht eine Playlist
- Für ein oder mehr Videos in dieser Playlist existiert bereits ein playlist-loser Weiterschauen-Eintrag (gleicher User, gleiche Media)
- Die Datenbank erzwingt einen bedingten Unique-Index auf `(UserId, MediaId, NULL)` und `(UserId, TVShowEpisodeId, NULL)`

**Verhalten:**
1. Vor dem eigentlichen Playlist-Löschen: Alle `ContinueWatchingEntry`-Zeilen mit dieser `PlaylistId` werden geladen
2. Für jede Zeile: Es wird geprüft, ob bereits ein playlist-loser Eintrag existiert (`UserId`, `MovieId`/`TVShowEpisodeId`, `PlaylistId = NULL`)
3. Falls ja: Der playlist-gebundene Eintrag wird gelöscht (entfernt aus dem Datenbank-Change-Tracker)
4. Falls nein: Der Eintrag wird wie geplant auf `PlaylistId = NULL` gesetzt (durch die FK-Aktion)
5. Die Playlist wird gelöscht
6. `SaveChangesAsync()` wird aufgerufen → kein Unique-Constraint-Fehler

**Umsetzung:** 
- `PlaylistService.DeletePlaylistAsync()` — Ruft `ResolvePlaylistDeletionConflictsAsync()` auf, bevor die Playlist gelöscht wird
- `ContinueWatchingService.ResolvePlaylistDeletionConflictsAsync()` — Implementiert die Konfliktprüfung und Duplikat-Entfernung

**Begründung:** Das Datenbankschema erzwingt Eindeutigkeit auf playlist-losen Einträgen. Wenn eine Playlist gelöscht wird und ihre Einträge auf NULL gesetzt werden sollen, aber bereits ein NULL-Eintrag für das gleiche Video existiert, schlägt die Operation ohne explizite Konfliktauflösung fehl. Die Entfernung des playlist-gebundenen Duplikats ist das sicherste Verfahren: Sie bewahrt den ursprünglichen playlist-losen Eintrag (einschließlich dessen Wiedergabeposition) und vermeidet gleichzeitig Duplikate.

---

## Regel: Fortschritt auf öffentlichen Playlists gehört dem Betrachter (Schritt 11)

Fortschritt mit `PlaylistId` darf melden, wer die Playlist lesen darf: der Besitzer, oder jeder Anwender, solange die
Playlist öffentlich ist (`ContinueWatchingService.ValidatePlaylistAccessAsync`). Der Eintrag gehört immer dem Melder
(`UserId` = Betrachter); Playlist und Einträge anderer werden nicht verändert. Ist die Playlist privat und gehört
einem anderen (auch nachdem die Kennzeichnung entfernt wurde) → `PlaylistAccessDeniedException` (403); ein bereits
gepufferter Fortschritt wird dann vom Worker verworfen.

**Anzeige:** Playlist-Name und `PlaylistEntryId` (Deep-Link) werden nur für Playlists ermittelt, die der Anwender lesen
darf (Besitzer oder öffentlich).

## Regel: Auswirkungen von Änderungen des Besitzers auf Einträge anderer Anwender (Schritt 11)

- **Titel entfernen** (auch still: Waisen-Bereinigung, Quellenlöschung): jeder betroffene Anwender wird auf den
  nächsten für **ihn** zugänglichen Titel umgehängt oder der Eintrag entfällt; Kollision mit einem vorhandenen Eintrag
  für den Ersatztitel: der vorhandene bleibt. Die Sicherheitsabfrage betrifft nur den eigenen Eintrag des Besitzers.
- **Playlist löschen / Konto des Besitzers löschen:** Konfliktauflösung für alle Anwender (siehe nächste Regel), auch
  wenn ein Anwender über mehrere gelöschte Playlists hinweg dasselbe Video gebunden hat (der zuletzt aktualisierte
  Eintrag überlebt).
- **Kennzeichnung entfernen:** Einträge anderer Anwender verlieren den Playlist-Bezug (`PlaylistId = NULL`), bei
  Konflikt mit einem vorhandenen Eintrag ohne Bezug entfällt der gebundene — atomar mit dem Entfernen der Kennzeichnung.

## Regel: Sicherheitsabfrage beim Entfernen eines Titels mit Weiterschauen-Bezug

**Beschreibung:** Wird ein einzelner Titel aus einer Playlist entfernt, für den in der Weiterschauen-Liste noch ein Eintrag mit Bezug zu genau dieser Playlist existiert, muss der Anwender das Entfernen ausdrücklich bestätigen ("Dieser Eintrag befindet sich in deiner Weiterschauen-Liste. Entfernen?"). Bestätigt er, wird der betroffene Weiterschauen-Eintrag durch den nächsten in dieser Playlist verfügbaren (abspielbaren und zugänglichen) Titel ersetzt; gibt es keinen, wird der Weiterschauen-Eintrag entfernt. Verschwindet ein Titel stattdessen still aus dem Medienbestand (z. B. weil die Datei nicht mehr existiert), entfällt die Sicherheitsabfrage, aber dasselbe Ersetzen-/Entfernen-Verhalten gilt sinngemäß.

**Bedingungen:**
- Ein einzelner `PlaylistEntry` wird entfernt (Benutzeraktion über `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}`, oder das darunterliegende Media verschwindet und der Eintrag wird beim nächsten Laden der Playlist als Waise erkannt)
- Ein `ContinueWatchingEntry` mit `PlaylistId` gleich dieser Playlist referenziert exakt dasselbe Video (`MovieId`/`TVShowEpisodeId`)

**Verhalten (Benutzeraktion):**
1. `PlaylistService.RemoveMediaFromPlaylistAsync()` prüft vor dem Entfernen, ob ein solcher `ContinueWatchingEntry` existiert (`ContinueWatchingService.HasPlaylistBoundEntryAsync()`)
2. Existiert einer und wurde nicht bestätigt (`confirmContinueWatchingRemoval != true`): `ContinueWatchingConfirmationRequiredException` → Controller antwortet mit `409 Conflict` (`DtoRemovePlaylistEntryConflictResponse.IsContinueWatchingConfirmationRequired = true`)
3. Der Client (`PlaylistEntriesList.razor`) zeigt den Bestätigungsdialog (`PlaylistEntryContinueWatchingConfirmationDialog.razor`) und wiederholt den Aufruf bei Bestätigung mit `confirmContinueWatchingRemoval: true`
4. Vor dem eigentlichen Entfernen des `PlaylistEntry` wird der nächste abspielbare und zugängliche Titel der Playlist ab der Position des zu entfernenden Eintrags ermittelt (`PlaylistService.FindAdjacentPlayableEntryAsync()`, dieselbe Logik wie bei der Weiterschauen-Navigation)
5. Der `PlaylistEntry` wird entfernt
6. `ContinueWatchingService.ResolvePlaylistEntryRemovalAsync()` ersetzt den betroffenen `ContinueWatchingEntry` durch den ermittelten nächsten Titel (Position wird auf 0 zurückgesetzt) oder entfernt ihn, falls kein nächster Titel existiert

**Verhalten (stilles Verschwinden aus dem Medienbestand):** Dieselbe Ersetzen-/Entfernen-Logik wird ohne Sicherheitsabfrage ausgelöst, sobald ein Titel aus dem Medienbestand verschwindet. Es gibt dafür zwei produktive Auslöser mit unterschiedlicher Anbindung:

- `PlaylistService.LoadValidPlaylistEntriesAsync()` erkennt verwaiste `PlaylistEntry`-Zeilen (deren referenziertes Medium nicht mehr existiert) beim nächsten Laden einer Playlist und ruft dafür `ResolveOrphanContinueWatchingReplacementsAsync()` auf. Dieser Zweig greift praktisch nur für Einträge, deren Medium auf einem anderen Weg als das komplette Löschen der Medienquelle verschwunden ist (in dieser Anwendung aktuell kein produktiver Fall, da es keinen Einzeltitel-Lösch- oder Bereinigungsmechanismus gibt) — er dient in erster Linie als generische, wiederverwendbare Implementierung des "verwaisten Eintrag ersetzen"-Verhaltens und als Absicherung, falls ein solcher Auslöser künftig hinzukommt.
- `AdminSourcesController.DeleteSource()` → `ApplicationDbContext.DeleteMediaSourceAsync()` ist der einzige tatsächlich erreichbare Auslöser, bei dem Filme/Episoden physisch gelöscht werden (eine komplette Medienquelle wird entfernt). Da `DeleteMediaSourceAsync` alle zugehörigen `ContinueWatchingEntries` **bevor** den Film-/Episodenzeilen selbst unbedingt löscht, ruft es zuvor, innerhalb derselben Transaktion, `IPlaylistService.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync()` auf: Für jeden playlist-gebundenen Weiterschauen-Eintrag, dessen Medium zur zu löschenden Quelle gehört, wird der nächste in derselben Playlist verfügbare Titel gesucht — ausdrücklich unter Ausschluss aller Titel derselben (ebenfalls verschwindenden) Quelle — und der Eintrag entsprechend umgehängt oder, falls kein Ersatztitel existiert, entfernt. Playlists können Titel aus mehreren Medienquellen mischen; ein Ersatztitel aus einer anderen, nicht gelöschten Quelle bleibt also ein real erreichbarer, nicht nur theoretischer Fall.

**Kollisionsfall beim Ersetzen:** Existiert für den Ersatztitel in derselben Playlist bereits ein eigener `ContinueWatchingEntry` (z. B. weil der Benutzer ihn separat schon einmal angesehen hat), würde ein Umhängen den Unique-Index `(UserId, MovieId/TVShowEpisodeId, PlaylistId)` verletzen. Analog zur bereits dokumentierten Konfliktauflösung beim Löschen einer ganzen Playlist bleibt der bereits vorhandene Eintrag (mit seinem echten Fortschritt) unverändert bestehen; der zu ersetzende Eintrag wird stattdessen entfernt statt umgehängt.

**Umsetzung:**
- `PlaylistService.RemoveMediaFromPlaylistAsync()` — Sicherheitsabfrage, Ermittlung des Ersatztitels, Aufruf der Ersetzen-/Entfernen-Logik
- `PlaylistService.LoadValidPlaylistEntriesAsync()` / `ResolveOrphanContinueWatchingReplacementsAsync()` — stiller Zweig ohne Sicherheitsabfrage für verwaiste `PlaylistEntry`-Zeilen
- `PlaylistService.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync()` — stiller Zweig für den Medienquellen-Löschfall; wird von `ApplicationDbContext.DeleteMediaSourceAsync()` über einen Hook (`beforeContinueWatchingCleanupAsync`) innerhalb derselben Transaktion aufgerufen, bevor dort `ContinueWatchingEntries` bedingungslos gelöscht werden. Ermittelt den Ersatztitel je betroffener Playlist analog zu `ResolveOrphanContinueWatchingReplacementsAsync()`, schließt dabei aber zusätzlich alle Titel der zu löschenden Quelle selbst als Kandidaten aus
- `ContinueWatchingService.HasPlaylistBoundEntryAsync()` / `ResolvePlaylistEntryRemovalAsync()` — Prüfung bzw. Ersetzen/Entfernen des Weiterschauen-Eintrags, inkl. Kollisionsauflösung
- `ContinueWatchingConfirmationRequiredException`, `DtoRemovePlaylistEntryConflictResponse` — Sicherheitsabfrage-Mechanik, analog zu `ManualSortOrderConfirmationRequiredException`/`DtoChangeSortModeConflictResponse` beim Sortiermodus-Wechsel
- `PlaylistEntryContinueWatchingConfirmationDialog.razor` — Bestätigungsdialog in `PlaylistEntriesList.razor`

**Begründung:** Ein Weiterschauen-Eintrag, der stillschweigend auf ein nicht mehr in der Playlist vorhandenes Video zeigt, würde beim Fortsetzen ins Leere laufen oder verwirrende Ergebnisse liefern. Das Ersetzen durch den nächsten verfügbaren Titel erhält den "roten Faden" beim Weiterschauen innerhalb der Playlist; wo das nicht möglich ist, ist ein sauberes Entfernen die einzige konsistente Alternative. Die Sicherheitsabfrage nur bei der Benutzeraktion (nicht beim stillen Verschwinden) entspricht dem Prinzip, dass nur eine vom Anwender selbst ausgelöste, überraschende Datenänderung eine explizite Bestätigung erfordert.

---

## Regel: Genau ein Eintrag je Anwender und Playlist

**Beschreibung:** Ein `ContinueWatchingEntry` mit gesetzter `PlaylistId` ist je (`UserId`, `PlaylistId`)
höchstens einmal vorhanden. Eine Playlist wird als Einheit weitergeschaut.

**Bedingungen:**
- Ein Fortschritt wird für eine Playlist geschrieben (Fortschrittsmeldung, Nachfolger nach der
  Endsequenz, Überspringen)
- Es können bereits Einträge derselben Playlist für andere Titel bestehen

**Verhalten:**
- Alle übrigen Einträge desselben Anwenders mit derselben `PlaylistId` werden entfernt — unabhängig von
  Serie, Staffel oder Filmsammlung
- Einträge mit `PlaylistId = NULL` und Einträge anderer Playlists bleiben unberührt
- Die Regel gilt je Anwender; Besitzer und Betrachter einer öffentlichen Playlist haben jeweils ihren
  eigenen einen Eintrag

**Umsetzung:** `ContinueWatchingService.RemoveOtherEntriesOfPlaylistAsync()`, aufgerufen aus
`UpsertAsync()` (auch beim reinen Aktualisieren, damit Altbestände mit mehreren Einträgen je Playlist
beim nächsten Schreiben verschwinden) und aus `SkipAsync()` über `RemoveSupersededEntriesAsync()`.

**Begründung:** Zwei Einträge derselben Playlist beantworten die Frage „wo war ich in dieser Playlist?"
widersprüchlich. Wer aus einer Playlist einen anderen Titel aufruft, hat den bisherigen verlassen.

**Erneutes Anspielen:** Wird ein bereits beendeter Titel derselben Playlist wieder angespielt
(Zurückspulen und Pausieren, Schließen des Players), wird er wieder zum einen Eintrag der Playlist und
der zuvor eingefügte Nachfolger entfällt. Gewollt: Der letzte Schreiber gewinnt, und ohne Playlist-Bezug
gilt dasselbe (ein Eintrag je Serie). Die Gesehen-Markierung bleibt dabei bestehen.

**Anzeige:** `GetListAsync` gibt je Playlist nur den zuletzt aktualisierten Eintrag aus, damit Altbestand
mit mehreren Zeilen je Playlist nicht als mehrere Kacheln erscheint. Beim Lesen wird nichts gelöscht.

---

## Regel: Nachfolger eines Playlist-Eintrags ist der nächste Titel der Playlist

**Beschreibung:** Ist ein Eintrag an eine Playlist gebunden, wird sein Nachfolger aus dieser Playlist
bestimmt, nicht aus der Serien- oder Sammlungsreihenfolge.

**Bedingungen:**
- Die Endsequenz eines aus der Playlist gestarteten Titels ist erreicht
  (`ProgramSettings.GetContinueWatchingEndThresholdAsync()`, Standard 30 Sekunden), **oder**
- der Anwender wählt „Überspringen" für einen Eintrag mit `PlaylistId`

**Verhalten:**
- Der aktuelle Titel wird über (`PlaylistId`, `MediaType`, `MediaId`) auf seinen `PlaylistEntry`
  abgebildet (je Playlist eindeutig)
- `IPlaylistService.GetNextPlaylistEntryAsync()` liefert den nächsten abspielbaren und für diesen
  Anwender zugänglichen Eintrag in der aktuellen Sortierung der Playlist; nicht abspielbare
  Sammel-Einträge und gesperrte Titel werden übersprungen, aus der Playlist entfernte Titel kommen gar
  nicht erst vor
- Der neue Eintrag ist wieder an dieselbe Playlist gebunden, Position `0`
- Gibt es keinen Nachfolger, wird der Eintrag ersatzlos entfernt — auch dann, wenn die Serie selbst
  weiterginge
- Ist die Playlist nicht mehr lesbar oder gelöscht, wird kein Nachfolger ermittelt (kein Fehler)
- Gehört der gemeldete Titel gar nicht (mehr) zur Playlist, verliert die Meldung ihren Playlist-Bezug:
  Der Titel bekommt einen eigenen Eintrag mit `PlaylistId = NULL` und fällt unter die Regeln ohne
  Playlist; der eine Eintrag der Playlist bleibt unverändert
  (`ContinueWatchingService.NormalizePlaylistBindingAsync`)
- Der Nachfolger wird innerhalb der Endsequenz nur einmal ermittelt, nicht bei jeder der rund zehn
  Fortschrittsmeldungen (`ShouldResolveSuccessorAsync`)

**Umsetzung:** `ContinueWatchingService.ResolveNextMediaAsync()` /
`ResolvePlaylistSuccessorAsync()`, genutzt von `ProcessBufferedEntryAsync()` und `SkipAsync()`.

**Begründung:** Ein Eintrag, der aus einer Playlist entstanden ist, muss die gesamte Playlist
berücksichtigen: Serien-Fortsetzungen in derselben Playlist gehen weiter, bewusst ausgeschlossene Titel
werden nicht wieder angeboten.
