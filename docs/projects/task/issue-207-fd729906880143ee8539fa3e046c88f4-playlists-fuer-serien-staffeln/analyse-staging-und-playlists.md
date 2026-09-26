# Analyse: Staging-Änderungen × Playlist-Feature

> **Dokumenttyp**: Analysebericht (nur Analyse, keine Änderung an Produktivcode oder Tests)
> **Branch**: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln`
> **Stand**: HEAD `ba3ad4b` (Merge von `origin/staging` `2636dff`, QR-Bootstrap #232)
> **Datum**: 2026-09-25

Untersucht wurde, ob die aus `origin/staging` übernommenen Änderungen (Ausnahmefehler-Fixes,
chunkbasierter Backup-Upload, Menü-Autorisierungs-Fix, lokale Verzeichnisse als Medienquelle #225,
Geräte-Pairing #229, QR-Bootstrap/Refresh-Tokens #232) fachlich und technisch zum Playlist-Feature
passen — mit Schwerpunkt auf der Geräte-/Client-API und der Testabdeckung.

---

## 1. Zusammenfassung (Ampel je Bereich)

| Bereich | Ampel | Kernaussage |
|---------|-------|-------------|
| Geräte-API × Playlists (Funktion) | 🟢 grün | Ein gekoppeltes Gerät kann Playlists vollständig nutzen. Empirisch nachgewiesen (Abschnitt 2.1). Keine Inkonsistenz `AnyClient`/`MauiOnly` bei Playlists. |
| Geräte-API × Playlists (Statuscodes/Client-Verhalten) | 🟡 gelb | `401` statt `403` bei fehlender Medienberechtigung trifft mit öffentlichen Playlists jetzt einen Regelfall und kollidiert mit der 401-Reauthentisierung des Clients (Befund F3). |
| Client-Bibliothek (`VideoWebPlayer.Client`) | 🟡 gelb | DTOs für Pairing/Bootstrap/Refresh vorhanden, aber **keine** Client-Methoden; `InternalVideoWebPlayerClient` überschreibt PUT/PATCH/DELETE/GET-mit-CT und die Playlist-Navigation nicht (F4, F5). |
| Migrationskette (leere und bestehende DB) | 🟢 grün | Fresh-Install und Upgrade einer simulierten Staging-Datenbank erfolgreich geprüft, Schema identisch, Daten erhalten, keine FK-Verletzung (Abschnitt 4.1). |
| Backup/Restore × neue Tabellen | 🔴 rot | **Alt-Backups lassen sich nicht mehr wiederherstellen** (`PairedDevices`/`PairingCodes`/`RefreshTokens` fehlen in `OptionalRestoreTables`), Release Notes behaupten das Gegenteil (F1). |
| Lokale Verzeichnisse × Playlists (Code) | 🟢 grün | Backfill-Marker, Zugänglichkeit, Cover-Collage, Quellenlöschung sind durchgängig quelltyp-unabhängig. |
| Lokale Verzeichnisse × Playlists (Tests) | 🟡 gelb | Kein einziger Test kombiniert lokale Quellen mit Playlists (F7). |
| Testlage gesamt | 🟡 gelb | 1500 Tests, 3 Fehlschläge, alle als flaky nachgewiesen; zwei Playlist-E2E-Tests flackern auch einzeln (F8). Keine Kombinationstests Gerät × Playlist (F6). |
| Dokumentation `docs/API.md` | 🔴 rot | 17 Playlist-Endpunkte fehlen, darunter der komplette Wiedergabe- und Cover-Teil, den eine Client-App braucht (F2). |
| Dokumentation `docs/help/geraete/*` | 🟡 gelb | Stand vor #232: Bootstrap, Refresh-Tokens und Self-Service-Kopplung fehlen in Beschreibung, Ablauf, API, Datenmodell, Architektur und Business Rules (F9). |
| Release Notes EN/DE | 🟡 gelb | Deutscher Teil fehlen 8 Einträge (gesamtes QR-Bootstrap #232) gegenüber dem englischen (F10). |
| Menü / statische Assets / Kestrel / web.config | 🟢 grün | Playlist-Menüeintrag hinter `AuthorizeView`, `playlistDragDrop.js` über `StaticAssetVersioner`, `Kestrel:Limits:MaxRequestBodySize = 0` → `null` (unbegrenzt), Cover-Upload-Grenze wird im Controller erzwungen. |

**Vor dem Push zu beheben:** F1 (Restore von Alt-Backups), F2 (API-Dokumentation, weil der Vertragstest
sie als verbindlich behauptet), F10 (deutsche Release Notes — Migrationshinweis fehlt dem Betreiber).
Alles Übrige ist als eigener Lauf planbar.

---

## 2. Befunde je Frage

### 2.1 Geräte-API und Playlists

**Endpunkte und Attribute.** `PlaylistsController` trägt auf Klassenebene ausschließlich
`[BearerTokenCheck]` und **kein** `[ApiTokenCheck]`
(`VideoWebPlayer/Controllers/PlaylistsController.cs:17-20`). Das ist konsistent mit allen anderen
Medien-Controllern: `ItemsController.cs:13-14`, `PicturesController.cs:13-15`,
`ContinueWatchingController.cs:14-16`, `FavoritesController.cs:11-12`, `SourcesController.cs:13-14`,
`EpisodesController.cs:14-15`, `ActorsController.cs:15-16`, `SourceGenresController.cs:17-18`,
`SourceIconsController.cs:12-13`, `UnlockedMediaController.cs:14-15`, `AdminSourcesController.cs:12-13`.
`[ApiTokenCheck]` steht ausschließlich im `AuthController`: `login`, `refresh`, `logout` mit
`ApiTokenScope.MauiOnly` (`AuthController.cs:58,98,127`), `impersonate` mit `AnyClient`
(`AuthController.cs:78`).

→ **Es gibt keine Inkonsistenz `AnyClient` vs. `MauiOnly` bei den Playlist-Endpunkten.** Das
`X-API-Key`-Gate ist bewusst nur ein Türsteher für die Sitzungsausstellung; danach zählt allein das
Benutzer-JWT. Die Playlist-Endpunkte verhalten sich exakt wie der Rest der Medien-API. Ein Endpunkt,
der für Geräte gesperrt sein müsste, es aber nicht ist, wurde nicht gefunden.

**Authentifizierung der Geräte.** Ein Geräte-Token ist **nicht** an einen Benutzer gebunden
(`DeviceTokenService.IssueAsync`, `VideoWebPlayer/Services/DeviceTokenService.cs`: `PairedDevice` hat
nur `CreatedByUserId` als Herkunftsvermerk, keine Sitzungsbindung). Der Benutzerkontext kommt
ausschließlich aus dem JWT:

- klassischer Weg: `POST /api/pairing/exchange` → Geräte-Token → `POST /api/auth/login` mit
  `X-API-Key: <Geräte-Token>` und Benutzer-Zugangsdaten → JWT (12 h, `AuthorizationTokenService.CreateToken`);
- QR-Bootstrap: `POST /api/pairing/bootstrap` → verschlüsseltes Trio aus Geräte-Token, JWT **des
  Ticket-Erstellers** und Refresh-Token (`PairingBootstrapService`).

→ Die Playlist-Besitz-/Öffentlich-Logik gilt damit für **den JWT-Benutzer**, nicht für „das Gerät".
Bei einem per QR gekoppelten Gerät sieht man also die Playlists desjenigen, der das Ticket erzeugt hat.
Ein Benutzerwechsel auf dem Gerät ist über `POST /api/auth/login` mit dem Geräte-Token möglich.

**Empirischer Nachweis (eigenes Prüfprogramm, Worktree, nicht committet).** Ablauf: Benutzer + lokale
Medienquelle + zwei Filme angelegt, Bootstrap-Ticket erzeugt, `POST /api/pairing/bootstrap` eingelöst,
danach ausschließlich mit dem Bootstrap-JWT gearbeitet:

```
bootstrap: 200
POST /api/playlists (Bearer, kein X-API-Key): 200
POST entries Film A: 200
POST entries Film B: 200
GET /api/playlists: 200
POST /play: 200  (currentEntryId=1, streamUrl, currentPosition=1/2)
POST /play/next: 200
GET /cover: 404 (kein Cover gesetzt — erwartet)
POST continue-watching/progress mit playlistId: 204
GET /cover?access_token=<JWT>: 404 (kein Cover — Authentifizierung greift)
GET /api/playlists?access_token=<JWT>: 200
NACH WIDERRUF DES GERÄTS: GET /api/playlists mit altem JWT: 200
NACH WIDERRUF DES GERÄTS: POST /play mit altem JWT: 200
NACH WIDERRUF DES GERÄTS: POST /api/auth/refresh: 401
```

Daraus folgt belegt:

- Geräte/Clients können Playlists **lesen, anlegen, befüllen, abspielen** (`play`, `play/next`,
  `play/previous`, `play/advance`) und **Weiterschauen mit `playlistId` melden** — ohne zusätzlichen
  `X-API-Key`.
- Das `access_token`-Query-Verfahren (`BearerTokenCheckAttribute.cs:31-34`) funktioniert **für alle**
  Playlist-Endpunkte, nicht nur für Bilder/Streams. Für Playlist-Cover (`GET /api/playlists/{id}/cover`)
  und `GET /api/pictures/{id}` ist das der Weg, den ein `<img>`-Tag im Client nutzen kann. Der Schutz des
  Covers ist doppelt implementiert (`PlaylistsController.cs:673-686` und `PicturesController.cs:55-62`,
  Letzteres gegen Erraten der Picture-Id).
- **Geräte-Widerruf entzieht den Playlist-Zugriff nicht sofort**: das bereits ausgestellte JWT bleibt bis
  zu 12 Stunden gültig; nur `refresh` schlägt fehl (`DeviceTokenService.RevokeAsync` ruft
  `RevokeAllForDeviceAsync`, ausgestellte JWTs sind davon unberührt). Das ist bewusst so und in
  `docs/API.md:20` sowie `docs/help/geraete/business-rules.md` („Widerruf") dokumentiert — **aber nur
  allgemein für „Benutzer-JWTs", nicht mit Bezug auf Playlists.** Schweregrad: Hinweis.
- Refresh-Rotation mit Reuse-Detection (`RefreshTokenService.RotateAsync`) wirkt korrekt, ist aber vom
  Playlist-Feature vollständig entkoppelt.

**Vertragstest.** `VideoWebPlayer.Tests/ApiDocumentationContractTests.cs:50-71` prüft 19 Pflichtrouten.
Darunter ist **keine einzige Playlist-Route**, und auch die Staging-Neuzugänge
`POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout` fehlen, obwohl sie in
`docs/API.md` stehen. Der Laufzeitteil (`MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds`)
endet bei `GET /api/items`. Schweregrad: **Lücke**.

**Client-Bibliothek.** In `VideoWebPlayer.Client/` gibt es die DTOs `PairingExchangeRequest/Response`,
`PairingBootstrapRequest/Response/Payload`, `RefreshTokenRequest/Response` — aber **keine einzige
Client-Methode** dafür: `grep -n "refresh|Refresh|pairing|Pairing|bootstrap|X-API-Key|DeviceToken"`
über `VideoWebPlayer.Client/*.cs` liefert nur zwei Logmeldungen
(`VideoWebPlayerClient.cs:89` und `:886`). `VideoWebPlayerClient.HandleUnauthorized()`
(`VideoWebPlayerClient.cs:58-61`) gibt in der Basisklasse immer `false` zurück; es gibt weder eine
Methode zum Setzen des `X-API-Key`-Headers noch eine automatische Token-Erneuerung per Refresh-Token.
Eine MAUI-App muss Pairing, Bootstrap, Refresh-Rotation und Logout also vollständig selbst bauen,
obwohl sie die Playlist-Aufrufe aus `IPlaylistApiClient` übernehmen kann. Schweregrad: **Lücke** (F4).

### 2.2 Lokale Verzeichnisse als Medienquelle × Playlists

Ergebnis: **Der Code ist durchgängig quelltyp-unabhängig; die Kombination funktioniert.** Belege:

- **Backfill-Marker:** Der Hook sitzt in `ApplicationDbContext.SaveChanges/SaveChangesAsync`
  (`VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs:41-80`), also unterhalb jeder
  Erfassungsquelle. `MediaSourceClassifier` legt Medien per EF an — lokale Quellen ebenso wie SFTP.
  Die einzige dokumentierte Grenze (Roh-SQL/Bulk) betrifft beide Quelltypen gleich.
- **Scan-Signal:** `IPlaylistBackfillSignal.BeginScan()` umschließt alle drei Scanpfade:
  Hintergrunddienst (`MediaSourceScanService.cs:101`), manueller Komplettscan
  (`Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor:230`) und „Neu erfassen" im Explorer
  (`Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor:262`). Kein Pfad fehlt.
- **Zugänglichkeit:** `PlaylistEntryAccessResolver` arbeitet über `MediaSourceId` und
  `MediaSourceUsers`/`UnlockedMediaEntries` (`VideoWebPlayer/Services/PlaylistEntryAccessResolver.cs`),
  kennt `MediaSourceType` nicht.
- **Quellenlöschung:** Es gibt genau einen Löschpfad (`AdminSourcesController.cs:45-67` →
  `ApplicationDbContext.DeleteMediaSourceAsync`), der vor dem Löschen der Weiterschauen-Einträge den
  Playlist-Ersatz auflöst. `DeleteMediaSourceAsync` löscht **keine** `PlaylistEntries` (verifiziert:
  0 Treffer für `PlaylistEntries` in `ApplicationDbContext.cs:242-440`); die verwaisten Einträge werden
  beim nächsten Lesen still bereinigt — so in `docs/API.md` beschrieben.
- **Cover-Collage:** `PlaylistCoverImageGenerator` liest ausschließlich aus `Pictures`/`Movies`/
  `TVShows`/… (`VideoWebPlayer/Services/PlaylistCover/PlaylistCoverImageGenerator.cs:115-263`), nie vom
  Dateisystem — quelltyp-unabhängig.
- **Streaming aus der Playlist:** `DtoPlaylistPlaybackStart.StreamUrl` zeigt auf
  `GET /api/items/{type}/{id}/stream`; dort wird über `IMediaSourceReader` (Dispatcher) gelesen, der
  `LocalMediaSourceReader` und `SftpMediaSourceReader` kapselt.

**Backup/Restore.** `MediaSources.SourceType` steht korrekt in `OptionalRestoreColumns` und
`OptionalRestoreIntDefaults` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs:68,102`) und
ist mit `ReadFromAsync_LegacyBackupWithoutMediaSourceSourceType_RestoresWithSftpDefault`
(`VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs:1247`) abgesichert. Alle
`Playlist*`-Tabellen und -Spalten sind ebenfalls eingetragen (`VideoWebPlayerBackupData.cs:31-35,61-68`)
und einzeln regressionsgetestet.

**→ Aber: `PairedDevices`, `PairingCodes` und `RefreshTokens` fehlen** — siehe F1. Der chunkbasierte
Upload selbst ist von Playlist-Daten unabhängig (er transportiert nur die `.bak`-Datei); das Problem
liegt allein in der Restore-Validierung.

### 2.3 Weitere Wechselwirkungen

- **Menü:** Der Playlist-Eintrag liegt innerhalb `<AuthorizeView><Authorized>`
  (`VideoWebPlayer/Components/Layout/NavMenu.razor:34-38`); der Staging-Fix ruft
  `Client.EnsureAuthorizationTokenAsync(state.User)` nur für authentifizierte Benutzer
  (`NavMenu.razor:115-120`). Beides passt zusammen. **Nebeneffekt:** Weil `NavMenu` im Layout liegt,
  ist es dieser Aufruf, der das Token des scoped `VideoWebPlayerClient` im Circuit setzt — davon hängen
  indirekt alle Playlist-Schreiboperationen ab (siehe F5).
- **`App.razor` / `StaticAssetVersioner`:** `playlistDragDrop.js` und `backupUpload.js` werden beide
  über `@Assets.Url(...)` eingebunden (`VideoWebPlayer/Components/App.razor:14-20`); die Fingerabdruck-
  Logik ist mit `VideoWebPlayer.Tests/Services/StaticAssetVersionerTests.cs` abgedeckt. Kein Konflikt.
- **Benutzerlöschung:** `UserManagement.razor:255-258` löst vor dem Löschen eines Kontos die
  Weiterschauen-Konflikte fremder Benutzer an dessen (öffentlichen) Playlists auf. Für `PairedDevices`
  und `RefreshTokens` gibt es keine Fremdschlüssel (siehe Migration
  `20260924103134_AddPairingBootstrapAndRefreshTokens.cs:28-45`), also auch keinen Kaskadenkonflikt —
  Refresh nach Benutzerlöschung schlägt mit `401` fehl (`AuthController.cs:108-110`). Unkritisch.
- **Kestrel / `web.config` / Produktion:** `appsettings.Production.json` setzt
  `Kestrel:Limits:MaxRequestBodySize = 0`; `KestrelLimits.ParseMaxRequestBodySize("0")` liefert `null`
  = unbegrenzt (`VideoWebPlayer/Extensions/KestrelLimits.cs`, Test `KestrelLimitsTests.cs:15-18`).
  Der Playlist-Cover-Upload ist damit nicht durch Kestrel begrenzt, sondern durch
  `PlaylistSettings.MaxCoverImageSizeBytes` im Controller (`PlaylistsController.cs:600-601`) — bewusst
  so dokumentiert. `web.config` hebt `maxAllowedContentLength` auf 4 GiB. Konsistent.
- **Sicherheit/Logging:** Alle Playlist-Endpunkte laufen durch eine zentrale Ausnahmeabbildung
  (`PlaylistsController.ExecuteAsync`, `PlaylistsController.cs:52-116`) und liefern für fremde
  Playlists sauber `403` (empirisch bestätigt). Der `ItemsController` dagegen hat diese Abbildung
  nicht — siehe F3.
- **`docs/help/index.md`:** enthält Playlists, Geräte und Medienquellen; keine Lücke.

### 2.4 Testlage

Siehe Abschnitt 3 (Läufe) und die Befunde F6–F8.

### 2.5 Dokumentation

Siehe Befunde F2, F9, F10.

---

## 3. Was ausgeführt wurde

| Lauf | Befehl | Ergebnis |
|------|--------|----------|
| Release-Build | `dotnet build VideoPlayer.sln -c Release` | **0 Fehler, 0 Warnungen** |
| Volle Testsuite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build` | **1500 Tests, 1497 grün, 3 rot**, 4 min 13 s |
| Wiederholung der 3 Fehlschläge | je 3 Einzelläufe | alle drei **flaky**, siehe F8 |
| Modell-/Snapshot-Abgleich | `dotnet ef migrations has-pending-model-changes` | „No changes have been made to the model since the last migration." |
| Migration gegen leere DB | `dotnet ef database update` auf frische SQLite-Datei | **Done.** (nur die bekannten `PRAGMA foreign_keys`-Warnungen) |
| Migration gegen bestehende DB | Worktree auf `origin/staging` → Migrationen anwenden → Testdaten (lokale Quelle mit `SourceType=1`, Benutzer, `PairedDevice`, `RefreshToken`) einsetzen → auf `HEAD` wechseln → `dotnet ef database update` | **Done.**; Schemavergleich gegen die frische DB: 0 fehlende Objekte, 0 überzählige Objekte, 0 abweichende `CREATE`-Statements; `pragma foreign_key_check` leer; alle Testdaten unverändert vorhanden |
| Geräte-/Playlist-Probe | temporäre xUnit-Klasse im Worktree (`WebApplicationFactory`, Bootstrap-Flow, Playlist-API, Widerruf) | Protokoll in Abschnitt 2.1 |
| Statuscode-Probe | temporäre xUnit-Klasse im Worktree (fremde/unbekannte Medien, öffentliche Playlist) | Protokoll in F3 |
| Alt-Backup-Probe | temporäre xUnit-Klasse im Worktree (Tabelle aus `index.json` entfernen, `ReadFromAsync`) | Protokoll in F1 |

Alle temporären Prüfklassen lagen in eigenen Git-Worktrees außerhalb des Arbeitsverzeichnisses und
wurden nach den Läufen samt Worktrees entfernt (`git worktree list` zeigt nur noch das Hauptverzeichnis;
`git status` nur die fremde Datei `docs/features/task/customer-feedback.md`).

**Nicht geprüft:**

- Die eigentliche MAUI-/TV-App (liegt in einem anderen Repository) — alle Aussagen dazu stammen aus
  `VideoWebPlayer.Client` und der API-Dokumentation.
- Ein echter Wiedergabe-Durchlauf einer Playlist von einem gekoppelten Gerät mit echten Videodateien
  (die Probe hatte keine `MediaItem`-Dateien; das Streaming selbst ist über
  `LocalMediaSourceStreamingE2ETests` separat abgedeckt).
- Die Produktionsumgebung Stromberg selbst (IIS, Reverse Proxy, tatsächliche `Data`-Datenbank).
- Ein echter Restore-Durchlauf mit einer produktiven `.bak`-Datei — F1 wurde mit einem aus dem
  aktuellen Schema abgeleiteten Alt-Backup nachgestellt.
- Lastverhalten des Backfills bei großen lokalen Verzeichnissen.
- Der UDP-Discovery-Pfad (`UdpDiscoveryListener`) im Zusammenspiel mit Pairing.

---

## 4. Befunde im Einzelnen

### 4.1 Migrationskette — kein Befund (positiv belegt)

Die Playlist-Migrationen (`20260905…` bis `20260920165219_AddPlaylistBackfillMarkers`) tragen
**frühere** Zeitstempel als die Staging-Migrationen (`20260922180232_AddMediaSourceSourceType`,
`20260923074815_AddDevicePairing`, `20260924103134_AddPairingBootstrapAndRefreshTokens`). Auf einer
bestehenden Staging-Datenbank werden sie also **nachträglich, außer der Reihe** angewendet. Das wurde
ausdrücklich ausprobiert (Abschnitt 3) und ist unkritisch: keine der Playlist-Migrationen baut eine
Tabelle neu auf, die eine Staging-Migration verändert hat (`AddPlaylistIdToContinueWatchingEntry` und
`AddPlaylistCoverFields` betreffen `ContinueWatchingEntries` bzw. `Pictures`, nicht `MediaSources`).
Das Endschema ist identisch zu einer Neuinstallation.

### F1 — 🔴 **Fehler (vor dem Push beheben):** Alt-Backups lassen sich nicht mehr wiederherstellen

`OptionalRestoreTables` in `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs:23-36` enthält
die drei neuen Tabellen **nicht**, obwohl sie EF-Entitäten sind
(`ApplicationDbContext.cs:185,189,193`) und damit von `GetTables()` (`VideoWebPlayerBackupData.cs:1057`)
als Pflichttabellen erwartet werden (`VideoWebPlayerBackupData.cs:455-463`).

Nachgestellt mit einer temporären Prüfklasse (Alt-Backup = aktuelles Backup ohne den jeweiligen
`index.json`-Eintrag und ohne dessen Datendatei, analog zu den bestehenden
`BuildLegacyBackupStreamRemovingTablesAsync`-Tests):

```
PROBE [PairedDevices] -> InvalidDataException: Tabelle PairedDevices fehlt.
PROBE [PairingCodes]  -> InvalidDataException: Tabelle PairingCodes fehlt.
PROBE [RefreshTokens] -> InvalidDataException: Tabelle RefreshTokens fehlt.
PROBE [Playlists]     -> OK (restore erfolgreich)
```

Wirkung: Jede Datensicherung, die **vor** #229 bzw. #232 erzeugt wurde — also jedes Backup einer
produktiven Installation vor dem 23./24.09. — wird beim Wiederherstellen mit
`InvalidDataException` abgelehnt. Genau das ist der Fall, den ein Betreiber im Störungsfall braucht.

Verschärfend:

- `docs/RELEASE_NOTES.md:5` behauptet das Gegenteil: „Backups from older versions remain restorable
  because the new tables and columns are optional during restore." (deutsch sinngemäß in Zeile 68).
- `AGENTS.md` §7 („Definition of Done") verlangt ausdrücklich: „Neue Tabellen und Spalten stehen in
  `VideoWebPlayerBackupData` (`OptionalRestoreTables` bzw. `OptionalRestoreColumns`) samt
  Regressionstest, der ein Alt-Backup ohne sie wiederherstellt."
- In `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs` existiert für jede
  Playlist-Tabelle und für `MediaSources.SourceType` ein solcher Test — für die drei Geräte-Tabellen
  keiner.

Ursache liegt in `origin/staging` (#229, #232), nicht im Playlist-Feature. Sie geht aber mit diesem
Branch weiter und würde mit dem Push in den Zielzweig gelangen. **Behebung vor dem Push empfohlen**:
drei Einträge in `OptionalRestoreTables` plus drei Regressionstests.

### F2 — 🔴 **Fehler (vor dem Push beheben):** 17 Playlist-Endpunkte fehlen in `docs/API.md`

`docs/API.md` dokumentiert im Abschnitt „Playlists" (Zeilen 475–643) nur die Verwaltung bis
`GET /api/playlists/{id}/entries/paged`. Geprüft per Abgleich gegen `PlaylistsController`:

| fehlt in `docs/API.md` |
|---|
| `POST /api/playlists/{id}/play`, `/play/next`, `/play/previous`, `/play/advance` |
| `GET`/`DELETE /api/playlists/{id}/cover`, `POST .../cover/upload`, `.../cover/regenerate`, `.../cover/preview` |
| `PATCH /api/playlists/{id}/sort-mode` |
| `PUT /api/playlists/{id}/genres`, `POST /api/playlists/{id}/genres/reset` |
| `PUT /api/playlists/{id}/entries/{entryId}/order`, `POST .../entries/batch-reorder`, `GET .../entries/max-sort-order`, `POST .../entries/{entryId}/move-to-beginning`, `POST .../entries/{entryId}/move-between` |

Das trifft ausgerechnet den Teil, den eine Client-App braucht: Wiedergabe und Cover. `docs/API.md:1-8`
bezeichnet sich als „versionierter API-Vertrag des Web-Repositorys" und nennt am Ende ausdrücklich den
Vertragstest als Absicherung — der Vertrag ist damit unvollständig, obwohl er sich als vollständig
ausgibt. Ergänzend ist der Kopfstand veraltet („Letzte Aktualisierung: 2026-08-25", `docs/API.md:6`),
obwohl der Pairing- und Bootstrap-Teil aus September stammt.

`docs/help/playlists-api.md` beschreibt die Endpunkte und die Berechtigungsmatrix dagegen vollständig;
es fehlt dort nur der Hinweis auf das `access_token`-Query-Verfahren. Der Widerspruch besteht also
zwischen den beiden Dokumenten.

Schweregrad „Fehler" statt „Lücke", weil ein externer Integrator `docs/API.md` als verbindlich lesen
soll und der Vertragscheck diesen Anspruch bestätigt. Der Aufwand ist gering (Dokumentation, kein Code).

### F3 — 🟡 Lücke: `401` statt `403` bei fehlender Medienberechtigung kollidiert mit der Client-Reauthentisierung

`ItemsController.EnsureAccessAsync` wirft bei fehlendem Zugriff `UnauthorizedAccessException`
(`ItemsController.cs:530-533`), was in den Endpunkten auf `Unauthorized(...)` = **401** abgebildet wird
(`ItemsController.cs:754-758`, `ItemsController.cs:623-627`). `docs/API.md:29-30` schreibt dagegen
`403 Forbidden` für „Benutzer ist angemeldet, besitzt aber keinen Zugriff auf die Quelle oder das Bild"
und `401` nur für fehlendes/ungültiges Token.

Zusätzlich wird `RecordNotFoundException` nirgends auf `404` abgebildet, sondern fällt in den generischen
`catch (Exception)` → **500**.

Empirisch (temporäre Prüfklasse, `WebApplicationFactory`):

```
Besitzer GET /api/items/movie/1                       : 200
Besitzer GET /api/items/movie/999999 (unbekannt)      : 500   [Doku: 404]
Besitzer GET /api/items/movie/1/stream (kein MediaItem): 500  [Doku: 404]
Fremder  GET /api/playlists/1 (öffentlich)            : 200
Fremder  POST /api/playlists/1/play (nichts zugänglich): 400  "Diese Playlist enthält keine abspielbaren Einträge."
Fremder  GET /api/items/movie/1 (kein Quellenzugriff)  : 401  [Doku: 403]
Fremder  GET /api/items/movie/1/stream (kein Zugriff)  : 401  [Doku: 403]
Fremder  DELETE /api/playlists/1 (nicht Besitzer)      : 403  [erwartet 403]
```

**Warum das durch Playlists relevant wird:** Öffentliche Playlists zeigen für fremde Anwender
ausdrücklich auch Titel, für die sie keine Freischaltung haben (gedimmt dargestellt). Ein Client, der so
einen Titel dennoch anfragt — versehentlich, per Deep Link oder weil der Zugriff zwischenzeitlich
entzogen wurde — bekommt `401`. Die generische Client-Logik
`VideoWebPlayerClient.SendWithReauthorizationAsync` (`VideoWebPlayer.Client/VideoWebPlayerClient.cs:66-94`)
interpretiert jedes `401` als abgelaufenes Token, ruft `HandleUnauthorized()` und wiederholt die
Anfrage. Für eine Geräte-App mit Refresh-Token bedeutet das: unnötige Token-Rotation bei jedem
Berechtigungsfehler, danach erneut `401` und eine irreführende Fehlermeldung („Unauthorized"), statt
„Für diesen Titel fehlt Ihnen die Freischaltung".

Die Playlist-Ebene selbst macht es richtig (`PlaylistsController.ExecuteAsync` bildet
`PlaylistAccessDeniedException` auf `Forbid` ab, `PlaylistsController.cs:71-75`). Die Inkonsistenz
liegt im älteren `ItemsController` und wird durch das neue Feature erst zum Regelfall.

### F4 — 🟡 Lücke: `VideoWebPlayer.Client` kennt weder Pairing noch Bootstrap noch Refresh

Siehe Beleg in 2.1. Konkret fehlen in `VideoWebPlayerClient`:

- Setzen des `X-API-Key`-Headers (Geräte-Token),
- `PairingExchangeAsync` / `PairingBootstrapAsync` (die DTOs liegen bereits in
  `VideoWebPlayer.Client/Models/`),
- `RefreshAsync` / `LogoutAsync`,
- eine Überschreibung von `HandleUnauthorized()`, die bei `401` den Refresh-Token einlöst und die
  Anfrage wiederholt — genau der Aufhänger, den `SendWithReauthorizationAsync` bereitstellt.

Dadurch kann eine Client-App die Playlist-Aufrufe aus `IPlaylistApiClient` zwar übernehmen, muss aber
die gesamte Sitzungsverwaltung danebenbauen. In Verbindung mit F3 wird ein 401-Handler dort besonders
fehleranfällig.

### F5 — 🟡 Lücke: `InternalVideoWebPlayerClient` deckt PUT/PATCH/DELETE und die Playlist-Navigation nicht ab

`InternalVideoWebPlayerClient` überschreibt nur drei Methoden, um bei fehlendem Token zu impersonieren:
`HttpGetAsync<T>(string)` (`InternalVideoWebPlayerClient.cs:55`), `HttpPostAsync<T>` (`:69`) und
`HttpPostAsync` (`:82`). Nicht überschrieben sind:

- `HttpGetAsync<T>(string, CancellationToken)` (`VideoWebPlayerClient.cs:140`) — genutzt von
  `RequestPlaylistEntriesPagedAsync` (`VideoWebPlayerClient.Playlists.cs:62-65`),
- `HttpPutAsync<T>` / `HttpPutAsync` (`:167`, `:178`) — Umbenennen, Genres setzen, Umordnen, Öffentlich-Kennzeichnung,
- `HttpPatchAsync<T>` (`:202`) — Sortiermodus,
- `HttpDeleteAsync<T>` / `HttpDeleteAsync` (`:236`, `:245`) — Playlist löschen, Eintrag entfernen, Cover löschen,
- `PostForOptionalPlaylistNavigationResultAsync` (`VideoWebPlayerClient.Playlists.cs:157-162`), das
  `SendAndDeserializeAsync` direkt aufruft und damit `play/next`, `play/previous` und `play/advance`
  vollständig an der Überschreibung vorbeiführt.

Dass das heute funktioniert, hängt allein daran, dass `NavMenu.OnInitializedAsync` im Layout
`EnsureAuthorizationTokenAsync` aufruft (`NavMenu.razor:119`) und damit das Token des scoped Clients
früh setzt. Fällt dieser Pfad weg (Änderung am Layout, Render-Modus, ein Playlist-Aufruf vor dem
Menü-Aufbau), laufen die genannten Operationen ohne `Authorization`-Header, erhalten `401` und werfen
— ohne dass die vorhandene Reparaturlogik greift, weil `HandleUnauthorized()` in der Basisklasse
`false` liefert. Schweregrad: Hinweis/Lücke (latent, aktuell nicht reproduzierbar ausgelöst).

### F6 — 🟡 Lücke: kein Test kombiniert Gerät/Pairing mit Playlists

Geprüft per Kreuzabgleich: die Dateien mit `X-API-Key`/`PairedDevice`/`RefreshToken`
(`ApiDocumentationContractTests`, `ApiTokenConfigurationTests`, `ApiTokenScopeTests`,
`DevicePairingE2ETests`, `DeviceTokenServiceTests`, `Helpers/PairingTestDb`, `PairingBootstrapE2ETests`,
`PairingBootstrapEndpointsTests`, `PairingBootstrapServiceTests`, `PairingExchangeContractTests_Flow`,
`PairingServiceTests_CodeCreation`, `PairingServiceTests_Exchange`, `RefreshTokenServiceTests`)
enthalten **keine** davon das Wort „playlist" — und umgekehrt.

Konkret fehlen: Playlist-Zugriff mit einem per Bootstrap gewonnenen JWT, Verhalten nach Widerruf des
Geräts (JWT weiter gültig, Refresh `401`), Playlist-Cover über `?access_token=`, Weiterschauen-Meldung
mit `playlistId` von einem Gerät. Der vorhandene Unterbau macht das leicht:
`Helpers/PairingWebApplicationFactory`, `Helpers/PairingTestDb`, `Helpers/PairingCryptoHelper` sowie die
Muster aus `PairingBootstrapEndpointsTests` sind unverändert wiederverwendbar (die Probe in 2.1 ist
genau so gebaut).

### F7 — 🟡 Lücke: kein Test kombiniert lokale Medienquellen mit Playlists

Die Dateien mit `LocalDirectory`/`LocalMediaSource` (`ApplicationDbContextTests`,
`Controllers/ItemsControllerLocalSourceTests`, `LocalMediaSourceClassifierTests`,
`LocalMediaSourcePipelineE2ETests`, `LocalMediaSourceStreamingE2ETests`,
`MediaSourceAdminDetailsValidationTests`, `MediaSourceLocalDirectoryE2ETests`,
`Services/LocalMediaSourceReaderTests`, `Services/MediaEntryFilterTests`,
`Services/MediaSourceReaderDispatcherTests`, `Services/MediaSourceScannerLocalTests`) enthalten mit
einer Ausnahme (`VideoWebPlayerBackupDataTests`, rein zufällig) kein „playlist". Es fehlen:
Wiedergabe eines Playlist-Eintrags aus einer lokalen Quelle, Backfill-Marker nach einem
„Neu erfassen" einer lokalen Quelle, Cover-Collage aus Bildern einer lokalen Quelle, Löschen einer
lokalen Quelle mit Playlist-Weiterschauen-Ersatz. Wiederverwendbar sind
`LocalMediaSourcePipelineE2ETests` (Aufbau echter Verzeichnisse) und `PlaylistsE2ETestBase`.

### F8 — 🟡 Lücke: drei flackernde Tests, zwei davon auch einzeln

Vollständiger Lauf: `Fehler: 3, erfolgreich: 1497, gesamt: 1500`.

| Test | Fehlerbild | Wiederholung einzeln |
|------|-----------|----------------------|
| `PlaylistMediaSearchE2ETests.SelectSeason_CascadesOnlyThatSeasonsEpisodes` | `TimeoutException: Timeout 5000ms exceeded` bzw. `30000ms`, wartend auf `.media-search-result[…]` / `#playlist-mode-add-button` | 4 Läufe: 3× grün, 1× rot |
| `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` | `TimeoutException: Timeout 5000ms exceeded`, wartend auf `.media-search-result[…]` | 3 Läufe: 2× grün, 1× rot |
| `BackupUploadE2ETests.BackupUpload_Interrupted_ResumesFromServerOffset` | Playwright-`expect` auf Statusmeldung | 3 Läufe: 3× grün |

Beide Playlist-Fehlschläge enden im selben Hilfsverfahren
`VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs:135-144` (`SelectSearchResultAsync`). Dort steht ein
fest kodiertes `Timeout = 5000` für das Suchergebnis und ein `WaitForTimeoutAsync(1000)` nach dem
Klick — deutlich unter dem Playwright-Standard von 30 s. Die entprellte Live-Suche plus Blazor-Roundtrip
überschreitet das unter Last. Weil zwei verschiedene Testklassen darauf aufsetzen, betrifft es beide
gleichzeitig. Nach `AGENTS.md` §7 sind flackernde Playwright-Tests „ehrlich zu nennen" — das geschieht
hiermit; ein Fehlschlag in ~25 % der Einzelläufe ist aber hoch genug, um die CI regelmäßig zu stören.

### F9 — 🟡 Lücke: `docs/help/geraete/*` ist auf dem Stand vor dem QR-Bootstrap

`docs/help/geraete/api.md` schreibt: „Das Feature exponiert einen **einzigen** öffentlichen Endpunkt
`POST /api/pairing/exchange`" und „Im Scope `MauiOnly` (**derzeit nur** `POST /api/auth/login`)". Beides
ist seit #232 falsch: es gibt zusätzlich `POST /api/pairing/bootstrap`, und `MauiOnly` gilt auch für
`POST /api/auth/refresh` und `POST /api/auth/logout` (`AuthController.cs:98,127`).

`grep` über `docs/help/geraete/datenmodell.md`, `architektur.md`, `ablauf-technisch.md` und
`beschreibung.md` nach `RefreshToken|Bootstrap|bootstrap` liefert **null Treffer** — die Tabelle
`RefreshTokens`, die Spalten `PairingCodes.Kind`/`TicketHash`, die Rotation mit Reuse-Detection und der
Self-Service-Ablauf sind in der technischen Gerätedokumentation nicht beschrieben. `beschreibung.md`
behauptet zudem „Die Verwaltung liegt im Einrichtungsbereich unter `Einrichtung` > `Geräte` und ist nur
für Administratoren sichtbar", obwohl jeder angemeldete Benutzer über `Profil` > `Geräte`
(`VideoWebPlayer/Components/Account/Pages/Manage/Devices.razor`) ein Bootstrap-Ticket erzeugen kann,
solange `Pairing:BootstrapAdminOnly` nicht gesetzt ist (Standard `false`). Beschrieben ist der
Self-Service nur in `qr-bootstrap-anwender.md`.

Playlist-Bezug fehlt in der Gerätedokumentation komplett: Nirgends steht, dass ein gekoppeltes Gerät die
Playlists **des JWT-Benutzers** sieht (und bei QR-Bootstrap die des Ticket-Erstellers), dass öffentliche
Playlists auch dort sichtbar sind und dass ein Widerruf die laufende Playlist-Wiedergabe bis zu 12
Stunden nicht beendet.

### F10 — 🟡 Lücke (vor dem Push beheben): deutsche Release Notes ohne QR-Bootstrap

`docs/RELEASE_NOTES.md`: englischer Teil 48 Aufzählungspunkte unter „What's New", deutscher Teil 42
unter „Neuerungen". Ein `grep` nach `QR|Bootstrap|Refresh|Einmal-Ticket` im deutschen Teil liefert
**null Treffer**. Es fehlen im Deutschen alle sechs #232-Einträge (QR-Bootstrap von der Profilseite,
`POST /api/pairing/bootstrap`, `POST /api/auth/refresh`/`logout`, Widerruf entzieht Refresh-Tokens,
Bootstrap-Tickets, NuGet `QRCoder`) sowie beide „Wichtige Hinweise vor dem Update":

- Schemaänderung (`PairingCodes.Kind`/`TicketHash`, neue Tabelle `RefreshTokens`) mit dem Hinweis auf
  `dotnet ef database update` — nur englisch (`docs/RELEASE_NOTES.md:11`),
- neue Konfigurationsschlüssel `Pairing:BootstrapTicketTtlMinutes`, `Pairing:BootstrapMaxTicketsPerHour`,
  `Pairing:BootstrapAdminOnly`, `Auth:RefreshTokenTtlDays` — nur englisch (`:12`).

Der zweite Punkt ist betrieblich relevant: die deutschen „Wichtigen Hinweise" sind das, was ein
deutschsprachiger Betreiber vor dem Update liest. **Behebung vor dem Push empfohlen** (reine
Dokumentation).

Zusätzlich ist die Aussage in **beiden** Sprachen sachlich falsch, solange F1 offen ist („Datensicherungen
älterer Versionen bleiben wiederherstellbar").

---

## 5. Anforderungsentwürfe für separate Lifecycle-Läufe

Die folgenden Entwürfe sind jeweils für sich verständlich und können unverändert an einen
`/lifecycle`-Lauf übergeben werden.

---

### A1 — Alte Datensicherungen müssen wiederherstellbar bleiben

**Priorität:** Hoch — **vor dem Push beheben**
**Eigener Lifecycle-Lauf:** Ja (klein, aber eigenständig; alternativ als Fehlerkorrektur mit Regressionstest)

**Ausgangslage.** Mit den Funktionen „Gerätekopplung" und „QR-Bootstrap" sind drei neue Datenbanktabellen
hinzugekommen: `PairedDevices`, `PairingCodes` und `RefreshTokens`. Beim Wiederherstellen einer
Datensicherung prüft das Programm, ob die Sicherung alle Tabellen enthält, die die heutige Version kennt.
Tabellen, die es früher noch nicht gab, müssen ausdrücklich als „darf fehlen" hinterlegt sein. Für die
drei neuen Tabellen ist das nicht geschehen. Eine Datensicherung, die vor diesen Funktionen erstellt
wurde, wird deshalb mit der Meldung „Tabelle PairedDevices fehlt." abgelehnt und lässt sich nicht mehr
einspielen. Das betrifft jede Sicherung einer produktiven Installation aus der Zeit davor. Die Release
Notes sagen ausdrücklich das Gegenteil („Datensicherungen älterer Versionen bleiben wiederherstellbar").

**Ziel / gewünschtes Verhalten.**
1. Eine Datensicherung, die ohne die Tabellen `PairedDevices`, `PairingCodes` und `RefreshTokens`
   erstellt wurde, lässt sich vollständig und ohne Fehlermeldung wiederherstellen.
2. Nach einer solchen Wiederherstellung bleiben die drei Tabellen leer; bereits gekoppelte Geräte sind
   also abgemeldet und müssen neu gekoppelt werden. Das ist das gewünschte und sichere Verhalten und
   wird in den Release Notes und in der Hilfe zu Datensicherungen so benannt.
3. Die Aussage in den Release Notes stimmt anschließend wieder.

**Akzeptanzkriterien.**
- [ ] Eine Sicherung ohne `PairedDevices` lässt sich wiederherstellen; danach ist die Anwendung
      benutzbar und die Geräteliste leer.
- [ ] Dasselbe gilt für eine Sicherung ohne `PairingCodes` und für eine ohne `RefreshTokens`.
- [ ] Dasselbe gilt für eine Sicherung, der alle drei Tabellen gleichzeitig fehlen.
- [ ] Je Fall gibt es einen automatischen Regressionstest, der ohne die Behebung fehlschlägt.
- [ ] Eine Sicherung der heutigen Version enthält die drei Tabellen weiterhin und stellt deren Inhalt
      vollständig wieder her.
- [ ] Release Notes (deutsch und englisch) und die Hilfeseite zu Datensicherungen nennen, dass gekoppelte
      Geräte und Sitzungen aus einer alten Sicherung nicht zurückkommen.

**Betroffene Bereiche.** Sicherung/Wiederherstellung der Datenbank, Gerätekopplung, Release Notes,
Hilfe „Backups".

**Abgrenzung.** Keine Änderung am Sicherungsformat, am Upload-Verfahren oder an der Gerätekopplung
selbst. Es geht ausschließlich um die Verträglichkeit mit älteren Sicherungen.

---

### A2 — Die API-Dokumentation muss die Playlist-Endpunkte vollständig beschreiben

**Priorität:** Hoch — **vor dem Push beheben**
**Eigener Lifecycle-Lauf:** Ja (Dokumentation plus Erweiterung des Vertragstests)

**Ausgangslage.** `docs/API.md` versteht sich als verbindlicher Vertrag für alle Programme, die den
VideoWebPlayer von außen ansprechen (insbesondere die TV-/Client-App), und verweist am Ende auf einen
automatischen Vertragstest, der die Vollständigkeit sichern soll. Der Playlist-Teil endet jedoch bei der
Verwaltung der Einträge. Siebzehn Endpunkte sind nicht beschrieben, darunter alles zur Wiedergabe
(Starten, Vorwärts, Rückwärts, automatisches Weiterschalten), zum Titelbild (Abrufen, Hochladen,
Neu erzeugen, Vorschau, Löschen), zum Sortiermodus, zu den Genres und zum manuellen Umsortieren. Der
Vertragstest wiederum verlangt keine einzige Playlist-Route und kennt auch die neuen Sitzungs-Endpunkte
`POST /api/pairing/bootstrap`, `POST /api/auth/refresh` und `POST /api/auth/logout` nicht. Ein
Entwickler einer Client-App kann die Playlist-Wiedergabe daher nicht aus der Dokumentation heraus bauen.

**Ziel / gewünschtes Verhalten.**
1. `docs/API.md` beschreibt jeden vorhandenen Playlist-Endpunkt mit Pfad, Verfahren, Parametern,
   Antwortformat und den möglichen Fehlerantworten — auf demselben Niveau wie die bereits beschriebenen.
2. Dokumentiert wird auch, dass sich der Anmeldenachweis bei allen Endpunkten alternativ als
   Abfrageparameter `access_token` mitgeben lässt (nötig für Bilder und Videoströme, funktioniert aber
   überall).
3. Der Vertragstest prüft, dass die zentralen Playlist-Routen und die Sitzungs-Endpunkte in der
   Dokumentation stehen, und führt zusätzlich einen echten Durchlauf aus: anmelden, Playlist anlegen,
   Titel hinzufügen, Wiedergabe starten, nächsten Titel holen.
4. Widersprüche zwischen `docs/API.md` und `docs/help/playlists-api.md` sind aufgelöst; das
   Aktualisierungsdatum im Kopf von `docs/API.md` stimmt.

**Akzeptanzkriterien.**
- [ ] Alle Endpunkte des Playlist-Bereichs sind in `docs/API.md` beschrieben; ein Abgleich gegen den
      Programmcode zeigt keine fehlende Route.
- [ ] Der Vertragstest schlägt fehl, wenn eine der geforderten Routen aus der Dokumentation entfernt wird.
- [ ] Der Vertragstest führt den genannten Durchlauf gegen die laufende Anwendung erfolgreich aus.
- [ ] Die Angaben zu Berechtigungen (wer darf lesen, wer darf ändern) stimmen mit
      `docs/help/playlists-api.md` und mit dem tatsächlichen Verhalten überein.

**Betroffene Bereiche.** `docs/API.md`, `docs/help/playlists-api.md`, API-Vertragstest.

**Abgrenzung.** Keine Änderung an den Endpunkten selbst. Die Statuscode-Abweichungen sind Gegenstand
von A4.

---

### A3 — Deutsche Release Notes für die Gerätekopplung per QR-Code nachziehen

**Priorität:** Hoch — **vor dem Push beheben**
**Eigener Lifecycle-Lauf:** Nein — klein genug, um zusammen mit A1 erledigt zu werden

**Ausgangslage.** Die Release Notes führen jede Neuerung zweimal auf: einmal englisch, einmal deutsch.
Für die Funktion „Gerät per QR-Code koppeln" fehlen im deutschen Teil sämtliche Einträge. Besonders
störend: Unter „Wichtige Hinweise vor dem Update" fehlen deutsch der Hinweis auf die Datenbankänderung
(neue Tabelle für Sitzungs-Erneuerungen, zusätzliche Spalten bei den Pairing-Codes) und die vier neuen
Einstellungsschlüssel. Genau diesen Abschnitt liest ein deutschsprachiger Betreiber vor dem Update.

**Ziel / gewünschtes Verhalten.** Der deutsche Teil der Release Notes enthält dieselben Aussagen wie der
englische — inhaltlich gleichwertig, in derselben Reihenfolge und im gewohnten Stil (Nutzen zuerst,
keine internen Bezeichner ohne Erklärung).

**Akzeptanzkriterien.**
- [ ] Zu jedem englischen Punkt gibt es einen deutschen Punkt und umgekehrt.
- [ ] Die deutschen „Wichtigen Hinweise vor dem Update" nennen die Datenbankänderung und die neuen
      Einstellungsschlüssel.
- [ ] Die Aussage zur Wiederherstellbarkeit alter Datensicherungen ist in beiden Sprachen richtig
      (hängt von A1 ab).

**Betroffene Bereiche.** `docs/RELEASE_NOTES.md`.

**Abgrenzung.** Keine Programmänderung.

---

### A4 — Verständliche Fehlerantworten der Medien-API (fehlende Berechtigung, unbekannte Inhalte)

**Priorität:** Mittel
**Eigener Lifecycle-Lauf:** Ja

**Ausgangslage.** Ruft ein angemeldeter Anwender einen Film oder eine Episode ab, für die ihm die
Freischaltung fehlt, antwortet der Server mit „nicht angemeldet" (401) statt mit „kein Zugriff" (403).
Fragt er eine Kennung ab, die es nicht gibt, oder einen Titel ohne hinterlegte Videodatei, antwortet der
Server mit „interner Serverfehler" (500) statt mit „nicht gefunden" (404). Die eigene API-Dokumentation
beschreibt beide Fälle anders (403 bzw. 404).

Mit den öffentlichen Playlists ist das kein Randfall mehr: Eine öffentliche Playlist zeigt einem fremden
Anwender bewusst auch Titel, für die ihm die Freischaltung fehlt. Client-Apps werten „nicht angemeldet"
außerdem als „Sitzung abgelaufen" und versuchen daraufhin, die Sitzung zu erneuern und die Anfrage zu
wiederholen — was hier nie hilft, unnötige Anmeldevorgänge auslöst und dem Anwender eine falsche
Meldung zeigt.

**Ziel / gewünschtes Verhalten.**
1. Fehlt einem angemeldeten Anwender die Berechtigung für einen Inhalt, antwortet der Server mit
   „kein Zugriff" und einer verständlichen Meldung.
2. Existiert ein Inhalt nicht oder ist zu ihm keine Videodatei hinterlegt, antwortet der Server mit
   „nicht gefunden".
3. „Nicht angemeldet" bleibt genau den Fällen vorbehalten, in denen kein oder ein ungültiger
   Anmeldenachweis vorliegt.
4. Die Oberfläche und eine Client-App zeigen in beiden Fällen eine passende Meldung, statt eine
   Neuanmeldung zu versuchen.

**Akzeptanzkriterien.**
- [ ] Abruf von Details, Videostrom und Download eines nicht freigeschalteten Titels durch einen
      angemeldeten Anwender ergibt „kein Zugriff" (403).
- [ ] Abruf mit unbekannter Kennung ergibt „nicht gefunden" (404).
- [ ] Abruf eines Titels ohne hinterlegte Videodatei ergibt „nicht gefunden" (404).
- [ ] Abruf ohne Anmeldenachweis ergibt weiterhin „nicht angemeldet" (401).
- [ ] Ein Anwender, der eine öffentliche Playlist eines anderen öffnet und einen für ihn gesperrten
      Titel anklickt, bekommt eine verständliche Meldung; es wird keine Neuanmeldung ausgelöst.
- [ ] `docs/API.md` und die Verhaltensbeschreibung stimmen überein.
- [ ] Bestehende Tests, die heute 401/500 erwarten, sind mitgezogen; neue Tests sichern jeden Fall ab.

**Betroffene Bereiche.** Medien-Endpunkte (Details, Videostrom, Download), Client-Bibliothek,
Playlist-Detailseite, API-Dokumentation.

**Abgrenzung.** Keine Änderung an der Berechtigungslogik selbst — nur daran, wie das Ergebnis nach
außen gemeldet wird.

---

### A5 — Client-Bibliothek um Gerätekopplung, QR-Bootstrap und Sitzungserneuerung ergänzen

**Priorität:** Mittel
**Eigener Lifecycle-Lauf:** Ja

**Ausgangslage.** Die gemeinsame Client-Bibliothek `VideoWebPlayer.Client` bündelt die Aufrufe, die eine
Client-App (z. B. die TV-App) gegen den Server macht — einschließlich aller Playlist-Funktionen. Für die
neue Gerätekopplung bringt sie zwar die Datenstrukturen mit (Pairing, Bootstrap, Erneuerung), aber keine
einzige Methode: Es gibt keinen Aufruf zum Einlösen eines Pairing-Codes, keinen für den QR-Bootstrap,
keinen zum Erneuern der Sitzung und keine Möglichkeit, das Geräte-Token als Zugangsschlüssel
mitzugeben. Der vorhandene Wiederholungsmechanismus bei abgelaufener Sitzung ist wirkungslos, weil er
nicht befüllt ist. Jede App muss diesen Teil also doppelt bauen, obwohl die Bibliothek dafür gedacht ist.

**Ziel / gewünschtes Verhalten.**
1. Die Bibliothek bietet Aufrufe für: Pairing-Code einlösen, QR-Bootstrap einlösen, Sitzung erneuern,
   abmelden.
2. Das Geräte-Token lässt sich einmal setzen und wird bei allen Aufrufen mitgeschickt, die es brauchen.
3. Läuft die Sitzung während der Benutzung ab, erneuert die Bibliothek sie selbsttätig mit dem
   Erneuerungs-Token und wiederholt die Anfrage — auch bei Playlist-Aufrufen, einschließlich der
   Wiedergabesteuerung (Starten, Vorwärts, Rückwärts, Weiterschalten) und aller ändernden Aufrufe.
4. Schlägt die Erneuerung fehl (Gerät widerrufen, Token abgelaufen), meldet die Bibliothek das eindeutig,
   statt die Anfrage endlos zu wiederholen.

**Akzeptanzkriterien.**
- [ ] Ein Testdurchlauf koppelt ein Gerät über den QR-Bootstrap, ruft anschließend Playlists ab, startet
      eine Wiedergabe und meldet Fortschritt — alles über die Bibliothek.
- [ ] Läuft die Sitzung zwischen zwei Playlist-Aufrufen ab, wird sie selbsttätig erneuert und der Aufruf
      erfolgreich wiederholt; das gilt auch für „nächster Titel", „vorheriger Titel",
      „automatisch weiterschalten", für das Umbenennen, Löschen, Umsortieren und die Cover-Aufrufe.
- [ ] Nach Widerruf des Geräts meldet die Bibliothek beim nächsten Erneuerungsversuch einen
      eindeutigen Fehler.
- [ ] Die Aufrufe der Bibliothek, die heute an der Sitzungsabsicherung vorbeilaufen, sind einbezogen
      (siehe technische Notiz: Aufrufe mit PUT/PATCH/DELETE, Abrufe mit Abbruchmarke und die drei
      Wiedergabe-Navigationsaufrufe).
- [ ] Die Weboberfläche verhält sich unverändert.

**Betroffene Bereiche.** `VideoWebPlayer.Client` (Sitzungsverwaltung, Playlist-Aufrufe), der
serverinterne Ableger der Bibliothek, Tests.

**Abgrenzung.** Keine Änderung an den Server-Endpunkten. Die TV-App selbst liegt in einem anderen
Repository und ist nicht Teil dieser Anforderung.

---

### A6 — Automatische Prüfungen für Playlists auf gekoppelten Geräten

**Priorität:** Mittel
**Eigener Lifecycle-Lauf:** Ja

**Ausgangslage.** Playlists sind ausgiebig getestet — aber ausschließlich über die Weboberfläche und
über direkte Dienst- und Endpunktaufrufe mit einer Web-Sitzung. Gerätekopplung und QR-Bootstrap sind
ebenfalls getestet — aber nur bis zur Anmeldung. Kein einziger Test verbindet beides. Damit ist nicht
abgesichert, dass ein gekoppeltes Gerät Playlists sehen, abspielen und den Fortschritt melden kann, und
dass sich ein Widerruf des Geräts so auswirkt, wie es die Dokumentation zusagt.

**Ziel / gewünschtes Verhalten.** Automatische Tests belegen den vollständigen Weg eines Geräts:
koppeln, Playlists abrufen, Wiedergabe starten, weiterschalten, Fortschritt mit Playlist-Bezug melden,
Titelbild abrufen, Sitzung erneuern, Gerät widerrufen.

**Akzeptanzkriterien.**
- [ ] Ein Test koppelt ein Gerät per QR-Bootstrap und ruft danach die Playlists des Anwenders ab.
- [ ] Ein Test startet aus einer Playlist eine Wiedergabe, holt den nächsten Titel und meldet Fortschritt
      mit Playlist-Bezug; der Eintrag erscheint anschließend in der Weiterschauen-Liste mit
      Playlist-Namen.
- [ ] Ein Test ruft das Titelbild einer Playlist mit dem Anmeldenachweis als Abfrageparameter ab.
- [ ] Ein Test belegt, dass nach dem Widerruf des Geräts die Sitzungserneuerung abgelehnt wird, die
      bereits laufende Sitzung aber bis zu ihrem Ablauf weiterarbeitet — so, wie es dokumentiert ist.
- [ ] Ein Test belegt, dass ein zweiter Anwender über ein eigenes Gerät nur seine eigenen und die
      öffentlichen Playlists sieht und fremde nicht ändern kann.
- [ ] Die vorhandenen Testhilfen für die Kopplung werden wiederverwendet, statt neue zu bauen.

**Betroffene Bereiche.** Automatische Tests (Endpunkt- und Ende-zu-Ende-Ebene).

**Abgrenzung.** Keine Programmänderung; falls dabei ein Fehlverhalten auffällt, wird es getrennt gemeldet.

---

### A7 — Automatische Prüfungen für Playlists mit lokalen Verzeichnissen als Medienquelle

**Priorität:** Mittel
**Eigener Lifecycle-Lauf:** Ja

**Ausgangslage.** Seit kurzem kann ein Verzeichnis auf dem Server statt eines SFTP-Servers als
Medienquelle dienen. Die Prüfung dieser Funktion und die Prüfung der Playlists laufen bisher getrennt
nebeneinander her; kein Test verbindet beides. Die Durchsicht des Programms zeigt, dass die beteiligten
Bausteine den Quelltyp gar nicht unterscheiden — belegt ist das für die Kombination aber nicht.

**Ziel / gewünschtes Verhalten.** Automatische Tests belegen, dass Playlists mit Inhalten aus einem
lokalen Verzeichnis genauso funktionieren wie mit Inhalten von einem SFTP-Server.

**Akzeptanzkriterien.**
- [ ] Ein Test legt eine Medienquelle vom Typ „lokales Verzeichnis" mit echten Dateien an, fügt einen
      Titel daraus einer Playlist hinzu und spielt ihn aus der Playlist heraus ab.
- [ ] Ein Test belegt, dass ein neu hinzugekommener Titel in einem lokalen Verzeichnis nach dem
      „Neu erfassen" selbsttätig in eine Playlist nachgeliefert wird, die die zugehörige Serie, Staffel
      oder Filmsammlung enthält.
- [ ] Ein Test belegt, dass das Titelbild einer Playlist aus Bildern lokaler Inhalte erzeugt wird.
- [ ] Ein Test belegt, dass beim Löschen einer lokalen Medienquelle ein Weiterschauen-Eintrag mit
      Playlist-Bezug durch den nächsten verfügbaren Titel derselben Playlist ersetzt wird.
- [ ] Die vorhandenen Testhilfen für lokale Verzeichnisse und für Playlists werden wiederverwendet.

**Betroffene Bereiche.** Automatische Tests.

**Abgrenzung.** Keine Programmänderung.

---

### A8 — Gerätedokumentation auf den Stand der QR-Kopplung bringen und den Playlist-Bezug ergänzen

**Priorität:** Mittel
**Eigener Lifecycle-Lauf:** Ja

**Ausgangslage.** Die Hilfeseiten unter „Geräte" beschreiben den Stand vor der Einführung der Kopplung
per QR-Code. Sie behaupten, es gebe genau einen öffentlichen Endpunkt und den Zugangsschlüssel brauche
nur die Anmeldung; beides stimmt nicht mehr. Die Sitzungs-Erneuerung, die einmaligen Bootstrap-Tickets
und die zugehörige Datenhaltung kommen in Beschreibung, technischem Ablauf, API, Datenmodell,
Architektur und Geschäftsregeln überhaupt nicht vor — beschrieben ist nur die Anwenderanleitung zum
QR-Code. Außerdem steht dort, die Geräteverwaltung sei ausschließlich Administratoren zugänglich,
obwohl jeder Anwender sich über seine Profilseite ein Gerät koppeln kann.

Zum Playlist-Feature fehlt der Bezug ganz: Es steht nirgends, wessen Playlists ein gekoppeltes Gerät
sieht, dass öffentliche Playlists auch dort erscheinen und dass ein Widerruf eine laufende Wiedergabe
nicht sofort beendet.

**Ziel / gewünschtes Verhalten.** Die Gerätedokumentation beschreibt vollständig und widerspruchsfrei,
wie ein Gerät gekoppelt wird (Code und QR-Code), wie eine Sitzung erneuert und beendet wird, welche
Daten dabei gespeichert werden, und was ein Gerät nach der Kopplung sehen und tun darf — einschließlich
der Playlists.

**Akzeptanzkriterien.**
- [ ] Beschreibung, technischer Ablauf, API, Datenmodell, Architektur und Geschäftsregeln decken den
      QR-Bootstrap, die Sitzungs-Erneuerung und das Abmelden ab.
- [ ] Die Aussage „nur ein öffentlicher Endpunkt" und die Aufzählung der Endpunkte mit
      Zugangsschlüssel sind korrigiert.
- [ ] Es ist beschrieben, dass sich auch ein Anwender ohne Administratorrechte über seine Profilseite ein
      Gerät koppeln kann und wie sich die Einstellung „nur Administratoren" darauf auswirkt.
- [ ] Es ist beschrieben, wessen Playlists ein gekoppeltes Gerät sieht (die des angemeldeten Anwenders,
      bei QR-Kopplung zunächst die des Erstellers des Tickets), dass öffentliche Playlists dort ebenfalls
      erscheinen und wie ein Benutzerwechsel auf dem Gerät möglich ist.
- [ ] Es ist beschrieben, dass ein Widerruf die Erneuerung sofort sperrt, eine laufende Sitzung aber bis
      zu zwölf Stunden weiterarbeitet — auch bei laufender Playlist-Wiedergabe.
- [ ] Alle Querverweise sind gültig (Link-Prüfung läuft durch).

**Betroffene Bereiche.** `docs/help/geraete/*`, Verweis aus der Playlist-Hilfe.

**Abgrenzung.** Keine Programmänderung.

---

### A9 — Flackernde Playlist-Prüfungen im Browser stabilisieren

**Priorität:** Niedrig bis mittel
**Eigener Lifecycle-Lauf:** Ja (klein)

**Ausgangslage.** Zwei automatische Browser-Prüfungen rund um die Titelsuche in Playlists schlagen
unregelmäßig fehl — im Messlauf in etwa jedem vierten Einzeldurchlauf. Beide scheitern an derselben
Stelle: Die gemeinsame Hilfsroutine, die einen Titel über die Suche auswählt, wartet nur fünf Sekunden
auf das Suchergebnis und danach eine feste Sekunde auf die Serverantwort. Unter Last reicht das nicht.
Ein einzelner Fehlschlag ohne Ursache kostet bei jedem Durchlauf Zeit und untergräbt das Vertrauen in
die Prüfungen.

**Ziel / gewünschtes Verhalten.** Die Prüfungen warten auf das tatsächliche Eintreten des erwarteten
Zustands statt auf feste Zeiten und laufen auch unter Last zuverlässig durch — ohne dass eine Prüfung
abgeschwächt oder abgeschaltet wird.

**Akzeptanzkriterien.**
- [ ] Zwanzig aufeinanderfolgende Durchläufe der beiden betroffenen Prüfungen sind fehlerfrei.
- [ ] Die vollständige Testsuite läuft dreimal hintereinander fehlerfrei durch.
- [ ] Keine Prüfung wurde deaktiviert, übersprungen oder in ihrer Aussage abgeschwächt.
- [ ] Feste Wartezeiten sind durch Warten auf einen beobachtbaren Zustand ersetzt.

**Betroffene Bereiche.** Gemeinsame Testhilfe der Playlist-Browserprüfungen.

**Abgrenzung.** Keine Programmänderung an der Anwendung.

---

## 6. Ehrliche Einordnung der Grenzen dieser Analyse

- Die Aussagen zur Client-App beruhen ausschließlich auf `VideoWebPlayer.Client` und der
  Dokumentation. Wie die tatsächliche MAUI-/TV-App mit 401-Antworten, Refresh-Tokens und Playlists
  umgeht, konnte hier nicht geprüft werden.
- F1, F3, F6 und F7 wurden empirisch belegt; F5 ist eine Code-Analyse ohne reproduzierten Ausfall —
  der beschriebene Schaden tritt heute nicht ein, weil das Menü den Anmeldenachweis vorher setzt.
- Die Migrationsprüfung gegen eine „bestehende Datenbank" ist eine Nachstellung des Staging-Standes,
  keine Kopie der produktiven Stromberg-Datenbank. Datenmengen, Altlasten und manuelle Eingriffe dort
  sind nicht abgedeckt.
- Nicht geprüft wurden: Lastverhalten des Backfills, UDP-Discovery, IIS-Betrieb, ein Restore mit einer
  echten produktiven Sicherungsdatei sowie die Wiedergabe echter Videodateien vom Gerät.
- Die drei flackernden Tests wurden je drei- bis viermal wiederholt; das ist genug für die Aussage
  „flaky", aber zu wenig für eine belastbare Fehlerquote.
