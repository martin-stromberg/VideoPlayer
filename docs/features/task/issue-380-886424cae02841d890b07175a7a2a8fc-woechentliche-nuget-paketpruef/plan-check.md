# Planprüfung: Wöchentliche NuGet-Paketprüfung (2026-W48, Issue #380)

**Status: Plan vollständig**

Zweite Prüfrunde nach `plan-check.1.md` (Status damals: lückenhaft). Alle sieben
beanstandeten Punkte sind geschlossen; die nachgebesserten Planstellen wurden
inhaltlich geprüft und die zentralen Versionsbehauptungen direkt gegen den
nuget.org-Flatcontainer re-verifiziert. Keine verbleibenden Lücken.

Geprüfte Eingaben: `requirement.md`, `inventory.md` + `inventory/`, `plan.md`,
`plan-check.1.md`, Issue #380 (Body via `gh issue view`), Repo-Fakten (vier
csproj-Dateien, `pr-staging-ci.yml`, `verify-pr-source.yml`, `lib/packages`,
`docs/RELEASE_NOTES.md`), nuget.org-Flatcontainer für alle entscheidungstragenden
Pakete, `gh run view 45192953736`.

## Schließung der Vorab-Beanstandungen (plan-check.1.md)

| Beanstandung | Stand | Beleg im Plan |
|---|---|---|
| (mittel) Kein Check auf Inline-Patches der aufgeschobenen Linien | geschlossen | Eigener Abschnitt „Getrennter Ausweis: Inline-Patches der gehaltenen Linien" mit Tabelle je Linie; Schritt-1-Bullet mit Flatcontainer-/Suchbefehl; Designentscheidung „Inline-Check". Der gefundene Microsoft-Inline-Patch 10.0.15 ist als Gruppe A in die Umsetzung aufgenommen (Schritt 3). |
| (mittel) Pflegestatus/Nachfolgepakete nicht operationalisiert | geschlossen | Schritt-1-Bullet (Release-Datum, Repo-Status, Deprecation-Flag inkl. Alternate-Metadaten); Designentscheidung „Pflegestatus/Nachfolgepakete" mit konkretem Kriterium (> 24 Monate ohne Release oder archiviertes Repo); Gruppe-D-Zeilen tragen teils bereits beantwortete Befunde (Makaretu-Fork, Nito.AsyncEx, msTools, SQLitePCLRaw-Prerelease-Linie); Befund verpflichtend in der Maßnahme-Spalte. |
| (klein) Release-Notes-Prüfung nur für entscheidungstragende Pakete | geschlossen | Schritt 6 benennt den Quercheck jetzt explizit für alle fünf Test-Tooling-Bumps samt Upstream-Repos (vstest, playwright-dotnet, moq4, bUnit, coverlet) und legt fest: Bruchhinweis → Neu-Einstufung (hoch/aufschieben) statt Umsetzung. |
| (klein) Kein lokaler Schwachstellen-Re-Scan nach den Bumps | geschlossen | Schritt 7: `dotnet list … --vulnerable --include-transitive` auf dem neuen Bestand mit Rücknahme-Regel; zusätzlich als Nachweis 3 der Schritt-10-Festlegung und in Akzeptanzszenario 2. |
| (klein) Ungenauigkeit Akzeptanzszenario 1 (csproj-Zählung) | geschlossen | Szenario 1 nennt jetzt „vier geänderte csproj" — das ist die richtige Zahl: `VideoWebPlayer`, `VideoWebPlayer.Client` (Familienpatch), `VideoWebPlayer.Tests`, `MarkdownLinkCheck.Tests` (Test.Sdk). Anmerkung: Die damalige Beanstandung „drei" war selbst zu niedrig — der Familienpatch berührt auch die Client-csproj, der Test.Sdk-Bump die Tool-Test-csproj. |
| (klein) Playwright-Install lokal unpräzise | geschlossen | Schritt 8 nennt jetzt den exakten Repo-Mechanismus `pwsh VideoWebPlayer.Tests/bin/<Konfiguration>/net10.0/playwright.ps1 install chromium` — gegen `pr-staging-ci.yml` Z. 216 verifiziert (CI nutzt dieselbe Datei mit `--with-deps` unter Linux). |
| (klein) Kein Duplikat-PR-Check | geschlossen | Schritt 10 prüft neben `gh pr list --base staging --head main` (Backmerge-Gate) jetzt auch `gh pr list --state open` auf einen offenen Paket-/NuGet-PR. |

## Nachprüfung der kritischen Punkte

- **Paketabdeckung vollständig:** 40 `PackageReference` / 33 Pakete der vier
  paketbehafteten Projekte sind restlos den Gruppen A–D zugeordnet (Zählung gegen
  die csproj-Dateien: 21 + 3 + 13 + 3; `MarkdownLinkCheck`/`SecretScan` korrekt
  referenzfrei). Microsoft-Familie korrekt als 11 Pakete / 13 Referenzen gezählt.
- **Versionsgleichheit:** Alle sieben Mehrfachreferenzen sind in der Scope-Klärung
  aufgeführt und werden respektiert — Familienpatch als eine gekoppelte Einheit,
  Test.Sdk in beiden Testprojekten, SkiaSharp/NativeAssets gepaart, unangetastete
  Paare (msTools.Updater, SQLitePCLRaw, xunit) bleiben versionsgleich. Kein
  Teilbump vorgesehen.
- **Risikoeinstufung konform:** Issue-Regeln korrekt angewendet — Patches niedrig,
  Test-Tooling/Minors mittel, net11-/xunit-4-Majors hoch mit begründetem Aufschub;
  Prerelease-Regel (SkiaSharp mindestens mittel) und Versionsmodell-Regel
  (SSH.NET/SkiaSharp anhand Release Notes) beachtet; AGENTS.md-§7-Prüfung
  (Lizenz/Release-Build/CI) in die Hoch-Begründung eingearbeitet.
- **Re-Verifizierte Versionsbehauptungen (nuget.org-Flatcontainer):**
  Microsoft-Familie → höchste stabile 10.x ist für **alle elf** Pakete `10.0.15`;
  Serilog.AspNetCore `10.0.0` bzw. Settings.Configuration `10.0.1` sind die
  jeweils höchsten stabilen 10.x (darüber nur dev-Prereleases); xunit.v3 `3.2.2`
  und runner.visualstudio `3.1.5` sind die höchsten stabilen 3.x; SkiaSharp
  (incl. NativeAssets.Linux.NoDependencies) `4.157.0` ist die höchste stabile
  Version (4.159.x nur als rc — der Plan nimmt bewusst nur den stabilen Cut);
  alle Gruppe-A/B-Zielversionen sind höchste stabile Stände. Angebotene Majors
  (11.0.0, xunit 4.0.3/4.0.1) stimmen mit dem Aufschub-Block überein.
- **Akzeptanzszenarien:** Sieben Szenarien decken den Zyklus inkl. bedingtem
  Rücknahme-Pfad ab; Nachweise konkret (outdated-Ausgabe, Build-Logs, TRX,
  csproj-Diff, PR-Body, `gh pr checks`, Revert-Diff).
- **Schritt-10-Festlegung ausreichend:** Dedizierter Abschnitt mit sechs
  konkreten Nachweisen (Restore, Debug+Release-Build, lokaler Re-Scan, volle
  Testsuite inkl. Flake-Konvention, Tool-Tests, PR-Gates) und `test-results.md`
  als Artefakt statt Screenshots — korrekt, da keine UI-Änderung.
- **PR-/Issue-Vorgaben:** Zielbranch `staging` (durch `verify-pr-source.yml`
  technisch erzwungen — Datei vorhanden), Ergebnistabelle im Issue-Format über
  alle 33 Pakete, `Closes #380` (Schließung erst bei Promotion — gewünscht),
  Fail-fast-Check-Beobachtung, Fallback „Issue-Kommentar statt leerem PR",
  Entscheidung „kein Folge-Issue" begründet (Wiederkehr über Wochen-Reviews).
- **Repo-Fakten stimmen:** csproj-Stände exakt wie im Inventory; `lib/packages`
  enthält die zwei erwarteten nupkgs; `security-scan.yml`-Run **45192953736**
  vom 2026-11-23 14:19 UTC = success (per `gh run view` verifiziert);
  `docs/RELEASE_NOTES.md` enthält die im Schritt 9 genannten Sektionen
  („Important Notes Before Update" Z. 3, „Wichtige Hinweise vor dem Update"
  Z. 168); offene Issue-Punkte (Gruppierung, Doku-Ort der Aufschübe,
  Scan-Referenz, kein-Handlungsbedarf-Fall) sind entschieden und begründet.

## Hinweise (keine Lücken)

- `Serilog.Sinks.File`: Der neuere Eintrag ist das Prerelease
  `8.0.0-nblumhardt-02322` (nicht `-dev-*` wie im Plan beispielhaft genannt);
  `Serilog.Sinks.Console` hat auf nuget.org gar keine Version oberhalb 6.1.1.
  Die Kernaussage „kein stabiler Nachfolger" stimmt — nur die Beispiel-Tags
  sind kosmetisch ungenau.
- `SharpCompress 1.0.0` existiert auf nuget.org, ist aber **unlisted**
  (published 1900-01-01) — taucht daher korrekt nicht als Update-Angebot auf;
  Ziel 0.50.11 bleibt richtig. Sollte Schritt 1 wider Erwarten ein Major-Angebot
  zeigen, greift die vorhandene „Versionswahl"-Regel (Neu-Bewertung).
- Warnungs-Baseline „139" stammt aus einem Release-Build des Testprojekts —
  wie in Runde 1 angemerkt bei Abweichung nicht überinterpretieren.
- Zeitdrift ist eingeplant: Schritt-1-Re-Verifikation plus Versionswahl-Regel
  decken neu erschienene Versionen zwischen Inventory und Umsetzung ab.
