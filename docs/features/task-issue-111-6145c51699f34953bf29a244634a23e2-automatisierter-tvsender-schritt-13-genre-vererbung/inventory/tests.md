# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-22 (lokale Zeit, Windows)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-13-genre-vererbung` @ `a1572c3e49776936bc1f5d7547ae1520a30f1594`
- Uncommittete Änderungen im getesteten Stand: keine (Arbeitsbaum sauber)
- Testumgebung: Windows, .NET SDK 10.0.402, `VideoWebPlayer.Tests` (xunit.v3, bunit, Playwright, EF Core SQLite/InMemory)
- Quelle der Basis: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/acceptance-schritt-12.md` dokumentiert den vollständigen synchronen Suite-Lauf auf Commit `c386318` (HEAD `a1572c3` enthält danach nur den Schritt-Marker-Commit `step: Schritt 12 Fertig, Schritt 13 In Arbeit` — kein Produktivcode-Diff).

### Testläufe (dokumentierte Basis)

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Schritt-12-Abnahme | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug, synchron) | Repo-Root | 0 (mit Flake) | 2045 + Flake-Re-Run grün | 0 (nach Einzelwiederholung) | 1 (bekannter Alt-Skip) | acceptance-schritt-12.md |

Effektiv: **2046 grün + 1 Alt-Skip** (`PlaylistSkipLockedEntriesE2ETest` — Playwright-
Timeout `SelectEntryAsync`, bekannter Last-Flake laut AGENTS.md, Einzelwiederholung grün).

### Nachgewiesene bestehende Testfehler

Keine — die dokumentierte Basis ist vollständig grün (nach Einzelwiederholung des
bekannten Flakes).

### Testlücken und Ausführungsprobleme

- Kein eigener Basislauf in dieser Session (zeitnahes, dokumentiertes Ergebnis auf
  identischem Produktivcode-Stand wurde als Basis herangezogen); der verbindliche
  Nachweis erfolgt durch den vollständigen Suite-Lauf nach der Implementierung.
- Genre-Vererbung: keine bestehenden Tests für `GetOrCreateGenresAsync`-Erzeugung,
  `SaveGenre`-Case-Verhalten oder `NormalizeGenresAsync`-Erzeugung mit
  Konfigurationskontext.

## Testklassen (relevant)

### `MediaSourceClassifierActorBackfillTests`
- `BackfillMissingActorsAsync_*` — Muster für SQLite-In-Memory-Setup (`file:...?mode=memory&cache=shared` + Keeper-Connection), direkte `MediaSourceClassifier`-Instanziierung mit `null!`-Stubs und `BackfillSftpMediaSourceReader`.

### `MediaSourceClassifierCollection`
- `[CollectionDefinition(Name, DisableParallelization = true)]` — Serienausführung für Classifier-Tests (statische `_classificationRunning`-Guard).

### `MediaMetadataEditorServiceTests`
- `UpdateMovieAsync_SavesMetadataAndCreatesNewGenres` — prüft Genre-Neuanlage beim Metadaten-Edit (EF InMemory; neue Tests sollen SQLite nutzen).
- `GetGenreOptionsAsync_ReturnsDistinctSortedGenreNames` — ci-Gruppierung der Options.

### `MediaSourceClassifierBackgroundImageTests`
- Weiteres SQLite-Setup-Beispiel für Classifier-Tests.

## Hilfsmethoden

### `Helpers`
- `ProgramScheduleTestDb.AddGenre` (`Helpers/ProgramScheduleTestDb.cs:163`) — legt `Genre`-Zeilen mit `IsHidden`/`StartDate`/`EndDate` an.
- `BackfillSftpMediaSourceReader` (in `MediaSourceClassifierActorBackfillTests.cs`) — überschriebener `SftpMediaSourceReader` mit In-Memory-Dateimap.
