# Solution-, Projekt- und NuGet-Infrastruktur

## `VideoPlayer.sln` — Projekte

Sechs Projekte (SDK-style `.csproj`, kein `.slnx`):

| Projekt | Pfad | SDK | Zweck |
|---|---|---|---|
| VideoWebPlayer | `VideoWebPlayer/VideoWebPlayer.csproj` | `Microsoft.NET.Sdk.Web` | Blazor-Server-Anwendung (Produktivcode) |
| VideoWebPlayer.Client | `VideoWebPlayer.Client/VideoWebPlayer.Client.csproj` | `Microsoft.NET.Sdk` | Client-Bibliothek (geteilter Code) |
| VideoWebPlayer.Tests | `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | `Microsoft.NET.Sdk` | Haupt-Testprojekt (`IsTestProject`) |
| MarkdownLinkCheck | `tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj` | `Microsoft.NET.Sdk` | Dev-Tool (Exe, Solution-Ordner `tools`) |
| MarkdownLinkCheck.Tests | `tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj` | `Microsoft.NET.Sdk` | Tests für das Tool |
| SecretScan | `tools/SecretScan/SecretScan.csproj` | `Microsoft.NET.Sdk` | Dev-Tool (Exe) |

Referenzgraph: `VideoWebPlayer` → `VideoWebPlayer.Client`;
`VideoWebPlayer.Tests` → `VideoWebPlayer` (damit transitiv `.Client`);
`MarkdownLinkCheck.Tests` → `MarkdownLinkCheck`.
`VideoWebPlayer.csproj` gewährt `InternalsVisibleTo` auf `VideoWebPlayer.Tests`.

## Ziel-Frameworks und SDK

- **Alle sechs Projekte zielen auf `net10.0`.**
- Kein `global.json` — die SDK-Auswahl folgt der Installation: lokal `11.0.100`
  (`dotnet --version`), in der CI `actions/setup-dotnet@v6` mit `dotnet-version:
  '10.0.x'` (alle Workflows einheitlich).
- Konsequenz für Updates: „Neueste" `Microsoft.*`/`Serilog.*`-Pakete liegen auf
  `11.0.0` (net11-Linie). Ein Bump der 10.0.12-Familie auf 11.x würde Zielframework
  bzw. CI-SDK betreffen — per requirement.md/AGENTS.md Kandidat für Risiko „hoch".
- `WarningsAsErrors`: `CS1591` in `VideoWebPlayer` und `.Client`; Nullable/ImplicitUsings
  überall aktiv.

## NuGet-Konfiguration

`NuGet.config` (Repo-Wurzel, gesamte Datei 8 Zeilen):

```xml
<packageSources>
  <clear />
  <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  <add key="local" value="lib/packages" />
</packageSources>
```

- `nuget.org` als einzige Remote-Quelle; `<clear/>` verhindert geerbte Quellen.
- **Lokaler Feed `lib/packages`** (versionskontrolliert, 2 Dateien):
  - `msTools.Backup.1.1.0-RC.4.nupkg` (38 KB)
  - `msTools.Updater.0.11.0-rc.6.nupkg` (131 KB)
  - Beide Pakete sind nicht auf nuget.org veröffentlicht; Updates laufen manuell:
    neues nupkg ablegen, altes entfernen, `PackageReference` in **beiden** csproj
    erhöhen (VideoWebPlayer + Tests). Verfahren steht in
    `docs/Anforderung_msTools_Updater_Dateierhalt.md` Z. 115 und den Task-Listen
    `docs/features/task/b21855dd…-mstoolsupdater0110-rc{4,5,6}-tasks.md`;
    `docs/GUIDE_Installation.md` Z. 15, 227–235 beschreibt den Feed.
- Git-Historie zeigt den Rhythmus: `9e149eb` msTools.Updater→rc.6,
  `a566f09` →rc.5, `7ce924b` →rc.4 (jeweils eigene Commits + nupkg-Tausch).

## Zentrale Paketverwaltung / Lock-Artefakte

- **Kein CPM:** `Directory.Packages.props` existiert nicht — Versionen stehen
  direkt in den csproj (`Version="-Attribut`, kein `VersionOverride`).
- **Keine Lock-Files:** kein `packages.lock.json`, kein `RestorePackagesWithLockFile`.
- **Keine gemeinsamen MSBuild-Dateien:** kein `Directory.Build.props`/
  `Directory.Build.targets` — Wiederholungen (TFM, Nullable, Lizenz-Meta) sind
  in jeder csproj einzeln gepflegt.
- **Kein Tool-Manifest:** kein `.config/dotnet-tools.json`; das einzige
  CI-Tool (`reportgenerator`) wird gepinnt per `dotnet tool install -g …
  --version 5.5.11` installiert.
- **Kein Dependabot/Renovate:** keine `dependabot.yml`/`renovate.json`
  (requirement.md schließt deren Einrichtung ausdrücklich aus).

## csproj-Besonderheiten mit Paket-Bezug

- `VideoWebPlayer.csproj`: `Microsoft.EntityFrameworkCore.Design`/`.Tools` mit
  `PrivateAssets=all` + `IncludeAssets runtime; build; native; contentfiles;
  analyzers; buildtransitive` (Z. 21–29). `coverlet.collector` im Testprojekt
  ebenso (Z. 24–27).
- `VideoWebPlayer.csproj` Z. 93–106/113–122: zwei eigene MSBuild-Targets
  (`CopyStandardSourcesDemoData`, `GenerateReleaseMetadata` schreibt
  `release-metadata.json` — Laufzeit-Basis für `msTools.Updater`s
  `ReleaseMetadataInstalledVersionProvider`, vgl. `build-and-package/action.yml`).
- `lib/` enthält ausschließlich `lib/packages` — das in `docs/TECH_Auto_Update.md`
  Z. 10–19 beschriebene `lib/msTools.Updater/`-Dateireferenz-Modell existiert
  nicht mehr (veralteter Doc-Bestand, siehe documentation.md).
- `build_script.bat` (Wurzel) referenziert noch `VideoWebPlayer.Maui`/
  `net9.0-android` — totes Legacy-Artefakt, nicht Teil der Solution.

## npm-Artefakte (Release-Tooling, Kontext)

`package.json` + `package-lock.json` (Wurzel): private Paket
`videoplayer-release`, devDependencies `semantic-release` ^25.0.9,
`@semantic-release/commit-analyzer` ^13.0.1, `@semantic-release/github` ^12.0.9,
`@semantic-release/release-notes-generator` ^14.1.1,
`conventional-changelog-conventionalcommits` ^9.3.1; Script `npm run release`.
Genutzt von `staging-ci.yml` (version-Job, `npx semantic-release --dry-run`) und
`release.yml` (`npm ci`, `npm run release`). `release.config.js` listet nur
`main` als Release-Branch.
