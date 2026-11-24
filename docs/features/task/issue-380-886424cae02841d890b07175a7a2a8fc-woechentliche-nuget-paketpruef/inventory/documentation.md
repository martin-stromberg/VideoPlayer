# Dokumentationsstellen zu Paketen, Abhängigkeiten und Update-Prozess

Wo Paket-/Update-Wissen heute dokumentiert ist — relevant als Referenz für die
Ergebnistabelle und für den Fall, dass Updates Doku-Anpassungen erzwingen
(AGENTS.md §7: `docs/help/`, README, Release Notes bei Bedarf).

## Prozess- und CI-Dokumentation

| Datei | Inhalt für diesen Zyklus |
|---|---|
| `docs/REVIEW_AUTOMATION.md` | Beschreibt alle drei Review-Issue-Workflows; Z. 47–54 erklärt das Verhältnis Scan (erkennt, bricht Build) vs. NuGet-Review-Issue (analysiert, empfiehlt). Referenz für „Ergebnis referenzieren statt doppelt analysieren". |
| `docs/STAGING_WORKFLOW.md` | Vollständige CI/CD-Doku; „Quality Gates" (Z. 154–170, inkl. Review-Automatisierung-Zeile 165), „CI Workflow Files" (Z. 246–259), Back-Merge-Regeln (Z. 196–211). |
| `docs/help/stabilitaet/os-interface-tests.md` | Lane-Trennung, Flaky-Retry, Quarantäne-Regeln — maßgeblich für die Interpretation grüner/roter Tests nach einem Bump (siehe tests.md). |
| `docs/BRANCH_PROTECTION_SETUP.md`, `docs/SECRETS_MANAGEMENT.md`, `docs/Secrets-Setup.md` | Schutz-/Setup-Doku, paketneutral. |

## Paket-/Feed-Dokumentation

| Datei | Inhalt |
|---|---|
| `docs/GUIDE_Installation.md` Z. 15, 227–235 | Dokumentiert die lokale Paketquelle `lib/packages` (`NuGet.config`-Quelle `local`) und den Restore-Fehlerpfad bei fehlendem msTools-nupkg. **Aktuell und korrekt.** |
| `docs/Anforderung_msTools_Updater_Dateierhalt.md` Z. 115 | Das eigentliche Update-Verfahren für msTools-Pakete: neues nupkg unter `lib/packages` ablegen + `PackageReference` erhöhen + Lizenz/Release-Build/CI-Skripte prüfen — dieselbe Regel wie AGENTS.md §7 für Hauptversionen. |
| `docs/TECH_Auto_Update.md` Z. 10–19 | **Veraltet:** beschreibt noch die Einbindung von `msTools.Updater` als *Datei-Referenz* unter `lib/msTools.Updater/` — tatsächlich ist es seit den rc-Bumps eine `PackageReference` aus `lib/packages`. Im rc.6-Task-Dokument ausdrücklich als „preexisting-veralteter Bestand … bleibt unangetastet" markiert. Kandidat für Korrektur-Notiz, nicht Scope dieses Zyklus. |
| `docs/help/updates.md` | Endanwender-Doku der Update-Verwaltungsseite (msTools.Updater-Laufzeitfunktion), keine Dependency-Pflege. |
| `docs/Anforderung_msTools_Updater_Installationsskript.md`, `docs/features/task/antworten-offene-punkte-updater-iis.md` | Updater-Fachlichkeit, kein Paketprozess. |

## Historische Präzedenzfälle (Belege für die Risikoregeln)

- `docs/projects/task/issue-207-…/acceptance-korrektur-nuget-aktualisierung.md` —
  Abnahmebericht der letzten größeren NuGet-Aktualisierung: dokumentiert den
  Bump Microsoft.* `10.0.10→10.0.12`, `Microsoft.NET.Test.Sdk 18.8.1→18.10.0`,
  `Microsoft.Playwright 1.49.0→1.62.0`, `bunit 1.35.3→2.11.3` und kritisch:
  **`SixLabors.ImageSharp 4.1.2` brach den Release-Build** → per Commit
  `81adb6b` auf 3.1.11 zurückgesetzt; `5617d2e` ersetzte ImageSharp später
  komplett durch `SkiaSharp` (Lizenz + Advisories). Beleg für die Regel „neue
  Hauptversionen nur nach Lizenz/Release-Build/CI-Prüfung".
- `docs/features/task/b21855dd…-mstoolsupdater0110-rc4-tasks.md` und
  `…-rc6-tasks.md` — Task-Listen früherer msTools.Updater-Bumps inkl. der
  gefilterten Update-Testliste (`MsToolsUpdaterIntegrationTests`,
  `AutoUpdateProtectedFilesTests`, `UpdateAdminServiceTests`, …), wiederverwendbar
  bei einem künftigen msTools-Bump.
- Commit-Historie der csproj-Dateien (`git log -- '*.csproj'`):
  `80475b9` bunit→2.11.3 („behebt AngleSharp-Sicherheitswarnung NU1902"),
  `db54f57` ImageSharp→4.1.2, `81adb6b` Revert→3.1.11, `5617d2e` SkiaSharp-Ersatz,
  `a566f09`/`9e149eb` msTools.Updater rc.5/rc.6.
- Vorgänger-Prüfzyklus Issue **#358** („Wöchentliche NuGet-Paketprüfung
  2026-W47", erzeugt 2026-11-16, geschlossen 2026-11-22): kein Kommentar, kein
  verknüpfter PR — die „Ergebnis dokumentieren"-Seite des Zyklus ist damit
  praktisch noch unbesetzt; Formatvorlage gibt erst der Issue-Body selbst.

## GitHub-Issue-Templates / Agent-Doku

- `.github/ISSUE_TEMPLATE/` (bug_report, custom, feature_request) — generisch,
  nicht für die Review-Issues (die kommen aus `create-review-issue`).
- `.github/agents/CodeReviewAgent.agent.md`, `DarkPattern.agent.md` und
  `.github/skills/` (u. a. `dotnet-upgrade`) — Review-/Upgrade-Hilfen,
  nicht versionspflichtig.

## Lücken

- Es existiert **kein** eigenes Prozessdokument „wie wird ein wöchentlicher
  NuGet-Prüfzyklus abgearbeitet" (Ergebnistabelle, Aufschub-Praxis,
  Gruppierungsentscheid) — der Issue-Body (#380) ist die einzige verbindliche
  Spezifikation. Der PR-Body dieses Zyklus setzt den De-facto-Präzedenzfall.
- `ci-target-schema.md`/`ci-inventory.md` (in Workflow-Kommentaren referenziert)
  liegen außerhalb des Repos.
