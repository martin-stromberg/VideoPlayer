# Anforderung: Korrekturen Playlist-Wiedergabe (Schritt 5, Runde 2)

## Fachliche Zusammenfassung

Die Playlist-Wiedergabefunktion (Schritt 5) weist drei kritische Fehler auf, die die Bedienbarkeit beeinträchtigen und das Verhalten von spezifizierten Szenarien nicht erfüllen:

1. **Falsche Endeverarbeitung beim Navigieren am Anfang:** Navigation zum "Vorheriger"-Eintrag am Anfang der Playlist triggert fälschlicherweise die Meldung "Ende der Playlist erreicht." (statt entweder keine Meldung oder eine korrekte Anfang-Meldung).
2. **Abspielen gesperrter Einträge führt in Sackgasse:** Der "Abspielen"-Button wird auf nicht freigeschalteten (`IsAccessible == false`) Einträgen angeboten; ein Klick erzeugt 403 und ersetzt die komplette Detailseite durch Fehlerbox (statt Button zu deaktivieren und Fehler inline zu zeigen).
3. **Doppelklick auf Sammel-Einträge startet falschen Inhalt:** Doppelklick auf nicht-abspielbare Zeilen (Serie/Staffel/Filmsammlung) startet Wiedergabe mit falscher Medien-ID statt den Fehler korrekt zu behandeln oder die Aktion zu sperren.

Die Kernmechanik (Start ab beliebigem Eintrag, Auto-Advance, manuelle Navigation, Überspringen gesperrter Einträge, Positionsanzeige) funktioniert bereits und darf nicht beeinträchtigt werden.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor`
  - Methode `ApplyPlaylistNavigationResultAsync` (Zeile ~309): Muss Richtung (forward/backward) unterscheiden
  - Feld `playlistEndReached` (Zeile ~104): Nur noch am tatsächlichen Ende setzen
  - Feld `playlistNavigationErrorMessage` (Zeile ~105): Bereits vorhanden, wird verwendet
  - `@if (playlistEndReached)` Block (Zeile ~40–46): Nur sichtbar bei echtem Ende

- `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor`
  - Methode `IsPlayableEntry` (Zeile ~306): Muss zusätzlich `entry.IsAccessible` prüfen
  - `@ondblclick` auf Zeile (Zeile ~59): Soll nur auf abspielbaren und zugänglichen Zeilen aktiv sein
  - Rendering des "Abspielen"-Buttons (Zeile ~68–71): Logik schon vorhanden, korrekt durch `IsPlayableEntry` gated

- `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`
  - Methode `StartPlaybackAsync` (Zeile ~150–165): Fehlerbehandlung muss Fehler nur im `playbackStart` setzen, nicht im `loadError`
  - Rendering der Fehler-UI (Zeile ~23–25): Muss getrennte Fehler-Ebenen für Laden vs. Wiedergabe unterstützen
  - Feld `loadError` (Zeile ~126): Nur noch für Fehler beim Laden der Playlist-Metadaten
  - Neues Feld `playbackError` (Zeile ~126ff): Für Fehler beim Starten der Wiedergabe

### Service-Klassen
- `VideoWebPlayer/Services/PlaylistService.cs`
  - Methode `ResolveExplicitStartEntry` (Zeile ~924–934): Muss zusätzlich `PlaylistEntryMediaTypeResolver.IsPlayable` prüfen, wie `ResolveFirstPlayableEntry` und `FindAdjacentPlayableEntryAsync` es tun
  - Methode `StartPlaylistAsync` (Zeile ~883–913): Ruft `ResolveExplicitStartEntry` auf; Fehler sollen sich entsprechend ändern

### Datenmodelle
- `VideoWebPlayer/Client/Models/DtoPlaylistNavigationResult.cs`
  - Feld `Position` (bereits vorhanden): Korrekt verwendet
  - Neues optionales Feld `Direction` (sofern gewünscht; alternative: übergeben über einen separate boolean an `ApplyPlaylistNavigationResultAsync`)
    - Optional: Als Kontext durchreichen, damit `ApplyPlaylistNavigationResultAsync` unterscheiden kann

### Tests
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Playback.cs`
  - Test für `ResolveExplicitStartEntry` mit nicht-abspielbarer ID (TVShow, TVShowSeason, MovieCollection)
  - Test für `ResolveExplicitStartEntry` mit nicht-zugänglicher ID
  
- `VideoWebPlayer.Tests/Components/PlaylistDetailTests.cs` (falls vorhanden) oder neue Datei
  - Unit-Test: `StartPlaybackAsync` mit 403 setzt `playbackError`, nicht `loadError`
  - Unit-Test: Fehlerbehandlung bei Start-Fehler ist getrennt von Lade-Fehler

- `VideoWebPlayer.Tests/Components/PlaylistEntriesListTests.cs` (falls vorhanden) oder neue Datei
  - Unit-Test: `IsPlayableEntry` prüft `entry.IsAccessible`
  - Unit-Test: Nicht zugängliche Zeile zeigt keinen "Abspielen"-Button
  - Unit-Test: Nicht abspielbare Zeile (TVShow) zeigt keinen "Abspielen"-Button

- E2E-Tests (neue oder erweitert)
  - `VideoPlayerPreviousAtStartE2ETest`: Klick auf "Vorheriger" am ersten Titel zeigt keine "Ende erreicht"-Meldung
  - `PlaylistLockedEntryPlayButtonSackgasseE2ETest`: Klick auf "Abspielen" bei gesperrtem Eintrag erzeugt Fehler in Fehlerbox, nicht Detailseiten-Ersatz
  - `PlaylistDoubleClickCollectionEntryE2ETest`: Doppelklick auf Sammel-Eintrag (Serie/Staffel/Filmsammlung) zeigt Fehler oder hat keine Wirkung

## Implementierungsansatz

### Punkt 1: Richtungsunabhängige "Ende"-Meldung

**Ort:** `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor`

- `OnNextPlaylistEntryAsync()` und `OnPreviousPlaylistEntryAsync()` rufen beide `ApplyPlaylistNavigationResultAsync(result)` auf. 
- Änderung: Übergabe der Navigationsrichtung (z. B. `forward: bool`) an `ApplyPlaylistNavigationResultAsync`.
- Logik in `ApplyPlaylistNavigationResultAsync`:
  - `result is null && forward`: `playlistEndReached = true` (weiterhin)
  - `result is null && !forward`: `playlistEndReached = false`, keine Meldung oder alternative Meldung (z. B. `playlistBeginningReached = true`)
  - `result is not null`: `playlistEndReached = false` (weiterhin)

Lösungsvarianten:
- **Variante A (einfach):** Keine Meldung am Anfang, Button wird stumm ignoriert.
- **Variante B (ausdrücklich):** Separate `playlistBeginningReached`-Flag + HTML-Block für "Anfang der Playlist erreicht." (ohne "Neu starten"-Button oder mit unterschiedlicher Aktion).

**Empfehlung:** Variante A (Anforderung erlaubt "ohne Fehlermeldung"), aber Buttons sollten deaktiviert werden, wenn die Grenzen erreicht sind. Hierzu kann `playlistEndReached` / `playlistBeginningReached` auch den Button-State steuern.

### Punkt 2: "Abspielen"-Button nur bei zugänglichen Einträgen + bessere Fehlerbehandlung

**Ort 1:** `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor`

- Methode `IsPlayableEntry(entry)` erweitern:
  ```csharp
  private static bool IsPlayableEntry(DtoPlaylistEntry entry)
      => PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType) && entry.IsAccessible;
  ```
- `@ondblclick` ungating: Analog zum "Abspielen"-Button conditional rendering nur wenn `IsPlayableEntry(entry)` wahr.

**Ort 2:** `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`

- Trennung von Lade- und Wiedergabe-Fehlern:
  - Behalte `loadError` für Fehler beim Laden der Playlist-Metadaten (Zeile ~195–218 in `LoadPlaylistAsync`).
  - Neues Feld `playbackError` für Fehler beim Starten der Wiedergabe.
  
- Rendering anpassen:
  ```html
  @if (isLoading)
  {
      <p>Lade Daten...</p>
  }
  else if (!string.IsNullOrWhiteSpace(loadError))
  {
      <div class="alert alert-danger">@loadError</div>
  }
  else if (playlist is not null)
  {
      <!-- Playlist-Details und Inhalte -->
      
      @if (!string.IsNullOrWhiteSpace(playbackError))
      {
          <div class="alert alert-danger">@playbackError</div>
      }
      
      <!-- PlaylistEntriesList, etc. -->
  }
  ```

- `StartPlaybackAsync` anpassen:
  ```csharp
  private async Task StartPlaybackAsync(long? entryId)
  {
      try
      {
          playbackStart = await PlaylistClient.StartPlaylistAsync(Id, entryId);
          playbackStart.StreamUrl = $"{playbackStart.StreamUrl}?access_token={Client.AuthorizationToken}";
          NavigationManager.NavigateTo($"/playlists/{Id}?entryId={playbackStart.CurrentEntryId}", replace: true);
          playbackError = null;  // Fehler zurücksetzen bei Erfolg
      }
      catch (Exception ex)
      {
          Logger.LogWarning(ex, "Fehler beim Starten der Wiedergabe von Playlist {PlaylistId}", Id);
          playbackError = $"Fehler beim Starten der Wiedergabe: {ex.Message}";
          // loadError bleibt unverändert, damit die Detailseite weiterhin sichtbar ist
      }
  }
  ```

### Punkt 3: `ResolveExplicitStartEntry` mit Abspielbarkeits- und Zugriffsprüfung

**Ort:** `VideoWebPlayer/Services/PlaylistService.cs`

- Methode `ResolveExplicitStartEntry` (Zeile ~924) erweitern:
  ```csharp
  private static PlaylistEntry ResolveExplicitStartEntry(
      List<PlaylistEntry> sortedEntries, long entryId, Dictionary<PlaylistEntry, bool> accessibilityByEntry)
  {
      var entry = sortedEntries.FirstOrDefault(e => e.Id == entryId)
          ?? throw new KeyNotFoundException("Der angegebene Eintrag gehoert nicht zu dieser Playlist.");

      // Neu: Abspielbarkeit prüfen (wie in ResolveFirstPlayableEntry und FindAdjacentPlayableEntryAsync)
      if (!PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType))
          throw new InvalidOperationException("Der angegebene Eintrag ist nicht abspielbar.");

      // Bestehendes: Zugriff prüfen
      if (!accessibilityByEntry.TryGetValue(entry, out var isAccessible) || !isAccessible)
          throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diesen Eintrag.");

      return entry;
  }
  ```

- Fehlerbehandlung im API-Controller prüfen (falls vorhanden), um `InvalidOperationException` korrekt zu mappen (z. B. als 400 Bad Request statt 500).

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Alle Änderungen sind reine Logik-Korrekturen.

## Offene Fragen

1. **Punkt 1 – Anfang-Feedback:** Soll bei Erreichen des Playlist-Anfangs über "Vorheriger" gar keine Meldung erscheinen (nur Button deaktivieren), oder eine separate "Anfang erreicht"-Meldung analog zur "Ende"-Meldung? 
   - Anforderung: "entweder gar keine Meldung ... oder eine fachlich korrekte, eigene Meldung"
   - Empfehlung: Keine Meldung, nur Buttons deaktivieren (schlankere UX).

2. **Punkt 2 – Fehlerbehandlung im Detail:** Soll der `playbackError` nach dem Schließen des Players automatisch gelöscht werden, oder bleibt er stehen, bis eine erfolgreiche Wiedergabe erfolgt?
   - Empfehlung: Beim Schließen des Players (`ClosePlayerAsync`) zurücksetzen.

3. **Punkt 3 – Error-Response:** Welcher HTTP-Status sollte für "nicht-abspielbare Einträge" bei `ResolveExplicitStartEntry` genutzt werden? 
   - `400 Bad Request` (semantischer Fehler in der Anfrage)?
   - `403 Forbidden` (analog zur Zugriffsverweigerung)?
   - `404 Not Found` (Eintrag ist "spielbar" Sicht nicht vorhanden)?
   - Empfehlung: `400 Bad Request`, da es ein Client-Fehler ist (falsche Auswahl des Benutzers / falsche Daten in der UI).

4. **Tests:** Sollen die neuen Tests in bestehenden Test-Dateien ergänzt oder separate Dateien angelegt werden?
   - Empfehlung: In bestehenden Dateien wie `PlaylistServiceTests_Playback.cs`, `PlaylistEntriesListTests.cs` ergänzen; E2E-Tests in separate Dateien aufteilen.

5. **UI-Buttons Status:** Sollen die "Vorheriger"/"Nächster"-Buttons im Player deaktiviert werden, wenn die Grenzen erreicht sind, oder nur visuell mit Tooltip?
   - Empfehlung: Disabled-Attribut + Tooltip für bessere Bedienbarkeit.
