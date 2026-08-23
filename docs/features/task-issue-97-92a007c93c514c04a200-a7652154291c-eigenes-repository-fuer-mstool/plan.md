# Umsetzungsplan

## Ziel

`msTools.Backup` und `msTools.Backup.Tests` werden aus dem Haupt-Repository in das eigene Repository `Sub-Repository/` ausgelagert, dort als NuGet-Paket konfiguriert und mit einem eigenen GitHub-Workflow für Staging-RC- und Main-Releases versehen. Im Haupt-Repository wird das Ergebnis anschließend als lokales NuGet-Paket eingebunden und der Quellcode des Sub-Repositorys lokal entfernt.

## Offene Punkte

Keine.

## Schritte

### 1. Projekt in das Ziel-Repository verschieben

- Kopiere bzw. verschiebe die Ordner `msTools.Backup/` und `msTools.Backup.Tests/` aus dem Haupt-Repository in `Sub-Repository/src/` (und `Sub-Repository/tests/`) oder beide auf oberste Ebene.
- Passe in `Sub-Repository/msTools.Backup.Tests/msTools.Backup.Tests.csproj` den `ProjectReference`-Pfad an: `..\msTools.Backup\msTools.Backup.csproj` bleibt bei gleicher Ordnerstruktur gültig.
- Erstelle in `Sub-Repository/` eine Lösung `msTools.Backup.slnx` bzw. `msTools.Backup.sln`, die beide Projekte enthält.
- Erstelle/ergänze `.gitignore` für das Sub-Repository.

### 2. NuGet-Paketierung vorbereiten

- Erweitere `Sub-Repository/msTools.Backup/msTools.Backup.csproj` um NuGet-Metadaten:
  - `<PackageId>msTools.Backup</PackageId>`
  - `<Version>1.0.0</Version>`
  - `<Authors>... </Authors>`
  - `<Description>... </Description>`
  - `<PackageLicenseFile>LICENSE</PackageLicenseFile>`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
  - `<RepositoryUrl>https://github.com/martin-stromberg/msTools.Backup</RepositoryUrl>`
  - `<GeneratePackageOnBuild>false</GeneratePackageOnBuild>` (wird im Workflow explizit gepackt)
- Kopiere oder verlinke `LICENSE` und `README.md` im Sub-Repository.
- Füge im `msTools.Backup.csproj` ein `<None Include="README.md" Pack="true" PackagePath="\" />` hinzu.

### 3. GitHub-Workflows im Sub-Repository anlegen

- `.github/workflows/staging-ci.yml` auf Basis des Haupt-Repositorys, aber:
  - Triggert auf `staging`
  - Führt `dotnet test` für `msTools.Backup.Tests` aus
  - Packt mit `dotnet pack msTools.Backup/msTools.Backup.csproj -c Release -p:Version={RC_VERSION} -o artifacts`
  - Veröffentlicht `artifacts/*.nupkg` als Pre-release `v{RC_VERSION}`
- `.github/workflows/main-release.yml` auf Basis des Haupt-Repositorys, aber:
  - Triggert auf `main`
  - Liest Version aus letztem RC-Tag oder berechnet via `compute-version.sh`
  - Packt Release-Version und veröffentlicht `*.nupkg` als GitHub-Release `v{VERSION}`
- Kopiere `.github/scripts/compute-version.sh` aus dem Haupt-Repository ins Sub-Repository.
- Stelle sicher, dass Git Bash-Syntax (yq/zip/sed) funktioniert.

### 4. Testaufbau im Sub-Repository validieren

- Führe im Sub-Repository `dotnet restore`, `dotnet build` und `dotnet test` aus.
- Packe lokal: `dotnet pack msTools.Backup/msTools.Backup.csproj -c Release -o artifacts`.

### 5. Erstes NuGet-Paket ins Haupt-Repository einbinden

- Erstelle im Haupt-Repository ein Verzeichnis `lib/msTools.Backup/` und kopiere die erzeugte `.nupkg` dorthin.
- Erstelle/ergänze `NuGet.config` im Haupt-Repository mit einer lokalen Package-Source:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="lib" />
  </packageSources>
</configuration>
```

- Ändere in `VideoWebPlayer/VideoWebPlayer.csproj`:
  - Entferne `ProjectReference Include="..\msTools.Backup\msTools.Backup.csproj"`.
  - Füge `PackageReference Include="msTools.Backup" Version="1.0.0"` hinzu.
- Entferne `msTools.Backup` und `msTools.Backup.Tests` aus `VideoPlayer.sln`.
- Stelle sicher, dass `VideoWebPlayer` noch kompiliert (`dotnet build VideoWebPlayer/VideoWebPlayer.csproj`).

### 6. Lokales Sub-Repository bereinigen

- Entferne den lokalen `Sub-Repository/`-Ordner (das remote Repository bleibt bestehen).
- Lösche `msTools.Backup/` und `msTools.Backup.Tests/` aus dem Haupt-Repository, da sie jetzt im Sub-Repository liegen.
- Das `.nupkg`-Paket bleibt im Haupt-Repository unter `lib/msTools.Backup/` erhalten.

## Test- und Review-Plan

- **Build-Validierung Hauptprojekt:** `dotnet build VideoWebPlayer/VideoWebPlayer.csproj`
- **Build-Validierung Sub-Repository (lokal):** `dotnet build Sub-Repository/msTools.Backup.sln`
- **Test-Validierung Sub-Repository (lokal):** `dotnet test Sub-Repository/msTools.Backup.Tests/msTools.Backup.Tests.csproj`
- **Paket-Validierung:** `dotnet pack Sub-Repository/msTools.Backup/msTools.Backup.csproj -c Release -o artifacts` und Paket-Verfügbarkeit für Hauptprojekt.

## Akzeptanzkriterien

- [ ] Sub-Repository enthält `msTools.Backup` und `msTools.Backup.Tests`.
- [ ] Sub-Repository ist bau- und testfähig.
- [ ] `Sub-Repository/.github/workflows/staging-ci.yml` und `main-release.yml` sind vorhanden und packen `*.nupkg`.
- [ ] Eine `.nupkg` wurde in `lib/msTools.Backup/` abgelegt.
- [ ] `VideoWebPlayer` baut mit der PackageReference.
- [ ] `msTools.Backup/` und `msTools.Backup.Tests/` wurden aus dem Haupt-Repository und `Sub-Repository/` lokal entfernt.
