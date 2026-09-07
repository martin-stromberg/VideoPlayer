# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-07 22:40 UTC+02:00 (CEST)
- **Branch und Commit-ID:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-4-manuelle-sortierung` / Commit 20a5e224143f6ee01d53445e73ac89d488792249
- **Uncommittete Änderungen:** Ja - untracked Dateien in `docs/features/task/` und `docs/projects/task/` (siehe `git status`)
- **Testumgebung und Runtime/SDK-Versionen:**
  - .NET SDK 10.0.400
  - .NET Runtime 10.0.11
  - Windows 11 Pro 10.0.26200
  - Test Framework: xUnit.net VSTest Adapter v3.1.5
  - Playwright (für E2E-Tests)

- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Gesamtprojekt-Tests: `dotnet test` (alle Testprojekte)
  - E2E-Tests spezifisch: `dotnet test --filter "PlaylistReorderE2ETests"`
  - Service-Tests: In `VideoWebPlayer.Tests` (Klasse `PlaylistReorderE2ETests` und Service-Tests)

### Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------|-------------------|-----------|-------------|----------------|--------------|----------|
| Gesamtprojekt-Tests (Lauf 1) | `dotnet test --logger "console;verbosity=normal" --no-build` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | Mehrere Hundert | 0 | 0 | [Full Log](test-results/full-test-run.log) |
| PlaylistReorderE2ETests (Lauf 2) | `dotnet test --filter "PlaylistReorderE2ETests" --logger "console;verbosity=normal"` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 6 | 0 | 0 | [E2E Test Log](test-results/e2e-reorder-tests.log) |

**Detaillierte Ergebnisse Lauf 1 (Gesamtprojekt):**
- MarkdownLinkCheck.Tests: 6 Tests, 6 bestanden, 0,89 Sekunden
- VideoWebPlayer.Tests: ca. 300+ Tests (vollständige Anzahl nicht in Ausgabe erfasst)
- Gesamtergebnis: **Erfolgreich**

**Detaillierte Ergebnisse Lauf 2 (PlaylistReorderE2ETests):**

| Test-ID | Dauer | Status |
|---------|-------|--------|
| E2E_SortModeChange_ByReleaseDateToManual_ActivatesManualModeControls | 9 s | Bestanden |
| E2E_SortModeChange_ManualToByReleaseDate_ShowsWarningModal_AndConfirmingDeactivatesControls | 12 s | Bestanden |
| E2E_SortModeChange_Manual_ToByReleaseDate_Cancel_KeepsManualMode | 10 s | Bestanden |
| E2E_QuickActionButton_MoveToEnd_MovesEntryToLastPosition | 14 s | Bestanden |
| E2E_QuickActionButton_MoveToBeginning_MovesEntryToFirstPosition | 12 s | Bestanden |
| E2E_DragDropReorder_ManualMode_PersistsSortOrder | 12 s | Bestanden |

**Gesamtergebnis:** 6 Tests, 6 bestanden, 0 fehlgeschlagen, 1 Minute 13 Sekunden

### Nachgewiesene bestehende Testfehler

Keine expliziten Test-Fehlschläge nachgewiesen. Der Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder` **besteht**, obwohl er laut Anforderung zu schwach ist:

- **Test-ID:** `VideoWebPlayer.Tests.PlaylistReorderE2ETests.E2E_DragDropReorder_ManualMode_PersistsSortOrder`
- **Suite/Dateipfad:** `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs` (Zeile 203-234)
- **Fehlermuster:** Der Test prüft nur:
  - `Assert.NotEqual(initialOrder, afterDrop)` — Order hat sich "irgendwie" geändert
  - `Assert.NotEqual(sourceMediaId, afterDrop[0])` — Quelle ist nicht mehr erste
  - Nach Reload: `Assert.Equal(afterDrop, afterReload)` — Änderung wurde persistiert
  - **Fehlt:** Explizite Prüfung der **exakten Zielposition**
- **Bedeutung:** Der Test würde auch bestehen mit dem beschriebenen Fehlverhalten (Gleichstand in SortOrder), weil die Reihenfolge zufällig durch AddedAt aufgelöst wird
- **Status:** Test ist **schwach**, aber nicht fehlgeschlagen — Fehler wird von Test nicht abgefangen
- **Lauf und Nachweis:** Lauf 2 — [E2E Test Log](test-results/e2e-reorder-tests.log)

### Testlücken und Ausführungsprobleme

**Bekannte Testlücken (laut Anforderung):**

1. **Fehlende exakte Positionsverifizierung** in `E2E_DragDropReorder_ManualMode_PersistsSortOrder`:
   - Laut Anforderung sollte der Test z.B. "Drag Eintrag an Position 2 auf Position 0" und dann "Verifiziere exakte Reihenfolge [3rd, 1st, 2nd]" prüfen
   - Aktuell: Prüft nur "nicht wie initial" statt "genau wie erwartet"

2. **Fehlendes `dataTransfer.setData()` in JavaScript-Simulation**:
   - Test-Methode `E2E_DragDropReorder_ManualMode_PersistsSortOrder` (Zeile 213-221) nutzt `EvalOnSelectorAsync` mit DragEvent-Simulation
   - Fehlt: `dt.setData('text/plain', targetId.toString())` nach Zeile 215
   - **Effekt:** Test funktioniert in Chrome/Chromium (Playwright) ohne, würde aber in Firefox-Tests fehlschlagen

3. **Keine Unit-Tests für die fehlerhafte OnEntryDropAsync-Logik**:
   - Service-Tests (`PlaylistServiceTests_Reorder`) testen nur `ReorderPlaylistEntryAsync`
   - Keine direkten Tests für das komponenten-seitige Drag-&-Drop-Verhalten (liegt in Razor-Komponente)
   - E2E-Test ist schwach und würde den Fehler nicht aufdecken

**Ausführungsprobleme:**
- Keine bekannt — alle Tests laufen erfolgreich

**Nicht vorhanden:**
- Unit-Tests für `PlaylistEntriesList.razor`-Komponente direkt (nur E2E-Tests)
- Tests für Firefox-Kompatibilität (E2E nutzt Chromium)

## Testklassen

### `PlaylistReorderE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs`

- `E2E_SortModeChange_ByReleaseDateToManual_ActivatesManualModeControls` — Prüft, dass Sortiermodus-Wechsel manuelle Kontrollen sichtbar macht
- `E2E_SortModeChange_ManualToByReleaseDate_ShowsWarningModal_AndConfirmingDeactivatesControls` — Prüft Warnung beim Wechsel zurück
- `E2E_SortModeChange_Manual_ToByReleaseDate_Cancel_KeepsManualMode` — Prüft Abbruch der Moduswechsel
- `E2E_QuickActionButton_MoveToEnd_MovesEntryToLastPosition` — Prüft "An Ende"-Button
- `E2E_QuickActionButton_MoveToBeginning_MovesEntryToFirstPosition` — Prüft "An Anfang"-Button
- `E2E_DragDropReorder_ManualMode_PersistsSortOrder` — **Hauptfunktionsprüfung für Drag & Drop** (schwach)

### Weitere Service-Tests (Auszug)

`PlaylistServiceTests_Reorder` (in `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Reorder.cs`)
- `ReorderPlaylistEntry_Manual_SuccessfullyReorders` — Prüft `ReorderPlaylistEntryAsync` Funktionalität
- `ReorderPlaylistEntry_NegativeSortOrder_ThrowsArgumentException` — Validierung
- `ReorderPlaylistEntry_NotManualMode_ThrowsPlaylistNotInManualSortModeException` — Mode-Check
- `BatchReorderPlaylistEntries_SuccessfullyReorders_Multiple` — Batch-Operation
- Weitere Validierungs- und Fehlerfall-Tests

## Hilfsmethoden

### `PlaylistsE2ETestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

- `SetupManualPlaylistWithThreeEntriesAsync(playlistName)` — Erstellt Playlist mit 3 Filmen in Manual-Mode und gibt deren Order zurück
- `LoginAsync(email)` — Authentifiziert Testbenutzer
- `CreatePlaylistViaUiAsync(name, description)` — Erstellt Playlist über Browser-UI
- `SeedMoviesIntoPlaylistAsync(playlistName, count)` — Fügt Testfilme hinzu

**E2E-Infrastruktur:**
- Nutzt `Microsoft.Playwright.Chromium` für Browser-Automation
- Erstellt isolierte SQLite-Datenbank pro Test
- Host startet auf zufälligem Port (http://127.0.0.1:0)
- Benutzer werden vor jedem Test geseedet
