# Tasks: Medienquellen — Merkmal „Im Menü ausblenden“ (Issue #284)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `MediaSource.HiddenInMenu` (`bool`, Default `false`) in `VideoWebPlayer/Data/MediaSource.cs` anlegen — Windows-1252-Encoding beibehalten | Offen | — |
| 2 | Datenmodell | `DtoMediaSource.HiddenInMenu` (`bool`) in `VideoWebPlayer.Client/Models/DtoSource.cs` anlegen | Offen | — |
| 3 | Datenmodell | EF-Migration `AddMediaSourceHiddenInMenu` erzeugen (`AddColumn<bool>` auf `MediaSources`, `nullable: false`, `defaultValue: false`) inkl. aktualisiertem `ApplicationDbContextModelSnapshot.cs` | Offen | — |
| 4 | Logik | `MediaSourceExtensions.Update` um `HiddenInMenu`-Kopie erweitern — Windows-1252-Encoding beibehalten | Offen | — |
| 5 | UI | `NavMenu.razor`: `mediaSources` in `OnInitializedAsync` auf `!HiddenInMenu` filtern, sodass Überschrift (`mediaSources.Any()`) und Liste auf der gefilterten Menge arbeiten | Offen | — |
| 6 | UI | `MediaSourceAdminDetails.razor`: Checkbox „Im Menü ausblenden“ (`InputCheckbox`, `form-check`-Stil mit `form-text`-Hilfetext) im `EditForm` ergänzen | Offen | — |
| 7 | UI | `MediaSourceAdminDetails.razor`: `HiddenInMenu` in der manuellen `editSource`-Kopie in `OnInitializedAsync` (Zeilen 209–223) übernehmen | Offen | — |
| 8 | Backup/Restore | `VideoWebPlayerBackupData.OptionalRestoreColumns` um `MediaSources.HiddenInMenu` erweitern | Offen | — |
| 9 | Backup/Restore | `VideoWebPlayerBackupData.OptionalRestoreBoolDefaults` um `(MediaSources, HiddenInMenu, false)` erweitern | Offen | — |
| 10 | Tests | `NoOpVideoWebPlayerClient` um optionalen `DtoMediaSource[]`-Parameter für `HttpGetAsync<DtoMediaSource[]>` erweitern | Offen | — |
| 11 | Tests | `ApplicationDbContextTests.Update_CopiesHiddenInMenu` (echtes SQLite): `Update`/`UpdateMediaSourceAsync` überträgt `HiddenInMenu` | Offen | — |
| 12 | Tests | `SourceVisibilityControllerTests`: `GetSources`/`GetSource` liefern `HiddenInMenu` im `DtoMediaSource` aus; versteckte Quelle bleibt in der Liste | Offen | — |
| 13 | Tests | Neue bUnit-Klasse `NavMenuHiddenSourceTests`: versteckte Quelle ohne Menüeintrag, sichtbare bleibt, Sektion „Quellen“ verschwindet wenn alle versteckt, Admin gleiche Filterung | Offen | — |
| 14 | Tests | Neue Klasse `VideoWebPlayerBackupDataTests_HiddenInMenu`: Alt-Backup ohne Spalte (via `LegacyBackupArchiveBuilder.RemoveColumnsAsync`) restored mit Default `false`; Seed mit `HiddenInMenu = true` | Offen | — |
| 15 | E2E-Tests | Neue Klasse `MediaSourceHiddenInMenuE2ETests`: Admin setzt Checkbox → persistiert → Menüeintrag verschwindet für Admin und Nicht-Admin; Direktaufruf `/mediasource/{id}` bleibt erreichbar | Offen | — |
| 16 | Dokumentation | `docs/API.md`: `DtoMediaSource`-Beispiel unter `GET /api/Sources` um `hiddenInMenu` erweitern | Offen | — |
| 17 | Dokumentation | `docs/help/medienquellen/datenmodell.md`: Feld, Migration, Restore-Eintrag und DTO-Grenze dokumentieren | Offen | — |
| 18 | Dokumentation | `docs/help/medienquellen/einrichtung-anwender.md` bzw. `beschreibung.md`: Admin-Anleitung zum Merkmal ergänzen | Offen | — |
| 19 | Dokumentation | `docs/RELEASE_NOTES.md` aktualisieren; `README.md` bei Bedarf | Offen | — |
