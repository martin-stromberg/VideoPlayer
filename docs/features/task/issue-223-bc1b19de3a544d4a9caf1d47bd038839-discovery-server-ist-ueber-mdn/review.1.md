# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen

- [x] `MdnsOptions` (Optionsklasse, `VideoWebPlayer/Configuration/MdnsOptions.cs`) — angelegt; `Enabled` (Default `true`), `InstanceName` (Default `VideoWebPlayer`), `ServiceType` (Default `_videowebplayer._tcp.local.`), `Port` (`int?`, Default `null`)
- [x] `MdnsOptionsValidator` (`IValidateOptions<MdnsOptions>`, `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`) — angelegt; Port 1–65535, Diensttyp-Muster `_{label}._tcp`/`_udp` (Label ≤ 15 Zeichen, Kleinbuchstaben/Ziffern/Bindestrich, optionaler `.local`/`.local.`-Suffix), `InstanceName` nicht leer und ≤ 63 Zeichen bei `Enabled`
- [x] `MdnsAdvertisement` (Record, `VideoWebPlayer/Services/MdnsAdvertisement.cs`) — angelegt; `InstanceName`, `ServiceType`, `Port`, `HostName`, `TxtRecords`
- [x] `MdnsServiceProfileBuilder` (Logikklasse, `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`) — angelegt; Port-Ableitungskette `Mdns:Port` → `IServerAddressesFeature.Addresses` → `Kestrel:Endpoints:Http:Url` → `Host:Port` → 5000, feste TXT-Records `path=/` und `app=VideoWebPlayer`, `HostName` via `Dns.GetHostName()`
- [x] `MdnsAdvertiserWorker` (`BackgroundService`, `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`) — angelegt; wartet `ApplicationStarted` ab, Konjunktion `MdnsOptions.Enabled && Setup.MdnsAdvertisementEnabled` als `internal`-Methode `IsAdvertisementEnabled`, DB-Zugriff via `IServiceScopeFactory` + `ProgramSettingsService`, `ServiceDiscovery`/`ServiceProfile` mit `Advertise`/`Announce`, 60-s-Re-Evaluation mit dynamischem Advertise/Unadvertise, `StopAsync` deregistriert (Goodbye-Pakete) und disposed, Fehler → Warnlog ohne App-Abbruch

### Geänderte bestehende Klassen/Dateien

- [x] Feld `Setup.MdnsAdvertisementEnabled` (`bool`, Default `true`) in `VideoWebPlayer/Data/Setup.cs:53` — vorhanden
- [x] Methoden `GetMdnsAdvertisementEnabledAsync` / `UpdateMdnsAdvertisementEnabledAsync` in `VideoWebPlayer/Services/ProgramSettingsService.cs:159,170` — vorhanden (über `GetOrCreateSetupAsync`)
- [x] `ProgramSettings.razor` — `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox` und `form-text` (Konjunktion mit `Mdns:Enabled`, ~60 s, `_videowebplayer._tcp.local.`, UDP 5353) in Z. 85–92; `SettingsModel.MdnsAdvertisementEnabled` (Z. 165); Laden in `OnInitializedAsync` (Z. 123); Speichern in `SaveAsync` via `UpdateMdnsAdvertisementEnabledAsync` (Z. 141); `IsAdmin`-Guard unverändert (Z. 10–13, 111)
- [x] `VideoWebPlayerBackupData` — `OptionalRestoreColumns` um `Setups.MdnsAdvertisementEnabled` (Z. 74) und `OptionalRestoreBoolDefaults` um `(Setups, MdnsAdvertisementEnabled, true)` (Z. 97) erweitert
- [x] `ServiceCollectionExtensions.AddVideoWebPlayerServices` — `AddOptions<MdnsOptions>().Bind(GetSection("Mdns")).ValidateOnStart()` (Z. 281–283), `IValidateOptions<MdnsOptions>`-Registrierung (Z. 280), `AddHostedService<MdnsAdvertiserWorker>()` nur wenn `!env.IsEnvironment("Testing")` (Z. 297–301)
- [x] `appsettings.json` — `Mdns`-Abschnitt mit Defaults (Z. 42–47); `Mdns:Enabled`, `Mdns:InstanceName`, `Mdns:ServiceType`, `Mdns:Port` in `AutoUpdate:ProtectedFiles[1].JsonKeys` (Z. 170–173, Eintrag `appsettings*.json`)
- [x] `VideoWebPlayer.csproj` — `PackageReference Makaretu.Dns.Multicast.New` Version 0.38.0 (Z. 30; publiziert 2024-11-23, älter als 7 Tage, MIT)
- [x] `Program.cs` — bewusst unverändert (Worker über DI gehostet)
- [x] `UdpDiscoveryListener` — unverändert (Fallback-Kanal Port 5001 besteht)

### Datenbankmigration

- [x] Migration `20261004173814_AddSetupMdnsAdvertisementEnabled` — `Setups.MdnsAdvertisementEnabled` (`INTEGER`, `NOT NULL`, `defaultValue: true`); `ApplicationDbContextModelSnapshot.cs:273` enthält die Spalte

### Neue Tests

- [x] `MdnsServiceProfileBuilderTests` — alle 7 geplanten Tests vorhanden (Defaults, `Mdns:Port`-Vorrang, Bound-Address-/Kestrel-/`Host:Port`-Kette, konfigurierter `ServiceType`/`InstanceName`)
- [x] `MdnsOptionsValidatorTests` — `Validate_RejectsInvalidPort`, `Validate_RejectsInvalidServiceType`, `Validate_AcceptsValidServiceType` (inkl. `.local.`-Suffix), `Validate_RejectsEmptyInstanceName`, `Validate_RejectsInstanceNameLongerThan63Characters`, `Validate_AcceptsDefaults`, `Validate_AcceptsEmptyInstanceName_WhenDisabled` vorhanden
- [x] `MdnsConfigurationTests.MdnsSection_BindsToOptions` — vorhanden (`AddInMemoryCollection`-Bindung)
- [x] `MdnsRegistrationTests` — `..._DoesNotRegisterWorker_InTesting` und `..._RegistersWorker_OutsideTesting` vorhanden
- [x] `MdnsAdvertiserWorkerTests.IsAdvertisementEnabled_RequiresBothSwitches` — 4 Theory-Fälle (Konjunktionstabelle) vorhanden
- [x] `ProgramSettingsServiceTests` — `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue` und `UpdateMdnsAdvertisementEnabledAsync_Persists` vorhanden; laufen gegen echtes SQLite (`PairingTestDb` → `UseSqlite`, `Helpers/PairingTestDb.cs:110`)
- [x] `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled` — vorhanden (Z. 593–614, via `LegacyBackupArchiveBuilder.RemoveColumnsAsync`)
- [x] `ProgramSettingsE2ETests` — `Admin_TogglesMdnsAdvertisement_AndSettingPersists` (Toggle + Reload-Persistenz, beide Richtungen) und `NonAdmin_GetsNotAuthorized_OnProgramSettings` („Nicht autorisiert", kein Schalter) vorhanden; Muster `WebApplicationFactory<Program>` + `Testing` + Playwright eingehalten

## Offene Aufgaben

- [ ] `docs/INSTALL_AVAHI.md` aktualisieren — fehlt vollständig: kein Hinweis auf das In-App-Advertisement als Standardweg, Avahi weiterhin als einziger dokumentierter Weg beschrieben; Warnung vor Doppel-Advertisement (statische Dienstdatei entfernen **oder** `Mdns:Enabled=false`) fehlt
- [ ] `docs/GUIDE_Installation.md` aktualisieren — fehlt vollständig: kein `Mdns:*`-Konfigurationsabschnitt, kein IIS-`OutOfProcess`-Hinweis auf `Mdns:Port`, kein Firewall-Hinweis UDP 5353 (einziger Discovery-Bezug ist die alte Fallback-Adresse in Z. 53)
- [ ] `docs/help/einrichtung.md` aktualisieren — fehlt vollständig: kein Discovery-Abschnitt (mDNS `_videowebplayer._tcp.local.`, Admin-Schalter, UDP-Fallback Port 5001)
- [ ] `docs/API.md` aktualisieren — fehlt vollständig: kein Discovery-Abschnitt (bei Erweiterung auf Contract-Test-Kompatibilität achten, `ApiDocumentationContractTests`)
- [ ] `README.md`, `docs/INDEX.md`, `docs/RELEASE_NOTES.md` aktualisieren — fehlen vollständig; ebenso der Hinweis auf die clientseitige MAUI-App-Umstellung auf `_videowebplayer._tcp.local.` (erfolgt im App-Repo)
- [ ] Manuelle mDNS-Verifikation dokumentieren — offen: `avahi-browse -a` / `dns-sd -B _videowebplayer._tcp local.` (Instanzname, Port, TXT-Records), Admin-Schalter aus → Dienst verschwindet nach ≤ ~60 s, Shutdown-Goodbye stichprobenartig; im Bericht zu dokumentieren (nicht automatisierbar)

## Hinweise

- Die Dokumentationslücken betreffen ausschließlich Dateien unter `docs/` bzw. `README.md`; der gesamte Code- und Test-Teil des Plans ist im Working Tree verifiziert. `dotnet build` von `VideoWebPlayer` und `VideoWebPlayer.Tests` (Debug) läuft fehlerfrei (0 Warnungen, 0 Fehler).
- Abweichung zur Planformulierung: Der Worker erzeugt nur `ServiceDiscovery` (das intern `MulticastService` instanziiert und beim Dispose mitstoppt) statt explizit beider Objekte — funktional gleichwertig zur geplanten Beteiligten-Liste, die Goodbye-/Stop-Semantik ist gegeben.
- Der Registrierungs-Guard nutzt `env.IsEnvironment("Testing")` mit `env = builder.Environment` (`ServiceCollectionExtensions.cs:45`) — deckt die geplante Bedingung `!builder.Environment.IsEnvironment("Testing")` ab.
- Verifikation per Tasks-Datei: Release-Build und volle Testsuite sind vom Implementierer als grün gemeldet (1451 nicht-E2E, 206 E2E + 1 Bestandsskip); der abschließende Suite-Lauf obliegt dem separaten Tests-Schritt.
