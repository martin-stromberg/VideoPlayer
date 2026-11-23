# Bestandsaufnahme-Detail: Tests und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-23, ~16:20 MEZ (Inventory-Erhebung)
- Branch und Commit-ID: `task/issue-356-…-schritt-2-github-page`, HEAD `bcf1794`
  (Bearbeiter-Commit; baust auf dem Schritt-1-Merge `cc016ab` auf — identischer
  Codebestand wie der dort getestete Stand, nur Doku-/Handover-Commits danach)
- Uncommittete Änderungen im getesteten Stand: keine relevanten — zum
  Erhebungszeitpunkt nur Lifecycle-Arbeitsartefakte unter `docs/features/`
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.404
  (plus 11.0.100 installiert), Node v24.15.0, Playwright 1.65.1
- Ermittelte Testsuiten und Quellen der Testbefehle:
  `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (DoD in
  `AGENTS.md`); Pre-Commit-Checks inkl. SecretScan + MarkdownLinkCheck
  (`.githooks/pre-commit`); CI: `pr-staging-ci.yml` / `staging-ci.yml`.

### Ausgangsbasis (aus Schritt-1-Handover, gleicher Codebestand)

Dieser Schritt ändert **keinen Produktcode und keine Testkonfiguration**
(neue statische Dateien unter `pages/`, neuer Workflow, neue Doku-Dateien).
Die verbindliche Baseline ist daher der dokumentierte Voll-Suite-Lauf des
Schritt-1-Merge-Stands:

| Lauf | Befehl inkl. Filter | Ergebnis | Nachweis |
|------|---------------------|----------|----------|
| Schritt-1-Abschluss (Release) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (volle Suite) | 2983 bestanden, 0 Fehler, 2 begründete Übersprünge (2985 gesamt) | `handover.md` Schritt 1; Projekt-Inventory `tests.md` |

### Nachgewiesene bestehende Testfehler

Keine im Ausgangsstand. Bekannte Umgebungs-Flakes der OsInterface-Kategorie
(E2E/Playwright, echte Ports/Prozesse): laufen im CI in der
Best-Effort-Lane `os-interface-tests` (`continue-on-error`); einzelne
Ausfälle werden lokal wiederholt (Konvention aus `AGENTS.md` bzw.
`docs/help/stabilitaet/os-interface-tests.md`). Debug-Lauf-Artefakt: 7
Playwright-Tests zeigen `404 /_framework/dotnet-browser-tools/hot-reload-
settings.json` (Dev-Only-Middleware, Release-Lauf grün) — kein Produktbefund.

### Testlücken und Ausführungsprobleme

- Für `pages/` (statische Seite) existiert kein Test-Runner im
  Solution-Scope und es wird keiner eingeführt (Anforderung: kein
  Site-Generator, keine dynamischen Inhalte). Absicherung erfolgt über:
  (a) MarkdownLinkCheck für die Doku-Links, (b) SecretScan, (c) lokale
  Auslieferung + Sichtprüfung hell/dunkel/schmaler Viewport mit
  Screenshot-Nachweis, (d) Workflow-Prüfung beim ersten `main`-Lauf.

## Testklassen (relevant für diesen Schritt)

### `DemoDataRegistrationE2ETests` (`VideoWebPlayer.Tests/DemoDataRegistrationE2ETests.cs`)

- `RegisterPage_FirstUser_ShowsDemoDataSetOption` — die Demodaten-Auswahl
  erscheint im Registrierungsformular (`#demo-data-set`).
- `RegisterFirstUser_WithDemoDataSet_FillsLibrary` — belegt Quelltyp
  `LocalDirectory`, gefüllte Bibliothek (3 Filme, 2 Serien, 6 Episoden,
  Bilder) direkt nach der Registrierung.
- `RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` — leerer Pfad unverändert.

Diese Tests sichern die Demoinstallation ab, aus der die Screenshots stammen —
sie werden durch diesen Schritt nicht verändert.

## Hilfsmethoden / Umgebung

- Node + Playwright (`npx playwright`) stehen bereit für die
  Screenshot-Produktion gegen die lokal laufende Demoinstallation
  (`dotnet run --project VideoWebPlayer`, Port 5039).
- Python + Pillow stehen für Bild-Optimierung bereit.
