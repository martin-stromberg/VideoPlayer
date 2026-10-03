# Bestandsaufnahme: Update überschreibt `web.config` — JWT-/Konfigurationseinträge gehen verloren

Bestandsaufnahme des Update-/Konfigurationspfads des VideoWebPlayer bezogen auf `requirement.md` (Issue #226): Wie wird `release-win-x64.zip` gebaut, was macht das von `msTools.Updater` generierte Installationsskript, wie fließen `Jwt:*`-Werte in die Anwendung, und welche Dokumentation/Tests existieren dazu bereits.

## Zusammenfassung

- **Fehlerbild bestätigt:** Das von `msTools.Updater` (Version `0.10.4-rc.1`, decompiliert aus `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg`) erzeugte Windows-Installationsskript entpackt das Release-Zip in ein Staging-Verzeichnis und kopiert anschließend **alle** Dateien per `Copy-Item -Recurse -Force` über das Anwendungsverzeichnis — ohne Ausschlussliste, ohne Sicherung vorhandener Dateien, ohne XML-Merge. Eine deployment-seitig erweiterte `web.config` (z. B. `<environmentVariables>` mit `Jwt:Key`, `Jwt:ApiToken:*`, `AutoUpdate__AppPoolName`) wird vollständig ersetzt.
- **Das Release-Paket enthält `web.config`:** `.github/actions/build-and-package/action.yml` packt das komplette `dotnet publish`-Output-Verzeichnis inklusive der durch die Publish-Transformation erzeugten `web.config` und aller `appsettings*.json` in `release-win-x64.zip` / `release-linux-x64.zip`. Es gibt keinen Ausschlussmechanismus.
- **`IAutoUpdateScriptGenerator` ist überschreibbar:** `UseAutoUpdate` registriert alle Updater-Dienste inklusive `IAutoUpdateScriptGenerator` über `TryAddSingleton`. Eine eigene Implementierung, die die Anwendung **vor** dem `builder.UseAutoUpdate(...)`-Aufruf in `AddVideoWebPlayerAutoUpdate` (`VideoWebPlayer/Extensions/AutoUpdateExtensions.cs:29-35`) registriert, würde von DI aufgelöst werden. `AutoUpdateScriptGenerator` ist `sealed` (nicht ableitbar); eine EigImplementierung müsste die Skripterzeugung nachbilden oder komponieren. `AutoUpdateInstaller` löst den Generator regulär per Konstruktor-Injektion auf.
- **Kein Dateierhalt-Konzept in der Bibliothek:** `AutoUpdateOptions` enthält keine Option für updategeschützte Dateien (kein `PreserveFiles`/`ExcludeFiles`-Äquivalent). Für IIS-Ziele existieren `AutoUpdate:AppPoolName`/`SiteName` sowie die Fluent-Methode `WithIisApplicationPool` — beides wird im VideoWebPlayer-Code nicht verwendet. Der Windows-Erkennungs-Probe findet nur Windows-Dienste (via `sc.exe queryex`, PID-Abgleich), **keine** IIS-App-Pools.
- **Konfigurationspipeline ist Standard:** `Program.cs` verwendet `WebApplication.CreateBuilder(args)` ohne eigene `AddJsonFile`-Aufrufe; die `Jwt:*`-Werte werden in `AddVideoWebPlayerServices` (`ServiceCollectionExtensions.cs:48-59`) gelesen und in Produktion als Pflicht erzwungen (`InvalidOperationException`). Es gibt keine optionale lokale Konfigurationsdatei; `appsettings.json`/`appsettings.Production.json` werden mit ausgeliefert und beim Update überschrieben.
- **Dokumentation empfiehlt faktisch bereits Umgebungsvariablen** (`Jwt__*`), erwähnt den IIS-`environmentVariables`-Weg aber nirgends explizit; `TECH_Auto_Update.md` beschreibt zudem noch den veralteten DLL-Datei-Referenz-Mechanismus (`lib/msTools.Updater/`), obwohl `msTools.Updater` längst als nupkg unter `lib/packages/` eingebunden ist.

Test-Ausgangszustand: Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` in Release — **1607 Tests: 1606 bestanden, 1 übersprungen, 0 fehlgeschlagen**. Der übersprungene Test ist ein bewusst deaktivierter E2E-Befund zu einem anderen, nicht auftragsbezogenen Fehler (`api/continue-watching` → 500 bei Film ohne Filmsammlung). Details und Nachweise: [Tests](inventory/tests.md).

## Details

- [Deployment-Pipeline und Update-Mechanik (Build-Action, `web.config`, `msTools.Updater`-Interna)](inventory/deployment.md)
- [Logik (Konfigurationspipeline, DI-Registrierung, Update-Services, Token-Gates)](inventory/logic.md)
- [Datenmodell und Optionsklassen](inventory/models.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Dokumentationsstand](inventory/dokumentation.md)
- [Tests](inventory/tests.md)
