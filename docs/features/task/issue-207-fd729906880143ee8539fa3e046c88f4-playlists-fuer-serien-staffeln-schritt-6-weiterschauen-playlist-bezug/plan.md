# Umsetzungsplan: Nachbesserung Schritt 6, Runde 2 — Regression bei Playlist-Navigation

## Übersicht

Die Wiedergabepositionen in Playlisten werden nach einem Navigationsereignis (Auto-Advance bei Medienende, Nächster, Vorheriger, Neustart) nicht aktualisiert: Der alte Titel's Startposition bleibt erhalten und wird fälschlich auf den neuen Titel angewendet. Die Korrektur erfolgt ausschließlich in der `PlaylistDetail`-Komponente (Client-seitig) durch Wiederverwendung der bestehenden `StartPlaylistAsync`-API, um die korrekte playlist-spezifische Position für jeden neuen Eintrag zu ermitteln.

## Designentscheidungen

Keine — folgt bestehenden Mustern. Die Korrektur nutzt ausschließlich vorhandene APIs und Komponenten-Callbacks ohne neue Abstraktionen oder Interfaces.

**Bewusst verworfen:** Der ursprüngliche Plan-Entwurf schlug einen neuen Backend-Endpoint `GetContinueWatchingPositionAsync` vor. Diese Option wurde technisch verworfen zugunsten der Wiederverwendung von `IPlaylistApiClient.StartPlaylistAsync`, die bereits genau diesen Mechanismus implementiert: Sie lädt Playlist-Einträge, löst den angegebenen `entryId`-Eintrag auf und ruft `GetContinueWatchingPositionSecondsAsync` auf, um die Fortschrittsposition zu ermitteln — ohne Nebenwirkungen (reiner Lesevorgang).

## Programmabläufe

### Playlist-Entry-Wechsel mit Startposition-Aktualisierung

1. Benutzer löst Navigation aus (Auto-Advance, Nächster, Vorheriger oder Neustart) im `VideoPlayer`
2. `VideoPlayer` ruft entsprechende Navigation-API auf (`AdvancePlaylistAsync`, `GetNextPlaylistEntryAsync`, `GetPreviousPlaylistEntryAsync` oder `StartPlaylistAsync`)
3. Backend liefert `DtoPlaylistNavigationResult` mit neuem `Entry` und `Position`
4. `VideoPlayer.ApplyPlaylistNavigationResultAsync` oder `OnRestartPlaylistAsync` aktualisiert lokalen State (`MediaId`, `StreamUrl`), setzt `StartPositionSeconds = null` und ruft `NotifyPlaylistEntryChangedAsync` auf
5. `VideoPlayer` benachrichtigt Parent via `CurrentPlaylistEntryIdChanged` Callback mit `PlaylistEntryPlaybackInfo` (enthält `EntryId`, `PlayerMediaType`, `MediaId`, `StreamUrl`)
6. **NEU:** `PlaylistDetail.OnPlaylistEntryIdChanged` (jetzt `async Task`) empfängt Callback:
   - Aktualisiert `playbackStart` mit `CurrentEntryId`, `MediaType`, `MediaId`, `StreamUrl` (wie bisher)
   - Ruft zusätzlich **`PlaylistClient.StartPlaylistAsync(Id, info.EntryId)`** auf
   - Extrahiert die neue `StartPositionSeconds` aus der Response
   - Überschreibt **nur** `playbackStart.StartPositionSeconds` mit dem neuen Wert
   - Fehlerfall: Wenn API-Aufruf fehlschlägt (Exception), setzt `playbackStart.StartPositionSeconds = null` (Fallback auf 0 / Start vom Anfang) und loggt Exception
7. `PlaylistDetail` re-rendert und übergibt die aktualisierte `StartPositionSeconds` an `VideoPlayer`
8. `VideoPlayer.OnAfterRenderAsync` wendet die neue (richtige) Position an
9. JavaScript-Hook `continueWatching.attach` speichert die korrekte Position zurück

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `VideoPlayer.razor`, `IPlaylistApiClient`, `DtoPlaylistPlaybackStart`, `PlaylistEntryPlaybackInfo`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `PlaylistDetail.razor` (UI-Komponente)

- **Geänderte Methoden:**
  - `OnPlaylistEntryIdChanged(PlaylistEntryPlaybackInfo info)` — Ändert von `void` zu `async Task`:
    - Behält bisherige Zeilen bei (aktualisiert `playbackStart.CurrentEntryId`, `MediaType`, `MediaId`, `StreamUrl`)
    - **Zusätzlich:** Ruft `await PlaylistClient.StartPlaylistAsync(Id, info.EntryId)` auf
    - Extrahiert `startPlaybackResponse.StartPositionSeconds` und weist es `playbackStart.StartPositionSeconds` zu
    - **Fehlerbehandlung:** try-catch: Falls Exception, loggt Fehler via `Logger.LogError`, setzt `playbackStart.StartPositionSeconds = null`, Methode setzt nicht abbrechen (Exception nicht erneut werfen)
    - Methode gibt `Task` zurück (nicht `void`)
  
- **Überprüfung erforderlich:**
  - Aufrufer: `CurrentPlaylistEntryIdChanged` EventCallback-Signatur in `VideoPlayer.razor`
  - Prüfung: Ist der Callback konfiguriert mit `EventCallback.Factory.Create` oder ähnlich, das async Handler unterstützt? (In der Regel bereits kompatibel, aber Prüfung notwendig)
  - Falls nicht kompatibel: `VideoPlayer.razor` anpassen, sodass der async Handler korrekt awaitet wird

### `VideoPlayer.razor` (UI-Komponente)

- **Überprüfung erforderlich:**
  - `CurrentPlaylistEntryIdChanged` Callback-Aufruf (vermutlich `CurrentPlaylistEntryIdChanged.InvokeAsync(...)`) — muss geprüft werden, ob `await` korrekt gesetzt ist, falls `OnPlaylistEntryIdChanged` zu `async Task` wechselt
  - Ggf. kleinere Anpassung notwendig (typischerweise aber `EventCallback` bereits async-kompatibel)

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Regression-Test für Initial-Start bleibt bestehen:** `PlaylistDetailTests.StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()` bleibt gültig und muss nicht angepasst werden (prüft Initial-Start, nicht Entry-Wechsel)
- **Performance:** Zusätzlicher API-Aufruf pro Entry-Wechsel (`StartPlaylistAsync`). Dies ist ein bewusster Tradeoff: Die genaue Weiterschauen-Position ist wichtiger als Latenz-Optimierung bei seltenen Navigation-Events
- **Fehlerbehandlung:** Falls `StartPlaylistAsync` fehlschlägt, startet neuer Titel von 0:00 (nicht von seiner tatsächlichen Position) — akzeptabeles Fallback
- **Komponenten-Lifecycle:** `OnPlaylistEntryIdChanged` wird bereits aufgerufen, wenn sich `CurrentPlaylistEntryIdChanged` ändert; die Änderung zu `async Task` erfordert keine zusätzliche Component-Lifecycle-Prüfung (wird via EventCallback ausgelöst, nicht via Lifecycle-Methode)

## Umsetzungsreihenfolge

1. **Überprüfung: EventCallback Async-Kompatibilität**
   - Voraussetzungen: Keine
   - Beschreibung: Prüfe in `VideoPlayer.razor` die Signatur von `CurrentPlaylistEntryIdChanged` (vermutlich `EventCallback<PlaylistEntryPlaybackInfo>`) und die Aufruustelle von `InvokeAsync`. Stelle sicher, dass ein `async Task` Handler korrekt awaitet wird. Notiere das Ergebnis — falls Änderung nötig ist, wird sie in Schritt 2 durchgeführt.

2. **Änderung: `PlaylistDetail.OnPlaylistEntryIdChanged` zu async Task**
   - Voraussetzungen: Überprüfung aus Schritt 1 abgeschlossen; `PlaylistClient` (Eigenschaft/Feld für `IPlaylistApiClient`) existiert bereits in `PlaylistDetail.razor`
   - Beschreibung: 
     - Ändere Methodensignatur von `void OnPlaylistEntryIdChanged(...)` zu `async Task OnPlaylistEntryIdChanged(...)`
     - Behalte bisherige Zeilen bei (aktualisiert `playbackStart.CurrentEntryId`, `MediaType`, `MediaId`, `StreamUrl`)
     - Füge nach diesen Zeilen hinzu:
       ```csharp
       try
       {
           var startPlaybackResponse = await PlaylistClient.StartPlaylistAsync(Id, info.EntryId);
           playbackStart.StartPositionSeconds = startPlaybackResponse.StartPositionSeconds;
       }
       catch (Exception ex)
       {
           Logger.LogError(ex, "Failed to fetch StartPositionSeconds for entry {EntryId} in playlist {PlaylistId}", info.EntryId, Id);
           playbackStart.StartPositionSeconds = null;
       }
       ```
     - Stelle sicher, dass `Logger` vorhanden ist (injiziert oder über Komponenten-Standard-Logger)

3. **Überprüfung und ggf. Anpassung: `VideoPlayer.razor` EventCallback-Handling**
   - Voraussetzungen: Schritt 2 abgeschlossen
   - Beschreibung: Prüfe die Aufrufstelle, wo `CurrentPlaylistEntryIdChanged.InvokeAsync(...)` aufgerufen wird (vermutlich in `NotifyPlaylistEntryChangedAsync`). Falls der Handler now `async Task` ist, stelle sicher, dass `await` korrekt gesetzt ist oder dass das EventCallback-System async Handler korrekt handhabt. Dies ist typischerweise bereits kompatibel (Blazor EventCallback unterstützt async Handler), aber Verifizierung ist erforderlich. Keine Codeänderung nötig, falls bereits kompatibel.

4. **Tests: Unit-Tests für alle vier Wechselarten (bUnit)**
   - Voraussetzungen: Schritte 1–3 abgeschlossen; `PlaylistDetailTests.cs` existiert
   - Beschreibung: Ergänze `PlaylistDetailTests` um vier neue Tests (falls nicht bereits vorhanden):
     - `OnPlaylistEntryIdChanged_AutoAdvance_FetchesAndAppliesNewStartPositionSeconds` — Simuliert Auto-Advance (Ende eines Videos), prüft, dass `PlaylistClient.StartPlaylistAsync(playlistId, newEntryId)` aufgerufen wird und `playbackStart.StartPositionSeconds` mit dem Ergebnis aktualisiert wird
     - `OnPlaylistEntryIdChanged_NextButton_FetchesAndAppliesNewStartPositionSeconds` — Simuliert "Nächster"-Klick, analog
     - `OnPlaylistEntryIdChanged_PreviousButton_FetchesAndAppliesNewStartPositionSeconds` — Simuliert "Vorheriger"-Klick, analog
     - `OnPlaylistEntryIdChanged_RestartButton_FetchesAndAppliesNewStartPositionSeconds` — Simuliert "Neu starten"-Klick, analog
   - **Mock-Strategie:** Konfiguriere `PlaylistClient`-Mock so, dass `StartPlaylistAsync` pro `entryId` unterschiedliche `StartPositionSeconds` zurückgibt (z. B. Entry A = 300s, Entry B = 450s, Entry C = 0s). Dies verifiziert, dass jeder Entry seine eigene Position bekommt, nicht die des vorherigen.
   - **Assertion:** Nach Aufruf von `OnPlaylistEntryIdChanged(info)` mit neuem `info.EntryId`, prüfe, dass `playbackStart.StartPositionSeconds` den **neuen** Wert hat (nicht den alten).

5. **Tests: Fehlerfall-Test (bUnit)**
   - Voraussetzungen: Schritt 4 abgeschlossen
   - Beschreibung: Ergänze `PlaylistDetailTests` um einen Test:
     - `OnPlaylistEntryIdChanged_StartPlaylistAsync_ThrowsException_FallsBackToNullPosition` — Konfiguriere Mock so, dass `StartPlaylistAsync` eine Exception wirft (z. B. `HttpRequestException`). Rufe `OnPlaylistEntryIdChanged` auf. Prüfe:
       - Dass Exception geloggt wird (Logger-Mock)
       - Dass `playbackStart.StartPositionSeconds = null` gesetzt wird
       - Dass die Methode nicht abbricht (keine Exception nach außen)
       - Dass andere Felder (`CurrentEntryId`, `MediaType`, `MediaId`, `StreamUrl`) trotzdem korrekt aktualisiert wurden

6. **Regression-Sicherung: Existing Test bleibt bestehen**
   - Voraussetzungen: Schritte 1–5 abgeschlossen
   - Beschreibung: Der bestehende Test `PlaylistDetailTests.StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()` muss weiterhin bestehen (prüft Initial-Start, nicht Entry-Wechsel). Falls dieser Test nach Schritt 2 fehlschlägt, ist eine Regression in den bisherigen Code eingetreten und muss korrigiert werden.

7. **Dokumentation: Acceptance-Dateien aktualisieren**
   - Voraussetzungen: Schritte 1–6 abgeschlossen und Tests grün
   - Beschreibung: 
     - Überprüfe `docs/projects/task/issue-207-fd729906880143ee8539-fa3e046c88f4-playlists-fuer-serien-staffeln/acceptance-schritt-6.md` und `acceptance-schritt-6.1.md`
     - Füge Abschnitt hinzu, der erklärt, dass die Regression (alte Startposition wird auf neuen Titel angewendet) durch Wiederverwendung von `StartPlaylistAsync` behoben wurde
     - Dokumentiere das Fallback-Verhalten (falls API fehlschlägt, startet neuer Titel von 0:00)
     - Erwähne die vier unterstützten Wechselarten (Auto-Advance, Nächster, Vorheriger, Neustart) und dass der Mechanismus alle abdeckt

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OnPlaylistEntryIdChanged_AutoAdvance_FetchesAndAppliesNewStartPositionSeconds` | `PlaylistDetailTests` | Prüft, dass nach Auto-Advance (Medienende) `StartPlaylistAsync` mit neuer `entryId` aufgerufen wird und `playbackStart.StartPositionSeconds` mit dem Ergebnis aktualisiert wird (nicht mit alter Position) |
| `OnPlaylistEntryIdChanged_NextButton_FetchesAndAppliesNewStartPositionSeconds` | `PlaylistDetailTests` | Prüft, dass nach "Nächster"-Klick `StartPlaylistAsync` aufgerufen wird und neue Position angewendet wird |
| `OnPlaylistEntryIdChanged_PreviousButton_FetchesAndAppliesNewStartPositionSeconds` | `PlaylistDetailTests` | Prüft, dass nach "Vorheriger"-Klick `StartPlaylistAsync` aufgerufen wird und neue Position angewendet wird |
| `OnPlaylistEntryIdChanged_RestartButton_FetchesAndAppliesNewStartPositionSeconds` | `PlaylistDetailTests` | Prüft, dass nach "Neu starten"-Klick `StartPlaylistAsync` aufgerufen wird und neue Position angewendet wird |
| `OnPlaylistEntryIdChanged_StartPlaylistAsync_ThrowsException_FallsBackToNullPosition` | `PlaylistDetailTests` | Prüft Fehlerfall: Falls `StartPlaylistAsync` wirft, wird `StartPositionSeconds = null` gesetzt, Exception geloggt, Methode bricht nicht ab |
| (Hilfsmethode: `CreatePlaylistClientMockWithPerEntryPositions(...)`) | `PlaylistDetailTests` | Konfiguriert Mock so, dass verschiedene `entryId`-Werte unterschiedliche `StartPositionSeconds` zurückgeben (z. B. Dictionary-basiert) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetailTests.StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()` | Muss weiterhin bestehen (prüft Initial-Start, nicht Entry-Wechsel). Keine Änderung nötig; als Regressions-Sicherung dienen. |
| Sämtliche andere `PlaylistDetailTests` | Müssen weiterhin bestehen. Keine Anpassungen nötig, sofern keine Mocks von `OnPlaylistEntryIdChanged` durchführen. |

Falls andere Tests `PlaylistDetail` manuell aufrufen und `CurrentPlaylistEntryIdChanged` Callback simulieren, müssen diese überprüft werden auf async-Kompatibilität (aber typischerweise nicht nötig).

### E2E-Tests (primärer Funktionsnachweis)

E2E-Tests sind für diese Regression **nicht als primärer Funktionsnachweis erforderlich**, da:
- Der Fehler ist eine reine Komponenten-State-Management-Regression (nicht UI-Ablauf-spezifisch)
- bUnit-Tests mit Mock `PlaylistClient` sind ausreichend, um die korrekte State-Aktualisierung nachzuweisen
- Der tatsächliche VideoPlayer-Ablauf (Position anwenden, JavaScript-Hook aufrufen) wird durch bestehende VideoPlayer-Tests bereits abgedeckt
- E2E-Tests würden nur sichtbar machen, dass die Position angewendet wird, aber nicht die interne State-Aktualisierung prüfen (was bUnit besser kann)

**Begründung:** Der Fehler tritt auf der Komponenten-State-Ebene auf (Parent aktualisiert `playbackStart.StartPositionSeconds` nicht), nicht auf der UI-Interaktions-Ebene. bUnit-Tests sind das richtige Instrument, um diese Regression zu fangen. E2E-Tests würden einen längeren Testlauf erfordern (Playwright) ohne zusätzliche Aussagekraft.

**Falls Playwright-E2E-Tests existieren:** Optional können zwei E2E-Tests hinzugefügt werden als dokumentarische Bestätigung (nicht als primärer Nachweis):
- Playlist mit Video A (Position 300 s) und Video B (Position 0 s) wird geladen
- User schaut A bis zum Ende → Auto-Advance zu B
- Verifiziere in der UI, dass B von Anfang startet (nicht bei 300 s)

Diese E2E-Tests sind aber nicht Voraussetzung für die Abnahme dieser Regression-Korrektur.

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Die bestehenden E2E-Tests in `PlaylistDetailE2ETests` müssen nicht angepasst werden, da sie nicht auf `StartPositionSeconds`-Logik prüfen (sie prüfen UI-Navigation, Zugriffskontrolle, Virtualisierung). |

## Offene Punkte

Keine. Alle technischen Punkte wurden durch die übergebene Korrektur geklärt:
- ✅ API-Design: Wiederverwendung von `StartPlaylistAsync` statt neuer Endpoint
- ✅ Fehlerbehandlung: Fallback auf `null` bei Exception
- ✅ Async-Handling: `OnPlaylistEntryIdChanged` wird `async Task`
- ✅ Deckt alle vier Wechselarten ab (Auto-Advance, Next, Previous, Restart)
- ✅ Playlist-Spezifität bleibt erhalten (bereits in `StartPlaylistAsync` implementiert)
