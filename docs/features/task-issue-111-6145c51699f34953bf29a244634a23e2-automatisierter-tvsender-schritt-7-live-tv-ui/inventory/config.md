# Konfiguration, Assets und externe Abhängigkeiten

## Externe Assets / Third-Party-Muster

- `VideoWebPlayer/wwwroot/bootstrap/bootstrap.min.css` (+ `.map`) ist als vendored Third-Party-Asset eingecheckt — direkter Präzedenzfall für ein vendored `hls.min.js`.
- `VideoWebPlayer/wwwroot/js/*.js` — eigene Interop-Module (`window.*`), in `App.razor` via `StaticAssetVersioner` referenziert.
- `StaticAssetVersioner` (`VideoWebPlayer/Services/StaticAssetVersioner.cs`): Singleton, `Url(path)` hängt Content-Fingerprint `?v=` an.

## npm / Paketmanagement

- `package.json` im Wurzelverzeichnis enthält **nur** `devDependencies` für `semantic-release` (`npm run release`); die Anwendung selbst hat **keinen** npm-Buildschritt — `dotnet publish` (`.github/actions/build-and-package`) packt `wwwroot` direkt mit ein; es gibt keinen Kopiervorgang von `node_modules` nach `wwwroot`.
- `lib/packages/` enthält zwei lokale `.nupkg`-Dateien (msTools.Backup/Updater via `NuGet.config`) — NuGet-intern, kein JS.

## CI / Release

- `.github/workflows/pr-staging-ci.yml` / `staging-ci.yml` / `release.yml`; Paketierung über `.github/actions/build-and-package` (`dotnet publish` win-x64 + linux-x64 self-contained, ZIP + `update.json`). Ein vendored wwwroot-Asset fließt automatisch durch — kein Workflow-Eingriff nötig.
- Sicherheitsscan (`.github/workflows/security-scan.yml` / `.github/actions/security-scan`) scannt NuGet-Pakete — ein vendored JS-Asset ist davon nicht erfasst (Feststellung, dokumentiert im Plan).

## ffmpeg (Kontext)

- `IFfmpegLocator`/`FfmpegLocator` (Singleton) analog `IFfprobeLocator` — serverseitig, für die UI irrelevant.

## `appsettings`

- `MediaProbe`-Sektion (`FfprobePath`/`FfmpegPath`) — serverseitig, keine UI-Auswirkung.
- Keine UI-relevanten neuen Konfigurationseinträge erforderlich.
