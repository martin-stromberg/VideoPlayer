# Umsetzungsplan: Backups

## Zielbild

Die Backupfunktion wird in einer neuen Klassenbibliothek `msTools.Backup` gekapselt und in `VideoWebPlayer` ueber Host-Adapter integriert. Administratoren erhalten unter `/admin/backups` eine Verwaltungsseite fuer manuelle Backups, automatische GVS-Backups, Download, Upload und Restore.

Die Bibliothek bleibt projektunabhaengig: Sie kennt keine EF-Entities und keine `VideoWebPlayer`-Typen. `VideoWebPlayer` stellt die konkreten Daten ueber einen Backup-Datenprovider bereit und koordiniert eigene Hintergrundprozesse waehrend eines Restores.

## Konservative Entscheidungen

- Backupumfang: Datenbankdaten aus `ApplicationDbContext` inklusive ASP.NET-Identity-Tabellen. Genre-Icon-Dateien unter `wwwroot/images/genres` werden als optionaler Host-Anhang mitgesichert, wenn das Verzeichnis existiert. Logs, Demo-/Seed-Dateien und Medienquelleninhalte werden nicht gesichert.
- Zielframework: `msTools.Backup` und `msTools.Backup.Tests` verwenden `net10.0`, passend zur bestehenden Solution.
- Backupformat: ZIP mit `manifest.json`, `data.json` und optionalem `files/`-Unterbaum. Uploads ohne gueltiges Manifest werden abgelehnt.
- Standardpfad: `Data/Backups`, relativ zum Content Root. Der Pfad ist ueber App-Konfiguration und Admin-UI aenderbar.
- Standardaufbewahrung GVS: Sohn taeglich 7 Generationen, Vater woechentlich 4 Generationen, Grossvater monatlich 12 Generationen. Manuelle und hochgeladene Backups bleiben erhalten, bis ein Administrator sie loescht.
- Automatische Ausfuehrung: einmal pro Stunde pruefen, ob ein faelliger Sohn-/Vater-/Grossvater-Slot existiert. Der Dienst laeuft nur, wenn automatische Backups aktiviert sind.
- Restore-Sperre: kooperativer In-Process-Gate-Service. Scanner, Klassifizierung, Continue-Watching-Worker und manuelle Scan-Aktionen duerfen waehrend Restore keine neuen Schreiboperationen starten; laufende Operationen werden abgewartet. Kein harter Prozessabbruch.
- Audit: Backup- und Restore-Aktionen werden serverseitig geloggt und in einer einfachen EF-Historie angezeigt. Das erfuellt die Nachvollziehbarkeit ohne separates Audit-Subsystem.
- Grosse Dateien: Upload-Limit als Option, Standard 512 MB. Keine Fortschrittsanzeige im ersten Schnitt; Statusmeldungen und Fehlermeldungen reichen.

## Arbeitspaket 1: Solution und Bibliothek

1. Neues Projekt `msTools.Backup/msTools.Backup.csproj` erstellen.
2. Projekt in `VideoPlayer.sln` eintragen.
3. `VideoWebPlayer/VideoWebPlayer.csproj` um ProjectReference auf `..\msTools.Backup\msTools.Backup.csproj` erweitern.
4. Neues Testprojekt `msTools.Backup.Tests/msTools.Backup.Tests.csproj` erstellen, in die Solution eintragen und auf `msTools.Backup` referenzieren.
5. Public API der Bibliothek mit XML-Dokumentation versehen, damit `WarningsAsErrors` im Webprojekt nicht durch referenzierte Typen auffaellt.

## Arbeitspaket 2: msTools.Backup Kernmodell

Neue Typen in `msTools.Backup`:

- `BackupOptions`: Speicherpfad, Upload-Limit, Dateinamenschema, automatische Backups, GVS-Retention.
- `BackupScheduleOptions`: Aktiviert, Pruefintervall, Sohn-/Vater-/Grossvater-Frequenzen.
- `BackupRetentionOptions`: Anzahl Sohn/Vater/Grossvater.
- `BackupGeneration`: `Manual`, `Uploaded`, `Son`, `Father`, `Grandfather`.
- `BackupDescriptor`: Dateiname, Pfad, Groesse, Erstellzeit, Generation, Providerkennung, Formatversion, Gueltigkeitsstatus.
- `BackupManifest`: Formatversion, Providerkennung, App-Name, Erstellzeit, Generation, Payload-Eintraege, optionale Pruefsummen.
- `BackupValidationResult`: gueltig/ungueltig plus Fehlermeldungen.
- `BackupCreateRequest`, `BackupRestoreRequest`, `BackupRestoreContext`.
- `BackupOperationResult`: Erfolg, Meldung, Descriptor, Fehlerdetails.

Interfaces:

- `IBackupDataProvider`
  - `string ProviderId { get; }`
  - `Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken)`
  - `Task<BackupValidationResult> ValidateAsync(Stream source, CancellationToken cancellationToken)`
  - `Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken)`
- `IBackupRestoreGuard`
  - `Task<IAsyncDisposable> EnterRestoreAsync(CancellationToken cancellationToken)`
  - Default-Implementierung als Noop fuer andere Hosts.
- `IBackupStore`
  - Liste, Lesen, Schreiben, Loeschen, Upload-Speichern, Validieren von ZIP-Dateien.
- `IBackupService`
  - `ListBackupsAsync`
  - `CreateBackupAsync`
  - `ValidateUploadAsync`
  - `ImportUploadedBackupAsync`
  - `OpenBackupReadAsync`
  - `RestoreBackupAsync`
  - `ApplyRetentionAsync`

## Arbeitspaket 3: ZIP-Store und Validierung

1. `FileSystemBackupStore` implementieren.
2. ZIP-Erzeugung ueber `System.IO.Compression.ZipArchive`.
3. `manifest.json` immer im Root schreiben.
4. Host-Payload als `data.json` im Root schreiben.
5. Optional gesicherte Dateien unter `files/{relativePath}` schreiben.
6. Upload validieren:
   - Datei ist lesbares ZIP.
   - Keine absoluten Pfade und keine `..`-Segmente in Entry-Namen.
   - `manifest.json` vorhanden und parsebar.
   - `formatVersion` wird unterstuetzt.
   - `providerId` entspricht dem registrierten Provider.
   - `data.json` vorhanden.
   - optionale Pruefsummen stimmen, falls im Manifest enthalten.
7. Dateien atomar speichern: erst temporäre Datei im Backup-Verzeichnis, dann finaler Move.
8. Dateinamen normalisieren: `yyyyMMdd-HHmmss-{generation}-{providerId}.zip`; Uploads erhalten Prefix `uploaded-`.

## Arbeitspaket 4: GVS und automatische Backups

1. `BackupRetentionService` implementieren.
2. Automatisch erstellte Backups werden anhand `BackupGeneration` getrennt aufbewahrt.
3. Retention loescht nur `Son`, `Father`, `Grandfather`, niemals `Manual` oder `Uploaded`.
4. `ScheduledBackupService : BackgroundService` in `msTools.Backup` implementieren:
   - periodisch anhand Optionen pruefen.
   - faellige Generation bestimmen.
   - Backup erstellen.
   - Retention anwenden.
   - Fehler loggen, aber Anwendung nicht beenden.
5. Zeitberechnungen ueber `TimeProvider`, damit Tests deterministisch bleiben.

## Arbeitspaket 5: Registrierungs-API

In `msTools.Backup` Erweiterungen bereitstellen:

- `IServiceCollection AddBackups(Action<BackupOptions> configure)`
- `IServiceCollection AddBackups(IConfigurationSection section)`
- `IApplicationBuilder UseBackups()`

`UseBackups()` darf im ersten Schnitt keine UI erzwingen. Es kann no-op sein oder nur vorbereitete Endpoints registrieren, falls Download/Upload/Restore als Minimal APIs umgesetzt werden. Die konkrete `VideoWebPlayer`-Integration soll weiterhin klar in `UseVideoWebPlayer()` sichtbar sein.

## Arbeitspaket 6: Persistente Host-Konfiguration

1. Neue EF-Entity `BackupSettings` im Webprojekt anlegen:
   - `Id`
   - `StoragePath`
   - `AutomaticBackupsEnabled`
   - `SonRetentionCount`
   - `FatherRetentionCount`
   - `GrandfatherRetentionCount`
   - `MaxUploadSizeBytes`
   - `UpdatedAtUtc`
2. Neue EF-Entity `BackupOperationHistory` anlegen:
   - `Id`
   - `StartedAtUtc`
   - `CompletedAtUtc`
   - `Operation`
   - `FileName`
   - `Generation`
   - `Succeeded`
   - `UserId`
   - `Message`
3. `ApplicationDbContext` um DbSets und Konfiguration erweitern.
4. EF-Migration erstellen.
5. `BackupSettingsService` in `VideoWebPlayer.Services.Backups` implementieren:
   - liefert Defaults aus `Backups:*` oder feste Defaults.
   - persistiert Admin-Aenderungen.
   - stellt `BackupOptions` fuer die Bibliothek bereit.
6. `appsettings.json` um `Backups`-Abschnitt ergaenzen:
   - `Path`: `Data/Backups`
   - `MaxUploadSizeBytes`: `536870912`

## Arbeitspaket 7: VideoWebPlayer Datenprovider

1. `VideoWebPlayerBackupDataProvider : IBackupDataProvider` implementieren.
2. Export:
   - alle Identity- und fachlichen Tabellen aus `ApplicationDbContext` mit `AsNoTracking()` laden.
   - stabiles JSON DTO schreiben, nicht EF ChangeTracker serialisieren.
   - `ProviderId = "VideoWebPlayer.ApplicationDbContext"`.
   - Schema-/Datenversion im Payload fuehren.
   - Genre-Icons optional als Dateianhaenge exportieren.
3. Restore:
   - ausfuehrenden Admin anhand `BackupRestoreContext.UserId` vor dem Loeschen laden.
   - Transaktion starten.
   - fachliche Tabellen und Identity-Tabellen kontrolliert leeren.
   - Daten aus Backup einfuegen.
   - Admin-Erhalt anwenden:
     - Admin im Backup vorhanden: aus Backup eingefuegte Werte beibehalten, `IsAdmin = true` sicherstellen.
     - Admin nicht im Backup vorhanden: gespeicherten Admin wieder einfuegen, `Sources = ""`, `IsAdmin = true`.
   - Genre-Icons aus Backup wiederherstellen, vorhandene Dateien im Genre-Icon-Verzeichnis nur fuer gesicherte relative Pfade ersetzen.
   - `SaveChangesAsync` und Commit.
4. Keine vorhandenen Fachmethoden wie `AddMediaSourceAsync` im Restore verwenden, um Events und Seiteneffekte zu vermeiden.
5. Fehlerfall: Transaktion rollbacken und Gate im `finally` freigeben.

## Arbeitspaket 8: Restore-Gate und Hintergrundprozesse

1. `IBackgroundProcessingGate` und Implementierung `BackgroundProcessingGate` im Webprojekt anlegen.
2. Gate-Funktionen:
   - `EnterOperationAsync(name, cancellationToken)` fuer Scanner/Worker/manuelle Aktionen.
   - `PauseForRestoreAsync(cancellationToken)` blockiert neue Operationen und wartet auf laufende.
   - Status fuer UI/Logs bereitstellen.
3. Adapter `VideoWebPlayerBackupRestoreGuard : IBackupRestoreGuard` implementieren und im DI registrieren.
4. `MediaSourceScanService` vor jedem Scan-/Klassifizierungsdurchlauf ueber Gate absichern.
5. `MediaSourceScanner` und `MediaSourceClassifier` an langen/oeffentlich startbaren Schreibpfaden pruefen oder deren Aufrufer konsequent ueber Gate fuehren.
6. `ContinueWatchingWorker` vor `ProcessBufferedEntryAsync` ueber Gate absichern.
7. Admin-Seiten fuer manuelle Scans (`MediaSourceAdmin`, `MediaSourceExplorer`) so erweitern, dass Restore-Sperre beachtet und eine verstaendliche Meldung angezeigt wird.

## Arbeitspaket 9: Admin-Autorisierung

1. Zentrale Policy `AdminOnly` in `AddAuthorization` ergaenzen: Claim `IsAdmin=True`.
2. Backup-UI prueft weiterhin den Claim wie bestehende Admin-Seiten.
3. Alle serverseitigen Backup-Endpunkte fuer Download, Upload und Restore mit `RequireAuthorization("AdminOnly")` oder `[Authorize(Policy = "AdminOnly")]` schuetzen.
4. Restore-Endpunkt verlangt eine explizite Bestaetigung, z. B. Feld `ConfirmRestore == true` plus Dateiname/OperationId. Ein reiner Browser-Confirm reicht nicht.

## Arbeitspaket 10: VideoWebPlayer Service-Integration

1. In `ServiceCollectionExtensions.AddVideoWebPlayerServices` registrieren:
   - `services.AddBackups(...)`
   - `IBackupDataProvider` als `VideoWebPlayerBackupDataProvider`
   - `IBackupRestoreGuard` als `VideoWebPlayerBackupRestoreGuard`
   - `IBackgroundProcessingGate`
   - `BackupSettingsService`
2. Optionen aus persistierten Einstellungen und `IConfiguration` zusammenfuehren. Falls die Bibliothek `IOptionsMonitor<BackupOptions>` erwartet, eine Host-spezifische Optionsquelle bereitstellen.
3. In `WebApplicationExtensions.UseVideoWebPlayer` `app.UseBackups()` nach Auth/Authorization einhaengen.
4. Fuer Download/Upload/Restore entweder Minimal APIs unter `/admin/backups/api/*` oder einen Controller anlegen. Minimal APIs passen gut, wenn `UseBackups()` Endpoints bereitstellt; Controller passen gut zur bestehenden `MapControllers()`-Struktur.

## Arbeitspaket 11: Admin-UI

Neue Komponente `VideoWebPlayer/Components/Pages/Admin/Backups.razor`:

1. Route `/admin/backups`.
2. Admin-Pruefung analog `ProgramSettings.razor`.
3. Statusbereiche:
   - Erfolgsmeldungen fuer Backup, Upload, Restore, Einstellungen.
   - Fehlermeldungen kontrolliert aus `BackupOperationResult`.
4. Tabelle vorhandener Backups:
   - Dateiname
   - Generation
   - Erstellzeit
   - Groesse
   - Gueltigkeitsstatus
   - Aktionen: Download, Restore
5. Button `Backup erstellen`.
6. Upload per `InputFile` mit ZIP- und Groessenvalidierung.
7. Restore mit Sicherheitsabfrage:
   - erste Aktion waehlt Backup aus.
   - zweiter Button bestaetigt Restore serverseitig.
   - Warntext nennt, dass vorhandene Daten ersetzt werden und das eigene Admin-Konto erhalten bleibt.
8. Einstellungsformular:
   - Speicherpfad
   - automatische Backups aktivieren
   - Sohn/Vater/Grossvater-Aufbewahrung
   - Upload-Limit
9. Historientabelle der letzten Backup-/Restore-Aktionen.
10. `NavMenu.razor` um Eintrag `Backups` im Verwaltungsbereich erweitern.

## Arbeitspaket 12: Fehlerbehandlung und Logging

1. Bibliothek wirft fuer erwartbare Benutzerfehler kontrollierte Exceptions oder liefert `BackupOperationResult`.
2. UI zeigt kurze deutsche Meldungen ohne Stacktrace.
3. Logs enthalten Operation, Datei, Generation, UserId und Dauer.
4. Historie wird bei Erfolg und Fehler geschrieben.
5. Download/Upload/Restore duerfen keine beliebigen Pfade akzeptieren; serverseitig nur bekannte Descriptoren oder validierte Upload-Dateien verwenden.

## Arbeitspaket 13: Tests

`msTools.Backup.Tests`:

1. `FileSystemBackupStore` erstellt ZIP mit Manifest und Payload.
2. Uploadvalidierung lehnt Nicht-ZIP, ZIP ohne Manifest, falsche Providerkennung und Path-Traversal-Entries ab.
3. GVS-Retention loescht nur automatische Generationen und behaelt manuelle/hochgeladene Backups.
4. Scheduled Service erstellt faellige Generationen mit fake `TimeProvider`.
5. Atomare Speicherung hinterlaesst bei Fehler keine halben finalen Dateien.

`VideoWebPlayer.Tests`:

1. `VideoWebPlayerBackupDataProvider` exportiert und restored alle relevanten Tabellen mit SQLite-In-Memory.
2. Restore erhaelt ausfuehrendes Admin-Konto, wenn es im Backup fehlt.
3. Restore aktualisiert ausfuehrendes Admin-Konto aus Backup und stellt `IsAdmin = true` sicher.
4. Restore nutzt Gate und blockiert parallele Schreiboperationen.
5. `BackupSettingsService` liefert Defaults und persistiert Admin-Aenderungen.
6. Autorisierung: Backup-Endpunkte sind ohne Admin-Claim nicht erreichbar.

Validierung:

1. `dotnet restore VideoPlayer.sln`
2. `dotnet build VideoPlayer.sln`
3. `dotnet test VideoPlayer.sln`

## Implementierungsreihenfolge

1. Projekte und Referenzen anlegen.
2. `msTools.Backup` Kernmodelle, Store, Service, Retention und Tests implementieren.
3. EF-Entities, Migration und Settings-Service in `VideoWebPlayer` anlegen.
4. Datenprovider fuer Export/Restore implementieren und mit SQLite-In-Memory testen.
5. Restore-Gate in Hintergrunddienste und manuelle Scanpfade integrieren.
6. Admin-Policy und Endpunkte implementieren.
7. Admin-UI und Navigation umsetzen.
8. Automatischen Backup-Service aktivieren.
9. Gesamtbuild und Tests ausfuehren.

## Akzeptanzkriterien-Abdeckung

- Manuelles Backup per Button: Arbeitspaket 11 plus 2/3/10.
- ZIP-Datei im konfigurierbaren Pfad: Arbeitspaket 3 und 6.
- Automatische GVS-Backups: Arbeitspaket 4 und 11.
- Liste und Download vorhandener Backups: Arbeitspaket 3, 10, 11.
- Upload gueltiger Backup-ZIPs: Arbeitspaket 3, 10, 11.
- Restore mit Sicherheitsabfrage: Arbeitspaket 7, 8, 9, 11.
- Hintergrundprozesse pausieren: Arbeitspaket 8.
- Admin-Konto erhalten: Arbeitspaket 7 und Tests.
- Wiederverwendbare Bibliothek und einfache Registrierung: Arbeitspaket 1, 2, 5.
- Kontrollierte Fehler und Nachvollziehbarkeit: Arbeitspaket 12.

## Nicht im Scope dieses Schnitts

- Sichern echter Mediendateien aus Medienquellen.
- Verschluesselung oder Passwortschutz von Backups.
- Externe Speicherziele wie S3, SMB oder SFTP.
- Fortschrittsanzeige fuer sehr grosse Restore-Vorgaenge.
- Cluster-/Mehrprozess-Sperren fuer parallele App-Instanzen.

## Offene Punkte

Keine. Die offenen Punkte aus `requirement.md` wurden mit konservativen Defaults entschieden.
