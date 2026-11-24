# Übersetzte Anforderung: Wöchentliche NuGet-Paketprüfung (2026-W48)

GitHub-Issue: martin-stromberg/VideoPlayer#380. Das Issue wurde vom Workflow
`.github/workflows/review-nuget-weekly.yml` erzeugt (Label `automated-review-nuget`);
maßgeblich ist der Issue-Body, der auslösende Kommentar enthält nur die
Bearbeitungsaufforderung.

### Fachliche Zusammenfassung

Alle in der `VideoPlayer.sln` verwendeten NuGet-Pakete sind zu analysieren und mit
Update-Empfehlungen inklusive Risikobewertung zu versehen. Empfehlungen mit
niedrigem bis mittlerem Risiko werden direkt in einem Pull Request gegen den Branch
`staging` umgesetzt; risikoreiche oder unsichere Empfehlungen werden dokumentiert
und aufgeschoben. Es handelt sich um eine wiederkehrende Prüfaufgabe ohne
funktionale Änderung der Anwendung — Änderungen beschränken sich auf
Paketversionen und die durch Updates zwingend nötigen Anpassungen.

### Fachlicher Scope

- **Verarbeitungseinheit:** Ein Prüfzyklus, konkret Kalenderwoche 2026-W48,
  repräsentiert durch Issue #380. Geprüft werden alle `PackageReference`-Einträge
  der Projekte `VideoWebPlayer`, `VideoWebPlayer.Client` und
  `VideoWebPlayer.Tests` sowie die Dev-Tools unter `tools/`
  (`MarkdownLinkCheck`, `MarkdownLinkCheck.Tests`, `SecretScan`). Die `tools/`
  haben niedrigere Priorität (kein Produktivcode), werden aber mitbewertet.
  Pakete, die in mehreren Projekten referenziert werden (z. B.
  `Microsoft.EntityFrameworkCore.Sqlite`, `msTools.Updater`,
  `SQLitePCLRaw.lib.e_sqlite3`), bilden je eine Einheit und müssen versionsgleich
  aktualisiert werden.
- **Auslösende Ereignisse:** Das wöchentlich (montags 05:00 UTC) vom Workflow
  `review-nuget-weekly.yml` erzeugte Analyse-Issue plus der Kommentar
  „@softwarewerkstatt Erledige die Aufgabe". Der Issue-Zyklus endet mit dem
  fertigen Ergebnis (PR bzw. dokumentiertem „kein Handlungsbedarf").
- **Erwartete Wirkung:**
  - Ein Pull Request gegen `staging`, der alle als niedriges oder mittleres Risiko
    eingestuften Paket-Updates enthält; im PR-Body eine Ergebnistabelle im Format
    Paket | aktuell → Ziel | Kategorie (Patch/Minor/Major) | Schwachstelle? |
    Risiko | Maßnahme (update / aufgeschoben + Grund).
  - Risikoreiche oder aufgeschobene Empfehlungen sind im PR-Body oder als eigenes
    Folge-Issue dokumentiert.
- **Umgang mit fehlenden Objekten:**
  - Keine verfügbaren Updates bzw. kein Handlungsbedarf: Ergebnis trotzdem als
    Tabelle dokumentieren (PR-Body bzw. Issue-Kommentar), kein leerer PR.
  - Fehlende Release Notes/Changelogs, unklare Lizenzlage oder sonstige
    Unsicherheit bei einer Hauptversion: als Risiko „hoch" einstufen und
    aufschieben statt zu raten.
  - Schlägt Build oder Testsuite nach einem Update fehl: das betroffene Update
    zurücknehmen und als aufgeschoben dokumentieren — kein PR mit bekanntem
    Bruch.
  - Transitive Pakete ohne Schwachstellen-Befund: nicht anfassen.
- **Scope-Ausschlüsse:**
  - Keine PRs gegen `main` — PRs dorthin sind nur aus `staging` erlaubt.
  - Keine Umsetzung von Empfehlungen mit hohem Risiko; diese werden nur
    dokumentiert/aufgeschoben.
  - Transitive Pakete nur, soweit sie durch Schwachstellen auffällig werden.
  - Keine Änderung an den Workflows `review-nuget-weekly.yml` oder
    `security-scan.yml` und kein Dependabot-/Renovate-Setup.
  - Keine fachlichen Codeänderungen — Codeanpassungen nur, soweit ein umgesetztes
    Update sie zwingend erfordert (ein Update, das sie erfordert, ist in der
    Regel als hoch einzustufen und aufzuschieben).

### Betroffene Klassen und Komponenten

Keine fachlichen C#-Klassen im engeren Sinn. Betroffene Artefakte:

- `VideoWebPlayer/VideoWebPlayer.csproj`, `VideoWebPlayer.Client/VideoWebPlayer.Client.csproj`,
  `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — `PackageReference`-Versionen
  des Produktiv- und Testcodes.
- `tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj`,
  `tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj`,
  `tools/SecretScan/SecretScan.csproj` — Dev-Tools, niedrigere Priorität.
- `VideoPlayer.sln` — Bezugsgröße der Projektliste für die Prüfung.
- `.github/workflows/security-scan.yml` — wöchentlicher Schwachstellen-Scan
  (montags 04:00 UTC, eine Stunde vor dem Issue-Erzeuger); dessen Ergebnisse sind
  zu referenzieren statt doppelt zu analysieren.
- `.github/workflows/staging-ci.yml` / `pr-staging-ci.yml` — Build- und
  Test-Gates, die der PR gegen `staging` durchlaufen muss; bei neuen
  Hauptversionen sind die CI-Skripte auf Kompatibilität zu prüfen
  (Präzedenzfall: `SixLabors.ImageSharp` 4.x brach den Release-Build).
- Ergebnisartefakte: Pull Request gegen `staging` mit Ergebnistabelle; ggf.
  Folge-Issue für aufgeschobene Empfehlungen.

### Implementierungsansatz

- Bestandsaufnahme: verfügbare Updates je Projekt ermitteln
  (`dotnet list package --outdated`), nach Patch/Minor/Major getrennt ausweisen;
  Schwachstellen prüfen via `dotnet list package --vulnerable
  --include-transitive` sowie GitHub Advisory Database / Dependabot-Alerts und
  den Ergebnissen von `security-scan.yml`.
- Je Paket bewerten: Security-Relevanz, Pflegestatus (letztes Release,
  archiviertes/verwaistes Upstream-Repo), empfohlene Nachfolgepakete, mögliche
  Breaking Changes anhand der Release Notes/Changelogs.
- Risikoeinstufung je Empfehlung gemäß Vorgabe:
  - **Niedrig:** Patch-Updates ohne API-Änderung.
  - **Mittel:** Minor-Updates, Updates von Test-/Tooling-Paketen, Major-Updates
    mit dokumentierter Rückwärtskompatibilität.
  - **Hoch:** Major-Updates mit Breaking Changes, Updates mit
    Code-Anpassungsbedarf, Versionsmodell-Änderungen.
- Sonderregeln aus dem Issue:
  - Prerelease-/RC-Pakete (`msTools.Backup`, `msTools.Updater`,
    `SkiaSharp`-4.x-Linie) sind wegen des Preview-Charakters mindestens als
    mittleres Risiko einzustufen.
  - Pakete mit eigenem Versionsmodell (z. B. `SSH.NET` mit Jahreszahl,
    `SkiaSharp`) werden anhand der Release Notes eingestuft, nicht bloß an der
    Versionsziffer.
  - Neue NuGet-Hauptversionen nur nach Prüfung von Lizenz, Release-Build und
    CI-Skripten (vgl. AGENTS.md); bei Unsicherheit hoch einstufen und
    aufschieben.
- Mehrfach referenzierte Pakete versionsgleich über alle betroffenen Projekte
  aktualisieren.
- Umsetzung aller Empfehlungen mit niedrigem bis mittlerem Risiko in einem
  Pull Request gegen `staging` (Gruppierung siehe Offene Fragen).
- Qualitätstore vor dem PR: `dotnet build VideoPlayer.sln` in Debug **und**
  Release fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests` grün.

### Konfiguration

- Keine neue Laufzeit- oder Anwendungskonfiguration erforderlich; die
  Paketversionen stehen in den `.csproj`-Dateien und sind versionskontrolliert.
- Zielbranch des PRs ist fest `staging`; das Ergebnisformat (Tabelle) ist im
  Issue vorgegeben.

### Offene Fragen

- **Gruppierung der Updates:** Ein Sammel-PR für alle unbedenklichen Updates
  oder getrennte PRs (bessere Bisectbarkeit bei Regressionen)? Im Issue als
  offener Punkt benannt — sinnvoll zu entscheiden und im Ergebnis zu
  begründen.
- **Dokumentationsort aufgeschobener Empfehlungen:** Nur im PR-Body oder
  zusätzlich/ stattdessen als eigenes Folge-Issue (v. a. sinnvoll, wenn kein PR
  entsteht)?
- **Form der Referenz auf `security-scan.yml`:** Reicht ein Hinweis in der
  Ergebnistabelle auf den Scan-Lauf/dessen Befunde, oder soll das Issue/der PR
  konkrete Scan-Artefakte verlinken?
- **Ergebnis ohne Handlungsbedarf:** Annahme — bei „alles aktuell" wird die
  Ergebnistabelle als Issue-Kommentar dokumentiert und kein PR geöffnet; mit
  dem Ablauf abzustimmen, falls stattdessen ein leerer PR oder gar kein
  Kommentar gewünscht ist.
