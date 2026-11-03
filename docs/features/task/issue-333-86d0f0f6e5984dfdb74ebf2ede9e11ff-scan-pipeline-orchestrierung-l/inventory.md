# Bestandsaufnahme: Scan-Pipeline: Orchestrierung in einem Einstiegspunkt (Issue #333)

Analysiert wird der Scan-Pfad von Medienquellen (Worker `MediaSourceScanService`, manueller Komplettscan in `MediaSourceAdmin.razor`, Collection-Rescan in `MediaSourceExplorer.razor`, Test-Helfer) einschließlich der Wecksignale für Sendeplan und Playlist-Backfill, bezogen auf die übersetzte Anforderung in [requirement.md](requirement.md).

## Zusammenfassung

**Vorhanden**
- Der Worker `MediaSourceScanService.ExecuteAsync` enthält die vollständige Referenzsequenz: Scopes für Backfill und Sendeplan, Gate-Lease, Scan, Benachrichtigung, Genre-Reload, Collection-Schleife mit Obergrenze 64, Klassifizierung und Benachrichtigung. Details in [logic.md](inventory/logic.md).
- Die Signale `ProgramScheduleSignal` (`BeginScan`, `NotifyScheduleChanged`) und `PlaylistBackfillSignal` (`BeginScan`) haben Scope-Mechanik, die Schedule-Benachrichtigungen während eines Scans unterdrückt und am Scan-Ende einmal weckt. Siehe [interfaces.md](inventory/interfaces.md).
- Ein Vorbild für gebündelte Wecksignale ist vorhanden (`MediaDurationProbeWorkerTests.MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake`).
- `BackgroundProcessingGate` (Interface und Implementierung) liefert die Lease `EnterOperationAsync` und `IsPausedForRestore`, die beide UI-Pfade bereits nutzen.
- Der Scanner (`ScanAllSourcesAsync`, `ScanNextMediaCollection`, `ScanCollectionTreeAsync`) und der Classifier (`ClassifyAllAsync`, `ClassifyMediaItemsAsync`, `ClassifyMediaCollectionsAsync`, `ClassifyCollectionTreeAsync`, `CheckReloadGenres`) sind als Bausteine vorhanden.
- Die Test-Helfer `ScanEverythingAsync` und `RescanCollectionAsync` existieren in `LocalMediaPlaylistE2ETestBase` ([tests.md](inventory/tests.md#hilfsmethoden)).

**Fehlt bzw. nicht vorhanden**
- Kein Einstiegspunkt `MediaSourceScanCoordinator` (Klasse und Interface existieren nicht).
- Die beiden UI-Pfade (`RunFullScan`, `RescanCurrent`) und die Test-Helfer bauen die Sequenz eigenständig nach. Sie nutzen nur `IPlaylistBackfillSignal`, nicht `IProgramScheduleSignal`, und lösen keine Benachrichtigung aus.
- Kein Test prüft die UI-Pfade `RunFullScan` oder `RescanCurrent` direkt, und es gibt keinen Regressionstest für Reihenfolge und Scopes von Worker und manuellen Pfaden.

**Ist-Abweichungen zwischen Worker und manuellen Pfaden (faktisch belegt)**

| Punkt | Worker (`MediaSourceScanService`) | Admin `RunFullScan` | Explorer `RescanCurrent` |
|-------|-----------------------------------|---------------------|--------------------------|
| Schedule-Scope | ja (Zeile 103) | nein | nein |
| Backfill-Scope | ja (Zeile 101), vor dem Gate | ja (Zeile 231), nach dem Gate | ja (Zeile 262), nach dem Gate |
| Benachrichtigung `NotifyNewVideosScannedAsync` | ja (129, 166, 185) | nein | nein |
| Genre-Reload | vor der Collection-Schleife (132) | nach der Klassifizierung (251) | nein |
| Obergrenze Collections | 64 | keine (ungedeckelte Schleife, 237) | nicht zutreffend (Teilbaum) |
| Klassifizierung | `ClassifyMediaItemsAsync`, bedingt `ClassifyMediaCollectionsAsync` | `ClassifyAllAsync` | `ClassifyCollectionTreeAsync` (Rückgabe ungenutzt) |

Diese Tabelle beschreibt nur den Ist-Zustand und enthält keine Bewertung.

**Wecksignal-Befund:** Der Datenbankkontext ruft `NotifyScheduleChanged()` bei jedem Commit mit Backfill-Markern auf (`ApplicationDbContext.PlaylistBackfillMarking.cs`, Zeilen 85, 94, 133, 142), nicht nur bei „sendeplan-relevanten“ Commits. Die Quellenlöschung weckt zusätzlich (`ApplicationDbContext.cs`, Zeile 448). Außerhalb eines Scans wirkt jeder solche Commit sofort.

**Nomenklatur:** Die Anforderung nennt `src/`. Der Produktivcode liegt unter `VideoWebPlayer/`, die Tests unter `VideoWebPlayer.Tests/`.

**Test-Ausgangszustand:** Die Hauptsuite `VideoWebPlayer.Tests` lief vollständig (Debug, ohne Filter). Ergebnis: 2888 Tests, 2885 bestanden, 1 fehlgeschlagen, 2 übersprungen (Exit-Code 1). Der Fehlschlag `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout) ist im Ausgangslauf nachgewiesen, bei zwei Einzelwiederholungen aber grün. Er gilt damit als instabil und liegt außerhalb des Scan-Pfads. Die Suite `tools/MarkdownLinkCheck.Tests` lief mit 6 von 6 grün. Alle Läufe, Befehle, Exit-Codes und Nachweise stehen in [tests.md](inventory/tests.md) und unter `inventory/test-results/`.

**Testlücken:** Siehe Abschnitt „Testlücken und Ausführungsprobleme“ in [tests.md](inventory/tests.md#testlücken-und-ausführungsprobleme). Ein Test ist übersprungen, weil der Testcode einen bekannten Fehler im Continue-Watching-Dienst dokumentiert (500 bei Film ohne Filmsammlung). Dieser Befund liegt außerhalb des Scan-Pfads und wurde nicht geprüft.

**Beobachtungen ohne Änderung:**
- `CheckReloadGenres` (`MediaSourceClassifier.cs`, Zeilen 2086–2087) verwendet im `catch`-Zweig `setup` ohne Null-Prüfung.
- `MediaSourceScanService.cs` und `ApplicationDbContext.cs` enthalten Zeichen in Windows-1252 (fehlerhafte Umlaute in Kommentaren und Logtexten); `ApplicationDbContext.cs` wird vom Editor deshalb nicht bearbeitet.

## Details

- [Datenmodell](inventory/models.md): `MediaCollection`, `MediaSource`, `Setup` und die geerbten Zeitstempel- und Änderungsfelder für den Scan-Pfad.
- [Logik](inventory/logic.md): Worker-Sequenz, Scanner, Classifier, Wecksignale, Gate, Benachrichtigungsdienst und die beiden manuellen Einstiege.
- [Interfaces](inventory/interfaces.md): `IProgramScheduleSignal`, `IPlaylistBackfillSignal`, `IBackgroundProcessingGate`.
- [Tests](inventory/tests.md): Test-Ausgangszustand, Testläufe mit Nachweisen, Testklassen und Hilfsmethoden, Testlücken.

Keine Detaildatei angelegt: Enums (keine für die Anforderung relevanten Enums gefunden).
