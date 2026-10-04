# Code-Review

## Ergebnis

**Status:** Keine Befunde

Iteration 3: Der einzige offene Befund aus `review-code.2.md` (`_consoleMessages` ohne
Konsumenten in `ProgramSettingsE2ETests`) ist korrekt behoben und hat keine neuen Probleme
eingeführt. `dotnet build VideoWebPlayer.Tests` (Debug) fehlerfrei, 0 Warnungen.

## Verifikation des Iteration-2-Befunds (behoben)

- **Toter Code `_consoleMessages`:** `ProgramSettingsE2ETests.cs` Z. 163–167 führt jetzt
  `SevereErrors()` ein — zeilenidentisch mit `UpdatesPageE2ETests.cs` Z. 463–467
  (Filter: `[page-error]` oder `[response]` mit 4xx/5xx). Der Konsument wird in beiden
  Tests über `Assert.Empty(SevereErrors())` am Ende ausgewertet (Z. 139 und Z. 153).
  Die Event-Handler (Z. 78–84) laufen damit nicht mehr ins Leere.

## Hinweise (keine Befunde)

- **`SevereErrors()`-Filter:** Die `Contains(" 4")`/`Contains(" 5")`-Prüfung auf
  `[response]`-Einträge ist streng genommen redundant, weil der Handler nur Status >= 400
  aufzeichnet — bewusst identisch zu `UpdatesPageE2ETests` gehalten, kein Befund.
- **`UpdateMdnsAdvertisementEnabledAsync`** (`ProgramSettingsService.cs` Z. 173) hat
  weiterhin keinen Produktiv-Aufrufer — bereits in `review-code.2.md` als bewusste
  Plan-Vorgabe (Methodenpaar, Schritt 7) dokumentiert, per SQLite-Test abgesichert.
- **Standardkriterien über den Gesamtdiff erneut verifiziert:**
  - Worker-Threading korrekt: Early-Return bei `Mdns:Enabled=false` (Z. 82–87),
    fail-closed `_adminSwitchEnabled` (Z. 32), `StopAsync` wartet über `base.StopAsync`
    auf `ExecuteAsync`, Goodbye-Pakete nur bei `_advertised == true`.
  - Konjunktionsregel `IsAdvertisementEnabled` (Z. 65–66) mit Theory-Test aller vier
    Kombinationen abgesichert.
  - Migration/Snapshot konsistent (`ProductVersion 10.0.12` deckt sich mit den
    EF-Paketreferenzen), `AddColumn` mit `defaultValue: true`.
  - Backup: `OptionalRestoreColumns` + `OptionalRestoreBoolDefaults` (Default `true`)
    ergänzt; Regressionstest `ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled`
    läuft gegen echtes SQLite.
  - Admin-Guard serverseitig (`IsAdmin`-Claim in `OnInitializedAsync`, kein
    Formular/Handler für Nicht-Admins; E2E-Test prüft `#mdnsAdvertisementEnabled` Count 0).
  - NuGet `Makaretu.Dns.Multicast.New` 0.38.0 (MIT), keine neue Hauptversion.
  - Optionsvalidierung per `IValidateOptions<MdnsOptions>` + `ValidateOnStart`;
    RFC-6335-Label-Regex lehnt führende/folgende Bindestriche ab (Tests Z. 38–40).
  - `Mdns:*`-Schlüssel in der `JsonKeys`-Merge-Liste des Updaters; Worker-Registrierung
    hinter `!env.IsEnvironment("Testing")` (`env` definiert Z. 45), Registrierungstests
    vorhanden; `InternalsVisibleTo` gesetzt.
- `ProgramSettingsE2ETests` wurde nicht ausgeführt (Playwright-Browser nötig); kompiliert
  fehlerfrei und folgt dem `UpdatesPageE2ETests`-Muster.

## Geprüfte Dateien

- `VideoWebPlayer/Configuration/MdnsOptions.cs`
- `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`
- `VideoWebPlayer/Services/MdnsAdvertisement.cs`
- `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`
- `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Data/Setup.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.cs`
- `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.Designer.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer/VideoWebPlayer.csproj`
- `VideoWebPlayer.Tests/MdnsAdvertiserWorkerTests.cs`
- `VideoWebPlayer.Tests/MdnsConfigurationTests.cs`
- `VideoWebPlayer.Tests/MdnsOptionsValidatorTests.cs`
- `VideoWebPlayer.Tests/MdnsRegistrationTests.cs`
- `VideoWebPlayer.Tests/MdnsServiceProfileBuilderTests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsServiceTests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsE2ETests.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
