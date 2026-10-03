# Plan-Gegenprüfung

Runde 3 der Gegenprüfung (Runde 1: `plan-check.1.md`, Runde 2: `plan-check.2.md` — Status „Plan lückenhaft" mit einem verbliebenen Befund: fehlende `appsettings.Local.json`-Dokumentation in V5). Der Befund ist eingearbeitet; die neuen Planelemente wurden gegen Repo, .NET SDK 10.0.401 und `lib/packages/msTools.Updater.0.11.0-rc.1.nupkg` (Skripttext aus der DLL extrahiert) verifiziert.

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Konfigurierte `web.config`-Elemente (`environmentVariables`, `ipSecurity`, `httpProtocol`) und -Attribute überleben Update-Installation | `Merge`-Eintrag `web.config`: `XmlElements` `//aspNetCore/environmentVariables`, `//security/ipSecurity`, `//system.webServer/httpProtocol`; `XmlAttributes` 8 deployment-eigene `aspNetCore`-Attribute ohne `processPath`/`arguments`/`hostingModel` — Elternpfade gegen echte `web.config` verifiziert (`//system.webServer`, `//security` via `requestFiltering`, `//aspNetCore` vorhanden; `//httpProtocol` korrekt vermieden, da Elternknoten fehlt) | `ConfigurationBindsProtectedFiles`, `ShippedAppsettingsProtectedFiles_AreValid`, `GeneratedScript_ContainsBackupAndMergeInOrder`, `ShippedWebConfig_ContainsAllMergeParents` | Abgedeckt |
| Konfigurierte `appsettings*.json`-Schlüssel überleben; neue Paket-Schlüssel kommen an | `Merge`-Eintrag `appsettings*.json` mit 50 `JsonKeys` (nur deployment-eigene Schlüssel; `AutoUpdate:ProtectedFiles` und Subtrees bewusst ausgenommen); alle Schlüssel gegen Repo-Verwendung/`appsettings.json`-Struktur verifiziert (`Jwt:ApiToken` als Fallback neben `:Web`/`:Maui` in `ServiceCollectionExtensions.cs:50` korrekt berücksichtigt) | dieselben vier Tests | Abgedeckt |
| `Preserve`-Dateien bleiben unverändert | Kein `Preserve`-Eintrag — begründet (nur-lokale Dateien sind ohnehin update-sicher; `Preserve` auf paketierte Dateien blockiert Paket-Verbesserungen) | — | Nicht erforderlich mit Begründung |
| Dateien ohne Schutzeintrag werden wie bisher ersetzt | Nur zwei Einträge; Rest unverändert (Bibliotheks-Default) | implizit über Skript-Inhaltstest | Abgedeckt |
| Paket-Verbesserungen gemergter Dateien kommen an | `Merge` statt `Preserve`; geschlossene `JsonKeys`-/`XmlElements`-/`XmlAttributes`-Liste | Bindungs-/Validierungstest der ausgelieferten Liste | Abgedeckt |
| Backup-/Merge-Fehler brechen nicht ab; Warnung + Backup liegt bereit | Bibliotheksverhalten im extrahierten Skript verifiziert: pro-Datei-`try/catch` → `Write-Warning` in `Invoke-AutoUpdateProtectedFiles`; `Updates/backup/` als manueller Restore-Pfad | kein eigener Test — bibliotheksseitig verifiziert | Abgedeckt |
| Leere/fehlende Schutzliste = bisheriges Verhalten | Additive API (`HasProtectedFiles` false → unverändertes Skript) | „Keine bestehenden Tests brechen" — plausibel | Abgedeckt |
| Windows beide Varianten + Linux | Bibliothek deckt alle drei Skript-Templates ab; geplante Pfade in ElementTree-Teilmenge (Guard-Test sichert das jetzt zusätzlich ab) | Skript-Inhaltstest plattformunabhängig via `IAutoUpdatePlatformResolver`-Testdouble | Abgedeckt |
| V1: `PackageReference` beider Projekte → `0.11.0-rc.1`, nupkg-Tausch, Lizenz, Debug+Release, CI | Verifiziert: `VideoWebPlayer.csproj:53` und `VideoWebPlayer.Tests.csproj:22` stehen auf `0.10.4-rc.1`; beide nupkgs in `lib/packages/` vorhanden (0.11.0 untracked); `NuGet.config` bindet `lib/packages` ein; MIT-Lizenz (nuspec) | Build-Nachweis Debug + Release (DoD, Schritt 5) | Abgedeckt |
| V3: `appsettings.Local.json` Neubewertung und Umsetzung | Umsetzung entschieden und begründet; Positionsanker (letzte `JsonConfigurationSource` mit `appsettings*.json`-Dateiname, Fallback vor erster `EnvironmentVariablesConfigurationSource`) korrekt gegen User-Secrets-Problem; `Content Update` statt `Include` (verifiziert, s. u.); `.gitignore`-Eintrag | `LocalConfigurationTests` (5 Tests inkl. Positions-Guard und beiden Vorrangnachweisen) | Abgedeckt |
| `ProtectedFiles` nicht DB-mutiert (`UpdateSettingsService`) | Verifiziert: `UpdateSettingsService.cs:167–181` mutiert `Enabled`, `SourceCheck`, `AllowPrereleaseUpdates`, `EnableAutomatic*`, `ServiceName`, `Source` — `ProtectedFiles` unberührt | Regressionstest `ApplyToRuntimeOptions_LeavesProtectedFilesUnchanged` | Abgedeckt |
| Ablage-Entscheid (Konfigurationsbindung statt Fluent) | `AutoUpdate:ProtectedFiles` in `appsettings.json`; kein Fluent-Aufruf — all-or-nothing-Vorrang verifiziert (`UseAutoUpdate`-Signatur `(IHostApplicationBuilder, Action<AutoUpdateBuilder>)` in XML-Doku bestätigt → `Host.CreateApplicationBuilder`-Testansatz funktioniert) | Bindungstest | Abgedeckt |
| Migrationshinweis (Schutz greift erst ab Folge-Update; einmaliges Nachtragen) | Fester Bestandteil von V5 + Risikoabschnitt; Mechanik verifiziert (Skript der laufenden alten Version) | in allen Doku-Stellen | Abgedeckt |
| `python3`-Bedingung Linux-`Merge` | Als Betriebsvoraussetzung in Doku + Release Notes (beide Sprachen); im Skript verifiziert: `command -v python3` fehlt → Logzeile, Merge übersprungen, Update läuft weiter | — | Abgedeckt |
| V5: alle 12 Doku-Stellen umstellen + **`appsettings.Local.json`-Doku (Runde-2-Befund)** | Eingearbeitet (plan.md Z. 221, tasks.md V5): `SECRETS_MANAGEMENT.md` (~Z. 65 verifiziert — Absatz „Update-Sicherheit der Ablage" existiert), `GUIDE_Installation.md` („Produktive Konfiguration" ~Z. 88–108 verifiziert), `TECH_Auto_Update.md` (Vorrang, `optional`/`reloadOnChange`, Paket-Ausschluss, Wildcard-Seiteneffekt), `help/updates.md` — deckt den Befund vollständig ab (Auffindbarkeit, Ablageort, Vorrang, Verhalten) | `MarkdownLinkCheckerTests` nach V5 | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Update-Installation mit Dateierhalt | kein E2E — Mechanismus läuft im generierten `update.ps1`/`update.sh` bei gestopptem Host, nicht über UI beobachtbar; Primärnachweis Skript-Inhaltstest + Bibliotheksverifikation | Nicht erforderlich mit Begründung (korrekt) |
| Admin-UI (`UpdateSettings`) | `ProtectedFiles` bewusst nicht über UI veränderbar; Regressionstest auf Serviceebene geplant | Nicht erforderlich mit Begründung |
| `appsettings.Local.json` | kein UI-Fluss; reine Konfigurationsquelle | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

**Verifikation des Runde-2-Befunds:**

- **`appsettings.Local.json`-Doku vollständig eingearbeitet:** V5 (plan.md Z. 221) dokumentiert die neue Quelle jetzt an vier Stellen — `SECRETS_MANAGEMENT.md` (update-sichere Secret-/Override-Ablage, `Jwt__*` bleibt Primärempfehlung), `GUIDE_Installation.md` (Dateiname, Content-Root, Zweck als Override-Auffang), `TECH_Auto_Update.md` (Vorrang vs. `appsettings*.json`/User Secrets/Env/Cmdline, `optional`/`reloadOnChange`, Paket-Ausschluss via `CopyToPublishDirectory="Never"` + `.gitignore`, Wildcard-Seiteneffekt) und `docs/help/updates.md`. Damit sind alle Punkte des Runde-2-Befunds abgedeckt; `tasks.md` V5 ist synchron aktualisiert.

**Verifikation der neuen technischen Planelemente:**

- **`Content Update` statt `Include` — korrekt:** `Microsoft.NET.Sdk.StaticWebAssets.StaticAssets.ProjectSystem.props` (SDK 10.0.401, Z. 37/41) definiert `<Content Include="**\*.json" CopyToPublishDirectory="PreserveNewest">` — sobald `appsettings.Local.json` existiert, ist sie glob-erfasst; ein `Include` erzeugt `NETSDK1022`, `Update` setzt nur Metadaten und ist bei fehlender Datei (gitignored → auf CI nicht vorhanden) ein No-op. Beide `CopyTo*`-Metadaten (`Never`) sind sinnvoll: der Glob-Default ist `PreserveNewest`.
- **Wildcard-Seiteneffekt realistisch dokumentiert:** Aus der DLL extrahiert — `Backup-AutoUpdateProtectedFiles` matcht `appsettings*.json` per `Get-ChildItem` im Anwendungsverzeichnis → `appsettings.Local.json` landet bei jedem Update in `Updates/backup/` (inkl. evtl. Secrets). Ein Nuance: `Merge-AutoUpdateJsonContent` schreibt nur bei `$changed = $true` neu — Neuformatierung tritt also nur ein, wenn die lokale Datei mindestens einen gelisteten `JsonKeys`-Pfad enthält (für Override-Dateien mit z. B. `Jwt:Key` wahrscheinlich); unparsebares JSON (Kommentare) → `Write-Warning`, Datei bleibt unverändert. V5 sollte den Effekt als „kann neu formatieren" formulieren.
- **ElementTree-Guard sinnvoll:** `ShippedWebConfig_ContainsAllMergeParents` prüft Elternpfade je `XmlElements` und Elementpfade je `XmlAttributes` gegen die echte `web.config` sowie `XmlElements` auf ElementTree-Kompatibilität (kein `[`, kein `@`). Kleinere Asymmetrie: Das ElementTree-Kriterium gilt technisch auch für den Elementpfad **vor** dem `@` in `XmlAttributes` (Linux-Python splittet an `@` und nutzt denselben `select`-Pfad) — der Guard prüft dort nur Existenz, nicht ElementTree-Kompatibilität. Praktisch irrelevant (`web.config` ist IIS-spezifisch), aber bei künftigen Einträgen mit Prädikaten wäre auch dort ein stiller Skip möglich — optional nachschärfen.
- **Positionsanker und Tests konsistent:** Die Quelleneinfügung („nach letzter `appsettings*.json`-`JsonConfigurationSource`", Fallback vor erster `EnvironmentVariablesConfigurationSource`, sonst anhängen) ist in Designentscheidung (Z. 17), Ablauf (Z. 45) und Umsetzungsschritt 3 (Z. 209) deckungsgleich; die fünf `LocalConfigurationTests` prüfen exakt die behaupteten Vorrangziele (vor User Secrets, nach `appsettings*`, verliert gegen `Jwt__*`).

**Weitere verifizierte Punkte / optionale Hinweise (keine Befunde):**

- **`appsettings.Local.json` kann `ProtectedFiles` deployment-seitig erweitern:** Da die Quelle vor `AddVideoWebPlayerAutoUpdate()` (Program.cs:30) registriert wird, bindet ein `AutoUpdate:ProtectedFiles`-Eintrag mit neuem Index (`ProtectedFiles:2:*`) aus der lokalen Datei update-sicher mit — eine elegantere Alternative zu den in Z. 180/186 genannten Umgebungsvariablen. In V5-Doku erwähnenswert, nicht vorgeschrieben.
- **`MsToolsUpdaterIntegrationTests` enthält kein `IAutoUpdatePlatformResolver`-Testdouble** — das TryAdd-Muster ist in `inventory/tests.md` begründet (TryAdd → vorherige Registrierung gewinnt), nicht wörtlich in der Testklasse. Da der Testdouble nur „ggf." geplant ist, unkritisch.
- **`{AppName}.settings*.json`-Quellen** (net10-Default, nach `appsettings.{Env}.json`): Die lokale Datei liegt davor und wird von ihnen geschlagen — konsistent zum Vorrangziel; im Repo nicht vergeben, dokumentiert der Vollständigkeit halber.
- **Konsistenz quer durch den Plan:** `tasks.md` (V2–V5) ist synchron zur aktualisierten `plan.md` (Anker-Position, `Content Update`, Wildcard-Seiteneffekt, neue Doku-Stellen). Keine Widersprüche zwischen den sukzessive ergänzten Abschnitten gefunden; „Offene Punkte: Keine" ist plausibel — alle fünf offenen Fragen der Anforderung sind entschieden und begründet.
