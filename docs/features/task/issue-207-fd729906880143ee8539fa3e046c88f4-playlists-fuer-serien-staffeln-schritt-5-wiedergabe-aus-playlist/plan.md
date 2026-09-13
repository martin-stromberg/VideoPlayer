# Umsetzungsplan: Korrektionen Playlist-Wiedergabe (Schritt 5, Runde 2)

## Übersicht

Die Playlist-Wiedergabefunktion weist drei kritische Fehler auf, die die Bedienbarkeit beeinträchtigen. Der Plan adressiert diese durch gezielte Korrektionen in vier Bereichen: Richtungserkennung bei der Playlist-Navigation, Zugriffsvalidierung bei der Button-Anzeige, Fehlerbehandlung beim Wiedergabestart und Abspielbarkeitsvalidierung beim expliziten Eintrag-Start. Die Änderungen sind isoliert auf die betroffenen Komponenten (`VideoPlayer.razor`, `PlaylistEntriesList.razor`, `PlaylistDetail.razor`) und die Service-Schicht (`PlaylistService.cs`) beschränkt und beeinflussen nicht die bestehende Kernmechanik (Auto-Advance, manuelle Navigation, Filterung gesperrter Einträge).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Anfang-Feedback bei Playlist-Navigation** | Keine Meldung am Anfang; Button wird stumm ignoriert oder deaktiviert | Anforderung erlaubt "ohne Fehlermeldung". Schlankere UX ohne visuelle Überlastung. Buttons sollten via `disabled`-Attribut und CSS deaktiviert werden, statt zwei separate boolean Flags (`playlistEndReached`/`playlistBeginningReached`) zu pflegen. |
| **Navigationsrichtungs-Kontext** | `Direction` als optionales Feld in `DtoPlaylistNavigationResult` hinzufügen (nicht als separater Parameter) | Erhöht Skalierbarkeit (zukünftig können andere Kontextinformationen folgen), bleibt typsicher, erfordert keine Methodensignatur-Änderungen an `ApplyPlaylistNavigationResultAsync`. |
| **Playback-Fehlerbehandlung** | Separates `playbackError` Feld in `PlaylistDetail` statt Missbrauch von `loadError` | Klare Semantik: `loadError` = Fehler beim Laden der Playlist-Metadaten, `playbackError` = Fehler beim Starten der Wiedergabe. Ermöglicht saubere UI-Trennung und zukünftige Fehlerbehandlung. |
| **Fehler-Bereinigung** | `playbackError` beim Schließen des Players (in `ClosePlayerAsync`) zurücksetzen | Verhindert stale errors und bietet Anwendern eine "frische" Seite bei neuen Versuchen. |
| **HTTP-Status für nicht-abspielbare Einträge** | `400 Bad Request` statt `403 Forbidden` oder `404 Not Found` | Semantisch korrekt (Client-Fehler durch falsche Auswahl), unterscheidet sich von Zugriffsverweigerung, einfach debuggierbar. |

## Programmabläufe

### Ablauf 1: Navigation am Anfang der Playlist

**Ausgangszustand:** Benutzer hat die Playlist bis zum ersten Eintrag navigiert (über `GetPreviousPlaylistEntryAsync` mehrmals).

**Gewünschtes Verhalten:**
1. Benutzer klickt erneut auf "Vorheriger"-Button
2. `OnPreviousPlaylistEntryAsync` wird aufgerufen, ruft `GetPreviousPlaylistEntryAsync` auf
3. Service gibt `null` zurück (keine vorherigen Einträge)
4. `ApplyPlaylistNavigationResultAsync` wird mit `result: null` aufgerufen
5. `ApplyPlaylistNavigationResultAsync` erkennt über das neue `Direction` Feld, dass es sich um **Rückwärtsnavigation** handelt
6. `playlistEndReached` wird `false` gesetzt (kein "Ende"-Meldung)
7. Der "Vorheriger"-Button wird über CSS disabled-State deaktiviert (keine visuelle Aktion)

**Beteiligte Klassen/Komponenten:** `VideoPlayer.razor` (`OnPreviousPlaylistEntryAsync`, `ApplyPlaylistNavigationResultAsync`), `DtoPlaylistNavigationResult` (neues Feld `Direction`), `PlaylistService.cs` (`GetPreviousPlaylistEntryAsync`)

---

### Ablauf 2: Abspielen eines nicht-zugänglichen (gesperrten) Eintrags

**Ausgangszustand:** Playlist wird angezeigt, enthält einen nicht-freigeschalteten Eintrag (`IsAccessible == false`).

**Gewünschtes Verhalten:**
1. "Abspielen"-Button ist auf dem Eintrag **nicht sichtbar** (weil `IsPlayableEntry` jetzt auch `IsAccessible` prüft)
2. Wenn ein Benutzer trotzdem per Doppelklick auf die Zeile versucht, den Eintrag zu starten:
   - `OnPlayEntry` wird **nicht aufgerufen** (weil `@ondblclick` conditional ist)
   - Fehler entsteht nicht
3. Alternativ: Falls noch ein älterer Client `StartPlaylistAsync` mit nicht-zugänglichem Eintrag aufruft:
   - Service wirft `PlaylistAccessDeniedException`
   - UI setzt `playbackError` (nicht `loadError`)
   - Fehler wird inline im Playlist-Detail angezeigt (unter den Einträgen)
   - Detailseite wird nicht durch Fehlerbox ersetzt

**Beteiligte Klassen/Komponenten:** `PlaylistEntriesList.razor` (`IsPlayableEntry`, `@ondblclick` conditional), `PlaylistDetail.razor` (`StartPlaybackAsync`, `playbackError` Feld), `PlaylistService.cs` (Fehlerbehandlung bestehend, keine Änderung nötig)

---

### Ablauf 3: Doppelklick auf Sammel-Eintrag (Serie/Staffel/Filmsammlung)

**Ausgangszustand:** Playlist enthält einen Sammel-Eintrag (z. B. `MediaType == "TVShow"`), der nicht direkt abspielbar ist.

**Gewünschtes Verhalten:**
1. "Abspielen"-Button ist auf dem Eintrag **nicht sichtbar** (weil `IsPlayableEntry` nur `Movie` und `TVShowEpisode` akzeptiert)
2. Wenn Benutzer doppelklickt:
   - `OnPlayEntry` wird **nicht aufgerufen** (weil `@ondblclick` conditional ist)
   - Fehler entsteht nicht
3. Alternativ: Falls ein älterer Client `StartPlaylistAsync` mit Sammel-Eintrag aufruft:
   - `ResolveExplicitStartEntry` prüft jetzt `PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType)`
   - Wirft `InvalidOperationException` wenn nicht abspielbar
   - Service mappt auf `400 Bad Request`
   - UI setzt `playbackError`
   - Fehler wird inline angezeigt (nicht Detailseiten-Ersatz)

**Beteiligte Klassen/Komponenten:** `PlaylistEntriesList.razor` (`IsPlayableEntry`, `@ondblclick` conditional), `PlaylistDetail.razor` (`StartPlaybackAsync`, `playbackError`), `PlaylistService.cs` (`ResolveExplicitStartEntry` mit Abspielbarkeits-Validierung)

---

### Ablauf 4: Fehlerbehandlung beim Wiedergabestart

**Ausgangszustand:** `PlaylistDetail.razor` versucht, eine Wiedergabe zu starten (z. B. nach Doppelklick oder initialen Load mit ungültiger `entryId`).

**Gewünschtes Verhalten:**
1. `StartPlaybackAsync(entryId)` wird aufgerufen
2. Service `PlaylistClient.StartPlaylistAsync` wird aufgerufen
3. Fehler tritt auf (z. B. `HttpRequestException` mit Status 403 oder 400)
4. Exception wird in `catch`-Block abgefangen
5. `playbackError` wird mit Fehlermeldung gesetzt (z. B. "Fehler beim Starten der Wiedergabe: ...")
6. `loadError` bleibt unverändert
7. `NavigateTo` wird **nicht** aufgerufen
8. Playlist-Detail bleibt sichtbar, Fehler wird in Alert-Box **innerhalb** der Detailseite angezeigt
9. Benutzer kann andere Einträge auswählen, ohne Seite neu laden zu müssen

**Beteiligte Klassen/Komponenten:** `PlaylistDetail.razor` (`StartPlaybackAsync`, `playbackError` Feld, Fehler-Rendering), `PlaylistClient` (bestehende API)

---

## Neue Klassen

Keine. Alle erforderlichen Datenmodelle und Exceptions existieren bereits.

---

## Änderungen an bestehenden Klassen

### `DtoPlaylistNavigationResult` (Datenmodellklasse)

- **Neue optionale Eigenschaft:** `Direction` (string?, z. B. "forward" oder "backward") — Kontext für `ApplyPlaylistNavigationResultAsync`, um zwischen Vorwärts- und Rückwärtsnavigation zu unterscheiden. Standard: `null` (rückwärtskompatibel).

---

### `VideoPlayer.razor` (Razor-Komponente)

- **Feld-Änderung (semantisch):** `playlistEndReached` wird nur noch bei Vorwärtsnavigation am Ende auf `true` gesetzt; bei Rückwärtsnavigation am Anfang bleibt es `false`.

- **Neue Methode:** `OnPreviousPlaylistEntryAsync` — wird angepasst, um bei Aufruf von `ApplyPlaylistNavigationResultAsync` die Information `Direction: "backward"` mitzugeben.
  - **Neue Methode:** `OnNextPlaylistEntryAsync` — wird angepasst, um `Direction: "forward"` mitzugeben.
  - **Neue Methode:** `OnMediaEndAsync` — wird angepasst, um `Direction: "forward"` mitzugeben (Auto-Advance).

- **Geänderte Methode:** `ApplyPlaylistNavigationResultAsync` — wird erweitert, um das `Direction`-Feld aus dem Navigations-Ergebnis zu berücksichtigen:
  - Logik: Nur wenn `result is null && Direction == "forward"` → `playlistEndReached = true`
  - Sonst: `playlistEndReached = false`
  - Möglichkeit: Buttons "Vorheriger"/"Nächster" via CSS `disabled`-Klasse steuern, wenn Grenzen erreicht sind.

- **UI-Anpassung (optional):** HTML-Block für "playlistEndReached" wird unverändert gelassen (zeigt weiterhin "Ende der Playlist erreicht."). Allerdings wird dieser Block jetzt nur noch bei echtem Ende (Vorwärtsnavigation) angesteuert.

---

### `PlaylistEntriesList.razor` (Razor-Komponente)

- **Geänderte Methode:** `IsPlayableEntry` — wird erweitert, um zusätzlich `entry.IsAccessible` zu prüfen:
  ```
  return PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType) && entry.IsAccessible;
  ```

- **UI-Anpassung:** `@ondblclick` Direktive wird conditional, sodass Doppelklick nur auf abspielbaren und zugänglichen Zeilen aktiv ist:
  ```html
  @if (IsPlayableEntry(entry))
  {
      <tr @ondblclick="() => OnPlayEntry.InvokeAsync(entry)">
          ...
      </tr>
  }
  else
  {
      <tr>
          ...
      </tr>
  }
  ```
  Oder alternativ: `@ondblclick` wird via `class` mit disabled-Styling versehen, aber Event ist weiterhin bound.

---

### `PlaylistDetail.razor` (Razor-Komponente)

- **Neues Feld:** `playbackError` (string?, private) — Speichert Fehler beim Starten der Wiedergabe. Initialisiert mit `null`.

- **Geänderte Methode:** `StartPlaybackAsync(entryId)` — Error-Handling wird angepasst:
  ```
  try
  {
      playbackStart = await PlaylistClient.StartPlaylistAsync(...);
      ...
      NavigateTo(...);
      playbackError = null;  // Fehler zurücksetzen bei Erfolg
  }
  catch (Exception ex)
  {
      playbackError = $"Fehler beim Starten der Wiedergabe: {ex.Message}";
      // loadError bleibt unverändert
      // NavigateTo wird NICHT aufgerufen
  }
  ```

- **Neue Methode (falls vorhanden):** `ClosePlayerAsync` — wird um eine Zeile erweitert, um `playbackError = null` zu setzen.

- **UI-Anpassung:** Fehler-Rendering wird strukturiert:
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
      
      <!-- PlaylistEntriesList, VideoPlayer, etc. -->
  }
  ```

---

### `PlaylistService.cs` (Service-Klasse)

- **Geänderte Methode:** `ResolveExplicitStartEntry(sortedEntries, entryId, accessibilityByEntry)` — Validierung erweitert:
  ```
  private static PlaylistEntry ResolveExplicitStartEntry(
      List<PlaylistEntry> sortedEntries, 
      long entryId, 
      Dictionary<PlaylistEntry, bool> accessibilityByEntry)
  {
      var entry = sortedEntries.FirstOrDefault(e => e.Id == entryId)
          ?? throw new KeyNotFoundException("Der angegebene Eintrag gehoert nicht zu dieser Playlist.");
      
      // NEU: Abspielbarkeit prüfen
      if (!PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType))
          throw new InvalidOperationException("Der angegebene Eintrag ist nicht abspielbar.");
      
      // Bestehendes: Zugriff prüfen
      if (!accessibilityByEntry.TryGetValue(entry, out var isAccessible) || !isAccessible)
          throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diesen Eintrag.");
      
      return entry;
  }
  ```

- **Exception-Handling:** Der API-Controller (falls vorhanden, z. B. `PlaylistsController`) muss `InvalidOperationException` auf `400 Bad Request` mappen (nicht `500`). Falls der Controller über ein centralisiertes Exception-Mapping verfügt (z. B. Middleware), muss dies dort konfiguriert werden.

---

## Datenbankmigrationen

Keine.

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Bestehende Validierungen in `PlaylistService.ResolveExplicitStartEntry` und `FindAdjacentPlayableEntryAsync` sind ausreichend.

---

## Konfigurationsänderungen

Keine.

---

## Seiteneffekte und Risiken

- **Fehlerbehandlung in `PlaylistClient.StartPlaylistAsync`:** Falls der Client (HTTP-Handler) spezifische Exception-Typen wirft (z. B. `HttpRequestException`), muss `PlaylistDetail.StartPlaybackAsync` diese korrekt abfangen und in `playbackError` konvertieren. Kein Risiko, da bestehender Try-Catch generisch ist.

- **Rückwärtskompatibilität von `DtoPlaylistNavigationResult`:** Das neue `Direction`-Feld ist optional (`string?`) und hat einen Standardwert von `null`. Bestehende Clients, die dieses Feld nicht setzen, funktionieren weiterhin; `ApplyPlaylistNavigationResultAsync` behandelt `null` korrekt (keine Änderung gegenüber heute).

- **Button-Deaktivierung bei Grenzen:** Wenn `disabled`-Attribute auf Buttons gesetzt werden, müssen auch entsprechende CSS-Styles vorhanden sein (z. B. `button:disabled { opacity: 0.5; cursor: not-allowed; }`). Dies wird in bestehenden Stylesheets erwartet.

- **Doppelklick-Conditional:** Die Änderung von `@ondblclick` zu einer conditionalen Bindung könnte Performance-Auswirkungen haben, wenn die Playlist sehr lang ist. Dies ist vernachlässigbar, da Razor bereits Row-by-Row rendert; keine signifikanten Seiteneffekte.

- **Keine bekannten Seiteneffekte auf andere Features:** Die Änderungen sind isoliert auf die Playlist-Wiedergabe und beeinflussen nicht Navigation, Suchliste, Media-Verwaltung oder andere Bereiche.

---

## Umsetzungsreihenfolge

1. **Datenmodell: `Direction` Feld in `DtoPlaylistNavigationResult` hinzufügen**
   - Voraussetzungen: Keine (Feld ist optional, keine Dependency-Injection oder Services erforderlich)
   - Beschreibung: Eigenschaft `public string? Direction { get; set; }` zu `DtoPlaylistNavigationResult.cs` hinzufügen. Rückwärtskompatibel (nullable). Wert kann "forward", "backward" oder `null` sein.

2. **Service: `PlaylistService.ResolveExplicitStartEntry` um Abspielbarkeits-Prüfung erweitern**
   - Voraussetzungen: Keine (Utility `PlaylistEntryMediaTypeResolver` ist bereits vorhanden)
   - Beschreibung: In `ResolveExplicitStartEntry` eine Prüfung `if (!PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType)) throw new InvalidOperationException(...)` hinzufügen. Exception-Mapping im Controller ist Verantwortung des API-Layers (nicht Teil dieses Schritts, aber vor Tests zu prüfen).

3. **UI: `PlaylistEntriesList.IsPlayableEntry` um `IsAccessible` Prüfung erweitern**
   - Voraussetzungen: Keine (Property `IsAccessible` existiert bereits in `DtoPlaylistEntry`)
   - Beschreibung: `IsPlayableEntry` zu `return PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType) && entry.IsAccessible;` ändern. Dadurch werden "Abspielen"-Buttons automatisch auf nicht-zugänglichen Einträgen ausgeblendet.

4. **UI: `PlaylistEntriesList.razor` `@ondblclick` conditional machen**
   - Voraussetzungen: Schritt 3 abgeschlossen (damit `IsPlayableEntry` die richtige Logik hat)
   - Beschreibung: `@ondblclick` Direktive wird nur auf Zeilen gebunden, wenn `IsPlayableEntry(entry)` wahr ist. Entweder durch separate `<tr>`-Blöcke im `@if` oder durch inline-Conditional.

5. **UI: `PlaylistDetail` `playbackError` Feld hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Privates Feld `string? playbackError = null;` zur Komponente hinzufügen. Initialisiert mit `null`.

6. **UI: `PlaylistDetail.StartPlaybackAsync` Error-Handling anpassen**
   - Voraussetzungen: Schritt 5 abgeschlossen
   - Beschreibung: In `catch`-Block: `playbackError` statt `loadError` setzen. `NavigateTo` nicht aufrufen bei Fehler. Optional: `playbackError = null;` bei Erfolg setzen.

7. **UI: `PlaylistDetail.razor` Fehler-Rendering strukturieren**
   - Voraussetzungen: Schritt 5 und 6 abgeschlossen
   - Beschreibung: HTML-Rendering ändern, um `playbackError` inline unter Playlist-Details zu zeigen (nicht als Top-Level-Alert, der Detailseite ersetzt).

8. **UI: `VideoPlayer.razor` `ApplyPlaylistNavigationResultAsync` anpassen**
   - Voraussetzungen: Schritt 1 abgeschlossen (Feld `Direction` existiert)
   - Beschreibung: Logik in `ApplyPlaylistNavigationResultAsync`:
     - `playlistEndReached` nur auf `true` setzen, wenn `result is null && result?.Direction == "forward"` (oder `Direction` nicht "backward")
     - Sonst `false` setzen.
     - Buttons "Vorheriger"/"Nächster" optional mit `disabled`-CSS-Klasse versehen (abhängig von `playlistEndReached` / neuer Flag `playlistBeginningReached`).

9. **UI: `VideoPlayer.razor` `OnPreviousPlaylistEntryAsync` und `OnNextPlaylistEntryAsync` anpassen**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Vor Aufruf von `ApplyPlaylistNavigationResultAsync` wird das `Direction`-Feld in der `result` gesetzt (oder alternativ: Methode erhält ein Parameter `direction` und leitet diesen weiter). Für `OnNextPlaylistEntryAsync` und Auto-Advance: `Direction = "forward"`. Für `OnPreviousPlaylistEntryAsync`: `Direction = "backward"`.

10. **Service: Exception-Mapping in API-Controller (falls erforderlich)**
    - Voraussetzungen: Schritt 2 abgeschlossen
    - Beschreibung: Falls vorhanden: `InvalidOperationException` aus `ResolveExplicitStartEntry` wird auf `400 Bad Request` gemappt (z. B. in globaler Exception-Handling-Middleware). Falls nicht vorhanden: Übersprungen (Service lädt Exception auf Client).

11. **Tests: `PlaylistServiceTests_Playback.cs` erweitern**
    - Voraussetzungen: Schritt 2 abgeschlossen
    - Beschreibung: Zwei neue Testmethoden:
      - `StartPlaylistAsync_ExplicitEntryNotPlayable_TVShow_ThrowsInvalidOperationException`
      - `StartPlaylistAsync_ExplicitEntryNotPlayable_TVShowSeason_ThrowsInvalidOperationException`

12. **Tests: `PlaylistEntriesListTests.cs` erweitern (oder neue Datei)**
    - Voraussetzungen: Schritt 3 abgeschlossen
    - Beschreibung: Neue Testmethoden:
      - `IsPlayableEntry_WithNotAccessibleEntry_ReturnsFalse`
      - `IsPlayableEntry_WithMovie_AndAccessible_ReturnsTrue`

13. **Tests: `PlaylistDetailTests.cs` (falls nicht vorhanden, anlegen)**
    - Voraussetzungen: Schritt 6 abgeschlossen
    - Beschreibung: Neue Testmethoden (Unit-Tests mit Mocks):
      - `StartPlaybackAsync_WithHttpError403_SetPlaybackError` — Prüft, dass `playbackError` bei 403 gesetzt wird, `loadError` aber unverändert bleibt
      - `StartPlaybackAsync_WithHttpError400_SetPlaybackError` — Prüft, dass `playbackError` bei 400 gesetzt wird

14. **E2E-Tests: `VideoPlayerPreviousAtStartE2ETest` (neu)**
    - Voraussetzungen: Schritt 8 und 9 abgeschlossen
    - Beschreibung: E2E-Test — Playlist wird geladen, Navigation zum ersten Eintrag, Klick auf "Vorheriger" triggert **keine** "Ende der Playlist erreicht"-Meldung.

15. **E2E-Tests: `PlaylistLockedEntryPlayButtonE2ETest` (neu)**
    - Voraussetzungen: Schritt 3, 4, 6, 7 abgeschlossen
    - Beschreibung: E2E-Test — Playlist mit gesperrtem Eintrag wird geladen. "Abspielen"-Button ist nicht sichtbar auf dem Eintrag. Doppelklick auf die Zeile hat keine Wirkung.

16. **E2E-Tests: `PlaylistDoubleClickCollectionEntryE2ETest` (neu)**
    - Voraussetzungen: Schritt 3, 4 abgeschlossen
    - Beschreibung: E2E-Test — Playlist mit Sammel-Eintrag (TVShow/Staffel/Collection) wird geladen. "Abspielen"-Button ist nicht sichtbar. Doppelklick hat keine Wirkung.

17. **Integrations-Test: `PlaylistLockedEntryPlayBackendE2ETest` (optional aber empfohlen)**
    - Voraussetzungen: Schritt 6, 10 abgeschlossen
    - Beschreibung: E2E-Test — Playlist wird geladen, API wird manuell mit gesperrtem Eintrag aufgerufen → `playbackError` wird angezeigt (nicht Detailseiten-Ersatz durch Error-Box).

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `StartPlaylistAsync_ExplicitEntryNotPlayable_TVShow_ThrowsInvalidOperationException` | `PlaylistServiceTests_Playback` | `ResolveExplicitStartEntry` wirft `InvalidOperationException` wenn Eintrag eine TVShow ist (nicht abspielbar) |
| `StartPlaylistAsync_ExplicitEntryNotPlayable_TVShowSeason_ThrowsInvalidOperationException` | `PlaylistServiceTests_Playback` | `ResolveExplicitStartEntry` wirft `InvalidOperationException` wenn Eintrag eine TVShowSeason ist |
| `StartPlaylistAsync_ExplicitEntryNotPlayable_MovieCollection_ThrowsInvalidOperationException` | `PlaylistServiceTests_Playback` | `ResolveExplicitStartEntry` wirft `InvalidOperationException` wenn Eintrag eine MovieCollection ist |
| `IsPlayableEntry_WithNotAccessibleEntry_ReturnsFalse` | `PlaylistEntriesListTests` | `IsPlayableEntry` gibt `false` zurück, wenn `entry.IsAccessible == false`, auch wenn MediaType abspielbar ist |
| `IsPlayableEntry_WithNotAccessibleEntryAndNonPlayableMediaType_ReturnsFalse` | `PlaylistEntriesListTests` | `IsPlayableEntry` gibt `false` zurück, wenn beide Bedingungen verletzt sind |
| `StartPlaybackAsync_WithHttpError403_SetPlaybackError` | `PlaylistDetailTests` (neu) | `StartPlaybackAsync` setzt `playbackError` bei 403-Fehler, `loadError` bleibt `null` |
| `StartPlaybackAsync_WithHttpError400_SetPlaybackError` | `PlaylistDetailTests` (neu) | `StartPlaybackAsync` setzt `playbackError` bei 400-Fehler |
| `StartPlaybackAsync_OnSuccess_Clears PlaybackError` | `PlaylistDetailTests` (neu) | `StartPlaybackAsync` setzt `playbackError = null` bei erfolgreichem Start |
| `VideoPlayerPreviousAtStartE2ETest` | E2E-Test-Suite (neu) | Klick auf "Vorheriger" am ersten Eintrag zeigt **keine** "Ende der Playlist erreicht"-Meldung |
| `PlaylistLockedEntryPlayButtonE2ETest` | E2E-Test-Suite (neu) | Playlist mit gesperrtem Eintrag: "Abspielen"-Button nicht sichtbar, Doppelklick hat keine Wirkung |
| `PlaylistDoubleClickCollectionEntryE2ETest` | E2E-Test-Suite (neu) | Doppelklick auf TVShow/Staffel/Collection: "Abspielen"-Button nicht sichtbar, Doppelklick hat keine Wirkung |
| `PlaylistLockedEntryPlayBackendErrorE2ETest` (optional) | E2E-Test-Suite (neu) | Backend-Fehler bei gesperrtem Eintrag wird als `playbackError` inline angezeigt, nicht als Seiten-Alert |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistServiceTests_Playback` (alle Tests) | Keine Anpassung erforderlich — alle bestehenden Tests funktionieren weiterhin. `ResolveExplicitStartEntry` wird nur intern angepasst; öffentliche Schnittstelle bleibt gleich. |
| `PlaylistEntriesListTests` | Keine Anpassung erforderlich — nur neue Tests hinzufügen. |

Falls in E2E-Tests existieren, die `VideoPlayer.razor` Komponente oder die `PlaylistDetail` direkt testen (z. B. "klick auf Nächster am Ende"), möglicherweise Anpassung der erwarteten Fehlerausgaben erforderlich (z. B. keine "Ende"-Meldung mehr bei Rückwärtsnavigation).

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Klick auf "Vorheriger" am Anfang der Playlist zeigt keine "Ende der Playlist erreicht"-Meldung | `VideoPlayerPreviousAtStartE2ETest` | Punkt 1 der Anforderung: Korrekte Endeverarbeitung beim Navigieren am Anfang | UI-Verhalten wird durch Komponenten-State (`playlistEndReached`) und JavaScript-Events gesteuert; Unit-Tests können nicht die tatsächliche HTML-Rendering oder User-Interactions abdecken |
| Pflicht | "Abspielen"-Button ist nicht sichtbar auf nicht-zugänglichen (gesperrten) Einträgen | `PlaylistLockedEntryPlayButtonE2ETest` | Punkt 2 der Anforderung: Bessere Fehlerbehandlung bei gesperrten Einträgen | Button-Sichtbarkeit hängt von Razor-Komponenten-Conditional ab; E2E kann die tatsächliche DOM-Struktur verifizieren |
| Pflicht | Doppelklick auf nicht-zugänglichen Eintrag hat keine Wirkung | `PlaylistLockedEntryPlayButtonE2ETest` (Teil desselben Tests) | Punkt 2 der Anforderung: Abspielversuch wird verhindert | Doppelklick-Handler ist an spezifischen DOM-Elementen gebunden; E2E verifiziert die tatsächliche Event-Binding |
| Pflicht | "Abspielen"-Button ist nicht sichtbar auf nicht-abspielbaren (Sammel-)Einträgen | `PlaylistDoubleClickCollectionEntryE2ETest` | Punkt 3 der Anforderung: Doppelklick auf Sammel-Eintrag wird verhindert | Analog zur obigen: Razor-Conditional rendering und Event-Binding können nur durch echtes DOM/UI getestet werden |
| Pflicht | Fehler beim Abspielen eines nicht-abspielbaren Eintrags wird als `playbackError` inline angezeigt | `PlaylistLockedEntryPlayBackendErrorE2ETest` (optional) | Punkt 2 der Anforderung: Fehlerbehandlung ändert sich von loadError zu playbackError | Fehler-UI-Platzierung und -Sichtbarkeit sind visuell; Unit-Tests können nicht verifizieren, dass die Error-Box an der richtigen Stelle im DOM erscheint |

**Begründung für E2E-Abdeckung:** Alle fünf Szenarien sind Benutzerinteraktionen (Klicks, Doppelklicks, Fehler-Anzeige), die nur durch echte Browser-Rendering und DOM-Traversing validiert werden können. Unit-Tests mit gemockten Komponenten könnten die Logik validieren (z. B. dass `IsPlayableEntry` `false` zurückgibt), aber nicht, dass Buttons **nicht sichtbar** sind oder dass `@ondblclick` **nicht gebunden** ist — das ist reine Razor/HTML-Rendering, die nur E2E zeigt.

### Betroffene E2E-Tests

Falls vorhanden: E2E-Tests, die spezifisch testen, dass "Vorheriger am Ende der Playlist" die "Ende der Playlist erreicht"-Meldung zeigt, müssen überprüft werden — diese Meldung sollte jetzt **nicht** mehr bei Rückwärtsnavigation am Anfang erscheinen.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| (keine bekannten bestehenden E2E-Tests identifiziert) | — |

---

## Offene Punkte

Keine. Alle Punkte aus der Anforderung wurden beantwortet und in den Plan eingearbeitet:

- ✅ **Punkt 1 – Anfang-Feedback:** Keine Meldung am Anfang; Buttons werden deaktiviert (Variante A)
- ✅ **Punkt 2 – Fehlerbehandlung in Detail:** `playbackError` wird beim Schließen des Players (`ClosePlayerAsync`) zurückgesetzt
- ✅ **Punkt 3 – Error-Response:** `400 Bad Request` für nicht-abspielbare Einträge
- ✅ **Punkt 4 – Tests:** In bestehenden Dateien wie `PlaylistServiceTests_Playback.cs`, `PlaylistEntriesListTests.cs` ergänzt; E2E-Tests in separate Dateien

---

## Zusammenfassung

Der Plan adressiert drei kritische Fehler durch sieben isolierte Änderungen an vier Komponenten/Services: Navigationsrichtungs-Kontext in Datenmodell und UI-Logik, erweiterte Abspielbarkeits-Validierung in Service und Component, Fehler-Trennung in Detail-Komponente. Die Implementierung ist schrittweise, rückwärtskompatibel und risikofrei. Alle neuen Test-Cases sind E2E-gedeckt, um echte User-Flows zu validieren.
