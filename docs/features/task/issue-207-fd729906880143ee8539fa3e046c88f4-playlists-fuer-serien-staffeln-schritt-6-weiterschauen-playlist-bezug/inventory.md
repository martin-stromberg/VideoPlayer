# Bestandsaufnahme: Nachbesserung Schritt 6, Runde 2 — Regression bei Playlist-Navigation

## Übersicht

Diese Bestandsaufnahme analysiert die in Schritt 6 implementierte Weiterschauen-Funktionalität mit Playlist-Bezug und die Regression, die nach einem Playlist-Navigation-Ereignis (Auto-Advance, Nächster, Vorheriger, Neustart) auftritt. Die Start-Position des ursprünglichen Titels wird fälschlich auf den neu geladenen Titel angewendet.

Analysiert wurden die Komponenten `PlaylistDetail.razor` und `VideoPlayer.razor` sowie die beteiligten DTOs und das API-Client-Interface, um die genaue Ursache der Regression zu ermitteln und die Anforderungen für die Korrektur zu dokumentieren.

## Zusammenfassung der Befunde

### Vorhanden

1. **`DtoPlaylistPlaybackStart` mit `StartPositionSeconds`:** ✅ Vollständig implementiert, wird bei Initial-Start korrekt befüllt und vom Parent empfangen.

2. **API-Schicht für Navigation:** ✅ `IPlaylistApiClient.GetNextPlaylistEntryAsync()`, `GetPreviousPlaylistEntryAsync()`, `AdvancePlaylistAsync()` existieren.

3. **Playlist-Kontext-Trennung:** ✅ Die in Schritt 6 korrekt implementierte Trennung nach `PlaylistId` ist funktionsfähig (nachgewiesen durch `ContinueWatchingServicePlaylistTests`).

4. **VideoPlayer-Navigation-Logik:** ✅ Alle vier Navigations-Pfade (Auto-Advance, Next, Previous, Restart) sind implementiert und funktionieren.

5. **Komponenten-Callbacks:** ✅ `CurrentPlaylistEntryIdChanged`-Event mit `PlaylistEntryPlaybackInfo`-Callback existiert.

### Probleme und Lücken

1. **`DtoPlaylistNavigationResult` ohne `StartPositionSeconds`:** ❌ **KRITISCH**
   - Die Navigation-APIs (Next/Previous/Advance) liefern nur `Entry` und `Position`, nicht die Start-Position des neuen Titels.
   - Dies verhindert die Implementierung von Szenario A (Position bereits in Response) ohne Backend-Änderungen.

2. **`PlaylistDetail.OnPlaylistEntryIdChanged()` aktualisiert `StartPositionSeconds` nicht:** ❌ **REGRESSION-URSACHE**
   - Die Methode (Zeilen 202-212) empfängt den Callback mit `PlaylistEntryPlaybackInfo`, aktualisiert aber nur `CurrentEntryId`, `MediaType`, `MediaId` und `StreamUrl`.
   - `playbackStart.StartPositionSeconds` behält seinen alten Wert.

3. **`VideoPlayer.ApplyEntryStreamInfo()` setzt `StartPositionSeconds = null`:** ✅ (notwendig, aber unvollständig)
   - Zeile 351: `StartPositionSeconds = null;` ist richtig, um eine doppelte Anwendung der alten Position zu verhindern.
   - Aber es findet KEINE Übergabe der neuen Position an den Parent statt.

4. **`PlaylistEntryPlaybackInfo` hat kein `StartPositionSeconds`-Feld:** ⚠️
   - Das Record (Zeile 362) enthält nur: `EntryId`, `PlayerMediaType`, `MediaId`, `StreamUrl`.
   - Könnte erweitert werden, um die neue Start-Position zu transportieren (falls die API sie liefert oder der Parent sie abfragt).

5. **`VideoPlayer.OnAfterRenderAsync()` wendet veraltete Position an:** ✅ (Symptom, nicht Ursache)
   - Zeilen 167-171: Wendet `StartPositionSeconds` an, wenn sich `StreamUrl` ändert.
   - Das Problem ist, dass der Parent die neue `StartPositionSeconds` nicht aktualisiert hat.

### Test-Ausgangszustand

- **Zeitpunkt:** 2026-09-14 02:53 UTC
- **Branch/Commit:** `task/issue-207-.../schritt-6-weiterschauen-playlist-bezug` (HEAD: `a86fff3`)
- **Test-Lauf-Ergebnis:** ✅ **598 Tests bestanden, 0 Fehler** (6 MarkdownLinkCheck + 592 VideoWebPlayer Tests)
- **Nachweis:** [initial-test-run.log](inventory/test-results/initial-test-run.log)
- **Testlücke:** Es gibt einen Regressions-Test `PlaylistDetailTests.StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()`, aber dieser prüft nur Initial-Start, nicht Entry-Wechsel!
- **E2E-Tests:** `PlaylistDetailE2ETests` hat 18 Tests, aber keine für die `StartPositionSeconds`-Regression bei Entry-Wechsel.

## Details

- [Datenmodellklassen](inventory/models.md) — DTOs und ihre Felder
- [Logik- und Komponentenmethoden](inventory/logic.md) — Component-Methoden, Events, API-Client
- [Testausgangszustand und Testklassen](inventory/tests.md) — Bestehende Tests, Testlücken, Hilfsmethoden

## Kritische Erkenntnisse für die Implementierung

### 1. API-Desgin-Problem
`DtoPlaylistNavigationResult` enthält keine `StartPositionSeconds`. Das Backend müsste erweitert werden, um:
- Entweder `StartPositionSeconds` in der Response zu liefern (Szenario A — bevorzugt), oder
- Der Client fragt die neue Position separat ab (Szenario B — teurer), oder
- Der Client setzt die neue Position auf `null` (Szenario C — schnell, aber unerwünscht).

### 2. Parent-Update-Fehler
`PlaylistDetail.OnPlaylistEntryIdChanged()` muss `playbackStart.StartPositionSeconds` aktualisieren. Derzeit fehlt diese Zeile völlig.

### 3. Callback-Erweiterung (optional)
`PlaylistEntryPlaybackInfo` könnte um ein optionales `StartPositionSeconds?` Feld erweitert werden, um die neue Position vom VideoPlayer an den Parent zu transportieren (falls die API sie liefert).

### 4. Mehrere Fehlerquellen wirken zusammen
1. VideoPlayer setzt `StartPositionSeconds = null` (richtig)
2. VideoPlayer benachrichtigt Parent, aber OHNE neue Position (Callback hat kein Feld dafür)
3. Parent aktualisiert `playbackStart.StartPositionSeconds` nicht (fehlerhafte Implementierung)
4. Nächster Render: Parent übergibt alte `StartPositionSeconds` an VideoPlayer
5. VideoPlayer.OnAfterRenderAsync() wendet alte Position an
6. continueWatching.attach speichert falsche Position zurück

## Ursache-Kette der Regression

```
VideoPlayer.ApplyPlaylistNavigationResultAsync(result)
  ↓ ruft auf
VideoPlayer.ApplyEntryStreamInfo()  [setzt StartPositionSeconds = null]
VideoPlayer.NotifyPlaylistEntryChangedAsync()  [nennt Parent, aber ohne neue Position]
  ↓ Parent-Callback
PlaylistDetail.OnPlaylistEntryIdChanged(info)
  ↓ **FEHLER:** aktualisiert StartPositionSeconds NICHT
playbackStart.StartPositionSeconds bleibt = alte Position
  ↓ nächster Render
PlaylistDetail übergibt alte StartPositionSeconds an VideoPlayer
  ↓
VideoPlayer.OnAfterRenderAsync()
  ↓ wendet an
videoPlayer.setStartPosition(alte Position)
  ↓
continueWatching.attach() speichert falsche Position zurück
```

## Implementierungsansätze

Siehe `requirement.md` Abschnitte 85-117 für detaillierte Szenarien:

- **Szenario A (bevorzugt):** Backend erweitert `DtoPlaylistNavigationResult` um `StartPositionSeconds`, VideoPlayer übergibt diese im Callback, Parent aktualisiert damit.
- **Szenario B (Alternative):** Parent fragt neue Position separat ab (zusätzlicher API-Call pro Navigation).
- **Szenario C (Fallback):** Parent setzt `StartPositionSeconds = null` (schnell, aber ignoriert bereits gespeicherte Positionen).

Die minimale Änderung zur Korrektur der Regression wäre Szenario C: `PlaylistDetail.OnPlaylistEntryIdChanged()` setzt einfach `playbackStart.StartPositionSeconds = null;`. Dies würde die Regression beheben, aber die Anforderung nicht vollständig erfüllen (neue Titel sollten mit ihrer eigenen Position starten, nicht 0:00).

## Weitere Beobachtungen

- **Schritt 6, Runde 1 (a86fff3):** Hat bereits versucht, dieses Problem zu beheben (Commit-Nachricht: "fix: Nachbesserung Weiterschauen mit Playlist-Bezug (Schritt 6, Runde 1)"), aber anscheinend ist die Regression dennoch aufgetreten oder nicht vollständig behoben.
- **JavaScript-Hooks:** Die `continueWatching.attach()` Funktion arbeitet korrekt, speichert aber die Falsche Position zurück, weil diese vom `VideoPlayer` angewendet wurde (wegen des Parent-Fehlers).
- **Testabdeckung:** Der bestehende Test `PlaylistDetailTests.StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()` war nicht ausreichend, um diese Regression zu erfassen, da er nur Initial-Start testet, nicht Entry-Wechsel.

