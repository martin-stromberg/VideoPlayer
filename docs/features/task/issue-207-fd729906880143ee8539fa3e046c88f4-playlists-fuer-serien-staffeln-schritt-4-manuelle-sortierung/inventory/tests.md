# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-07T10:00:00 UTC (aktueller Stand)
- **Branch und Commit-ID:** 
  - Branch: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-4-manuelle-sortierung`
  - Commit: `daad1d00868686480d7fb767dcccb0ab1ff4fe8f`
- **Uncommittete Änderungen im getesteten Stand:** Siehe `git status` Output - mehrere `.md` und `.cs` Dateien geändert/hinzugefügt
- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET SDK 10.0.400
  - OS: Windows 11 Pro (10.0.26200)
  - Visual Studio 2024 (Prozess sperrt Ausgabedateien)
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Test-Projekt: `VideoWebPlayer.Tests`
  - Test-Framework: Vermutlich xUnit oder NUnit (Analyse erforderlich)
  - Testbefehl: `dotnet test` (standard)
  - Alle Playlist-Tests sind in `VideoWebPlayer.Tests/` organisiert

### Testläufe

#### Lauf 1: Build- und Testversuch (2026-09-07 10:00 UTC)

| Aspekt | Details |
|--------|---------|
| Befehl | `dotnet test --logger "json"` |
| Arbeitsverzeichnis | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` |
| Exit-Code | 1 (Fehler) |
| Status | **NICHT AUSFÜHRBAR** |
| Grund | Visual Studio (Prozesse 522424 und 525388) sperrt die Ausgabedatei `VideoWebPlayer.Client.dll`, was einen Build-Fehler verursacht (MSB3027) |
| Nachweis | Build-Fehler bei Dateikopie nach max. 10 Wiederholungen |

**Implikation:** Vollständiger Testlauf nicht möglich bis Visual Studio beendet wird. Tests für Schritt 4 können nicht verifiziert werden.

### Nachgewiesene bestehende Testfehler

Keine bestätigten Testfehler aufgrund der Build-Blockade. Tests wurden nicht ausgeführt.

### Testlücken und Ausführungsprobleme

- **Infrastruktur-Blockade:** Visual Studio-Prozesse sperren Ausgabedateien, verhindern Build und Tests
- **Lösung erforderlich:** VS schließen oder Remote-/Docker-Build verwenden
- **Status vor Step 4:** Unbekannt - keine Testläufe durchgeführt
- **Zu verifizierende Test-Kategorien für Schritt 4:**
  - Unit-Tests für `ReorderPlaylistEntry_Manual_SuccessfullyReorders`
  - Unit-Tests für `ReorderPlaylistEntry_NotOwner_ReturnsUnauthorized`
  - Unit-Tests für `ReorderPlaylistEntry_NotManualMode_ReturnsBadRequest`
  - Unit-Tests für `BatchReorderPlaylistEntries_DuplicateSortOrder_ReturnsConflict`
  - Unit-Tests für `AddMediaToPlaylistAsync_ManualMode_AppendsAtEnd`
  - Unit-Tests für `AddMediaToPlaylistAsync_ByReleaseDateMode_SortOrderIsNull`
  - Unit-Tests für `ChangeSortModeAsync_*` (verschiedene Szenarien)
  - Integrationstests für Drag-&-Drop, Modal-Dialog, Batch-Operationen

## Testklassen

Analyse der vorhandenen Teststruktur basierend auf Code-Einsicht:

### `PlaylistServiceTests_GetEntriesPaged`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntriesPaged.cs`

- Testet die Methode `GetPlaylistEntriesPagedAsync` mit Paginierung
- Wahrscheinlich Tests für ByReleaseDate-Sortierung vorhanden
- **Status für Schritt 4:** Müssen erweitert werden um Manual-Mode-Sortierungstests

### `PlaylistServiceTests_AddMedia`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_AddMedia.cs`

- Testet `AddMediaToPlaylistAsync`
- **Status für Schritt 4:** Müssen erweitert werden um SortOrder-Assignment-Tests bei Manual-Mode

### `PlaylistDetailE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs`

- End-to-End-Tests für Playlist-Detail-Komponente
- **Status für Schritt 4:** Müssen erweitert werden um Drag-&-Drop und Modal-Dialog-Tests

### `PlaylistEntriesE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistEntriesE2ETests.cs`

- End-to-End-Tests für Playlist-Einträge
- **Status für Schritt 4:** Müssen erweitert werden um Batch-Reorder-Tests

### Controller-Tests
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_*.cs`

- Separate Test-Dateien für verschiedene Operationen (Create, Update, Delete, Entries, Read, Auth)
- **Status für Schritt 4:** Müssen erweitert werden um neue Endpoints (Reorder, Batch-Reorder, ChangeSortMode)

## Hilfsmethoden

### `PlaylistServiceTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`

- Basis-Testklasse mit gemeinsamen Setup- und Mock-Methoden für Service-Tests
- Stellt Datenbank-Context und Test-Daten bereit

### `PlaylistsE2ETestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

- Basis-Testklasse für End-to-End-Tests
- Initialisiert vollständige Test-Infrastruktur (eventuell Test-Server, Authentifizierung)

### `PlaylistsControllerTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsControllerTestBase.cs`

- Basis-Testklasse für Controller-Tests
- Stellt Mock-Services und Authentifizierung bereit

## Erkannte Testlücken

1. **Keine Tests für `ReorderPlaylistEntryAsync`** - Methode existiert noch nicht
2. **Keine Tests für `BatchReorderPlaylistEntriesAsync`** - Methode existiert noch nicht
3. **Keine Tests für `ChangeSortModeAsync`** - Methode existiert noch nicht
4. **Keine Tests für SortOrder-Eigenschaft** - Eigenschaft existiert noch nicht
5. **Erweiterte Tests für `GetPlaylistEntriesPagedAsync`** - Muss Manual-Mode-Sortierung testen
6. **Erweiterte Tests für `AddMediaToPlaylistAsync`** - Muss SortOrder-Zuweisung testen

## Existierende Testmuster

Basierend auf Analyse der Teststruktur werden folgende Testmuster in bestehenden Dateien verwendet:

- **Ownership-Checks:** Tests prüfen, ob nur Playlist-Besitzer Operationen durchführen können
- **Mode-Validierung:** Tests prüfen Mode-spezifisches Verhalten
- **Exception-Handling:** Tests erwarten spezifische Exceptions bei ungültigen Eingaben
- **Paginierung:** Tests verifizieren korrekte Pagination-Arithmetik
- **Orphan-Handling:** Tests prüfen, ob verwaiste Einträge automatisch entfernt werden

Diese Muster sollten auch für Schritt-4-Tests angewendet werden.
