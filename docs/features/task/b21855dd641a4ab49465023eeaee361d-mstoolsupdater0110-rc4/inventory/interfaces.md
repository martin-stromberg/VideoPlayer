# Interfaces (eigener Code)

## `IUpdateSettingsService`

Datei: `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs` (Z. 10–45)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetDefaultSettings` | – | `UpdateSettings` | Default-Werte wie für eine neue Settings-Zeile. |
| `GetOrCreateAsync` | `CancellationToken` | `Task<UpdateSettings>` | Persistierte Singleton-Zeile (Id 1), legt sie aus Konfiguration an, normalisiert persistierte Werte. |
| `UpdateAsync` | `UpdateSettingsUpdate`, `CancellationToken` | `Task<UpdateSettings>` | Validiert, persistiert und wendet Werte auf die Runtime-`AutoUpdateOptions` an. |
| `ApplyToRuntimeOptionsAsync` | `CancellationToken` | `Task` | Persistierte Einstellungen in `AutoUpdateOptions` schreiben. |
| `GetBackupOptionsAsync` | `CancellationToken` | `Task<UpdateBackupOptions>` | Aktuelle Update-Backup-Optionen aus den persistierten Settings. |

Implementiert von `UpdateSettingsService`; konsumiert von `UpdateAdminService`,
`UpdateBackupCoordinator`, `UpdateSettingsInitializer` und in Tests durch
`FakeUpdateBackend` (`UpdatesPageE2ETests`).

## `IUpdateBackupService`

Datei: `VideoWebPlayer/Services/Updates/IUpdateBackupService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateBackupAsync` | `UpdateBackupRequest`, `CancellationToken` | `Task<UpdateBackupResult>` | Vollständigen Datenexport als Update-Sicherung erstellen. |

Optionaler Dienst: `UpdateBackupCoordinator` löst ihn per `GetService` auf — ohne Registrierung
läuft die Anwendung weiter (Installation abhängig von `CancelInstallationOnFailure`).
Implementiert von `VideoWebPlayerUpdateBackupService` (Adapter auf `msTools.Backup`).

## `IUpdateHostEnvironment`

Datei: `VideoWebPlayer/Services/Updates/UpdateHostEnvironment.cs`

| Mitglied | Typ | Zweck |
|----------|-----|-------|
| `RunsUnderIis` | `bool` (Property) | Anwendung läuft unter dem IIS AspNetCoreModule (In- oder Out-of-Process); `ServiceName` ist dann wirkungslos. |

Implementiert von `UpdateHostEnvironment` (Singleton, ANCM-Umgebungsvariablen); konsumiert von
`UpdateSettingsService` und `Updates.razor`; in Tests durch Moq/`FakeUpdateBackend` ersetzt
(`UpdatesPageE2ETests` registriert die echte Implementierung nicht — dort greift die
Standard-DI-Registrierung; Unit-Tests mocken das Flag über `Mock.Of<IUpdateHostEnvironment>`).

## Bibliotheks-Interfaces (aus `msTools.Updater`, nur konsumiert)

| Interface | Verwendung im eigenen Code |
|-----------|---------------------------|
| `IAutoUpdateOrchestrator` | `UpdateAdminService` (Statusabfrage `GetStatusAsync`); in `UpdatesPageE2ETests.FakeUpdateBackend` implementiert. |
| `IAutoUpdateCommandHandler` | `UpdateAdminService` (`CheckAsync`, `DownloadAsync`, `InstallAsync`); in `FakeUpdateBackend` implementiert. |
| `IAutoUpdateEventAggregator` | `UpdateBackupEventBinder` (`BeforeInstall`, `ErrorOccurred`); Tests nutzen die konkrete `AutoUpdateEvents` (mit `RaiseBeforeInstall`). |
| `IAutoUpdateScriptGenerator`, `IAutoUpdatePackageStore`, `IAutoUpdatePlatformResolver` | Nur in `AutoUpdateProtectedFilesTests` (Skript-Generierung im Test). |
