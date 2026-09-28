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
git clone https://github.com/martin-stromberg/VideoPlayer.git VideoWebPlayer
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
git clone https://github.com/martin-stromberg/VideoPlayer.git VideoWebPlayer
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
export Jwt__ApiToken__Maui="<PRODUKTIVER_MAUI_API_TOKEN>"
export Jwt__Issuer="VideoWebPlayer"
```

```powershell
$env:Jwt__Key = "<PRODUKTIVER_JWT_KEY>"
$env:Jwt__ApiToken__Web = "<PRODUKTIVER_WEB_API_TOKEN>"
$env:Jwt__ApiToken__Maui = "<PRODUKTIVER_MAUI_API_TOKEN>"
$env:Jwt__Issuer = "VideoWebPlayer"
```

`Jwt:ApiToken:Maui` ist in Produktion Pflicht und dient als Fallback-Gate-Token für ältere App-Versionen. Neuere Apps koppeln sich über das Geräte-Pairing (siehe nächster Abschnitt) und erhalten ein individuelles, widerrufbares Geräte-Token.

## Geräte-Pairing

Client-Apps melden sich mit einem individuellen Geräte-Token als `X-API-Key` an. Das Token wird nicht manuell verteilt, sondern über einen Einmal-Pairing-Code eingelöst:

1. Als Administrator die Web-UI öffnen und unter `Einrichtung` die Kachel `Geräte` (`/admin/devices`) aufrufen.
2. `Pairing-Code erzeugen` klicken. Der Code wird einmalig im Klartext angezeigt und ist standardmäßig 5 Minuten gültig.
3. In der App den Code eingeben. Die App ruft `POST /api/pairing/exchange` auf und erhält daraus ein verschlüsseltes Geräte-Token (ECDH P-256 + AES-256-GCM, Details siehe [API-Vertrag](./API.md)).
4. Das Gerät erscheint in der Liste `Gekoppelte Geräte`. Über `Widerrufen` wird das Geräte-Token sofort ungültig; bereits ausgestellte Benutzer-JWTs bleiben bis zum Ablauf (12 Stunden) gültig.

Optionale Konfiguration (Defaults im Code, keine Pflichtwerte):

```bash
export Pairing__CodeLength="8"
export Pairing__CodeTtlMinutes="5"
```

| Schlüssel | Standard | Zweck |
|-----------|----------|-------|
| `Pairing:CodeLength` | `8` | Länge des Pairing-Codes (Alphabet ohne verwechselbare Zeichen `0/O/1/I/L`). |
| `Pairing:CodeTtlMinutes` | `5` | Gültigkeitsdauer eines Pairing-Codes in Minuten. |

Wiederholte Einlöse-Fehlversuche sperren die Client-IP (Schwelle: 5, geteilt mit dem Web-Login); gesperrte IPs werden unter `Einrichtung` → `Sicherheit` angezeigt und können dort entsperrt werden.

## Lokale Git-Hooks aktivieren

Die Hooks blockieren direkte Commits und Pushes auf `main` und `staging`, weil diese Branches nur über Pull Requests aktualisiert werden dürfen. Der Pre-Commit-Hook blockiert außerdem mögliche GitHub-Tokens in gestagten Dateien und konfigurierten Remote-URLs. Danach prüft er lokale Markdown-Links ohne externe Netzwerkzugriffe:

```bash
dotnet run --no-restore --project tools/SecretScan/SecretScan.csproj -- --root .
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
git ls-files --stage .githooks/pre-push
```

Unter Windows PowerShell:

```powershell
dotnet run --no-restore --project .\tools\SecretScan\SecretScan.csproj -- --root .
dotnet run --no-restore --project .\tools\MarkdownLinkCheck\MarkdownLinkCheck.csproj -- --root .
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
git ls-files --stage .githooks/pre-push
```

Die Hooks laufen lokal vor Commits und Pushes. Sie sind versioniert, werden aber nicht automatisch durch Git aktiviert. Die Stage-Ausgabe muss jeweils mit `100755` beginnen; falls ein Linux-Arbeitsbaum `100644` zeigt, `chmod +x .githooks/pre-commit .githooks/pre-push` ausführen und die Git-Metadaten vor der Veröffentlichung erneut prüfen.

Die lokale Hook-Sperre ersetzt keine serverseitige Branch Protection auf GitHub. `main` und `staging` müssen dort zusätzlich so konfiguriert werden, dass direkte Pushes verhindert und Änderungen nur über Pull Requests gemergt werden.

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
