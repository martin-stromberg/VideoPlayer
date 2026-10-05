# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Die `requirement.md` enthält keinen explizit nummerierten Akzeptanzkriterien-Block; die
Kriterien wurden aus „Fachliche Zusammenfassung", „Implementierungsansatz" und
„Konfiguration" abgeleitet. Zusätzlich fließen die getroffenen Anwender-Entscheidungen
(Admin-Einstellbarkeit, vollständige URL inkl. Pfad) ein.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Discovery-Antwort meldet die aus Clientsicht erreichbare Basis-URL statt des `localhost`-Konstrukts aus `Host:Address`/`Host:Port` (Program.cs Zeile 54) | `DiscoveryResponseBuilder` (neu, `internal static`) + Resolver-Delegate im `UdpDiscoveryListener` + `Program.cs`-Verdrahtung (Umsetzungsreihenfolge 5–7) | `Build_DerivesLanAddress_WhenNothingConfigured`, `UdpDiscoveryListenerTests.Start_AnswersDiscoveryRequest_WithResolvedAddress` (echter Loopback-UDP) | Abgedeckt |
| Explizit konfigurierbare öffentliche Basis-URL mit vollem Schema/Host/Port/Pfad (IIS-Unterpfad `/videoplayer/`, Reverse-Proxy/TLS-Terminierung) | `Discovery:PublicBaseUrl` (`DiscoveryOptions` + `DiscoveryOptionsValidator` + `ValidateOnStart()`) plus admin-gepflegtes `Setup.DiscoveryPublicBaseUrl` mit oberstem Vorrang | `Build_ReturnsPublicBaseUrl_WhenConfigured`, `Build_ReturnsAdminBaseUrl_WhenSet` (inkl. `https`/Pfad), `DiscoveryOptionsValidatorTests` | Abgedeckt |
| Fallback-Ableitung ohne Konfiguration: LAN-Adresse/Hostname + Port aus bestehender Kette; `localhost`/Loopback/Wildcards werden nie gemeldet | Host-Kette (`Host:Address` ohne Loopback/Wildcard → literale gebundene Adresse → erste nicht-Loopback-IPv4 aus `Dns.GetHostEntry`-Liste → `Dns.GetHostName()`) + Port-Kette (gebundene Adressen → `Kestrel:Endpoints:Http:Url` → `Https:Url` → `Host:Port` → 5000) | `Build_IgnoresLoopbackHostAddress` (Theory), `Build_UsesBoundWildcardPort_ButDnsHost`, `Build_SkipsLoopbackBoundAddress_ForHost`, `Build_UsesHostName_WhenNoLanAddress`, `Build_SkipsIpv6Addresses`, Port-Ketten-Tests | Abgedeckt |
| Protokollformat unverändert `VIDEOWEBPLAYER_SERVER:<url>` | Listener-Änderung beschränkt sich auf die URL-Herkunft (Delegate), Sendeformat bleibt | `Start_AnswersDiscoveryRequest_WithResolvedAddress` prüft die exakte Antwortzeichenkette | Abgedeckt |
| Auflösung zum Ablesezeitpunkt statt einmalig beim Start (gebundene Adressen erst nach `ApplicationStarted` verfügbar) | Resolver-Delegate `Func<CancellationToken, Task<string>>` pro Anfrage; Admin-Wert pro Anfrage aus DB gelesen (kein Neustart nötig) | `Start_ResolvesAddress_PerRequest` (zwei Anfragen mit geändertem Delegate-Ergebnis) | Abgedeckt |
| `Testing`-Abschaltung des Listeners bleibt bestehen (fester Port 5001, E2E-Schutz) | Explizit „unverändert" in `Program.cs`-Änderungsbeschreibung und Umsetzungsreihenfolge 7 | Kein eigener Test geplant — die Abschirmung ist durch die Listener-Instanziierung in `Program.cs` (nicht DI) weiterhin strukturell gegeben; verifiziert in `Program.cs` Zeile 51 | Abgedeckt |
| Neue Konfigurationsschlüssel in `AutoUpdate:ProtectedFiles[1].JsonKeys` (Update-Überstehung) | Task 5: `Discovery:PublicBaseUrl`-Eintrag | Bestehender `AutoUpdateProtectedFilesTests.ShippedAppsettingsProtectedFiles_AreValid` validiert die geänderte `appsettings.json` mit (verifiziert: `VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs` Zeile 61) | Abgedeckt |
| Admin-Einstellbarkeit (Anwender-Entscheidung, offene Frage 6): DB-Spalte, Migration, Backup-Kompatibilität, UI, serverseitige Validierung | `Setup.DiscoveryPublicBaseUrl` (`string?`) + Migration `AddSetupDiscoveryPublicBaseUrl` + `OptionalRestoreColumns`-Eintrag ohne Defaults (Präzedenzfall `PlaylistBackfillLastSweepAt`, verifiziert Zeile 70/85–98) + `ProgramSettingsService`-Erweiterung mit `ArgumentException`-Validierung + `admin-card` in `ProgramSettings.razor` | `ProgramSettingsServiceTests`-Erweiterung (echtes SQLite per `PairingTestDb`), `VideoWebPlayerBackupDataTests`-Alt-Backup-Regressionstest per `LegacyBackupArchiveBuilder.RemoveColumnsAsync` (verifiziert Zeile 144), E2E-Tests | Abgedeckt |
| `Host:Address`/`Host:Port` bleiben als Ableitungsbausteine bestehen (Rückwärtskompatibilität, offene Frage 5) | Designentscheidung dokumentiert; Host-/Port-Ketten enthalten beide Schlüssel | `Build_UsesHostAddress_WhenLanAddress`, `Build_UsesHostPortFallback`, `Build_UsesDefaultPort5000` | Abgedeckt |
| UDP-Antwort unauthentifiziert (keine Berechtigungsprüfung nötig); Admin-Feld nur für Admins sichtbar | `IsAdmin`-Claim-Prüfung in `ProgramSettings.razor` bleibt unverändert (verifiziert Zeile 111) | `NonAdmin_GetsNotAuthorized_OnProgramSettings` um Assertion `#discoveryPublicBaseUrl` Count 0 erweitert | Abgedeckt |
| Client-Vertrag / Pfadanteil (offene Frage 1): verbindlich „vollständige URL inkl. Pfad" | Designentscheidung festgehalten; MAUI-Verifikation als externe Folgeaufgabe benannt | Kein serverseitiger Test nötig/möglich (Client im externen Repository) | Abgedeckt |
| Dokumentation (`docs/API.md`, `GUIDE_Installation.md`, `docs/help/einrichtung.md`, `README.md`, `RELEASE_NOTES.md`) | Tasks 24–28, inkl. IIS-`OutOfProcess`-/TLS-Pflichtkonfigurationshinweis und Korrektur der `http://localhost:5000`-Stelle (verifiziert `GUIDE_Installation.md` Zeile 53) | `tools/MarkdownLinkCheck.Tests` im Verifikationsschritt (Task 29) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin pflegt öffentliche Basis-URL unter `/admin/program-settings` (einziger UI-Benutzerfluss des Features; durch Anwender-Entscheidung hinzugekommen) | `Admin_SavesDiscoveryPublicBaseUrl_AndSettingPersists` (Playwright in `ProgramSettingsE2ETests`: Login → Feld `#discoveryPublicBaseUrl` füllen → „Gespeichert." → Service-Lesung im Scope → Reload → Löschpfad) — Muster `Admin_TogglesMdnsAdvertisement_AndSettingPersists` verifiziert (`ProgramSettingsE2ETests.cs` Zeilen 110–140) | Abgedeckt |
| Ungültige Admin-Eingabe wird clientseitig blockiert | `Admin_EntersInvalidDiscoveryPublicBaseUrl_ValidationBlocksSave` (Validierungsmeldung sichtbar, kein „Gespeichert.", DB unverändert) | Abgedeckt |
| Nicht-Admin sieht das neue Feld nicht | Erweiterung von `NonAdmin_GetsNotAuthorized_OnProgramSettings` (verifiziert Zeilen 144–154) | Abgedeckt |
| UDP-Broadcast-Anfrage → Antwort mit erreichbarer URL (Kernmechanismus, kein UI-Fluss; Listener unter `Testing` abgeschaltet, fester Port 5001) | Kein E2E möglich — Ersatz: echte Loopback-UDP-Tests `Start_AnswersDiscoveryRequest_WithResolvedAddress` und `Start_ResolvesAddress_PerRequest` in `UdpDiscoveryListenerTests`; Begründung im Plan (Testing-Abschirmung) nachvollziehbar | Nicht erforderlich mit Begründung |

## Hinweise

- Alle stichprobenartig geprüften Code-Fakten stimmen mit dem tatsächlichen Stand überein:
  `UdpDiscoveryListener`-Signatur und `catch { }`-Schleife (`Services/UdpDiscoveryListener.cs`
  Zeilen 23, 46–63), `Program.cs` Zeilen 50–57/59, `MdnsServiceProfileBuilder` (`internal static`,
  `Dns.GetHostName()` Zeile 37, `TryGetPort`-Wildcard-Normalisierung),
  `ServiceCollectionExtensions.cs` Zeilen 206 (`App:BaseUrl`), 251 (scoped `ProgramSettingsService`),
  280–283 (`MdnsOptions`-Block), 297–301 (Testing-Schirmung Worker),
  `Setup.MdnsAdvertisementEnabled` (Zeile 53), `ProgramSettingsService` Zeilen 141–160/167–171,
  `ProgramSettings.razor` (EditForm/`DataAnnotationsValidator`/`SettingsModel`, `@using`
  `System.ComponentModel.DataAnnotations` Zeile 2), `VideoWebPlayerBackupData`
  (`OptionalRestoreColumns` Zeilen 41–75, `PlaylistBackfillLastSweepAt` Zeile 70 ohne
  Defaults-Eintrag, `ApplicationTitle`-Sonder-Default Zeilen 873–879), `appsettings.json`
  (`Mdns` Zeilen 42–47, `Host:Address`/`Host:Port` in `JsonKeys` Zeilen 133–134),
  `ProgramSettingsE2ETests`/`ProgramSettingsServiceTests`/`PairingTestDb`/
  `LegacyBackupArchiveBuilder`/`InternalsVisibleTo` (`VideoWebPlayer.csproj` Zeile 15),
  `appsettings.Production.json` `http://*:5002` (Zeile 24).
- Kleinere Ungenauigkeiten ohne Abdeckungslücke, bei der Umsetzung zu berücksichtigen:
  - Plan Zeile 191 listet `Microsoft.Extensions.DependencyInjection` als neues Using — es ist
    in `Program.cs` bereits vorhanden (Zeile 6).
  - Plan Zeile 228 nennt `OptionalRestoreIntDefaults` — die tatsächliche Liste heißt
    `OptionalRestoreLongDefaults` (`VideoWebPlayerBackupData.cs` Zeile 100); für die neue
    `string?`-Spalte ohnehin ohne Eintrag.
  - `docs/INSTALL_AVAHI.md` (Zeilen 90–91 nennen den UDP-5001-Fallback mit
    `VIDEOWEBPLAYER_SERVER:<adresse>`) ist in `inventory.md` als Dokumentationsstelle gelistet,
    taucht aber in keiner Doku-Aufgabe (24–28) auf. Die dort beschriebenen Fakten bleiben
    korrekt (Protokollformat unverändert) — optional beim Doku-Schritt mitaktualisieren.
  - `JsonKeys`-Zeilenangabe „~121–174" im Plan vs. tatsächlich 123–174 — trivial.
- Die pro Anfrage erfolgende DB-Lesung über `GetOrCreateSetupAsync` legt die `Setup`-Zeile
  bei Bedarf schreibend an — vom Plan als fail-open eingeordnet und vertretbar; bei der
  Implementierung prüfen, ob der Schreibpfad pro Broadcast unerwünscht ist.
- Der JSON-Wert `"PublicBaseUrl": null` in der ausgelieferten `appsettings.json` sollte
  im Zuge des `AutoUpdateProtectedFilesTests` verifiziert werden (Merge-Verhalten bei
  `null`-Werten); der Test deckt die formale Gültigkeit ab.
