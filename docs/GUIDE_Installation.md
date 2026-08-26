# Installation und Setup

> **Dokumenttyp**: Allgemeine Dokumentation  
> **Zielgruppe**: Entwickler, Administratoren  
> **Version**: 2.0
> **Letzte Aktualisierung**: 2026-08-25

Diese Anleitung beschreibt die Einrichtung des Web-Repositorys unter Linux und Windows.

## Voraussetzungen

- .NET 10 SDK.
- Git.
- Zugriff auf NuGet.org.
- Lokale Paketquelle `lib/packages/` aus `NuGet.config`.
- Lokale DLL `lib/msTools.Updater/msTools.Updater.dll`.
- Schreibrechte im Arbeitsverzeichnis für SQLite-Datenbank, Logs, Backups, Updates und generierte Bilddateien.
- Optional: Visual Studio 2022 oder neuer mit ASP.NET-/Web-Workload.

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
export Jwt__Issuer="VideoWebPlayer"
```

```powershell
$env:Jwt__Key = "<PRODUKTIVER_JWT_KEY>"
$env:Jwt__ApiToken__Web = "<PRODUKTIVER_WEB_API_TOKEN>"
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

## Nächste Schritte

- [API-Vertrag prüfen](./API.md)
- [Secrets Management prüfen](./SECRETS_MANAGEMENT.md)
- [Veröffentlichungscheckliste abarbeiten](./PUBLICATION_CHECKLIST.md)
