# Release Notes

> Diese Datei wird von `.github/workflows/main-release.yml` als Body des GitHub-Releases verwendet.
> Vor einem Release sollte sie auf den aktuellen Stand gebracht werden; alter Inhalt kann entfernt oder durch den neuen Release-Text ersetzt werden.

## Wichtige Hinweise vor dem Update (Backup-Format Breaking Change)

:danger: **Vor dem Update** muss eine vollständige Sicherung des Anwendungsverzeichnisses (Dateien, Datenbank, Konfiguration) außerhalb des Anwendungspfads erfolgen.

:danger: **Nach dem Update** muss unbedingt eine neue Datensicherung im Programm erstellt werden:
`Einrichtung` > `Backups` > `Backup erstellen`.

Backups im alten `.zip`-Format werden von dieser Version nicht mehr unterstützt und können nicht wiederhergestellt werden.

## What’s New

- Backup-System komplett auf `msTools.Backup` mit objektbasiertem `.bak`-Format umgestellt.
- Manuelle und automatische Backups verwenden jetzt `IBackupData` / `IBackupDataFactory`.
- Upload, Download, Restore und Validierung arbeiten mit dem neuen Format.

## Verification

- :white_check_mark: All tests passed
- :white_check_mark: Build artifacts created successfully
