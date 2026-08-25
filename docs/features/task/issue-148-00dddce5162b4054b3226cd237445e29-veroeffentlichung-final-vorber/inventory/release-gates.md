# Release-Gates

## Lokal pruefbar

- Lizenz- und Dokumentationskonsistenz.
- Markdown-Linkcheck.
- Web-Solution-Build.
- Web-Testprojekt.
- MarkdownLinkCheck-Testprojekt.
- API-Dokumentationsvertragstest.
- Vulnerability-Scan der Web-Solution.
- Secret-Scan im Web-Arbeitsbaum und in der Web-Historie.
- Hook-Metadaten und Hook-Lauf in der aktuellen Arbeitskopie.

## Nicht vollstaendig lokal pruefbar

- Frischer Linux-Clone mit Hook-Lauf und `dotnet`-Fehlerpfad.
- MAUI-Build, MAUI-Tests und MAUI-Remote-Commit-Nachweis, falls das ausgelagerte Repository in dieser Arbeitskopie nicht vorhanden ist.
- GitHub-Repository-Settings, Branch Protection, Actions Permissions, Issues/Discussions und Security Advisories, falls kein authentifizierter GitHub-Zugriff eingerichtet ist.
- Produktive Secret-/Token-Rotation.
- Tatsaechliches Umschalten auf `public` und Tagging.
