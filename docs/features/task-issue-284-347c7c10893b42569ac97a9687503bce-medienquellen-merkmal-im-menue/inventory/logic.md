# Logik — Bestandsaufnahme

Betroffene Logikklassen und Komponenten mit ihrem heutigen Stand.

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs` (Encoding: Windows-1252)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddMediaSourceAsync(MediaSource)` | `public` | Setzt `CreatedAt = DateTime.UtcNow`, fügt die Quelle hinzu, speichert, publiziert `MediaSourceCreatedEvent` (Zeilen 251–257) |
| `UpdateMediaSourceAsync(MediaSource)` | `public` | Lädt die Quelle per `FindAsync`, ruft `existingSource.Update(source)` auf (`MediaSourceExtensions.Update`), speichert, publiziert `MediaSourceUpdatedEvent` (Zeilen 263–272) |
| `DeleteMediaSourceAsync(MediaSource, IProgress<double>?, CancellationToken, Func<CancellationToken, Task>?)` | `public` | Löscht Quelle inkl. Collections/Items rekursiv (Zeile 293 ff.) |

Publizierte Events: `MediaSourceCreatedEvent`, `MediaSourceUpdatedEvent` (beide `VideoWebPlayer/Events/`, tragen die `MediaSource` in `Source`).

## `MediaSourceExtensions`
Datei: `VideoWebPlayer/Data/MediaSourceExtensions.cs` (Encoding: Windows-1252)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Update(this MediaSource target, MediaSource source)` | `public static` | Kopiert `Name`, `Path`, `Host`, `Port`, `Username`, `Password`, `SourceType`, `IconPictureId`, `LastScannedAt` — `CreatedAt` wird ausdrücklich nicht überschrieben. Ein neues Flag muss hier ergänzt werden, sonst geht es bei `UpdateMediaSourceAsync` verloren. |

## `SourcesController`
Datei: `VideoWebPlayer/Controllers/SourcesController.cs`

Erbt `ApiBaseController`; `[ApiController]`, `[Route("api/[controller]")]`, `[BearerTokenCheck]`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSources()` | `public` (`[HttpGet]`) | Quellen-IDs aus `MediaSourceUsers` (eigene `UserId`) vereinigt mit `IUnlockedMediaService.GetUnlockedSourceIdsForUserAsync`; mappt Treffer per `Create<DtoMediaSource>` (Zeilen 38–73). Keine Filterung nach einem Sichtbarkeits-Flag. |
| `GetSource(long id)` | `public` (`[HttpGet("{id:long}")]`) | Zugriffsprüfung über `MediaSourceUsers` ∪ `GetUnlockedSourceIdsForUserAsync`; liefert einzelne Quelle als `DtoMediaSource`, `403`/`404` sonst (Zeilen 80–112) |

## `ApiBaseController`
Datei: `VideoWebPlayer/Controllers/ApiBaseController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Create<T>(object ms)` | `protected static` | DTO-Mapping per Reflection: kopiert jede Property von `T` (außer `[IgnoreAssignProperty]`) von der namensgleichen Quell-Property (Zeilen 75–89). Ein neues `HiddenInMenu` auf `DtoMediaSource` würde automatisch aus `MediaSource.HiddenInMenu` befüllt. |
| `CheckLoggedIn()` / `CheckLogedIn()` | `protected` | Authentifizierungsprüfung |

## `AdminSourcesController`
Datei: `VideoWebPlayer/Controllers/AdminSourcesController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DeleteSource(long id, CancellationToken)` | `public` (`[HttpDelete("{id:long}")]`) | Einziger Endpunkt des Controllers; Admin-Claim-Prüfung serverseitig. Kein Schreibpfad für Quell-Eigenschaften über die REST-API. |

## `NavMenu` (Blazor-Komponente)
Datei: `VideoWebPlayer/Components/Layout/NavMenu.razor`

- `@code`-State: `private List<DtoMediaSource> mediaSources = new()` (Zeile 114), `tvSenderEnabled` (Zeile 115).
- `OnInitializedAsync` (Zeilen 123–146): lädt `mediaSources` einmalig über `Client.RequestSourcesAsync()` (Zeile 131) nach `EnsureAuthorizationTokenSilentlyAsync`; `tvSenderEnabled` über `ProgramSettings.IsTvSenderAccessGrantedAsync` — kein Re-Load nach Änderungen.
- Rendern der Quellen-Sektion (Zeilen 50–71): Überschrift „Quellen" nur bei `mediaSources.Any()`; danach `foreach` über **ungefilterte** `mediaSources` mit `NavLink` auf `/mediasource/{source.Id}` und optionalem Icon (`GetIconUrl`, Zeile 203).
- `IDisposable` für `LocationChanged`-Handler; Live-TV-Eintrag an `tvSenderEnabled` gekoppelt (Zeilen 41–48) — Muster für flaggesteuerte Menüeinträge.

## `MediaSourceAdminDetails` (Blazor-Komponente)
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor`

- Routen: `/admin/mediasources/new` und `/admin/mediasources/{Id:long}`; `isAdmin`-Gate in `OnInitializedAsync` (Zeilen 174–184) plus `AuthorizeView`.
- `editSource`-Kopie beim Bearbeiten (Zeilen 209–223): überträgt Felder **einzeln von Hand** (`Id`, `Name`, `Host`, `Port`, `Path`, `Username`, `Password`, `SourceType`, `IconPictureId`, `CreatedAt`, `LastScannedAt`, `MediaSourceUsers`) — ein neues Feld muss hier explizit ergänzt werden.
- Neu-Anlage: `editSource = new MediaSource { Port = 22 }` (Zeile 194).
- `Save()` (Zeilen 281–368): `AddMediaSourceAsync` / `UpdateMediaSourceAsync`, danach `MediaSourceUsers`-Zuordnung ersetzen.
- Formular enthält `InputText`/`InputNumber`/`InputSelect`/`InputFile` und `form-check`-Checkboxen nur für die Benutzerfreigaben (`checked`/`@onchange`, Zeilen 127–135) — noch **kein** `InputCheckbox` für ein Quell-Flag.
- `internal static TryValidateLocalDirectory` (Zeilen 374–411): Pfad-Validierung, von Tests direkt aufgerufen.

## `MediaSourceAdmin` (Blazor-Komponente)
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`

- Admin-Quellenliste (`/admin/mediasources`): `LoadSources` lädt **alle** `DbContext.MediaSources` inkl. `MediaSourceUsers` (Zeilen 157–164) — versteckte Quellen blieben hier sichtbar.
- `ResetScan` (Zeilen 204–209) ruft `UpdateMediaSourceAsync` mit der getrackten Entity auf — ein weiterer Pfad, der `MediaSourceExtensions.Update` durchläuft.

## `MediaSourceDetails` / `MediaSourceDetailsViewModel`
Dateien: `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor`, `VideoWebPlayer/ViewModels/MediaSourceDetailsViewModel.cs`

- Route `/mediasource/{Id:long}` — Direktaufruf einer Quelle.
- `MediaSourceDetailsViewModel.InitializeAsync` (Zeilen 36–77) lädt die Quelle über `_client.RequestSourceAsync(sourceId)` (Einzel-Endpunkt `GET /api/Sources/{id}`) und danach `RequestSourceGenresAsync`. `MediaSource` ist `DtoMediaSource?` — ein neues DTO-Feld stünde hier automatisch zur Verfügung.
- Kein Hinweis-/Sperrmechanismus für „versteckte" Quellen vorhanden.

## `VideoWebPlayerClient`
Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.cs` (Encoding: Windows-1252)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RequestSourcesAsync()` | `public` | `GET api/Sources` → `DtoMediaSource[]`; bei Fehler leeres Array (Zeilen 413–424) |
| `RequestSourceAsync(long sourceId)` | `public` | `GET api/Sources/{id}` → `DtoMediaSource?`; bei Fehler `null` (Zeilen 430–440) |

## `VideoWebPlayerBackupData`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Zeilen 23–49): optionale Tabellen beim Restore.
- `OptionalRestoreColumns` (Zeilen 51–108): optional fehlende Spalten — enthält `MediaSources.SourceType` (Zeile 81), noch **kein** `MediaSources.HiddenInMenu`.
- `OptionalRestoreBoolDefaults` (Zeilen 118–133): Defaults für fehlende bool-Spalten — Vorbild `(nameof(ApplicationDbContext.Setups), nameof(Setup.TvSenderEnabled), false)` (Zeile 132); kein `MediaSources`-Eintrag.
- `OptionalRestoreIntDefaults` (Zeilen 140–152): enthält `(MediaSources, SourceType, (int)MediaSourceType.Sftp)` (Zeile 144).
- `GetTables()` (Zeile 1152) iteriert `_db.Model.GetEntityTypes()` — neue Spalten landen in neuen Backups automatisch; nur der Alt-Backup-Pfad braucht die o. g. Registrierungen.
- `ValidatePayloadAsync` (Zeilen ~540–655): bemängelt fehlende Spalten außer `IsOptionalRestoreColumn` — ohne `OptionalRestoreColumns`-Eintrag schlägt der Restore eines Alt-Backups fehl.

## Verifikationspunkte (dürfen laut Anforderung nicht verändert werden)

- `TVSenderAdmin.razor` (Zeile 940): `mediaSources = await DbContext.MediaSources.AsNoTracking().OrderBy(s => s.Name).ToListAsync()` — versorgt u. a. die Materialauswahl `ProgramRuleMaterialType.MediaSourceClips` und `ProgramRuleMediaSource`-Filter.
- `TVSenderAdminClipGroups.razor` (Zeilen 104–112): Quellenfilter-Dropdown der Clip-Kandidaten iteriert `MediaSources`; `ReloadCandidatesAsync` ruft `ClipGroupService.GetClipCandidatesAsync` (Zeile 382).
- `ProgramClipGroupService.GetClipCandidatesAsync` (`VideoWebPlayer/Services/ProgramClipGroupService.cs`, Zeilen 116 ff.): `mediaSourceId`-Filter auf unklassifizierte `MediaItem`s.
- `MediaSourceScanner` (Zeile 57): `_db.MediaSources.ToListAsync` — scannt alle Quellen; `MediaSourceScanService` (BackgroundService) und `MediaSourceReaderDispatcher` arbeiten quelltypgesteuert, ohne Menübezug.
- `ProgramScheduleService` / `ProgramRuleService` / `ProgramRule` (`ClipGroups`, `MediaSourceFilters`): lesen `MediaSource` über den `DbContext`, kein Bezug zum Menü.
