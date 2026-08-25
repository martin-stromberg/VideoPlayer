# Installation und Setup

> **Dokumenttyp**: Allgemeine Dokumentation  
> **Zielgruppe**: Entwickler, Administratoren  
> **Version**: 2.0
> **Letzte Aktualisierung**: 2026-08-25

Diese Anleitung beschreibt die Einrichtung des Web-Repositorys unter Linux und Windows. Die .NET-MAUI-App wird separat im MAUI-Repository gepflegt; in dieser Arbeitskopie liegt der vorhandene Klon unter `Sub-Repository/`.

## Voraussetzungen

### Web-Repository

- .NET 10 SDK.
- Git.
- Zugriff auf NuGet.org.
- Lokale Paketquelle `lib/packages/` aus `NuGet.config`.
- Lokale DLL `lib/msTools.Updater/msTools.Updater.dll`.
- Schreibrechte im Arbeitsverzeichnis für SQLite-Datenbank, Logs, Backups, Updates und generierte Bilddateien.
- Optional: Visual Studio 2022 oder neuer mit ASP.NET-/Web-Workload.

### MAUI-Repository

- .NET 10 SDK.
- MAUI-Workload passend zum Zielsystem.
- Windows: Windows App SDK/Windows-Zielplattform für `net10.0-windows10.0.19041.0`.
- Android: Android SDK und `net10.0-android`.
- iOS/MacCatalyst: macOS mit Xcode; diese Targets sind auf Linux nicht buildbar.

## Linux: Web installieren

```bash
git clone <REPOSITORY_URL> VideoWebPlayer
cd VideoWebPlayer
dotnet --version
dotnet restore VideoPlayer.sln
dotnet build VideoPlayer.sln
```

Secrets für Entwicklung setzen:

```bash
cd VideoWebPlayer
dotnet user-secrets init --project VideoWebPlayer/VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:Key" "<ENTWICKLUNGS_JWT_KEY_BASE64_MIN_32_BYTES>" --project VideoWebPlayer/VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:ApiToken:Web" "<ENTWICKLUNGS_WEB_API_TOKEN>" --project VideoWebPlayer/VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:ApiToken:Maui" "<ENTWICKLUNGS_MAUI_API_TOKEN>" --project VideoWebPlayer/VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:Issuer" "VideoWebPlayer" --project VideoWebPlayer/VideoWebPlayer.csproj
cd ..
```

Anwendung starten:

```bash
dotnet run --project VideoWebPlayer/VideoWebPlayer.csproj
```

Erreichbarkeit prüfen:

```bash
curl http://localhost:5039/api/health
```

Wenn ein anderes Launch-Profil oder `Host:Port` verwendet wird, die URL entsprechend anpassen. Die Projekt-Launch-Profile enthalten `http://localhost:57331` und `http://localhost:5039`; die Discovery-Adresse fällt ohne Konfiguration auf `http://localhost:5000` zurück.

## Windows: Web installieren

```powershell
git clone <REPOSITORY_URL> VideoWebPlayer
Set-Location VideoWebPlayer
dotnet --version
dotnet restore .\VideoPlayer.sln
dotnet build .\VideoPlayer.sln
```

Secrets für Entwicklung setzen:

```powershell
Set-Location VideoWebPlayer
dotnet user-secrets init --project .\VideoWebPlayer\VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:Key" "<ENTWICKLUNGS_JWT_KEY_BASE64_MIN_32_BYTES>" --project .\VideoWebPlayer\VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:ApiToken:Web" "<ENTWICKLUNGS_WEB_API_TOKEN>" --project .\VideoWebPlayer\VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:ApiToken:Maui" "<ENTWICKLUNGS_MAUI_API_TOKEN>" --project .\VideoWebPlayer\VideoWebPlayer.csproj
dotnet user-secrets set "Jwt:Issuer" "VideoWebPlayer" --project .\VideoWebPlayer\VideoWebPlayer.csproj
Set-Location ..
```

Anwendung starten:

```powershell
dotnet run --project .\VideoWebPlayer\VideoWebPlayer.csproj
```

Erreichbarkeit prüfen:

```powershell
Invoke-WebRequest http://localhost:5039/api/health
```

## Produktive Konfiguration

Produktive Werte dürfen nicht aus dieser Dokumentation übernommen werden. Erzeuge eigene Secrets und setze sie als Umgebungsvariablen oder über ein Secret-Management-System:

```bash
export Jwt__Key="<PRODUKTIVER_JWT_KEY>"
export Jwt__ApiToken__Web="<PRODUKTIVER_WEB_API_TOKEN>"
export Jwt__ApiToken__Maui="<PRODUKTIVER_MAUI_API_TOKEN>"
export Jwt__Issuer="VideoWebPlayer"
```

```powershell
$env:Jwt__Key = "<PRODUKTIVER_JWT_KEY>"
$env:Jwt__ApiToken__Web = "<PRODUKTIVER_WEB_API_TOKEN>"
$env:Jwt__ApiToken__Maui = "<PRODUKTIVER_MAUI_API_TOKEN>"
$env:Jwt__Issuer = "VideoWebPlayer"
```

## Markdown-Linkcheck aktivieren

Der Linkcheck prüft lokale Markdown-Links ohne externe Netzwerkzugriffe:

```bash
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
```

Unter Windows PowerShell:

```powershell
dotnet run --no-restore --project .\tools\MarkdownLinkCheck\MarkdownLinkCheck.csproj -- --root .
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
```

Der Hook läuft lokal vor Commits. Er ist versioniert, wird aber nicht automatisch durch Git aktiviert. Die Stage-Ausgabe muss mit `100755` beginnen; falls ein Linux-Arbeitsbaum `100644` zeigt, `chmod +x .githooks/pre-commit` ausführen und die Git-Metadaten vor der Veröffentlichung erneut prüfen.

## MAUI-Setup

Im MAUI-Repository:

```bash
dotnet restore VideoPlayer.App.sln
dotnet test VideoWebPlayer.Maui.Tests/VideoWebPlayer.Maui.Tests.csproj
dotnet build VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj -p:MauiClientApiToken="<ENTWICKLUNGS_MAUI_API_TOKEN>"
```

Alternativ kann der Client-Gate-Wert zur Laufzeit über `VIDEOWEBPLAYER_MAUI_API_TOKEN` gesetzt werden. Der Wert muss backendseitig zu `Jwt:ApiToken:Maui` passen. Er ist kein Ersatz für Benutzer-Authentifizierung, muss aber als sensibler gemeinsam konfigurierter Zugangswert geschützt und darf nicht in öffentliche Dokumentation oder Logs übernommen werden. Benutzerpasswörter werden im MAUI-Client nicht in `Preferences`, sondern im plattformsicheren Speicher abgelegt.

Windows-Ziel:

```powershell
dotnet build .\VideoWebPlayer.Maui\VideoWebPlayer.Maui.csproj -f net10.0-windows10.0.19041.0
```

Android-Ziel:

```bash
dotnet build VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj -f net10.0-android
```

Linux kann das MAUI-Testprojekt und das gemeinsame Client-Projekt prüfen, aber keine iOS-, MacCatalyst- oder Windows-MAUI-Ziele bauen.

## Tests

```bash
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj
```

API-Dokumentationsvertrag:

```bash
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter ApiDocumentationContractTests
```

## Häufige Fehler

### Restore findet lokale Pakete nicht

Prüfe, ob `lib/packages/` vorhanden ist und `NuGet.config` die Quelle enthält:

```bash
dotnet nuget list source
```

### `msTools.Updater.dll` fehlt

Die Webprojektdatei referenziert `lib/msTools.Updater/msTools.Updater.dll`. Lege die DLL aus dem vorgesehenen Release-Paket an diesem Pfad ab.

### Port ist belegt

Starte mit einer anderen URL:

```bash
dotnet run --project VideoWebPlayer/VideoWebPlayer.csproj --urls http://localhost:5220
```

### Health-Check liefert `401`

`GET /api/health` benötigt keine Authentifizierung. Wird `401` zurückgegeben, wurde vermutlich eine andere Route oder ein Proxy geprüft.

### MAUI-App verbindet sich nicht

Prüfe die aus Sicht des Geräts erreichbare Serveradresse. Android-Emulatoren verwenden häufig `10.0.2.2`, physische Geräte die LAN-IP des Entwicklungsrechners. Der MAUI-Client benötigt zusätzlich den Client-Gate-Wert, der backendseitig zu `Jwt:ApiToken:Maui` passt; der Backendwert ist als sensibler Konfigurationswert zu schützen.

## Nächste Schritte

- [API-Vertrag prüfen](./API.md)
- [Secrets Management prüfen](./SECRETS_MANAGEMENT.md)
- [Veröffentlichungscheckliste abarbeiten](./PUBLICATION_CHECKLIST.md)
