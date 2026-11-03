# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Geprüft wurde gegen `plan.md` (Stand 06:49). Die Implementierung liegt im Arbeitsverzeichnis (uncommitted). Der Plan verwendet die Pfade `VideoWebPlayer/` und `VideoWebPlayer.Tests/`; die Anforderung nennt `src/`. Die Prüfung umfasst nur das Vorhandensein und den Umfang der Planelemente im Code, keine Tests und keine Qualitätsbewertung. Schritt 11 des Plans (Abnahme-Build und Suite) ist nicht Gegenstand dieses Reviews und bleibt in der Tasks-Datei offen.

## Umgesetzte Planelemente

- [x] `MediaSourceScanCoordinator` (Klasse, Service Layer, scoped) — angelegt in `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs`, registriert in `ServiceCollectionExtensions.cs`
- [x] Konstruktorabhängigkeiten (`MediaSourceScanner`, `MediaSourceClassifier`, `IPlaylistBackfillSignal`, `IProgramScheduleSignal`, `MediaUpdateNotificationService`, `ApplicationDbContext`, `ILogger`, optional `IBackgroundProcessingGate`) — vorhanden
- [x] Methode `RunFullScanAsync(string, int?, CancellationToken)` (public) — vorhanden, Reihenfolge Scopes, Gate, Zählung, Scan, Benachrichtigung, Genre-Reload, Schleife mit Cap, bedingte Klassifizierung und Benachrichtigung entspricht dem Plan
- [x] Methode `RescanCollectionTreeAsync(long, string, CancellationToken)` (public) — vorhanden, Teilbaum-Scan, Benachrichtigung, Genre-Reload, Klassifizierung mit `ClassificationDeferred = !classified`
- [x] Validierung `maxCollectionsPerRun > 0` (`ArgumentOutOfRangeException`) — vorhanden
- [x] Freigabereihenfolge (Lease, dann Schedule-Scope, dann Backfill-Scope) — durch `using`/`await using`-Deklarationsreihenfolge umgesetzt
- [x] Record `MediaSourceScanRunResult(NewItemsCount, CollectionsScanned, CapReached)` — vorhanden (gleiche Datei)
- [x] Record `CollectionRescanResult(NewItemsCount, ClassificationDeferred)` — vorhanden (gleiche Datei)
- [x] `MediaSourceScanService.ExecuteAsync` — Scope-, Gate-, Zählungs- und Klassifizierungsblock entfernt, Aufruf `RunFullScanAsync("Automatischer Scanprozess", 64)` im `innerScope`, Logmeldungen „Starte Scanprozess…“ bleiben erhalten
- [x] `MediaSourceAdmin.razor::RunFullScan` — ruft `RunFullScanAsync("Manueller Komplettscan", null)`, Pausierungsprüfung vor dem Aufruf, Ergebnismeldung, `Task.Yield`-Schleife entfernt, Injektionen von Scanner/Classifier/Backfill-Signal entfernt
- [x] `MediaSourceExplorer.razor::RescanCurrent` — ruft `RescanCollectionTreeAsync(collectionId, "Manueller Collection-Scan")`, Hinweis bei `ClassificationDeferred`, Meldung „Collection nicht mehr vorhanden.“ bei nicht mehr ladbarer Collection
- [x] `LocalMediaPlaylistE2ETestBase.ScanEverythingAsync` und `RescanCollectionAsync` — rufen den Koordinator auf
- [x] `AgeRatingE2ETests` (Sequenz) und `LocalMediaSourcePipelineE2ETests` (Sequenz) — auf den Koordinator umgestellt
- [x] `TestableMediaSourceScanService` — Benachrichtigungsdienst aus dem DI-Container, `CreateNullNotificationService` öffentlich
- [x] Test `RunFullScanAsync_DefersScheduleWake_UntilScanEnds` — in `MediaSourceScanCoordinatorScopeTests`
- [x] Test `RunFullScanAsync_WaitsForRestoreLease_ThenRuns` — in `MediaSourceScanCoordinatorScopeTests`
- [x] Test `RunFullScanAsync_NotifiesNewVideos_WithCountedDelta` — in `MediaSourceScanCoordinatorScanTests`
- [x] Test `RunFullScanAsync_RunsGenreReloadBeforeCollectionScan` — in `MediaSourceScanCoordinatorScanTests`
- [x] Test `RunFullScanAsync_RespectsCap_WhenProvided` — in `MediaSourceScanCoordinatorScanTests`
- [x] Test `RescanCollectionTreeAsync_ScansOnlySubtree` — in `MediaSourceScanCoordinatorScanTests`
- [x] Test `RescanCollectionTreeAsync_ReportsDeferredClassification` — in `MediaSourceScanCoordinatorScanTests`
- [x] Test `RescanCollectionTreeAsync_MissingCollection_ReturnsStatusInsteadOfThrowing` — in `MediaSourceScanCoordinatorScanTests`
- [x] Test `Worker_AndManualFullScan_ProduceSameResult` — in `MediaSourceScanServiceTests`
- [x] E2E `Admin_RunsFullScan_AndNewEpisodeAppearsInExplorer` und `Admin_FullScanBlocked_WhileRestoreIsActive` — in `MediaSourceAdminFullScanE2ETests`
- [x] E2E `RescanCurrent_FindsNewFile_InSubtreeOnly_SiblingUnchanged` und `RescanCurrent_WhileClassificationRunning_ShowsDeferredHint` — in `MediaSourceExplorerRescanE2ETests`
- [x] Keine Änderung an `ApplicationDbContext`, Datenmodell, Migrationen und Konfiguration (Scope-Ausschlüsse 5 und 6 eingehalten; `git status` zeigt keine Änderung daran)
- [x] Bestehende Assertions in `MediaSourceScanServiceTests` unverändert (Diff enthält nur Ergänzungen)

## Offene Aufgaben

Keine. Alle Planelemente sind im Code vorhanden.

## Hinweise

Beobachtungen, die keine offene Planlücke darstellen, aber bei der Abnahme oder Nachbesserung relevant sein können:

- **Testklassen geteilt:** Der Plan nennt in den Tabellen die Klasse `MediaSourceScanCoordinatorTests` mit Datei `Services/MediaSourceScanCoordinatorTests.cs`. Umgesetzt sind `MediaSourceScanCoordinatorScopeTests` und `MediaSourceScanCoordinatorScanTests`. Der Plan erlaubt diese Aufteilung ausdrücklich als Fallback.
- **Reihenfolge des Genre-Reloads nicht direkt abgesichert:** `RunFullScanAsync_RunsGenreReloadBeforeCollectionScan` prüft den Endzustand (`GenresChanged == false`, Genres vorhanden). Dass `CheckReloadGenres` vor der Collection-Schleife läuft, wird durch diesen Test nicht bewiesen.
- **Cap-Konstante:** Der Plan sieht „Cap 64 bleibt eine Konstante im Worker“ vor. Der Worker übergibt jetzt das Literal `64`, ohne Konstante.
- **Ungenutztes Feld im Worker:** `MediaSourceScanService._notificationService` (Zeilen 18 und 47) wird nach dem Entfernen des Blocks nicht mehr gelesen. Der Konstruktorparameter ist nicht im Plan geregelt.
- **Restore-Meldung weicht vom Plan-Zitat ab:** Akzeptanzszenario 3 nennt „Restore läuft, Scan nicht möglich“. Der bestehende Text lautet im Admin „Restore läuft. Der Komplettscan ist derzeit gesperrt.“ und im Explorer „Restore läuft. Neu erfassen ist derzeit gesperrt.“. Der Code ist unverändert; das Plan-Zitat ist ungenau.
- **Optionales E2E nicht umgesetzt:** Akzeptanzszenario 7 (nicht vorhandene Collection beim Klick) ist im Plan als optional markiert. Abgedeckt durch den Unit-Test `RescanCollectionTreeAsync_MissingCollection_ReturnsStatusInsteadOfThrowing`.
- **Nicht im Plan genannte Testdatei:** `VideoWebPlayer.Tests/Helpers/GatedMetadataWriteCoordinator.cs` ist neu, im Plan nicht erwähnt. Sie wird von `MediaSourceScanCoordinatorScopeTests` und `MediaSourceExplorerRescanE2ETests` genutzt.
- **Keine Doppelladen-Gefahr in der UI:** Kein Razor-Component abonniert `NewVideosScanned`; die neuen Benachrichtigungen aus manuellen Läufen führen daher nicht zu doppeltem Laden (Seiteneffekt aus dem Plan).
- **Hilfe-Dokumentation** (`docs/help/medienquellen/fehlerbehebung-anwender.md`) und `docs/RELEASE_NOTES.md` sind bereits geändert.
- **Tasks-Datei angelegt:** `docs/features/task-issue-333-86d0f0f6e5984dfdb74ebf2ede9e11ff-scan-pipeline-orchestrierung-l-tasks.md` existierte nicht und wurde mit den Plan-Aufgaben neu angelegt. Alle Aufgaben außer Schritt 14 (Abnahme-Build und Suite) sind als `Erledigt` markiert, weil ihre Umsetzung im Code nachgewiesen ist.
