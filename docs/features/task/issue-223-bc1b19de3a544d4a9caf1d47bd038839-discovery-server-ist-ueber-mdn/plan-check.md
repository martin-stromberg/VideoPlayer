# Plan-Gegenprüfung

Geprüft: `plan.md` gegen `requirement.md`, `inventory.md` inkl. Detaildokumente
(`inventory/logic.md`, `inventory/models.md`, `inventory/configuration.md`,
`inventory/tests.md`, `inventory/documentation.md`) sowie die Anwender-Entscheidungen
(dedizierter Diensttyp `_videowebplayer._tcp.local.`, `Makaretu.Dns.Multicast.New`,
`Mdns:Enabled` Default `true`, Admin-Schalter über `Setup`/`ProgramSettingsService` mit
Konjunktionsregel). Zusätzlich wurden die im Plan referenzierten Code-Muster
stichprobenartig gegen den Ist-Stand verifiziert (siehe Hinweise).

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Server announcet sich per mDNS/DNS-SD (UDP 5353) selbst | `MdnsAdvertiserWorker` (`BackgroundService`) mit `Makaretu.Dns.Multicast.New` (`MulticastService`/`ServiceDiscovery`/`ServiceProfile`), Registrierung in `AddVideoWebPlayerServices` (Schritte 1, 7) | Funktionsnachweis manuell per `avahi-browse`/`dns-sd` (Schritt 13) — begründet nicht automatisierbar; Ableitungslogik durch `MdnsServiceProfileBuilderTests` abgesichert | Abgedeckt |
| Dienst eindeutig als VideoPlayer-Server erkennbar (dedizierter Diensttyp statt generischem `_http._tcp`) | Genau ein `ServiceProfile` unter `_videowebplayer._tcp.local.` (Default von `Mdns:ServiceType`, konfigurierbar), TXT `app=VideoWebPlayer`, `path=/`, Instanzname `VideoWebPlayer` (Schritt 6) | `Build_UsesDefaults_WhenNothingConfigured`, `Build_UsesConfiguredServiceType`, `Build_UsesConfiguredInstanceName` | Abgedeckt |
| Advertisement beim Start registrieren, beim Shutdown sauber deregistrieren (Goodbye-Pakete) | `ExecuteAsync` advertised/announced; `StopAsync` ruft `Unadvertise`/Dispose auf (Programmablauf „Deregistrierung beim Shutdown") | Kein automatisierter Versand-Test (begründet); Start-/Stop-Verhalten Teil des manuellen Nachweises | Abgedeckt |
| Testing-Abschaltung: E2E-/Unit-Tests belegen keinen Port 5353 | Konditionale Registrierung: `AddHostedService<MdnsAdvertiserWorker>()` nur wenn `!builder.Environment.IsEnvironment("Testing")` (Schritt 7) | `AddVideoWebPlayerServices_DoesNotRegisterWorker_InTesting` / `..._RegistersWorker_OutsideTesting` | Abgedeckt |
| Konfigurierbarkeit über `appsettings`/Umgebung (`Enabled`, `ServiceType`, `InstanceName`, `Port`, TXT) | `MdnsOptions` + `MdnsOptionsValidator` (`ValidateOnStart`), Abschnitt `Mdns` in `appsettings.json` (Schritte 2, 8) | `MdnsSection_BindsToOptions`, `MdnsOptionsValidatorTests` (Reject-/Accept-Fälle), Port-Ableitungskette in `MdnsServiceProfileBuilderTests` | Abgedeckt |
| Neue Schlüssel in `AutoUpdate:ProtectedFiles[1].JsonKeys` (Update-sicher) | Alle `Mdns:*`-Schlüssel in `JsonKeys` (Schritt 8) | Bestandstest `AutoUpdateProtectedFilesTests.ShippedAppsettingsProtectedFiles_AreValid` als betroffener Test genannt | Abgedeckt |
| `UdpDiscoveryListener` (Port 5001) bleibt unverändert als Fallback | Explizit „Keine Änderung" im Plan | Nicht erforderlich — keine Änderung | Abgedeckt |
| Admin-Schalter `Setup.MdnsAdvertisementEnabled` (DB, Default `true`) | Neue `bool`-Spalte auf `Setup`, EF-Migration `AddSetupMdnsAdvertisementEnabled` (Schritt 3) | `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue` / `UpdateMdnsAdvertisementEnabledAsync_Persists` gegen echtes SQLite (`PairingTestDb`-Muster) | Abgedeckt |
| `ProgramSettingsService` um Getter/Setter erweitern | `GetMdnsAdvertisementEnabledAsync` / `UpdateMdnsAdvertisementEnabledAsync` (Schritt 4); `UpdateGeneralSettingsAsync` bleibt stabil | `ProgramSettingsServiceTests` (SQLite) | Abgedeckt |
| `VideoWebPlayerBackupData`: `OptionalRestoreColumns` + `OptionalRestoreBoolDefaults` + Regressionstest für Alt-Backup | Einträge `Setups.MdnsAdvertisementEnabled` bzw. `(Setups, …, true)` (Schritt 5) | `ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled` via `LegacyBackupArchiveBuilder` (verifiziert vorhanden) | Abgedeckt |
| Konjunktionsregel: aktiv nur wenn `Mdns:Enabled` UND `Setup.MdnsAdvertisementEnabled` `true`; kein Schalter überstimmt den anderen | Durchgängig konsistent (Übersicht, Designentscheidung, Programmabläufe, Offene Punkte); `internal` prüfbare Methode (`InternalsVisibleTo` existiert) | `IsAdvertisementEnabled_RequiresBothSwitches` (Konjunktionstabelle) | Abgedeckt |
| Admin-Schalter wirkt ohne Neustart (Laufzeit-Umschaltung) | 60-s-Re-Evaluation des DB-Schalters im Worker mit `Advertise`/`Unadvertise` (Programmablauf „Laufzeit-Umschaltung") | Konjunktions-Unit-Test; Laufzeit-Effekt im manuellen Nachweis (Schritt 13, „Dienst verschwindet ≤ ~60 s") | Abgedeckt |
| Blazor-UI: Checkbox im Setup-Bereich `/admin/program-settings` | Neue `admin-card` „Netzwerk-Erkennung (mDNS)" mit `InputCheckbox` + `form-text` (Konjunktion, ~60 s, Diensttyp, UDP 5353); Laden in `OnInitializedAsync`, Speichern über `UpdateMdnsAdvertisementEnabledAsync` (Schritt 9) | E2E: Toggle speichern → Reload persistiert (beide Richtungen) | Abgedeckt |
| Serverseitige Berechtigung für den Schalter | Vorhandener `IsAdmin`-Claim-Guard der Seite bleibt unverändert (verifiziert in `ProgramSettings.razor` Zeilen 10–13, 102); `IsAdmin`-Prüfung läuft serverseitig im InteractiveServer-Circuit | E2E: Nicht-Admin erhält „Nicht autorisiert", kein Zugriff auf den Schalter | Abgedeckt |
| Neues NuGet-Paket nur nach Lizenz-/Release-/CI-Prüfung (AGENTS.md §7) | Schritt 1: Lizenz (MIT, auf nuget.org verifiziert), transitive Abhängigkeiten, Paketversion > 7 Tage alt, Debug+Release-Build, CI-`security-scan` (`.github/actions/security-scan` existiert) | Build-Nachweis im Schritt; Ergebnis im Handover | Abgedeckt |
| Keine sensiblen Daten in TXT-Records | Feste TXT-Records `path=/`, `app=VideoWebPlayer` im Code, nicht konfigurierbar | `Build_UsesDefaults_WhenNothingConfigured` prüft TXT-Inhalt | Abgedeckt |
| Fehlerfall: mDNS-/Socket-Fehler dürfen die App nicht stoppen | Fehler beim Bind/Advertise/Umschalten → Warnlog, bisheriger Zustand bleibt, Schleife/App läuft weiter (Programmabläufe) | Indirekt abgesichert (kein expliziter Test für den Catch-Pfad — siehe Hinweise) | Abgedeckt |
| Doku-Updates (`INSTALL_AVAHI.md`, `API.md`, `GUIDE_Installation.md`, `docs/help/einrichtung.md`, `README.md`, `RELEASE_NOTES.md`) | Schritt 12 deckt alle genannten Dateien plus `docs/INDEX.md` ab; `API.md` konditional wg. `ApiDocumentationContractTests` | Contract-Tests als betroffene Tests genannt | Abgedeckt |
| DoD: Build Debug+Release, volle Testsuite, Migration/Backup-Regression, echtes SQLite, serverseitige Berechtigung | Schritt 13 (Gesamtlauf), Schritte 3/5 (Migration + Backup), `PairingTestDb`-SQLite, IsAdmin-Guard + Nicht-Admin-E2E | Jeweils benannt | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (Status `Plan vollständig`, nicht ausgefüllt)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin schaltet mDNS-Advertisement in `/admin/program-settings` aus/ein, speichert, lädt neu → Zustand persistiert (UI-Fluss über `ProgramSettingsService` → `Setup`-Tabelle) | `ProgramSettingsE2ETests` (neu; Muster `UpdatesPageE2ETests`/`BackupUploadE2ETests`: `WebApplicationFactory<Program>` + `EnvironmentName="Testing"` + `UseUrls("http://127.0.0.1:0")` + Playwright + Admin-Seed per `UserManager` — Muster verifiziert) | Abgedeckt |
| Nicht-Admin öffnet `/admin/program-settings` → „Nicht autorisiert", kein Schalter sichtbar | `ProgramSettingsE2ETests` (Muster `BackupUpload_NonAdmin_SeesNoUploadControl` — verifiziert vorhanden) | Abgedeckt |
| mDNS-Advertisement selbst (Dienst erscheint/verschwindet im Browse) | Kein E2E — begründet: `Testing`-Umgebung registriert den Worker bewusst nicht; echtes Multicast-Verhalten ist per Browser-Test nicht verlässlich nachweisbar. Manueller Nachweis per `avahi-browse`/`dns-sd` in Schritt 13, im Abnahmebericht zu dokumentieren | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

— (Status `Plan vollständig`, nicht ausgefüllt)

## Hinweise

- **Verifikation der Plan-Annahmen gegen den Code:** Alle stichprobenartig geprüften
  Referenzen stimmen: `ProgramSettings.razor` (`/admin/program-settings`, IsAdmin-Claim
  `user.HasClaim("IsAdmin", "True")` in Zeile 102, `admin-card`-Muster, `GetOrCreateSetupAsync`,
  `UpdateGeneralSettingsAsync`-Signatur), `ProgramSettingsService` ist `AddScoped`
  (`ServiceCollectionExtensions.cs` Zeile 251 → `IServiceScopeFactory`-Muster im Worker korrekt),
  `VideoWebPlayerBackupData.OptionalRestoreColumns`/`OptionalRestoreBoolDefaults` (Zeilen 41/84),
  `AddVideoWebPlayerServices(this WebApplicationBuilder)` (Zeile 41, `builder.Environment`
  bereits in Gebrauch), `EpisodeBackgroundImageOptionsValidator` + `ValidateOnStart()` (Z. 276–279),
  `LegacyBackupArchiveBuilder` inkl. `RemoveColumnsAsync`, `InternalsVisibleTo` im csproj (Z. 15),
  `Testing`-Guard in `Program.cs` Z. 51. `Makaretu.Dns.Multicast.New` existiert (0.38.0, MIT,
  `ServiceDiscovery` mit `Advertise`/`Unadvertise`).
- **Konjunktionsregel:** konsistent an allen vier Nennungsstellen (beide Schalter müssen `true`
  sein, kein Override in beide Richtungen). Der `form-text` in der UI erklärt die Konjunktion —
  gute Verständlichkeit für Admins.
- **Optionale Nachplanung (kein Lückengrund):**
  - Für den Fehlerbehandlungspfad des Workers (Bind-/Advertise-Fehler → Warnung statt
    Host-Abbruch; `BackgroundService`-Exceptions stoppen den Host) ist kein expliziter Test
    benannt. Ein Test über einen internen Seam analog zur Konjunktionsmethode wäre möglich.
  - Bei `Mdns:Enabled=false` (statisch) pollt der Worker dennoch alle 60 s die
    `Setup`-Tabelle, obwohl die Konjunktion zur Laufzeit nie `true` werden kann — ggf.
    Polling nur bei `Enabled=true` aktivieren.
  - `docs/API.md` ist im Plan konditional („falls Contract-Tests es zulassen") — bei der
    Umsetzung prüfen, ob ein Discovery-Abschnitt die Contract-Prüfung bricht.
