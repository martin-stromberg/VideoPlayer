# Umsetzungsplan: Scan-Pipeline: Orchestrierung in einem Einstiegspunkt (Issue #333)

## Übersicht

Die Komplettscan-Sequenz einer Medienquelle (Wecksignal-Scopes, Gate-Lease, Scan, Benachrichtigung, Genre-Reload, Collection-Schleife, Klassifizierung) wird in einer neuen Service-Klasse `MediaSourceScanCoordinator` gekapselt. Der Worker `MediaSourceScanService`, der manuelle Komplettscan in `MediaSourceAdmin.razor` und der Collection-Rescan in `MediaSourceExplorer.razor` rufen diesen Einstieg auf; die Test-Helfer folgen. Betroffen sind damit `VideoWebPlayer/Services` (neue Klasse, Worker), `VideoWebPlayer/Components/Pages/Admin/MediaSources` (zwei Razor-Komponenten) sowie `VideoWebPlayer.Tests` (Helfer und neue Tests). Datenmodell, Migrationen und Konfiguration bleiben unverändert.

Hinweis zur Nomenklatur: Die Anforderung nennt `src/`. Produktivcode liegt unter `VideoWebPlayer/`, Tests unter `VideoWebPlayer.Tests/`. Dieser Plan verwendet die tatsächlichen Pfade.

## Scope-Klärung

Übernommen aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Scan-Lauf einer Medienquelle (Komplettscan, Worker-Zyklus) bzw. eines Collection-Teilbaums (Neu erfassen), inklusive Klassifizierung und Benachrichtigung. |
| Auslösende Ereignisse | Klick „Komplettscan aller Quellen“ (Admin); Klick „Neu erfassen“ (Explorer, aktuelle Collection); zyklischer Lauf des Workers. |
| Erwartete Wirkung | Nach einem manuellen Lauf sind Klassifizierung, Genres und Sendeplan konsistent. Der Sendeplan wird gebündelt am Laufende geweckt, nicht je Commit mitten im Scan. Nutzer sehen den Abschluss; neue Videos lösen wie beim Worker eine Benachrichtigung aus. |
| Umgang mit fehlenden Objekten | `BackgroundGate`-Lease: wird pro Lauf erzeugt; ist das Gate durch einen Restore pausiert, wird der manuelle Lauf verworfen mit Statusmeldung (wie heute). `IPlaylistBackfillSignal.BeginScan()` und `IProgramScheduleSignal.BeginScan()`: werden vor dem Lauf erzeugt und am Laufende freigegeben, damit Commits während des Laufs nicht wecken, das Laufende aber einmal weckt. Genre-Reload-Zustand (`Setup.GenresChanged`): wird wiederverwendet (`CheckReloadGenres`). Benachrichtigungsdienst: wird wiederverwendet. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Zeitplan, Intervall (Default 1 h) und Sendeplan-Logik des Workers bleiben unverändert. | Anforderung: nur Einstiegspunkt, keine Änderung der Zeitsteuerung. | festgelegt |
| 2 | Laufende Läufe werden nicht abgebrochen oder neu priorisiert. | Anforderung. | festgelegt |
| 3 | Der Collection-Rescan bleibt auf den Teilbaum der aktuellen Collection beschränkt; er scannt keine ganze Quelle. | Anforderung. | festgelegt |
| 4 | Test-Helfer werden nur auf den Einstiegspunkt umgestellt; Assertions der bestehenden Tests werden nicht fachlich verändert. | Anforderung. | festgelegt |
| 5 | Keine Änderung am Datenmodell, keine Migration. | Anforderung. | festgelegt |
| 6 | Das Wecken des Sendeplans durch `ApplicationDbContext` bei Commits mit Backfill-Markern außerhalb eines Scans bleibt unverändert (keine Änderung an `ApplicationDbContext.PlaylistBackfillMarking.cs`). Die Anforderung spricht von „sendeplan-relevanten“ Commits; der Ist-Zustand weckt bei jedem Marker-Commit. Die Bündelung gilt nur innerhalb von Läufen. | Eigenes GitHub-Issue https://github.com/martin-stromberg/VideoPlayer/issues/337 („Sendeplan: Jeder Commit mit sendeplan-relevanten Entitaeten weckt den Worker sofort“). Die Hooks liegen außerhalb des Auftrags; die Datei ist in Windows-1252 kodiert (Editor-Risiko). | ausgelagert an Issue #337 |
| 7 | Der Drain-Cap (64) gilt nur für den Worker; der manuelle Komplettscan (`RunFullScanAsync` mit `null`) läuft ungedeckelt und vollständig durch. | Anwenderentscheidung. | festgelegt |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Einstiegspunkt | Service Layer: Klasse `MediaSourceScanCoordinator` (`VideoWebPlayer/Services/MediaSourceScanCoordinator.cs`) mit zwei Methoden `RunFullScanAsync` und `RescanCollectionTreeAsync`. Kein separates Interface. | Entspricht der Anforderung (Klasse). Interface nicht nötig, da kein zweiter Implementierer und Tests die Klasse mit echten Diensten (SQLite, lokales Verzeichnis) betreiben. |
| Lebensdauer | Scoped, wie `MediaSourceScanner` und `MediaSourceClassifier`. Worker erzeugt ihn im `innerScope`, Razor-Komponenten über die Blazor-Scope. | Scanner und Classifier nutzen den DbContext des Scopes; der Koordinator muss denselben Lebenszyklus haben. Lebensdauer in `Program.cs` und im Test-Host vor Schritt 2 verifizieren. |
| Abhängigkeiten | Konstruktorinjektion: `MediaSourceScanner`, `MediaSourceClassifier`, `IPlaylistBackfillSignal`, `IProgramScheduleSignal`, `MediaUpdateNotificationService`, `ApplicationDbContext`, `ILogger`; `IBackgroundProcessingGate` optional (wie heute im Worker). | Der Worker toleriert fehlende Signale/Gate per `GetService`. Beibehalten, damit Test-Hosts ohne Gate weiter laufen. Signale werden für die Aufrufer als vorhanden angenommen, wenn Schritt 1 das bestätigt. |
| Gate-Verhalten | Der Koordinator erwirbt die Lease immer per `EnterOperationAsync(operationName)` und wartet dabei wie der Worker. Die Pausierungsprüfung `IsPausedForRestore` bleibt in den Razor-Komponenten vor dem Aufruf (heutige Statusmeldung bleibt erhalten). | Worker wartet auf Restore-Ende; manuelle Pfade verwerfen vorab. Dieses Verhalten bleibt je Aufrufer erhalten, ohne Parameter-Verzweigung im Koordinator. |
| Operationsname | Parameter `operationName` (Worker: „Automatischer Scanprozess“, Admin: „Manueller Komplettscan“, Explorer: „Manueller Collection-Scan“). | Die Gate-Lease-Namen werden heute in Logs und Diagnose verwendet; sie bleiben unverändert. |
| Reihenfolge | Verbindlich die Worker-Reihenfolge (Scopes vor Gate, Zählung, Scan, Benachrichtigung, Genre-Reload, Collection-Schleife, Klassifizierung mit bedingter Collection-Klassifizierung und Benachrichtigung). Scopes werden am Ende in umgekehrter Reihenfolge freigegeben (Gate, Schedule-Scope, Backfill-Scope), damit das Laufende genau einmal weckt. | Anforderung nennt den Worker als Referenz; damit entfallen die divergierenden Abläufe. |
| Schedule-Scope in UI | Jeder Einstieg öffnet `IProgramScheduleSignal.BeginScan()`. | Schließt die Lücke aus der Bestandsaufnahme (Admin und Explorer ohne Schedule-Scope). |
| Benachrichtigung in UI | Jeder Einstieg ruft `NotifyNewVideosScannedAsync(0L, delta)` mit der Differenz der `MediaItems`-Zählung, wie der Worker. | Anforderung nennt Benachrichtigungen ausdrücklich. Anwenderentscheidung: auch bei manuellen Läufen. |
| Klassifizierung im Explorer | `ClassifyCollectionTreeAsync` liefert `false`, wenn der Baum in die Warteschlange gestellt wurde (Klassifizierung läuft bereits). Der Koordinator gibt dies im Ergebnis (`ClassificationDeferred`) zurück; die Razor-Komponente zeigt dann „Klassifizierung läuft im Hintergrund, das Ergebnis erscheint nach Abschluss“. | Heute wird der Rückgabewert ignoriert; der Nutzer bekäme sonst ohne Hinweis einen unvollständigen Stand. |
| Ergebnistypen | Records `MediaSourceScanRunResult` (`NewItemsCount`, `CollectionsScanned`, `CapReached`) und `CollectionRescanResult` (`NewItemsCount`, `ClassificationDeferred`). | Die Aufrufer brauchen Statusmeldungen; Logik bleibt im Koordinator. |
| Cap | Parameter `maxCollectionsPerRun` (`int?`); Worker übergibt 64 (Konstante bleibt im Worker), manueller Komplettscan übergibt `null` (ungedeckelt). | Anwenderentscheidung: kein Cap bei manuellen Läufen. |
| Genre-Reload im Explorer | Der Koordinator führt `CheckReloadGenres` auch im Collection-Rescan aus. | `CheckReloadGenres` ist flag-gesteuert und idempotent, ein Lauf ohne Flag bleibt ohne Wirkung. Anwenderentscheidung: Ja, Genre-Reload und Benachrichtigung auch bei „Neu erfassen“. |
| Zählung und Scope im Gate | Scopes und Zählung liegen im Koordinator, nicht mehr in den Aufrufern. | Single Responsibility; Test-Helfer rufen dann denselben Code auf. |
| Kurzfristige Variante | Kein Vorab-Hotfix (`IProgramScheduleSignal.BeginScan()` nur in den UI-Pfaden); direkt der Koordinator. | Anwenderentscheidung. Die Variante behebt die divergierende Reihenfolge und die fehlende Benachrichtigung nicht. |
| Muster | Service Layer mit Ergebnisobjekten (PoEAA); vergleichbar mit `ExecutePassAsync` im Sendeplan (`problem-sendeplan-orchestrierung.md`). | Vorhandenes Muster im Projekt. |

## Programmabläufe

### Komplettscan (`RunFullScanAsync`, Worker und Admin)

1. Aufrufer übergibt `operationName` und `maxCollectionsPerRun` (Worker: 64; Admin: `null`).
2. `IPlaylistBackfillSignal.BeginScan()` öffnet den Backfill-Scope (falls Signal vorhanden).
3. `IProgramScheduleSignal.BeginScan()` öffnet den Schedule-Scope (falls Signal vorhanden).
4. `IBackgroundProcessingGate.EnterOperationAsync(operationName)` erwirbt die Lease (falls Gate vorhanden); wartet bei Restore.
5. Zählung der `MediaItems` vor dem Scan.
6. `MediaSourceScanner.ScanAllSourcesAsync`.
7. Zählung nach dem Scan; Differenz = `NewItemsCount`.
8. `MediaUpdateNotificationService.NotifyNewVideosScannedAsync(0L, NewItemsCount)`.
9. `MediaSourceClassifier.CheckReloadGenres`.
10. Schleife `MediaSourceScanner.ScanNextMediaCollection`, bis `false` oder Cap erreicht; zählt `CollectionsScanned`.
11. Wenn mindestens eine Collection gescannt: `ClassifyMediaItemsAsync`. Wenn `unclassifiedBefore > 0` (Zählung über `MediaCollections`): `ClassifyMediaCollectionsAsync` und `NotifyNewVideosScannedAsync(0L, unclassifiedBefore)`.
12. Sonst: `ClassifyMediaCollectionsAsync` und `NotifyNewVideosScannedAsync(0L, unclassifiedBefore)`.
13. Freigabe: Gate-Lease, dann Schedule-Scope, dann Backfill-Scope (Dispose in umgekehrter Reihenfolge). Das Schedule-Wecken erfolgt hier, falls unterdrückt.
14. Rückgabe `MediaSourceScanRunResult`.

Fehlerbehandlung: `OperationCanceledException` bei Stop wird nach oben gereicht (Worker beendet die Schleife). Andere Exceptions werden vom Worker wie heute geloggt; die Schleife läuft weiter. Für die UI-Aufrufer werden Fehler im Razor-Code wie heute als Statusmeldung angezeigt.

Beteiligte Klassen/Komponenten: `MediaSourceScanCoordinator`, `MediaSourceScanner`, `MediaSourceClassifier`, `MediaUpdateNotificationService`, `PlaylistBackfillSignal`, `ProgramScheduleSignal`, `BackgroundProcessingGate`, `ApplicationDbContext`.

### Collection-Rescan (`RescanCollectionTreeAsync`, Explorer)

1. Aufrufer (`MediaSourceExplorer.razor::RescanCurrent`) prüft vorab `IsPausedForRestore` und zeigt bei `true` die bestehende Statusmeldung.
2. Übergabe von `collectionId` und `operationName` („Manueller Collection-Scan“).
3. Backfill-Scope und Schedule-Scope öffnen.
4. Gate-Lease erwerben.
5. Zählung der `MediaItems` vor dem Scan.
6. `MediaSourceScanner.ScanCollectionTreeAsync(collectionId)`.
7. Zählung nach dem Scan; Differenz = `NewItemsCount`; `NotifyNewVideosScannedAsync(0L, NewItemsCount)`.
8. `CheckReloadGenres`.
9. `MediaSourceClassifier.ClassifyCollectionTreeAsync(collectionId)`; Rückgabe `false` setzt `ClassificationDeferred = true`.
10. Freigabe in umgekehrter Reihenfolge (Gate, Schedule-Scope, Backfill-Scope).
11. Rückgabe `CollectionRescanResult`. Die Razor-Komponente lädt danach die aktuelle Collection neu und zeigt die Meldung (ggf. mit Hinweis auf die Hintergrundklassifizierung).

Beteiligte Klassen/Komponenten: `MediaSourceScanCoordinator`, `MediaSourceScanner`, `MediaSourceClassifier`, `MediaUpdateNotificationService`, `PlaylistBackfillSignal`, `ProgramScheduleSignal`, `BackgroundProcessingGate`, `MediaSourceExplorer.razor`.

### Worker-Zyklus (`MediaSourceScanService.ExecuteAsync`)

1. Unverändert: Start, Upgrade-Prüfung, `initialDelay`, Schleife, Intervallprüfung über `ProgramSettingsService.GetScanIntervalsAsync`.
2. Bei fälligem Lauf: `innerScope` erzeugen, `MediaSourceScanCoordinator` daraus beziehen und `RunFullScanAsync("Automatischer Scanprozess", 64)` aufrufen.
3. Die Logmeldungen „Starte Scanprozess (Intervall: …).“, „Starte Scan aller Quellen.“ und „Scan aller Quellen abgeschlossen. … neue Items gefunden.“ bleiben erhalten (die Tests warten darauf). Sie werden vom Koordinator bzw. Worker an derselben Stelle ausgegeben.
4. Fehlerbehandlung und Schleifenverzögerung unverändert.

Beteiligte Klassen/Komponenten: `MediaSourceScanService`, `MediaSourceScanCoordinator`.

### Admin-Komplettscan (`MediaSourceAdmin.razor::RunFullScan`)

1. Unverändert: Prüfung `IsPausedForRestore` (Statusmeldung bei `true`).
2. Aufruf `RunFullScanAsync("Manueller Komplettscan", null)`.
3. Statusaktualisierung je Quelle/Collection entfällt, weil der Koordinator die Schleife kapselt; stattdessen Status „Läuft…“ vor dem Aufruf und Ergebnismeldung danach (`CollectionsScanned`, `NewItemsCount`). Die bisherige fortlaufende Statusaktualisierung mit `Task.Yield` wird nicht übernommen (siehe Seiteneffekte).
4. `LoadSources` und Fehlerstatus wie bisher.

Beteiligte Klassen/Komponenten: `MediaSourceAdmin.razor`, `MediaSourceScanCoordinator`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MediaSourceScanCoordinator` | Klasse (Service Layer) | Einstiegspunkt für Komplettscan und Collection-Rescan; kapselt Scopes, Gate, Scan, Benachrichtigung, Genre-Reload, Klassifizierung. |
| `MediaSourceScanRunResult` | Record (Ergebnistyp) | Ergebnis eines Komplettscans: `NewItemsCount`, `CollectionsScanned`, `CapReached`. |
| `CollectionRescanResult` | Record (Ergebnistyp) | Ergebnis eines Collection-Rescans: `NewItemsCount`, `ClassificationDeferred`. |

Die Records können in derselben Datei wie der Koordinator liegen, sofern das Projektschema dies erlaubt (Konvention prüfen, Schritt 2).

## Änderungen an bestehenden Klassen

### `MediaSourceScanService` (BackgroundService)

- **Neue Abhängigkeiten:** keine im Konstruktor; der Koordinator wird im `innerScope` per `GetRequiredService` bezogen.
- **Geänderte Methoden:** `ExecuteAsync` — Scope-, Gate-, Zählungs- und Klassifizierungslogik (Block ab Zeile 101 bis 186) entfällt und wird durch einen Aufruf von `RunFullScanAsync` ersetzt. Die Logmeldungen bleiben erhalten.
- **Neue Events:** keine. **Neue Event-Handler:** keine.
- Hinweis Encoding: Die Datei ist in Windows-1252 kodiert (Umlaute in Kommentaren und Logtexten). Die Änderung muss die Kodierung erhalten (siehe Seiteneffekte).

### `MediaSourceAdmin.razor` (Razor-Komponente)

- **Neue Abhängigkeiten:** `MediaSourceScanCoordinator` (injiziert); `IPlaylistBackfillSignal`, `MediaSourceScanner`, `MediaSourceClassifier` und `BackgroundProcessingGate` entfallen aus `RunFullScan`, falls sonst nicht benötigt (prüfen).
- **Geänderte Methoden:** `RunFullScan` — ruft `RunFullScanAsync("Manueller Komplettscan", null)`; zeigt Ergebnismeldung. Die Pausierungsprüfung bleibt.
- **Neue Events:** keine.

### `MediaSourceExplorer.razor` (Razor-Komponente)

- **Neue Abhängigkeiten:** `MediaSourceScanCoordinator`; `IPlaylistBackfillSignal`, `MediaSourceScanner`, `MediaSourceClassifier` und `BackgroundProcessingGate` entfallen aus `RescanCurrent`, falls sonst nicht benötigt (prüfen).
- **Geänderte Methoden:** `RescanCurrent` — ruft `RescanCollectionTreeAsync(currentCollection.Id, "Manueller Collection-Scan")`; wertet `ClassificationDeferred` aus und zeigt den Hinweis.
- **Neue Events:** keine.

### `LocalMediaPlaylistE2ETestBase` (Testbasis)

- **Geänderte Methoden:** `ScanEverythingAsync` und `RescanCollectionAsync` rufen den Koordinator auf, statt die Sequenz nachzubauen. Die Test-Host-Registrierung von `MediaSourceScanCoordinator` (und Signal-/Gate-Dienste) ist Voraussetzung.

### `AgeRatingE2ETests`, `LocalMediaSourcePipelineE2ETests` (Tests)

- **Geänderte Methoden:** Eigene Sequenz (Zeilen 230–232 bzw. 109–111) durch Koordinator-Aufruf ersetzen (Lauf ohne Cap, mit Scope und Benachrichtigung).

## Datenbankmigrationen

Keine. Keine neuen Tabellen oder Spalten; `VideoWebPlayerBackupData` (`OptionalRestoreTables`/`OptionalRestoreColumns`) bleibt unverändert, ein Regressionstest für Alt-Backups ist daher nicht erforderlich.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `collectionId` (`RescanCollectionTreeAsync`) | Muss eine vorhandene Collection sein. | Ist sie zwischen Laden und Klick gelöscht, darf keine Exception an der UI ankommen; Statusmeldung „Collection nicht mehr vorhanden“ (Schritt 7 prüft das Verhalten von `ScanCollectionTreeAsync` und ergänzt die Prüfung im Koordinator bzw. Razor-Code). |
| `maxCollectionsPerRun` | Wenn gesetzt, muss der Wert größer als 0 sein. | Ungültig: `ArgumentOutOfRangeException` im Koordinator (interner Aufruf, keine Benutzereingabe). |

## Konfigurationsänderungen

Keine. Der Cap 64 bleibt eine Konstante im Worker (als Argument übergeben), keine `appsettings`-Einträge.

## Seiteneffekte und Risiken

- **Admin-Komplettscan (Fortschritt):** Der Admin-Pfad aktualisiert heute den Status pro Collection (`Task.Yield`, Zeilen 237–243). Nach der Umstellung entfällt die fortlaufende Anzeige während des Laufs; es gibt nur „Läuft…“ und das Ergebnis. Anwenderentscheidung: keine Fortschrittsanzeige; die Ergebnismeldung genügt.
- **Benachrichtigungen:** Manuelle Läufe senden nun SignalR-Meldungen (`NotifyNewVideosScannedAsync`). Die Admin- und Explorer-Seiten reagieren auf diese Meldungen eventuell (Lade-Logik). Prüfen, ob Seiten auf `NotifyNewVideosScannedAsync` abonniert sind; falls ja, Doppelladen vermeiden.
- **Sendeplan-Wecken:** Manuelle Läufe unterdrücken Wecken je Commit und wecken am Ende einmal. Außerhalb von Läufen bleibt das Verhalten unverändert (Scope-Ausschluss 6, eigenes Issue #337). Der Sendeplan-Worker reagiert nur verzögert bis zum Laufende.
- **Genre-Reload-Reihenfolge im Admin:** Der Genre-Reload läuft nun vor der Collection-Schleife statt danach. Anwenderentscheidung: die bisherige Admin-Reihenfolge war ein Fehler; die Worker-Reihenfolge (Genres vor Klassifizierung) wird übernommen. Das ändert die Klassifizierungs-Eingaben im manuellen Pfad.
- **Klassifizierungs-Warteschlange (Explorer):** Ist die Klassifizierung bereits aktiv, reiht `ClassifyCollectionTreeAsync` den Baum ein. Die Meldung im UI muss das klar sagen (Designentscheidung oben).
- **Gate-Lease-Reihenfolge:** Im manuellen Pfad werden Scopes vor dem Gate geöffnet (bisher Gate zuerst). Während eines Restores halten Scopes kurz länger; keine fachliche Wirkung erwartet, durch Test abzusichern.
- **Encoding:** `VideoWebPlayer/Services/MediaSourceScanService.cs` ist Windows-1252. Der Editor kann Umlaute beschädigen. Änderungen mit kodierungserhaltenden Werkzeugen durchführen und `git diff` auf unbeabsichtigte Zeichenänderungen prüfen. `ApplicationDbContext.cs` wird nicht geändert.
- **Bestehende Tests mit Logmeldungen:** `MediaSourceScanServiceTests.ExecuteAsync_RunsScanAndClassification_WhenNoCollectionsExist` wartet auf „Starte Scan aller Quellen.“ und „Scan aller Quellen abgeschlossen.“. Diese Meldungen müssen exakt erhalten bleiben.
- **Instabiler bestehender Test:** `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` ist im Ausgangslauf einmal fehlgeschlagen und bei Wiederholung grün; außerhalb des Scan-Pfads. Bei Fehlschlag in der Abnahme einzeln wiederholen und im Bericht nennen.
- **Bestehende Befunde (nicht Teil dieses Plans):** `CheckReloadGenres` (`MediaSourceClassifier.cs` Zeilen 2086–2087) verwendet im `catch` die Variable `setup` ohne Null-Prüfung; nur beobachten, nicht ändern, außer die Änderung fällt in den Scan-Pfad und wird bewusst in einem eigenen Commit behoben.
- **Unerwartete Nebenwirkung im Test-Host:** Benachrichtigungen benötigen SignalR-Hub. `TestableMediaSourceScanService` nutzt einen Null-Hub; die E2E-Hosts verwenden den echten Hub. Vor Schritt 9 prüfen.

## Umsetzungsreihenfolge

1. **Voraussetzungen im Repo verifizieren (Lebensdauer und Registrierung)**
   - Voraussetzungen: Keine.
   - Beschreibung: In `VideoWebPlayer/Program.cs` und im Test-Host (`VideoWebPlayer.Tests`, Helfer und `TestableMediaSourceScanService`) prüfen, mit welcher Lebensdauer `MediaSourceScanner`, `MediaSourceClassifier`, `MediaUpdateNotificationService`, `IProgramScheduleSignal`, `IPlaylistBackfillSignal` und `IBackgroundProcessingGate` registriert sind. Ergebnis im Handover festhalten. Fehlt eine Registrierung, im selben Schritt ergänzen.

2. **Ergebnisrecords und Koordinator-Klasse anlegen (ohne Aufrufer)**
   - Voraussetzungen: Schritt 1 (Lebensdauer bestätigt).
   - Beschreibung: `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs` mit `RunFullScanAsync` und `RescanCollectionTreeAsync` gemäß Programmabläufen; Records `MediaSourceScanRunResult` und `CollectionRescanResult`; Logmeldungen des Workers übernehmen; Registrierung im DI-Container (scoped). Noch keine Aufrufer umstellen.

3. **Koordinator-Tests (SQLite, lokales Verzeichnis)**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Neue Testklasse `VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorTests.cs` (Tests siehe Abschnitt „Tests“). Bei Umfang ggf. in zwei Klassen teilen (Skill `test-class-structure`).

4. **Worker auf Koordinator umstellen**
   - Voraussetzungen: Schritt 2 (Koordinator vorhanden und registriert), Schritt 3 grün.
   - Beschreibung: `MediaSourceScanService.ExecuteAsync` ruft `RunFullScanAsync("Automatischer Scanprozess", 64)` im `innerScope` auf; Scope-, Gate-, Zählungs- und Klassifizierungscode im Worker entfällt. Kodierung erhalten. Bestehende Tests in `MediaSourceScanServiceTests` müssen grün bleiben.

5. **Regressionstest Worker gegen Koordinator**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Test, der denselben Datenbestand einmal über `TestableMediaSourceScanService` und einmal über `RunFullScanAsync` verarbeitet und Ergebnis (Klassifizierung, Genres, Anzahl Wecke-Ereignisse) vergleicht. Ort: `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`, neue Fakt-Methode.

6. **Admin-Komplettscan umstellen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `MediaSourceAdmin.razor::RunFullScan` ruft `RunFullScanAsync("Manueller Komplettscan", null)`; Pausierungsprüfung bleibt; Statusmeldung mit Ergebnis; ungenutzte Injektionen entfernen.

7. **Explorer-Rescan umstellen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `MediaSourceExplorer.razor::RescanCurrent` ruft `RescanCollectionTreeAsync(currentCollection.Id, "Manueller Collection-Scan")`; Hinweis bei `ClassificationDeferred`; Behandlung nicht mehr vorhandener Collection; ungenutzte Injektionen entfernen.

8. **E2E-Tests für Admin- und Explorer-Pfad**
   - Voraussetzungen: Schritt 6 und 7.
   - Beschreibung: Neue E2E-Testklassen für die Admin- und Explorer-Seite (siehe Abschnitt „E2E-Tests“). Muster aus `LocalMediaPlaylistE2ETests_*` und `PlaylistsE2ETestBase` übernehmen.

9. **Test-Helfer und Sequenzen in Tests umstellen**
   - Voraussetzungen: Schritt 2 und 1 (Registrierung im Test-Host).
   - Beschreibung: `LocalMediaPlaylistE2ETestBase.ScanEverythingAsync` und `RescanCollectionAsync`, `AgeRatingE2ETests` (Zeilen 230–232), `LocalMediaSourcePipelineE2ETests` (Zeilen 109–111) auf den Koordinator umstellen. Assertions unverändert.

10. **Dokumentation prüfen**
    - Voraussetzungen: Schritte 6 und 7 abgeschlossen.
    - Beschreibung: `docs/help/` (Admin-Medienquellen, Komplettscan, Neu erfassen) prüfen und bei Änderung der Meldungen anpassen; Release Notes und README nur bei Bedarf.

11. **Abnahme-Build und Suite**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: `dotnet build VideoWebPlayer.sln` in Debug und Release; `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig grün (einzelne Playwright-Flakes einzeln wiederholen und nennen).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RunFullScanAsync_DefersScheduleWake_UntilScanEnds` | `MediaSourceScanCoordinatorTests` | Ein Commit mit Backfill-Markern während des Laufs weckt den Sendeplan nicht; das Laufende weckt genau einmal (Wake-Zähler über Wrapper wie in `MediaDurationProbeWorkerTests`, Zeilen 292–325). |
| `RunFullScanAsync_NotifiesNewVideos_WithCountedDelta` | `MediaSourceScanCoordinatorTests` | `NotifyNewVideosScannedAsync` erhält die Differenz der `MediaItems`-Zählung (Test-Notifier-Doppel). |
| `RunFullScanAsync_RunsGenreReloadBeforeCollectionScan` | `MediaSourceScanCoordinatorTests` | `Setup.GenresChanged` wird vor der Collection-Schleife zurückgesetzt und `ReloadGenres` ausgeführt. |
| `RunFullScanAsync_RespectsCap_WhenProvided` | `MediaSourceScanCoordinatorTests` | Mit Cap 1 werden nur eine Collection gescannt und `CapReached = true` gemeldet; mit `null` werden alle fälligen Collections gescannt. |
| `RunFullScanAsync_WaitsForRestoreLease_ThenRuns` | `MediaSourceScanCoordinatorTests` | Gate-Lease wird erworben; während Pause wartet der Lauf (Muster `BackgroundProcessingGateTests`). |
| `RescanCollectionTreeAsync_ScansOnlySubtree` | `MediaSourceScanCoordinatorTests` | Geschwister-Collection im selben Quellbaum bleibt unverändert. |
| `RescanCollectionTreeAsync_ReportsDeferredClassification` | `MediaSourceScanCoordinatorTests` | Ist die Klassifizierung belegt, liefert `ClassificationDeferred = true`; nach Freigabe wird der Baum klassifiziert. |
| `RescanCollectionTreeAsync_MissingCollection_ReturnsStatusInsteadOfThrowing` | `MediaSourceScanCoordinatorTests` | Nicht vorhandene Collection-Id wird nach der Validierungsregel behandelt (Schritt 7 legt das Verhalten fest). |
| `Worker_AndManualFullScan_ProduceSameResult` | `MediaSourceScanServiceTests` | Regressionstest aus Schritt 5: gleiche Ausgangsdaten, gleiche Ergebnisse und Wecke-Anzahl über Worker und Koordinator. |
| `ScanEverythingAsync` (geändert) | `LocalMediaPlaylistE2ETestBase` | Ruft `RunFullScanAsync` auf (Hilfsmethode, kein neuer Test). |
| `RescanCollectionAsync` (geändert) | `LocalMediaPlaylistE2ETestBase` | Ruft `RescanCollectionTreeAsync` auf (Hilfsmethode). |

Hinweis zur Testklassen-Größe: Falls die Klasse `MediaSourceScanCoordinatorTests` zu groß wird, in `MediaSourceScanCoordinatorScopeTests` (Wecken/Gate) und `MediaSourceScanCoordinatorScanTests` (Scan/Klassifizierung) teilen.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MediaSourceScanServiceTests` (fünf Fakten) | Worker-Sequenz ist nun im Koordinator; Logmeldungen bleiben erhalten, Verhalten muss gleich sein. Keine Assertion-Änderung erwartet. |
| `LocalMediaPlaylistE2ETests_Backfill` (`NewEpisodeInLocalDirectory_AfterRescan_IsBackfilledIntoThePlaylist`, `RescanWithoutNewFiles_LeavesThePlaylistUnchanged`) | Nutzt `RescanCollectionAsync`; Verhalten ändert sich durch Benachrichtigung und Genre-Reload im Helfer. Assertions unverändert, Lauf prüfen. |
| `LocalMediaPlaylistE2ETests_Playback`, `_Cover`, `_SourceDeletion` | Nutzen `ScanEverythingAsync`; zusätzlicher Genre-Reload und Benachrichtigung. |
| `AgeRatingE2ETests` (`MovieNfoRating_ShowsBadgeOnMovieDetails`, `TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt`, `Admin_SetsAgeRating_ViaEditDropdown`) | Eigene Sequenz wird durch Koordinator ersetzt. |
| `LocalMediaSourcePipelineE2ETests` (`LocalSource_ScanAndClassify_ExposesTvShowViaApi`) | Eigene Sequenz wird durch Koordinator ersetzt. |
| `Services/MediaSourceScannerLocalTests`, `Services/MediaSourceScannerTests` | Keine Änderung erwartet (Scanner unverändert); nur Lauf zur Kontrolle. |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung betrifft zwei Benutzerabläufe (Admin „Komplettscan aller Quellen“, Explorer „Neu erfassen“). Laut Bestandsaufnahme ruft kein Test `RunFullScan` oder `RescanCurrent` direkt auf; die Abdeckung bisher erfolgt über die Helfer.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin startet Komplettscan, neue Serien-Datei erscheint danach in Liste und Episoden-Ansicht | `VideoWebPlayer.Tests/MediaSourceAdminFullScanE2ETests.cs` (neu; Muster `LocalMediaPlaylistE2ETests_*`) | Akzeptanzszenario 1 | Der Klick auf die Schaltfläche in `MediaSourceAdmin.razor` ist bisher ungetestet. |
| Pflicht | Admin-Klick bei aktivem Restore zeigt Statusmeldung und startet keinen Lauf | gleiche Klasse | Akzeptanzszenario 3 | Sichtbare Blockade muss dem Anwender erklärt werden. |
| Pflicht | Explorer „Neu erfassen“ in einer Unter-Collection findet neue Datei; Geschwister unverändert | `VideoWebPlayer.Tests/MediaSourceExplorerRescanE2ETests.cs` (neu) | Akzeptanzszenario 2 | Ersetzt die Abdeckung über den Helfer `RescanCollectionAsync` durch den realen Klick. |
| Pflicht | „Neu erfassen“ bei laufender Klassifizierung zeigt Hinweis zur Hintergrundklassifizierung | gleiche Klasse | Akzeptanzszenario 4 | Sichtbare Meldung, die sonst fehlt. |
| Pflicht | Bestehende Seiten-Tests für Medienquellen laufen weiter grün | bestehende Testklassen | Akzeptanzszenario 5 | Regressionsnachweis. |
| Optional | Nicht vorhandene Collection beim Klick | `MediaSourceExplorerRescanE2ETests` | Akzeptanzszenario 7 | Nur, wenn die UI den Fall leicht erzeugen kann; sonst Unit-Test (siehe Tests). |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AgeRatingE2ETests`, `LocalMediaSourcePipelineE2ETests` | Umstellung der Sequenz auf Koordinator (Schritt 9). |
| `LocalMediaPlaylistE2ETests_*` | Umstellung über Helfer; Lauf prüfen. |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Admin-Komplettscan (Happy Path) | Nutzer klickt „Komplettscan aller Quellen“ in `MediaSourceAdmin.razor` | Quelle mit lokalem Verzeichnis, neue Serien-Datei vorhanden; Root-Collection und Sendeplan-Objekte sind noch nicht angelegt bzw. werden erzeugt (fehlende Objekte: Root-Collection wird vom Scan erzeugt, Sendeplan wird am Laufende geweckt, kein Sendeplan-Objekt wird vorausgesetzt). | Backfill-Scope, Schedule-Scope, Gate, Scan, Benachrichtigung, Genre-Reload, Schleife, Klassifizierung, Scope-Freigabe | Status „Komplettscan abgeschlossen“ mit Anzahl neuer Videos; Serie und Episode erscheinen in der Liste; Sendeplan wird genau einmal am Laufende aktualisiert. | E2E `MediaSourceAdminFullScanE2ETests` plus Test `RunFullScanAsync_DefersScheduleWake_UntilScanEnds` |
| 2 | Explorer „Neu erfassen“ (Happy Path) | Nutzer klickt „Neu erfassen“ in einer Unter-Collection | Neue Datei im Teilbaum vorhanden; Geschwister-Collection existiert | Backfill- und Schedule-Scope, Gate, Teilbaum-Scan, Benachrichtigung, Genre-Reload, Teilbaum-Klassifizierung | Neue Datei erscheint im Teilbaum; Geschwister unverändert; Statusmeldung mit Anzahl. | E2E `MediaSourceExplorerRescanE2ETests` plus `RescanCollectionTreeAsync_ScansOnlySubtree` |
| 3 | Restore aktiv | Klick auf „Komplettscan“ oder „Neu erfassen“ | `IsPausedForRestore = true` | UI prüft vorab, zeigt Meldung, startet keinen Koordinator-Aufruf | Statusmeldung „Restore läuft, Scan nicht möglich“ (bestehender Text); kein Scan, keine Lease. | E2E-Test Admin (zweites Pflicht-Szenario) |
| 4 | Klassifizierung bereits aktiv | Klick „Neu erfassen“, während der Worker klassifiziert | `_classificationRunning` ist gesetzt | Scan läuft; Klassifizierung wird eingereiht; `ClassificationDeferred = true` | Meldung „Klassifizierung läuft im Hintergrund; das Ergebnis erscheint nach Abschluss“; die Warteschlange klassifiziert den Baum nach Abschluss des laufenden Laufs (`FinishClassificationAsync`). Wiederaufnahme: automatisch durch den laufenden Klassifizierungslauf, kein Nutzerschritt nötig. | Test `RescanCollectionTreeAsync_ReportsDeferredClassification` und E2E-Test |
| 5 | Worker-Zyklus (unverändert) | Intervall abgelaufen | Scan-Intervall (Default 1 h) abgelaufen | Koordinator wie Komplettscan mit Cap 64 | Logmeldungen wie bisher; neue Items erscheinen ohne Nutzeraktion. | Bestehende `MediaSourceScanServiceTests` und Regressionstest |
| 6 | Lokales Verzeichnis fehlt | Komplettscan bei Quelle, deren Verzeichnis entfernt wurde | Verzeichnis existiert nicht (bestehender Fall) | Scan überspringt die Collection (wie bisher) | Lauf endet ohne Absturz; Status „abgeschlossen“ mit 0 neuen Videos. | Bestehender Test `ScanNextMediaCollection_SkipsCollection_WhenLocalDirectoryMissing`; Koordinator-Lauf prüft Verhalten |
| 7 | Collection gelöscht zwischen Laden und Klick | Klick „Neu erfassen“ auf zwischenzeitlich gelöschte Collection | Collection-Id nicht mehr in der Datenbank | Koordinator prüft Existenz (Validierungsregel), liefert Fehlerstatus | Statusmeldung „Collection nicht mehr vorhanden“, keine Exception in der Oberfläche. | Test `RescanCollectionTreeAsync_MissingCollection_ReturnsStatusInsteadOfThrowing` |

## Getroffene Entscheidungen

Die offenen Fragen aus der Planung sind durch den Anwender beantwortet. Es gibt keine offenen Punkte mehr.

| # | Frage | Entscheidung | Auswirkung im Plan |
|---|-------|--------------|--------------------|
| 1 | Drain-Cap (64) für manuelle Läufe? | Kein Cap (`null`) beim manuellen Komplettscan. Der Cap bleibt Parameter für den Worker. | Designentscheidung „Cap“; Scope-Ausschluss 7; Admin-Ablauf Schritt 2. |
| 2 | Genre-Reload-Reihenfolge im Admin (nach der Klassifizierung)? | Fehler. Worker-Reihenfolge (Genres vor Klassifizierung) wird übernommen. | Programmablauf Komplettscan Schritt 9; Seiteneffekte. |
| 3 | Benachrichtigung bei manuellen Läufen? | Ja, `NotifyNewVideosScannedAsync` wird auch bei manuellen Läufen ausgelöst. | Programmablauf Schritte 7/8; Designentscheidung „Benachrichtigung in UI“. |
| 4 | `RescanCurrent`: Genre-Reload und Benachrichtigung? | Ja, beides wird ausgeführt. | Programmablauf Collection-Rescan Schritte 7/8; Designentscheidung „Genre-Reload im Explorer“. |
| 5 | Kurzfristige Variante (`BeginScan` in den UI-Pfaden) vorab? | Nein, kein Vorab-Hotfix; direkt der Koordinator. | Designentscheidung „Kurzfristige Variante“. |
| 6 | Namensgebung `MediaSourceScanCoordinator.RunFullScanAsync` / `RescanCollectionTreeAsync`? | Beibehalten. | Designentscheidungen, Neue Klassen. |
| 7 | Wecken des Sendeplans bei Marker-Commits außerhalb eines Laufs? | Nicht in diesem Auftrag. Eigenes GitHub-Issue https://github.com/martin-stromberg/VideoPlayer/issues/337 („Sendeplan: Jeder Commit mit sendeplan-relevanten Entitaeten weckt den Worker sofort“). | Scope-Ausschluss 6; Seiteneffekte „Sendeplan-Wecken“. |
| 8 | Fortschrittsanzeige im Admin-Komplettscan? | Entfällt; die Ergebnismeldung reicht. | Admin-Ablauf Schritt 3; Seiteneffekte „Fortschritt“. |

## Dokumentation und Abschluss (Hinweis)

- Hilfe-Dokumentation unter `docs/help/` prüfen (Schritt 10). README und Release Notes nur bei Bedarf.
- Der `todo.md`-Status für Schritt 5 (Umsetzungsplanung) wird vom Lifecycle-Ablauf gepflegt und in diesem Schritt nicht verändert.
