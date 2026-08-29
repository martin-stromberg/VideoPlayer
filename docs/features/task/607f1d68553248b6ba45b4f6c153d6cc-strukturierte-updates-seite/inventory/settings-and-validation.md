# Persistenz, Defaults und Validierung

## Persistierte Felder

`UpdateSettings` ist eine Singleton-Entitaet mit folgenden fuer die Seite relevanten Werten:

- `AutomaticChecksEnabled`
- `CheckIntervalMinutes`
- `AllowPrereleaseUpdates`
- `AutomaticInstallationEnabled`
- `ServiceName`
- `CreateBackupBeforeInstallation`
- `CancelInstallationOnBackupFailure`
- `UpdateBackupPath`
- `RetainedUpdateBackupCount`

`AutomaticDownloadEnabled` wird intern ebenfalls verwaltet, ist aber kein eigenes Feld der Anforderung. Beim Speichern setzt die aktuelle UI es auf `true`; der Service leitet daraus zusammen mit Auto-Install die Runtime-Einstellung ab.

## Aktuelle Defaults

Beim erstmaligen Erzeugen werden Konfigurationswerte aus `AutoUpdate:*` gelesen. Fallbacks sind aktuell: automatische Checks `true`, Intervall `360` Minuten, Prerelease `false`, Auto-Install `false`, Backup vor Installation `true`, Abbruch bei Backupfehler `true`, Backup-Pfad `Backups`, Retention `5`. Der Dienstname ist optional und kommt aus der Konfiguration.

## Aktuelle Schutzregeln

Die UI validiert das Intervall mit `[Range(1, 1440)]`, den Dienstnamen mit `[MaxLength(200)]`, den Pfad als erforderlich mit maximal 1024 Zeichen und Retention aktuell mit `[Range(0, 365)]`. `UpdateSettingsService` erzwingt beim Speichern Intervall mindestens 1, normalisiert optionale Texte und setzt Retention aktuell mindestens auf 0.

Damit weicht der Bestand von der Anforderung ab: Retention muss 1 bis 10 akzeptieren. Diese Grenze muss sowohl im Formularmodell als auch serverseitig gelten, damit sie nicht nur eine Clientvalidierung bleibt. Fuer `Reset Defaults` existiert kein Servicevertrag; ein belastbarer Reset muss entweder die Default-Erzeugung zentral wiederverwenden oder einen expliziten Reset-Pfad erhalten.

## Fachliche Installationsregel

`UpdateAdminService.IsInstallable` erlaubt die Aktion nur bei `UpdateAvailable` oder `ReadyToInstall`, vorhandener verfuegbarer Versionszeichenkette und ohne Busy-/Lock-Zustand. Ein UI-Redesign darf diese Regel nicht duplizieren oder abschwaechen; der Controller/Fassade bleibt die autoritative Durchsetzung.
