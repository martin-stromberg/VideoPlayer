# Dokumentations-Index

> Zentrale Übersicht über die vorhandene Dokumentation des VideoWebPlayer-Web-Repositorys.

## Einstieg

| Dokument | Beschreibung | Status |
|----------|--------------|--------|
| [Installation und Setup](./GUIDE_Installation.md) | Linux-/Windows-Einrichtung, Start, Tests und Fehlerbehebung | Vollständig |
| [API-Vertrag](./API.md) | REST- und SignalR-Vertrag für das MAUI-Team | Vollständig |
| [Secrets Management](./SECRETS_MANAGEMENT.md) | Umgang mit Backend-Secrets, JWTs und Client-Gate-Werten | Vollständig |
| [Veröffentlichungscheckliste](./PUBLICATION_CHECKLIST.md) | Technische Prüfliste vor öffentlicher Bereitstellung | Vollständig |

## Technische Dokumentation

| Dokument | Beschreibung |
|----------|--------------|
| [SignalR-Implementation](./TECH_SignalR_Implementation.md) | Echtzeit-Update-System |
| [Event-System](./TECH_Event_System.md) | Ausgelagerte Event-Infrastruktur mit MAUI-Repository-Kontext |
| [Episode Selection](./TECH_Episode_Selection.md) | Episodenauswahl und Wiedergabe |
| [MediaElement Error Handling](./TECH_MediaElement_Error_Handling.md) | Fehlerbehandlung im Videoplayer |
| [Notification Ticker](./TECH_Notification_Ticker.md) | Footer-Lauftext-Komponente |
| [Automatisierte Programmupdates](./TECH_Auto_Update.md) | Update-Admin-UI, Sicherung und Installation |

## Benutzer- und Admin-Hilfen

| Dokument | Beschreibung |
|----------|--------------|
| [Einrichtung](./help/einrichtung.md) | Zentraler Administrationsbereich |
| [Backups](./help/backups.md) | Backup-Erstellung, Restore und Aufbewahrung |
| [Updates](./help/updates.md) | Manuelle und automatische Update-Verwaltung |
| [Medienbibliothek](./help/medienbibliothek.md) | Bedienung der Weboberfläche |
| [Medienmetadaten bearbeiten](./help/medien-editiermodus.md) | Administrativer Editiermodus |
| [Projektstruktur](./help/projektstruktur.md) | Solution-Projekte und Repository-Grenzen |

## Lokale Qualitätsprüfungen

```bash
dotnet build VideoPlayer.sln
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
```

Der versionierte Git-Hook liegt unter `.githooks/pre-commit` und wird mit `git config core.hooksPath .githooks` aktiviert.
Die Git-Metadaten der Hook-Datei müssen `100755` ausweisen.
