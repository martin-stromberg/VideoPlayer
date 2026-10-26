# Tasks: Medienquellen — Merkmal „Im Menü ausblenden“ (Issue #284)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `MediaSource.HiddenInMenu` (`bool`, Default `false`) in `VideoWebPlayer/Data/MediaSource.cs` anlegen — Windows-1252-Encoding beibehalten | Fertig | `ApplicationDbContextTests.Update_CopiesHiddenInMenu` (echtes SQLite) + `MediaSourceHiddenInMenuE2ETests` |
| 2 | Datenmodell | `DtoMediaSource.HiddenInMenu` (`bool`) in `VideoWebPlayer.Client/Models/DtoSource.cs` anlegen | Fertig | `SourceVisibilityControllerTests.GetSources_And_GetSource_Return_HiddenInMenu_Flag_And_Keep_Hidden_Source_In_List` |
| 3 | Datenmodell | EF-Migration `AddMediaSourceHiddenInMenu` erzeugen (`AddColumn<bool>` auf `MediaSources`, `nullable: false`, `defaultValue: false`) inkl. aktualisiertem `ApplicationDbContextModelSnapshot.cs` | Fertig | `dotnet ef migrations has-pending-model-changes` ohne Befund; indirekt über alle `EnsureCreatedAsync`-Tests |
| 4 | Logik | `MediaSourceExtensions.Update` um `HiddenInMenu`-Kopie erweitern — Windows-1252-Encoding beibehalten | Fertig | `ApplicationDbContextTests.Update_CopiesHiddenInMenu` |
| 5 | UI | `NavMenu.razor`: `mediaSources` in `OnInitializedAsync` auf `!HiddenInMenu` filtern, sodass Überschrift (`mediaSources.Any()`) und Liste auf der gefilterten Menge arbeiten | Fertig | `NavMenuHiddenSourceTests` (4 bUnit-Tests) + `MediaSourceHiddenInMenuE2ETests` |
| 6 | UI | `MediaSourceAdminDetails.razor`: Checkbox „Im Menü ausblenden“ (`InputCheckbox`, `form-check`-Stil mit `form-text`-Hilfetext) im `EditForm` ergänzen | Fertig | `MediaSourceHiddenInMenuE2ETests.Admin_HidesSource_ViaCheckbox_FlagPersists_AndMenuEntryDisappears` |
| 7 | UI | `MediaSourceAdminDetails.razor`: `HiddenInMenu` in der manuellen `editSource`-Kopie in `OnInitializedAsync` (Zeilen 209–223) übernehmen | Fertig | Derselbe E2E-Test prüft den gesetzten Zustand nach Reload (`ToBeCheckedAsync`) |
| 8 | Backup/Restore | `VideoWebPlayerBackupData.OptionalRestoreColumns` um `MediaSources.HiddenInMenu` erweitern | Fertig | `VideoWebPlayerBackupDataTests_HiddenInMenu.ReadFromAsync_LegacyBackupWithoutHiddenInMenuColumn_RestoresDefaultFalse` |
| 9 | Backup/Restore | `VideoWebPlayerBackupData.OptionalRestoreBoolDefaults` um `(MediaSources, HiddenInMenu, false)` erweitern | Fertig | Derselbe Test (Seed bewusst mit `true`, Restore liefert `false`) |
| 10 | Tests | `NoOpVideoWebPlayerClient` um optionalen `DtoMediaSource[]`-Parameter für `HttpGetAsync<DtoMediaSource[]>` erweitern | Fertig | `NavMenuHiddenSourceTests` nutzt den Parameter; zudem die 1-Argument-Überladung `HttpGetAsync<T>(string)` überschrieben (sonst echter HTTP-Versuch) |
| 11 | Tests | `ApplicationDbContextTests.Update_CopiesHiddenInMenu` (echtes SQLite): `Update`/`UpdateMediaSourceAsync` überträgt `HiddenInMenu` | Fertig | Grün im gefilterten Lauf |
| 12 | Tests | `SourceVisibilityControllerTests`: `GetSources`/`GetSource` liefern `HiddenInMenu` im `DtoMediaSource` aus; versteckte Quelle bleibt in der Liste | Fertig | Grün im gefilterten Lauf |
| 13 | Tests | Neue bUnit-Klasse `NavMenuHiddenSourceTests`: versteckte Quelle ohne Menüeintrag, sichtbare bleibt, Sektion „Quellen“ verschwindet wenn alle versteckt, Admin gleiche Filterung | Fertig | 4/4 grün |
| 14 | Tests | Neue Klasse `VideoWebPlayerBackupDataTests_HiddenInMenu`: Alt-Backup ohne Spalte (via `LegacyBackupArchiveBuilder.RemoveColumnsAsync`) restored mit Default `false`; Seed mit `HiddenInMenu = true` | Fertig | Grün im gefilterten Lauf |
| 15 | E2E-Tests | Neue Klasse `MediaSourceHiddenInMenuE2ETests`: Admin setzt Checkbox → persistiert → Menüeintrag verschwindet für Admin und Nicht-Admin; Direktaufruf `/mediasource/{id}` bleibt erreichbar | Fertig | 4/4 grün (Playwright, 26 s) |
| 16 | Dokumentation | `docs/API.md`: `DtoMediaSource`-Beispiel unter `GET /api/Sources` um `hiddenInMenu` erweitern | Fertig | — |
| 17 | Dokumentation | `docs/help/medienquellen/datenmodell.md`: Feld, Migration, Restore-Eintrag und DTO-Grenze dokumentieren | Fertig | — |
| 18 | Dokumentation | `docs/help/medienquellen/einrichtung-anwender.md` bzw. `beschreibung.md`: Admin-Anleitung zum Merkmal ergänzen | Fertig | — |
| 19 | Dokumentation | `docs/RELEASE_NOTES.md` aktualisieren; `README.md` bei Bedarf | Fertig | `README.md` enthält keine Feld-Doku pro Merkmal — keine Anpassung nötig |
