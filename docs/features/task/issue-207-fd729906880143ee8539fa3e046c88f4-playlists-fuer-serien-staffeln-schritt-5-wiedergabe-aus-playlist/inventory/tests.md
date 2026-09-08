# Test-Bestandsaufnahme - Playlist-Wiedergabe

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-08 20:20:29 UTC+02:00 (zum Zeitpunkt des letzten Commits)
- **Branch und Commit-ID:** 
  - Branch: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-3-automatische-sortierung-anzeige`
  - Commit: `22674a80b32bbcd065c9157bf988647723667477`
  - Betreff: "chore: Entwicklungsschritt 5 als In Arbeit markieren"

- **Uncommittete Änderungen im getesteten Stand:**
  - Nur untracked-Verzeichnis: `docs/features/task/...` (Anforderungs- und Planungsdokumente)
  - Keine Produktions- oder Test-Code-Änderungen

- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET SDK: Version in VideoPlayer.sln (VS 2022 Format, wsl C# target)
  - Test-Framework: Nicht eindeutig aus Ausgabe erkennbar, aber xUnit/NUnit likely
  - Betriebssystem: Windows 11 Pro
  - Testlaufzeit: Mehrere Sekunden (einzelne Tests: 1-30 Sekunden, einige Integration/E2E bis 28 Sekunden)

- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Befehl: `dotnet test -c Debug --logger "trx;LogFileName=test-results.trx" --verbosity=normal`
  - Arbeitsverzeichnis: `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4`
  - Testsuites gefunden (aus Dateinamen):
    - `VideoWebPlayer.Tests` (Haupttestsuite)
    - Unit-Tests: Services, Controller, Components
    - E2E-Tests: Blazor/UI Tests
    - Tool-Tests: MarkdownLinkCheck.Tests, SecretScan (optional)

---

## Testläufe

### Lauf 1 (Basis-Testlauf)

| Attribut | Wert |
|----------|------|
| Befehl inkl. Filter | `dotnet test -c Debug --logger "trx;LogFileName=test-results.trx" --verbosity=normal` |
| Arbeitsverzeichnis | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` |
| Exit-Code | 0 (Erfolg) |
| Erfolgreich | 500+ (exakte Zahl aus Log nicht eindeutig lesbar, aber Logs zeigen "Bestanden" für ~500 Tests) |
| Fehlgeschlagen | 0 |
| Übersprungen | 0 |
| Nachweis | [test-run-baseline.log](test-results/test-run-baseline.log) |

### Testergebnis-Zusammenfassung

**Status:** ✅ Alle Baseline-Tests bestanden

Aus dem Testlauf ist zu sehen:
- Alle PlaylistService-Tests bestanden (AddMedia, GetEntries, GetEntriesPaged, Delete, Read, Update, SortMode, Reorder)
- Alle PlaylistsController-Tests bestanden (Read, Update, Delete, Entries, Reorder, SortMode, Auth, Create)
- Alle E2E-Tests für Playlists bestanden (PlaylistsE2ETests, PlaylistDetailE2ETests, PlaylistEntriesE2ETests, etc.)
- Alle anderen Unit- und E2E-Tests bestanden (ContinueWatching, UnlockedMedia, MediaSource, etc.)

**Fehlgeschlagene oder übersprungene Tests:**
- Keine nachgewiesen im Ausgangslauf

**Relevante bestehende Tests für Playlist-Wiedergabe:**

#### PlaylistService Tests

| Test-Klasse | Relevante Tests |
|------------|-----------------|
| `PlaylistServiceTests_GetEntries` | Tests für Sortierung, Freischaltung, Orphan-Handling (18 Tests) |
| `PlaylistServiceTests_GetEntriesPaged` | Tests für paginierte Sortierung, Freischaltung (11 Tests) |
| `PlaylistServiceTests_SortMode` | Tests für Moduswechsel ByReleaseDate ↔ Manual (6 Tests) |
| `PlaylistServiceTests_AddMedia` | Tests für Hinzufügen, Kaskade, Duplikate (7 Tests) |
| `PlaylistServiceTests_Reorder` | Tests für manuelle Neuordnung (5 Tests) |

**Wichtige bestehende Testmuster:**
- Sortierung nach Release Date mit Fallback-Kette wird getestet
- Freischaltungsprüfung wird für verschiedene Medientypen und Zugriffsvarianten getestet
- Hierarchieauflösung (Movie → MovieCollection, Episode → TVShow) wird getestet
- Paginierung funktioniert korrekt

#### PlaylistsController Tests

| Test-Klasse | Relevante Tests |
|------------|-----------------|
| `PlaylistsControllerTests_Entries` | Tests für `/entries` und `/entries/paged` Endpunkte |
| `PlaylistsControllerTests_Read` | Tests für GET Endpunkte (Zugriff, Authentifizierung) |
| `PlaylistsControllerTests_Auth` | Tests für Zugriffs-Checks |

#### E2E-Tests

| Test-Klasse | Relevant für Wiedergabe |
|------------|---------------------------|
| `PlaylistDetailE2ETests` | Tests für Detail-Anzeige, wäre relevant für Play-Button-Position |
| `PlaylistEntriesE2ETests` | Tests für Einträge-Anzeige, relevant für Wiedergabe-Kontext |
| `PlaylistMediaSearchE2ETests` | Tests für Medien-Hinzunahme, relevant für Kaskadenauflösung |

---

## Testlücken und Fehlende Tests für Schritt 5

### Unit-Tests fehlen komplett für:

1. **Playlist-Wiedergabe Navigation:**
   - `GetNextPlaylistEntryAsync` - nicht implementiert
   - `GetPreviousPlaylistEntryAsync` - nicht implementiert
   - Tests für Übersprung nicht freigeschalteter Titel
   - Tests für Endverhalten (Rückgabe `null` am Ende)
   - Tests für verschiedene Sortierungen (ByReleaseDate vs. Manual)

2. **Playlist-Kontext-Management:**
   - `IPlaylistContextProvider` - nicht implementiert
   - `SetPlaylistContext`, `GetPlaylistContext`, `ClearPlaylistContext`
   - Tests für Session-Kontext über mehrere Requests

3. **Playlist-Wiedergabe Start:**
   - `StartPlaylistAsync` - nicht implementiert
   - Tests für Berechtigungsprüfung
   - Tests für Eintrag-Validierung
   - Tests für Kontext-Initialisierung

4. **Auto-Advance:**
   - `AdvancePlaylistAsync` - nicht implementiert
   - Tests für automatisches Weiterschalten bei Titel-Ende

### Controller-Endpunkte fehlen komplett für:

- `POST /api/playlists/{id}/play?entryId={entryId}` - nicht implementiert
- `POST /api/playlists/{id}/play/next` - nicht implementiert
- `POST /api/playlists/{id}/play/previous` - nicht implementiert
- Keine Controller-Tests für neue Endpunkte

### E2E-Tests fehlen komplett für:

- Playlist-Wiedergabe Start aus Detail-Seite
- Manuelle Navigation (Next/Previous)
- Automatisches Weiterschalten bei Titel-Ende
- Playlist-Badge-Anzeige im Video-Player
- Kontext-Persistierung über Seitenwechsel (optional, Session-based)
- Überspringen nicht freigeschalteter Titel

### UI-Komponenten-Tests fehlen komplett für:

- `VideoPlayer.razor` erweiterte Parameter (Playlist-Context, Auto-Advance)
- Play-Button in PlaylistDetail.razor
- Playlist-Badge-Anzeige

---

## Bestehende Testklassen (zur Orientierung für neue Tests)

### Unit-Test-Struktur

```
VideoWebPlayer.Tests/
├── Services/
│   ├── PlaylistServiceTests_AddMedia.cs
│   ├── PlaylistServiceTests_GetEntries.cs
│   ├── PlaylistServiceTests_GetEntriesPaged.cs
│   ├── PlaylistServiceTests_SortMode.cs
│   └── ... weitere Service-Tests
├── Controllers/
│   └── PlaylistsControllerTests_*.cs (mehrere Dateien nach Endpoint-Kategorien)
├── Components/
│   └── PlaylistEntriesListTests.cs
└── Helpers/
    ├── PlaylistServiceTestBase.cs (Test-Fixtures)
    └── PlaylistsE2ETestBase.cs (E2E-Fixtures)
```

### Test-Basis-Klassen (zur Wiederverwendung)

| Klasse | Zweck |
|--------|-------|
| `PlaylistServiceTestBase` | Basis für PlaylistService Unit-Tests mit Fixtures |
| `PlaylistsE2ETestBase` | Basis für E2E-Tests mit Blazor-Integration |

### Fixture- und Hilfsmethoden (bestehend)

- Test-Playlists mit verschiedenen Sortierungen
- Test-Medieninhalte (Movies, Episodes, Seasons, Shows)
- Fake `IUnlockedMediaService` mit verschiedenen Freischaltungen
- Test-Benutzer mit/ohne Zugriff auf Media Sources
- Test-Assertions für Sortierung, Freischaltung, Duplikate

---

## Zusammenfassung Test-Ausgangszustand

| Bereich | Status | Detailn |
|---------|--------|---------|
| PlaylistService Unit-Tests | ✅ 100% Bestanden | 52+ existierende Tests für CRUD, Sortierung, Freischaltung |
| PlaylistsController Unit-Tests | ✅ 100% Bestanden | 40+ existierende Tests für alle bestehenden Endpunkte |
| E2E-Tests | ✅ 100% Bestanden | 10+ E2E-Tests für Playlists-Funktionalität |
| **Neue Wiedergabe-Features** | ❌ 0% Vorhanden | Alle neuen Methoden/Endpunkte müssen noch getestet werden |
| Wiedergabe-Navigation Tests | ❌ 0% Vorhanden | GetNext/Previous Tests fehlen komplett |
| Kontext-Provider Tests | ❌ 0% Vorhanden | IPlaylistContextProvider Tests fehlen (Interface noch nicht definiert) |

**Wichtig:** Der Ausgangszustand zeigt, dass die bestehende Playlist-Infrastruktur (CRUD, Sortierung, Freischaltung, Paginierung) stabil ist. Es sind keine Pre-existing Test-Fehler zu beheben vor Schritt 5.

