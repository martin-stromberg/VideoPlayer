# VideoWebPlayer

<h1 align="center">
  <br>
  <img src="https://github.com/Muesli84/VideoPlayer/blob/master/VideoPlayer/Resources/AppIcon/appicon.png?raw=true" alt="VideoWebPlayer" width="200">
  <br>
  VideoWebPlayer
  <br>
</h1>

<h4 align="center">Eine moderne Video-Management- und Streaming-Plattform für Ihre private Mediathek</h4>

<p align="center">
  <a href="https://paypal.me/martinstromberg">
    <img src="https://img.shields.io/badge/$-donate-ff69b4.svg?maxAge=2592000&amp;style=flat">
  </a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet">
  <img src="https://img.shields.io/badge/MAUI-latest-512BD4?logo=dotnet">
  <img src="https://img.shields.io/badge/Blazor-latest-512BD4?logo=blazor">
</p>

<p align="center">
  <a href="#übersicht">Übersicht</a> •
  <a href="#funktionsumfang">Funktionsumfang</a> •
  <a href="#technologie-stack">Technologie-Stack</a> •
  <a href="#installation">Installation</a> •
  <a href="#dokumentation">Dokumentation</a> •
  <a href="#unterstützung">Unterstützung</a> •
  <a href="#lizenz">Lizenz</a>
</p>

---

## Übersicht

**VideoWebPlayer** ist eine Full-Stack-Lösung für die Verwaltung, Kategorisierung und das Streaming von privaten Video-Bibliotheken. Die Anwendung besteht aus einem ASP.NET Core Blazor-Backend und einer .NET MAUI Cross-Platform-App für mobile Endgeräte.

### Hauptmerkmale

- 🎬 **Automatische Medienverwaltung** - Scannt und kategorisiert Videos in Filme, Serien und Episoden
- 📱 **Cross-Platform** - Blazor Web-App und native iOS/Windows MAUI-App
- 🔄 **Echtzeit-Synchronisation** - SignalR-basierte Live-Updates über alle Geräte
- 📥 **Offline-Downloads** - Vollständige Offline-Unterstützung in der MAUI-App
- 🎯 **Intelligente Wiedergabe** - Continue-Watching, automatische Episodenfortschaltung
- 🌐 **Mehrere Quellen** - FTP, SFTP und lokale Medienbibliotheken

---

## Funktionsumfang

### Backend (ASP.NET Core Blazor)

#### Medienquellen-Verwaltung
- ✅ Registrierung von FTP/SFTP-Servern
- ✅ Automatisches Scannen und Indizieren von Videos
- ✅ NFO-Datei-Parsing für Metadaten
- ✅ Automatische Poster- und Banner-Zuordnung

#### Medien-Kategorisierung
- ✅ Automatische Erkennung von Filmen und Filmsammlungen
- ✅ TV-Show-Strukturierung (Shows → Seasons → Episodes)
- ✅ Genre-basierte Klassifizierung
- ✅ TMDB-Integration für Metadaten

#### Bibliotheksverwaltung
- ✅ Kategorisierte Navigation durch Filme und Serien
- ✅ Favoriten-System
- ✅ Continue-Watching mit Positionsspeicherung
- ✅ Recent Entries Timeline

#### Streaming & Playback
- ✅ Direktes Video-Streaming vom Server
- ✅ Multi-Format-Unterstützung
- ✅ Adaptive Bitrate-Unterstützung
- ✅ Position-Synchronisation über Geräte

#### Echtzeit-Updates
- ✅ SignalR-basierte Push-Notifications
- ✅ Live-Updates der Medienbibliothek
- ✅ Multi-Device-Synchronisation

#### Administration
- ✅ Administrative Backup-Verwaltung mit manuellen Backups, automatischer GVS-Aufbewahrung, Upload, Download, Löschen und Restore

### Frontend (.NET MAUI App)

#### Cross-Platform Support
- ✅ iOS (iPhone & iPad)
- ✅ Windows Desktop
- ⏳ Android (in Planung)
- ⏳ macOS (in Planung)

#### Offline-Funktionalität
- ✅ Download-Management mit SQLite-Persistenz
- ✅ Lokale Wiedergabe ohne Internetverbindung
- ✅ Automatische Download-Bereinigung (Cache/Persistenz)
- ✅ Playback-Position lokal gespeichert

#### Benutzeroberfläche
- ✅ Native Performance mit .NET MAUI
- ✅ Responsive Grid-Layout
- ✅ Carousel-basierte Medien-Navigation
- ✅ Echtzeit-Benachrichtigungsticker im Footer

#### Event-System
- ✅ Pub/Sub-basiertes Notification-System
- ✅ Download-Events (Completed, Deleted)
- ✅ SignalR-Event-Integration
- ✅ UI-Thread-sichere Event-Verarbeitung

---

## Technologie-Stack

### Backend

| Kategorie | Technologie | Version |
|-----------|-------------|---------|
| Framework | ASP.NET Core | .NET 10 |
| UI | Blazor Server | .NET 10 |
| Datenbank | SQLite | via Entity Framework Core |
| Real-Time | SignalR | .NET 10 |
| Authentication | ASP.NET Identity | .NET 10 |
| FTP/SFTP | FluentFTP, SSH.NET | Latest |
| Backups | msTools.Backup | Projektbibliothek |

### Frontend (MAUI)

| Kategorie | Technologie | Version |
|-----------|-------------|---------|
| Framework | .NET MAUI | .NET 10 |
| UI Toolkit | CommunityToolkit.Maui | 9.x |
| Media | CommunityToolkit.Maui.MediaElement | 4.x |
| Datenbank | sqlite-net-pcl | Latest |
| Real-Time | Microsoft.AspNetCore.SignalR.Client | 10.0 |

### Shared

| Kategorie | Technologie |
|-----------|-------------|
| Serialization | Newtonsoft.Json |
| HTTP Client | System.Net.Http |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Backups | msTools.Backup |

---

## Drittanbieter-Komponenten

### Open Source Libraries

- **[CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui)** - MAUI UI Controls & Extensions
- **[FluentFTP](https://github.com/robinrodricks/FluentFTP)** - FTP/FTPS Client
- **[SSH.NET](https://github.com/sshnet/SSH.NET)** - SFTP-Unterstützung
- **[Newtonsoft.Json](https://www.newtonsoft.com/json)** - JSON-Serialisierung
- **[sqlite-net-pcl](https://github.com/praeclarum/sqlite-net)** - SQLite für Mobile
- **[SyncFusion](https://www.syncfusion.com/)** - UI-Komponenten (optional)

### Icons & Assets

- Icons von [flaticon.com](https://www.flaticon.com)
- Eigene Hintergrund-Assets für MAUI-App

---

## Besondere Abhängigkeiten

### Plattform-spezifisch

#### iOS
- **Minimum Version**: iOS 14.0+
- **Berechtigungen**: Netzwerkzugriff, Speicher
- **Frameworks**: UIKit, AVFoundation

#### Windows
- **Minimum Version**: Windows 10 1809+
- **Runtime**: .NET Desktop Runtime 10.0+

### Netzwerk-Anforderungen
- HTTP/HTTPS-Zugriff zum Backend-Server
- WebSocket-Support für SignalR
- Ports: 5000 (HTTP), 5001 (HTTPS), oder konfigurierbar

### Backend-Anforderungen
- .NET 10 Runtime
- SQLite (embedded)
- Schreibrechte für Datenbankdatei
- Zugriff auf FTP/SFTP-Server (für Medienquellen)

---

## Installation

### Schnellstart (Entwicklungsumgebung)

```bash
# Repository klonen
git clone https://github.com/Muesli84/VideoPlayer.git
cd VideoPlayer

# Solution öffnen
VideoWebPlayer.sln
```

**Voraussetzungen:**
- Visual Studio 2022 (17.8+) mit .NET 10 SDK
- .NET MAUI Workload installiert
- iOS/Android Build-Tools (für Mobile-Entwicklung)

**Erste Schritte:**
1. `VideoWebPlayer` als Startup-Projekt setzen (Backend)
2. F5 zum Starten des Blazor-Backends
3. `VideoWebPlayer.Maui` als Startup-Projekt setzen (Optional)
4. iOS-Simulator oder Windows-Deployment wählen

📖 **Detaillierte Installationsanleitung**: Siehe [INSTALLATION.md](./Docs/GUIDE_Installation.md)

---

## Dokumentation

### 📚 Allgemeine Dokumentation

| Dokument | Beschreibung |
|----------|--------------|
| [Installation & Setup](./Docs/GUIDE_Installation.md) | Vollständige Installations- und Konfigurationsanleitung |
| [Benutzerhandbuch](./Docs/GUIDE_User_Manual.md) | Anleitung zur Nutzung der Anwendung |
| [Feature-Übersicht](./Docs/GUIDE_Features.md) | Detaillierte Beschreibung aller Features |

### 🔧 Technische Dokumentation

| Dokument | Beschreibung |
|----------|--------------|
| [Architektur-Übersicht](./Docs/TECH_Architecture.md) | System-Architektur und Design-Entscheidungen |
| [SignalR-Implementation](./Docs/TECH_SignalR_Implementation.md) | Echtzeit-Update-System (Backend ↔ Frontend) |
| [Event-System](./Docs/TECH_Event_System.md) | MAUI Notification Event Infrastructure |
| [Download-Management](./Docs/TECH_Download_Management.md) | Offline-Download-System in MAUI |
| [Media-Kategorisierung](./Docs/TECH_Media_Classification.md) | Automatische Video-Klassifizierung |
| [Database Schema](./Docs/TECH_Database_Schema.md) | Datenbankstruktur und Entitäten |

### 🎯 Spezifische Features

| Dokument | Beschreibung |
|----------|--------------|
| [Episode Selection](./Docs/TECH_Episode_Selection.md) | Smart Episode Selection & Play Button |
| [MediaElement Error Handling](./Docs/TECH_MediaElement_Error_Handling.md) | Video-Player Fehlerbehandlung |
| [Notification Ticker](./Docs/TECH_Notification_Ticker.md) | Footer-Lauftext-Komponente |
| [Backups](./docs/help/backups.md) | Administrative Backup-Verwaltung, automatische GVS-Backups, Löschen und Restore |

### 📋 API-Dokumentation

- [REST API Endpoints](./Docs/TECH_API_Reference.md)
- [SignalR Hub Events](./Docs/TECH_SignalR_Events.md)

---

## Entwicklung

### Repository-Struktur

```
VideoWebPlayer/
├── msTools.Backup/              # Wiederverwendbare Backup-Bibliothek
├── msTools.Backup.Tests/        # Tests der Backup-Bibliothek
├── VideoWebPlayer/              # ASP.NET Core Blazor Backend
│   ├── Components/              # Blazor-Komponenten
│   ├── Controllers/             # API-Controller
│   ├── Services/                # Backend-Services
│   ├── Hubs/                    # SignalR Hubs
│   └── Data/                    # EF Core DbContext
├── VideoWebPlayer.Client/       # Blazor Shared Client Library
│   └── Models/                  # Shared DTOs
├── VideoWebPlayer.Maui/         # .NET MAUI App
│   ├── Components/              # MAUI UI-Komponenten
│   ├── Services/                # MAUI Services
│   ├── ViewModels/              # MVVM ViewModels
│   └── Docs/                    # MAUI-spezifische Docs
├── VideoWebPlayer.Tests/        # Backend Unit Tests
├── VideoWebPlayer.Maui.Tests/   # MAUI Unit Tests
└── Docs/                        # Zentrale Dokumentation
```

### Branch-Strategie

- `master` - Stabile Production-Releases
- `develop` - Aktuelle Entwicklung
- `feature/*` - Feature-Branches
- `hotfix/*` - Bugfix-Branches

### Contributing

Contributions sind willkommen! Bitte erstellen Sie einen Issue oder Pull Request.

---

## Roadmap

### Version 2.0 (In Arbeit)
- ✅ SignalR Echtzeit-Updates
- ✅ Offline-Download-System
- ✅ Event-basiertes Notification-System
- ⏳ Android-Support
- ⏳ macOS-Support

### Version 2.1 (Geplant)
- ⏳ Multi-User-Management
- ⏳ Watch-Together-Feature (gemeinsames Schauen)
- ⏳ Subtitle-Support
- ⏳ Chromecast-Integration

### Version 3.0 (Vision)
- ⏳ AI-basierte Empfehlungen
- ⏳ Social Features (Bewertungen, Kommentare)
- ⏳ Plugin-System für Erweiterungen

---

## Unterstützung

### 💬 Community

- **Issues**: [GitHub Issues](https://github.com/Muesli84/VideoPlayer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Muesli84/VideoPlayer/discussions)

### ☕ Spenden

Unterstützen Sie die Entwicklung:

<a href="https://www.buymeacoffee.com/mstromberg" target="_blank">
  <img src="https://cdn.buymeacoffee.com/buttons/v2/default-green.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" >
</a>

---

## Lizenz

Dieses Projekt ist unter der **MIT License** lizenziert - siehe [LICENSE](LICENSE) für Details.

---

## Autoren

**Martin Stromberg**
- GitHub: [@Muesli84](https://github.com/Muesli84)
- Spenden: [PayPal](https://paypal.me/martinstromberg)

---

<p align="center">
  Made with ❤️ and .NET 10
</p>
