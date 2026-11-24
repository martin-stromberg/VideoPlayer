# Umsetzungsplan: Wöchentliche NuGet-Paketprüfung (2026-W48, Issue #380)

## Übersicht

Prüfzyklus ohne Feature-Entwicklung: Alle 33 direkt referenzierten NuGet-Pakete der
`VideoPlayer.sln` sind bewertet (Bestand und Update-Lage: `inventory/packages.md`,
Stand 2026-11-23; Inline-Linien zusätzlich gegen den nuget.org-Flatcontainer
verifiziert). Die als niedriges bzw. mittleres Risiko eingestuften Updates werden
direkt umgesetzt und in **einem** Sammel-PR gegen `staging` eingereicht — darunter
der Microsoft-10.x-Familienpatch auf **10.0.15**, den `dotnet list --outdated`
hinter dem 11.0.0-Major-Angebot verschluckt (getrennter Patch/Minor/Major-Ausweis
des Issues). Aufgeschoben und dokumentiert werden die Hoch-Risiko-Blöcke
(Microsoft.*/Serilog.* →11.x, xunit.v3-Ökosystem →4.x). Sicherheitslage ist sauber:
kein Schwachstellen- oder Deprecation-Befund, Dependabot-Alerts `[]`,
`security-scan.yml`-Lauf 45192953736 vom 2026-11-23 erfolgreich. Keine Änderungen an
Anwendungscode, UI, Datenmodell oder Workflows.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Prüfzyklus 2026-W48 (Issue #380): alle `PackageReference` der Projekte `VideoWebPlayer` (21), `VideoWebPlayer.Client` (3), `VideoWebPlayer.Tests` (13) und `tools/MarkdownLinkCheck.Tests` (3). `MarkdownLinkCheck` und `SecretScan` haben keine Paketreferenzen. Mehrfachreferenzen (`Microsoft.EntityFrameworkCore.Sqlite`, `msTools.Updater`, `SQLitePCLRaw.lib.e_sqlite3`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`) sind Einheiten mit Versionsgleichheits-Gebot. |
| Auslösendes Ereignis | Issue #380 aus `review-nuget-weekly.yml` (2026-11-23) + Bearbeitungskommentar. Zyklus endet mit dem Ergebnis-PR gegen `staging` (Fallback: dokumentierter „kein Handlungsbedarf“-Issue-Kommentar). |
| Erwartete Wirkung | Ein PR gegen `staging` mit allen niedrigen/mittleren Updates; PR-Body enthält die Ergebnistabelle aller 33 Pakete (Paket \| aktuell → Ziel \| Kategorie \| Schwachstelle? \| Risiko \| Maßnahme) inkl. aufgeschobener Empfehlungen mit Grund, der Inline-Patch-Befunde der gehaltenen Linien (getrennt vom Major-Angebot), des Pflegestatus-/Nachfolge-Befunds je Paket ohne Update und der Referenz auf den Security-Scan-Lauf. |
| Umgang mit fehlenden Objekten | Schlägt Restore/Build/Testsuite nach einem Update fehl: genau dieses Update zurücknehmen und im PR-Body als aufgeschoben dokumentieren — kein PR mit bekanntem Bruch. Kein PR ohne Inhalt: Falls sämtliche Updates zurückgenommen werden müssten, Ergebnistabelle als Kommentar auf Issue #380 statt leerem PR. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein PR gegen `main` — Zielbranch ist fest `staging` | Direkt aus dem Issue; technisch zusätzlich durch `verify-pr-source.yml` erzwungen | bestätigt (Issue-Vorgabe) |
| 2 | Keine Umsetzung von Empfehlungen mit Risiko „hoch“ — Microsoft.*/Serilog.* 11.x und xunit.v3-Ökosystem 4.x werden nur dokumentiert/aufgeschoben. Der Ausschluss betrifft den **Major**; Inline-Patches/-Minors innerhalb der gehaltenen Linien (z. B. Microsoft-10.0.x, xunit-3.x) sind eigenständige Empfehlungen und werden separat bewertet | Direkt aus dem Issue; Hoch-Einstufung begründet unten; Inline-Sicht verlangt der getrennte Patch/Minor/Major-Ausweis des Issues | bestätigt (Issue-Vorgabe) |
| 3 | Transitive Pakete werden nicht angefasst — kein Schwachstellen-Befund liegt vor | Direkt aus dem Issue; `dotnet list package --vulnerable --include-transitive` und Dependabot-Alerts ohne Befund | bestätigt (Issue-Vorgabe) |
| 4 | Keine Änderung an `review-nuget-weekly.yml`, `security-scan.yml`; kein Dependabot-/Renovate-Setup | Direkt aus dem Issue | bestätigt (Issue-Vorgabe) |
| 5 | Keine fachlichen Codeänderungen — Anpassungen nur, wenn ein umgesetztes Update sie zwingend erfordert; ein solches Update wird stattdessen zurückgenommen und als hoch aufgeschoben | Direkt aus dem Issue | bestätigt (Issue-Vorgabe) |
| 6 | Keine TFM-/SDK-Änderung (`net10.0`, CI `10.0.x`) — sie wäre die Voraussetzung für die 11.x-Linie und ist Teil der Hoch-Risiko-Entscheidung | Folge der Ausschlüsse 2 und 5 | bestätigt (Issue-Vorgabe) |
| 7 | Kein UI-/Usability-Review (Lifecycle-Schritt 8) — keine Oberflächenänderung | Keine UI-Änderung geplant | bestätigt (logische Folge) |
| 8 | Keine Paket-Ersetzung oder -Entfernung — ein ungepflegtes Paket bleibt referenziert; ein empfohlenes Nachfolgepaket wird nur in der Tabelle benannt, nicht getauscht | Paketwechsel wäre eine fachliche Code-/Funktionsänderung (Ausschluss 5) und kein „Update“ im Sinne des Issues | ergänzt (operationalisiert den Pflegestatus-/Nachfolge-Prüfpunkt des Issues) |

## Bewertung und Entscheidung je Paket

Einstufungsregeln aus `requirement.md`: Patch ohne API-Änderung = niedrig;
Minor-/Test-Tooling-Updates und Majors mit dokumentierter Rückwärtskompatibilität = mittel;
Majors mit Breaking Changes, Code-Anpassungsbedarf oder Versionsmodell-Wechsel = hoch.
Prerelease-Linien mindestens mittel; eigene Versionsmodelle (SSH.NET, SkiaSharp) anhand
der Release Notes bewertet. Release Notes wurden für die entscheidungstragenden Pakete
gelesen (Belege in der Begründungsspalte); der Quercheck der übrigen umzusetzenden
Pakete (Moq, Microsoft.NET.Test.Sdk, coverlet.collector, Microsoft.Playwright, bunit)
ist in Umsetzungsschritt 6 eingeplant.

### Getrennter Ausweis: Inline-Patches der gehaltenen Linien

`dotnet list package --outdated` meldet je Paket nur die **absolut** neueste Version.
Bei Paketen mit aufgeschobenem Major verschluckt das einen Patch/Minor innerhalb der
gehaltenen Linie — der Issue verlangt den Ausweis aber getrennt nach Patch/Minor/Major,
und der Präzedenzfall 10.0.10→10.0.12 zeigt, dass solche Inline-Patches hier üblich
sind. Deshalb wurde für jedes Gruppe-C-Paket die Versionsliste der eigenen
Hauptversion geprüft (nuget.org-Flatcontainer
`https://api.nuget.org/v3-flatcontainer/{paket-id}/index.json` — Re-Verifikation in
Umsetzungsschritt 1):

| Gehaltene Linie | Aktuell | Höchste stabile Version der Linie | Befund |
|-----------------|---------|-----------------------------------|--------|
| Microsoft.*-Familie (AspNetCore/EFCore/Extensions/Mvc.Testing/InMemory — 11 Pakete, 13 Referenzen) | 10.0.12 | **10.0.15** | Inline-Patch vorhanden → umsetzen (Gruppe A); Major → aufschieben (Gruppe C) |
| Serilog.AspNetCore / Serilog.Settings.Configuration | 10.0.0 / 10.0.1 | 10.0.0 / 10.0.1 (darüber nur dev-Prereleases) | kein Inline-Update; Major → aufschieben |
| xunit.v3 / xunit.runner.visualstudio | 3.2.2 / 3.1.5 | 3.2.2 / 3.1.5 | kein Inline-Update; Major → aufschieben |

Bewertung eines Inline-Fundes nach denselben Regeln: Patch = niedrig → umsetzen;
Minor = mittel, sofern die Release Notes keinen TFM-/Bruchhinweis liefern (bei der
Microsoft-Familie: 10.x zielt weiterhin `net10.0`, kein SDK-/TFM-Wechsel), sonst
hoch → aufschieben. Versionsgleichheits-Gebote gelten unverändert: Die Microsoft-
Familie und das xunit-Paar werden nur als ganze Einheiten angehoben, kein Teilbump.
In der Ergebnistabelle steht der umgesetzte Inline-Patch getrennt vom aufgeschobenen
Major-Angebot.

### Gruppe A — Umzusetzen, Risiko niedrig

| Paket | Aktuell → Ziel | Kategorie | Begründung |
|-------|----------------|-----------|------------|
| SharpCompress | 0.50.5 → 0.50.11 | Patch | Patchfolge innerhalb 0.50.x ohne API-Bruch. Relevanz erhöht: Release Notes zu 0.50.9 nennen „a bunch of CVE fixes" (github.com/adamhathcock/sharpcompress/releases) — der Advisory-Scan meldet zwar keinen Befund, die Fixes betreffen die Archiv-Behandlung und SharpCompress verarbeitet im Produktivcode heruntergeladene ffmpeg-Archive. |
| SSH.NET | 2026.0.0 → 2026.0.1 | Patch im Jahresmodell | Release Notes 2026.0.1 (sshnet/SSH.NET): „Bugfix/hardening release — Breaking changes: None known". Enthält Security-Hardening (ScpClient-Härtung GHSA-h4g8-5qcx-7ccw, Validierung GHSA-fpgr-j7rx-jw2p). Patch innerhalb derselben Jahres-Linie, kein Versionsmodell-Wechsel. |
| Microsoft.*-Familie: AspNetCore.Authentication.JwtBearer, .Diagnostics.EntityFrameworkCore, .Identity.EntityFrameworkCore, EntityFrameworkCore.Design/.Sqlite/.Tools, Extensions.Logging.Abstractions, .Http, .Configuration.Abstractions, AspNetCore.Mvc.Testing, EntityFrameworkCore.InMemory (13 Referenzen über `VideoWebPlayer`, `.Client`, `.Tests`) | 10.0.12 → 10.0.15 | Patch | Inline-Patch, den `--outdated` hinter dem 11.0.0-Major-Angebot verschluckt — Flatcontainer-Verifikation zeigt 10.0.15 als höchste stabile 10.x für **alle** elf Pakete. Kumulative .NET-10-Servicing-Patches ohne API-Bruch; TFM bleibt `net10.0`, CI-SDK `10.0.x` unverändert. Familie = gekoppelte Einheit (Versionsgleichheits-Gebot): alle 13 Referenzen gehen gemeinsam auf dieselbe 10.0.x, kein Teilbump. Re-Verifikation in Schritt 1: Gilt dann eine höhere stabile 10.0.x, ist sie das Ziel; schlägt sie fehl, entfällt der Block und bleibt 10.0.12. |

### Gruppe B — Umzusetzen, Risiko mittel

| Paket | Aktuell → Ziel | Kategorie | Begründung |
|-------|----------------|-----------|------------|
| QRCoder | 1.8.0 → 1.9.0 | Minor | Release v1.9.0 (Shane32/QRCoder): Optimierungen und kleine Fixes (Raw-Data-Handling, Allokationen, SwissQR-Kulturformatierung), keine Breaking Changes; TFM net5.0+/netstandard1.3 unverändert. |
| System.IdentityModel.Tokens.Jwt | 8.22.0 → 8.23.0 | Minor | IdentityModel-8.x-Linie (unabhängig vom AspNetCore-10.x-Familienzug). Release 8.23.0: additive Features (ML-DSA-Schlüsselauflösung, WsTrust-Reinstatement), Bugfixes, keine Breaking Changes; SHR-p-claim-Änderung ist opt-in. |
| SkiaSharp | 4.153.1 → 4.157.0 | Milestone-Update, eigenes Modell | Issue-Regel: 4.x-Linie mindestens mittel. Faktisch ist 4.157.0 ein **stabiler** Milestone-Cut (GitHub-Release „Stable", 2026-11-18), kein Prerelease. Skia m153→m157, managed APIs unverändert; einziger Breaking-Change-Hinweis betrifft Graphite-Metal-Minimum iOS/tvOS 15 — für diese Server-Anwendung (win-x64/linux-x64) irrelevant. Versionspaarung mit NativeAssets zwingend. |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.153.1 → 4.157.0 | gekoppelt | Muss exakt versionsgleich mit `SkiaSharp` stehen (Native-Asset-Paarung). |
| Microsoft.NET.Test.Sdk | 18.10.0 → 18.12.0 | Minor | Test-Tooling; in `VideoWebPlayer.Tests` **und** `tools/MarkdownLinkCheck.Tests` versionsgleich zu heben. |
| Microsoft.Playwright | 1.62.0 → 1.65.1 | Minor | Test-Tooling. Die CI installiert Chromium über `playwright.ps1 install --with-deps chromium` aus der jeweils referenzierten Paketversion — Browser-Binary und Paket bleiben damit automatisch konsistent. Lokal muss nach dem Bump `playwright install` (bzw. der gewohnte Install-Mechanismus) laufen, bevor OsInterface-Tests starten. |
| Moq | 4.20.72 → 4.21.0 | Minor | Test-Tooling. |
| bunit | 2.11.3 → 2.15.2 | Minor | Test-Tooling; zieht AngleSharp 1.10.1/AngleSharp.Css 1.4.1 transitiv nach (behebt u. a. TypeLoadException-Fixes). AngleSharp-Update ist transitiv und unvermeidbar — zulässig, da es vom direkten bunit-Update kommt, nicht separat angefasst wird. |
| coverlet.collector | 10.0.1 → 10.1.1 | Minor | Test-Tooling (`PrivateAssets=all`), speist das 70-%-Coverage-Gate; Grün-Nachweis der CI reicht hier als Funktionsprobe. |

### Gruppe C — Aufgeschoben, Risiko hoch

| Paket | Aktuell → angeboten | Grund der Einstufung |
|-------|---------------------|----------------------|
| Microsoft.AspNetCore.Authentication.JwtBearer, .Diagnostics.EntityFrameworkCore, .Identity.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design/.Sqlite/.Tools, Microsoft.Extensions.Logging.Abstractions, .Http, .Configuration.Abstractions, Microsoft.AspNetCore.Mvc.Testing, Microsoft.EntityFrameworkCore.InMemory (13 Referenzen über 3 Projekte) | 10.0.15 → 11.0.0 | net11-Familienlinie: Der Sprung setzt `net11.0`-TFM aller Projekte und SDK `11.0.x` in allen CI-Workflows voraus — ein Plattform-Migrationsvorhaben, kein Paket-Bump (AGENTS.md §7: neue Hauptversionen nur nach Lizenz-/Release-Build-/CI-Skript-Prüfung; hier kommt zusätzlich der TFM-Wechsel hinzu). Versionsgleichheits-Gebot: kein Teilbump. Der Aufschub betrifft nur den Major — der Inline-Patch auf 10.0.15 wird in Gruppe A umgesetzt; `--outdated` zeigt danach weiterhin 11.0.0. Wiederkehr automatisch über die nächsten Wochen-Reviews bzw. ein .NET-11-Migrationsthema. |
| Serilog.AspNetCore, Serilog.Settings.Configuration | 10.0.0/10.0.1 → 11.0.0 | Serilog-Hauptversion 11 folgt dem .NET-11-Release-Zug; gleiche Begründung wie der Microsoft-Block — gekoppelt aufschieben. Inline-Check: keine stabile 10.x-Nachfolgeversion (darüber nur dev-Prereleases) — nichts zusätzlich umzusetzen. |
| xunit.v3, xunit.runner.visualstudio (je in `VideoWebPlayer.Tests` + `tools/MarkdownLinkCheck.Tests`) | 3.2.2/3.1.5 → 4.0.3/4.0.1 | Major-Sprung des Testframeworks inkl. Runner — Breaking Changes erwartet, eigene Migrationsbewertung nötig. Versionsgleichheit über beide Testprojekte: kein Teilbump. Inline-Check: 3.2.2/3.1.5 sind die höchsten stabilen 3.x-Stände — kein Inline-Patch vorhanden. |

### Gruppe D — Keine Maßnahme

| Paket | Version | Grund |
|-------|---------|-------|
| msTools.Backup | 1.1.0-RC.4 | Lokaler Feed `lib/packages` enthält kein neueres nupkg; Update-Weg ist manuelle nupkg-Ablage — ohne neue Datei keine Aktion. Bei der Re-Verifikation (Schritt 1) Upstream-Angebot und Pflegestatus prüfen (letztes Release, Repo-Status); Befund geht in die Maßnahme-Spalte („aktuell für diesen Feed" bzw. Pflegevermerk). |
| msTools.Updater | 0.11.0-rc.6 | Wie `msTools.Backup`; beide Projekte bleiben versionsgleich. |
| Makaretu.Dns.Multicast.New | 0.38.0 | Kein Update verfügbar (0.38.0 = höchste Version). Nachfolge-Frage bereits beantwortet: Das Paket ist selbst der aktiv gepflegte Nachfolge-Fork von `Makaretu.Dns.Multicast`/`richardschneider/net-mdns` (README: „to update dependencies and continue development") — kein Ersatz nötig. Letztes Release-Datum/Repo-Aktivität in Schritt 1 verifizieren. |
| Nito.AsyncEx | 5.1.2 | Kein Update verfügbar (5.1.2 = höchste). Reife, API-stabile Standardbibliothek; kein dokumentiertes Nachfolgepaket. Letztes Release-Datum + Repo-Aktivität in Schritt 1 als Pflegestatus-Befund festhalten. |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Kein stabiler Nachfolger; neuer ist nur das Prerelease 3.53.4.1-pre1 — ein Wechsel auf die Prerelease-Linie wäre eine eigene risikobewertete Entscheidung, die Stable-Linie bleibt. Mehrfachreferenz bleibt unangetastet; Pflegestatus-Befund in Schritt 1. |
| Serilog.Sinks.Console / Serilog.Sinks.File | 6.1.1 / 7.0.0 | Kein stabiler Nachfolger (neuer sind nur dev-Prereleases wie 6.1.1-dev-*, 7.0.1-dev-*, 8.0.0-dev-*). Aktiv gepflegte Serilog-Org — kein Ersatz nötig. |
| Transitive Pakete | – | Kein Schwachstellen-Befund → nicht anfassen (Scope-Ausschluss 3). |

## Designentscheidungen

| Bereich | Entscheidung | Begründung |
|---------|--------------|------------|
| Gruppierung | **Ein Sammel-PR** gegen `staging` für alle Updates der Gruppen A+B | Genau die Issue-Anforderung („Umsetzung aller Empfehlungen mit niedrigem bis mittlerem Risiko in einem Pull Request"). Bisectbarkeit bleibt über die Commit-Granularität erhalten — ein Commit je Paket bzw. je gekoppelter Einheit. |
| Dokumentationsort der Aufschübe | **Im PR-Body** (Ergebnistabelle mit Grund je aufgeschobenem Paket); **kein** separates Folge-Issue | Auftragsvorgabe: kein Folge-Issue, sofern ein PR entsteht — und ein PR entsteht. Der wöchentliche Workflow garantiert ohnehin die Wiederkehr der 11.x-/xunit-4.x-Blöcke in den Folgezyklen. |
| Referenz auf `security-scan.yml` | Fußnote im PR-Body: Verweis auf Run **45192953736** (2026-11-23, success) plus Angabe, dass `dotnet list package --vulnerable --include-transitive` lokal und die Dependabot-Alerts leer sind | Der Scan ist eine Ja/Nein-Aussage; ein Run-Link genügt als Beleg, Artefakt-Anhänge sind unnötig (requirement.md: „referenzieren statt doppelt analysieren"). |
| „Kein Handlungsbedarf"-Fallback | Entfällt — es gibt Handlungsbedarf. Falls wider Erwarten alle Updates zurückgenommen werden müssten: Ergebnistabelle als Kommentar auf Issue #380, kein leerer PR | Entscheidung aus dem offenen Punkt der Anforderung; deckt sich mit „kein leerer PR". |
| Commit-Granularität | Ein Commit je Paket; gekoppelte Einheiten gemeinsam: (a) SkiaSharp + NativeAssets, (b) Test.Sdk über beide Testprojekte, (c) Microsoft-10.x-Familie über alle drei Projekte (13 Referenzen — Versionsgleichheits-Gebot, kein Teilbump) | Bisectbarkeit bei Regressionen ohne PR-Inflation; entspricht dem Commit-Präfix-Muster (`step:`/`chore:`-artige deutsche Nachrichten, Autor über `GIT_AUTHOR_NAME`/`GIT_COMMITTER_NAME` = „Devin (Softwareschmiede Bot)", `Co-Authored-By`-Zeile bleibt). |
| Inline-Check der aufgeschobenen Linien | Je Gruppe-C-Paket die Versionsliste der gehaltenen Hauptversion prüfen — nuget.org-Flatcontainer `https://api.nuget.org/v3-flatcontainer/{paket-id}/index.json` (kleingeschrieben) oder `dotnet package search <id> --exact-match`; ein gefundener Inline-Patch/-Minor wird getrennt vom Major-Angebot ausgewiesen | `--outdated` zeigt nur das absolute Maximum — ohne Linien-Check ginge der 10.0.15-Patch verloren; der Issue verlangt den getrennten Patch/Minor/Major-Ausweis. |
| Pflegestatus/Nachfolgepakete | Je Paket ohne Update (Gruppe D) in Schritt 1 dokumentieren: letztes Release-Datum (nuget.org-Versionsliste), Upstream-Repo-Status (archiviert/inaktiv), Deprecation-Flag inkl. Alternate-Package-Metadaten, README-/Release-Hinweise auf empfohlene Nachfolger. Kriterium „nicht mehr gepflegt": kein Release seit > 24 Monaten oder archiviertes Repo. Befund in der Maßnahme-Spalte; kein Pakettausch (Ausschluss 8) | Issue-Prüfpunkte „veraltete/nicht mehr gepflegte Pakete" und „empfohlene Nachfolgepakete"; `dotnet list --deprecated` allein deckt nur das NuGet-Flag ab, nicht Repo-Verwaistheit oder Ersatzempfehlungen. |
| Closing-Keyword | PR-Body enthält `Closes #380` | Globale Commit-/PR-Regel: Issue schließt erst bei Promotion staging→main — gewünscht. |
| Versionswahl bei Umsetzung | Es gilt die zum Umsetzungszeitpunkt neueste **stabile** Version derselben Linie, sofern sie die Einstufung nicht ändert; ändert sie sie (z. B. neuer Major), wird neu bewertet | Zwischen Bestandsaufnahme (2026-11-23) und Umsetzung können Folgeversionen erscheinen; Re-Verifikation ist Schritt 1. |
| msTools-Pakete | Kein Bump — nur wenn ein neueres nupkg in `lib/packages` läge | Update-Verfahren (nupkg-Ablage + beidseitiger csproj-Bump, `docs/Anforderung_msTools_Updater_Dateierhalt.md` Z. 115) setzt ein geliefertes Paket voraus; nichts liegt vor. |
| SkiaSharp-Zielversion | 4.157.0 (stabiler Cut), **nicht** die neueren 4.158.x-preview/rc- oder 4.159.x-preview-Versionen | `--outdated` schlägt 4.157.0 vor; Prereleases jenseits davon wären die „Prerelease-Linie" der Issue-Regel und fielen in die Bewertungspflicht erneut — bewusst nur der stabile Milestone. |

## Umsetzungsreihenfolge

1. **Update-Lage re-verifizieren**
   - `dotnet list VideoPlayer.sln package --outdated` (ohne `--include-transitive` — bekannte Tooling-Quirk), `--vulnerable --include-transitive` und `--deprecated` erneut laufen lassen; `gh api repos/{owner}/{repo}/dependabot/alerts` prüfen.
   - **Inline-Patches der aufgeschobenen Linien prüfen** — `--outdated` zeigt nur das absolute Maximum und verschluckt Patches innerhalb der gehaltenen Linie: Für jedes Gruppe-C-Paket die Versionsliste derselben Hauptversion gegen nuget.org prüfen (Flatcontainer `https://api.nuget.org/v3-flatcontainer/{paket-id}/index.json` oder `dotnet package search <id> --exact-match`). Planstand (verifiziert): Microsoft-10.x-Familie → 10.0.15 (→ Schritt 3); Serilog.AspNetCore/Settings.Configuration → kein stabiler 10.x-Nachfolger; xunit.v3 3.2.2 / runner.visualstudio 3.1.5 → höchste 3.x. Bewertung wie im Abschnitt „Getrennter Ausweis": Patch → umsetzen, Minor → mittel sofern kein TFM-/Bruchhinweis, sonst aufschieben.
   - **Pflegestatus-/Nachfolge-Check für Pakete ohne Update** (Gruppe D; Issue-Prüfpunkte „veraltete/nicht mehr gepflegte Pakete" und „empfohlene Nachfolgepakete"): je Paket letztes Release-Datum auf nuget.org, Upstream-Repo-Status (archiviert/inaktiv), Deprecation-Flag samt Alternate-Metadaten, README-Hinweise auf Nachfolger. Kriterium und Befund-Behandlung: siehe Designentscheidung „Pflegestatus/Nachfolgepakete" — Ergebnis geht in die Maßnahme-Spalte, kein Pakettausch.
   - `lib/packages` auf etwaig neu abgelegte msTools-nupkgs prüfen; kurzer Upstream-Check msTools.Backup/Updater.
   - Ergebnis mit `inventory/packages.md` abgleichen; bei Abweichungen die Einstufung je Paket neu bewerten (Designentscheidung „Versionswahl").

2. **Gruppe A1 — Produktiv-Patches** (`VideoWebPlayer.csproj`)
   - `SharpCompress` → 0.50.11, `SSH.NET` → 2026.0.1 — je eigener Commit.

3. **Gruppe A2 — Microsoft-10.x-Familienpatch** (`VideoWebPlayer.csproj`, `VideoWebPlayer.Client.csproj`, `VideoWebPlayer.Tests.csproj`)
   - Alle 13 Referenzen der Familie versionsgleich 10.0.12 → 10.0.15 in **einem** Commit (gekoppelte Einheit, kein Teilbump).
   - Nur ausführen, wenn Schritt 1 den Patch bestätigt; gilt dann eine höhere stabile 10.0.x, ist sie das Ziel.

4. **Gruppe B1 — Produktiv-Minors** (`VideoWebPlayer.csproj`)
   - `QRCoder` → 1.9.0, `System.IdentityModel.Tokens.Jwt` → 8.23.0 — je eigener Commit.

5. **Gruppe B2 — SkiaSharp-Paar** (`VideoWebPlayer.csproj`)
   - `SkiaSharp` und `SkiaSharp.NativeAssets.Linux.NoDependencies` versionsgleich → 4.157.0 in einem Commit.
   - Zwischenstände 4.154–4.156 in den Release Notes querlesen (Managed-API-Brüche ausschließen; bereits gelesen: 4.157.0 stabil, kein managed-API-Bruch).

6. **Gruppe B3 — Test-Tooling**
   - `Microsoft.NET.Test.Sdk` → 18.12.0 in `VideoWebPlayer.Tests.csproj` **und** `tools/MarkdownLinkCheck.Tests.csproj` (ein Commit).
   - `Microsoft.Playwright` → 1.65.1, `Moq` → 4.21.0, `bunit` → 2.15.2, `coverlet.collector` → 10.1.1 in `VideoWebPlayer.Tests.csproj` — je eigener Commit.
   - **Release-Notes-Quercheck vor den Bumps** (Issue-Regel „Breaking Changes anhand der Release Notes"): `microsoft/vstest` für Test.Sdk 18.10→18.12; `microsoft/playwright-dotnet` für Playwright 1.62→1.65 (Browser-Baseline beachten); `moq/moq4` für Moq 4.20.72→4.21.0; `bUnit`/`bunit.web` Release Notes für bunit 2.11.3→2.15.2 — v. a. API-Änderungen der Blazor-Testhelfer und die AngleSharp-Hebung; coverlet-Releases für coverlet.collector 10.0.1→10.1.1. Ein Bruchhinweis stuft das betroffene Update neu ein (hoch → aufschieben) statt umzusetzen.

7. **Restore + Builds + Schwachstellen-Re-Scan**
   - `dotnet restore VideoPlayer.sln`, dann `dotnet build VideoPlayer.sln -c Debug` **und** `-c Release` fehlerfrei (Release wegen des ImageSharp-Präzedenzfalls; NU1903-toleriert, sonst keine neuen Warnungen über die dokumentierten 139 hinaus).
   - **Lokaler Re-Scan auf dem neuen Bestand:** `dotnet list VideoPlayer.sln package --vulnerable --include-transitive` nach den Bumps erneut laufen lassen (bunit hebt AngleSharp transitiv an, der Familienpatch weitere transitive Stände — Gate-2-Scan der CI nicht abwarten). Befund → betroffenes Update zurücknehmen und begründen.
   - Bei Bruch: betroffenes Update einzeln zurücknehmen, als „aufgeschoben (Build-/Testbruch)" in der Ergebnistabelle markieren.

8. **Volle Testsuite + Tool-Tests**
   - `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig grün (beide Lanes; einmalige Einzelfehlschläge einzeln mehrfach wiederholen, ehrlich berichten — Konvention `docs/help/stabilitaet/os-interface-tests.md`; kein Deaktivieren).
   - `dotnet test tools/MarkdownLinkCheck.Tests` grün (6 Tests; gehört nicht zum PR-Gate, wird aber durch den Test.Sdk-Bump berührt).
   - Nach dem Playwright-Bump vor den OsInterface-Tests die Browser-Binaries zur neuen Paketversion installieren — Repo-Mechanismus wie in der CI (`pr-staging-ci.yml` Z. 216): `pwsh VideoWebPlayer.Tests/bin/<Konfiguration>/net10.0/playwright.ps1 install chromium` aus dem Build-Ausgabeverzeichnis der genutzten Konfiguration; `--with-deps` ist ein Linux-Detail und lokal unter Windows entbehrlich.

9. **Release Notes**
   - `docs/RELEASE_NOTES.md`: je ein Bullet in „Important Notes Before Update" (EN) und „Wichtige Hinweise vor dem Update" (DE) — Wartungs-Dependency-Update des W48-Zyklus, Nennung der sicherheitsrelevanten Fixes (SharpCompress-CVE-Fixes, SSH.NET-Härtungen), des Microsoft-Familienpatches auf 10.0.15 und der bewusst aufgeschobenen 11.x-/xunit-4.x-Linien mit Begründung (net11-Migration). Präzedenz: msTools-/SkiaSharp-Einträge in derselben Datei.

10. **PR-Body und PR**
    - Ergebnistabelle **aller 33 Pakete** im Issue-Format (Paket \| aktuell → Ziel \| Kategorie \| Schwachstelle? \| Risiko \| Maßnahme), darunter die Aufschub-Gründe der Gruppe C (Major-Angebote getrennt vom umgesetzten Inline-Patch), die „aktuell/keine Maßnahme"-Zeilen der Gruppe D inkl. Pflegestatus-/Nachfolge-Befund, die Scan-Referenz (Run 45192953736, Dependabot `[]`) und `Closes #380`.
    - Vor dem Öffnen prüfen: `gh pr list --base staging --head main` (`no-pending-backmerge`-Gate) **und** `gh pr list --state open` auf einen bereits offenen Paket-/NuGet-PR (Duplikat-Check; der Vorgängerzyklus #358 endete ohne PR — ein offener Rest-PR würde gebündelt oder abgestimmt, nicht gedoppelt). PR gegen `staging` öffnen.
    - Checks fail-fast beobachten (`gh pr checks --watch --fail-fast` bzw. Polling mit Fail-Abbruch): `static-checks`, `build-and-test` inkl. 70-%-Coverage, `no-pending-backmerge` müssen grün sein; `os-interface-tests` ist best-effort. Bei Check-Fail sofort in die Diagnose (`gh run view --job <id> --log-failed`).

## Tests

### Neue Tests

Keine. Paketversionen erzeugen keinen testbaren neuen Codepfad; der Funktionsnachweis
sind Restore-/Build-/Test-Ausführung und die CI-Gates (siehe Akzeptanzszenarien).

### Betroffene bestehende Tests

Die gesamte Suite ist indirekt betroffen (Test-Tooling-Bumps). Besonders zu beobachten:

- bunit 2.15.2 zieht AngleSharp 1.10.1 nach — bunit-Tests müssen ohne TypeLoad-/API-Bruch laufen.
- xunit verbleibt auf 3.x — keine Framework-Migration nötig.
- `FlakyConventionTests` und die Lane-Trennung bleiben unberührt; der eine `[Flaky]`-Test
  (`TvSenderOnAirE2ETests.Admin_TogglesOffAir_…`) folgt der Retry-Konvention.
- Keine EF-/Backup-/Migrations-Tests betroffen: Der EFCore-Familienpatch 10.0.12 →
  10.0.15 ist reine Servicing-Hebung ohne Schema-Änderung (AGENTS.md-§7-Punkte zu
  `VideoWebPlayerBackupData`/Migrationen entfallen sachlich).

### E2E-Tests

Keine neuen E2E-Tests. Begründung und Festlegung der Ersatz-Abnahme: siehe nächster
Abschnitt (Lifecycle-Schritt 10).

## Festlegung für Lifecycle-Schritt 10 (Testausführung/Abnahme)

Es ist **keine UI- oder Anwendungslogik-Änderung** geplant — Screenshots und eine
visuelle Abnahme entfallen. Die reale End-to-End-Abnahme dieses Zyklus ist:

1. `dotnet restore VideoPlayer.sln` fehlerfrei mit dem aktualisierten Paketbestand.
2. `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei.
3. Lokaler Schwachstellen-Re-Scan auf dem neuen Bestand:
   `dotnet list VideoPlayer.sln package --vulnerable --include-transitive` ohne Befund.
4. Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün
   (Einzelfehlschläge-Retry-Konvention beachten und dokumentieren).
5. `dotnet test tools/MarkdownLinkCheck.Tests` grün.
6. PR-Checks des Ergebnis-PRs gegen `staging` grün: `static-checks` (inkl. Gate-2-Security-Scan
   auf dem neuen Paketbestand), `build-and-test` inkl. 70-%-Coverage-Gate,
   `no-pending-backmerge`; `os-interface-tests` best-effort (kein Blocker, aber
   Auffälligkeiten werden im Ergebnis genannt).

Nachweisartefakt: `test-results.md` dokumentiert die sechs Nachweise statt Screenshots.

## Dokumentation

- `docs/RELEASE_NOTES.md` — **wird aktualisiert** (Umsetzungsschritt 9). Dependency-
  Updates werden dort üblicherweise vermerkt, wenn sie betreiber-/sicherheitsrelevant
  sind (Präzedenz: msTools.Updater-rc-Bumps, SkiaSharp-Umstellung); hier zusätzlich
  relevant, weil SharpCompress- und SSH.NET-Updates Security-Fixes enthalten.
- `docs/help/` — **wird nicht angepasst**, begründet: keine Änderung an
  Anwendungsverhalten, Konfiguration oder Bedienoberfläche; die Hilfe-Seiten
  beschreiben Fachlichkeit, nicht Paketstände.
- `README.md` — **wird nicht angepasst**, begründet: keine neue Abhängigkeit, kein
  geändertes Setup- oder Build-Verfahren (lokaler `playwright install`-Hinweis ist
  bestehende Entwicklerkonvention, kein neuer Schritt).
- `docs/TECH_Auto_Update.md` — der dort bekannte veraltete `msTools.Updater`-
  Dateireferenz-Bestand bleibt unangetastet (preexisting, bereits als veraltet
  markiert, kein Scope dieses Zyklus).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Paketbestand aktualisiert | Umsetzungsschritte 2–6 | csproj-Stände laut `inventory/packages.md` | csproj-Diffs + `dotnet list VideoPlayer.sln package --outdated` | Gruppe-A/B-Pakete stehen auf den Zielversionen (Microsoft-Familie 10.0.15); die aufgeschobenen Linien bleiben auf Serilog-10.x und xunit-3.x; `outdated` zeigt nur noch die Major-Angebote 11.0.0/4.x (plus ggf. neu erschienene Versionen) | `outdated`-Ausgabe im Ergebnis/PR; `git diff` der vier geänderten csproj (`VideoWebPlayer`, `VideoWebPlayer.Client`, `VideoWebPlayer.Tests`, `MarkdownLinkCheck.Tests`) |
| 2 | Debug-/Release-Build grün | Umsetzungsschritt 7 | Alle Bumps angewendet | `dotnet build VideoPlayer.sln -c Debug` und `-c Release` | 0 Fehler in beiden Konfigurationen; keine neuen Warnungen außer NU1903-Toleranz; lokaler `--vulnerable --include-transitive`-Re-Scan ohne Befund | Build-Logs + Re-Scan-Ausgabe in `test-results.md` |
| 3 | Volle Testsuite grün | Umsetzungsschritt 8 | Build grün | `dotnet test VideoWebPlayer.Tests` (beide Lanes) + `dotnet test tools/MarkdownLinkCheck.Tests` | Alle Tests grün; dokumentierte Einzelwiederholungen bei etwaigen Einzelfläckern | TRX-/Konsolen-Zusammenfassung in `test-results.md` |
| 4 | Versionsgleichheit der Mehrfachreferenzen | Umsetzungsschritte 3 und 6 | Bumps angewendet | `grep` der Versionen in allen csproj | Microsoft-Familie einheitlich `10.0.15` (inkl. der Mehrfachreferenzen `EFCore.Sqlite`, `Logging.Abstractions`); `Microsoft.NET.Test.Sdk` in beiden Testprojekten identisch; `SkiaSharp`-Paar identisch; unangetastete Mehrfachreferenzen (`msTools.Updater`, `SQLitePCLRaw`, `xunit`) unverändert gleich | csproj-Vergleich |
| 5 | Ergebnis-PR mit vollständiger Tabelle | Umsetzungsschritt 10 | Lokale Nachweise 1–4 erbracht | PR gegen `staging` öffnen | PR-Body enthält Tabelle aller 33 Pakete mit Maßnahme-Spalte (inkl. Inline-Patch-Befund und Pflegestatus-/Nachfolge-Befunden), Aufschub-Gründe (11.x/xunit 4.x), Scan-Referenz 45192953736, Dependabot `[]`, `Closes #380`; kein separates Folge-Issue | PR-Body |
| 6 | PR-Checks grün | PR geöffnet | CI läuft | Fail-fast-Beobachtung | `static-checks`, `build-and-test` inkl. Coverage-Gate, `no-pending-backmerge` erfolgreich; Security-Scan-Gate auf neuem Bestand sauber | `gh pr checks` |
| 7 | Rücknahme-Pfad (bedingt) | Ein Update bricht Build/Tests | Bumps angewendet, Gate schlägt fehl | Betroffenes Update zurücknehmen | Das Update ist nicht im PR; es steht in der Tabelle als „aufgeschoben (Bruch: …)" mit Grund; verbleibende Updates bleiben im PR | `git revert`/Diff + PR-Body |

## Seiteneffekte und Risiken

- **SkiaSharp-Native-Assets:** Milestone-Sprung ändert native Binaries für win-x64/linux-x64
  — der lokale Release-Build (Schritt 7) ist der eigentliche Fang; der CI-Publish-Fang liegt
  erst nach Merge (`staging-ci.yml` `prerelease`-Job). Der ImageSharp-Präzedenzfall zeigt,
  dass ein grüner PR-Check allein nicht ausreicht.
- **Playwright-Browser-Version:** Nach dem Paket-Bump passen vorhandene lokale Browser-
  Binaries nicht mehr — ohne `playwright install` schlagen OsInterface-Tests lokal fehl
  (Umgebungsfehler, kein Produktfehler). In der CI automatisch konsistent.
- **Transitive Verschiebungen:** bunit→AngleSharp 1.10.1, SkiaSharp→`System.Memory`-Floor,
  Microsoft-Familienpatch→weitere Framework-nähe transitive Stände; Restore-Ausgabe auf
  neue transitive Versionen prüfen. Der lokale Re-Scan (Schritt 7) ist die Vorprüfung,
  das Gate-2 der CI die abschließende Kontrolle — beide müssen sauber bleiben.
- **Microsoft-10.x-Familienpatch:** Drei Patchebenen auf einmal (10.0.12→10.0.15) —
  kumulative Servicing-Releases sind rückwärtskompatibel, aber der Block berührt alle
  drei Projekte gleichzeitig; Versionsgleichheits-Fehler fallen in Akzeptanzszenario 4
  auf, ein fachlicher Bruch in der Testsuite.
- **Timing-Drift:** Neue Paketversionen zwischen Inventory und Umsetzung — Schritt-1-
  Re-Verifikation abfedert; neu erschienene Majors ändern die Einstufung (dann neu bewerten).
- **Zurückgenommene Updates:** Jede Rücknahme verkleinert den PR; die Tabelle muss den
  Endzustand abbilden, nicht den Plan.
- **Kein Eingriff in Workflows:** Die 11.x-Aufschub-Entscheidung verändert keine CI-Datei;
  `WarningsAsErrors`/Suppressions bleiben unberührt.

## Offene Punkte

Keine — alle im Issue offen gelassenen Punkte sind unter „Designentscheidungen"
getroffen (Sammel-PR, Aufschub-Dokumentation im PR-Body, Scan-Referenz als Run-Link,
kein-Handlungsbedarf-Fallback als Issue-Kommentar).
