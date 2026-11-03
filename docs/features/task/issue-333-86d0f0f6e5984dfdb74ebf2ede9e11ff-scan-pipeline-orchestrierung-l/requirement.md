# Übersetzte Anforderung: Scan-Pipeline: Orchestrierung in einem Einstiegspunkt (Issue #333)

## Fachliche Zusammenfassung

Die Komplettscan-Sequenz einer Medienquelle (Scannen, Klassifizieren, Genre-Reload, Benachrichtigung, Signal-Scopes) wird in einem einzigen Einstiegspunkt gekapselt, den der Worker (`MediaSourceScanService.ExecuteAsync`), der manuelle Komplettscan (`MediaSourceAdmin.razor::RunFullScan`), der Collection-Rescan (`MediaSourceExplorer.razor::RescanCurrent`) und die Test-Helfer gemeinsam aufrufen. Dadurch entfallen die drei divergierenden Aufrufer mit abweichender Reihenfolge und fehlenden Nebenbedingungen (`BeginScan(ProgramSchedule)`, Benachrichtigung, Drain-Cap).

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein Scan-Lauf einer Medienquelle bzw. eines Collection-Teilbaums inklusive aller Folgeschritte bis zur Klassifizierung und Benachrichtigung.
- **Auslösende Ereignisse:** Manueller Klick auf „Komplettscan aller Quellen“ (Admin), manueller Klick auf „Neu erfassen“ (Explorer, bezogen auf die aktuelle Collection), zyklischer Lauf des Workers.
- **Erwartete Wirkung:** Nach einem manuellen Lauf sind Klassifizierung, Genres und Sendeplan konsistent und der Sendeplan wird gebündelt am Ende des Laufs neu berechnet, nicht je Commit mitten im Scan. Nutzer sehen den Abschluss des Laufs; Benachrichtigungen zu neuen Videos erscheinen wie beim Worker.
- **Umgang mit fehlenden Objekte:**
  - `BackgroundGate`-Lease (Operation „Manueller Komplettscan“ bzw. Worker-Lauf): erzeugen für die Dauer des Laufs; ist das Gate durch Restore gesperrt, wird der Lauf verworfen mit Statusmeldung (wie heute).
  - `IPlaylistBackfillSignal`- und `IProgramScheduleSignal`-Scope (`BeginScan()`): erzeugen vor dem Lauf und am Ende freigeben, sodass Commits während des Laufs nicht wecken und das Ende des Laufs wecken.
  - Genre-Reload-Zustand (`CheckReloadGenres`): wiederverwenden, wird in der Worker-Reihenfolge ausgeführt.
  - Benachrichtigungsdienst (`NotifyNewVideosScannedAsync`): wiederverwenden.
- **Scope-Ausschlüsse:**
  - Der Zeitplan des Workers, sein Intervall und die Sendeplan-Logik selbst werden nicht geändert.
  - Laufende Läufe werden nicht abgebrochen oder neu priorisiert.
  - Der Collection-Rescan bleibt auf den Teilbaum der aktuellen Collection beschränkt; er scannt keine ganze Quelle.
  - Die Test-Helfer werden nur auf den Einstiegspunkt umgestellt; deren Testinhalt (Assertions) wird nicht fachlich verändert.
  - Keine Änderung am Datenmodell.

## Betroffene Klassen und Komponenten

- **Datenmodellklassen:** keine Änderung erwartet.
- **Logikklassen / Services:**
  - Neu (vorgeschlagen): `MediaSourceScanCoordinator` mit `RunFullScanAsync` und `RescanCollectionTreeAsync`.
  - Bestehend, aufzurufen oder anzupassen: `MediaSourceScanService` (Worker-Referenzsequenz), `MediaSourceScanner` (`ScanAllSourcesAsync`, `ScanNextMediaCollection`), `MediaSourceClassifier` (`ClassifyAllAsync`, `ClassifyCollectionTreeAsync`, `CheckReloadGenres`, `ClassifyMediaItemsAsync`, `ClassifyMediaCollectionsAsync`), Benachrichtigungsdienst (`NotifyNewVideosScannedAsync`).
- **Interfaces:** `IProgramScheduleSignal` (`BeginScan`, `NotifyScheduleChanged`), `IPlaylistBackfillSignal` (`BeginScan`), `BackgroundGate` (`EnterOperationAsync`).
- **Enums:** keine.
- **UI-Komponenten / Controller:**
  - `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` (`RunFullScan`): auf den Einstieg umstellen.
  - `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` (`RescanCurrent`): auf den Einstieg umstellen.
- **Tests:**
  - `VideoWebPlayer.Tests/Helpers/LocalMediaPlaylistE2ETestBase.cs` (`ScanEverythingAsync`, Collection-Rescan-Helfer): auf den Einstieg umstellen.
  - `VideoWebPlayer.Tests/LocalMediaPlaylistE2ETests_*.cs`, `AgeRatingE2ETests`, `LocalMediaSourcePipelineE2ETests`: auf den Einstieg umstellen, soweit sie eigene Sequenzen nachbauen.
  - `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`: Regressionstest, dass Worker und manuelle Pfade dieselbe Reihenfolge und dieselben Scopes verwenden.
  - Neuer Test: Ein manueller Komplettscan unterdrückt Schedule-Wecken je Commit (Bündelung am Ende).

## Implementierungsansatz

- **Events, Hooks, Erweiterungspunkte:** `ApplicationDbContext` ruft bei sendeplan-relevanten Commits `NotifyScheduleChanged()` auf (`ApplicationDbContext.cs`, `ApplicationDbContext.PlaylistBackfillMarking.cs`). Der Worker unterdrückt das über `programScheduleScanScope`; der Einstieg muss denselben Scope für jeden Lauf setzen.
- **Neue Klasse:** `MediaSourceScanCoordinator` kapselt die Worker-Reihenfolge: Gate-Lease, `BeginScan(PlaylistBackfill)`, `BeginScan(ProgramSchedule)`, `ScanAllSourcesAsync` (bzw. Collection-Teilbaum), `NotifyNewVideosScannedAsync`, `CheckReloadGenres`, Drain mit Cap, Klassifizierung. Das Muster entspricht `ExecutePassAsync` im Sendeplan (`problem-sendeplan-orchestrierung.md`).
- **Abhängigkeiten:** Der Einstieg hängt von den bestehenden Scanner-, Classifier-, Signal- und Gate-Diensten ab; die UI-Komponenten und Tests werden auf den Einstieg umgestellt.
- **Kurzfristige Variante ohne Umbau:** `IProgramScheduleSignal.BeginScan()` in `RunFullScan` und `RescanCurrent` ergänzen. Dies schließt die Lücke beim Schedule-Scope, behebt aber nicht die divergierende Reihenfolge und die fehlende Benachrichtigung.

## Konfiguration

Keine Konfiguration erforderlich. Der Drain-Cap (64 je Lauf) ist derzeit im Worker fest codiert; ob er auch für manuelle Läufe gilt, ist offen (siehe unten).

## Offene Fragen

1. Sollen manuelle Läufe ebenfalls den Drain-Cap (64) verwenden? Der Admin-Pfad läuft aktuell ungedeckelt (`while ScanNextMediaCollection`).
2. Ist die Abweichung der Genre-Reload-Reihenfolge im Admin (nach der Klassifizierung statt davor) bewusst oder ein Fehler?
3. Sollen manuelle Läufe Benachrichtigungen zu neuen Videos auslösen wie der Worker?
4. Soll `RescanCurrent` ebenfalls Genre-Reload und Benachrichtigung ausführen, oder nur Scan und Klassifizierung des Teilbaums?
5. Soll die kurzfristige Variante (`BeginScan` in beiden UI-Pfaden) vor dem Umbau umgesetzt werden?
6. Namensgebung des Einstiegs: `MediaSourceScanCoordinator.RunFullScanAsync` / `RescanCollectionTreeAsync` oder anders?
