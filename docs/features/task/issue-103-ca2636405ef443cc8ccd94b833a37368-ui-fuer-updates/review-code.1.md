# Code-Review: UI fuer Updates

## Status

Befunde vorhanden.

## Befunde

### 1. Update-Backup-Retention kann falsche Dateien loeschen

- Schweregrad: Hoch
- Betroffen:
  - `VideoWebPlayer/Services/Updates/IUpdateBackupService.cs:22`
  - `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateBackupService.cs:35`
  - `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs:72`
  - `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs:107`

`IUpdateBackupService` dokumentiert `UpdateBackupRequest.TargetDirectory` als Verzeichnis, in dem die Backupdatei erstellt werden muss. Der neue `VideoWebPlayerUpdateBackupService` ignoriert dieses Zielverzeichnis aber und erstellt das Backup ueber `msTools.Backup.IBackupService` im normalen Backup-Speicherort. Danach fuehrt `UpdateBackupCoordinator` trotzdem eine eigene Retention auf dem konfigurierten Update-Backup-Verzeichnis aus und loescht dort pauschal Dateien nach `LastWriteTimeUtc`.

Das hat zwei problematische Folgen:

- Der UI-Wert `UpdateBackupPath` ist fuer die tatsaechliche Erstellung des Backups wirkungslos.
- Wenn `UpdateBackupPath` auf den normalen Backup-Speicher oder ein anderes existierendes Verzeichnis zeigt, kann die Update-Retention beliebige Dateien in diesem Verzeichnis loeschen, inklusive manueller oder regulaerer automatischer Backups. `msTools.Backup` wendet bereits eigene Retention ueber `ApplyRetentionAsync` an; die zusaetzliche Coordinator-Retention ist hier nicht auf Update-Backups begrenzt.

Empfehlung: Entweder den Update-spezifischen Pfad aus UI/Persistenz entfernen und nur die bestehende Backup-Infrastruktur samt deren Retention nutzen, oder den Adapter tatsaechlich in `TargetDirectory` schreiben lassen und die Retention auf eindeutig als Update-Backups identifizierbare Dateien beschraenken. In beiden Varianten sollte die doppelte Retention vermieden oder sauber abgegrenzt werden.

### 2. Deaktivierte automatische Pruefung deaktiviert wahrscheinlich auch manuelle Update-Aktionen

- Schweregrad: Mittel
- Betroffen:
  - `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs:128`
  - `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:59`
  - `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:72`

`AutomaticChecksEnabled` wird direkt auf `AutoUpdateOptions.Enabled` gemappt. Laut lokaler Updater-Dokumentation ist `AutoUpdateOptions.Enabled` aber die globale Aktivierung des Auto-Update-Subsystems, nicht nur des periodischen Checkers. `UpdateAdminService.CheckAsync` und `InstallAsync` wenden diese Option unmittelbar vor manuellen Aktionen an.

Damit kann ein Admin, der nur automatische Hintergrundpruefungen deaktivieren moechte, die Update-Funktion insgesamt deaktivieren. Das widerspricht der Anforderung: Bei deaktivierter automatischer Installation oder Pruefung sollen gefundene Updates weiterhin sichtbar und manuell pruef-/installierbar sein.

Empfehlung: Die Einstellung fuer automatische Pruefung separat vom globalen `AutoUpdateOptions.Enabled` behandeln. Falls `msTools.Updater` keine dynamische Checker-Aktivierung anbietet, sollte die Anwendung den Hintergrundlauf anderweitig unterbinden, ohne manuelle `IAutoUpdateCommandHandler`-Aktionen zu deaktivieren.

### 3. Antiforgery-Aufruf im UpdatesController ist nicht getestet

- Schweregrad: Niedrig
- Betroffen:
  - `VideoWebPlayer/Controllers/UpdatesController.cs:39`
  - `VideoWebPlayer/Controllers/UpdatesController.cs:52`
  - `VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs:21`

Der Controller validiert Antiforgery in beiden POST-Aktionen, aber die Tests pruefen nur `AdminOnly` und die POST-Routen. Der im Plan geforderte Nachweis, dass `Check` und `Install` `IAntiforgery.ValidateRequestAsync(HttpContext)` aufrufen, fehlt weiterhin.

Empfehlung: Zwei Controller-Unit-Tests ergaenzen, die `IAntiforgery` mocken und den Validierungsaufruf fuer `Check` und `Install` verifizieren.

### 4. Tests decken die kritischen Backup- und Disabled-Szenarien nicht ab

- Schweregrad: Niedrig
- Betroffen:
  - `VideoWebPlayer.Tests/Services/VideoWebPlayerUpdateBackupServiceTests.cs:42`
  - `VideoWebPlayer.Tests/Services/UpdateBackupCoordinatorTests.cs:88`
  - `VideoWebPlayer.Tests/Services/UpdateSettingsServiceTests.cs:46`

Die vorhandenen Tests bestaetigen den aktuellen Happy Path, pruefen aber nicht die riskanten Randfaelle:

- `VideoWebPlayerUpdateBackupService` ignoriert `UpdateBackupRequest.TargetDirectory`; dafuer gibt es keinen negativen Test.
- `UpdateBackupCoordinator` testet Retention nur mit einem Fake-Adapter, der in das Zielverzeichnis schreibt. Der reale Adapter schreibt dort nicht hinein, wodurch der Test die Produktivverdrahtung nicht absichert.
- Es fehlt ein Test, dass deaktivierte automatische Pruefung manuelle Checks weiterhin erlaubt bzw. welche Semantik hier gewollt ist.

Empfehlung: Tests um diese Szenarien erweitern, damit die oben genannten Regressionen nicht unbemerkt bleiben.

## Unauffaellige Bereiche

- `UpdatesController` ist mit `AdminOnly` geschuetzt und ruft Antiforgery im Code auf.
- Die EF-Migration fuer `UpdateSettings` ist vorhanden; `Program.cs` ruft `app.MigrateDatabase()` vor `app.Run()` auf, sodass die Tabelle vor dem Start der HostedServices angelegt wird.
- Die Navigation blendet den Update-Link nur fuer Admin-Claims ein.
- Die UI verhindert Mehrfachklicks clientseitig, und `UpdateAdminService` prueft `IsLocked`/Busy-States serverseitig erneut.

## Ausgefuehrte Pruefung

- Statischer Code-Review der aktuellen Arbeitsbaum-Aenderungen.
- Keine Tests ausgefuehrt; Schritt 8b ist im Lifecycle separat vorgesehen.
