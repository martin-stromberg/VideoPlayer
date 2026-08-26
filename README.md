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
- SignalR-basierte Aktualisierungen für verbundene Browser- und API-Clients.
- Administrative Bereiche für Quellen, Backups, Updates, Sicherheit, Genres und Benutzer.

Das Web-Repository verantwortet Backend, Blazor-Oberfläche, gemeinsame Client-DTOs, Tests und den versionierten API-Vertrag.

## Voraussetzungen

- .NET 10 SDK.
- Git.
- NuGet.org und die lokale Paketquelle `lib/packages/` aus `NuGet.config`.
- Die lokale DLL `lib/msTools.Updater/msTools.Updater.dll`.
- Schreibrechte für SQLite-Datenbank, Logs, Backups und Medien-/Bildverzeichnisse.

## Installation

```bash
git clone https://github.com/martin-stromberg/VideoPlayer.git VideoWebPlayer
cd VideoWebPlayer
dotnet restore VideoPlayer.sln
dotnet build VideoPlayer.sln
dotnet run --project VideoWebPlayer/VideoWebPlayer.csproj
```

Für eine vollständige Einrichtung: Voraussetzungen prüfen, Repository klonen, `NuGet.config` und die lokale Quelle `lib/packages/` bereitstellen, anschließend Restore und Build ausführen. Entwicklungs-Secrets werden über User Secrets, Umgebungsvariablen oder ein Secret-Management-System gesetzt; die Platzhalter aus der Dokumentation dürfen nicht übernommen werden.

Der lokale Entwicklungsstart verwendet die Projektkonfiguration. Die Launch-Profile enthalten `http://localhost:57331` und `http://localhost:5039`; `Host:Address`/`Host:Port` steuern zusätzlich die Discovery-Adresse, standardmäßig `http://localhost:5000`. Nach dem Start kann die Einrichtung über `GET /api/health` geprüft werden.

Die vollständige Anleitung für Linux und Windows steht in [docs/GUIDE_Installation.md](./docs/GUIDE_Installation.md).

## Secrets

Beispielwerte in der Dokumentation sind synthetische Platzhalter wie `<PRODUKTIVER_JWT_KEY>` oder `<CLIENT_API_TOKEN>`. Sie dürfen nicht produktiv verwendet werden. Backend-Secrets werden über User Secrets, Umgebungsvariablen oder ein Secret-Management-System gesetzt; API-Gate-Werte sind als sensible Konfigurationswerte zu schützen.

Details: [docs/SECRETS_MANAGEMENT.md](./docs/SECRETS_MANAGEMENT.md).

## Markdown-Linkcheck

Das Repository enthält einen lokalen Linkcheck für Markdown-Dateien:

```bash
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
```

Nach einem frischen Klon müssen die Hook-Tools einmal restauriert und gebaut werden, bevor der Hook mit `--no-restore` ausgeführt wird:

```bash
dotnet restore tools/SecretScan/SecretScan.csproj
dotnet restore tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj
dotnet build tools/SecretScan/SecretScan.csproj --no-restore
dotnet build tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj --no-restore
```

Der versionierte Client-Hook liegt unter `.githooks/pre-commit` und wird lokal aktiviert mit:

```bash
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
```

Die Stage-Ausgabe muss mit `100755` beginnen. Unter Linux kann ein frischer Arbeitsbaum andernfalls mit `chmod +x .githooks/pre-commit` korrigiert werden. Fehlt `dotnet`, bricht der Hook mit einer verständlichen Fehlermeldung ab; externe HTTP(S)-Links werden nicht über das Netzwerk geprüft.

Der Hook blockiert mögliche GitHub-Tokens in gestagten Dateien und in konfigurierten Remote-URLs. Anschließend prüft er lokale und repositoryinterne Markdown-Links ohne Netzwerkzugriff. CI-Workflows werden dafür nicht vorausgesetzt.

## Dokumentation

- [Installationsanleitung](./docs/GUIDE_Installation.md)
- [API-Vertrag](./docs/API.md)
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

Das Web-Repository baut Backend, Blazor-Oberfläche, Client-DTOs, Web-Tests und Linkcheck-Tools.

## Veröffentlichung

Vor einer öffentlichen Bereitstellung müssen die [Veröffentlichungscheckliste](./docs/PUBLICATION_CHECKLIST.md), der lokale Markdown-Linkcheck, die API-Dokumentation und der Secret-Scan nachvollzogen werden. Diese README beschreibt die technische Einrichtung und ist keine Aussage, dass eine Veröffentlichung oder alle offenen technischen Nacharbeiten bereits abgeschlossen sind.

Der aktuelle lokale Prüfstand vor der Veröffentlichung ist in [docs/PUBLICATION_AUDIT.md](./docs/PUBLICATION_AUDIT.md) dokumentiert. Externe Freigaben wie Linux-Frischclone, GitHub-Repository-Einstellungen und produktive Secret-Rotation müssen dort vor dem Umschalten auf `public` abgeschlossen werden.

## Lizenz

Dieses Projekt steht unter der **PolyForm Noncommercial License 1.0.0**. Die Software darf für private, persönliche, nicht-kommerzielle oder edukative Zwecke genutzt, verändert und weitergegeben werden.

Kommerzielle Nutzung, einschließlich direkter oder indirekter Einnahmeerzielung, gewerblicher Nutzung, Nutzung in Unternehmen oder Nutzung zur Erzielung finanzieller Vorteile, ist ohne vorherige schriftliche Zustimmung des Urhebers untersagt.

Für kommerzielle Nutzung ist ein separater Lizenzvertrag erforderlich. Kontakt für kommerzielle Lizenzanfragen: mstromberg84+videoplayer@gmail.com

## Autor

Martin Stromberg
GitHub: [@Muesli84](https://github.com/Muesli84)
