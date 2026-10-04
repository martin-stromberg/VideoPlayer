# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Alle Befunde sind klein; die Kern-Implementierung (Worker-Threading, Makaretu-API-Nutzung,
Migration, Backup-Regression, DI-Registrierung) ist korrekt. `dotnet build VideoPlayer.sln`
(Debug und Release) fehlerfrei, die 36 betroffenen Unit-Tests sind grün.

## Befunde

### MdnsAdvertiserWorker.cs (MdnsAdvertiserWorker)

- **Überflüssige Arbeit** — `ExecuteAsync` (Z. 90–112): Bei `Mdns:Enabled=false` läuft die
  60-s-Schleife endlos weiter und führt `RefreshAdminSwitchAsync` mit DB-Abfrage aus, obwohl die
  Konjunktion `configurationEnabled && adminSwitchEnabled` nie wahr werden kann (`IOptions` ist
  statisch, der Operatorschalter ändert sich zur Laufzeit nicht).

  Empfehlung: Nach dem initialen Check bei `!_options.Value.Enabled` aus `ExecuteAsync`
  zurückkehren (der vorhandene Info-Log bleibt), statt die Schleife zu betreten.

- **Fehlerbehandlung / fail-open** — `_adminSwitchEnabled` ist mit `true` initialisiert (Z. 30).
  Schlägt die erste `RefreshAdminSwitchAsync`-Abfrage fehl (Z. 78), wird bei aktivem
  `Mdns:Enabled` sofort advertised — ein evtl. deaktivierter Admin-Schalter wird dann
  ignoriert, ohne dass sein Wert je gelesen wurde. Für einen Datenschutz-relevanten Schalter
  ist fail-closed sicherer.

  Empfehlung: `_adminSwitchEnabled` mit `false` initialisieren, sodass erst nach einer
  erfolgreichen DB-Lesung advertised wird — oder das bewusste fail-open-Verhalten im
  Kommentar begründen.

### ProgramSettings.razor (SaveAsync)

- **Atomarität / Kapselung** — `SaveAsync` (Z. 127–148) ruft `UpdateGeneralSettingsAsync` und
  `UpdateMdnsAdvertisementEnabledAsync` nacheinander auf; jede Methode committet mit eigenem
  `SaveChangesAsync`. Schlägt der zweite Aufruf fehl, ist die Speicherung der
  Einstellungsseite teilweise erfolgt, ohne dass die UI einen Fehlerzustand zeigt.

  Empfehlung: `MdnsAdvertisementEnabled` als Parameter in `UpdateGeneralSettingsAsync`
  aufnehmen (ein `SaveChangesAsync` für das gesamte Formular) — entspricht dem bisherigen
  Muster einer Update-Methode pro Seite.

### MdnsServiceProfileBuilder.cs (MdnsServiceProfileBuilder)

- **Namenskonvention** — `TryGetHttpPort` (Z. 71) akzeptiert neben `http` ausdrücklich auch
  `https`-URIs (Z. 84); der Name beschreibt die Methode enger als ihr Verhalten.

  Empfehlung: Umbenennen in z. B. `TryGetEndpointPort` oder `TryGetUrlPort`.

### MdnsOptionsValidator.cs (MdnsOptionsValidator)

- **Fehlende Validierung (klein)** — Das Label-Muster `^_[a-z0-9-]{1,15}` akzeptiert
  führende/folgende Bindestriche (`_-x._tcp`, `_x-._tcp`), die RFC 6335 für Servicenamen
  nicht erlaubt.

  Empfehlung: Muster schärfen, z. B.:

  ```
  ^_[a-z0-9]([a-z0-9-]{0,13}[a-z0-9])?\._(?:tcp|udp)…
  ```

## Hinweise (keine Befunde)

- **mDNS-Nutzung korrekt:** `ServiceProfile`-Konstruktor, `Advertise`/`Announce`/
  `Unadvertise` (Goodbye-Pakete), Stripping des `.local`-Suffixes in `ToServiceName` und das
  Überschreiben von `HostName` (aktualisiert SRV-Target und AddressRecord-Namen, verifiziert
  gegen Paketquelle `jdomnitz/net-mdns`) sind korrekt verwendet.
- **Worker-Threading korrekt:** `StopAsync` wartet über `base.StopAsync` auf das Ende von
  `ExecuteAsync` (keine parallele Mutation), `IServiceScopeFactory` erzeugt disposed Scopes,
  kein `DbContext`-Leak, Cancellation sauber.
- **Berechtigung:** Der Schalter ist nur über die Admin-Seite erreichbar; der `IsAdmin`-Claim
  wird in `OnInitializedAsync` serverseitig geprüft und für Nicht-Admins wird kein
  Formular/Event-Handler gerendert (Muster der übrigen Admin-Seiten).
- **Migration/Snapshot:** `AddColumn` mit `defaultValue: true` auf `Setups`, Designer- und
  Snapshot-Modell konsistent.
- **Backup-Regression:** `OptionalRestoreColumns` und `OptionalRestoreBoolDefaults`
  (Default `true`) ergänzt; Regressionstest stellt ein Alt-Backup ohne die Spalte wieder her.
- **NuGet:** `Makaretu.Dns.Multicast.New` 0.38.0 (MIT), transitiv `Makaretu.Dns.New` 3.1.2
  (MIT) und `Common.Logging` 3.4.1 (Apache-2.0); net9.0-Assembly, mit net10.0 kompatibel.
- **`RaiseUiActionRequested`:** Mechanismus existiert in dieser Codebasis nicht; keine
  Handler erforderlich.
- **Dokumentation:** Die Doku-Schritte (`INSTALL_AVAHI.md`, `docs/help/einrichtung.md`,
  `README.md`, `docs/RELEASE_NOTES.md` u. a.) stehen in
  `issue-223-…-tasks.md` (Schritte 23/24) noch auf „Offen" — geplant separat, kein Code-Befund.
- E2E-Tests (`ProgramSettingsE2ETests`) wurden nicht ausgeführt (Playwright-Browser nötig);
  der Code ist konsistent mit den vorhandenen E2E-Basismustern (`UseKestrel`, `UseEnvironment("Testing")`).

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
