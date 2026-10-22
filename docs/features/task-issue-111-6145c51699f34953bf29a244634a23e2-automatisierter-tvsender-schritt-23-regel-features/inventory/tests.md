# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt: 2026-10-22 ~18:20 MESZ (UTC+2)
- Branch: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-23-regel-features`, Commit `7a506c6`
- Uncommittete Änderungen: keine fachlichen (nur untracked Live-Stream-Segmente unter `VideoWebPlayer/Data/LiveStream/` der laufenden Benutzer-App)
- Umgebung: .NET SDK 10.0.402, Windows; `bin\Debug` der App durch laufende Benutzer-Instanz `VideoWebPlayer` (PID 58256) gesperrt → Suite über `-p:OutDir=<repo>/artifacts/schritt23-bin` gebaut/ausgeführt (etabliertes Verfahren seit Schritt 21).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -p:OutDir=<repo>/artifacts/schritt23-bin --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 2157 | 0 | 1 (bekannter Alt-Skip) | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |

### Nachgewiesene bestehende Testfehler

Keine. Baseline vollständig grün (2157/2158, 1 Skip).

## Testklassen (relevant)

- `ProgramScheduleServiceTests_Generation` — Tages-/Wochen-/Einmal-/Feiertags-Occurrences,
  Serien-Cursor (`EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` belegt die
  Lookback-Verbrauchssemantik), Wrap-Around, SlotDuration, lückenlose Füllung.
- `ProgramScheduleServiceTests_Genres` — Genre-Expansion (Union, Quellfilter, versteckte/
  saisonale Genres) — **kein Test zu Multi-Serien-Interleaving vorhanden**.
- `ProgramScheduleServiceTests_Boundary` — Fenstergrenzen, oversized-head-Consumption,
  Displaced-Bereinigung, Reset.
- `ProgramScheduleServiceTests_Reconcile` — Verdrängen, Cursor-Rewind, Stabilität über
  Reconcile-Läufe.
- `ProgramRuleServiceTests_Validation` — Regel-Input-Validierung.
- `TVSenderAdminTests` (bUnit) — Tab-Struktur; `RuleEditor_RendersAsCard_InsideRulesTab`
  assertiert Card + KEIN Overlay → **anzupassen für N21**.
- `TVSenderAdminE2ETests` (Playwright) — assertiert `#rule-editor` sichtbar + kein
  `.admin-dialog-overlay` → **anzupassen für N21**.
- `VideoWebPlayerBackupDataTests_*` — Alt-Backup-Regressionen via
  `LegacyBackupArchiveBuilder.RemoveColumnsAsync` (Muster: `..._Trailer`).

## Hilfsmethoden

- `ProgramScheduleTestDb` (echtes In-Memory-SQLite): `CreateShowAsync` (Serie+Staffel+
  Episoden mit Laufzeit), `CreateMovieAsync`, `CreateGenreAsync`, `LinkShowGenreAsync`,
  `LinkMovieGenreAsync`, `CreateRuleAsync` (inkl. `genreIds`/`mediaSourceIds`),
  `CreateSlotAsync`, `SlotsAsync`, `AssertGapless`, `GetChannelAsync`.
- `LegacyBackupArchiveBuilder` — baut ge.seedte Backup-Archive und entfernt Spalten für
  Alt-Stand-Tests.
