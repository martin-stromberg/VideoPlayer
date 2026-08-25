# Release Notes

> Diese Datei wird von `.github/workflows/main-release.yml` als Body des GitHub-Releases verwendet.
> Vor einem Release sollte sie auf den aktuellen Stand gebracht werden; alter Inhalt kann entfernt oder durch den neuen Release-Text ersetzt werden.

## Wichtige Hinweise vor dem Update (Backup-Format Breaking Change)

:danger: **Vor dem Update** muss eine vollständige Sicherung des Anwendungsverzeichnisses (Dateien, Datenbank, Konfiguration) außerhalb des Anwendungspfads erfolgen.

:danger: **Nach dem Update** muss unbedingt eine neue Datensicherung im Programm erstellt werden:
`Einrichtung` > `Backups` > `Backup erstellen`.

Backups im alten `.zip`-Format werden von dieser Version nicht mehr unterstützt und können nicht wiederhergestellt werden.

## What’s New

- Veröffentlichungsvorbereitung ergänzt: konsistente PolyForm-Noncommercial-Lizenzhinweise, Linux-/Windows-Installationsanleitung, zentrale API-Dokumentation und Secret-Hinweise mit synthetischen Platzhaltern.
- Markdown-Linkcheck als versionierter lokaler Client-Hook unter `.githooks/pre-commit` ergänzt.
- Projektstruktur getrennt: Das Web-Repository enthält `VideoWebPlayer`, `VideoWebPlayer.Client`, `VideoWebPlayer.Tests` und Linkcheck-Tools; die MAUI-App wird im separaten MAUI-Repository gepflegt.

## Stand und Prüfhinweis

- Die Veröffentlichungsunterlagen, der versionierte Linkcheck-Hook, die API-Dokumentation sowie die Installations- und Secret-Hinweise sind vorbereitet.
- Vor einer Freigabe sind Web-Build und Web-Tests sowie die MAUI-Builds und MAUI-Tests im separaten MAUI-Repository erneut auszuführen.
- Dieser Stand ist noch keine vollständige Freigabe: Zwei technische Nacharbeiten bleiben offen. Die Produktionskonfiguration muss das erforderliche MAUI-API-Token validieren, und ungültige API-Schlüssel dürfen nicht vollständig ins Warning-Log geschrieben werden.
