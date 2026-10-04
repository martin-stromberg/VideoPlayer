# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Alle **Code**-Planelemente sind vollständig umgesetzt. Offen sind ausschließlich die
Dokumentationsaufgaben (Tasks 23/24) und die manuelle mDNS-Verifikation (Task 26), die explizit
den späteren Lifecycle-Schritten (12/12b/12c) bzw. der manuellen Abnahme vorbehalten sind.

## Umgesetzte Planelemente

- [x] `MdnsOptions` (`VideoWebPlayer/Configuration/MdnsOptions.cs`) — angelegt: `Enabled` (Default `true`), `InstanceName` (Default `VideoWebPlayer`), `ServiceType` (Default `_videowebplayer._tcp.local.`), `Port` (`int?`, Default `null`)
- [x] `MdnsOptionsValidator` (`VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`) — angelegt: `IValidateOptions<MdnsOptions>` mit Port-Range 1–65535, Diensttyp-Muster `_{label}._tcp`/`_udp` (Label ≤ 15 Zeichen, optionaler `.local.`-Suffix), `InstanceName`-Regeln bei `Enabled`
- [x] `MdnsAdvertisement` (`VideoWebPlayer/Services/MdnsAdvertisement.cs`) — Record angelegt: `InstanceName`, `ServiceType`, `Port`, `HostName`, `TxtRecords`
- [x] `MdnsServiceProfileBuilder` (`VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`) — angelegt: Port-Ableitungskette `Mdns:Port` → `IServerAddressesFeature.Addresses` → `Kestrel:Endpoints:Http:Url` → `Host:Port` → 5000; feste TXT-Records `path=/`, `app=VideoWebPlayer`; `HostName` via `Dns.GetHostName()`
- [x] `MdnsAdvertiserWorker` (`VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`) — `BackgroundService` angelegt: `ApplicationStarted`-Warte, Konjunktion `MdnsOptions.Enabled && Setup.MdnsAdvertisementEnabled` über `internal static IsAdvertisementEnabled`, `ServiceDiscovery`/`ServiceProfile` mit `Advertise`/`Announce`, 60-s-Re-Evaluation (Konstante `ReEvaluationInterval`) mit dynamischem `Advertise`/`Unadvertise`, `StopAsync` deregistriert (Goodbye), Fehler → Warnlog ohne Abbruch
- [x] Feld `MdnsAdvertisementEnabled` (`bool`, Default `true`) in `Setup` (`VideoWebPlayer/Data/Setup.cs:53`) — vorhanden
- [x] Methoden `GetMdnsAdvertisementEnabledAsync` / `UpdateMdnsAdvertisementEnabledAsync` in `ProgramSettingsService` (`ProgramSettingsService.cs:162,173`) — vorhanden
- [x] `ProgramSettings.razor` — `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox` und `form-text` (Konjunktion mit `Mdns:Enabled`, ~60 s, Diensttyp, UDP 5353); `SettingsModel.MdnsAdvertisementEnabled`; Laden in `OnInitializedAsync`; Speichern in `SaveAsync`
- [x] `VideoWebPlayerBackupData` — `OptionalRestoreColumns` um `Setups.MdnsAdvertisementEnabled` (Zeile 74) und `OptionalRestoreBoolDefaults` um `(Setups, MdnsAdvertisementEnabled, true)` (Zeile 97) erweitert
- [x] `ServiceCollectionExtensions.AddVideoWebPlayerServices` — `AddOptions<MdnsOptions>().Bind(GetSection("Mdns")).ValidateOnStart()`, `IValidateOptions<MdnsOptions>`-Registrierung, `AddHostedService<MdnsAdvertiserWorker>()` nur bei `!env.IsEnvironment("Testing")` (Zeilen 280–301)
- [x] `appsettings.json` — `Mdns`-Abschnitt mit Defaults (Zeilen 42–47); `Mdns:Enabled`, `Mdns:InstanceName`, `Mdns:ServiceType`, `Mdns:Port` in `AutoUpdate:ProtectedFiles[1].JsonKeys` (Zeilen 170–173)
- [x] EF-Migration `AddSetupMdnsAdvertisementEnabled` (`Migrations/20261004173814_*`) — `Setups.MdnsAdvertisementEnabled`, `INTEGER`, `NOT NULL`, `defaultValue: true`; Snapshot aktualisiert
- [x] `PackageReference` `Makaretu.Dns.Multicast.New` 0.38.0 in `VideoWebPlayer.csproj:30` — vorhanden; Debug-Build fehlerfrei
- [x] `Program.cs` — unverändert (Worker über DI gehostet)
- [x] `UdpDiscoveryListener` — unverändert (Fallback-Kanal bleibt)
- [x] `MdnsServiceProfileBuilderTests` — alle 7 geplanten Tests vorhanden (Defaults, `Mdns:Port`-Vorrang, Bound-Address/Kestrel/Host:Port-Kette, konfigurierbarer ServiceType/InstanceName)
- [x] `MdnsOptionsValidatorTests` — ungültiger Port, ungültiger/gültiger Diensttyp (inkl. `.local.`-Suffix), leerer/zu langer Instanzname, Defaults gültig, leerer Instanzname bei `Enabled=false` erlaubt
- [x] `MdnsConfigurationTests.MdnsSection_BindsToOptions` — vorhanden
- [x] `MdnsRegistrationTests` — `DoesNotRegisterWorker_InTesting` + `RegistersWorker_OutsideTesting` vorhanden
- [x] `MdnsAdvertiserWorkerTests` — `IsAdvertisementEnabled_RequiresBothSwitches` (Theory, Konjunktionstabelle) plus Zusatztests (Config deaktiviert, Fail-closed bei DB-Lesefehler)
- [x] `ProgramSettingsServiceTests` — `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue`, `UpdateMdnsAdvertisementEnabledAsync_Persists` (echtes SQLite) vorhanden
- [x] `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled` — vorhanden (per `LegacyBackupArchiveBuilder.RemoveColumnsAsync`)
- [x] `ProgramSettingsE2ETests` — `Admin_TogglesMdnsAdvertisement_AndSettingPersists` und `NonAdmin_GetsNotAuthorized_OnProgramSettings` vorhanden

## Offene Aufgaben

- [ ] Task 23: `docs/INSTALL_AVAHI.md` aktualisieren (In-App-Advertisement als Standardweg, Avahi als Alternative, Doppel-Advertisement-Hinweis) — für späteren Lifecycle-Schritt (Dokumentation) vorgesehen
- [ ] Task 24: `docs/GUIDE_Installation.md`, `docs/help/einrichtung.md`, `docs/API.md`, `README.md`, `docs/INDEX.md`, `docs/RELEASE_NOTES.md` aktualisieren inkl. Hinweis auf MAUI-App-Umstellung — für späteren Lifecycle-Schritt (Dokumentation) vorgesehen
- [ ] Task 26: Manuelle mDNS-Verifikation per `avahi-browse`/`dns-sd` (Instanzname, Port, TXT-Records; Verschwinden nach Admin-Ausschalten ≤ ~60 s; Shutdown-Goodbye) — manuelle Abnahme, nicht automatisierbar

## Hinweise

- **Dokumentierte Planabweichung (in Iteration 2 akzeptiert):** `SaveAsync` in `ProgramSettings.razor` speichert den mDNS-Schalter atomar über den erweiterten `UpdateGeneralSettingsAsync(..., bool mdnsAdvertisementEnabled, ...)` statt über einen separaten `UpdateMdnsAdvertisementEnabledAsync`-Aufruf. Das eigenständige Methodenpaar aus dem Plan existiert dennoch im Service und wird vom `MdnsAdvertiserWorker` (Getter) genutzt; der Setter bleibt für spätere Verwendung verfügbar. In der Tasks-Datei (Task 14) vermerkt.
- Debug-Build `VideoWebPlayer.csproj --no-restore` in dieser Iteration geprüft: 0 Fehler, 0 Warnungen.
- Der Worker implementiert Fail-closed: bei unlesbarem Admin-Schalter (`_adminSwitchEnabled=false`) wird nicht advertised — konsistent mit der Konjunktionsregel.
