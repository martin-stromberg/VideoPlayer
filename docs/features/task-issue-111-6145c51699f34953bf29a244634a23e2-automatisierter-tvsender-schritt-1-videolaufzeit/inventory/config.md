# Konfiguration und Infrastruktur

## Konfigurationsmechanismen

| Mechanismus | Ort | Verwendung |
|-------------|-----|------------|
| `services.Configure<T>(configuration.GetSection("X"))` / `services.AddOptions<T>().Bind(...).ValidateOnStart()` | `ServiceCollectionExtensions` | `Playlists` → `PlaylistSettings`; `EpisodeBackgroundImage` → `EpisodeBackgroundImageOptions`; `Mdns` → `MdnsOptions`; `Discovery` → `DiscoveryOptions` |
| DB-persistente Einstellungen (`Setup`-Tabelle) | `ProgramSettingsService` | Scan-Intervalle, Apptitel, mDNS-Flag — durch Admin änderbar |
| Options-Validatoren | `Configuration/*OptionsValidator.cs` (`IValidateOptions<T>`) | `MdnsOptionsValidator`, `DiscoveryOptionsValidator` |

`appsettings.json` enthält Abschnitte `Playlists`, `Backups`, `EpisodeBackgroundImage`, `Mdns`, `Discovery`, `Serilog`, `AutoUpdate` (u. a. `AutoUpdate:ProtectedFiles` mit Merge-Strategie und `appsettings*.json`-`JsonKeys`-Liste — neue appsettings-Schlüssel brauchen dort nur dann einen Eintrag, wenn admin-gesetzte Werte über Updates erhalten bleiben sollen; hier nicht nötig, da der Pfad ein Deployment-Wert ist, der in `appsettings.json` selbst bzw. `appsettings.Local.json`/`Production.json` stehen kann).

`appsettings.Testing.json` steuert die E2E-/Testumgebung (WebApplicationFactory nutzt Umgebung `Testing`; dort wird `MdnsAdvertiserWorker` nicht registriert).

## Build / CI

- `VideoPlayer.sln` enthält `VideoWebPlayer` (net10.0, ASP.NET Core + Blazor Server), `VideoWebPlayer.Client`, `VideoWebPlayer.Tests`, `tools/MarkdownLinkCheck`, `tools/SecretScan`.
- CI: `.github/workflows/pr-staging-ci.yml` und `staging-ci.yml` bauen Debug (Solution) + Release (Testprojekt), installieren Playwright-Chromium und führen `dotnet test` gesplittet in `Category!=E2E` (mit Coverage), `Category=Integration`, `Category=E2E` aus; `release.yml` führt die volle Suite aus. Kein Schritt für externe Binaries vorhanden.
- `build_script.bat` ist ein veralteter Maui-Build-Stub (pfadfest auf ein anderes Repo) — nicht relevant.
- Publish: Standard `dotnet publish`; `release-metadata.json` wird per MSBuild-Target erzeugt. Kein Installer-Projekt im Repo — Auslieferung der Binaries erfolgt nicht über den Build.

## ffprobe-Bereitstellung (Ergebnis der Prüfung)

- Im Repo existiert **kein** Mechanismus zum Mitliefern externer nativer Binaries (`tools/` enthält nur eigene .NET-Tools `MarkdownLinkCheck`, `SecretScan`; `lib/` nur NuGet-Caches).
- Konsequenz: ffprobe wird **nicht** ins Repo eingecheckt und nicht in der CI gebaut. Bereitstellung erfolgt zur Laufzeit: konfigurierbarer Pfad (`MediaProbe:FfprobePath`), Konventionsverzeichnis `tools/ffmpeg/` bzw. `ffprobe` neben der App (`AppContext.BaseDirectory`) oder `PATH`-Auflösung. Fehlt das Binary, bleiben Items ungeprüft (kein Fehlerzustand), der Worker wartet.
- Lizenz: siehe [ffmpeg.md](ffmpeg.md).
