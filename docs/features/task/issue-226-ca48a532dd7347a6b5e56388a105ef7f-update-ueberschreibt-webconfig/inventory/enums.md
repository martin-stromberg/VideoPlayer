# Enums

## `ApiTokenScope`
Datei: `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs` (Zeilen 102–113)

| Wert | Bedeutung |
|------|-----------|
| `AnyClient` | akzeptiert `Jwt:ApiToken`, `Jwt:ApiToken:Web` und `Jwt:ApiToken:Maui` |
| `MauiOnly` | akzeptiert nur `Jwt:ApiToken:Maui` sowie individuelle Gerätetokens via `IDeviceTokenService` |

## `AutoUpdateState` (msTools.Updater)
Datei: extern, `msTools.Updater.dll` — verwendet u. a. in `UpdateAdminService.IsBusy`/`IsInstallable`.

| Wert | Bedeutung |
|------|-----------|
| `Idle` | keine Update-Aktivität |
| `Checking` | Quelle wird geprüft |
| `UpdateAvailable` | neuere Version gefunden |
| `Downloading` | Download läuft |
| `ReadyToInstall` | Paket validiert, installierbar |
| `Installing` | Installationsskript gestartet |
| `Failed` | Fehlerzustand (in `MsToolsUpdaterIntegrationTests` gesetzt) |

## `AutoUpdateOutcome` (msTools.Updater)
Verwendet in `UpdateAdminService.ToActionResult`: `Success`, `Failed`, `Skipped`, `Canceled`.
