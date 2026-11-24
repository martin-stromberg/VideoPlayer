# Paketreferenzen und Update-Lage

Alle `PackageReference`-Einträge der `VideoPlayer.sln`, exakte Versionsstände
(Branch `task/issue-380-…`, identisch mit `origin/staging` @ `5e4737e`) und die
Ergebnisse der Paket-Abfragen vom 2026-11-23 (lokales SDK `11.0.100`; Quellen:
`https://api.nuget.org/v3/index.json` + lokaler Feed `lib/packages`).

## `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 18–42, 58–59)

| Paket | Version (csproj-Zeile) | Neueste laut `--outdated` | Kategorie |
|---|---|---|---|
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 (Z.18) | 11.0.0 | Major |
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 10.0.12 (Z.19) | 11.0.0 | Major |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 (Z.20) | 11.0.0 | Major |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 (Z.21–24, `PrivateAssets=all`) | 11.0.0 | Major |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 (Z.25) | 11.0.0 | Major |
| Microsoft.EntityFrameworkCore.Tools | 10.0.12 (Z.26–29, `PrivateAssets=all`) | 11.0.0 | Major |
| Makaretu.Dns.Multicast.New | 0.38.0 (Z.30) | – (kein Update) | – |
| Nito.AsyncEx | 5.1.2 (Z.31) | – | – |
| QRCoder | 1.8.0 (Z.32) | 1.9.0 | Minor |
| SharpCompress | 0.50.5 (Z.33) | 0.50.11 | Patch |
| SkiaSharp | 4.153.1 (Z.34) | 4.157.0 | eigenes Versionsmodell |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.153.1 (Z.35) | 4.157.0 | eigenes Versionsmodell |
| SSH.NET | 2026.0.0 (Z.36) | 2026.0.1 | Jahreszahl-Modell, Patch |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 (Z.37) | – | – |
| Serilog.AspNetCore | 10.0.0 (Z.38) | 11.0.0 | Major |
| Serilog.Settings.Configuration | 10.0.1 (Z.39) | 11.0.0 | Major |
| Serilog.Sinks.Console | 6.1.1 (Z.40) | – | – |
| Serilog.Sinks.File | 7.0.0 (Z.41) | – | – |
| System.IdentityModel.Tokens.Jwt | 8.22.0 (Z.42) | 8.23.0 | Minor |
| msTools.Backup | 1.1.0-RC.4 (Z.58) | „Nicht in den Quellen gefunden" | RC, nur lokaler Feed |
| msTools.Updater | 0.11.0-rc.6 (Z.59) | „Nicht in den Quellen gefunden" | RC, nur lokaler Feed |

Zusätzlich: `ProjectReference` auf `VideoWebPlayer.Client` (Z.60);
`InternalsVisibleTo` für `VideoWebPlayer.Tests` (Z.15).

## `VideoWebPlayer.Client/VideoWebPlayer.Client.csproj` (Z. 13–15)

| Paket | Version | Neueste | Kategorie |
|---|---|---|---|
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 (Z.13) | 11.0.0 | Major |
| Microsoft.Extensions.Http | 10.0.12 (Z.14) | 11.0.0 | Major |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 (Z.15) | 11.0.0 | Major |

## `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 12–27)

| Paket | Version | Neueste | Kategorie |
|---|---|---|---|
| bunit | 2.11.3 (Z.12) | 2.15.2 | Minor |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 (Z.13) | 11.0.0 | Major |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.12 (Z.14) | 11.0.0 | Major |
| Microsoft.NET.Test.Sdk | 18.10.0 (Z.15) | 18.12.0 | Minor |
| Microsoft.Playwright | 1.62.0 (Z.16) | 1.65.1 | Minor |
| Moq | 4.20.72 (Z.17) | 4.21.0 | Minor |
| xunit.v3 | 3.2.2 (Z.18) | 4.0.3 | Major |
| xunit.runner.visualstudio | 3.1.5 (Z.19) | 4.0.1 | Major |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 (Z.20) | 11.0.0 | Major |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 (Z.21) | 11.0.0 | Major |
| msTools.Updater | 0.11.0-rc.6 (Z.22) | „Nicht in den Quellen gefunden" | RC, nur lokaler Feed |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 (Z.23) | – | – |
| coverlet.collector | 10.0.1 (Z.24–27, `PrivateAssets=all`) | 10.1.1 | Minor |

## `tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj` (Z. 10–12)

| Paket | Version | Neueste | Kategorie |
|---|---|---|---|
| Microsoft.NET.Test.Sdk | 18.10.0 (Z.10) | 18.12.0 | Minor |
| xunit.v3 | 3.2.2 (Z.11) | 4.0.3 | Major |
| xunit.runner.visualstudio | 3.1.5 (Z.12) | 4.0.1 | Major |

## Projekte ohne `PackageReference`

- `tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj` — keine Pakete (Exe, net10.0).
- `tools/SecretScan/SecretScan.csproj` — keine Pakete (Exe, net10.0).

## Mehrfachreferenzen (Versionsgleichheit erforderlich, requirement.md Z. 26–29)

| Paket | Projekte | Gemeinsame Version |
|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite | VideoWebPlayer, VideoWebPlayer.Tests | 10.0.12 |
| msTools.Updater | VideoWebPlayer, VideoWebPlayer.Tests | 0.11.0-rc.6 |
| SQLitePCLRaw.lib.e_sqlite3 | VideoWebPlayer, VideoWebPlayer.Tests | 3.53.3 |
| Microsoft.Extensions.Logging.Abstractions | VideoWebPlayer.Client, VideoWebPlayer.Tests | 10.0.12 |
| Microsoft.NET.Test.Sdk | VideoWebPlayer.Tests, MarkdownLinkCheck.Tests | 18.10.0 |
| xunit.v3 | VideoWebPlayer.Tests, MarkdownLinkCheck.Tests | 3.2.2 |
| xunit.runner.visualstudio | VideoWebPlayer.Tests, MarkdownLinkCheck.Tests | 3.1.5 |

Die `Microsoft.*`-10.0.12-Familie (AspNetCore/EFCore/Extensions, insgesamt 13 Referenzen
über alle drei Hauptprojekte) bildet faktisch eine Einheit: „Neueste" ist durchgängig
`11.0.0` (net11-Linie — Update setzt voraussichtlich `net11.0`-TFM bzw. .NET-11-SDK in
der CI voraus → Kandidat für Risiko „hoch"/aufgeschoben).

## Rohausgaben der Abfragen (Kernfakten)

### `dotnet list VideoPlayer.sln package --vulnerable --include-transitive`

> Für alle sechs Projekte: „liegen gemäß den aktuellen Quellen **keine anfälligen
> Pakete** vor." (kein direkter, kein transitiver Befund)

### `dotnet list VideoPlayer.sln package --deprecated`

> Für alle sechs Projekte: „keine veralteten Pakete" (= nicht als deprecated markiert).

### `dotnet list VideoPlayer.sln package --outdated`

> Tabelle wie oben; Projekte `MarkdownLinkCheck` und `SecretScan`: „keine Updates".
> `msTools.Backup`/`msTools.Updater`: „Nicht in den Quellen gefunden" für „Neueste"
> (lokale Prerelease-Feed-Pakete, `lib/packages`).

### Tooling-Quirk

`dotnet list VideoPlayer.sln package --outdated --include-transitive` bricht mit
`error: Sequence contains no matching element` ab (Exit 1). Ohne
`--include-transitive` läuft der Befehl sauber; `--vulnerable --include-transitive`
funktioniert dagegen. Für die transitive Update-Sicht ggf. pro Projekt oder ohne
Transitive arbeiten.

### Dependabot / Advisory-Quellen

- `gh api repos/{owner}/{repo}/dependabot/alerts` → `[]` (keine offenen Alerts;
  Dependabot ist nicht eingerichtet — kein `dependabot.yml`/`renovate` im Repo).
- `security-scan.yml` (Mo 04:00 UTC) lief am 2026-11-23 erfolgreich — siehe
  [ci.md](ci.md).

## Nicht-NuGet-Abhängigkeiten (Kontext, kein Scope der Anforderung)

- `package.json`/`package-lock.json`: reine Release-Tooling-devDependencies
  (`semantic-release` ^25.0.9, `@semantic-release/*`, `conventional-changelog-*`)
  für `release.yml`/`staging-ci.yml` (`npm ci`).
- `dotnet-reportgenerator-globaltool` wird in der CI gepinnt installiert
  (`--version 5.5.11`, beide CI-Workflows) — kein `dotnet-tools.json`-Manifest.
- ffmpeg/ffprobe sind Laufzeit-Abhängigkeiten außerhalb von NuGet.
