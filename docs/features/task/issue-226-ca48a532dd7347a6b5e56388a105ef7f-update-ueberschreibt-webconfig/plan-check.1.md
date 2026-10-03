# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Konfigurierte `web.config`-Elemente (`environmentVariables`, `ipSecurity`, `customHeaders`) und -Attribute überleben Update-Installation | `Merge`-Eintrag `web.config`: `XmlElements` `//aspNetCore/environmentVariables`, `//security/ipSecurity`, `//system.webServer/httpProtocol`; `XmlAttributes` 8 deployment-eigene `aspNetCore`-Attribute ohne `processPath`/`arguments`/`hostingModel` | `ConfigurationBindsProtectedFiles`, `ShippedAppsettingsProtectedFiles_AreValid`, `GeneratedScript_ContainsBackupAndMergeInOrder` | Lücke — kein Test sichert die Elternelement-Abhängigkeit der ausgelieferten `web.config` ab (s. u.) |
| Konfigurierte `appsettings*.json`-Schlüssel überleben; neue Paket-Schlüssel kommen an | `Merge`-Eintrag `appsettings*.json` mit 50 `JsonKeys` (nur deployment-eigene Schlüssel; `AutoUpdate:ProtectedFiles` und Subtrees bewusst ausgenommen) | dieselben drei Tests | Abgedeckt |
| `Preserve`-Dateien bleiben unverändert | Kein `Preserve`-Eintrag — begründet: nur-lokale Dateien sind ohnehin update-sicher; `Preserve` für paketierte Dateien würde Paket-Verbesserungen blockieren | — | Nicht erforderlich mit Begründung |
| Dateien ohne Schutzeintrag werden wie bisher ersetzt | Nur zwei Einträge; Rest unverändert (Bibliotheks-Default) | implizit über Skript-Inhaltstest | Abgedeckt |
| Paket-Verbesserungen gemergter Dateien kommen an | `Merge` statt `Preserve`; `JsonKeys`/`XmlElements`/`XmlAttributes` geschlossene Liste, Paketdatei bleibt maßgeblich | Bindungs-/Validierungstest der ausgelieferten Liste | Abgedeckt |
| Backup-/Merge-Fehler brechen nicht ab; Warnung + Backup liegt bereit | Bibliotheksverhalten (`try/catch` → `Write-Warning`/`update.log`), `Updates/backup/` als manueller Restore-Pfad | kein eigener Test — bibliotheksseitig verifiziert (decompiliert), akzeptabel | Abgedeckt |
| Leere/fehlende Schutzliste = bisheriges Verhalten | Additive API (`HasProtectedFiles` false → unverändertes Skript) | „Keine bestehenden Tests brechen" — plausibel, da kein Test `new AutoUpdateScriptGenerator` nutzt (ctor-Delta ist kompilierbar sicher) | Abgedeckt |
| Windows beide Varianten + Linux | Bibliothek deckt alle drei Skript-Templates ab; Pfade in ElementTree-Teilmenge (Linux-kompatibel) | Skript-Inhaltstest plattformunabhängig via `IAutoUpdatePlatformResolver`-Testdouble | Abgedeckt |
| V1: `PackageReference` beider Projekte → `0.11.0-rc.1`, nupkg-Tausch, Lizenz, Debug+Release, CI | Beide `PackageReference` (Z. 53 / Z. 22 verifiziert, aktuell `0.10.4-rc.1`), altes nupkg löschen, neues committen; MIT-Lizenz im nuspec verifiziert; lokaler Feed `lib/packages` unverändert | Build-Nachweis Debug + Release (DoD) | Abgedeckt |
| V3: `appsettings.Local.json` Neubewertung und ggf. Umsetzung | Umsetzung entschieden, Mehrwert sauber begründet: `LocalConfigurationExtensions`, `Program.cs`, `CopyToPublishDirectory="Never"`, `.gitignore` | `LocalConfigurationTests` (3 Tests) | Lücke — Positionsalgorithmus widerspricht in Development dem eigenen Vorrangziel (s. u.) |
| `ProtectedFiles` nicht DB-mutiert (`UpdateSettingsService`) | verifiziert (Z. 167–181 mutiert es nicht); keine Admin-UI-Änderbarkeit beabsichtigt | Regressionstest `ApplyToRuntimeOptions_LeavesProtectedFilesUnchanged` in `UpdateSettingsServiceTests` | Abgedeckt |
| Ablage-Entscheid (Konfigurationsbindung statt Fluent) | `AutoUpdate:ProtectedFiles` in `appsettings.json`; kein Fluent-Aufruf — all-or-nothing-Vorrang verifiziert (`ReapplyExplicitValues`), Projektkonvention | Bindungstest | Abgedeckt |
| Migrationshinweis (Schutz greift erst ab Folge-Update; einmaliges Nachtragen) | fester Bestandteil von V5 + Risikoabschnitt | Dokumentiert in allen 12 Doku-Stellen | Abgedeckt |
| `python3`-Bedingung Linux-`Merge` | als Betriebsvoraussetzung in Doku + Release Notes (beide Sprachen), Anwender-Entscheid eingearbeitet | — | Abgedeckt |
| V5: alle 12 Doku-Stellen umstellen | `TECH_Auto_Update.md` (Tabelle, Warnblock Z. 94–101, Backup-Absatz ~126, Elternknoten-Hinweis, `Updates/backup/`-Retention), `GUIDE_Installation.md` ~108, `SECRETS_MANAGEMENT.md` ~65, `help/updates.md` 38/71/77, `help/backups.md` ~61/~96, `RELEASE_NOTES.md` 5 EN + 74 DE | `MarkdownLinkCheckerTests` nach V5 | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Test, der die Elternelement-Abhängigkeit der ausgelieferten `web.config` absichert: Der XML-Merge hängt fehlende Elemente nur an **existierende** Elternknoten an. Der Plan nennt selbst das Risiko, dass ein künftiges Entfernen von `//security` (aktuell über `requestFiltering` garantiert) den `ipSecurity`-Schutz **still** verlieren lässt — und deckt es nur per Dokumentationshinweis ab. Ergänzend zu `ShippedAppsettingsProtectedFiles_AreValid` fehlt ein Test, der die ausgelieferte `VideoWebPlayer/web.config` parst und für jeden `XmlElements`-/`XmlAttributes`-Eintrag der ausgelieferten Liste prüft, dass der Elternpfad existiert (`//system.webServer`, `//security`, `//aspNetCore`). Bezug: Akzeptanzkriterium „konfigurierte `web.config`-Elemente überleben die Installation" — ohne diesen Guard kann eine Repo-Änderung den Schutz ohne Testfehler und ohne Laufzeitwarnung deaktivieren.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Update-Installation mit Dateierhalt | kein E2E — der Mechanismus läuft im generierten `update.ps1`/`update.sh` bei gestopptem Host; nicht über UI/Nutzeraktion beobachtbar. Primärnachweis: Skript-Inhaltstest + Bibliotheksverifikation | Nicht erforderlich mit Begründung (korrekt) |
| Admin-UI (`UpdateSettings`) | `ProtectedFiles` ist bewusst nicht über die UI veränderbar; Regressionstest auf Serviceebene geplant | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

- [ ] `LocalConfigurationExtensions`-Positionsalgorithmus widerspricht dem eigenen Vorrangziel: Der Plan beschreibt das Einfügen „direkt nach der letzten `JsonConfigurationSource`" mit der Behauptung, die letzte JSON-Quelle sei `appsettings.{Environment}.json`. In Development registriert `CreateBuilder` zusätzlich User Secrets als `JsonConfigurationSource` **nach** `appsettings.Development.json` — die Quelle würde dann **nach** User Secrets landen und diese schlagen, statt wie vorgesehen „vor Umgebungsvariablen, User Secrets und Kommandozeile". Der Plan sollte den Anker konkretisieren: Einfügen nach der letzten `JsonConfigurationSource`, deren `Path` auf `appsettings.*.json` matcht (nicht letzte JSON-Quelle generell).
- [ ] Regression-Guard für die `web.config`-Elternelemente als Planpunkt ergänzen (siehe fehlende Testanforderung oben) — alternativ bewusst als akzeptiertes Risiko dokumentieren statt nur als Doku-Hinweis.

## Hinweise

- **XPath-Listen verifiziert korrekt:** `//aspNetCore` (`web.config` Z. 15), `//security` (Z. 4 via `requestFiltering`) und `//system.webServer` (Z. 3) existieren in der Paket-`web.config`; `//httpProtocol` fehlt — die Wahl `//system.webServer/httpProtocol` ist korrekt und deckt `customHeaders` (plus `responseHeaders`/`redirectHeaders`) sogar vollständiger ab als das engere Anwendungsbeispiel `//httpProtocol/customHeaders`. Alle Pfade sind ElementTree-kompatibel (keine Attributprädikate) → Linux-tauglich.
- **`XmlAttributes` verifiziert:** Format `//aspNetCore@{attr}` korrekt; `aspNetCore` existiert in jeder deployment-seitigen `web.config` (ANCM-Pflicht); nicht vorhandene Attribute sind Merge-No-ops → großzügige Liste sicher. `processPath`/`arguments`/`hostingModel` korrekt ausgenommen (Publish-Transform/`outofprocess`-Zwang — verifiziert `web.config` Z. 14–15, csproj `AspNetCoreHostingModel`).
- **`JsonKeys` gegen echten Config-Gebrauch verifiziert:** `Jwt:ApiToken(:Web/:Maui)` (`ServiceCollectionExtensions.cs:50–51`), `Host:Address`/`Host:Port` (`Program.cs:52`), `Pairing:*` (`PairingService.cs:158–159`, `PairingBootstrapService.cs:167–183`), `Auth:RefreshTokenTtlDays` (`RefreshTokenService.cs:130`), `AutoUpdate:GitHubToken` (`VideoWebPlayerUpdateSourceFactory.cs:13`), `Kestrel:*` (`appsettings.Production.json`), alle `AutoUpdate:*`-Options existieren im nupkg. Keine Array-Pfade in der Liste — `Serilog:WriteTo:*` korrekt ausgenommen (Merge navigiert Objekte, keine Arrays).
- **Fluent-Verzicht konsistent begründet:** `ReapplyExplicitValues` ersetzt die gebundene Liste all-or-nothing — verifiziert am decompilierten `UseAutoUpdate`-Ablauf und in der XML-Doku. Projektkonvention trifft zu (Lambda enthält nur Quelle + Unit-Name, `AutoUpdateExtensions.cs:29–35`).
- **nupkg-API verifiziert** (`lib/packages/msTools.Updater.0.11.0-rc.1.nupkg`, entpackt): `AutoUpdateProtectedFile`, `AutoUpdateProtectedFileStrategy`, `AutoUpdateOptions.ProtectedFiles`, `AutoUpdateOptionsValidator.Validate` (public), `IAutoUpdateScriptGenerator.GenerateAsync(descriptor, zipPath, target, ct)`, `AutoUpdateInstallationTarget`, `IAutoUpdatePlatformResolver` — alle referenzierten Typen sind public und wie geplant nutzbar. Lizenz MIT.
- **Konstruktor-Delta unkritisch:** `AutoUpdateScriptGenerator`-ctor hat neuen `AutoUpdateOptions`-Parameter; kein Test instanziiert die Klasse direkt (DI-Auflösung) — kompilierbar sicher.
- **`CopyToOutputDirectory="Never"`** für `appsettings.Local.json` ist mit dem Ziel „nie paketieren" konsistent; Nebeneffekt: ein direkt aus `bin/` gestartetes Binary sieht die Datei nicht, `dotnet run` aus dem Projektverzeichnis schon — akzeptabel, ggf. in `TECH_Auto_Update.md`/`SECRETS_MANAGEMENT.md` kurz erwähnen.
- **`Content`-Item:** Bei der Umsetzung `Content Update` statt `Content Include` verwenden — die Datei existiert i. d. R. nicht im Repo und wird vom SDK-Content-Glob bereits erfasst; `Include` auf nicht vorhandene Dateien ist die unüblichere Variante.
- **`Updates/backup/` ohne Retention** ist im Risikoabschnitt benannt und für V5-Doku eingeplant — vollständig.
