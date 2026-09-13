# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-13T12:30:00Z (UTC)
- **Branch und Commit-ID:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug` (f48a054)
- **Uncommittete Änderungen im getesteten Stand:** 
  - Untracked: `docs/features/task/issue-207-fd729906880143ee8539-fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug/` (neue Verzeichnisstruktur)
  - Staged oder Modified: keine relevanten Änderungen
- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET: .NET 10.0 (net10.0 target framework)
  - dotnet test Runner: xUnit.net VSTest Adapter v3.1.5+1b188a7b0a
  - Betriebssystem: Windows 11 Pro 10.0.26200
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - `VideoWebPlayer.Tests.csproj` (xUnit-basiert)
  - Filter-Muster: `FullyQualifiedName~ContinueWatching`
  - Befehl: `dotnet test VideoWebPlayer.Tests --filter "FullyQualifiedName~ContinueWatching" --logger "console;verbosity=normal"`

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1    | `dotnet test VideoWebPlayer.Tests --filter "FullyQualifiedName~ContinueWatching" --logger "console;verbosity=normal"` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 31 | 0 | 0 | [TRX Log](test-results/TestResults_ContinueWatching.trx) |

### Nachgewiesene bestehende Testfehler

Keine bestehenden Testfehler. Alle 31 Tests bestanden erfolgreich.

### Testlücken und Ausführungsprobleme

**Keine Testlücken für die aktuell implementierte Funktionalität dokumentiert.** Alle bestehenden Tests für ContinueWatching-Funktionen konnten ausgeführt werden.

**Zu beachten für Schritt 6:**
- Es existieren aktuell keine Tests für Playlist-bezogene Weiterschauen-Einträge
- Die neuen Tests müssen abdecken:
  - `ContinueWatchingServicePlaylistTests` (Erstellen/Aktualisieren mit Playlist-Bezug)
  - `ContinueWatchingServiceMultipleEntriesTests` (mehrfaches Vorkommen desselben Videos)
  - `ContinueWatchingServiceRemovalTests` (Entfernen mit Playlist-Filter)
  - `ContinueWatchingDtoTests` (DTO mit Playlist-Properties)

---

## Testklassen

### `ContinueWatchingServiceGetNextEpisodeTests`

**Datei:** `VideoWebPlayer.Tests\Services\ContinueWatchingServiceGetNextEpisodeTests.cs`

Erbt von `ContinueWatchingServiceTestBase`. Testet die Episode-Navigation-Logik.

**Getestete Methoden:**
- `ContinueWatchingService.ProcessBufferedEntryAsync()` (indirekt über GetNextEpisodeAsync)

**Tests:**
- `HappyPath_SimpleEpisodeSequence_ReturnsNextEpisode` — Basis-Szenario mit zwei aufeinanderfolgenden Episoden
- `AllEpisodesWithoutReleaseDate_SortsByNumber` — Episode ohne Veröffentlichungsdatum (sortiert nach Nummer)
- `AllEpisodesWithReleaseDate_SortsCorrectly` — Episoden mit Veröffentlichungsdatum (sollten nach Veröffentlichungsdatum sortiert werden, nicht nach Nummer)
- `MixedReleaseDate_NullAndNonNull_SortsConsistently` — Gemischte Veröffentlichungsdaten (null + Datum)
- `SeasonTransition_LastEpisodeOfSeason_JumpsToNextSeason` — Übergang zwischen Staffeln
- `NextSeasonEmpty_ReturnsNull` — Keine Staffel nach der aktuellen
- `NoNextSeason_ReturnsNull` — Keine nächste Staffel vorhanden
- `FirstEpisodeMissing_SkipsTo_NextAvailable` — Erste Episode fehlt, sollte zur nächsten verfügbaren springen
- `RegressionTest_LoopScenario_NoInfiniteLoop` — Regressions-Test für Schleifenszenario
- `MultipleEpisodesWithIdenticalReleaseDate_SortsByNumber` — Mehrere Episoden mit identischem Datum
- `OffByOne_PositionNotConfusedWithId` — Off-by-one Fehler-Vermeidung
- `SingleEpisodeInSeason_ReturnsNull` — Nur eine Episode in der Staffel
- `EpisodeGaps_SkipsGappedEpisodes_FindsNext` — Lücken in Episoden-Nummern
- `LastEpisodeOfSeason_ReturnsNull_InSameSeason` — Letzte Episode einer Staffel

**Basis-Klasse Test-Setup:**
- Alle Tests erben von `ContinueWatchingServiceTestBase`
- Verfügen über: `_db` (ApplicationDbContext), `_service` (ContinueWatchingService), `_testUserId` (string)
- Verwendete Test-Konstanten: `CompletedPosition` (99% Position), `Duration` (100s)

---

### `ContinueWatchingContextMenuActionTests`

**Datei:** `VideoWebPlayer.Tests\Services\ContinueWatchingContextMenuActionTests.cs`

Testet die Hide- und Skip-Funktionalität aus dem Context-Menü der UI.

**Tests:**
- `HideAsync_RemovesOnlyEntryForCurrentUser` — Verstecken-Funktion entfernt nur den Eintrag des aktuellen Benutzers
- `SkipAsync_Episode_ReplacesWithNextEpisodeAndKeepsListOrder` — Überspringen bei Episode
- `SkipAsync_Movie_ReplacesWithNextMovieAndKeepsListOrder` — Überspringen bei Film
- `SkipAsync_LastEpisode_RemovesEntryWithoutReplacement` — Überspringen bei letzter Episode

---

### `ContinueWatchingServiceSignalRTests`

**Datei:** `VideoWebPlayer.Tests\Services\ContinueWatchingServiceSignalRTests.cs`

Testet die SignalR-Benachrichtigungslogik bei Weiterschauen-Änderungen.

**Tests:**
- `BufferFlow_EnqueueAndProcess_SendsSignalREvent` — Kompletter Buffer-Prozessfluss
- `ProcessBufferedEntry_NewEntry_SendsSignalRUpdate` — Neue Einträge triggern Update
- `ProcessBufferedEntry_UpdateExisting_SendsSignalRUpdate` — Aktualisierte Einträge triggern Update
- `ProcessBufferedEntry_Episode_SendsSignalRUpdate` — Episode-spezifischer Update
- `MultipleUpdates_SendsMultipleEvents` — Mehrfach-Updates senden mehrfach Events

---

### `ContinueWatchingWatchedStatusTests`

**Datei:** `VideoWebPlayer.Tests\Services\ContinueWatchingWatchedStatusTests.cs`

Testet die Integration mit `WatchedStatusService` (Markierung als „gesehen").

**Tests:**
- `ProcessBufferedEntry_OutsideEndThreshold_DoesNotMarkMovieWatched` — Film nicht als „gesehen" markiert, wenn noch nicht am Ende
- `ProcessBufferedEntry_InsideEndThreshold_MarksMovieWatched` — Film als „gesehen" markiert bei Erreichen des Schwellenwerts
- `ProcessBufferedEntry_RepeatedCompletion_UpdatesSingleWatchedEntry` — Mehrfaches Markieren aktualisiert nur einen Eintrag

---

### `ContinueWatchingE2ETests`

**Datei:** `VideoWebPlayer.Tests\ContinueWatchingE2ETests.cs`

End-to-End-Tests für komplexe Szenarien.

**Tests:**
- `HappyPath_EpisodeCompleted_NextEpisodeAppearsInContinueWatchingList` — Erste Episode fertig, zweite Episode erscheint in Liste
- `SeasonTransition_LastEpisodeOfSeasonCompleted_FirstEpisodeOfNextSeasonAppears` — Letzte Episode einer Staffel fertig, erste der nächsten erscheint
- `EpisodeGap_EpisodeCompleted_NextAvailableEpisodeAppearsInContinueWatchingList` — Episode mit Lücke dazu
- `SeriesEnd_LastEpisodeOfLastSeasonCompleted_NoContinueWatchingEntryCreated` — Letzte Episode der Serie, kein neuer Eintrag

---

## Hilfsmethoden

### `ContinueWatchingServiceTestBase`

**Zweck:** Basisklasse für alle ContinueWatchingService-Tests

**Ausgliederung:** In dieser Bestandsaufnahme nicht vollständig untersucht; bietet:
- Datenbank-Setup und Teardown
- Standard-Test-User und Testkonstanten
- Instanzen von `ApplicationDbContext` und `ContinueWatchingService`

### `TestHelpers`

**Zweck:** Generische Test-Hilfsfunktionen

**Verwendete Methoden (aus Tests sichtbar):**
- `CreateTvShowWithSeasonsAsync(_db, seasons)` — Erstellt Test-Serie mit Staffeln und Episoden

### Test-Daten-Factories

- `MockDataGenerator` oder ähnliches (nicht vollständig untersucht)
- Wird für die Erstellung von Filmen, Serien, Episoden mit variablen Attributen (ReleaseDate, etc.) verwendet

---

## Konfiguration

Keine spezielle Test-Konfiguration für Playlist-Weiterschauen dokumentiert. Die neuen Tests (Schritt 6) müssen:
1. Test-Playlists erstellen
2. Filme/Episoden zu Playlists hinzufügen
3. Deduplizierung mit PlaylistId testen
4. Separate Fortschritts-Verwaltung pro Playlist-Kombination prüfen
