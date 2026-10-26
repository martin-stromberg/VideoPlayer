# Umsetzungsplan: Medienquellen — Merkmal „Im Menü ausblenden“ (Issue #284)

## Übersicht

Neues optionales boolesches Merkmal `HiddenInMenu` (Default `false`) an `MediaSource`, das den Eintrag der Quelle im `NavMenu` für Admins und Nicht-Admins gleichermaßen unterdrückt. Das Merkmal ist rein kosmetisch: Zugriffssteuerung (`MediaSourceUsers`, `IUnlockedMediaService`), Scanner, Planungslogik und Direktaufruf von `/mediasource/{id}` bleiben unverändert. Betroffen sind Datenmodell, EF-Migration, DTO, `NavMenu.razor`, `MediaSourceAdminDetails.razor`, `VideoWebPlayerBackupData` sowie Tests und Dokumentation.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Filterort der Menüliste | **Clientseitig** in `NavMenu.razor` (Filter direkt bei der Zuweisung in `OnInitializedAsync`), `SourcesController.GetSources` bleibt unverändert | Die API-Liste bleibt vollständig, sodass das DTO-Flag für externe Listenkonsumenten (MAUI-App über `VideoWebPlayerClient`) nutzbar ist — genau die Begründung des Akzeptanzkriteriums. Das Merkmal ist rein kosmetisch, eine serverseitige Filterung würde `HiddenInMenu` in der Liste nutzlos machen (immer `false`). |
| Filterstelle im `NavMenu` | Filterung beim Befüllen von `mediaSources` in `OnInitializedAsync` (`Where(s => !s.HiddenInMenu)`), nicht beim Rendern | Die Sektionsbedingung `mediaSources.Any()` (Zeile 50) greift dann automatisch auf die gefilterte Menge — die Überschrift „Quellen“ verschwindet, wenn alle Quellen versteckt sind, ohne dass eine zweite gefilterte Liste oder ein berechnetes Property nötig wird. Entspricht dem Muster der flaggesteuerten Live-TV-Ausblendung (`tvSenderEnabled`, Zeilen 41–48). |
| Optionaler Hinweis bei Direktaufruf `/mediasource/{id}` | **Nicht umsetzen** | Nicht gefordert; die Anforderung beschreibt das Merkmal ausdrücklich als rein kosmetisch mit unverändertem Direktaufruf. Ein Hinweis wäre eine spekulative Erweiterung. |
| Kennzeichnung versteckter Quellen in `MediaSourceAdmin.razor` (Badge/Spalte) | **Nicht umsetzen** | Ausdrücklich nicht gefordert; der Zustand ist in der Detailansicht (`MediaSourceAdminDetails.razor`) über die Checkbox nachvollziehbar. |
| Benennung | `HiddenInMenu` beibehalten | Arbeitsname aus der Anforderung, fachlich präzise; die Codebasis mischt ohnehin Konventionen (`IsManuallyEdited` vs. `TvSenderEnabled`), ein `Is`-Präfix ist nicht zwingend. |
| Refresh des laufenden Menüs | Kein Refresh-Mechanismus; Bestandsverhalten beibehalten | `NavMenu` befüllt `mediaSources` nur in `OnInitializedAsync` — das gilt bereits heute für Umbenennungen. Ein Echtzeit-Refresh wäre eine Erweiterung über die Anforderung hinaus. |
| Checkbox in `MediaSourceAdminDetails.razor` | `InputCheckbox` mit `form-check`-Markup und `form-text`-Hilfetext innerhalb des `EditForm` | Wiederverwendung des bestehenden UI-Musters der Benutzerfreigabe-Checkboxen (`form-check`/`form-check-input`/`form-check-label`, Zeilen 127–135) und der `form-text text-muted`-Hilfetexte (z. B. Zeile 105). `InputCheckbox` bindet direkt an `editSource.HiddenInMenu` — kein manueller `checked`/`@onchange`-Code nötig, da anders als bei den Benutzerfreigaben keine Mengenlogik dahintersteht. |
| Client-Double für bUnit-`NavMenu`-Tests | `NoOpVideoWebPlayerClient` (`VideoWebPlayer.Tests/Helpers/`) um optionalen Konstruktorparameter `DtoMediaSource[]? sources` erweitern; `HttpGetAsync<DtoMediaSource[]>` liefert diesen Wert (Default: leeres Array) | `NavMenu` lädt Quellen über `Client.RequestSourcesAsync()` → `HttpGetAsync<DtoMediaSource[]>`. Heute fällt dieser Typ in den Default-Branch (`List<MediaEntryDto>`-Cast) — die `InvalidCastException` wird in `RequestSourcesAsync` zwar abgefangen (leere Liste), für Filtertests mit Quellen ist aber ein lieferbares `DtoMediaSource[]` nötig. Ein einzelnes konfigurierbares Double vermeidet eine weitere private Kopie (Muster: `FakeAuthService`). |

## Programmabläufe

### Admin pflegt das Merkmal

1. Admin öffnet `/admin/mediasources/{Id}` (bzw. `/admin/mediasources/new`).
2. `MediaSourceAdminDetails.OnInitializedAsync` prüft `isAdmin` (Nicht-Admins: „Zugriff verweigert“, serverseitig erzwungen), lädt die Quelle per `DbContext.MediaSources.Include(x => x.MediaSourceUsers)` und kopiert die Felder einzeln in `editSource` — `HiddenInMenu` wird in dieser manuellen Kopie ergänzt (Zeilen 209–223).
3. Das Formular zeigt eine Checkbox „Im Menü ausblenden“ mit Hilfetext (z. B. „für reine Clip-/Füller-Quellen — die Quelle bleibt für Scanner, Füller und Clip-Gruppen nutzbar“), gebunden an `editSource.HiddenInMenu`.
4. `Save()` ruft `UpdateMediaSourceAsync(editSource)` (bzw. `AddMediaSourceAsync` bei Neu-Anlage): `FindAsync` lädt die Entität, `MediaSourceExtensions.Update` kopiert `HiddenInMenu` mit, `SaveChangesAsync` persistiert, `MediaSourceUpdatedEvent` wird publiziert.
5. Bei Neu-Anlage gilt der CLR-Default `false`; `editSource = new MediaSource { Port = 22 }` benötigt keine Ergänzung.

Beteiligte Klassen/Komponenten: `MediaSourceAdminDetails`, `ApplicationDbContext`, `MediaSourceExtensions`, `MediaSource`, `MediaSourceUpdatedEvent`.

### Menüfilterung im NavMenu

1. Angemeldeter Benutzer lädt eine Seite → `NavMenu.OnInitializedAsync` ruft `Client.EnsureAuthorizationTokenSilentlyAsync` und `Client.RequestSourcesAsync()` auf.
2. `RequestSourcesAsync` ruft `GET /api/Sources` → `SourcesController.GetSources` liefert die freigegebenen Quellen (`MediaSourceUsers` ∪ `IUnlockedMediaService.GetUnlockedSourceIdsForUserAsync`) als `DtoMediaSource[]`; `ApiBaseController.Create<DtoMediaSource>` befüllt `HiddenInMenu` automatisch per Reflection (namensgleiche Property).
3. `NavMenu` filtert beim Zuweisen: `mediaSources` enthält nur Quellen mit `HiddenInMenu == false`.
4. Rendering: `mediaSources.Any()` (Zeile 50) steuert die Überschrift „Quellen“; die `foreach` (Zeilen 55–70) rendert nur sichtbare `NavLink`s auf `/mediasource/{id}`.
5. Admins erhalten dieselbe gefilterte Liste — `GetSources` unterscheidet nicht nach Rolle, und die Filterung erfolgt für alle gleich.

Beteiligte Klassen/Komponenten: `NavMenu`, `VideoWebPlayerClient`, `SourcesController`, `ApiBaseController`, `DtoMediaSource`, `MediaSourceUser`, `IUnlockedMediaService`.

### Backup und Restore

1. Neues Backup: `VideoWebPlayerBackupData.GetTables()` iteriert `_db.Model.GetEntityTypes()` — die neue Spalte landet automatisch im Backup, kein weiterer Code nötig.
2. Alt-Backup ohne `MediaSources.HiddenInMenu`: `ValidatePayloadAsync` bemängelt fehlende Spalten außer den in `OptionalRestoreColumns` registrierten — dort wird `MediaSources.HiddenInMenu` ergänzt.
3. Beim Restore fehlender Spalten greift `OptionalRestoreBoolDefaults`: Eintrag `(MediaSources, HiddenInMenu, false)` füllt den Wert mit `false` (sichtbar) auf — analog `Setups.TvSenderEnabled`.

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData`, `ApplicationDbContext`, `MediaSource`.

### Direktaufruf einer versteckten Quelle (unverändert)

1. `GET /mediasource/{id}` → `MediaSourceDetails.razor` → `MediaSourceDetailsViewModel.InitializeAsync` → `RequestSourceAsync(id)` → `SourcesController.GetSource`.
2. `GetSource` prüft nur die Freigabe (`MediaSourceUsers` ∪ Einzelfreischaltung), nicht `HiddenInMenu` — die Seite bleibt erreichbar.

Beteiligte Klassen/Komponenten: `MediaSourceDetails`, `MediaSourceDetailsViewModel`, `VideoWebPlayerClient`, `SourcesController`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| — | — | Keine neuen Produktivklassen. Das Merkmal ist eine einzelne boolesche Eigenschaft; Persistenz (`DbSet<MediaSource>`), DTO-Mapping (`ApiBaseController.Create<T>`) und Backup-Iteration (`GetTables`) greifen über bestehende Mechanismen. Neue Testklassen sind im Abschnitt „Tests“ gelistet. |

## Änderungen an bestehenden Klassen

### `MediaSource` (Datenmodellklasse, `VideoWebPlayer/Data/MediaSource.cs`)

- **Neue Eigenschaften:** `HiddenInMenu` (`bool`, Default `false` per CLR-Initialisierer) — unterdrückt den Menüeintrag der Quelle im `NavMenu`.
- **Hinweis (Encoding):** Die Datei ist Windows-1252-kodiert — Änderungen müssen das Encoding erhalten (kein UTF-8-Rewrite); neue Zeilen idealerweise ohne Nicht-ASCII-Zeichen.

### `MediaSourceExtensions` (static class, `VideoWebPlayer/Data/MediaSourceExtensions.cs`)

- **Geänderte Methoden:** `Update(this MediaSource target, MediaSource source)` — zusätzlich `target.HiddenInMenu = source.HiddenInMenu`; ohne die Ergänzung ginge das Flag bei `UpdateMediaSourceAsync` (Admin-Speichern, `MediaSourceAdmin.ResetScan`) still verloren.
- **Hinweis (Encoding):** Datei ist Windows-1252.

### `DtoMediaSource` (DTO, `VideoWebPlayer.Client/Models/DtoSource.cs`)

- **Neue Eigenschaften:** `HiddenInMenu` (`bool`) — wird in `GET /api/Sources` und `GET /api/Sources/{id}` ausgeliefert; `ApiBaseController.Create<T>` kopiert namensgleiche Properties automatisch, kein Mapping-Code nötig. Damit können externe Clients (MAUI-App) das Flag perspektivisch auswerten.

### `NavMenu` (Razor-Komponente, `VideoWebPlayer/Components/Layout/NavMenu.razor`)

- **Geänderte Logik:** `OnInitializedAsync` (Zeile 131) — `mediaSources` wird gefiltert befüllt (`!s.HiddenInMenu`), sodass die Überschrift `mediaSources.Any()` (Zeile 50) und die `foreach`-Liste (Zeilen 55–70) unverändert auf der gefilterten Menge arbeiten.

### `MediaSourceAdminDetails` (Razor-Komponente, `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor`)

- **Geändertes Markup:** Neue Checkbox (`InputCheckbox`, `form-check`-Stil mit Hilfetext) im `EditForm`, gebunden an `editSource.HiddenInMenu` — Platzierung z. B. als eigener Block vor „Freigegebene Benutzer“ (Zeilen 124–136).
- **Geänderte Methoden:** `OnInitializedAsync` — die manuelle `editSource`-Kopie (Zeilen 209–223) übernimmt `HiddenInMenu` explizit, sonst zeigt die Checkbox beim Bearbeiten immer `false`.

### `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`)

- **Erweiterte Registrierung `OptionalRestoreColumns`** (Zeilen 51–108): `$"{nameof(ApplicationDbContext.MediaSources)}.{nameof(MediaSource.HiddenInMenu)}"`.
- **Erweiterte Registrierung `OptionalRestoreBoolDefaults`** (Zeilen 118–133): `(nameof(ApplicationDbContext.MediaSources), nameof(MediaSource.HiddenInMenu), false)` — Default `false` = sichtbar, analog `Setup.TvSenderEnabled` (Zeile 132).

### `NoOpVideoWebPlayerClient` (Test-Hilfsklasse, `VideoWebPlayer.Tests/Helpers/NoOpVideoWebPlayerClient.cs`)

- **Erweiterung:** Optionaler Konstruktorparameter `DtoMediaSource[]? sources`; `HttpGetAsync<T>` liefert für `T == typeof(DtoMediaSource[])` diesen Wert (Default `Array.Empty<DtoMediaSource>()`). Bestehende Aufrufer ohne Parameter bleiben unverändert — wo heute der `InvalidCastException`-Pfad in `RequestSourcesAsync` abgefangen wurde, kommt künftig ein echtes leeres Array zurück (gleiches Ergebnis: leere Quellenliste).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddMediaSourceHiddenInMenu` | Tabelle `MediaSources`, neue Spalte `HiddenInMenu` (`INTEGER`, `nullable: false`, `defaultValue: false`) | Vorbild `20261023050115_AddSetupTvSenderEnabled`: non-nullable Spalte mit `defaultValue: false`; `ApplicationDbContextModelSnapshot.cs` wird von der Migration mitgeführt. Bestandsdaten werden `false` (sichtbar). |

## Validierungsregeln

Keine — `HiddenInMenu` ist ein boolesches Merkmal, das ausschließlich über eine Checkbox gepflegt wird; es gibt keine freie Eingabe und keine Wertebereichsprüfung. Die Admin-Absicherung läuft über das bestehende `isAdmin`-Gate der Seite.

## Konfigurationsänderungen

Keine — das Merkmal ist eine Eigenschaft je `MediaSource` und kein globaler Schalter; `Setup`/`ProgramSettingsService` werden nicht angefasst.

## Seiteneffekte und Risiken

- **Bestandsdaten:** Durch `defaultValue: false` werden alle bestehenden Quellen sichtbar — gewünschtes Verhalten, kein Eingriff nötig.
- **`MediaSourceAdmin.ResetScan`:** Ruft `UpdateMediaSourceAsync` mit der getrackten Entity auf — der geladene `HiddenInMenu`-Wert wird identisch zurückkopiert, kein Verlust. Verifikationspunkt für die Abnahme.
- **API-Payload:** `DtoMediaSource` wächst um ein Feld — additive, abwärtskompatibele Änderung; ältere Clients ignorieren es. `docs/API.md` muss das Feld dokumentieren (DoD; `ApiDocumentationContractTests` prüft nur Routen, nicht DTO-Felder).
- **Verifikationspunkte (dürfen nicht verändert werden):** `TVSenderAdmin.razor` (Materialauswahl `ProgramRuleMaterialType.MediaSourceClips`), `TVSenderAdminClipGroups.razor`, `ProgramClipGroupService.GetClipCandidatesAsync`, `ProgramScheduleService`/`ProgramRuleService`, `MediaSourceScanService`/`MediaSourceScanner`, `SourcesController.GetSource`, `MediaSourceAdmin.razor`, `AdminSourcesController` — alle lesen `MediaSource` direkt über den `DbContext` und sehen versteckte Quellen weiterhin vollständig.
- **Encoding-Falle:** `MediaSource.cs`, `MediaSourceExtensions.cs`, `ApplicationDbContext.cs` und `VideoWebPlayerClient.cs` sind Windows-1252-kodiert. Editor/Tooling muss beim Editieren das Encoding beibehalten (UTF-8-Rewrite würde Umlaute zerstören); `DtoSource.cs`, `NavMenu.razor` und `MediaSourceAdminDetails.razor` sind UTF-8.
- **Menü-Aktualität:** Eine Flag-Änderung wirkt im `NavMenu` erst beim nächsten Laden (Bestandsverhalten wie bei Quell-Umbenennungen).
- **bUnit-Double:** Ohne `NoOpVideoWebPlayerClient`-Erweiterung schlägt `HttpGetAsync<DtoMediaSource[]>` mit `InvalidCastException` fehl, die `RequestSourcesAsync` zur leeren Liste schluckt — NavMenu-Filtertests brauchen die Erweiterung.

## Umsetzungsreihenfolge

1. **`MediaSource.HiddenInMenu` und `DtoMediaSource.HiddenInMenu` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Neue `bool`-Eigenschaft mit Default `false` in `MediaSource` (Windows-1252 beachten) und in `DtoMediaSource` (UTF-8). Ab hier liefert `ApiBaseController.Create<DtoMediaSource>` das Feld automatisch aus.

2. **EF-Migration `AddMediaSourceHiddenInMenu` erzeugen**
   - Voraussetzungen: Schritt 1 (Property im EF-Modell).
   - Beschreibung: `dotnet ef migrations add` nach Vorbild `AddSetupTvSenderEnabled`; `AddColumn<bool>` non-nullable mit `defaultValue: false`; Snapshot wird mitgeführt.

3. **`MediaSourceExtensions.Update` ergänzen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `HiddenInMenu` in die Kopierliste aufnehmen (Windows-1252 beachten). Sichert den Speicherpfad über `UpdateMediaSourceAsync`.

4. **Admin-UI: Checkbox in `MediaSourceAdminDetails.razor`**
   - Voraussetzungen: Schritt 1 und 3 (Property und Kopierpfad müssen existieren, sonst persistiert die Checkbox nichts).
   - Beschreibung: `InputCheckbox` mit `form-check`-Markup und `form-text`-Hilfetext (Muster: Benutzerfreigabe-Checkboxen Zeilen 127–135, Hilfetext Zeile 105); `HiddenInMenu` in der manuellen `editSource`-Kopie (Zeilen 209–223) ergänzen.

5. **Menüfilterung in `NavMenu.razor`**
   - Voraussetzungen: Schritt 1 (`DtoMediaSource.HiddenInMenu`).
   - Beschreibung: `mediaSources` in `OnInitializedAsync` gefiltert befüllen (`!s.HiddenInMenu`), sodass Überschrift und Liste auf der gefilterten Menge arbeiten.

6. **Backup-Registrierungen in `VideoWebPlayerBackupData`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `OptionalRestoreColumns` und `OptionalRestoreBoolDefaults` um `MediaSources.HiddenInMenu` (Default `false`) erweitern.

7. **Unit-/Komponententests**
   - Voraussetzungen: Schritte 1–6; für die bUnit-Tests zusätzlich die `NoOpVideoWebPlayerClient`-Erweiterung (Teil dieses Schritts).
   - Beschreibung: `Update_CopiesHiddenInMenu` in `ApplicationDbContextTests` (echtes SQLite), `GetSources`-DTO-Test in `SourceVisibilityControllerTests`, neue `NavMenuHiddenSourceTests` (bUnit), neue `VideoWebPlayerBackupDataTests_HiddenInMenu` (Alt-Backup via `LegacyBackupArchiveBuilder.RemoveColumnsAsync`).

8. **E2E-Test `MediaSourceHiddenInMenuE2ETests`**
   - Voraussetzungen: Schritte 1–5 (vollständiger Benutzerfluss Admin-Checkbox → Menü).
   - Beschreibung: Playwright-Test nach Vorbild `MediaSourceSwitchE2ETests`/`MediaSourceLocalDirectoryE2ETests` (Seed über `WebApplicationFactory` + `EnsureCreatedAsync`, Login über UI).

9. **Dokumentation**
   - Voraussetzungen: Schritte 1–6 (Dokumentiertes muss existieren).
   - Beschreibung: `docs/API.md` (`DtoMediaSource`-Beispiel unter `GET /api/Sources`), `docs/help/medienquellen/datenmodell.md` (Feld, Migration, Restore-Eintrag, DTO-Grenze — Konvention „DTO enthält bewusst nicht alles“ beachten), `einrichtung-anwender.md`/`beschreibung.md` (Admin-Anleitung), `docs/RELEASE_NOTES.md`; `README.md` nur bei Bedarf.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Update_CopiesHiddenInMenu` | `ApplicationDbContextTests` (echtes SQLite, Vorbild `Update_CopiesSourceType` Zeile 598) | `MediaSourceExtensions.Update` überträgt `HiddenInMenu`; nach Reload per `SaveChangesAsync`/`FindAsync` steht der Wert in der DB. |
| `GetSources_ReturnsHiddenInMenuFlag` (Arbeitsname) | `SourceVisibilityControllerTests` | `GetSources` liefert `HiddenInMenu` im `DtoMediaSource` aus; versteckte Quelle bleibt in der Liste enthalten (clientseitige Filterung ist UI-Aufgabe), `GetSource(id)` liefert das Flag ebenfalls. |
| `NavMenu_HidesSourceEntry_WhenHiddenInMenu` / `NavMenu_ShowsSourceEntry_WhenNotHidden` / `NavMenu_HidesSourcesSection_WhenAllSourcesHidden` / `NavMenu_HidesHiddenSource_ForAdmin` | Neue Klasse `VideoWebPlayer.Tests/Components/NavMenuHiddenSourceTests` (bUnit, Aufbau wie `NavMenuLiveTvTests`/`NavMenuPlaylistsTests`) | Filterung im `NavMenu`: versteckte Quelle erzeugt keinen `a.nav-link` auf `/mediasource/{id}`; sichtbare bleibt; Überschrift „Quellen“ verschwindet, wenn alle Quellen versteckt sind; Admin erhält dieselbe Filterung. |
| `ReadFromAsync_LegacyBackupWithoutHiddenInMenuColumn_RestoresDefaultFalse` | Neue Klasse `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_HiddenInMenu` (Vorbild `..._TvSenderEnabled`) | Alt-Backup ohne `MediaSources.HiddenInMenu` restored fehlerfrei; Seed belegt eine `MediaSource` mit `HiddenInMenu = true`, nach `RemoveColumnsAsync` + Restore muss der Wert `false` sein. `CreateSeededBackupAsync` seedet keine `MediaSources`-Zeile — der Test muss sie selbst hinzufügen. |
| `sources`-Parameter | `NoOpVideoWebPlayerClient` (Helper) | Stellt `DtoMediaSource[]` für bUnit-`NavMenu`-Tests bereit. |

### Betroffene bestehende Tests

Keine — die Migration ist additiv (`defaultValue: false`), `NavMenu`-Verhalten für sichtbare Quellen unverändert, bestehende bUnit-Tests erhalten weiterhin eine leere Quellenliste (nun über echtes leeres Array statt abgefangener `InvalidCastException`).

### E2E-Tests (primärer Funktionsnachweis)

Die Menüfilterung ist zwar über bUnit direkt testbar (`NavMenuLiveTvTests`/`NavMenuPlaylistsTests` existieren), doch der eigentliche Benutzerfluss — Admin setzt die Checkbox in `MediaSourceAdminDetails.razor`, speichert, angemeldete Benutzer sehen die Quelle nicht mehr im Menü, Direktaufruf funktioniert weiterhin — braucht E2E-Abdeckung, weil er EditForm-Bindung, `UpdateMediaSourceAsync`, EF-Persistenz und das NavMenu-Rendering im echten Circuit zusammenführt.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin öffnet `/admin/mediasources/{id}`, setzt „Im Menü ausblenden“, speichert; nach Reload der Admin-Seite ist die Checkbox gesetzt (Persistenz) und der Menüeintrag des Admins verschwindet | Neu: `VideoWebPlayer.Tests/MediaSourceHiddenInMenuE2ETests.cs` | Merkmal ist über die Admin-UI pflegbar und unterdrückt den Menüeintrag auch für Admins | Prüft den vollständigen Schreibpfad inkl. manueller `editSource`-Kopie und `MediaSourceExtensions.Update` — die beiden dokumentierten Stolperstellen sind nur im Gesamtablauf nachweisbar. |
| Pflicht | Nicht-Admin-Benutzer mit Freigabe auf versteckte und sichtbare Quelle sieht nur die sichtbare im `NavMenu`; die Überschrift „Quellen“ bleibt | `MediaSourceHiddenInMenuE2ETests` | Sichtbarkeitsregel gilt für Nicht-Admins | Echter Render-Pfad mit `RequestSourcesAsync` über HTTP. |
| Pflicht | Direktaufruf `/mediasource/{id}` einer versteckten, freigegebenen Quelle bleibt erreichbar | `MediaSourceHiddenInMenuE2ETests` | Merkmal ist rein kosmetisch, kein Zugriffsschutz | Stellt sicher, dass die Filterung nicht versehentlich in `GetSource`/Routing eingreift. |
| Empfohlen | Sind alle freigegebenen Quellen versteckt, verschwindet die Sektion „Quellen“ im Menü | `MediaSourceHiddenInMenuE2ETests` (alternativ bUnit-abgedeckt) | Überschrift hängt an der gefilterten Menge | Im bUnit-Test bereits primär abgedeckt; E2E dient als Absicherung des realen Rendering. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | `MediaSourceSwitchE2ETests`, `UnlockedSourceE2ETests`, `MediaSourceLocalDirectoryE2ETests` u. a. arbeiten mit `HiddenInMenu = false` (Default) — Verhalten unverändert. |

## Offene Punkte

Keine — die Fragen aus der Anforderung (Filterort, Direktaufruf-Hinweis, Admin-Badge, Benennung, Menü-Refresh) sind in „Designentscheidungen“ beantwortet.
