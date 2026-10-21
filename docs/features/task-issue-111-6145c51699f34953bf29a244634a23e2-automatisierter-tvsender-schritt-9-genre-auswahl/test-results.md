# Testergebnisse — Schritt 9 (Genre-Material N1+N2+N3)

Datum: 2026-10-21 (Implementierungs-Testlauf)

## Builds

| Build | Ergebnis |
|-------|----------|
| `dotnet build VideoPlayer.sln` (Debug) | 0 Fehler |
| `dotnet build VideoPlayer.sln -c Release` | 0 Fehler |

## Vollständige Testsuite

`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (synchron, ohne `--no-build`)

| Lauf | Ergebnis |
|------|----------|
| Baseline (vor Schritt 9) | 1975 bestanden / 0 Fehler / 1 übersprungen (siehe `inventory/test-results/baseline-schritt9.trx`) |
| Erst-Lauf nach Implementierung | 1997 bestanden / **1 Fehler** / 1 übersprungen (1999 gesamt) |
| Einzelfehler `Admin_GenreRule_MultiSelectAndSourceFilter` | Test-Seed-Bug: `MediaSourceId` wurde vor dem ersten `SaveChangesAsync` gelesen (FK-Verletzung) — behoben |
| Wiederholung des E2E-Tests | **Bestanden** (1/1, 8 s) |
| Endstand | **1998 bestanden / 0 Fehler / 1 übersprungen (1999 gesamt)** |

Die eine Überspringung ist der bekannte Legacy-Skip der Baseline
(`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_...`, SKIP-Bedingung).

## Neue Tests (24)

- `ProgramScheduleServiceTests_Genres` (10): Multi-Genre-Union + Dedupe, Quell-Filter
  (eingeschränkt/leer), Hidden+Alternativname→kanonisch, Hidden ohne Alternativname→kein
  Material, Saisonfenster inaktiv/aktiv, `MaterialId`-Fallback (Altbestand), Serien-Episoden
  im Genre-Union, FK-/Kaskadenlöschung beider Join-Tabellen.
- `ProgramRuleServiceTests_GenreMaterial` (10): Genre leer → Fehler, Genre/Quell-Listen bei
  Nicht-Genre-Material → Fehler, unbekannte Genre-/Quell-Ids → `KeyNotFoundException`,
  CRUD-Roundtrip/Wechsel auf Nicht-Genre-Material räumt Joins ab, Optionsquelle (Hidden+Alt →
  kanonisch, Hidden ohne Alt weg, Quell-Differenzierung, Saisonfenster, Admin-Pflicht).
- `VideoWebPlayerBackupDataTests_ProgramRuleLinks` (2): Alt-Backup ohne beide Tabellen
  restauriert (Fallback bleibt `MaterialId`), aktuelles Backup restauriert beide Tabellen.
- `TVSenderAdminE2ETests.Admin_GenreRule_MultiSelectAndSourceFilter` (1 E2E): UI-Flow
  Regel anlegen → GenreRotation → zwei Genre-Checkboxen + Quell-Checkbox → Speichern →
  Anzeige „Genre: A, B — Quellen: …" → Bearbeiten zeigt Auswahl erneut → DB-Joins verifiziert.
  Verstecktes Genre ist als Checkbox nicht vorhanden.

## Bemerkungen

- FK-/Kaskaden- und Unique-Verhalten gegen echtes SQLite geprüft (InMemory würde es nicht erzwingen).
- Keine Tests deaktiviert oder abgeschwächt.
