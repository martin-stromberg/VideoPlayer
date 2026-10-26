# Offene Aufgaben

Erstellt am: 2026-10-26
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (12 offene Punkte in Iteration 1 → 13 in Iteration 2)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

(Fortsetzungslauf: alle Punkte erledigt — Reviews `review-code.md`/`review-usability.md`
jeweils „Keine Befunde", Testsuite 2491/2493 grün mit 2 bekannten Skips.)

## Offene Planelemente

Keine. (`review.md`: Vollständig umgesetzt)

## Code-Review-Befunde

- [x] `LiveStreamStandbySegmenter.cs` — Toter Code: `using System.Globalization;` (Z. 4) ungenutzt → Using entfernt.
- [x] `LiveStreamStandbySegmenter.cs` — Fehlerbehandlung/Logik: Alt-Segment-Löschung schluckte Fehler lautlos; `Directory.GetFiles(…, "standby_*.ts")` nahm nicht löschbare Altsegmente unbemerkt in den neuen Satz auf → Löschfehler wird geloggt, Reste mit Änderungsstempel (`staleLeftOver`) aus dem neuen Satz ausgeschlossen.
- [x] `LiveStreamStandbySegmenter.cs` — Fehlerbehandlung/Timeout: `await stderrTask` unbegrenzt → `stderrTask.WaitAsync(_stderrDrainTimeout)` (10 s, intern injizierbar), Kill-Fehler im Timeout-Pfad wird geloggt, faultierter Drain wird beobachtet.
- [x] `SegmenterProcessSupport.cs` — Fehlerbehandlung: `catch (Exception) { return null; }` in `StartRealProcess` verschluckte die Startursache → Logger-Parameter ergänzt, Exception samt `FileName` wird mit Caller-Präfix geloggt.
- [x] `LiveStreamStandbyProvider.cs` — Toter Code: `StandbyCacheMarker.PreparedAtUtc` write-only + `DateTime.UtcNow` statt `_timeProvider` → Feld entfernt.
- [x] `LiveStreamController.cs` — Toter Filter/falsche Statuszuordnung: `or UnauthorizedAccessException` unerreichbar; Datei-Zugriffsfehler wurden als 401 gemeldet → `CheckLoggedIn()` in eigenem try/catch (401 bleibt dem Anmeldefehler vorbehalten), alle übrigen Ausnahmen → geloggter 500.
- [x] `FfmpegLiveStreamSegmenterE2ETests.cs` — dritte `FindBinary`-Kopie entfernt, `E2EBinaryLocator.Find("ffmpeg")` an beiden Aufrufstellen.
- [x] `LiveTvE2ETests.cs` — verschobener `<summary>`-Block zurück über `SeedChannelWithRunningProgramAsync` gelegt.

## Usability-Befunde

- [x] `TVSenderAdmin.razor` — Sackgasse beim Speichern: verwaiste Standby-Auswahl blockierte jedes „Speichern" → `LoadStandbyLabelAsync` setzt die verwaiste Auswahl beim Anzeigen des Fallback-Textes still auf Standard zurück (sichtbarer Zustand = wirksamer Zustand); bUnit-Test `StandbyCard_OrphanedSelection_SaveStillWorks`.
- [x] `TVSenderAdmin.razor` — Fachjargon: Überschrift jetzt „Standby-Clip (Überbrückung)", Hilfetext anwenderorientiert („Wird gezeigt, bis der Live-Stream nach einem Server-Start wirklich läuft — ebenso bei Unterbrechungen. Auswählbar sind Videodateien ohne Film-/Serien-Zuordnung.").
- [x] `TVSenderAdmin.razor` — Ungespeicherte Auswahl ohne Hinweis → Hinweis `#tvsender-standby-pending` („Auswahl wird mit ‚Speichern' übernommen"), solange die angezeigte Wahl vom gespeicherten Stand abweicht (`standbySelectionPending`/`standbySavedMediaItemId`); konsistent zum gebündelten Speichern des Tabs.
- [x] `TVSenderAdmin.razor` — Picker-Toggle: bei geöffnetem Picker „Auswahl schließen" + `aria-expanded`/`aria-controls`.
- [x] `TVSenderAdmin.razor` — Leerzustand-Flash: „Clips werden geladen…" (`#tvsender-standby-loading`) während der ersten Abfrage und jeder Suche/Filterung; „Weitere laden" während des Nachladens gesperrt (`standbyCandidatesLoading`).

## Fehlgeschlagene Tests

Keine. (`test-results.md`: 2493 Tests — 2491 bestanden, 0 fehlgeschlagen, 2 bekannte Skips.)
