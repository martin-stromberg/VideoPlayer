# Tasks: Trailer als Vorlauf-Füllmaterial (N11)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `MovieMediaItem.IsTrailer` hinzufügen | Offen | — |
| 2 | Datenmodell | Migration `AddMovieMediaItemIsTrailer` erzeugen | Offen | — |
| 3 | Datenmodell | `OptionalRestoreColumns`/`OptionalRestoreBoolDefaults` in `VideoWebPlayerBackupData` ergänzen | Offen | `ReadFromAsync_LegacyBackupWithoutIsTrailerColumn_RestoresSuccessfully` |
| 4 | Enums | `ProgramRuleMaterialType.Trailer=5`, `ProgramCursorContentType.Trailer=3` | Offen | `GetAllowedMaterialTypes_Filler_IncludesTrailer` |
| 5 | Konfiguration | `ProgramScheduleOptions` + DI-Bindung + `appsettings.json` | Offen | Schedule-Tests |
| 6 | Classifier | `-trailer`-Suffix + `trailers/`-Ordner erkennen, Links setzen, keine Movie-Erzeugung | Offen | `ClassifyAllAsync_Trailer*` |
| 7 | Classifier/Controller | `!IsTrailer`-Filter in `MovieMediaItemsAsync`, `FindMediaItemAsync`, `BackfillMissingActorsAsync`, `AssignPicturesToMovieAsync` | Offen | Schedule-/Classifier-Tests |
| 8 | Schedule | `ExpandTrailersAsync` + `airingsByMovie` + verzögerte Expansion (`EnsureCoverage`/`Reconcile`/`Diagnose`) | Offen | `EnsureCoverage_Trailer*` |
| 9 | Schedule | Vorlauf-Prüfung in `FillGaps` (`(fillAt, fillAt+Lead]`, Commit-Offset) | Offen | `EnsureCoverage_TrailerFiller_AirsBeforeScheduledMovie`, `…FallsBackToNormalFiller` |
| 10 | Regel-Service | `GetAllowedMaterialTypes(Filler)`+`exists`-Switch `Trailer` | Offen | `ProgramRuleServiceTests_Validation`-Neufälle |
| 11 | UI | `TVSenderAdmin.razor`: Trailer-Option + `MaterialLabel`-Sonderfall | Offen | bUnit/E2E |
| 12 | Tests | Neue Testklassen/-fälle (Classifier, Schedule, Validation, Backup, bUnit/E2E) | Offen | — |
| 13 | Doku | `docs/help/tv-sender.md`, `README.md`, `RELEASE_NOTES.md` (EN+DE), `changes.log` | Offen | — |
