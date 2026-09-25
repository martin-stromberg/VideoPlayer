# VideoWebPlayer

VideoWebPlayer ist eine selbst gehostete ASP.NET-Core-/Blazor-Anwendung für die private Verwaltung und Wiedergabe einer eigenen Videobibliothek im Browser.

[![License: PolyForm Noncommercial 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-blue)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)

## Funktionen

- Medienquellen vom Typ `SFTP-Server` oder `Lokales Verzeichnis` (Verzeichnis auf dem Server, auch UNC-Pfad) verwalten.
- Filme, Serien, Staffeln und Episoden indizieren und kategorisieren.
- Poster, Banner, Fanart und Hintergrundbilder anzeigen.
- Favoriten, Weiterschauen-Positionen (mit optionalem Playlist-Bezug) und Gesehen-Kennzeichen pro Benutzer speichern.
- Eigene Playlists anlegen, öffnen (mit Detailseite), bearbeiten und löschen; Filme, Serien, Staffeln,
  Episoden und Filmsammlungen per Namenssuche mit Live-Ergebnisliste (inkl. Titelbild) auswählen und
  per Kaskaden-Logik hinzufügen oder entfernen. Die Suche funktioniert case-insensitiv (z. B. findet
  die Suche nach „breaking bad" den Titel „Breaking Bad"). Duplikat-Handling mit benutzerfreundlicher
  Rückmeldung (z. B. „3 Titel hinzugefügt, 2 bereits vorhanden"); MediaType-Normalisierung für
  konsistente API-Aufrufe. Wahlweise automatische Sortierung nach Erscheinungsdatum (mit Fallback auf
  Serien-/Staffelreihenfolge und Hinzufügedatum) oder manuelle Sortierung per Drag & Drop bzw.
  Schnellaktionen („An Anfang"/„An Ende"); ein Wechsel von manueller zu automatischer Sortierung
  erfordert wegen des damit verbundenen Datenverlusts eine ausdrückliche Bestätigung. Dazu fortlaufend
  nachladende, virtualisierte Anzeige der Einträge für flüssiges Scrollen auch bei sehr großen
  Playlists. Jeder Eintrag zeigt ein Titelbild; Einträge, auf die der Anwender weder über regulären
  Quellenzugriff noch über eine individuelle Freischaltung zugreifen kann, werden abgeblendet
  dargestellt, lassen sich aber weiterhin aus der Playlist entfernen. Titel lassen sich direkt aus
  der Playlist heraus abspielen, mit manuellem und automatischem Weiterschalten zum nächsten
  abspielbaren und zugänglichen Titel (nicht-abspielbare Sammel-Einträge und nicht freigeschaltete
  Einträge werden dabei übersprungen), Playlist-Badge mit Name und aktueller Position sowie
  reload-resistenter Fortsetzung der Wiedergabeposition. Jede Playlist besitzt außerdem ein
  Coverbild, das in Übersicht und Detailansicht angezeigt wird: entweder manuell hochgeladen
  (JPEG, PNG oder WebP, max. 5 MB) oder auf Knopfdruck („Neu erzeugen") automatisch als Collage
  aus bis zu fünf Titelbildern der Einträge erzeugt — mit Priorität Serien- vor Episoden-,
  Filmsammlungs- und Filmbildern; ein hochgeladenes Bild hat stets Vorrang, ohne Coverbild greift
  ein Platzhalter. Administratoren können eigene Playlists als „öffentlich" kennzeichnen: Sie sind
  dann für alle Anwender in einer eigenen Übersicht sichtbar und abspielbar, aber ausschließlich
  lesend (nur der Besitzer darf sie ändern); nicht freigeschaltete Titel erscheinen abgeblendet, und
  der Fortschritt eines Betrachters landet in dessen eigener Weiterschauen-Liste.
- Einzelne Serien und Filmsammlungen für andere Anwender freischalten, ohne die gesamte Quelle freizugeben.
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
- [Hilfe zu Playlists](./docs/help/playlists.md) (inkl. öffentliche Playlists)
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
