# Installation & Setup Guide

> **Dokumenttyp**: Allgemeine Dokumentation  
> **Zielgruppe**: Entwickler, Administratoren  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2024

## Übersicht

Dieses Dokument beschreibt die Installation und Einrichtung der VideoWebPlayer-Solution für Entwicklungs- und Produktionsumgebungen.

## Voraussetzungen

### Software-Anforderungen

#### Entwicklungsumgebung

- **Visual Studio 2022** (Version 17.8 oder höher)
  - Workload: "ASP.NET and web development"
  - Workload: ".NET Multi-platform App UI development"
- **.NET 10 SDK** (oder höher)
- **Git** für Version Control

#### Optional für Mobile-Entwicklung

- **Xcode** (für iOS-Entwicklung auf macOS)
- **Android SDK** (für Android-Entwicklung)
- **Windows SDK** (für Windows-App-Entwicklung)

### Hardware-Anforderungen

- **Minimum**: 8 GB RAM, 10 GB freier Speicherplatz
- **Empfohlen**: 16 GB RAM, 50 GB freier Speicherplatz (für Medienbibliothek)

### Netzwerk-Anforderungen

- Internetzugang für NuGet-Package-Downloads
- Zugriff auf FTP/SFTP-Server (für Medienquellen)

## Installation

### 1. Repository klonen

```bash
# HTTPS
git clone https://github.com/Muesli84/VideoPlayer.git
cd VideoPlayer

# Oder SSH
git clone git@github.com:Muesli84/VideoPlayer.git
cd VideoPlayer
```

### 2. Solution öffnen

```bash
# Windows
start VideoWebPlayer.sln

# macOS
open VideoWebPlayer.sln

# Oder direkt in Visual Studio öffnen
```

### 3. NuGet-Packages wiederherstellen

Visual Studio restauriert Packages automatisch beim ersten Build. Manuell:

```bash
dotnet restore
```

### 4. .NET MAUI Workload überprüfen

```bash
# Installierte Workloads anzeigen
dotnet workload list

# MAUI Workload installieren (falls nicht vorhanden)
dotnet workload install maui
```

## Konfiguration

### Backend (VideoWebPlayer)

#### 1. Datenbank konfigurieren

Die Anwendung verwendet SQLite. Die Datenbank wird automatisch erstellt.

**Connection String** in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

#### 2. JWT-Secrets konfigurieren

**Entwicklung** - User Secrets verwenden:

```bash
cd VideoWebPlayer
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "DEIN_BASE64_ENCODED_SECRET_KEY"
dotnet user-secrets set "Jwt:ApiToken" "DEIN_API_TOKEN"
```

**Produktion** - Umgebungsvariablen:

```bash
# Linux/macOS
export Jwt__Key="DEIN_BASE64_ENCODED_SECRET_KEY"
export Jwt__ApiToken="DEIN_API_TOKEN"

# Windows PowerShell
$env:Jwt__Key = "DEIN_BASE64_ENCODED_SECRET_KEY"
$env:Jwt__ApiToken = "DEIN_API_TOKEN"
```

#### 3. Server-Adresse konfigurieren

In `appsettings.json`:

```json
{
  "Host": {
    "Address": "0.0.0.0",
    "Port": "5000"
  }
}
```

### Frontend (VideoWebPlayer.Maui)

#### 1. Server-Adresse

Die Server-Adresse wird zur Laufzeit konfiguriert (Login-Seite).

**Für Entwicklung** - Emulator/Simulator verwenden:

- **iOS Simulator**: `localhost:5000`
- **Android Emulator**: `10.0.2.2:5000`
- **Physisches Gerät**: `<IP_DES_DEV_RECHNERS>:5000`

#### 2. API-Token

Hardcodiert in `MauiProgram.cs` (nur für Entwicklung):

```csharp
client.DefaultRequestHeaders.Add("X-API-Key", "DEIN_API_TOKEN");
```

**Für Produktion**: Token über sichere Konfiguration laden.

## Erste Schritte

### 1. Backend starten

```bash
cd VideoWebPlayer
dotnet run

# Oder in Visual Studio:
# VideoWebPlayer als Startup-Projekt setzen
# F5 drücken
```

Die Anwendung startet auf:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### 2. Frontend starten (Optional)

#### iOS

```bash
cd VideoWebPlayer.Maui
dotnet build -t:Run -f net10.0-ios

# Oder in Visual Studio:
# VideoWebPlayer.Maui als Startup-Projekt
# iOS-Simulator auswählen
# F5 drücken
```

#### Windows

```bash
cd VideoWebPlayer.Maui
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# Oder in Visual Studio:
# VideoWebPlayer.Maui als Startup-Projekt
# Windows Machine auswählen
# F5 drücken
```

### 3. Erste Anmeldung

#### Backend (Blazor)

1. Navigiere zu `http://localhost:5000`
2. Klicke auf "Register"
3. Erstelle einen neuen Account
4. Bestätige die E-Mail (in Development-Modus auto-confirmed)

#### MAUI-App

1. App starten
2. Server-Adresse eingeben (z.B. `192.168.1.100:5000`)
3. Mit erstelltem Account anmelden

## Medienquellen einrichten

### FTP/SFTP-Quelle hinzufügen

1. Im Backend einloggen
2. Zu "Medienquellen" navigieren
3. "Neue Quelle hinzufügen" klicken
4. Konfiguration eingeben:
   - **Name**: Name der Quelle
   - **Typ**: FTP oder SFTP
   - **Server**: IP oder Hostname
   - **Port**: 21 (FTP) oder 22 (SFTP)
   - **Benutzername** & **Passwort**
   - **Pfad**: Basispfad auf Server
5. "Speichern" klicken
6. "Scan starten" klicken

### Erwartete Ordnerstruktur

```
/medienbibliothek/
├── Filme/
│   ├── Movie Name (2023)/
│   │   ├── Movie Name.mp4
│   │   ├── Movie Name.nfo
│   │   ├── Movie Name-poster.jpg
│   │   └── Movie Name-fanart.jpg
│   └── ...
└── Serien/
    ├── Show Name/
    │   ├── Season 01/
    │   │   ├── S01E01 - Episode Name.mp4
    │   │   ├── S01E01 - Episode Name.nfo
    │   │   └── ...
    │   └── ...
    └── ...
```

## Problembehandlung

### Backend startet nicht

**Problem**: Port bereits belegt

**Lösung**:
```json
// appsettings.json
{
  "Host": {
    "Port": "5002"  // Anderen Port verwenden
  }
}
```

### MAUI-App kann nicht verbinden

**Problem**: Server nicht erreichbar

**Diagnose**:
```bash
# Prüfe Server-Erreichbarkeit
curl http://<SERVER_IP>:5000/api/health

# Oder im Browser
http://<SERVER_IP>:5000
```

**Lösung**:
- Firewall-Regeln prüfen
- Server-Adresse korrekt eingegeben?
- Server läuft?

### NuGet-Restore schlägt fehl

**Problem**: Package-Quelle nicht erreichbar

**Lösung**:
```bash
# Package-Quellen anzeigen
dotnet nuget list source

# Package-Quellen hinzufügen
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

### iOS-Build schlägt fehl

**Problem**: Provisioning Profile fehlt

**Lösung**:
1. Xcode öffnen
2. Preferences → Accounts
3. Apple ID hinzufügen
4. Team auswählen
5. In Visual Studio: iOS Bundle Signing konfigurieren

## Build für Produktion

### Backend

```bash
cd VideoWebPlayer
dotnet publish -c Release -o ./publish

# Ausgabe in ./publish/
```

### MAUI-App

#### iOS (App Store)

```bash
cd VideoWebPlayer.Maui
dotnet publish -f net10.0-ios -c Release -p:ArchiveOnBuild=true
```

#### Windows (MSIX)

```bash
cd VideoWebPlayer.Maui
dotnet publish -f net10.0-windows10.0.19041.0 -c Release -p:GenerateAppxPackageOnBuild=true
```

## Deployment

### Docker (Backend)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["VideoWebPlayer/VideoWebPlayer.csproj", "VideoWebPlayer/"]
RUN dotnet restore "VideoWebPlayer/VideoWebPlayer.csproj"
COPY . .
WORKDIR "/src/VideoWebPlayer"
RUN dotnet build "VideoWebPlayer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VideoWebPlayer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VideoWebPlayer.dll"]
```

```bash
# Build
docker build -t videowebplayer:latest .

# Run
docker run -d -p 5000:5000 \
  -e Jwt__Key="..." \
  -e Jwt__ApiToken="..." \
  -v /data/videos:/app/data \
  videowebplayer:latest
```

### Systemd-Service (Linux)

`/etc/systemd/system/videowebplayer.service`:

```ini
[Unit]
Description=VideoWebPlayer Service
After=network.target

[Service]
Type=notify
User=videowebplayer
WorkingDirectory=/opt/videowebplayer
ExecStart=/usr/bin/dotnet /opt/videowebplayer/VideoWebPlayer.dll
Restart=always
RestartSec=10
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="Jwt__Key=..."
Environment="Jwt__ApiToken=..."

[Install]
WantedBy=multi-user.target
```

```bash
# Enable & Start
sudo systemctl enable videowebplayer
sudo systemctl start videowebplayer

# Status
sudo systemctl status videowebplayer
```

## Nächste Schritte

Nach erfolgreicher Installation:

1. 📖 [Benutzerhandbuch](./GUIDE_User_Manual.md) - Nutzung der Anwendung
2. 🔧 [Technische Dokumentation](./INDEX.md) - Für Entwickler
3. 🎯 [Feature-Übersicht](./GUIDE_Features.md) - Alle Features im Detail

## Support

Bei Problemen:
- **Issues**: [GitHub Issues](https://github.com/Muesli84/VideoPlayer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Muesli84/VideoPlayer/discussions)

---

**Letzte Aktualisierung**: 2024  
**Getestet mit**: .NET 10, Visual Studio 2022 17.8+
