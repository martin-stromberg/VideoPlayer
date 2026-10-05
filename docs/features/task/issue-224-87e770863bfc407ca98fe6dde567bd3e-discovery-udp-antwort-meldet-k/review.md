# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Klassen

- [x] `DiscoveryUrlRules` (`internal static`, `VideoWebPlayer/Configuration/DiscoveryUrlRules.cs`) – angelegt mit `IsValidPublicBaseUrl(string?)` (null/leer zulässig, sonst absolute http/https-URI) und `NormalizePublicBaseUrl(string?)` (Trim, leer → null); genutzt von `DiscoveryOptionsValidator`, `ProgramSettingsService`, `AbsoluteHttpUrlAttribute` und `DiscoveryResponseBuilder`
- [x] `DiscoveryOptions` (`public sealed`, `VideoWebPlayer/Configuration/DiscoveryOptions.cs`) – angelegt, Eigenschaft `PublicBaseUrl` (`string?`, Standard null)
- [x] `DiscoveryOptionsValidator` (`public sealed`, `IValidateOptions<DiscoveryOptions>`, `VideoWebPlayer/Configuration/DiscoveryOptionsValidator.cs`) – angelegt, delegiert an `DiscoveryUrlRules.IsValidPublicBaseUrl`, Fail mit klarer deutscher Meldung
- [x] `DiscoveryResponseBuilder` (`internal static`, `VideoWebPlayer/Services/DiscoveryResponseBuilder.cs`) – angelegt mit `Build(DiscoveryOptions, IConfiguration, IEnumerable<string>?, IReadOnlyList<IPAddress>?, string?)`; Vorrangkette Admin-Wert (normalisiert + Defensivprüfung) → `Discovery:PublicBaseUrl` → Ableitung; Schema-/Port-Kette (gebundene Adressen → `Kestrel:Endpoints:Http:Url` → `Kestrel:Endpoints:Https:Url` → `Host:Port` → 5000); Host-Kette (`Host:Address` ohne Loopback/Wildcard → literaler Host aus gebundenen/Kestrel-Adressen → erste nicht-Loopback-IPv4 mit APIPA als letzte IP-Stufe → `Dns.GetHostName()` → `localhost`-Notfallback); Loopback-/Wildcard-Verbot; Wildcard-Normalisierung `*`/`+` für die URI-Prüfung; IPv6 für den Host-Teil ausgeschlossen
- [x] `AbsoluteHttpUrlAttribute` (`public sealed`, `ValidationAttribute`, `VideoWebPlayer/Components/Shared/AbsoluteHttpUrlAttribute.cs`) – angelegt, null/leer zulässig, delegiert an `DiscoveryUrlRules`, deutsche `ErrorMessage`

### Änderungen an bestehenden Klassen/Dateien

- [x] Feld `DiscoveryPublicBaseUrl` (`string?`) in `Setup` (`VideoWebPlayer/Data/Setup.cs:62`) – vorhanden, XML-Doc mit Vorrangregel
- [x] Methode `GetDiscoveryPublicBaseUrlAsync(CancellationToken)` in `ProgramSettingsService` (`ProgramSettingsService.cs:176`) – vorhanden, normalisiert, Muster `GetMdnsAdvertisementEnabledAsync`
- [x] Methode `UpdateGeneralSettingsAsync` in `ProgramSettingsService` (`ProgramSettingsService.cs:144`) – um `string? discoveryPublicBaseUrl` vor `CancellationToken` erweitert; Normalisierung via `DiscoveryUrlRules`, `ArgumentException` bei nicht leerem ungültigem Wert vor `GetOrCreateSetupAsync`/`SaveChangesAsync` (atomar, nichts wird geschrieben); alle Call-Sites angepasst
- [x] Konstruktor `UdpDiscoveryListener(int, Func<CancellationToken, Task<string>>)` (`UdpDiscoveryListener.cs:23`) – `string serverAddress` ersetzt; `ListenAsync` löst die URL pro Anfrage auf und sendet `VIDEOWEBPLAYER_SERVER:{url}`; `Start`/`Stop`/`catch { }` unverändert
- [x] `Program.cs` (`Program.cs:55-93`) – String-Interpolation entfernt; Resolver-Delegate mit `CreateAsyncScope` + `GetDiscoveryPublicBaseUrlAsync` (try/catch → null, fail-open), `IOptions<DiscoveryOptions>`, `IServer`/`IServerAddressesFeature`, fehlerabgeschirmtes `Dns.GetHostEntry(Dns.GetHostName())`, `DiscoveryResponseBuilder.Build`; Usings ergänzt; `Testing`-Abschirmung, Port 5001 und Start vor `app.Run()` unverändert
- [x] `ServiceCollectionExtensions` (`ServiceCollectionExtensions.cs:284-287`) – `AddSingleton<IValidateOptions<DiscoveryOptions>, DiscoveryOptionsValidator>()` und `AddOptions<DiscoveryOptions>().Bind(...).ValidateOnStart()` direkt neben dem `MdnsOptions`-Block
- [x] `appsettings.json` – `Discovery`-Sektion mit `"PublicBaseUrl": null` (Zeilen 48-50) und `Discovery:PublicBaseUrl` in `AutoUpdate:ProtectedFiles[1].JsonKeys` (Zeile 177)
- [x] `ProgramSettings.razor` – neue `admin-card` „Öffentliche Basis-URL" hinter der mDNS-Karte (Zeilen 98-106): `InputText` id `discoveryPublicBaseUrl`, Label, `ValidationMessage`, `form-text` mit Zweck/Vorrang/Ableitung/Beispiel-URL; `SettingsModel.DiscoveryPublicBaseUrl` mit `[AbsoluteHttpUrl]` (Zeilen 189-190); Laden in `OnInitializedAsync` (Zeile 139); Speichern in `SaveAsync` inkl. `ArgumentException` → `alert-danger` (Zeilen 162-165); `IsAdmin`-Prüfung und Formulargerüst unverändert
- [x] `VideoWebPlayerBackupData` – `OptionalRestoreColumns` um `Setups.DiscoveryPublicBaseUrl` ergänzt (Zeile 75); korrekt **kein** Eintrag in den `OptionalRestore*Defaults`-Listen (nullable `string?`-Spalte)

### Datenbankmigration

- [x] Migration `20261005171612_AddSetupDiscoveryPublicBaseUrl` – `AddColumn<string>` `DiscoveryPublicBaseUrl` auf `Setups`, nullable TEXT; `Down` entfernt die Spalte; Designer-Datei und `ApplicationDbContextModelSnapshot` (Zeile 270-271) aktualisiert

### Tests

- [x] `DiscoveryUrlRulesTests` – angelegt, alle geplanten Fälle (`null`/leer/Whitespace gültig; http/https inkl. Pfad/Port gültig; relativ/Nicht-URI/`ftp`/`dns`/`file` ungültig; Normalize: Trim + leer → null)
- [x] `DiscoveryResponseBuilderTests` – angelegt, alle 15 geplanten Cases vorhanden inkl. `CreateConfiguration`-Hilfsmethode per `AddInMemoryCollection`
- [x] `DiscoveryOptionsValidatorTests` – angelegt, alle geplanten Cases
- [x] `DiscoveryConfigurationTests.DiscoverySection_BindsToOptions` – angelegt (Muster `MdnsConfigurationTests`)
- [x] `UdpDiscoveryListenerTests` – angelegt: `Start_AnswersDiscoveryRequest_WithResolvedAddress` (echter Loopback-UDP auf freiem Port, exakte Antwortzeichenkette, `Stop()` im `finally`) und `Start_ResolvesAddress_PerRequest` (Nachweis der pro-Anfrage-Auflösung)
- [x] `ProgramSettingsServiceTests` – um `GetDiscoveryPublicBaseUrlAsync_DefaultsToNull`, `UpdateGeneralSettingsAsync_PersistsDiscoveryPublicBaseUrl` (Trim, leer → NULL, Löschpfad, Atomarität) und `UpdateGeneralSettingsAsync_RejectsInvalidDiscoveryPublicBaseUrl` (Theory `notaurl`/`/relativ`/`ftp://x`, Zeile unverändert) erweitert; echtes SQLite per `PairingTestDb`; Bestandsaufrufe an neue Signatur angepasst
- [x] `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutDiscoveryPublicBaseUrlColumn_RestoresWithNull` – angelegt, Spalte per `LegacyBackupArchiveBuilder.RemoveColumnsAsync` real entfernt, echtes SQLite
- [x] `ProgramSettingsE2ETests` – `Admin_SavesDiscoveryPublicBaseUrl_AndSettingPersists` (Füllen → „Gespeichert." → Service-Lesung → Reload → Löschpfad → null), `Admin_EntersInvalidDiscoveryPublicBaseUrl_ValidationBlocksSave` (Validierungsmeldung sichtbar, kein „Gespeichert.", DB unverändert), `NonAdmin_GetsNotAuthorized_OnProgramSettings` um `#discoveryPublicBaseUrl`-Count-0-Assertion erweitert

### Dokumentation

- [x] `docs/API.md` – UDP-Fallback-Absatz um vollständige Basis-URL inkl. Pfad, Vorrangkette und Ableitungskette erweitert; IIS/Reverse-Proxy/TLS-Pflichthinweis ergänzt
- [x] `docs/GUIDE_Installation.md` – `http://localhost:5000`-Fallback-Formulierung korrigiert; Abschnitt „Netzwerk-Erkennung (Discovery)" um UDP-Broadcast-Subabschnitt mit `Discovery:PublicBaseUrl`-/`Host:*`-Tabelle, Admin-Vorrang und IIS-`OutOfProcess`-/TLS-Pflichthinweis erweitert
- [x] `docs/help/einrichtung.md` – neuer Abschnitt „Öffentliche Basis-URL" mit Zweck, Vorrang, leer = Ableitung und Backup-Übertragbarkeit
- [x] `README.md` – Konfigurationsabsatz um `Discovery:PublicBaseUrl` und Admin-Feld ergänzt
- [x] `docs/RELEASE_NOTES.md` – Eintrag in „Important Notes Before Update" (EN), „What's New" (EN), „Wichtige Hinweise vor dem Update" (DE) und „Neuerungen" (DE), inkl. Migration und Merge-Schutz
- [x] Bonus: `docs/INSTALL_AVAHI.md` mitaktualisiert (war in `plan-check.md` als optional markiert)

## Hinweise

- Im Review selbst verifiziert: `dotnet build VideoPlayer.sln` Debug und Release je 0 Fehler/0 Warnungen; gefilterter Lauf der neuen/geänderten Tests 63/63 grün; `VideoWebPlayerBackupDataTests` 27/27 grün; `tools/MarkdownLinkCheck.Tests` 6/6 grün; **voller** Lauf `dotnet test VideoWebPlayer.Tests` 1723/1724 grün (1 übersprungener Bestandstest `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`).
- `docs/API.md` Zeile 12 („Basis-URL lokal: `http://localhost:5000`, sofern `Host:Address`/`Host:Port` nicht anders konfiguriert") wurde bewusst unverändert belassen — der Satz beschreibt die lokale API-Basis-URL, nicht die Discovery-Antwort; der Plan verlangte hier nur „prüfen/anpassen". Vertretbar, dem Implementierer entgegenzunehmen.
- `PairingTestDb` nutzt `EnsureCreated` (Modellabgleich statt Migrationen); die Migration selbst läuft über `app.MigrateDatabase()` beim realen Start bzw. in den E2E-Fixtures (`WebApplicationFactory`) mit — kein reiner Migrationstest vorhanden, entspricht aber dem bisherigen Projektstandard.
- Pro Discovery-Anfrage legt `GetOrCreateSetupAsync` die `Setup`-Zeile bei Bedarf schreibend an — bereits in `plan-check.md` als vertretbar eingestuft.
- Der komplette Arbeitsstand liegt noch **uncommitted** im Working Tree des Branches (geänderte + neue Dateien per `git status`); Commit obliegt dem Lifecycle-Prozess.
