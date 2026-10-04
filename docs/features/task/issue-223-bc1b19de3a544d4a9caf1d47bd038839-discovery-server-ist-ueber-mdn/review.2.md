# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Alle Code-Planelemente sind **vollständig umgesetzt** (Iteration 2, inkl. der 5 behobenen
Code-Review-Befunde aus Runde 1). Verbleibend offen sind ausschließlich die
Dokumentationsaufgaben (Lifecycle-Schritte 12/12b/12c) und die manuelle mDNS-Verifikation
(manuelle Abnahme) — keine Code-Lücken.

## Umgesetzte Planelemente

### Neue Klassen

- [x] `MdnsOptions` (Optionsklasse, `VideoWebPlayer/Configuration/MdnsOptions.cs`) — angelegt;
  `Enabled` (Default `true`, Z. 13), `InstanceName` (Default `VideoWebPlayer`, Z. 18),
  `ServiceType` (Default `_videowebplayer._tcp.local.`, Z. 23), `Port` (`int?`, Z. 29)
- [x] `MdnsOptionsValidator` (`IValidateOptions<MdnsOptions>`,
  `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`) — angelegt; Port 1–65535 (Z. 21–22),
  Diensttyp-Muster `_{label}._tcp`/`_udp` mit optionalem `.local`/`.local.`-Suffix, Label ≤ 15
  Zeichen ohne führenden/folgenden Bindestrich (Z. 14–16; Befund aus Runde 1 behoben),
  `InstanceName` bei `Enabled` nicht leer und ≤ 63 Zeichen (Z. 28–34)
- [x] `MdnsAdvertisement` (Record, `VideoWebPlayer/Services/MdnsAdvertisement.cs`) — angelegt;
  `InstanceName`, `ServiceType`, `Port`, `HostName`, `TxtRecords`
- [x] `MdnsServiceProfileBuilder` (reine Logikklasse,
  `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`) — angelegt; Port-Ableitungskette
  `Mdns:Port` → `IServerAddressesFeature.Addresses` → `Kestrel:Endpoints:Http:Url` → `Host:Port`
  → 5000 (Z. 46–69), feste TXT-Records `path=/` und `app=VideoWebPlayer` (Z. 38–42), `HostName`
  via `Dns.GetHostName()` (Z. 37)
- [x] `MdnsAdvertiserWorker` (`BackgroundService`,
  `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`) — angelegt; wartet
  `ApplicationStarted` ab (Z. 143–151), Konjunktion als `internal`-Methode
  `IsAdvertisementEnabled` (Z. 65–66, `InternalsVisibleTo` in csproj Z. 15), DB-Zugriff via
  `IServiceScopeFactory` + `ProgramSettingsService.GetMdnsAdvertisementEnabledAsync` (Z. 153–171),
  `ServiceDiscovery`/`ServiceProfile` mit `Advertise`/`Announce` (Z. 173–203), 60-s-Re-Evaluation
  mit dynamischem Advertise/Unadvertise (Z. 18, 101–123), `StopAsync` deregistriert via
  `Unadvertise` + `Dispose` (Z. 127–141), Fehler → Warnlog ohne App-Abbruch

### Geänderte bestehende Klassen/Dateien

- [x] Feld `Setup.MdnsAdvertisementEnabled` (`bool`, Default `true`) in
  `VideoWebPlayer/Data/Setup.cs:53` — vorhanden
- [x] Methoden `GetMdnsAdvertisementEnabledAsync` (Z. 162) und
  `UpdateMdnsAdvertisementEnabledAsync` (Z. 173) in
  `VideoWebPlayer/Services/ProgramSettingsService.cs` — vorhanden (über `GetOrCreateSetupAsync`)
- [x] `ProgramSettings.razor` — `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox`
  „Server per mDNS im Netzwerk ankündigen" und `form-text` (Konjunktion mit `Mdns:Enabled`,
  ~60 s, Diensttyp `_videowebplayer._tcp.local.`, UDP 5353) in Z. 85–92;
  `SettingsModel.MdnsAdvertisementEnabled` (Z. 165, Default `true`); Laden in
  `OnInitializedAsync` (Z. 123); Speichern in `SaveAsync` (Z. 136–141); `IsAdmin`-Guard
  unverändert (Z. 10–13, 111–114)
- [x] `VideoWebPlayerBackupData` — `OptionalRestoreColumns` um `Setups.MdnsAdvertisementEnabled`
  (Z. 74) und `OptionalRestoreBoolDefaults` um `(Setups, MdnsAdvertisementEnabled, true)`
  (Z. 97) erweitert
- [x] `ServiceCollectionExtensions.AddVideoWebPlayerServices` —
  `AddSingleton<IValidateOptions<MdnsOptions>, MdnsOptionsValidator>()` (Z. 280),
  `AddOptions<MdnsOptions>().Bind(GetSection("Mdns")).ValidateOnStart()` (Z. 281–283),
  `AddHostedService<MdnsAdvertiserWorker>()` nur wenn `!env.IsEnvironment("Testing")`
  (Z. 297–301, `env = builder.Environment` Z. 45)
- [x] `appsettings.json` — `Mdns`-Abschnitt mit Defaults (Z. 42–47); `Mdns:Enabled`,
  `Mdns:InstanceName`, `Mdns:ServiceType`, `Mdns:Port` in
  `AutoUpdate:ProtectedFiles[1].JsonKeys` (Z. 170–173)
- [x] `VideoWebPlayer.csproj` — `PackageReference Makaretu.Dns.Multicast.New` Version 0.38.0
  (Z. 30; publiziert 2024-11-23, älter als 7 Tage, MIT)
- [x] `Program.cs` — bewusst unverändert (Worker über DI gehostet; `Testing`-Guard in der
  Registrierung)
- [x] `UdpDiscoveryListener` — unverändert (Fallback-Kanal Port 5001 besteht; nicht in
  `git status` enthalten)

### Datenbankmigration

- [x] Migration `20261004173814_AddSetupMdnsAdvertisementEnabled` —
  `Setups.MdnsAdvertisementEnabled` (`INTEGER`, `NOT NULL`, `defaultValue: true`);
  Designer-Datei und `ApplicationDbContextModelSnapshot.cs:273` konsistent

### Neue Tests

- [x] `MdnsServiceProfileBuilderTests` — alle 7 geplanten Tests vorhanden:
  `Build_UsesDefaults_WhenNothingConfigured`, `Build_PrefersMdnsPort_OverAllSources`,
  `Build_UsesBoundServerAddressPort`, `Build_UsesKestrelEndpointUrlPort` (inkl.
  `http://*:5002`-Produktionssimulation), `Build_UsesHostPortFallback`,
  `Build_UsesConfiguredServiceType`, `Build_UsesConfiguredInstanceName`
- [x] `MdnsOptionsValidatorTests` — `Validate_RejectsInvalidPort`,
  `Validate_RejectsInvalidServiceType` (inkl. Bindestrich-Fälle `_-x._tcp`/`_x-._tcp`),
  `Validate_AcceptsValidServiceType` (inkl. `.local.`-Suffix), `Validate_RejectsEmptyInstanceName`,
  `Validate_RejectsInstanceNameLongerThan63Characters`, `Validate_AcceptsDefaults`,
  `Validate_AcceptsEmptyInstanceName_WhenDisabled` — 18 Fälle vorhanden
- [x] `MdnsConfigurationTests.MdnsSection_BindsToOptions` — vorhanden
  (`AddInMemoryCollection`-Bindung)
- [x] `MdnsRegistrationTests` — `AddVideoWebPlayerServices_DoesNotRegisterWorker_InTesting` und
  `..._RegistersWorker_OutsideTesting` vorhanden
- [x] `MdnsAdvertiserWorkerTests` — `IsAdvertisementEnabled_RequiresBothSwitches` (4
  Theory-Fälle, Konjunktionstabelle); zusätzlich
  `ExecuteAsync_ConfigDisabled_ExitsWithoutPollingAdminSwitch` und
  `ExecuteAsync_AdminSwitchInitialReadFails_DoesNotAdvertise` (Regressionstests zu den
  Runde-1-Befunden)
- [x] `ProgramSettingsServiceTests` — `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue`,
  `UpdateMdnsAdvertisementEnabledAsync_Persists` (beide Richtungen) sowie
  `UpdateGeneralSettingsAsync_PersistsMdnsAdvertisementEnabled`; gegen echtes SQLite
  (`PairingTestDb`-Muster)
- [x] `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled`
  — vorhanden (Z. 593–614, via `LegacyBackupArchiveBuilder.RemoveColumnsAsync`)
- [x] `ProgramSettingsE2ETests` —
  `Admin_TogglesMdnsAdvertisement_AndSettingPersists` (Toggle + Reload-Persistenz, beide
  Richtungen) und `NonAdmin_GetsNotAuthorized_OnProgramSettings` („Nicht autorisiert", kein
  Schalter) vorhanden; Muster `WebApplicationFactory<Program>` + `Testing` + `UseKestrel` +
  Playwright eingehalten

## Offene Aufgaben

- [ ] `docs/INSTALL_AVAHI.md` aktualisieren — fehlt vollständig: In-App-Advertisement als
  Standardweg, Avahi als Alternativweg, Warnung vor Doppel-Advertisement (statische
  Dienstdatei entfernen **oder** `Mdns:Enabled=false`) — Lifecycle-Schritt 12/12b/12c
- [ ] `docs/GUIDE_Installation.md` aktualisieren — fehlt vollständig: `Mdns:*`-Abschnitt,
  IIS-`OutOfProcess`-Hinweis auf `Mdns:Port`, Firewall UDP 5353 — Lifecycle-Schritt 12/12b/12c
- [ ] `docs/help/einrichtung.md` aktualisieren — fehlt vollständig: Discovery-Abschnitt
  (mDNS `_videowebplayer._tcp.local.`, Admin-Schalter, UDP-Fallback Port 5001) —
  Lifecycle-Schritt 12/12b/12c
- [ ] `docs/API.md` aktualisieren — fehlt vollständig: Discovery-Abschnitt (bei Erweiterung auf
  `ApiDocumentationContractTests`-Kompatibilität achten) — Lifecycle-Schritt 12/12b/12c
- [ ] `README.md`, `docs/INDEX.md`, `docs/RELEASE_NOTES.md` aktualisieren — fehlen vollständig;
  ebenso Hinweis auf clientseitige MAUI-App-Umstellung auf `_videowebplayer._tcp.local.` —
  Lifecycle-Schritt 12/12b/12c
- [ ] Manuelle mDNS-Verifikation dokumentieren — offen: `avahi-browse -a` /
  `dns-sd -B _videowebplayer._tcp local.` (Instanzname, Port, TXT-Records), Admin-Schalter aus →
  Dienst verschwindet nach ≤ ~60 s, Shutdown-Goodbye stichprobenartig — manuelle Abnahme

## Hinweise

- **Abweichung zur Planformulierung (dokumentiert, aus Code-Review Runde 1):** `SaveAsync`
  ruft nicht `UpdateMdnsAdvertisementEnabledAsync` auf, sondern persistiert den Schalter
  atomar über den erweiterten `UpdateGeneralSettingsAsync(..., bool mdnsAdvertisementEnabled, ...)`
  (ein `SaveChangesAsync` für das gesamte Formular; `ProgramSettingsService.cs:137–158`,
  `ProgramSettings.razor:136–141`). Das widerspricht der Planbegründung „Signatur von
  `UpdateGeneralSettingsAsync` stabil halten", wurde aber bewusst so umgesetzt
  (Atomaritäts-Befund in `review-code.1.md`); das geplante dedizierte Methodenpaar existiert
  weiterhin und ist per SQLite-Test abgesichert (`UpdateMdnsAdvertisementEnabledAsync` wird
  produktiv nur noch von Tests verwendet).
- Der Worker erzeugt nur `ServiceDiscovery` (das intern `MulticastService` instanziiert und
  beim Dispose mitstoppt) statt explizit beider Objekte — funktional gleichwertig,
  Goodbye-/Stop-Semantik gegeben.
- Ein dediziertes Warnlog zum Avahi-Doppel-Advertisement existiert nicht; Socket-Bind- und
  Advertise-Fehler (z. B. blockierter Port 5353) werden generisch als Warnung geloggt —
  deckt den im Programmablauf geforderten Fehlerfall ab; der Doku-Hinweis ist Teil der
  offenen Doku-Aufgaben.
- Verifikation der 5 Runde-1-Befunde (alle behoben): frühe Rückkehr bei `Mdns:Enabled=false`
  (`MdnsAdvertiserWorker.cs:82–87`), fail-closed `_adminSwitchEnabled` (Z. 32 + Test),
  atomare Speicherung (s. o.), Umbenennung `TryGetHttpPort` → `TryGetPort` (Z. 71),
  geschärftes ServiceType-Regex (Z. 14–16 + Tests).
- Build-/Testverifikation: `dotnet build` Debug+Release und volle Suite wurden vom
  Implementierer in `test-results.md` als grün dokumentiert (1657/1658 bestanden, 1
  dokumentierter Bestandsskip; ein hängender Bestandstest einzeln nachgeholt, alle grün).
