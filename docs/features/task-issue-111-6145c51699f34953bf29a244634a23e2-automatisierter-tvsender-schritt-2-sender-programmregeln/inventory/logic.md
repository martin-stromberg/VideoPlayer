# Logik — Bestandsaufnahme

## `ProgramSettingsService`
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs` — scoped (`AddScoped` in `ServiceCollectionExtensions`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateSetupAsync` | public | Liefert die einzige `Setup`-Zeile, legt sie mit Defaults an |
| `GetScanIntervalsAsync` | public | Scan-Intervalle mit Fallback-Defaults |
| `UpdateScanIntervalsAsync` | public | Persistiert Scan-Intervalle (min 1) |
| `GetApplicationTitleAsync` | public | Anwendungstitel mit Fallback |
| `GetContinueWatchingEndThresholdAsync` | public | Weiterschauen-Schwelle |
| `UpdateGeneralSettingsAsync` | public | Atomares Update der allgemeinen Einstellungen (`GeneralSettingsUpdate`-Record), inkl. Validierung via `DiscoveryUrlRules` |
| `GetDiscoveryPublicBaseUrlAsync` | public | Liest `Setup.DiscoveryPublicBaseUrl` normalisiert |
| `GetMdnsAdvertisementEnabledAsync` / `UpdateMdnsAdvertisementEnabledAsync` | public | Muster für einzelne Einstellung (Get/Update) |

Muster für neue Einstellung `HolidayFederalState`: Get-/Update-Paar analog `Get/UpdateMdnsAdvertisementEnabledAsync`.

## Admin-Berechtigungsmuster

### Blazor-Seiten (`Components/Pages/Admin/*.razor`)
- `@rendermode InteractiveServer`; `AuthenticationStateProvider` → `auth.User.HasClaim("IsAdmin", "True")`; Nicht-Admin: `<div class="alert alert-danger">Nicht autorisiert.</div>` und **keine** Datenaktionen.
- `GenreAdmin.razor` (`/admin/genres`): Admin-Karten/-Tabellen-Layout (`admin-console`, `admin-card`, `admin-dialog-overlay`/`admin-dialog`), Edit-Dialog-Muster, direkter `ApplicationDbContext`-Zugriff + `IGenreService`.
- `AdminIndex.razor` (`/admin`): Kacheln `admin-tile` zu allen Admin-Seiten — hier kommt die „TV-Sender"-Kachel hin.
- `NavMenu.razor`: „Einrichtung" (`/admin`) nur bei `IsAdmin`-Claim sichtbar.
- `ConfirmationDialog.razor` (`Components/Shared/`): wiederverwendbarer Lösch-Bestätigungsdialog (verwendet in Playlist-Komponenten).

### Controller
- `AdminSourcesController` (`api/admin/sources`): `[ApiController]`, `[BearerTokenCheck]`, erbt `ApiBaseController`; prüft `User.Identity.IsAuthenticated` + `User.HasClaim("IsAdmin", "True")` → 401/403.
- `ApiBaseController`: `CurrentUser` (über `IAuthService`), `CheckLogedIn()`, `ExecuteAsync`-Fehlermapping (404 `KeyNotFoundException`/`RecordNotFoundException`, 403 `ForbiddenAccessException`, 401 `UnauthorizedAccessException`, 400 `ArgumentException`).
- `PlaylistsController` (`api/playlists`): `GetPlaylists` (eigene), `GetPublicPlaylists`, `SetPlaylistPublic` (Admin-Status wird in `PlaylistService.SetPlaylistPublicAsync` aus dem Benutzerdatensatz gelesen — `requesterIsAdmin` aus `CurrentUser.IsAdmin`, nicht aus dem Token-Claim). Muster für serverseitige Admin-Erzwingung auf Service-Ebene.

## `ItemsController` — Medien-Such-/Auswahlendpunkte
Datei: `VideoWebPlayer/Controllers/ItemsController.cs` (`api/items`, `[BearerTokenCheck]`)

| Methode | Route | Kurzbeschreibung |
|---------|-------|------------------|
| `Get` | `GET /api/items?mediaSourceId&page&size&search&genreId&includeIndividualMediaTypes` | Namenssuche (`ApplySearchFilter` via `lower_invariant` + `EF.Functions.Like` mit Escaping); liefert `MediaEntryDto` (`Type`, `Id`, `Title`, `PictureId`, `ItemCount` u. a.); filtert auf `MediaSourceUser`-Zugriff + `UnlockedMediaEntry` des angemeldeten Benutzers; `includeIndividualMediaTypes=true` ergänzt `Movie`/`TVShowSeason`/`TVShowEpisode` zu `MovieCollection`/`TVShow` |
| `GetGenres` | `GET /api/items/genres` | `MediaMetadataEditorService.GetGenreOptionsAsync` → `List<DtoGenreOption>` (ein Eintrag je distinctem Genre-Namen, `Id`+`Name`) |
| `Get({type}/{id})` | `GET /api/items/{type}/{id}` | Detailabruf je Medium |

Client-Seite (`VideoWebPlayerClient`):
| Methode | Kurzbeschreibung |
|---------|------------------|
| `RequestItemsAsync(search, page, size, ct)` | `GET /api/items` mit `includeIndividualMediaTypes=true` — vom `MediaSearchSelector` genutzt |
| `RequestGenreOptionsAsync()` | `GET /api/items/genres` → `List<DtoGenreOption>` |
| `RequestPlaylistsAsync`-Familie (`VideoWebPlayerClient.Playlists.cs`) | `api/playlists`-Aufrufe |

## `MediaSearchSelector.razor` (Such-/Auswahldialog)
Datei: `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor`

| Element | Beschreibung |
|---------|--------------|
| `OnMediaSelected` | `EventCallback<(string MediaType, long MediaId)>` — Auswahl-Callback statt Navigation |
| `searchTerm`-Input | Debounce 400 ms, `PageSize` 30, `latestRequestId` gegen Races |
| `results` | `List<MediaEntryDto>` — Kacheln via `MediaBox` mit `data-media-type`/`data-media-id` |
| `Reset()` | öffentliche Methode zum Zurücksetzen durch die Elternkomponente |

Liefert alle fünf Medientypen gemischt; eine typbezogene Einschränkung (nur Serie / nur Film / nur Filmsammlung) ist clientseitig im Aufrufer oder durch Erweiterung der Komponente denkbar — das ist die zentrale Wiederverwendungsstelle für die Materialauswahl „keine internen IDs".

## Weitere betroffene Infrastruktur

| Artefakt | Datei | Rolle |
|----------|-------|-------|
| `ServiceCollectionExtensions.AddVideoWebPlayerServices` | `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` | DI-Registrierung (`AddScoped<IPlaylistService, PlaylistService>` u. a.) — hier kommt `ProgramRuleService` hin |
| `WebApplicationExtensions.MigrateDatabase` | `VideoWebPlayer/Extensions/WebApplicationExtensions.cs` | `db.Database.Migrate()` beim Start — neue Migrationen werden automatisch angewendet |
| `BearerTokenCheckAttribute` | `VideoWebPlayer/Controllers/Attributes/` | Token-Prüfung für API-Controller |
| `EventManager` | `VideoWebPlayer/Services/` (Singleton) | Event-Publishing-Muster des DbContext |
| `VideoWebPlayerClient` (`InternalVideoWebPlayerClient`) | `VideoWebPlayer.Client/` + interne Registrierung | In-Process-Client der Blazor-Komponenten |

## Fehlende Logik (Nachweis)

Keine Klasse `ProgramRuleService`/`ScheduleService`/Sender-Verwaltung, keine Admin-Seite `/admin/tv-sender`, keine Bundesland-Einstellung (`HolidayFederalState`) — Volltextsuche ergab keine Treffer.
