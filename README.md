# VideoWebPlayer

VideoWebPlayer ist eine selbst gehostete ASP.NET-Core-/Blazor-Anwendung für die private Verwaltung und Wiedergabe einer eigenen Videobibliothek im Browser.

[![License: PolyForm Noncommercial 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-blue)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)

## Funktionen

- Medienquellen vom Typ `SFTP-Server` oder `Lokales Verzeichnis` (Verzeichnis auf dem Server, auch UNC-Pfad) verwalten.
- Filme, Serien, Staffeln und Episoden indizieren und kategorisieren.
- Poster, Banner, Fanart und Hintergrundbilder anzeigen.
- Favoriten, Weiterschauen-Positionen und Gesehen-Kennzeichen pro Benutzer speichern.
- Einzelne Serien und Filmsammlungen fuer andere Anwender freischalten, ohne die gesamte Quelle freizugeben.
- Browserbasierte Oberfläche für Bibliothek, Wiedergabe und Administration.
- Automatische Erfassung und Übersicht von Schauspielern inklusive Such- und Filtermöglichkeiten.
- Backups, strukturierte Programmupdates, Benutzer, Genres und Sicherheitseinstellungen verwalten.
- Backup-Dateien chunked hochladen — auch sehr große Archive über 6 GB — mit Fortschrittsanzeige und Wiederaufnahme nach Unterbrechungen.
- Client-Apps per Einmal-Pairing-Code koppeln — jedes Gerät erhält ein individuelles, widerrufbares Geräte-Token statt eines geteilten statischen API-Keys.

## Schnellstart

Vorausgesetzt werden das .NET 10 SDK, Git und Zugriff auf die im Repository konfigurierte lokale Paketquelle unter `lib/`.

```bash
git clone https://github.com/martin-stromberg/VideoPlayer.git VideoWebPlayer
cd VideoWebPlayer
dotnet restore VideoPlayer.sln
dotnet build VideoPlayer.sln
dotnet run --project VideoWebPlayer/VideoWebPlayer.csproj
```

Die vollständige Einrichtung für Linux und Windows steht in [docs/GUIDE_Installation.md](./docs/GUIDE_Installation.md).

## Erste Schritte in der Anwendung

1. Anwendung starten und den ersten Benutzer anlegen.
2. In `Einrichtung` unter `Quellen` eine Medienquelle hinzufügen und als `Quelltyp` `SFTP-Server` oder `Lokales Verzeichnis` wählen.
3. Quelle speichern und Scan/Klassifizierung starten oder den automatischen Scan abwarten.
4. Die Quelle in der Navigation öffnen.
5. Film, Serie, Staffel oder Episode auswählen und abspielen.

Client-Apps (z. B. die TV-App) werden in `Einrichtung` unter `Geräte` mit einem Einmal-Pairing-Code gekoppelt.

## Konfiguration

Produktive Secrets dürfen nicht im Repository abgelegt werden. Konfiguriere JWT-Schlüssel, API-Tokens und ähnliche Werte über User Secrets, Umgebungsvariablen oder ein Secret-Management-System.

Details stehen in [docs/SECRETS_MANAGEMENT.md](./docs/SECRETS_MANAGEMENT.md).

Für den Backup-Upload sind produktiv zwei Werte relevant: `Backups:MaxUploadSizeBytes` (Standard 5 GiB, auch in der Admin-Oberfläche änderbar) begrenzt die akzeptierte Dateigröße, und `Kestrel:Limits:MaxRequestBodySize` (in `appsettings.Production.json` auf `0` = unbegrenzt gesetzt) deaktiviert das serverseitige Request-Limit, damit große Uploads nicht blockiert werden. Unter IIS ist dafür das OutOfProcess-Hosting-Modell erforderlich (in der Projektdatei über `AspNetCoreHostingModel` festgelegt); Details stehen in [docs/help/backups.md](./docs/help/backups.md).

Für das Geräte-Pairing sind optional `Pairing:CodeLength` (Standard 8 Zeichen) und `Pairing:CodeTtlMinutes` (Standard 5 Minuten) konfigurierbar; `Jwt:ApiToken:Maui` bleibt in Produktion als Fallback-Gate-Token Pflicht. Details stehen in [docs/GUIDE_Installation.md](./docs/GUIDE_Installation.md).

## Dokumentation

- [Installationsanleitung](./docs/GUIDE_Installation.md)
- [API-Vertrag](./docs/API.md)
- [Secrets Management](./docs/SECRETS_MANAGEMENT.md)
- [Dokumentationsindex](./docs/INDEX.md)
- [Hilfe zu Backups](./docs/help/backups.md)
- [Hilfe zur Einrichtung](./docs/help/einrichtung.md)
- [Hilfe zu Medienquellen](./docs/help/medienquellen/index.md)
- [Hilfe zu Programmupdates](./docs/help/updates.md)
- [Hilfe zum Gesehen-Kennzeichen](./docs/help/gesehen-status.md)
- [Hilfe zu Geräten und Pairing](./docs/help/geraete/index.md)

## Entwicklung

```bash
dotnet restore VideoPlayer.sln
dotnet build VideoPlayer.sln
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj
```

Der versionierte Pre-Commit-Hook unter `.githooks/pre-commit` blockiert direkte Commits auf `main`/`staging` und führt Übersetzungsprüfung, Secret-Scan und Markdown-Linkcheck aus. Details zur Aktivierung stehen in [docs/GUIDE_Installation.md](./docs/GUIDE_Installation.md).

## Lizenz

Dieses Projekt steht unter der **PolyForm Noncommercial License 1.0.0**. Die Software darf für private, persönliche, nicht-kommerzielle oder edukative Zwecke genutzt, verändert und weitergegeben werden.

Kommerzielle Nutzung, einschließlich direkter oder indirekter Einnahmeerzielung, gewerblicher Nutzung, Nutzung in Unternehmen oder Nutzung zur Erzielung finanzieller Vorteile, ist ohne vorherige schriftliche Zustimmung des Urhebers untersagt.

Für kommerzielle Nutzung ist ein separater Lizenzvertrag erforderlich. Kontakt für kommerzielle Lizenzanfragen: mstromberg84+videoplayer@gmail.com

## Autor

Martin Stromberg
GitHub: [@martin-stromberg](https://github.com/martin-stromberg)
