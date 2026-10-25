# Bestandsaufnahme: Altersfreigaben (FSK & MPAA) aus NFO-Dateien bzw. TMDB/IMDb erfassen

Analysiert wurde der Bestand des Projekts (Datenmodell, Scan-/Klassifizierungslogik, DTO/API, Detail-UI, Backup-Restauration, Tests) bezogen auf `requirement.md` — Erfassung, Normalisierung und Anzeige von Altersfreigaben aus `<mpaa>`/`<certification>`-NFO-Elementen mit optionaler Online-Ergänzung.

## Zusammenfassung

- **Datenmodell:** Es existiert keinerlei Altersfreigabe-Feld. `Movie` (`VideoWebPlayer/Data/Movie.cs`) und `TVShow` (`VideoWebPlayer/Data/TVShow.cs`) besitzen je eine `LoadFromXml(XElement)`-Methode als Erweiterungspunkt; gemeinsame Basisklasse `MediaBaseEntry` trägt u. a. `IsManuallyEdited` und `Changed`. `TVShowEpisode.LoadFromXml` existiert, wird aber vom `MediaSourceClassifier` **nicht** aufgerufen — Episodenfelder werden inline in `ProcessEpisodesForTVShowAsync` gesetzt.
- **NFO-Auswertung:** `MediaSourceClassifier` liest `tvshow.nfo` (Collection-Ebene) und `<videodatei>.nfo` (Filme/Episoden). Ein `movie.nfo`-Fallback existiert nur im Actor-Backfill (`BackfillMissingActorsAsync`/`TryReadActorNfoAsync`), **nicht** im regulären Film-Scan. `<mpaa>`/`<certification>` wird nirgends ausgewertet.
- **`IsManuallyEdited`-Schutz:** greift eintragsweit in allen Scan-Pfaden (Serie, Staffel, Episode, Film, Filmsammlung) sowie im Backfill — deckt „kein Überschreiben manuell gepflegter Werte" auf Eintragsebene ab.
- **Normalisierung / Online-Quelle:** nicht vorhanden. Kein `AgeRatingNormalizer`, keine TMDB-/IMDb-Anbindung, keine API-Key-Optionsklasse. Einziger ausgehender HTTP-Zugriff im Classifier lädt Schauspieler-Thumbs (`DownloadActorPictureAsync`, fehlertolerant via `LogDebug`). Optionsmuster (`MediaProbe`, `Playlists`, `EpisodeBackgroundImage` mit `ValidateOnStart`) und `appsettings.json`-Abschnitte sind etabliert; Secrets-Regeln in `docs/SECRETS_MANAGEMENT.md`.
- **Persistenz/Backup:** Neue Spalten müssen in `VideoWebPlayerBackupData.OptionalRestoreColumns` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs:51`) eingetragen werden; EF-Migrationen unter `VideoWebPlayer/Migrations/`, angewendet via `app.MigrateDatabase()` beim Start. Alt-Backup-Testmuster mit `LegacyBackupArchiveBuilder` existiert.
- **DTO/API/UI:** Detail-DTOs (`DtoMovie`, `DtoTVShow`, …) werden per Reflection aus Entities befüllt (`ApiBaseController.Create<T>`) — ein gleichnamiges Property wird automatisch ausgeliefert. Meta-Zeilen in `MovieCollectionDetails.razor` (Zeilen 48–51, 105–108) und `TVShowDetails.razor` (Zeilen 96–99, 105–108, 114–117) sind die Anzeigestelle. Editierpfad: `MediaMetadataUpdateRequest` + `MetadataEditModel` + `MediaMetadataEditorService.UpdateAsync` (Admin-only, setzt `IsManuallyEdited`) — ohne Rating-Feld.
- **Priorität NFO > Online:** es gibt keinen Mechanismus, der NFO- und Online-Werte kombiniert; die Online-Ergänzung muss komplett neu gebaut werden (inkl. „bleibt leer statt den Scan zu behindern").
- **Backfill-Vorbild:** `ActorBackfillWorker` + `BackfillMissingActorsAsync` mit Markierungsspalte `ActorsClassifiedAt` ist das etablierte Muster für einmalige Nacherfassung des Altbestands (Offene Frage 5).

**Test-Ausgangszustand:** Volle Testsuite am 2026-10-25 (Commit `6b6798f`, Branch `task/issue-269-…`) grün — 2346 von 2348 Tests bestanden, **0 Fehlschläge**, 2 bewusst übersprungene Tests (Unix-only-Test; ein dokumentierter Bestandsfehler `api/continue-watching` HTTP 500 bei Film ohne Filmsammlung — nicht anforderungsrelevant). Details und Nachweise (TRX + Konsolenlogs): [inventory/tests.md](inventory/tests.md). Es existieren **keine** Tests für `<mpaa>`/`<certification>`-Auswertung, Rating-Normalisierung oder eine Online-Quelle — sämtliche anforderungsrelevanten Tests sind neu zu erstellen.

## Details

- [Datenmodell](inventory/models.md) — Entities, `ApplicationDbContext`, `Setup`, EF-Migrationen, `OptionalRestoreColumns`, Optionsklassen
- [Logik](inventory/logic.md) — `MediaSourceClassifier` (Scan-Pfade, `IsManuallyEdited`-Stellen, Backfill), `MediaSourceScanService`, `ActorBackfillWorker`, `MediaMetadataEditorService`, `ItemsController`, `VideoWebPlayerClient`, `VideoWebPlayerBackupData`, `DataUpgradeManager`
- [Enums](inventory/enums.md) — `MediaType`/`MediaTypeValues`, `MediaSourceType`, Vertrags-Enums (kein Rating-Enum vorhanden)
- [Interfaces](inventory/interfaces.md) — `IMediaSourceReader`, `IMediaMetadataWriteCoordinator`, `IUnlockedMediaService`, `IBackgroundProcessingGate`, `IBackupData`-Familie
- [UI, DTOs und API-Modelle](inventory/ui.md) — Detail-DTOs, `MediaMetadataUpdateRequest`, Meta-Zeilen der Detailseiten, `MetadataEditModel`, `MediaBox`/`MediaBaseEntryList`
- [Tests](inventory/tests.md) — Test-Ausgangszustand (Nachweise), anforderungsrelevante Testklassen und Hilfsmethoden
