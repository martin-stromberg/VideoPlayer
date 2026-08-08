# Konfiguration, Tests und Integrationspunkte fuer msTools.Backup

## Konfiguration

Vorhandene Konfiguration im Webprojekt:

- `ConnectionStrings:DefaultConnection` in `VideoWebPlayer/appsettings.json`: `Data Source=Data/WebVideoPlayer.db`.
- Serilog Console/File mit Tagesrolling und `retainedFileCountLimit: 3`.
- `Host:Address` und `Host:Port` werden in `Program.cs` optional fuer UDP Discovery gelesen.
- JWT-Secrets und API-Token werden in `ServiceCollectionExtensions` aus `Jwt:*` gelesen; Produktion verlangt Werte.

Es gibt noch keine Backup-Konfiguration.

Empfohlene neue Konfiguration:

- `Backups:Path`: Dateisystempfad fuer ZIP-Dateien.
- `Backups:ManualPrefix` oder Namensschema.
- `Backups:Schedule`: Aktivierung und Intervalle.
- `Backups:Retention`: GVS-Regeln fuer Sohn/Vater/Grossvater.
- `Backups:MaxUploadSizeBytes`.

Wenn Admins Einstellungen zur Laufzeit aendern sollen, muessen sie persistent sein. Optionen nur aus `appsettings.json` reichen dann nicht. Moeglichkeiten:

- Neue EF-Entity `BackupSettings` im Webprojekt.
- Erweiterung von `Setup`.
- JSON-Datei im AppData-/Data-Verzeichnis. EF ist wegen vorhandener Admin-Settings naheliegender.

## Tests

Vorhandene Testprojekte:

- `VideoWebPlayer.Tests`: xUnit v3, EF Core InMemory/SQLite, Moq.
- `VideoWebPlayer.Maui.Tests`: kleiner Testumfang fuer Continue-Watching-Ingress.

Vorhandene Testmuster:

- `MediaSourceScanServiceTests` baut DI-ServiceProvider und verwendet SQLite-In-Memory.
- `MediaSourceScannerTests` testet Scanner mit SQLite-In-Memory.
- `ContinueWatchingServiceSignalRTests` nutzt EF InMemory und Mocks.
- Helper wie `TestableMediaSourceScanService`, `FakeSftpMediaSourceReader`, `ListLogger`, `TestHelpers`.

Fuer Backups sollten Tests auf zwei Ebenen entstehen:

- `msTools.Backup.Tests` fuer ZIP-Store, Datei-Validierung, Retention/GVS, Optionen, Upload-Pruefung.
- `VideoWebPlayer.Tests` fuer Host-Provider: Export/Restore von `ApplicationDbContext`, Admin-Erhalt, Restore-Gate, UI-nahe Service-Integration.

SQLite-In-Memory ist fuer Restore-Tests geeigneter als EF InMemory, weil relationale Constraints und Transaktionen realistischer sind.

## Sinnvolle Integrationspunkte

### DI

`ServiceCollectionExtensions.AddVideoWebPlayerServices` ist der zentrale Ort fuer:

- `services.AddBackups(...)`
- Registrierung von `VideoWebPlayerBackupDataProvider`
- Registrierung eines Restore-/Background-Gates
- ggf. Registrierung eines geplanten Backup-Hosted-Service

### Middleware/Endpoints

`WebApplicationExtensions.UseVideoWebPlayer` ist der zentrale Ort fuer:

- `app.UseBackups(...)`
- optionales Mapping von Download/Upload/Restore-Endpunkten
- Admin-Policy fuer Backup-Endpunkte

### Admin UI

Neue Razor-Komponente:

- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- Route `/admin/backups`
- Navigationseintrag in `NavMenu.razor`

Die Komponente sollte nicht direkt ZIP-Dateien manipulieren, sondern einen Backup-Service verwenden.

### Datenprovider

Die Bibliothek sollte ein Interface fuer Host-Daten anbieten. Ein moeglicher Zuschnitt:

- `Task ExportAsync(Stream target, CancellationToken cancellationToken)`
- `Task<BackupValidationResult> ValidateAsync(Stream backup, CancellationToken cancellationToken)`
- `Task RestoreAsync(Stream backup, BackupRestoreContext context, CancellationToken cancellationToken)`

Der `VideoWebPlayer`-Provider entscheidet, welche Tabellen exportiert und in welcher Reihenfolge geloescht/importiert werden. Dadurch bleibt `msTools.Backup` projektunabhaengig.

### Operation-Koordination

Restore braucht hostseitige Hooks:

- Vor Restore: laufende Scanner/Worker pausieren.
- Waehrend Restore: neue manuelle Scans blockieren.
- Nach Restore: Gate freigeben, ggf. SignalR-Status senden.

Die Backup-Bibliothek sollte nur ein abstraktes Interface verlangen, z. B. `IBackupRestoreGuard`, und die konkrete Umsetzung dem Host ueberlassen.

## Validierung von Upload-ZIPs

Mindestens pruefen:

- Datei ist ZIP lesbar.
- Manifest ist vorhanden.
- Manifest enthaelt Formatversion, App-/Providerkennung und Erstellzeit.
- Erwartete Datenpayloads sind vorhanden.
- Keine Pfad-Traversal-Eintraege im ZIP.
- Optional: Pruefsummen der Eintraege.

Ohne Manifest ist schwer sicherzustellen, dass eine hochgeladene ZIP tatsaechlich ein wiederherstellbares Backup dieser Anwendung ist.
