# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die Anforderung ist inhaltlich fast vollständig abgedeckt (kein multipart, `application/octet-stream`, Chunk-Metadaten via Header, `Request.Body`-Streaming ohne `IFormFile`, `FileStream`+`Seek`, transaktionaler Temp-Abschluss über `ImportUploadAsync`, Resume per 308 + Status-Endpunkt, IIS-OutOfProcess + systemd, `Kestrel:Limits:MaxRequestBodySize = 0` in `appsettings.Production.json`). Alle Planbehauptungen zum Ist-Stand wurden am Code verifiziert und sind korrekt (`BackupsController.Upload` Z. 64–89, `Backups.razor` Formular Z. 240–250 / Token Z. 384–397, `Updates.razor`-`PersistentComponentState`-Muster Z. 309–325, `App.razor` Z. 12–16, `appsettings.Production.json` Z. 18–24, `BackupsControllerAuthorizationTests` Z. 47–62, `docs/help/backups.md` Z. 47–51). Zwei Lücken verhindern den Status „vollständig" (siehe unten).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Upload > 6 GB ohne Browser-/IIS-/Kestrel-Limits: Umstellung `multipart/form-data` → `application/octet-stream`, kein `IFormFile`/`MultipartReader`/Form-Binding | `UploadChunk` (`POST upload/chunk`, `[DisableRequestSizeLimit]`), multipart-`Upload` + `[RequestFormLimits]` + `Request.Form` entfernt; JS sendet `fetch()` mit `Blob.slice()` | `UploadChunkEndpoint_IsExposedAsUnlimitedStreamPost` (Attribute), E2E `BackupUpload_ValidFile_…` | Abgedeckt |
| `Request.Body`-Streaming ohne `IFormFile` | `session.AppendChunkAsync(Request.Body, contentLength, …)` mit `FileStream.Seek` | `AppendChunk_WritesAtOffsetAndTracksReceivedBytes` | Abgedeckt |
| Chunk-Metadaten via HTTP-Header (Upload-Kennung/Dateiname, Startoffset, Gesamtgröße), `Content-Length` = Chunk-Länge | Header-Protokoll `Upload-Id`/`Upload-Name`/`Upload-Length`/`Upload-Offset`, Validierungsregeln-Tabelle | Unit-Tests `BeginSession_*`, `AppendChunk_*`; E2E Multi-Chunk | Abgedeckt |
| Offsetbasiertes Schreiben per `FileStream`+`Seek` in Temp-Datei; Validierung Offset/Länge/Gesamtlänge (keine Überlappung/Lücken) | Sequenziell erzwungen (`Upload-Offset == ReceivedBytes`), `SemaphoreSlim` pro Session | `AppendChunk_RejectsMismatchedOffset`, `…_RejectsOverflowBeyondTotalLength` | Abgedeckt |
| Transaktionaler Abschluss (Temp + Move), `manifest.json`-Prüfung, Historieneintrag „Upload" | Wiederverwendung `VideoWebPlayerBackupFacade.ImportUploadAsync` (Z. 86–150 verifiziert: Temp + `ZipArchive`-Prüfung + Kopie in `StoragePath` + Historie „Upload") | `ImportUploadAsync_*`-Tests (neu), E2E Happy Path | Abgedeckt |
| Resume: 308 + aktueller Offset; Status-/Resume-Endpunkt | 308 + `Upload-Offset`-Header bei Mismatch/Abbruch; `GET upload/{uploadId:guid}` mit JSON + `Upload-Offset` | `GetSession_ReturnsNullForUnknownOrExpired`, `GetUploadStatusEndpoint_IsExposedAsGet`, E2E `BackupUpload_Interrupted_ResumesFromServerOffset` | Abgedeckt |
| `[Authorize(Policy = "AdminOnly")]` + Antiforgery (`IAntiforgery.ValidateRequestAsync`) bleiben; Token per Header `RequestVerificationToken` | Unverändert auf Klassenebene; Token im Header; `PersistentComponentState`-Persistenz nach `Updates.razor`-Muster (korrekt identifiziert: `IHttpContextAccessor.HttpContext` ist im Circuit `null`) | `BackupsController_RequiresAdminOnlyPolicy` (bestehend, unverändert gültig), E2E Happy Path implizit, Sichtbarkeits-E2E | Abgedeckt |
| Client: JS-Upload mit Fortschrittsanzeige + Wiederaufnahme aus `Backups.razor`; kein SignalR-/`InputFile`-Stream | `window.backupUpload` (`wwwroot/js/backupUpload.js`), 128-MiB-Chunks, `localStorage`-Persistenz der `Upload-Id`, Fortschritt/Abbrechen im DOM, `IJSRuntime`-Aufruf | E2E Happy Path (Fortschritt sichtbar), Resume-E2E | Abgedeckt |
| `Kestrel:Limits:MaxRequestBodySize = 0` in `appsettings.Production.json` | Schritt 6, `"Limits": { "MaxRequestBodySize": 0 }` unter `Kestrel` (bestehende `Endpoints` bleiben — verifiziert Z. 18–24) | Konfigurationswert, implizit über E2E (Kestrel-Hosting) | Abgedeckt |
| IIS OutOfProcess + systemd ohne Zusatzlimits | `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` in `VideoWebPlayer.csproj` (generierte `web.config`), Deployment-Hinweis `requestFiltering`/`maxAllowedContentLength` in `docs/help/backups.md`; systemd ohne Zusatzlimits | Dokumentations-Schritt 9; kein automatisierter Test erforderlich (Deployment-Voraussetzung) | Abgedeckt |
| `MaxUploadSizeBytes` bleibt administrierbares Limit; Durchsetzung geklärt | Serverseitig in `BeginSession` anhand `Upload-Length` über `IBackupOptionsProvider`; Überschreitung → 413 | `BeginSession_RejectsLengthAboveMaxUploadSize`, E2E `BackupUpload_ExceedingLimit_ShowsError` | Abgedeckt (siehe jedoch DI-Lücke unten) |
| Bestehender Test `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` angepasst | Ersetzt durch `UploadChunkEndpoint_IsExposedAsUnlimitedStreamPost` + `GetUploadStatusEndpoint_IsExposedAsGet` | In „Betroffene bestehende Tests" benannt | Abgedeckt |
| `docs/help/backups.md` aktualisieren | Schritt 9: Upload-Abschnitt, Limit-Hinweis (veraltetes „512 MB" → 5 GiB), IIS-/systemd-Hinweis | MarkdownLinkCheck läuft weiter | Abgedeckt |
| Offene Fragen der Anforderung entschieden | Protokoll (eigenes tus-ähnliches Header-Schema), Upload-Identifikation (serverseitige GUID), Session-Retention (24 h Lazy-Cleanup + verwaiste Temp-Dateien), Hash-Prüfung (bewusst nein, im Issue nur optional), Kompatibilität (vollständige Ersetzung), IIS-Deployment (csproj + Doku) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] **Fixture für Nicht-Admin-Login fehlt:** Der Pflicht-E2E `BackupUpload_NonAdmin_SeesNoUploadControl` setzt einen authentifizierten Benutzer ohne `IsAdmin`-Claim voraus (Nicht-Admins sollen „Nicht autorisiert" und kein Upload-Control sehen). Im Plan ist nur `SeedAdminAsync` (`UpdatesPageE2ETests` Z. 441–461, erzeugt Admin mit `IsAdmin`-Claim) genannt; eine analoge Seed-Hilfsmethode für einen regulären Benutzer (bzw. die explizite Entscheidung, den Fall anonym zu testen — die Seite hat kein `[Authorize]`, `isAdmin`-Prüfung in `OnInitializedAsync` Z. 374–382) ist nicht benannt.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Happy Path: Admin wählt `.bak`, klickt „Backup hochladen" → Fortschritt, Erfolgsmeldung, Archiv-Eintrag, Historie „Upload" | `BackupUploadE2ETests.BackupUpload_ValidFile_ShowsProgressAndImportsBackup` (Playwright, `SetInputFilesAsync`, `CreateValidBackupFileAsync` via `IBackupService.StoreAsync` im DI-Scope) | Abgedeckt |
| Multi-Chunk-Upload (mehrere Requests, Offset-Fortschaltung) | `BackupUpload_LargeFile_UsesMultipleChunks` (Chunk-Größe per `AddInitScriptAsync` klein gesetzt) | Abgedeckt (Hinweis zu Init-Script-Timing unten) |
| Resume nach Abbruch (308/Status-Endpunkt, anwendersichtbar) | `BackupUpload_Interrupted_ResumesFromServerOffset` (`Page.RouteAsync` + `AbortAsync` auf `**/upload/chunk`) | Abgedeckt |
| Fehlerfall: ungültige Datei → `backupError`, kein Archiv-Eintrag | `BackupUpload_InvalidFile_ShowsError` | Abgedeckt |
| Limit-Überschreitung → Fehlermeldung, kein Import | `BackupUpload_ExceedingLimit_ShowsError` (`Backups:MaxUploadSizeBytes` per `UseSetting` klein — wirkt, da `GetOrCreateAsync` bei frischer Test-DB die Konfiguration übernimmt) | Abgedeckt |
| Sichtbarkeit/Berechtigung: Nicht-Admin sieht kein Upload-Control | `BackupUpload_NonAdmin_SeesNoUploadControl` | Lücke: benötigte Nicht-Admin-Testfixture nicht benannt (siehe oben) |

Infrastruktur verifiziert und umsetzbar: `WebApplicationFactory<Program>` + `UseKestrel()` + `StartServer()` + `SeedAdminAsync` + Playwright Chromium (`UpdatesPageE2ETests` Z. 36–101), `[Trait("Category", "E2E")]`, Environment `Testing` deaktiviert UDP-Listener (`Program.cs` Z. 38).

## Fehlende oder unvollständige Planbestandteile

- [ ] **DI-Lifetime-Konflikt im `BackupUploadSessionService` nicht aufgelöst:** Der Plan spezifiziert den Service als Singleton (`AddSingleton`, Schritt 2; „Neue Klassen"-Tabelle: Abhängigkeiten `IBackupOptionsProvider`, `TimeProvider`, `ILogger`). `IBackupOptionsProvider`/`BackupSettingsService` sind jedoch Scoped registriert (`ServiceCollectionExtensions.cs` Z. 243–244) und hängen am Scoped-`ApplicationDbContext` (`BackupSettingsService.cs` Z. 17–27). Direkte Konstruktor-Injektion in einen Singleton scheitert unter `ValidateScopes` (Development-Default) mit `InvalidOperationException` und erzeugt andernfalls eine captive, thread-unsichere `DbContext`-Instanz über die Lebensdauer der App (relevant bei parallelen Sessions mit eigenem `SemaphoreSlim`). Der Plan benennt keine Auflösungsstrategie — erforderlich wäre z. B. `IServiceScopeFactory`/Scope pro `BeginSession`-Aufruf oder die Auflösung des Limits im Scoped-Controller mit Übergabe an `BeginSession`. Als früherer Schritt/Entscheidung im Plan zu ergänzen.

## Hinweise

- `Page.AddInitScriptAsync` läuft vor dem Laden von `backupUpload.js` (`<script>` in `App.razor`-<head>); `window.backupUpload.chunkSizeBytes` existiert zum Init-Zeitpunkt noch nicht. Für den Multi-Chunk-E2E muss der Override z. B. über `Object.defineProperty(window, 'backupUpload', …)`, Polling oder eine vom Modul gelesene Override-Variable erfolgen — bei der JS-Implementierung mitbedenken (die deklarierte Überschreibbarkeit reicht als Planungsaussage aus).
- Ein expliziter negativer Antiforgery-Test (Chunk-Request ohne/ mit ungültigem Token → 400) ist nicht geplant; die Prüfung wird implizit über den Happy-Path-E2E mit abgedeckt. Optional ergänzbar (z. B. Attribut-/Integrationstest).
- Der Abschluss kopiert die Daten zweimal (Session-Temp → `ImportUploadAsync`-Temp `vwp-backup-import-*.tmp` → `StoragePath`); bei 6-GB-Dateien entsprechend I/O-/Platzbedarf auf ggf. zwei Volumes. Funktional korrekt und im Risikoabschnitt teilweise benannt — die Wiederverwendung der Fassade ist fachlich sauber, eine direkte Pfad-Übergabe wäre optional effizienter.
- `MediaBoxContextMenuE2ETestBase` existiert als alternative E2E-Basisklasse (`VideoWebPlayer.Tests/Helpers/`); der Plan folgt dem einfacheren `UpdatesPageE2ETests`-Muster — ausreichend.
