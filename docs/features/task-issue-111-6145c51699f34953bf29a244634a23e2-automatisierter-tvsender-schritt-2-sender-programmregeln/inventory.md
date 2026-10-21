# Bestandsaufnahme: Sender und Programmregeln verwalten (Schritt 2)

Analyse des bestehenden Projektstandes (ASP.NET Core + Blazor Server, Projekte `VideoWebPlayer/`, `VideoWebPlayer.Client/`, `VideoWebPlayer.Tests/`) bezogen auf die übersetzte Anforderung in [requirement.md](requirement.md) — Schritt 2 des Projektplans „Automatisierter TV-Sender" (GitHub Issue #111).

## Zusammenfassung

- **Die Kernkomponenten dieses Schritts existieren nicht.** Es gibt keine Entities `TVChannel`/`ProgramRule`, keine Enums `ProgramRuleType`/`ProgramRuleRecurrence`/`ProgramRuleMaterialType`, keinen `ProgramRuleService`, keine Sender-Verwaltungsseite (verifiziert per Volltextsuche über `VideoWebPlayer/`, `VideoWebPlayer.Client/`).
- **Schritt 1 ist gemergt:** `MediaItem.Duration` (`TimeSpan?`) und `MediaItem.DurationProbedAt` (`DateTime?`) existieren (`VideoWebPlayer/Data/MediaItem.cs`) samt Migration `20261021033436_AddMediaItemDuration` und Backup-Registrierung — für diesen Schritt aber nicht weiter relevant (nur Dokumentation des Stands).
- **Wiederverwendbare Infrastruktur ist vorhanden:**
  - `MediaSearchSelector.razor` — debounced Such-/Auswahldialog über `Client.RequestItemsAsync` (`GET /api/items?search=…&includeIndividualMediaTypes=true`), liefert `MediaEntryDto` mit `Type`+`Id` für `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`, gefiltert auf den sichtbaren Medienbestand des angemeldeten Benutzers.
  - `GET /api/items/genres` (`ItemsController.GetGenres` → `MediaMetadataEditorService.GetGenreOptionsAsync`) — liefert `DtoGenreOption` (ein Genre je distinctem Namen); Client-Methode `RequestGenreOptionsAsync`.
  - `api/playlists` (`PlaylistsController`/`IPlaylistService`) — eigene Playlists des Benutzers (`GetPlaylistsAsync`) und öffentliche (`GetPublicPlaylistsAsync`).
  - Admin-Muster: Blazor-Admin-Seiten (`AdminIndex`, `GenreAdmin`, `ProgramSettings` — `@rendermode InteractiveServer`, Claim-Prüfung `IsAdmin` via `AuthenticationStateProvider`, direkter `ApplicationDbContext`-Zugriff), `AdminSourcesController` mit `[BearerTokenCheck]` + Claim-Prüfung; `PlaylistService.SetPlaylistPublicAsync` prüft den Admin-Status aus dem Benutzerdatensatz.
  - Einstellungen: `Setup`-Tabelle + `ProgramSettingsService` (Single-Row-Pattern mit `GetOrCreateSetupAsync`).
  - EF-Konfigurationen unter `Data/Configurations/` via `ApplyConfigurationsFromAssembly`; Migrationen werden beim Start automatisch angewendet (`app.MigrateDatabase()` in `VideoWebPlayer/Program.cs`).
  - Backup-Registrierung: `VideoWebPlayerBackupData.OptionalRestoreTables`/`OptionalRestoreColumns`; Tabellen werden automatisch aus dem EF-Modell ermittelt (`GetTables()`), fehlende Tabellen/Spalten in Alt-Backups nur mit Registrierung toleriert.
  - Polymorphe Medienreferenz ohne harte FK: `PlaylistEntry.MediaType` + `MediaId`.
  - `ConfirmationDialog.razor` für Lösch-Bestätigungen.
- **Namenskonflikt beachtet:** `Playlist`/`PlaylistEntry`/`IPlaylistService`/`PlaylistsController` bezeichnen die Benutzer-Playlists; die Sender-Begriffswelt (Sender/Programmregel) muss davon getrennt bleiben.
- **`Genre` ist medienquellenbezogen** (`Genre.MediaSourceId`); die Genre-Auswahl der UI dedupliziert nach Name (`GetGenreOptionsAsync` gruppiert nach `Name`).

**Test-Ausgangszustand:** 1.795 Tests bestanden, 0 fehlgeschlagen, 1 übersprungen (bereits per `[Fact(Skip)]` deaktivierter Test). Lauf: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` auf Commit `69c5860`, Release-Debug-Build. Nachweis: [inventory/tests.md](inventory/tests.md), Report [inventory/test-results/baseline-schritt2.trx](inventory/test-results/baseline-schritt2.trx).

## Details

- [Datenmodell](inventory/models.md) — relevante Entities (`Playlist`/`PlaylistEntry`-Muster, `MediaItem`, `Genre`, `MediaSourceUser`, `UnlockedMediaEntry`, `Setup`), Backup-Registrierung, EF-Konfigurationsmuster
- [Logik](inventory/logic.md) — `ProgramSettingsService`, Admin-UI-/Controller-Muster, `ItemsController`-Suche, `MediaSearchSelector`, `PlaylistsController`, DI-Registrierung, Migrationsanwendung
- [Enums](inventory/enums.md) — vorhandene Enums/Konstanten (`MediaTypeValues`, `PlaylistSortMode`, `MediaSourceType`), fehlende Sender-Enums
- [Interfaces](inventory/interfaces.md) — `IUnlockedMediaService`, `IPlaylistService` (relevante Methoden), `IAuthService`
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Lauf-Nachweis, relevante Testklassen und Hilfsmethoden
