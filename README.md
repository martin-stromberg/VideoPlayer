# VideoWebPlayer

VideoWebPlayer ist eine ASP.NET-Core-/Blazor-Anwendung für die private Verwaltung und Wiedergabe einer eigenen Videobibliothek. Das Web-Repository enthält Backend, Weboberfläche, gemeinsame Client-DTOs, Tests und technische Dokumentation.

[![License: PolyForm Noncommercial 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-blue)](./LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)

## Funktionsumfang

- Medienquellen per FTP, SFTP oder lokaler Ablage verwalten.
- Filme, Serien, Staffeln und Episoden indizieren und kategorisieren.
- Poster, Banner, Fanart und generierte Hintergrundbilder ausliefern.
- Favoriten und Weiterschauen-Positionen pro Benutzer speichern.
- Blazor-Weboberfläche für Bibliothek, Wiedergabe und Administration.
- SignalR-basierte Aktualisierungen für verbundene Clients.
- Administrative Bereiche für Quellen, Backups, Updates, Sicherheit, Genres und Benutzer.

Die .NET-MAUI-App wird in einem separaten Repository gepflegt. In dieser Arbeitskopie liegt der vorhandene Klon unter `Sub-Repository/`; er ist absichtlich nicht Teil des Web-Repositorys.

Das Web-Repository verantwortet Backend, Blazor-Oberfläche, gemeinsame Client-DTOs und den versionierten API-Vertrag. Das MAUI-Repository verantwortet die mobile App, ihre Tests und die für den mobilen Build übernommene Client-Codebasis. Das Web-Repository baut und testet keine MAUI-Projekte.

## Voraussetzungen

- .NET 10 SDK.
- Git.
- NuGet.org und die lokale Paketquelle `lib/packages/` aus `NuGet.config`.
- Die lokale DLL `lib/msTools.Updater/msTools.Updater.dll`.
- Schreibrechte für SQLite-Datenbank, Logs, Backups und Medien-/Bildverzeichnisse.

Für die MAUI-App gelten zusätzlich plattformabhängige MAUI-Workloads und SDKs; Details stehen im separaten MAUI-Repository.

## Installation

```bash
git clone <REPOSITORY_URL> VideoWebPlayer
cd VideoWebPlayer
dotnet restore VideoPlayer.sln
dotnet build VideoPlayer.sln
dotnet run --project VideoWebPlayer/VideoWebPlayer.csproj
```

Für eine vollständige Einrichtung: Voraussetzungen prüfen, Repository klonen, `NuGet.config` und die lokale Quelle `lib/packages/` bereitstellen, anschließend Restore und Build ausführen. Entwicklungs-Secrets werden über User Secrets, Umgebungsvariablen oder ein Secret-Management-System gesetzt; die Platzhalter aus der Dokumentation dürfen nicht übernommen werden.

Der lokale Entwicklungsstart verwendet die Projektkonfiguration. Die Launch-Profile enthalten `http://localhost:57331` und `http://localhost:5039`; `Host:Address`/`Host:Port` steuern zusätzlich die Discovery-Adresse, standardmäßig `http://localhost:5000`. Nach dem Start kann die Einrichtung über `GET /api/health` geprüft werden.

Die vollständige Anleitung für Linux und Windows steht in [docs/GUIDE_Installation.md](./docs/GUIDE_Installation.md).

## Secrets

Beispielwerte in der Dokumentation sind synthetische Platzhalter wie `<PRODUKTIVER_JWT_KEY>` oder `<MAUI_CLIENT_API_TOKEN>`. Sie dürfen nicht produktiv verwendet werden. Backend-Secrets werden über User Secrets, Umgebungsvariablen oder ein Secret-Management-System gesetzt; der Client-Gate-Wert wird getrennt dokumentiert und ist im Backend als sensibler Konfigurationswert zu schützen.

Details: [docs/SECRETS_MANAGEMENT.md](./docs/SECRETS_MANAGEMENT.md).

## Markdown-Linkcheck

Das Repository enthält einen lokalen Linkcheck für Markdown-Dateien:

```bash
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
```

Nach einem frischen Klon muss der Linkcheck einmal restauriert und gebaut werden, bevor der Hook mit `--no-restore` ausgeführt wird:

```bash
dotnet restore tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj
dotnet build tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj --no-restore
```

Der versionierte Client-Hook liegt unter `.githooks/pre-commit` und wird lokal aktiviert mit:

```bash
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
```

Die Stage-Ausgabe muss mit `100755` beginnen. Unter Linux kann ein frischer Arbeitsbaum andernfalls mit `chmod +x .githooks/pre-commit` korrigiert werden. Fehlt `dotnet`, bricht der Hook mit einer verständlichen Fehlermeldung ab; externe HTTP(S)-Links werden nicht über das Netzwerk geprüft.

Der Hook prüft lokale und repositoryinterne Markdown-Links ohne Netzwerkzugriff. CI-Workflows werden dafür nicht vorausgesetzt.

## Dokumentation

- [Installationsanleitung](./docs/GUIDE_Installation.md)
- [API-Vertrag für das MAUI-Team](./docs/API.md)
- [Secrets Management](./docs/SECRETS_MANAGEMENT.md)
- [Dokumentationsindex](./docs/INDEX.md)
- [Veröffentlichungscheckliste](./docs/PUBLICATION_CHECKLIST.md)
- [Projektstruktur](./docs/help/projektstruktur.md)
- [Medienbibliothek](./docs/help/medienbibliothek.md)
- [Einrichtung](./docs/help/einrichtung.md)
- [Backups](./docs/help/backups.md)
- [Updates](./docs/help/updates.md)

## Entwicklung

```bash
dotnet restore VideoPlayer.sln
dotnet build VideoPlayer.sln
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj
```

Das Web-Repository baut keine MAUI-Projekte. MAUI-App, MAUI-Tests und eine Kopie des gemeinsamen Client-Projekts werden im separaten MAUI-Repository gepflegt. Für die plattformabhängigen MAUI-Workloads, Builds und Tests siehe die Installationshinweise des MAUI-Repositorys.

## Veröffentlichung

Vor einer öffentlichen Bereitstellung müssen die [Veröffentlichungscheckliste](./docs/PUBLICATION_CHECKLIST.md), der lokale Markdown-Linkcheck, die API-Dokumentation und der Secret-Scan nachvollzogen werden. Diese README beschreibt die technische Einrichtung und ist keine Aussage, dass eine Veröffentlichung oder alle offenen technischen Nacharbeiten bereits abgeschlossen sind.

## Lizenz

Dieses Projekt steht unter der **PolyForm Noncommercial License 1.0.0**. Die Software darf für private, persönliche, nicht-kommerzielle oder edukative Zwecke genutzt, verändert und weitergegeben werden.

Kommerzielle Nutzung, einschließlich direkter oder indirekter Einnahmeerzielung, gewerblicher Nutzung, Nutzung in Unternehmen oder Nutzung zur Erzielung finanzieller Vorteile, ist ohne vorherige schriftliche Zustimmung des Urhebers untersagt.

Für kommerzielle Nutzung ist ein separater Lizenzvertrag erforderlich. Kontakt für kommerzielle Lizenzanfragen: mstromberg84+videoplayer@gmail.com

## Autor

Martin Stromberg
GitHub: [@Muesli84](https://github.com/Muesli84)
