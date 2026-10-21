# Umsetzungsplan: Sender und Programmregeln verwalten (Schritt 2)

## Übersicht

Der automatische TV-Sender bekommt sein Datenfundament und seine Administrations-Oberfläche: die Entities `TVChannel` (Sender, kanalfähig, zunächst genau einer) und `ProgramRule` (Programmregel) samt Enums, EF-Konfigurationen, Migration und Backup-Registrierung. Ein `ProgramRuleService` kapselt CRUD mit serverseitiger Admin-Erzwingung und fachlicher Validierung; eine neue Admin-Seite `/admin/tv-sender` stellt die Verwaltung mit Such-/Auswahldialogen für das Programmmaterial bereit. Betroffene Bereiche: Administration, Datenhaltung und Backup, Berechtigungen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `ProgramRule`-Materialreferenz | Polymorphe Referenz `MaterialType` (Enum `ProgramRuleMaterialType`) + `MaterialId` (`long`) **ohne** EF-Fremdschlüssel — bewährtes Muster von `PlaylistEntry.MediaType`/`MediaId` | Fünf verschiedene Zielentitäten (Serie, Film, Filmsammlung, Genre, Benutzer-Playlist) mit je einer Referenz je Regelart; harte FKs würden fünf Nullable-Spalten plus Löschverhalten-Entscheidungen je Ziel erfordern und bei `DeleteMediaSourceAsync` (`ExecuteDeleteAsync`, FK-bypassend) ohnehin nicht greifen. Existenz wird beim Schreiben im Service geprüft; verwaiste Referenzen behandelt Schritt 3 bei der Planung. |
| `TVChannel` | Eigenständige Entity `Id`, `Name`, `CreatedAt`, `UpdatedAt`; der eine ausgelieferte Sender wird bei erstem Zugriff automatisch als „TV-Sender" angelegt und ist nur umbenennbar (kein Anlegen/Löschen in der UI) | Anforderung „kanalfähig, zunächst genau ein Sender": Das Modell trägt Mehrkanalfähigkeit schon (`ProgramRule.TVChannelId`), ohne Mehraufwand in der UI. Auto-Seed statt Migration-Seed, damit kein fester Name in der Migration verankert wird. |
| Länge vs. Folgenanzahl | Zwei optionale Felder `EpisodeCount` (`int?`) und `SlotDuration` (`TimeSpan?`); Validierung: mindestens eines gesetzt und positiv; `SeriesFixedTime` verlangt `EpisodeCount` | „Länge bzw. Anzahl der Folgen" ist fachlich je Regelart verschieden gemeint; zwei getrennte Spalten sind unmissverständlich und einfach validierbar. |
| Materialart je Regelart | Validierungsmatrix im Service: `SeriesFixedTime`→`TVShow`; `MoviePrimeTime`→`Movie`,`MovieCollection`; `GenreRotation`→`Genre`; `Repeat`,`HolidaySpecial`→alle fünf Arten; `Filler`→`Movie`,`MovieCollection`,`Genre`,`Playlist` | „Je Regelart genau eines" wird als Matrix im Service gepinnt und getestet; die UI zeigt nur die passenden Auswahldialoge. `SeriesFixedTime` ist seriengebunden, `GenreRotation` genrebasiert, `Filler` braucht einen Pool (keine Einzelserie). |
| Genre als Material | `MaterialType=Genre` + `MaterialId=Genre.Id` einer deduplizierten Genre-Option (`DtoGenreOption`); Schritt 3 löst gleichnamige Genres aller Quellen auf | Genres sind quellenbezogen (`Genre.MediaSourceId`), werden aber namensdedupliziert ausgewählt — konsistent mit `GetGenreOptionsAsync` und der `ItemsController`-Genre-Filterung. |
| Admin-Erzwingung | `ProgramRuleService` verlangt bei jeder Mutation die `userId` und prüft `ApplicationUser.IsAdmin` aus dem Benutzerdatensatz (`ForbiddenAccessException` sonst); Lesemethoden zusätzlich admin-gated | Muster `PlaylistService.SetPlaylistPublicAsync` (DB-Status statt Token-Claim): ein entzogenes Admin-Recht wirkt sofort. In Blazor Server läuft der Service-Aufruf serverseitig — kein Umgehungsweg über die UI. |
| Mediensuche mit Typeinschränkung | `MediaSearchSelector` erhält optionalen Parameter `AllowedMediaTypes` (`IReadOnlyCollection<string>?`), der die angezeigten Ergebnisse filtert | `RequestItemsAsync` liefert alle Typen gemischt; die Erweiterung ist minimal, rückwärtskompatibel und wiederverwendbar. |
| Genre-/Playlist-Auswahl | Klartext-Dropdowns (`<select>`): Genres via `Client.RequestGenreOptionsAsync`, Playlists via `Client.RequestPlaylistsAsync` + `RequestPublicPlaylistsAsync` | Überschaubare Mengen benannter Entitäten; kein neuer Suchdialog nötig; erfüllt „keine internen IDs". |
| Bundesland-Einstellung | `Setup.HolidayFederalState` (`string?`, ISO-Code z. B. `DE-NW`) + Get/Update in `ProgramSettingsService`; Pflege über Dropdown auf der Sender-Admin-Seite | Entscheidung „Konfiguration über etablierte Einstellungsmechanismen"; `null` = nur bundesweit gültige Feiertage (Fallback in Schritt 3). |
| Zeitfeld | `TimeOfDay` als `TimeOnly` (SQLite: TEXT); `SingleDate` als `DateOnly` (TEXT); `DayOfWeek` als `int`-Enum | EF Core 10 mappt `TimeOnly`/`DateOnly` nativ auf SQLite-TEXT — lesbar, vergleichbar. |

## Programmabläufe

### Sender-Verwaltung öffnen (Admin)

1. Administrator öffnet `/admin` → Kachel „TV-Sender" → `/admin/tv-sender`.
2. `TVSenderAdmin.razor` prüft `IsAdmin`-Claim; bei Nicht-Admin „Nicht autorisiert." und keine Daten.
3. Bei Admin: `ProgramRuleService.GetOrCreateChannelAsync` liefert den einen Sender (legt ihn beim ersten Aufruf als „TV-Sender" an); `GetRulesAsync` lädt seine Regeln; `ProgramSettingsService` liefert `HolidayFederalState`.
4. Umbenennen: Textfeld + Speichern → `RenameChannelAsync` (Admin-Prüfung serverseitig).
5. Bundesland: Dropdown → `UpdateHolidayFederalStateAsync`.

Beteiligte Klassen/Komponenten: `TVSenderAdmin`, `ProgramRuleService`, `ProgramSettingsService`, `AdminIndex`.

### Programmregel anlegen/bearbeiten

1. „Neue Regel" bzw. „Bearbeiten" öffnet `admin-dialog`-Overlay mit Feldern: Name, Regelart (`<select>`), Wiederholungsmuster (`<select>`), Wochentag (nur `Weekly`), Datum (nur `Once`), Sendezeit (`InputTime`), Länge (`SlotDuration` Minuten) und Folgenanzahl, Priorität, Aktiv-Flag.
2. Materialbereich abhängig von `GetAllowedMaterialTypes(ruleType)`: enthält `TVShow`/`Movie`/`MovieCollection` → `MediaSearchSelector` mit `AllowedMediaTypes`-Filter; enthält `Genre` → Genre-Dropdown; enthält `Playlist` → Playlist-Dropdown. Bereits gewähltes Material wird mit Namen angezeigt und kann gewechselt werden.
3. Speichern → `CreateRuleAsync`/`UpdateRuleAsync(userId, input)`; Service validiert (Pflichtfelder je Muster, Materialart, Material-Existenz) und wirft `ArgumentException`/`KeyNotFoundException`/`ForbiddenAccessException` — die Seite zeigt die Meldung im Dialog an.
4. Liste lädt neu; Fehler werden sichtbar statt technischer Fehlerbilder.

Beteiligte Klassen/Komponenten: `TVSenderAdmin`, `MediaSearchSelector`, `VideoWebPlayerClient`, `ProgramRuleService`, `ApplicationDbContext`.

### Programmregel aktivieren/deaktivieren/löschen

1. Toggle „Aktiv" in der Regelzeile → `SetRuleActiveAsync`.
2. „Löschen" → `ConfirmationDialog` → `DeleteRuleAsync`.

Beteiligte Klassen/Komponenten: `TVSenderAdmin`, `ConfirmationDialog`, `ProgramRuleService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `TVChannel` | Datenmodellklasse (`VideoWebPlayer/Data/TVChannel.cs`) | Sender als benanntes, kanalfähiges Objekt: `Id`, `Name` (required, max 255), `CreatedAt`, `UpdatedAt`, `ProgramRules`-Navigation |
| `ProgramRule` | Datenmodellklasse (`VideoWebPlayer/Data/ProgramRule.cs`) | Programmregel mit allen Regelattributen (s. Designentscheidungen) |
| `ProgramRuleType` | Enum (`VideoWebPlayer/Data/`) | `SeriesFixedTime`, `MoviePrimeTime`, `GenreRotation`, `Repeat`, `HolidaySpecial`, `Filler` |
| `ProgramRuleRecurrence` | Enum (`VideoWebPlayer/Data/`) | `Daily`, `Weekly`, `Once`, `Holiday` |
| `ProgramRuleMaterialType` | Enum (`VideoWebPlayer/Data/`) | `TVShow`, `Movie`, `MovieCollection`, `Genre`, `Playlist` |
| `TVChannelConfiguration` | `IEntityTypeConfiguration<TVChannel>` (`Data/Configurations/`) | PK, Name required + `MaxLength(255)` + NOCASE, Zeitstempel required |
| `ProgramRuleConfiguration` | `IEntityTypeConfiguration<ProgramRule>` (`Data/Configurations/`) | PK, Pflichtfelder, Enum-Defaults, FK `TVChannelId` → `TVChannel` mit Cascade, Index `(TVChannelId, Name)` unique, Index `TVChannelId` |
| `IProgramRuleService` | Interface (`VideoWebPlayer/Services/`) | Contract des Regelverwaltungs-Service |
| `ProgramRuleService` | Service (`VideoWebPlayer/Services/`, scoped) | CRUD + Admin-Erzwingung + Validierung + Material-Existenzprüfung + `GetOrCreateChannelAsync` + `GetAllowedMaterialTypes` |
| `ProgramRuleInput` | Record (`VideoWebPlayer/Services/`) | Eingabeparameter-Objekt für Create/Update (alle Regelattribute ohne `Id`) |
| `TVSenderAdmin.razor` | Blazor-Seite (`Components/Pages/Admin/`, Route `/admin/tv-sender`) | Sender-Umbenennung, Bundesland-Dropdown, Regel-CRUD-UI mit Material-Auswahldialogen |
| Migration `AddTVChannelsAndProgramRules` | EF-Migration | Tabellen `TVChannels`, `ProgramRules`, Spalte `Setups.HolidayFederalState` |

## Änderungen an bestehenden Klassen

### `ApplicationDbContext` (DbContext)

- **Neue Eigenschaften:** `TVChannels` (`DbSet<TVChannel>`), `ProgramRules` (`DbSet<ProgramRule>`) — Zweck: Datenhaltung der neuen Entities.

### `Setup` (Entity)

- **Neue Eigenschaften:** `HolidayFederalState` (`string?`) — Bundesland-Code für den Feiertagskalender.

### `ProgramSettingsService` (Service)

- **Neue Methoden:** `GetHolidayFederalStateAsync` — liefert `Setup.HolidayFederalState` oder `null`; `UpdateHolidayFederalStateAsync(string? code, ct)` — persistiert den Code (Whitelist der 16 `DE-*`-Codes bzw. `null`, sonst `ArgumentException`).

### `MediaSearchSelector.razor` (Komponente)

- **Neue Parameter:** `AllowedMediaTypes` (`IReadOnlyCollection<string>?`, default `null`) — filtert gerenderte Suchergebnisse auf die erlaubten `MediaTypeValues`; `null` = unverändertes Verhalten.

### `AdminIndex.razor` (Seite)

- Neue `admin-tile`-Kachel „TV-Sender" → `/admin/tv-sender` (Sender-Name, Programmregeln und Feiertags-Bundesland verwalten).

### `VideoWebPlayerBackupData` (Backup)

- `OptionalRestoreTables` += `TVChannels`, `ProgramRules` (via `nameof(ApplicationDbContext.*)`)
- `OptionalRestoreColumns` += `Setups.HolidayFederalState`

### `ServiceCollectionExtensions.AddVideoWebPlayerServices` (DI)

- `services.AddScoped<IProgramRuleService, ProgramRuleService>();`

### `PlaylistsE2ETestBase` (Test-Hilfsklasse)

- `DriveUntilStateAsync` von `private` → `protected static` — Wiederverwendung des Anti-Flake-Treibers durch die neue E2E-Testklasse (kein Verhaltensunterschied).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddTVChannelsAndProgramRules` | `TVChannels` (neu), `ProgramRules` (neu), `Setups.HolidayFederalState` (neu, nullable TEXT) | Neue Tabellen für Sender/Programmregeln inkl. Indizes + FK-Kaskade; neue optionale Setup-Spalte |

Erzeugung per `dotnet ef migrations add AddTVChannelsAndProgramRules -p VideoWebPlayer`; Anwendung automatisch via `app.MigrateDatabase()`.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramRule.Name` | Pflicht, max 255, je Sender eindeutig | `ArgumentException`; Unique-Index `(TVChannelId, Name)` → `DbUpdateException` als Rückversicherung |
| `ProgramRule.RuleType`/`Recurrence`/`MaterialType` | gültige Enum-Werte | `ArgumentException` |
| `Recurrence=Weekly` | `DayOfWeek` gesetzt | `ArgumentException` |
| `Recurrence=Once` | `SingleDate` gesetzt | `ArgumentException` |
| `TimeOfDay` | gesetzt (Pflicht) | `ArgumentException` |
| `EpisodeCount`/`SlotDuration` | mindestens eines gesetzt und > 0; `SeriesFixedTime` verlangt `EpisodeCount` | `ArgumentException` |
| `MaterialType`/`MaterialId` | Materialart in `GetAllowedMaterialTypes(ruleType)`; referenzierte Entität existiert (Serie→`TVShows`, Film→`Movies`, Filmsammlung→`MovieCollections`, Genre→`Genres`, Playlist→`Playlists`) | `ArgumentException` / `KeyNotFoundException` |
| `Priority` | ganzzahlig, frei (höher = wichtiger, Semantik in Schritt 3) | — |
| `TVChannel.Name` | Pflicht, max 255 | `ArgumentException` |
| `HolidayFederalState` | `null` oder einer der 16 `DE-*`-Codes | `ArgumentException` |
| `userId` je Mutation | Benutzer existiert und `IsAdmin` | `UnauthorizedAccessException` (kein Benutzer) / `ForbiddenAccessException` (kein Admin) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Setup.HolidayFederalState` | `string?` (DB-Spalte) | `null` (≙ nur bundesweit gültige Feiertage) | Bundesland für den Feiertagskalender (Auswertung in Schritt 3) |

Keine `appsettings`-Änderungen.

## Seiteneffekte und Risiken

- **`MediaSearchSelector`:** Neuer optionaler Parameter — bestehende Aufrufer (`PlaylistEntriesList`) unverändert.
- **Backup:** `TVChannels`/`ProgramRules` fließen ab sofort in Backups ein; Alt-Backups ohne diese Tabellen bleiben via `OptionalRestoreTables` restorebar (Regressionstest).
- **`DeleteMediaSourceAsync` / Playlist-Löschung:** Material-Referenzen haben keinen DB-FK — Regeln können verwaiste `MaterialId`s halten; bewusst akzeptiert (Planungs-Behandlung in Schritt 3), im Usability-Review zu beachten.
- **`Setup`-Spalte:** Alt-Backups ohne `HolidayFederalState` restoren dank `OptionalRestoreColumns`-Eintrag.
- **Kanalfähigkeit:** UI zeigt absichtlich nur den einen Sender; Service-API ist kanalfähig (`GetRulesAsync(channelId)`).

## Umsetzungsreihenfolge

1. **Enums + Entities + EF-Konfigurationen + DbContext-Erweiterung**
   - Voraussetzungen: Keine
   - `ProgramRuleType`, `ProgramRuleRecurrence`, `ProgramRuleMaterialType`, `TVChannel`, `ProgramRule`, `TVChannelConfiguration`, `ProgramRuleConfiguration`, DbSets in `ApplicationDbContext`
2. **`Setup.HolidayFederalState` + `ProgramSettingsService`-Methoden**
   - Voraussetzungen: Keine
3. **EF-Migration `AddTVChannelsAndProgramRules`**
   - Voraussetzungen: Schritte 1–2 (Modell steht)
4. **Backup-Registrierung in `VideoWebPlayerBackupData`**
   - Voraussetzungen: Schritte 1–2
5. **`IProgramRuleService` + `ProgramRuleService` + `ProgramRuleInput` + DI-Registrierung**
   - Voraussetzungen: Schritt 1
6. **`MediaSearchSelector.AllowedMediaTypes`**
   - Voraussetzungen: Keine
7. **`TVSenderAdmin.razor` + `AdminIndex`-Kachel**
   - Voraussetzungen: Schritte 5–6
8. **Unit-/Integrationstests (Service, EF-Verhalten, Setup-Einstellung)**
   - Voraussetzungen: Schritte 1–5
9. **Backup-Regressionstest**
   - Voraussetzungen: Schritt 4
10. **E2E-Tests (`PlaylistsE2ETestBase`-Freigabe + `TVSenderAdminE2ETests`)**
    - Voraussetzungen: Schritt 7
11. **Build Debug+Release, volle Testsuite**
    - Voraussetzungen: alle vorherigen

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateRule_ValidInput_PersistsRule` | `ProgramRuleServiceTests_Crud` | Regel wird mit allen Feldern gespeichert; `GetRulesAsync` liefert sie |
| `GetOrCreateChannel_FirstCall_CreatesSingleChannel` | `ProgramRuleServiceTests_Channel` | Auto-Anlage „TV-Sender", idempotent, genau ein Sender |
| `RenameChannel_PersistsNewName` | `ProgramRuleServiceTests_Channel` | Umbenennung |
| `UpdateRule_ChangesFields` / `SetRuleActive_TogglesFlag` / `DeleteRule_RemovesRule` | `ProgramRuleServiceTests_Crud` | Bearbeiten, Aktiv-Toggle, Löschen |
| `*Async_NonAdmin_ThrowsForbidden` (je Mutation, parametrisiert wo sinnvoll) | `ProgramRuleServiceTests_Authorization` | `ForbiddenAccessException` für Nicht-Admin; `UnauthorizedAccessException` bei unbekannter userId — serverseitige Erzwingung |
| `CreateRule_WeeklyWithoutDayOfWeek_Throws` / `OnceWithoutDate_Throws` | `ProgramRuleServiceTests_Validation` | Pflichtfelder je Wiederholungsmuster |
| `CreateRule_SeriesFixedTimeWithMovie_Throws` / `GenreRotationWithTvShow_Throws` / `FillerWithTvShow_Throws` | `ProgramRuleServiceTests_Validation` | Materialart-Matrix |
| `CreateRule_WithoutEpisodeCountAndDuration_Throws` / `SeriesFixedTime_RequiresEpisodeCount` | `ProgramRuleServiceTests_Validation` | Länge/Folgen-Pflicht |
| `CreateRule_MissingMaterial_ThrowsKeyNotFound` | `ProgramRuleServiceTests_Validation` | Existenzprüfung Material (je Materialart) |
| `CreateRule_DuplicateNameSameChannel_Fails` | `ProgramRuleServiceTests_Crud` | Unique `(TVChannelId, Name)` auf echtem SQLite |
| `DeleteChannel_CascadesRules` | `ProgramRuleServiceTests_Channel` | FK-Kaskade auf echtem SQLite |
| `Get/UpdateHolidayFederalState` | `ProgramSettingsServiceTests` (erweitern) | Get-Default `null`, Update persistiert, ungültiger Code → `ArgumentException` |
| `Restore_LegacyBackupWithoutSenderTables_Succeeds` | `VideoWebPlayerBackupDataTests` (erweitern) | `RemoveTablesAsync(["TVChannels","ProgramRules"])` + `RemoveColumnsAsync("Setups", ["HolidayFederalState"])` → `ReadFromAsync` erfolgreich |
| `Render_OnlyAllowedTypes_AreShown` | `MediaSearchSelectorTests` (erweitern) | `AllowedMediaTypes` filtert Ergebnisliste |
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | `TVSenderAdminE2ETests` | Playwright: Regel per Dialog + Mediensuche anlegen → in Liste → bearbeiten → deaktivieren → löschen (ein konsolidierter Fluss) |
| `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` | `TVSenderAdminE2ETests` | Nicht-Admin → „Nicht autorisiert.", keine Verwaltungselemente |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MediaSearchSelectorTests` | Erweiterung um `AllowedMediaTypes`-Fall (bestehende Tests bleiben unverändert) |
| `ApiDocumentationContractTests*` | Nur falls Controller geändert würden — nicht geplant |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin legt Regel an (inkl. Materialauswahl per Suche), bearbeitet, deaktiviert, löscht | `TVSenderAdminE2ETests` | Programmregel-CRUD + Auswahl über Suchdialog statt IDs | Benutzerfluss über Blazor-UI; einzig belastbarer Nachweis der bedienbaren Auswahl |
| Pflicht | Nicht-Admin sieht „Nicht autorisiert." | `TVSenderAdminE2ETests` | Berechtigung nur Administratoren | Sichtbarkeits-/Zugriffsregel über UI |

Bestehende E2E-Tests betroffen: Keine (`MediaSearchSelector`-Erweiterung ist rückwärtskompatibel).

## Offene Punkte

Keine.
