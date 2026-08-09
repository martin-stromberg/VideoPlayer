# Umsetzungsplan: UI fuer Updates

## Zielbild

Im bestehenden Blazor-Webprojekt `VideoWebPlayer` entsteht ein neuer Adminbereich `/admin/updates`. Administratoren koennen dort Update-Einstellungen persistent pflegen, den aktuellen `msTools.Updater`-Status sehen und manuelle Update-Aktionen ausloesen. Die bestehende Updater-Integration bleibt die technische Quelle fuer Check, Download, Installation und Status; die Anwendung ergaenzt nur Persistenz, Admin-Fassade, UI und Backup-Adapter.

## Codebereiche

- `VideoWebPlayer/Data/UpdateSettings.cs`: neue EF-Entitaet fuer eine einzelne persistente Update-Settings-Zeile.
- `VideoWebPlayer/Data/ApplicationDbContext.cs`: `DbSet<UpdateSettings>` ergaenzen.
- `VideoWebPlayer/Data/Configurations/UpdateSettingsConfiguration.cs`: Tabellenname, Key, Laengen und Default-/Required-Regeln konfigurieren.
- EF-Migration unter `VideoWebPlayer/Migrations/`: Tabelle `UpdateSettings` anlegen und `ApplicationDbContextModelSnapshot` aktualisieren. Die neue Programmversion fuehrt diese Migration wie bisher beim Start ueber `app.MigrateDatabase()` aus; es wird keine alternative SQLite-Update-Strategie geplant.
- `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs`: Defaults aus `IConfiguration` lesen, Settings anlegen, validieren, speichern und auf runtime-mutierbare Updater-Optionen anwenden.
- `VideoWebPlayer/Services/Updates/UpdateAdminService.cs`: Status, Settings und manuelle Aktionen fuer UI/Controller buendeln.
- `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateBackupService.cs`: Adapter von `IUpdateBackupService` auf den vorhandenen manuellen Backup-Ablauf mit `msTools.Backup.IBackupService`, Backup-Historie und Retention.
- `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs`: nicht mehr statisch ueber `IOptions<UpdateBackupOptions>` arbeiten, sondern aktuelle Backup-Settings ueber `UpdateSettingsService` lesen.
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`: neue Services registrieren, inklusive `IUpdateBackupService`.
- `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`: nach `UseAutoUpdate` eine Initialisierung der persistenten Settings in die runtime `AutoUpdateOptions` einhaengen; `UpdateUnitName` bleibt konstant, `ServiceName` kommt aus Settings.
- `VideoWebPlayer/Controllers/UpdatesController.cs`: geschuetzte POST-Endpunkte fuer manuelle Aktionen.
- `VideoWebPlayer/Components/Pages/Admin/Updates.razor`: neue Adminseite mit Settings-Formular, Statusanzeige, Warnabfragen und Aktionsbuttons.
- `VideoWebPlayer/Components/Layout/NavMenu.razor`: Admin-Navigation um "Updates" erweitern.

## Datenmodell und Persistenz

Neue Entitaet `UpdateSettings`:

- `Id` als int, eine Zeile mit `Id = 1`.
- `AutomaticChecksEnabled` fuer automatische Versionspruefung.
- `CheckIntervalMinutes` als int, Minimum `1`, Default aus `AutoUpdate:SourceCheck:Interval` oder `360`.
- `AllowPrereleaseUpdates`.
- `AutomaticInstallationEnabled`, gemappt auf `AutoUpdateOptions.EnableAutomaticInstallation`.
- `AutomaticDownloadEnabled`, intern weiterhin aus Konfiguration defaulten und im Service auf `true` setzen, wenn automatische Installation aktiv ist; optional nicht prominent in der UI anzeigen, da die Anforderung Installation statt Download konfiguriert.
- `ServiceName` als nullable/string, getrimmt, maximale Laenge z. B. `200`, gemappt auf `AutoUpdateOptions.ServiceName`.
- `CreateBackupBeforeInstallation`, gemappt auf `UpdateBackupOptions.Enabled`.
- `CancelInstallationOnBackupFailure`, default `true`, nicht zwingend prominent in der UI; fuer Akzeptanzkriterium "Installation abbrechen bei Backupfehler" bleibt `true`.
- `UpdateBackupPath`, default aus `AutoUpdate:Backup:Path` oder `Backups`.
- `RetainedUpdateBackupCount`, default aus `AutoUpdate:Backup:RetainedBackupCount` oder `5`.
- `UpdatedAtUtc`.

`UpdateSettingsService` stellt bereit:

- `GetOrCreateAsync(CancellationToken)`: liest erste Zeile oder legt sie aus `IConfiguration` an.
- `UpdateAsync(UpdateSettingsUpdate update, CancellationToken)`: validiert/clamped Intervalle und Retention, trimmt Strings, speichert und ruft danach `ApplyToRuntimeOptionsAsync`.
- `ApplyToRuntimeOptionsAsync(CancellationToken)`: setzt `AutoUpdateOptions.Enabled`, `AutoUpdateOptions.SourceCheck.Interval`, `AutoUpdateOptions.AllowPrereleaseUpdates`, `AutoUpdateOptions.EnableAutomaticInstallation`, `AutoUpdateOptions.ServiceName` sowie die dynamisch gelesenen Backup-Werte.
- `GetBackupOptionsAsync(CancellationToken)`: liefert aktuelle `UpdateBackupOptions` fuer den Coordinator.

Persistenzentscheidung: Die DB ist fuehrend fuer Admin-Aenderungen. `appsettings.json` liefert nur Initialwerte, solange noch keine DB-Zeile existiert. Das vermeidet laufende Konfigurationsdatei-Schreibzugriffe und passt zu `BackupSettingsService`.

Migrationsentscheidung: Das Projekt pflegt EF-Core-Migrationen direkt unter `VideoWebPlayer/Migrations/` und ruft beim Start der Anwendung `app.MigrateDatabase()` auf. Die Umsetzung muss daher eine reguläre Migration fuer `UpdateSettings` erzeugen; die Ausfuehrung erfolgt erst durch die installierte neue Programmversion beim naechsten Start.

## Updater-Integration

`UpdateAdminService` nutzt:

- `IAutoUpdateOrchestrator.GetStatusAsync` fuer Status-Snapshots.
- `IAutoUpdateCommandHandler.CheckAsync` fuer manuelle Pruefung.
- `IAutoUpdateCommandHandler.DownloadAsync` und danach `InstallAsync(confirmDowntime: true, ct)` fuer manuelle Installation, falls der Status noch nicht `ReadyToInstall` ist und eine installierbare Version bekannt ist.
- Status-Mapping aus `AutoUpdateStatusSnapshot.State` fuer UI: `Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `ReadyToInstall`, `Installing`, `Success`, `Failed`, `Disabled`.

Parallelisierungsschutz:

- UI deaktiviert Buttons bei `Checking`, `Downloading`, `Installing` oder `IsLocked`.
- `UpdateAdminService` prueft vor Aktionen denselben Status und gibt ein fachliches Ergebnis zurueck, statt doppelte Aktionen blind zu starten.
- Die interne Serialisierung von `msTools.Updater` bleibt die letzte technische Absicherung.

## UI-Integration

Neue Seite `VideoWebPlayer/Components/Pages/Admin/Updates.razor`:

- Route `/admin/updates`, `@rendermode InteractiveServer`.
- Admin-Claim-Pruefung analog `Backups.razor`; Nicht-Admins sehen nur "Nicht autorisiert.".
- Statusbereich oben mit Badge/Alert fuer laufende Pruefung, Installation, neue Version, aktuell, deaktiviert und Fehler.
- Statusdetails: installierte Version, verfuegbare Version, Prerelease-Kennzeichen, Veroeffentlichungsdatum, letzter Check, letzte Check-/Download-/Install-Ergebnisse, letzter Fehler, Lock-Information.
- Aktionsleiste: "Jetzt pruefen", "Update installieren", "Aktualisieren". Installieren ist nur aktiv bei `UpdateAvailable` oder `ReadyToInstall` mit bekannter Version und nicht laufender Aktion.
- Settings-Formular mit Bootstrap-Controls:
  - automatische Pruefung aktivieren,
  - Pruefintervall in Minuten,
  - Prerelease-Versionen akzeptieren,
  - neue Version automatisch installieren,
  - Dienstname fuer Neustart,
  - Backup vor Installation erstellen,
  - Aufbewahrung Update-Backups.
- Prerelease-Sicherheitsabfrage: Beim Aktivieren muss eine separate Checkbox/Bestätigung gesetzt sein; ohne Bestaetigung setzt die UI `AllowPrereleaseUpdates` wieder auf `false` und zeigt eine Warnung.
- Automatische Installation bekommt ebenfalls bewusstes UI: klare Checkbox und optional Hinweistext im Formularbereich, aber keine zusaetzliche fachliche Blockade, weil die Anforderung nur fuer Prerelease eine Pflichtabfrage verlangt.
- Polling per `PeriodicTimer` alle 3 Sekunden analog Backup-Seite; nach Aktionen und Speichern sofort neu laden.
- Navigationseintrag in `NavMenu.razor` im Adminbereich nahe `Programmeinstellungen` oder `Backups`.

## Admin-Schutz

- `UpdatesController` wird mit `[ApiController]`, `[Authorize(Policy = "AdminOnly")]` und `[Route("admin/updates/api")]` versehen.
- POST-Endpunkte:
  - `POST check`: Antiforgery validieren, `UpdateAdminService.CheckAsync`, Redirect auf `/admin/updates` mit Status/Error-Query.
  - `POST install`: Antiforgery validieren, serverseitig Installierbarkeit pruefen, `UpdateAdminService.InstallAsync`, Redirect mit Status/Error.
- Die Razor-Seite prueft weiterhin `IsAdmin=True`, damit der Menueintrag und Inhalt konsistent mit bestehenden Adminseiten bleiben.
- Keine Update-Aktion wird nur clientseitig geschuetzt; Controller und Service pruefen den Zustand erneut.

## Backup-vor-Installation-Adapter

`VideoWebPlayerUpdateBackupService` implementiert `IUpdateBackupService` und erzeugt das Backup so, wie es die Webseite bei einem manuellen Backup bereits tut:

- nutzt `msTools.Backup.IBackupService.CreateBackupAsync(new BackupCreateRequest(generation, "VideoWebPlayer"), ct)`.
- schreibt wie `VideoWebPlayerBackupFacade.CreateManualBackupAsync` einen Eintrag in `BackupOperationHistoryService`, Operation z. B. `ProgramUpdateBackup`.
- ruft bei Erfolg `IBackupService.ApplyRetentionAsync(ct)` auf, damit die bestehende Backup-Verwaltung konsistent bleibt.
- gibt bei Erfolg `UpdateBackupResult.Success(result.Descriptor.Path, result.Message)` zurueck; `BackupOperationResult.Descriptor.Path` ist die vorhandene Quelle fuer den erzeugten Dateipfad.
- gibt bei fehlendem Descriptor oder fehlgeschlagenem `BackupOperationResult` ein `UpdateBackupResult.Failure(...)` zurueck und protokolliert die Meldung.
- nutzt die bestehende Backup-Infrastruktur als Speicherort; `UpdateBackupRequest.TargetDirectory` bleibt fuer Retention/Kompatibilitaet des Coordinators relevant, erzwingt aber keinen zweiten Speicherort, weil `msTools.Backup` den Pfad ueber `IBackupOptionsProvider` bzw. `BackupSettingsService` bestimmt.

Generation-Entscheidung:

- `msTools.Backup.BackupGeneration` ist Teil der vorliegenden Solution und damit erweiterbar.
- Die Umsetzung soll `ProgramUpdate` als neue Enum-Generation in `msTools.Backup.BackupGeneration` ergaenzen und im Update-Backup-Adapter verwenden.
- Dadurch bleibt fachlich sichtbar, dass das Backup durch ein Programmupdate ausgeloest wurde, ohne es als manuelle Benutzeraktion zu deklarieren.
- Falls die Erweiterung beim Implementieren unerwartet unverhaeltnismaessige Folgeaenderungen ausloest, ist `Manual` die naechstpassende bestehende Generation; diese Abweichung muss dann in `review.md` oder `continue.md` begruendet werden.

`UpdateBackupCoordinator` liest `UpdateSettingsService.GetBackupOptionsAsync`, damit UI-Aenderungen ohne Neustart vor der naechsten Installation gelten. Bei Backupfehler und `CancelInstallationOnBackupFailure = true` bleibt das bisherige Abbruchverhalten bestehen und der Updater-Status zeigt den Fehler ueber das Installations-/LastError-Feld.

## Tests

Neue bzw. angepasste Tests in `VideoWebPlayer.Tests`:

- `UpdateSettingsServiceTests`
  - legt Default-Zeile aus `IConfiguration` an.
  - speichert Admin-Aenderungen persistent.
  - clampet `CheckIntervalMinutes` auf mindestens `1`.
  - aktualisiert `AutoUpdateOptions` nach Speichern.
  - liefert dynamische `UpdateBackupOptions` aus DB-Werten.
- `UpdateAdminServiceTests`
  - manuelle Pruefung ruft `IAutoUpdateCommandHandler.CheckAsync`.
  - Installation wird blockiert, wenn kein installierbarer Status vorhanden ist.
  - Installation ruft bei `UpdateAvailable` zuerst Download und danach `InstallAsync(true, ct)` auf.
  - laufende/gelockte Status blockieren doppelte Aktionen.
- `UpdatesControllerAuthorizationTests`
  - Controller hat `AdminOnly`.
  - `check` und `install` sind POST-Endpunkte.
  - Controller validiert Antiforgery analog Backup-Controller.
- `VideoWebPlayerUpdateBackupServiceTests`
  - erfolgreicher `IBackupService`-Aufruf mit `BackupGeneration.ProgramUpdate` wird zu `UpdateBackupResult.Success`.
  - der Dateipfad wird aus `BackupOperationResult.Descriptor.Path` uebernommen.
  - Backup-Historie und Retention werden analog zum manuellen Backup ausgefuehrt.
  - fehlgeschlagener/werfender Backup-Aufruf wird zu `Failure`.
- `UpdateBackupCoordinatorTests` anpassen
  - Optionen werden dynamisch aus `UpdateSettingsService` gelesen.
  - deaktiviertes Backup ueberspringt Adapter.
  - aktiviertes Backup blockiert Installation bei Fehler.

Testlauf:

- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
- Bei Build-Auswirkungen auf gemeinsame Projekte zusaetzlich `dotnet build` fuer die Solution.

## Dokumentation

- `docs/TECH_Auto_Update.md`: neuen Adminbereich, persistente DB-Settings, manuelle Aktionen, Adminschutz und Backup-Adapter dokumentieren.
- `README.md`: im Administrations-/Betriebsabschnitt kurzen Hinweis auf Update-UI, automatische Installation, Prerelease-Warnung und Backup-vor-Installation ergaenzen.
- Migrationshinweis aufnehmen: Die neue Version bringt die EF-Migration fuer `UpdateSettings` mit und wendet sie beim Start wie bestehende Migrationen automatisch an.

## Umsetzungsreihenfolge

1. Datenmodell, EF-Konfiguration, EF-Migration und `UpdateSettingsService` erstellen.
2. Runtime-Options-Anwendung in DI/Startup verdrahten und `UpdateBackupCoordinator` auf dynamische Settings umstellen.
3. `BackupGeneration.ProgramUpdate` ergaenzen, `VideoWebPlayerUpdateBackupService` analog zum manuellen Backup-Ablauf implementieren und registrieren.
4. `UpdateAdminService` fuer Status und manuelle Aktionen implementieren.
5. `UpdatesController` mit `AdminOnly` und Antiforgery anlegen.
6. `Updates.razor` und Navigation integrieren.
7. Tests ergaenzen/anpassen.
8. Dokumentation aktualisieren.
9. `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` ausfuehren und Befunde beheben.

## Offene Punkte

Keine.
