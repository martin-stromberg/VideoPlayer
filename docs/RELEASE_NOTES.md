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
- Pre-Commit-Hook unter `.githooks/pre-commit` ergänzt: blockiert mögliche GitHub-Tokens in staged Dateien und Remote-URLs und führt den Markdown-Linkcheck aus.
- README für die GitHub-Startseite gestrafft und About-Seite mit ersten Schritten zur Videobibliothek ergänzt.
- Projektstruktur bereinigt: Das Web-Repository enthält `VideoWebPlayer`, `VideoWebPlayer.Client`, `VideoWebPlayer.Tests` und Linkcheck-Tools.

## Stand und Prüfhinweis

- Die Veröffentlichungsunterlagen, der versionierte Linkcheck-Hook, die API-Dokumentation sowie die Installations- und Secret-Hinweise sind vorbereitet.
- Web-Build, Web-Tests, API-Vertragstest, Markdown-Linkcheck und Web-Vulnerability-Scan wurden am 2026-08-25 lokal erfolgreich ausgeführt; Details stehen in `docs/PUBLICATION_AUDIT.md`.
- Dieser Stand ist noch keine vollständige Freigabe: Linux-Frischclone-Hook-Test, GitHub-Repository-Einstellungen und produktive Secret-Rotation müssen vor dem Umschalten auf `public` abgeschlossen werden.
