# Umsetzungsplan: Plattformübergreifender Backup-Upload für sehr große Dateien (6 GB+)

## Übersicht

Der Backup-Import auf der Admin-Seite `/admin/backups` wird von `multipart/form-data` auf einen chunkbasierten `application/octet-stream`-Upload mit Resume-Fähigkeit umgestellt. Betroffen sind der `BackupsController` (neue Chunk-/Status-Endpunkte, Entfernung von `Request.Form`/`IFormFile`/`[RequestFormLimits]`), ein neuer Singleton `BackupUploadSessionService` für Session-/Temp-Dateiverwaltung mit offsetbasiertem Schreiben, die Blazor-Seite `Backups.razor` mit neuem JS-Modul `backupUpload.js` (Fortschritt + Wiederaufnahme) sowie die Produktivkonfiguration (`Kestrel:Limits:MaxRequestBodySize` in `appsettings.Production.json`, wirksam gemacht über eine explizite `ConfigureKestrel`-Bindung in `Program.cs`, und `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` in `VideoWebPlayer.csproj`). Der fachliche Import bleibt in `VideoWebPlayerBackupFacade`, wird aber als `ImportUploadFileAsync` auf die Session-Temp-Datei per Pfad angewendet (Validierung in-place + `File.Move` mit Überschreiben statt erneuter Vollkopie) und nach dem letzten Chunk aufgerufen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Upload-Protokoll | Proprietäres, tus-angelehntes Header-Protokoll: `Upload-Id`, `Upload-Name`, `Upload-Length`, `Upload-Offset`; `Content-Type: application/octet-stream`; 308 + `Upload-Offset` als Resume-Hinweis | Das Issue beschreibt tus-ähnliches Verhalten (308, offsetbasiertes Resume), nennt aber keine Header. Ein schlankes Eigenprotokoll deckt exakt den geforderten Umfang ab und vermeidet eine zusätzliche Server-/Client-Bibliothek. |
| Upload-Identifikation | Serverseitig vergebene `Upload-Id` (GUID) beim ersten Chunk ohne `Upload-Id`; Rückgabe im Response-Header `Upload-Id` | Kollisionsfrei bei parallelen Uploads gleichen Dateinamens; der Client muss keine IDs erzeugen; die Session ist eindeutig vom Dateinamen entkoppelt. |
| Session-/Chunk-Verwaltung | `BackupUploadSessionService` als Singleton (Service Layer) mit `ConcurrentDictionary<Guid, BackupUploadSession>` und benannter Temp-Datei `vwp-backup-upload-{id:N}.tmp` unter `Path.GetTempPath()` | Sessions müssen Request-/DI-Scope-Grenzen überdauern (Controller/Facade sind Scoped). Temp-Datei unter `Path.GetTempPath()` folgt dem bisherigen Temp-Muster der Import-Implementierung (`vwp-backup-import-*.tmp`); `FileOptions.DeleteOnClose` ist für Resume über Requests hinweg ungeeignet, daher benannte Datei mit explizitem Cleanup. |
| DI-Lifetime-Auflösung | `BackupUploadSessionService` injiziert `IServiceScopeFactory`; `BeginSessionAsync` erzeugt pro Aufruf `using var scope = _scopeFactory.CreateScope()` und löst `IBackupOptionsProvider` daraus auf | `IBackupOptionsProvider`/`BackupSettingsService` sind Scoped registriert (`ServiceCollectionExtensions.cs` Z. 243–244) und hängen am Scoped-`ApplicationDbContext` — direkte Konstruktor-Injektion in den Singleton würde unter `ValidateScopes` scheitern bzw. einen thread-unsicheren captive `DbContext` erzeugen. `IServiceScopeFactory` ist das etablierte Muster der bestehenden Backup-Singletons (`ManualBackupJobService` Z. 8/66, `RestoreBackupJobService` Z. 10/73). Die Alternative „Limit im Scoped-Controller auflösen und als Parameter übergeben" entfällt, weil die Limit-Prüfung dann außerhalb der Service-Kapselung läge. |
| Chunk-Schreiben | Sequenziell erzwungen: `Upload-Offset` muss `session.ReceivedBytes` entsprechen; Schreiben per `FileStream.Seek(offset)` + `CopyToAsync` aus `Request.Body`, pro Session serialisiert (`SemaphoreSlim`) | Erfüllt die Anforderung (Seek-basiertes Schreiben, keine Überlappung/Lücken) und hält die Validierung einfach: Offset-Mismatch → 308 mit erwartetem Offset. |
| Abschluss/Import | `VideoWebPlayerBackupFacade.ImportUploadAsync(Stream, …)` wird zu `ImportUploadFileAsync(string tempFilePath, string fileName, string? userId, CancellationToken)` geändert und mit dem Pfad der Session-Temp-Datei aufgerufen: `ZipArchive`/`manifest.json`-Prüfung direkt an der vorhandenen Datei, danach `File.Move` in den `StoragePath` | Der bisherige Stream-Pfad kopiert die Daten zweimal (Session-Temp → `vwp-backup-import-*.tmp` → `StoragePath`) — bei 6-GB-Dateien eine vermeidbare Vollkopie samt doppeltem Platzbedarf. `File.Move` ist auf demselben Volume reine Metadatenoperation und unterstützt Cross-Volume-Moves (Kopie + Löschen) nativ. `ImportUploadAsync` hat ohnehin nur den entfallenen `Upload`-Endpunkt als Aufrufer; die fachliche Validierung (`.bak`, `manifest.json`, `StoragePath`-Auflösung, Historieneintrag `"Upload"`) bleibt unverändert an einer Stelle. |
| Kollisionsverhalten beim Import | `File.Move(tempPath, targetPath, overwrite: true)` — eine gleichnamige Datei im `StoragePath` wird überschrieben | Der bisherige Import überschreibt gleichnamige Zieldateien stillschweigend (`FileMode.Create`, `VideoWebPlayerBackupFacade.cs` Z. 130); `File.Move` ohne `overwrite` würde dagegen `IOException` werfen und das bisherige Verhalten ändern (Upload bei Namensgleichheit abgelehnt statt ersetzt). Das Überschreiben wird bewusst beibehalten: Der Admin importiert explizit, und ein gleichnamiges Archiv-Backup durch eine neuere Version zu ersetzen ist das erwartete Verhalten. |
| `MaxUploadSizeBytes` | Serverseitige Durchsetzung im `BackupUploadSessionService` anhand des `Upload-Length`-Headers; `BeginSessionAsync` löst `IBackupOptionsProvider` über einen kurzlebigen `IServiceScopeFactory`-Scope auf; Überschreitung → 413 | Das administrierbare Limit wäre im neuen Pfad sonst wirkungslos. Prüfung an der Gesamtgröße ist früh und ohne Datentransfer möglich; der Scope hält den Scoped-`DbContext` nur für die Optionsabfrage. |
| Client-Upload | Neues JS-Modul `window.backupUpload` (`wwwroot/js/backupUpload.js`, Muster `continueWatching.js`): `fetch()` mit `Blob.slice()`-Chunks (Konstante 128 MiB, überschreibbar über die globale Variable `window.backupUploadChunkSizeBytes`, die das Modul bei jedem Chunk-Senden liest), Fortschritt/Status direkt im DOM; Blazor ruft nur einmal `backupUpload.start(...)` per `IJSRuntime` | `InputFile`/Circuit-Streams unterlägen SignalR-Größenlimits; die Circuit-Verbindung darf während minutenlanger Uploads nicht kritisch sein — DOM-Updates durch JS sind circuit-unabhängig. Die Override-Variable statt einer Modul-Eigenschaft ist nötig, weil `Page.AddInitScriptAsync` vor dem Laden von `backupUpload.js` (`<script>` im `App.razor`-`<head>`) läuft und `window.backupUpload` zum Init-Zeitpunkt noch nicht existiert. |
| Resume-Persistenz (Client) | `localStorage`-Eintrag `vwp-backup-upload:{name}:{size}:{lastModified}` → `Upload-Id`; beim Start Status-Abfrage, bei 404 Neustart | Resume über Seitenreload/Browser-Neustart ohne serverseitige Zusatzstrukturen. |
| Antiforgery | Token im Header `RequestVerificationToken` (Default-Headername der `AddAntiforgery`-Registrierung); in `Backups.razor` Persistenz des Request-Tokens via `PersistentComponentState` (Muster aus `Updates.razor`, `CreateAntiforgeryRequestToken`) | `ValidateRequestAsync(HttpContext)` liest den Header automatisch. Ohne `PersistentComponentState` wäre `antiforgeryRequestToken` im interaktiven Circuit `null` (`IHttpContextAccessor.HttpContext` ist dort nicht gesetzt) und jeder JS-Upload würde an der Antiforgery-Prüfung scheitern. |
| multipart-Endpunkt | Vollständige Ersetzung: Methode `Upload` entfällt, `POST upload` existiert nicht mehr; `[RequestFormLimits]` wird entfernt, `[DisableRequestSizeLimit]` bleibt an den neuen Endpunkten | Das Issue fordert die Umstellung; ein Parallelbetrieb würde zwei Upload-Pfade und doppelte Wartung schaffen. |
| Verwaiste Sessions | Lazy-Cleanup im `BackupUploadSessionService` bei jedem Zugriff: Sessions mit `LastAccessUtc` älter `SessionTimeout` (Konstante 24 h) verwerfen und Temp-Datei löschen | Keine neue Hintergrund-Infrastruktur nötig; Sessions überleben keinen App-Neustart (In-Memory-Registry), verwaiste Temp-Dateien werden beim nächsten Upload-Zugriff aufgeräumt. |
| Integritätsprüfung | Keine Hash-Prüfung (kein `Upload-Checksum`-Header); Transport-Sicherung über `Content-Length` + Offset-Validierung, fachliche Integrität über die `manifest.json`-Prüfung in `ImportUploadFileAsync` | Die Hash-Prüfung ist im Issue nur optional; die vorhandenen Prüfungen genügen und halten das Protokoll schlank. |
| Hosting/Deployment | `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` in `VideoWebPlayer.csproj`; Deployment-Hinweis (IIS `requestFiltering`/`maxAllowedContentLength`, systemd ohne Zusatzlimits) in `docs/help/backups.md` | Nur OutOfProcess umgeht das IIS-InProcess-Limit; die csproj-Eigenschaft sorgt dafür, dass die beim Publish generierte `web.config` korrekt ist, ohne eine `web.config` ins Repo zu legen. Unter Linux/systemd ohne Effekt. |
| Kestrel-Body-Limit | `Kestrel:Limits:MaxRequestBodySize: 0` bleibt in `appsettings.Production.json`; in `Program.cs` wird `builder.WebHost.ConfigureKestrel((context, options) => …)` ergänzt, das den Konfigurationswert `Kestrel:Limits:MaxRequestBodySize` explizit liest und auf `options.Limits.MaxRequestBodySize` anwendet — Projektkonvention: `0` → `null` (= unbegrenzt), `> 0` → Limit in Bytes; ist der Schlüssel nicht gesetzt, bleibt der Kestrel-Default unberührt | Der `KestrelConfigurationLoader` bindet aus der `Kestrel`-Sektion nur `Endpoints`/`EndpointDefaults`/Zertifikate — `Limits` bleibt ungelesen (bekannte Einschränkung, dotnet/aspnetcore#37544), der appsettings-Eintrag allein wäre also wirkungslos. Zudem wäre ein gebundener Wert `0` semantisch „0 Bytes", nicht „unbegrenzt" (unbegrenzt = `null`). Die explizite Bindung macht den Konfigurationseintrag mit der geforderten Bedeutung tatsächlich wirksam und bleibt auf die Umgebungen beschränkt, die den Schlüssel setzen. |

## Programmabläufe

### Chunk-Empfang (`POST admin/backups/api/upload/chunk`)

1. `BackupsController.UploadChunk` (`[HttpPost("upload/chunk")]`, `[DisableRequestSizeLimit]`) ruft `_antiforgery.ValidateRequestAsync(HttpContext)` auf (Token aus Header `RequestVerificationToken`); bei Fehler → 400.
2. Header auslesen: `Upload-Id` (optional), `Upload-Name` (URI-encodeter Dateiname), `Upload-Length` (Gesamtgröße), `Upload-Offset`, `Content-Length` (Chunk-Länge). `Content-Type` muss `application/octet-stream` sein (sonst 415); fehlende/unparsebare Pflicht-Header → 400.
3. Ohne `Upload-Id`: `BackupUploadSessionService.BeginSessionAsync(fileName, totalLength, cancellationToken)` — validiert `Upload-Name` (`Path.GetFileName`-Regel, nicht leer), `Upload-Length > 0` und `Upload-Length <= MaxUploadSizeBytes`; dafür erzeugt der Service einen Scope über `IServiceScopeFactory` und ruft `IBackupOptionsProvider.GetOptionsAsync` darin auf. Verletzungen → 400 bzw. 413. Legt Temp-Datei `vwp-backup-upload-{id:N}.tmp` an und registriert die Session.
4. Mit `Upload-Id`: `BackupUploadSessionService.GetSession(uploadId)`; unbekannt/abgelaufen → 404 (Client startet neu).
5. `Upload-Offset != session.ReceivedBytes` → Antwort `308` mit Header `Upload-Offset` = aktueller serverseitiger Offset (Resume-Hinweis).
6. `Upload-Offset + Content-Length > session.TotalLength` → 400. Bei bekannter Session werden `Upload-Name`/`Upload-Length` gegen die Session-Werte geprüft; Abweichung → 409.
7. `session.AppendChunkAsync(Request.Body, contentLength, cancellationToken)`: `FileStream.Seek(Upload-Offset)`, Kopieren von `Request.Body` (limitiert auf `Content-Length`), `ReceivedBytes += gelesene Bytes`, `LastAccessUtc` aktualisieren. Bricht der Body-Transfer vorzeitig ab, wird die Temp-Datei auf den letzten konsistenten Offset gekürzt und mit `308` + `Upload-Offset` geantwortet, sofern noch keine Antwort begonnen wurde.
8. `session.ReceivedBytes < session.TotalLength` → `204 No Content` mit Headern `Upload-Id` und `Upload-Offset` (nächster erwarteter Offset).
9. `session.ReceivedBytes == session.TotalLength` → Abschluss-Ablauf (siehe unten).

Beteiligte Klassen/Komponenten: `BackupsController`, `BackupUploadSessionService`, `BackupUploadSession`, `IAntiforgery`, `IBackupOptionsProvider`/`BackupSettingsService`.

### Resume-Status (`GET admin/backups/api/upload/{uploadId}`)

1. `BackupsController.GetUploadStatus` (`[HttpGet("upload/{uploadId:guid}")]`): sichere Methode — keine Antiforgery-Prüfung nötig, `[Authorize(Policy = "AdminOnly")]` auf Klassenebene bleibt wirksam.
2. `BackupUploadSessionService.GetSession(uploadId)` → bekannt: `200 OK` mit JSON `{ uploadId, fileName, uploadLength, uploadOffset }` und Header `Upload-Offset`; unbekannt → `404`.

Beteiligte Klassen/Komponenten: `BackupsController`, `BackupUploadSessionService`.

### Abschluss nach letztem Chunk

1. Session als abgeschlossen markieren, Temp-`FileStream` flushen und schließen (`await session.Stream.DisposeAsync()`) — die Datei muss frei sein, damit die Fassade sie öffnen und verschieben kann.
2. `_backupFacade.ImportUploadFileAsync(session.TempPath, session.FileName, userId, cancellationToken)` — geänderte Signatur (Pfad statt `Stream`): `.bak`-Endung, `manifest.json`-Prüfung via `ZipArchive` direkt an der Temp-Datei, anschließend `File.Move(tempPath, targetPath, overwrite: true)` in `BackupOptions.StoragePath` (Cross-Volume-fähig; überschreibt eine gleichnamige Zieldatei — entspricht dem bisherigen `FileMode.Create`-Verhalten), Historieneintrag `"Upload"`. Bei Validierungsfehlschlag bleibt die Temp-Datei bestehen.
3. Session aus der Registry entfernen; bei Erfolg wurde die Temp-Datei bereits nach `StoragePath` verschoben, bei Fehlschlag löscht der Service sie (`File.Delete`).
4. Erfolg → `200 OK` mit JSON `{ message, fileName }`; Fehlschlag → `400` mit JSON `{ error }` (Text aus `BackupOperationResult`). Der JS-Client überführt die Antwort in eine Navigation auf `/admin/backups?backupStatus=…` bzw. `?backupError=…` (bestehender `ApplyQueryMessages`-Mechanismus in `Backups.razor`).

Beteiligte Klassen/Komponenten: `BackupsController`, `BackupUploadSessionService`, `VideoWebPlayerBackupFacade`, `BackupOperationHistoryService`.

### Client-Upload (JS + Blazor)

1. `Backups.razor`: Admin wählt eine `.bak`-Datei im `<input type="file">` und klickt „Backup hochladen" → `StartUploadAsync` ruft `IJSRuntime.InvokeVoidAsync("backupUpload.start", fileInputRef, uploadContainerRef, antiforgeryRequestToken)`.
2. `backupUpload.js` liest `fileInputRef.files[0]`, bildet den `localStorage`-Schlüssel `vwp-backup-upload:{name}:{size}:{lastModified}` und fragt bei vorhandener `Upload-Id` `GET upload/{id}` ab: Offset übernehmen (Resume) oder bei 404 neu beginnen.
3. Pro Chunk: `fetch('admin/backups/api/upload/chunk', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream', 'Upload-Id'?, 'Upload-Name': encodeURIComponent(file.name), 'Upload-Length': file.size, 'Upload-Offset': offset, 'RequestVerificationToken': token }, body: file.slice(offset, offset + chunkSize), signal: abortSignal })` — `chunkSize` wird bei jedem Senden als `window.backupUploadChunkSizeBytes ?? 128 MiB` aufgelöst (die Override-Variable kann per `Page.AddInitScriptAsync` gesetzt werden, bevor das Modul lädt). Bei `308` wird `Upload-Offset` aus dem Response-Header gelesen und ab dort fortgesetzt; bei Netzwerk-/5xx-Fehlern Status-Abfrage + begrenzte Wiederholungen mit Backoff.
4. Fortschritt: JS rendert Progressbar und Statustext (übertragene Bytes/Gesamt) direkt in `uploadContainerRef` und deaktiviert Datei-Input und Upload-Button während des Laufs; ein „Abbrechen"-Button bricht per `AbortController` ab — die `Upload-Id` bleibt für ein späteres Fortsetzen erhalten.
5. Abschluss: `200` → `localStorage`-Eintrag löschen und `window.location.assign('/admin/backups?backupStatus=' + encodeURIComponent(message))`; `4xx` → analog `backupError`.

Beteiligte Klassen/Komponenten: `Backups.razor`, `backupUpload.js`, `IJSRuntime`, `PersistentComponentState`, `App.razor`.

### Verwaiste Sessions aufräumen

1. Bei jedem Aufruf von `BeginSessionAsync`/`GetSession` prüft `BackupUploadSessionService` die Registry: Sessions mit `LastAccessUtc < utcNow - SessionTimeout (24 h)` werden disposed und ihre Temp-Dateien `vwp-backup-upload-*.tmp` gelöscht.
2. Zusätzlich werden verwaiste Temp-Dateien des Namensmusters gelöscht, die keiner bekannten Session zugeordnet sind und älter als `SessionTimeout` sind (deckt App-Neustarts ab).

Beteiligte Klassen/Komponenten: `BackupUploadSessionService`, `TimeProvider` (für Testbarkeit).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `BackupUploadSessionService` (`VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs`) | Klasse (Singleton) | Verwaltet Upload-Sessions und Temp-Dateien: `BeginSessionAsync`, `GetSession`, Validierung von `Upload-Length` gegen `MaxUploadSizeBytes`, Lazy-Cleanup verwaister Sessions/Temp-Dateien. Abhängigkeiten: `IServiceScopeFactory` (löst den Scoped-`IBackupOptionsProvider` pro `BeginSessionAsync` in einem eigenen Scope auf), `TimeProvider`, `ILogger`; Temp-Ablage unter `Path.GetTempPath()`. |
| `BackupUploadSession` (nested in `BackupUploadSessionService.cs` oder eigene Datei) | Datenmodellklasse (`sealed`, `IAsyncDisposable`) | Session-Zustand und -Verhalten: `Id` (`Guid`), `FileName`, `TotalLength`, `ReceivedBytes`, `TempPath`, geöffneter `FileStream`, `LastAccessUtc`, `SemaphoreSlim` zur Serialisierung der Chunk-Schreibzugriffe, `IsCompleted`; Methode `AppendChunkAsync(Stream source, long contentLength, CancellationToken)` (Seek + limitiertes Kopieren, `ReceivedBytes`-Fortschaltung, Offset-/Overflow-Validierung) — das Schreiben liegt bei der Session, weil `SemaphoreSlim` und `FileStream` dort gekapselt sind. |
| `BackupUploadStatusResponse` | Datenmodellklasse (Record) | JSON-Antwort des Status-Endpunkts: `UploadId`, `FileName`, `UploadLength`, `UploadOffset`. |
| `backupUpload.js` (`VideoWebPlayer/wwwroot/js/backupUpload.js`) | JS-Modul (`window.backupUpload`) | Chunked `fetch()`-Upload (`Blob.slice()`, Konstante `CHUNK_SIZE = 128 MiB`, für Tests über die globale Variable `window.backupUploadChunkSizeBytes` überschreibbar — wird beim Senden gelesen, da `Page.AddInitScriptAsync` vor dem Modul läuft), Header-Metadaten, 308-/Fehler-Resume, Fortschritts-Rendering im übergebenen Container, `localStorage`-Persistenz der `Upload-Id`, Abort per `AbortController`. |
| `BackupUploadSessionServiceTests` (`VideoWebPlayer.Tests/Services/Backups/`) | Testklasse | Unit-Tests für Session-Service (siehe Tests). |
| `BackupUploadE2ETests` (`VideoWebPlayer.Tests/`) | Testklasse (`[Trait("Category", "E2E")]`, Playwright) | E2E-Nachweis des Admin-Upload-Flusses (siehe Tests). |

## Änderungen an bestehenden Klassen

### `BackupsController` (Controller, `VideoWebPlayer/Controllers/BackupsController.cs`)

- **Neue Abhängigkeit:** `BackupUploadSessionService` per Konstruktor.
- **Neue Methoden:** `UploadChunk(CancellationToken)` — `[HttpPost("upload/chunk")]`, `[DisableRequestSizeLimit]`; Ablauf wie oben beschrieben (Header-Validierung, Session-Handling, 308/204/200/4xx-Antworten). `GetUploadStatus(Guid uploadId)` — `[HttpGet("upload/{uploadId:guid}")]`; liefert 200 + `BackupUploadStatusResponse` oder 404.
- **Entfernte Methoden:** `Upload(CancellationToken)` (`POST upload`, Zeilen 64–89) wird vollständig entfernt — multipart-Pfad entfällt, ebenso `[RequestFormLimits]` und `Request.Form`-Zugriff. `RedirectWithUploadError` entfällt mit (JS übernimmt die Fehleranzeige via `backupError`-Navigation).
- **Geändert:** Klassenkommentar/Using-Liste (`Microsoft.AspNetCore.Http.Features` bleibt für `DisableRequestSizeLimit`).
- Unverändert: `[ApiController]`, `[Authorize(Policy = "AdminOnly")]`, Basisroute `admin/backups/api`, `Create`, `Download`, `IAntiforgery`-Prüfung per `ValidateRequestAsync`.

### `VideoWebPlayerBackupFacade` (Service, `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`)

- **Geänderte Methoden:** `ImportUploadAsync(Stream stream, string fileName, string? userId, CancellationToken)` (Zeilen 86–150) wird zu `ImportUploadFileAsync(string tempFilePath, string fileName, string? userId, CancellationToken)` — die Methode öffnet die übergebene Temp-Datei selbst (`FileStream` lesend), führt die bestehende Validierung (`.bak`-Endung, `manifest.json` via `ZipArchive`, nicht leer) in-place aus und verschiebt die Datei per `File.Move(tempPath, targetPath, overwrite: true)` in den `StoragePath` statt sie über eine zweite Temp-Kopie zu kopieren. `overwrite: true` ist Pflicht: Der bisherige `FileMode.Create`-Zielstream überschreibt gleichnamige Dateien stillschweigend; `File.Move` ohne `overwrite` würde bei Namensgleichheit `IOException` werfen und das Verhalten ungewollt ändern (siehe Designentscheidung „Kollisionsverhalten beim Import"). Grund: Der einzige Aufrufer (`BackupsController.Upload`) entfällt; der neue Abschluss übergibt den Session-Temp-Pfad und vermeidet so eine zusätzliche Vollkopie der 6-GB-Datei. Bei Fehlschlag bleibt die Temp-Datei erhalten (Cleanup durch `BackupUploadSessionService`).

### `Backups.razor` (Komponente, `VideoWebPlayer/Components/Pages/Admin/Backups.razor`)

- **Ersetzt:** Upload-`<form>`-Block (Zeilen 240–250) durch: `<input type="file" @ref="backupFileInput" accept=".bak">`, Upload-`<button @onclick="StartUploadAsync">`, einen Upload-Container (`@ref="uploadContainer"`) für Fortschrittsbalken/Statustext/Abbrechen-Button; der Hinweistext `Maximal @FormatBytes(settingsModel.MaxUploadSizeBytes)` bleibt.
- **Neue Injects:** `IJSRuntime`, `PersistentComponentState ApplicationState`.
- **Neue Methoden:** `StartUploadAsync` — ruft `JS.InvokeVoidAsync("backupUpload.start", backupFileInput, uploadContainer, antiforgeryRequestToken)`; Fehlerfall (kein Token/keine Datei) → `errorMessage`.
- **Geänderte Methoden:** `CreateAntiforgeryRequestToken` — Token-Persistenz über `ApplicationState.PersistAsJson("antiforgeryToken", …)`/`TryTakeFromJson` nach dem Muster aus `Updates.razor` (Zeilen 309–325), inkl. `PersistingComponentStateSubscription`-Feld.
- **Geänderte Methoden:** `Dispose` — zusätzlich `_stateSubscription.Dispose()`.
- Bestehende UI-Muster, die wiederverwendet werden: `antiforgeryRequestToken`-Erzeugung (Zeilen 384, 391–397), `ApplyQueryMessages` für `backupStatus`/`backupError` (Zeilen 399–406), Disabled-Logik `isBusy || IsRestoreActive`, `FormatBytes`. Eine Auswahl benannter Entitäten ist nicht Teil der Anforderung — das `<input type="file">` bleibt die geforderte Interaktion.

### `App.razor` (Komponente, `VideoWebPlayer/Components/App.razor`)

- **Neu:** `<script src="js/backupUpload.js"></script>` im `<head>` (Zeilen 12–16, analog zu den übrigen `window.<name>`-Modulen).

### `ServiceCollectionExtensions` (DI-Registrierung, `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`)

- **Neu:** `services.AddSingleton<BackupUploadSessionService>();` im Backup-Block (Zeilen 237–250). Singleton ist erforderlich, da Sessions über Scoped-Requests hinweg bestehen müssen.

### `VideoWebPlayer.csproj` (Projektdatei)

- **Neu:** `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` im Property-Block — die beim Publish generierte `web.config` verwendet dann OutOfProcess-Hosting, sodass Kestrel den Request-Body ohne IIS-InProcess-Limit entgegennimmt. Voraussetzung für große Uploads unter IIS; unter Linux/systemd ohne Effekt.

### `Program.cs` (Host-Start, `VideoWebPlayer/Program.cs`)

- **Neu:** `builder.WebHost.ConfigureKestrel((context, options) => …)` im Anschluss an `builder.AddVideoWebPlayerServices()` (vor `builder.Build()`, Zeile 32): liest `context.Configuration["Kestrel:Limits:MaxRequestBodySize"]`; ist der Schlüssel gesetzt, wird `options.Limits.MaxRequestBodySize` auf `null` gesetzt, wenn der Wert `<= 0` ist, sonst auf den konfigurierten Wert. Ist der Schlüssel nicht gesetzt (z. B. Development/Testing), bleibt der Kestrel-Default unverändert. Grund: Der `KestrelConfigurationLoader` bindet `Limits` nicht aus der Konfiguration (nur `Endpoints`/`EndpointDefaults`/Zertifikate); ohne diesen Schritt wäre der appsettings-Eintrag wirkungslos.

### `appsettings.Production.json`

- **Neu:** `"Limits": { "MaxRequestBodySize": 0 }` unter `Kestrel` — unbegrenzte Request-Body-Größe produktiv (IIS OutOfProcess/ANCM sowie Linux/systemd). Wirksam wird der Eintrag ausschließlich über die explizite `ConfigureKestrel`-Bindung in `Program.cs` — Kestrel liest `Limits` nicht selbst aus der Konfiguration, und `0` wird dort als Projektkonvention „unbegrenzt" interpretiert (Kestrel-Semantik: `MaxRequestBodySize = null` = unbegrenzt; ein gebundener `0` wäre „0 Bytes"). Bestehende `Kestrel:Endpoints` bleiben.

### `BackupsControllerAuthorizationTests` (Testklasse, `VideoWebPlayer.Tests/`)

- **Geändert:** `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` → prüft `UploadChunk`: `[HttpPost("upload/chunk")]`, `DisableRequestSizeLimitAttribute` vorhanden, `RequestFormLimitsAttribute` nicht vorhanden.
- **Neu:** Test für `GetUploadStatus` (`[HttpGet("upload/{uploadId:guid}")]`).

### `docs/help/backups.md` (Dokumentation)

- **Geändert:** Abschnitt „Upload" — beschreibt chunkbasierten Upload mit Fortschrittsanzeige und Wiederaufnahme statt Formularpost; Hinweis, dass das anwendungsseitige Limit (`MaxUploadSizeBytes`) serverseitig gilt; Deployment-Hinweis für IIS (OutOfProcess-Hosting, `requestFiltering`/`maxAllowedContentLength` darf nicht greifen) und systemd (keine Zusatzlimits). Der veraltete Hinweis „Standard ist 512 MB" wird korrigiert (konfigurierter Standard 5 GiB).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Content-Type` | Muss `application/octet-stream` sein (Chunk-Endpunkt) | 415 Unsupported Media Type |
| `Upload-Name` | Pflicht bei Session-Erstellung; URI-decodiert, nicht leer, `Path.GetFileName(name) == name` | 400 |
| `Upload-Length` | Pflicht bei Session-Erstellung; `> 0` und `<= MaxUploadSizeBytes` | 400 bzw. 413 Payload Too Large |
| `Upload-Id` | Falls vorhanden: gültige GUID einer bekannten, nicht abgelaufenen Session | 404 (Client startet neu) |
| `Upload-Offset` | Muss `session.ReceivedBytes` entsprechen | 308 + `Upload-Offset`-Header (erwartete Position) |
| `Upload-Offset` + `Content-Length` | Darf `session.TotalLength` nicht überschreiten | 400 |
| `Upload-Name`/`Upload-Length` bei Folge-Chunks | Müssen den Session-Werten entsprechen (falls gesendet) | 409 |
| Antiforgery-Token | Header `RequestVerificationToken` muss gültig sein (`ValidateRequestAsync`) | 400 |
| Dateiinhalt (bestehend) | `.bak`-Endung (wird erzwungen), ZIP mit `manifest.json`, nicht leer — in `ImportUploadFileAsync` | 400 + `backupError`-Navigation |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Kestrel:Limits:MaxRequestBodySize` in `appsettings.Production.json` | `long` (Konvention: `0` = unbegrenzt) | `0` | Kein serverseitiges Body-Limit unter Kestrel hinter IIS (OutOfProcess) und unter Linux/systemd; alternativ per Umgebungsvariable `Kestrel__Limits__MaxRequestBodySize=0` setzbar. **Wirksamkeitsmechanismus:** explizite `ConfigureKestrel`-Bindung in `Program.cs` (`0` → `MaxRequestBodySize = null`), da Kestrel `Limits` nicht selbst aus der Konfiguration bindet. |
| `Backups:MaxUploadSizeBytes` / `BackupSettings.MaxUploadSizeBytes` (bestehend) | `long` | 5 GiB | Bleibt die fachliche Upload-Obergrenze; wird jetzt serverseitig im `BackupUploadSessionService` durchgesetzt. |
| `BackupUploadSessionService.Chunk…`/`SessionTimeout` | Konstanten (keine Konfiguration) | 24 h Timeout; Chunk-Größe 128 MiB als Client-Konstante | Annahme der Anforderung: keine neue Konfigurationssektion erforderlich. |

## Seiteneffekte und Risiken

- **Antiforgery-Token im interaktiven Circuit:** `Backups.razor` erzeugt das Token bisher nur über `IHttpContextAccessor` — im InteractiveServer-Circuit ist `HttpContext` `null`. Ohne `PersistentComponentState`-Persistenz (Muster `Updates.razor`) wäre jeder JS-Upload ungültig. Die Umstellung des Token-Abrufs ist Pflichtbestandteil, kein Nebenpunkt.
- **`MaxUploadSizeBytes` wird erstmals durchgesetzt:** Uploads oberhalb des administrierten Limits wurden bisher nicht blockiert; jetzt → 413. Gewolltes Verhalten, aber sichtbare Änderung.
- **Speicherbedarf während des Imports:** Die Session-Temp-Datei wird per `File.Move` in den `StoragePath` verschoben — auf demselben Volume ohne Zusatzkopie; bei unterschiedlichen Volumes (`Path.GetTempPath()` ≠ `Data/Backups`-Volume) entsteht während des Moves eine Kopie, also transient doppelter Platzbedarf für eine Datei.
- **Gleichnamige Archiv-Backups werden überschrieben:** `File.Move(…, overwrite: true)` ersetzt eine bereits im `StoragePath` vorhandene gleichnamige `.bak` — entspricht dem bisherigen `FileMode.Create`-Verhalten, wird nun aber explizit festgelegt (siehe Designentscheidung). Ein Upload unter dem Namen eines bestehenden Backups ersetzt dieses; der ersetzte Stand ist nur noch über den Historieneintrag nachvollziehbar.
- **Kestrel-Default-Limit in Nicht-Produktionsumgebungen:** `Kestrel:Limits:MaxRequestBodySize` greift nur dort, wo der Schlüssel gesetzt ist (Production). In Development/Testing bleibt der Kestrel-Default (~30 MB) bestehen — für den Upload-Endpunkt irrelevant, weil `[DisableRequestSizeLimit]` das Limit pro Request über `IHttpMaxRequestBodySizeFeature` aufhebt; andere Endpunkte behalten ihr Default-Limit.
- **Sessions überleben keinen App-Neustart:** In-Memory-Registry geht verloren; Clients erhalten 404 auf die Status-Abfrage und starten neu; verwaiste Temp-Dateien werden lazy aufgeräumt.
- **`RestoreInProgressMiddleware`:** lässt `/admin/backups/api/*` während eines Restores durch — unverändert; Upload ist reine Dateiablage ohne DB-Schreibzugriff, daher unproblematisch.
- **IIS-Betrieb:** InProcess-Hosting oder `requestFiltering`/`maxAllowedContentLength` (IIS-Default ~30 MB) würden den Upload trotzdem begrenzen — OutOfProcess + angepasstes `maxAllowedContentLength` ist Deployment-Voraussetzung; die csproj-Eigenschaft wird gesetzt (Schritt 6) und der Hinweis in `docs/help/backups.md` dokumentiert (Schritt 9).
- **Bestehender Test bricht:** `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` assertiert `[RequestFormLimits]` — wird angepasst (siehe Tests).
- **`Create`-Endpunkt:** bleibt ein klassischer Formularpost mit hidden `__RequestVerificationToken` — unverändert und unabhängig vom Upload-Pfad.

## Umsetzungsreihenfolge

1. **`BackupUploadSession` + `BackupUploadSessionService` anlegen**
   - Voraussetzungen: Keine neuen Pakete — `IServiceScopeFactory` (DI-Builtin), `IBackupOptionsProvider` (msTools.Backup, Scoped), `TimeProvider.System` (bereits registriert), `ILogger` sind vorhanden.
   - Beschreibung: Singleton-Service mit `ConcurrentDictionary`-Registry, `BeginSessionAsync` (Validierung Name/Länge/`MaxUploadSizeBytes` — `IBackupOptionsProvider` wird pro Aufruf über `using var scope = _scopeFactory.CreateScope()` aufgelöst, Muster `ManualBackupJobService`; Temp-Datei `vwp-backup-upload-{id:N}.tmp` in `Path.GetTempPath()`), `GetSession`, Lazy-Cleanup der 24-h-Timeout-Sessions und verwaister Temp-Dateien. `BackupUploadSession` erhält `AppendChunkAsync` (`FileStream.Seek` + limitiertes Kopieren aus `Request.Body`, `ReceivedBytes`-Fortschaltung, serialisiert über das sessioneigene `SemaphoreSlim`).

2. **DI-Registrierung**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `services.AddSingleton<BackupUploadSessionService>()` in `ServiceCollectionExtensions.AddVideoWebPlayerServices` (Backup-Block).

3. **`BackupsController` umbauen**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `Upload`/`RedirectWithUploadError` entfernen; `UploadChunk` (`POST upload/chunk`, `[DisableRequestSizeLimit]`, Header-Auswertung, 400/404/409/413/308/204/200) und `GetUploadStatus` (`GET upload/{uploadId:guid}`) hinzufügen; `VideoWebPlayerBackupFacade.ImportUploadAsync` zu `ImportUploadFileAsync(tempFilePath, …)` ändern (Validierung in-place + `File.Move(tempPath, targetPath, overwrite: true)` statt Stream-Kopie) und im Abschluss mit `session.TempPath` aufrufen; `BackupUploadStatusResponse`-Record anlegen.

4. **JS-Modul `backupUpload.js` + Einbindung**
   - Voraussetzungen: Schritt 3 (Endpunkte festlegen); `wwwroot/js`-`window.<name>`-Muster vorhanden (`continueWatching.js`); keine Build-Pipeline nötig (statische Datei).
   - Beschreibung: `window.backupUpload` mit `start(inputEl, containerEl, antiforgeryToken)`, Chunking via `Blob.slice()` (128 MiB, überschreibbar über die globale Variable `window.backupUploadChunkSizeBytes`, die das Modul beim Senden liest), Header-Protokoll, 308-/Status-Resume, `localStorage`-Persistenz, Fortschritts-/Status-Rendering + Abbrechen per `AbortController`, Abschluss-Navigation mit `backupStatus`/`backupError`; `<script src="js/backupUpload.js"></script>` in `App.razor` ergänzen.

5. **`Backups.razor` umbauen**
   - Voraussetzungen: Schritt 4; Vergleichsmuster `PersistentComponentState` in `Updates.razor` vorhanden.
   - Beschreibung: multipart-`<form>` durch File-Input + Button + Upload-Container ersetzen; `IJSRuntime`/`PersistentComponentState` injizieren; `StartUploadAsync`; Token-Persistenz wie `Updates.razor`; `Dispose` um `_stateSubscription.Dispose()` erweitern.

6. **Produktivkonfiguration + Hosting-Modell + Kestrel-Bindung**
   - Voraussetzungen: Keine.
   - Beschreibung: `Kestrel:Limits:MaxRequestBodySize: 0` in `appsettings.Production.json` ergänzen; `builder.WebHost.ConfigureKestrel((context, options) => …)` in `Program.cs` ergänzen, das `Kestrel:Limits:MaxRequestBodySize` explizit aus der Konfiguration liest und anwendet (`<= 0` → `null` = unbegrenzt, `> 0` → Bytes; Schlüssel nicht gesetzt → Kestrel-Default unverändert) — ohne diese Bindung ist der appsettings-Eintrag wirkungslos, da Kestrel `Limits` nicht aus der Konfiguration lädt; `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` in `VideoWebPlayer.csproj` ergänzen (die generierte `web.config` nutzt dann OutOfProcess; der Deployment-Hinweis zu `requestFiltering`/`maxAllowedContentLength` folgt in Schritt 9).

7. **Unit-/Attributtests**
   - Voraussetzungen: Schritte 1–3, 5; Testinfrastruktur vorhanden (xunit.v3, EF InMemory, `FakeWebHostEnvironment`-Muster aus `VideoWebPlayerBackupDataTests`).
   - Beschreibung: `BackupUploadSessionServiceTests` neu; `BackupsControllerAuthorizationTests` anpassen/erweitern; `ImportUploadFileAsync`-Tests neu (bisher ungetesteter Pfad, jetzt Abschluss des Uploads).

8. **E2E-Tests**
   - Voraussetzungen: Schritte 3–6; Playwright-Infrastruktur vorhanden (`WebApplicationFactory` + `UseKestrel`, Muster `UpdatesPageE2ETests` inkl. `SeedAdminAsync`); im E2E-Setup `Backups:Path` auf ein Temp-Verzeichnis setzen.
   - Beschreibung: `BackupUploadE2ETests` mit den unten genannten Szenarien; `SeedAdminAsync` nach `UpdatesPageE2ETests`-Muster plus neuer Hilfsmethode `SeedRegularUserAsync` (regulärer Benutzer ohne `IsAdmin`-Property/-Claim) und parametrisiertem `LoginAsync(email, password)`; `CreateValidBackupFileAsync` erzeugt eine echte `.bak` über `IBackupService.StoreAsync` im DI-Scope des gehosteten Test-Servers und stellt sie dem Browser unter eigenem Namen aus einem Temp-Verzeichnis bereit (Kollisionsvermeidung, siehe Tests).

9. **Dokumentation**
   - Voraussetzungen: Schritte 3–6 (endgültiges Verhalten).
   - Beschreibung: `docs/help/backups.md` Abschnitt „Upload" aktualisieren inkl. IIS-/systemd-Hinweis.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `BeginSessionAsync_CreatesTempFileAndRejectsInvalidInput` | `BackupUploadSessionServiceTests` | Session mit Temp-Datei wird angelegt; leerer/unsicherer `Upload-Name`, `Upload-Length <= 0` → Fehler. |
| `BeginSessionAsync_RejectsLengthAboveMaxUploadSize` | `BackupUploadSessionServiceTests` | `Upload-Length > MaxUploadSizeBytes` → Limit-Fehler (→ 413 im Controller); der Fake-`IBackupOptionsProvider` wird in einer Test-`ServiceCollection` registriert, deren `BuildServiceProvider()` die `IServiceScopeFactory` für den Service liefert. |
| `AppendChunk_WritesAtOffsetAndTracksReceivedBytes` | `BackupUploadSessionServiceTests` | Chunk landet an deklariertem Offset in der Temp-Datei, `ReceivedBytes` wird aktualisiert; Inhalt bytegenau prüfbar. |
| `AppendChunk_RejectsMismatchedOffset` | `BackupUploadSessionServiceTests` | `Upload-Offset != ReceivedBytes` → Mismatch-Ergebnis mit erwartetem Offset (Basis für 308). |
| `AppendChunk_RejectsOverflowBeyondTotalLength` | `BackupUploadSessionServiceTests` | `Offset + ChunkLen > TotalLength` → Validierungsfehler (400). |
| `GetSession_ReturnsNullForUnknownOrExpired` | `BackupUploadSessionServiceTests` | Unbekannte/abgelaufene `Upload-Id` → kein Ergebnis (→ 404); `TimeProvider`-Fake (`IncrementingTimeProvider`) steuert Ablauf. |
| `Cleanup_RemovesExpiredSessionsAndOrphanedTempFiles` | `BackupUploadSessionServiceTests` | Sessions/Temp-Dateien älter `SessionTimeout` werden beim nächsten Zugriff entfernt; aktive bleiben erhalten. |
| `Sessions_SameFileName_DoNotCollide` | `BackupUploadSessionServiceTests` | Zwei parallele Sessions mit gleichem `Upload-Name` erhalten eigene IDs/Temp-Dateien. |
| `ImportUploadFileAsync_*` (mehrere Tests) | `VideoWebPlayerBackupFacadeTests` (neu) | Gültige `.bak`-Temp-Datei (ZIP + `manifest.json`) → Erfolg + Datei per `File.Move` im `StoragePath` + Historieneintrag, Temp-Datei anschließend nicht mehr vorhanden; bereits vorhandene gleichnamige Zieldatei → wird überschrieben (`overwrite: true`, dokumentiertes Kollisionsverhalten); fehlendes `manifest.json`/leere Datei → `Failure` und Temp-Datei bleibt; Name ohne `.bak` → Endung ergänzt. Fakes nach Muster `RestoreBackupJobServiceTests` (`NoopBackupOptionsProvider`, `FakeWebHostEnvironment`, `RecordingBackupService`-Analog). |
| `UploadChunkEndpoint_IsExposedAsUnlimitedStreamPost` | `BackupsControllerAuthorizationTests` | `[HttpPost("upload/chunk")]`, `DisableRequestSizeLimitAttribute` vorhanden, `RequestFormLimitsAttribute` abwesend. |
| `GetUploadStatusEndpoint_IsExposedAsGet` | `BackupsControllerAuthorizationTests` | `[HttpGet("upload/{uploadId:guid}")]` auf `GetUploadStatus`. |
| `SeedRegularUserAsync` (Hilfsmethode) | `BackupUploadE2ETests` | Erzeugt per `UserManager.CreateAsync` einen authentifizierten regulären Benutzer (`IsAdmin = false`, kein `IsAdmin`-Claim) — Fixture für den Nicht-Admin-Sichtbarkeitstest; Muster `SeedAdminAsync` aus `UpdatesPageE2ETests` (Z. 441–461). |
| `LoginAsync(email, password)` (Hilfsmethode) | `BackupUploadE2ETests` | Parametrisiertes Login über `/Account/Login` (`#email`/`#password`), Muster `UpdatesPageE2ETests` (Z. 344–351); erlaubt Admin- und Nicht-Admin-Anmeldung im selben Browserkontext. |
| `CreateValidBackupFileAsync` (Hilfsmethode) | `BackupUploadE2ETests` | Erzeugt im DI-Scope der gehosteten App über `IBackupService.StoreAsync` eine echte `.bak` im `StoragePath`, kopiert sie unter einem eigenen Upload-Namen (z. B. `upload-{guid}.bak`) in ein Temp-Verzeichnis außerhalb des `StoragePath` und liefert diesen Pfad für `IPage.SetInputFilesAsync` — `SetInputFilesAsync` übernimmt den realen Dateinamen, daher vermeidet die Kopie die garantierte Namenskollision mit der bereits archivierten Datei. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` (`BackupsControllerAuthorizationTests`) | Assertiert `[HttpPost("upload")]` + `RequestFormLimitsAttribute` auf der entfallenen Methode `Upload`; wird durch die neuen Attributtests ersetzt/angepasst. |

### E2E-Tests (primärer Funktionsnachweis)

Der Benutzerfluss „Admin wählt Backupdatei auf `/admin/backups` und lädt sie hoch" ist nur über die UI erreichbar und muss E2E-nachgewiesen werden. Neue Testklasse `BackupUploadE2ETests` (`[Trait("Category", "E2E")]`) nach dem Muster `UpdatesPageE2ETests` (gehostete App via `WebApplicationFactory` + `UseKestrel`, `SeedAdminAsync` mit `IsAdmin`-Claim, Playwright Chromium); `Backups:Path` wird per `UseSetting` auf ein Temp-Verzeichnis gesetzt.

Fixture/Hilfsmethoden in `BackupUploadE2ETests` (Muster `UpdatesPageE2ETests` Z. 344–351, 441–461):

- `SeedAdminAsync` — Admin mit `IsAdmin`-Property + `IsAdmin`-Claim (übernommenes Muster).
- `SeedRegularUserAsync` — **neu:** erzeugt einen authentifizierten regulären Benutzer (`UserManager.CreateAsync`, `EmailConfirmed = true`, `IsAdmin = false`, **kein** `IsAdmin`-Claim) für den Sichtbarkeits-E2E. Entscheidung: Der Nicht-Admin-Fall wird mit eingeloggtem regulärem Benutzer getestet, nicht anonym — die Anforderung zielt auf angemeldete Nicht-Admins (die Seite hat kein `[Authorize]`, `isAdmin`-Prüfung in `OnInitializedAsync` Z. 374–382 zeigt „Nicht autorisiert"); anonyme Aufrufer würden ohnehin auf die Login-Seite umgeleitet und wären ein anderer Pfad.
- `LoginAsync(email, password)` — parametrisierte Variante des `UpdatesPageE2ETests`-Logins (`/Account/Login`, `#email`/`#password`), damit derselbe Browserkontext je nach Test als Admin oder regulärer Benutzer angemeldet wird.
- `CreateValidBackupFileAsync` — erzeugt im DI-Scope der gehosteten App über `IBackupService.StoreAsync` eine echte `.bak` im `StoragePath` und kopiert sie anschließend unter eigenem Namen (z. B. `upload-{guid:N}.bak`) in ein Temp-Verzeichnis außerhalb des `StoragePath`; dieser Pfad wird an `IPage.SetInputFilesAsync` übergeben. Die Kopie unter abweichendem Namen ist nötig, weil `SetInputFilesAsync` den realen Dateinamen übernimmt: Ohne sie wäre `Upload-Name` identisch mit der bereits archivierten Datei — der Happy-Path-Test hinge dann am Überschreibpfad statt am Normalfall (das Überschreiben selbst wird in den `ImportUploadFileAsync`-Unit-Tests nachgewiesen).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: Admin wählt gültige `.bak`, klickt „Backup hochladen" → Fortschrittsanzeige läuft, Erfolgsmeldung (`backupStatus`), Datei erscheint im Archiv, Historieneintrag „Upload" | `BackupUploadE2ETests.BackupUpload_ValidFile_ShowsProgressAndImportsBackup` | Octet-stream-Upload funktioniert End-to-End inkl. JS-Modul, Antiforgery-Header, Abschluss-Import und UI-Rückmeldung | Gesamter Benutzerfluss über Datei-Input + fetch + Blazor-Seite — Unit-Tests decken JS/Browser-Interaktion nicht ab. |
| Pflicht | Multi-Chunk: `Page.AddInitScriptAsync("window.backupUploadChunkSizeBytes = …")` setzt die Override-Variable klein (läuft vor dem Laden von `backupUpload.js`; das Modul liest sie erst beim Senden — eine Modul-Eigenschaft wie `window.backupUpload.chunkSizeBytes` existiert zum Init-Zeitpunkt noch nicht), Upload einer Datei > Chunk-Größe → mehrere Chunk-Requests, Erfolg | `BackupUploadE2ETests.BackupUpload_LargeFile_UsesMultipleChunks` | Chunking-Pfad (mehrere Requests, Offset-Fortschaltung) funktioniert im Browser | Chunk-Sequenz und Offset-Handshake entstehen nur im Zusammenspiel JS↔Server. |
| Pflicht | Resume: `Page.RouteAsync` auf `**/upload/chunk` bricht einen Chunk-Request ab (`AbortAsync`), Route danach entfernt → Upload setzt über Status-Abfrage fort und endet erfolgreich; Fortschrittsanzeige zeigt Unterbrechung/Fortsetzung | `BackupUploadE2ETests.BackupUpload_Interrupted_ResumesFromServerOffset` | 308-/Status-Resume verhält sich für den Anwender korrekt | Resume ist Kernanforderung und nur über echten HTTP-Abbruch im Browser glaubwürdig. |
| Pflicht | Fehlerfall: ungültige Datei (ZIP ohne `manifest.json`) → `backupError`-Meldung, kein Eintrag im Archiv | `BackupUploadE2ETests.BackupUpload_InvalidFile_ShowsError` | Serverseitige Fachvalidierung erreicht die UI | Fehlerfall ist über die UI auslösbar und anwendersichtbar. |
| Pflicht | Limit: `Backups:MaxUploadSizeBytes` im Test-Host klein setzen → Upload darüber → Fehlermeldung, kein Import | `BackupUploadE2ETests.BackupUpload_ExceedingLimit_ShowsError` | 413/Fehler wird in der UI gemeldet | Anwendersichtbare Ablehnung; prüft Durchsetzung des administrierbaren Limits. |
| Pflicht | Sichtbarkeit: eingeloggter regulärer Benutzer (via `SeedRegularUserAsync` + `LoginAsync(userEmail, userPassword)`) sieht „Nicht autorisiert" und kein Upload-Control auf `/admin/backups` | `BackupUploadE2ETests.BackupUpload_NonAdmin_SeesNoUploadControl` | Berechtigungsregel bleibt wirksam | Sichtbarkeitsregel der UI; benötigt die neue Nicht-Admin-Fixture. |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — es existiert kein E2E-Test für die Backup-Seite; die übrigen 34 E2E-Tests berühren den Upload-Pfad nicht.

## Offene Punkte

Keine — alle Entscheidungen vom Anwender bestätigt (siehe Plan).
