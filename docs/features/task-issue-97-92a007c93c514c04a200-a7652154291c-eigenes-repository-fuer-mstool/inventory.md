# Bestandsaufnahme

## Ausgangsprojekt

- Der aktive Branch ist `task/issue-97-92a007c93c514c04a200a7652154291c-eigenes-repository-fuer-mstool`.
- Haupt-Repository: `D:\Repositories\softwareschmiede\92a007c9-3c51-4c04-a200-a7652154291c`
- Ziel-Ordner: `Sub-Repository/`

## Vorhandene Projekte im Haupt-Repository

### msTools.Backup

- Pfad: `msTools.Backup/`
- Projektdatei: `msTools.Backup.csproj`
- SDK: `Microsoft.NET.Sdk`
- TargetFramework: `net10.0`
- Einstellungen: `Nullable=enable`, `ImplicitUsings=enable`, `GenerateDocumentationFile=true`
- FrameworkReference: `Microsoft.AspNetCore.App`
- Quellcode: 12 C#-Dateien (BackupModels, BackupOptions, BackupService, BackupRegistrationExtensions, etc.)

### msTools.Backup.Tests

- Pfad: `msTools.Backup.Tests/`
- Projektdatei: `msTools.Backup.Tests.csproj`
- SDK: `Microsoft.NET.Sdk`
- TargetFramework: `net10.0`
- Test-Framework: xUnit v3 (`xunit.v3` 3.2.2)
- Test-SDK: `Microsoft.NET.Test.Sdk` 18.8.1
- Referenz: `..\msTools.Backup\msTools.Backup.csproj`
- Testdateien:
  - `BackupRetentionServiceTests.cs`
  - `FileSystemBackupStoreTests.cs`
  - `ScheduledBackupServiceTests.cs`

## Solution-Integration

- `VideoPlayer.sln` enthält die Projekte `msTools.Backup` und `msTools.Backup.Tests`.
- GUIDs:
  - `msTools.Backup`: `{083A1DC0-95E0-44D8-AD5B-404A64F55A14}`
  - `msTools.Backup.Tests`: `{7A2D34D9-A65D-4937-8583-DB4FD8042C22}`

## Referenzen auf msTools.Backup

- `VideoWebPlayer/VideoWebPlayer.csproj` und `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj` referenzieren `msTools.Backup` per ProjectReference (siehe `inventory/references.md`).

## Vorhandene Workflows im Haupt-Repository

- `.github/workflows/main-release.yml` — Release aus `main` (zip-basierte Anwendungsartefakte)
- `.github/workflows/staging-ci.yml` — Staging-CI mit RC-Prerelease
- `.github/workflows/staging-to-main-promotion.yml`
- `.github/workflows/pr-main-ci.yml`
- `.github/workflows/pr-staging-ci.yml`
- `.github/workflows/verify-pr-source.yml`
- `.github/scripts/compute-version.sh` — Semantische Versionsermittlung

## Ziel-Repository-Ordner

- `Sub-Repository/` ist aktuell leer bis auf:
  - `.vs/` (Visual Studio Cache)
  - `.gitignore`
  - `README.md`
  - `.git/`-Verzeichnis (eigenständiges Git-Repository auf Branch `main`)
- Der Ordner ist im Haupt-Repository ungetrackt.

## Detaildokumente

- `inventory/references.md` — genaue ProjectReference-Verweise im Haupt-Repository
- `inventory/workflows.md` — Auszüge der relevanten Haupt-Workflows
