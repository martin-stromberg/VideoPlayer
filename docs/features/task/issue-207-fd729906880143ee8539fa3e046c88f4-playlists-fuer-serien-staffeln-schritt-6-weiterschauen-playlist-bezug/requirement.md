# Anforderungsübersetzung: Nachbesserung Schritt 6, Runde 2

## Fachliche Zusammenfassung

Bei der Wiederaufnahme einer playlist-gebundenen Weiterschauen-Wiedergabe tritt eine Regression auf: Die wiederhergestellte Startposition des ursprünglichen Titels bleibt nach einem Playlist-Navigation-Ereignis (Medienende mit Auto-Advance, „Nächster", „Vorheriger" oder „Neu starten") im State des Parent-Components erhalten und wird fälschlich auf den neu geladenen Titel angewendet. Der neue Titel startet dadurch nicht mit seiner eigenen, tatsächlich für ihn gespeicherten Weiterschauen-Position, sondern mit der des vorherigen Titels; diese falsche Position wird anschließend als Fortschritt des neuen Titels zurückgeschrieben.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- **`PlaylistDetail.razor`** — Parent-Component für die Playlist-Wiedergabe; hält den State der aktuellen Wiedergabe-Session in `playbackStart` (`DtoPlaylistPlaybackStart`)
- **`VideoPlayer.razor`** — Abspieler-Component; handhabt interne Navigationen und teilt neue Entry-Informationen mit dem Parent via `CurrentPlaylistEntryIdChanged`-Callback

### DTOs und Datenklassen
- **`DtoPlaylistPlaybackStart`** — DTO für die Initial- oder aktualisierte Wiedergabevorbereitung, enthält:
  - `CurrentEntryId` — ID des aktuellen Playlist-Eintrags
  - `MediaType` — Medientyp des Eintrags (üblicherweise aus API)
  - `MediaId` — ID des Mediums
  - `StreamUrl` — Streaming-URL (mit Access-Token)
  - `StartPositionSeconds` — Startposition in Sekunden (wird derzeit **nicht** aktualisiert bei Entry-Wechsel)
  - `PlaylistId`, `TotalCount`, `PlaylistName`, `CurrentPosition` — Playlist-Kontext-Informationen

- **`DtoPlaylistNavigationResult`** — Ergebnis einer Navigation-API-Operation (Next/Previous/Advance)
  - `Entry` — Der neue Eintragsdetail mit `Id`, `MediaId`, `MediaType`
  - `Position` — Neue Position in der Playlist
  - Enthält normalerweise **keine** Weiterschauen-Position

### Logik/Services
- **`IPlaylistApiClient.AdvancePlaylistAsync(playlistId, entryId)`** — API-Aufruf für Auto-Advance bei Medienende
- **`IPlaylistApiClient.NextPlaylistAsync(playlistId, entryId)`** — API-Aufruf für manuelles „Nächster"
- **`IPlaylistApiClient.PreviousPlaylistAsync(playlistId, entryId)`** — API-Aufruf für manuelles „Vorheriger"
- **`IPlaylistApiClient.StartPlaylistAsync(playlistId, entryId)`** — API-Aufruf für Initial-Start oder Neustart einer Playlist

## Problem-Mechanismus

1. **Initial-Aufruf:** `PlaylistDetail.OnInitializedAsync` ruft `StartPlaylistAsync` auf, das eine `DtoPlaylistPlaybackStart` mit korrekter `StartPositionSeconds` für den initial angeforderten Eintrag liefert (ermittelt aus der Weiterschauen-Datenbank nach `entryId`-Query-Parameter).

2. **VideoPlayer-Navigation:** Der User trifft eine Navigations-Aktion im `VideoPlayer` (Auto-Advance am Medienende, Klick auf „Nächster", „Vorheriger", oder „Neu starten"). Der `VideoPlayer` ruft die entsprechende Navigation-API auf (z. B. `AdvancePlaylistAsync`).

3. **Lokale State-Anwendung:** `VideoPlayer.ApplyPlaylistNavigationResultAsync` erhält das Ergebnis und:
   - Aktualisiert `MediaType`, `MediaId`, `StreamUrl` lokal via `ApplyEntryStreamInfo`
   - Setzt dabei explizit `StartPositionSeconds = null` (Zeile 351 in VideoPlayer.razor)
   - Benachrichtigt den Parent via `CurrentPlaylistEntryIdChanged.InvokeAsync(new PlaylistEntryPlaybackInfo(...))`

4. **Parent-Update-Fehler:** `PlaylistDetail.OnPlaylistEntryIdChanged` empfängt die neue Entry-Information und aktualisiert `playbackStart` mit `CurrentEntryId`, `MediaType`, `MediaId`, `StreamUrl` — aktualisiert aber **nicht** `playbackStart.StartPositionSeconds`. Der Wert behält seinen alten Wert bei (von der Initial-Aktion).

5. **Re-Render und Doppeltes Anwenden:** Beim nächsten Render übergibt `PlaylistDetail` die unverändert alte `StartPositionSeconds` an den `VideoPlayer`:
   ```blazor
   StartPositionSeconds="@playbackStart.StartPositionSeconds"
   ```
   `VideoPlayer.OnAfterRenderAsync` erkennt die Änderung der `StreamUrl` und setzt `_startApplied = false` zurück. Bei der nächsten Render-Iteration wendet `OnAfterRenderAsync` (Zeile 167–171) die (alte) `StartPositionSeconds` erneut an, obwohl sie für den neuen Titel nicht gilt.

6. **Datenspeicherung:** Der `continueWatching.attach`-JavaScript-Hook speichert die angewendete falsche Position als Fortschritt des neuen Titels zurück.

## Korrektives Verhalten

### Anforderung 1: Startposition nur für den ursprünglichen Eintrag gelten lassen
Die `StartPositionSeconds` dürfen nur für den Eintrag gelten, für den sie bei der Initial-Aktion (über `entryId`-Query-Parameter) ermittelt wurden. Nach einem Wechsel des aktuellen Eintrags (über Navigation-API-Aufrufe) muss der lokale State des Parent korrekt aktualisiert werden.

**Implementierungsansatz:**
- `PlaylistDetail.OnPlaylistEntryIdChanged` muss bei jedem Aufruf **zusätzlich** `playbackStart.StartPositionSeconds` aktualisieren.
- Der neue Wert sollte **nicht** die alte Position des vorherigen Titels sein, sondern:
  - `null` (sodass der VideoPlayer mit 0:00 startet), **ODER**
  - Die korrekte, tatsächlich für den neuen Titel gespeicherte Position (falls sie bereits aus einer anderen Quelle bekannt ist oder abgefragt wird)

Laut der Anforderung soll der neue Titel mit seiner eigenen, tatsächlich gespeicherten Fortschrittsposition starten (falls vorhanden). Das bedeutet:

**Option A (bevorzugt):** Die Navigation-API-Aufrufe (`AdvancePlaylistAsync`, `NextPlaylistAsync`, `PreviousPlaylistAsync`) geben bereits die neue `StartPositionSeconds` zurück (in `DtoPlaylistNavigationResult`), und `VideoPlayer` übergibt diese an den Parent.

**Option B:** Der Parent fragt die neue Position via API ab, nachdem der VideoPlayer die neue Entry-ID mitgeteilt hat (kostenintensiver, zusätzlicher API-Call).

**Option C:** `PlaylistDetail.OnPlaylistEntryIdChanged` setzt `playbackStart.StartPositionSeconds = null`, wodurch der neue Titel mit 0:00 (oder dem vom Backend ermittelten Standard-Fortschritt) startet.

### Anforderung 2: Konsistenz über alle Navigation-Pfade
Das Verhalten muss für alle vier Wechselarten konsistent sein:
1. **Auto-Advance bei Medienende** (`.OnMediaEndAsync` → `AdvancePlaylistAsync`)
2. **Manuelles „Nächster"** (`.OnNextPlaylistEntryAsync` → `NextPlaylistAsync`)
3. **Manuelles „Vorheriger"** (`.OnPreviousPlaylistEntryAsync` → `PreviousPlaylistAsync`)
4. **„Neu starten" der Playlist** (`.OnRestartPlaylistAsync` → `StartPlaylistAsync`)

Alle vier rufen (direkt oder indirekt) `NotifyPlaylistEntryChangedAsync` auf oder triggern einen neuen `playbackStart`-Zustand. `PlaylistDetail.OnPlaylistEntryIdChanged` **oder** eine neue parallele Callback-Methode muss in allen vier Fällen `StartPositionSeconds` korrekt setzen.

### Anforderung 3: Trennung nach Playlist-Zugehörigkeit (bereits implementiert)
Der neue Titel soll mit seiner **in genau dieser Playlist** gespeicherten Position starten (nicht mit einer Position, die er in einer anderen Playlist oder ohne Playlist-Bezug hat). Die bereits in Schritt 6 korrekt implementierte Trennung nach `PlaylistId` muss gewahrt bleiben.

## Implementierungsansatz

### Szenario A: Neue Position bereits in der Navigation-API-Response

**Änderungen:**
1. **Backend-API erweiterbar (falls nötig):** `DtoPlaylistNavigationResult` kann optional um `StartPositionSeconds` erweitert werden, falls nicht bereits vorhanden.

2. **`VideoPlayer.NotifyPlaylistEntryChangedAsync`-Erweiterung:** Das `PlaylistEntryPlaybackInfo`-Record wird um ein optionales Feld `StartPositionSeconds?` erweitert (oder ein neues Record `PlaylistEntryPlaybackInfoExtended` wird eingeführt).

3. **`VideoPlayer.ApplyPlaylistNavigationResultAsync`:** Nach dem Aufruf von `ApplyEntryStreamInfo` (das `StartPositionSeconds = null` setzt), wird `StartPositionSeconds` mit dem Wert aus `DtoPlaylistNavigationResult` wieder gefüllt (falls vorhanden).

4. **`PlaylistDetail.OnPlaylistEntryIdChanged`:** Empfängt den neuen `StartPositionSeconds`-Wert via `PlaylistEntryPlaybackInfo` und aktualisiert `playbackStart.StartPositionSeconds` damit.

### Szenario B: Parent ruft neue Position nach dem Wechsel ab (Alternative)

1. **`PlaylistDetail.OnPlaylistEntryIdChanged`** macht einen zusätzlichen API-Call, um die Weiterschauen-Position des neuen Eintrags zu ermitteln:
   ```csharp
   var newPosition = await PlaylistClient.GetContinueWatchingPositionAsync(newEntryId, playlistId);
   playbackStart.StartPositionSeconds = newPosition;
   ```

2. Diese Option ist teurer (zusätzlicher API-Call) und sollte nur gewählt werden, wenn die Navigation-API nicht bereits die Position liefert.

### Szenario C: Startposition auf null setzen (Fallback)

Wenn keine neue Position verfügbar ist oder sein soll, setzt `PlaylistDetail.OnPlaylistEntryIdChanged`:
```csharp
playbackStart.StartPositionSeconds = null;
```

Dies führt dazu, dass der neue Titel mit 0:00 startet, es sei denn, das Backend wird separat abgefragt. Das violiert die Anforderung, aber ist das sicherste Fallback gegen die Regression.

**Empfehlung:** Zunächst Szenario A prüfen; falls die Navigation-API bereits Start-Positionen liefert, ist dies die sauberste Lösung.

## Datenfluss-Beispiele

### Beispiel 1: Auto-Advance bei Medienende

```
1. User schaut Video A aus Playlist P, Position 600 s, bis zum Ende
2. VideoPlayer.OnMediaEndAsync → AdvancePlaylistAsync(playlistId=P, entryId=A)
3. Backend ermittelt nächsten Eintrag B, Position Position in Playlist
4. VideoPlayer.ApplyPlaylistNavigationResultAsync erhält DtoPlaylistNavigationResult:
   - Entry.Id = B, Entry.MediaId = 8, ...
   - StartPositionSeconds = 0 (oder die für B in P gespeicherte Position)
5. VideoPlayer.ApplyEntryStreamInfo setzt MediaId=8, StreamUrl=..., StartPositionSeconds=null
6. VideoPlayer.NotifyPlaylistEntryChangedAsync ruft Parent-Callback auf
7. PlaylistDetail.OnPlaylistEntryIdChanged:
   - Aktualisiert CurrentEntryId=B, MediaId=8, StreamUrl=...
   - Aktualisiert StartPositionSeconds = <neue Position für B in P> (aus Callback oder API-Abfrage)
8. Re-Render: PlaylistDetail übergibt StartPositionSeconds="<Position für B>" an VideoPlayer
9. VideoPlayer.OnAfterRenderAsync wendet die neue Position für B an
10. continueWatching.attach speichert Fortschritt für Video 8 in Kontext Playlist P
```

### Beispiel 2: Manueller "Nächster"-Klick

Identischer Datenfluss wie Beispiel 1, nur dass der User aktiv auf die Schaltfläche klickt.

### Beispiel 3: "Neu starten" der Playlist

```
1. Playlist P am Ende erreicht, "Neu starten"-Schaltfläche sichtbar
2. VideoPlayer.OnRestartPlaylistAsync → StartPlaylistAsync(playlistId=P, entryId=null)
3. Backend ermittelt den ersten "zu betrachtenden" Eintrag C für Restart
4. VideoPlayer.ApplyEntryStreamInfo, ApplyPlaylistContext, NotifyPlaylistEntryChangedAsync
5. PlaylistDetail.OnPlaylistEntryIdChanged: Aktualisiert StartPositionSeconds korrekt für C
6. Konsistenz mit der Initial-Aktion: Restart verhält sich wie der erste Start (Position für C wird ermittelt)
```

## Offene Fragen / Annahmen

1. **Liefert `DtoPlaylistNavigationResult` bereits `StartPositionSeconds`?**
   - Falls ja: Szenario A ist sofort umsetzbar
   - Falls nein: Muss das Backend erweitert werden, oder wird Szenario B/C gewählt?

2. **Einzigartigkeit der neuen Position:**
   - Soll `StartPositionSeconds` beim Wechsel immer neu ermittelt werden (aus der Weiterschauen-DB), oder wird ein gecachter Wert aus dem DTO genommen?
   - Die Anforderung verlangt, dass die Position "playlist-spezifisch gemäß der bereits in diesem Schritt korrekt implementierten Trennung nach Playlist-Zugehörigkeit" ermittelt wird — das ist bereits implementiert, muss aber beim Wechsel aktiv angefragt werden.

3. **Performance:**
   - Szenario B (zusätzlicher API-Call pro Navigation) kann unter starker Nutzung (viele Wechsel) zu Latenz führen. Szenario A (Position bereits in Response) ist performanter.

4. **Behandlung von `null`-Positionen:**
   - Wenn für einen neuen Titel noch keine Weiterschauen-Position existiert (er wurde noch nie geschaut), was passiert?
   - Annahme: `StartPositionSeconds = null` oder `0`, und der Title startet vom Anfang.

## Test-Regressions-Szenarien

Die folgenden Szenarien müssen mit Unit-Tests (bUnit für Blazor-Components) abgedeckt werden:

1. **Auto-Advance-Regression:** Wiedergabe von A bei 600 s beenden → Auto-Advance zu B → Verifizieren, dass B mit seiner eigenen Position startet, nicht mit 600 s.

2. **Nächster-Button-Regression:** A bei 400 s → "Nächster" → B → B startet nicht bei 400 s.

3. **Vorheriger-Button-Regression:** B bei 200 s → "Vorheriger" → A → A startet nicht bei 200 s.

4. **Neu-Starten-Regression:** Während Playback von C → "Neu starten" → C (oder erste Entry) startet nicht bei der Position von C, sondern beim Neustart der Playlist.

5. **Mehrfach-Wiedergabeposition-Trennung:** Playlist P enthält Video V mit Position 300 s in P, aber Video V ohne P-Bezug hat Position 100 s → Wechsel innerhalb P zu V → V startet bei 300 s, nicht 100 s.

## Zusammengefasste Massnahmen

1. **`PlaylistDetail.OnPlaylistEntryIdChanged`-Update:** Muss `StartPositionSeconds` nach jedem Entry-Wechsel aktualisieren.

2. **`PlaylistEntryPlaybackInfo`-Erweiterung (optional):** Falls Szenario A gewählt, kann die neue Position bereits im Callback übergeben werden.

3. **API-Erweiterung (optional):** Falls `DtoPlaylistNavigationResult` nicht bereits `StartPositionSeconds` enthält, sollte es erweitert werden (Szenario A).

4. **Regressions-Test-Suite:** Mindestens vier bUnit-Tests für die Navigation-Szenarien.

5. **Dokumentation:** Die Fehlerursache und Korrektur sollten in den Akzeptanzprotokolldateien und in `docs/help/weiterschauen/ablauf-technisch.md` aufgenommen werden (Abschnitt: "Restart-Positionen", "Position-Trennung nach Playlist-Zugehörigkeit").
