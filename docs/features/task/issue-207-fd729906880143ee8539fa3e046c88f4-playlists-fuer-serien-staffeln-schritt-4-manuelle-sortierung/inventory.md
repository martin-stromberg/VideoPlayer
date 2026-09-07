# Bestandsaufnahme: Schritt 4 – Manuelle Sortierung von Playlists

Diese Bestandsaufnahme dokumentiert den aktuellen Zustand des Projekt-Codes bezogen auf die Anforderung "Schritt 4 – Manuelle Sortierung von Playlists", insbesondere für die Implementierung von Drag-&-Drop und manuellem Reordering von Playlist-Einträgen.

---

## Zusammenfassung

**Was bereits existiert:**
- `Playlist` Datenmodell mit `SortMode` Enum-Eigenschaft (Werte: `ByReleaseDate`, `Manual`)
- `PlaylistSortMode` Enum mit beiden Sortiermodi komplett definiert
- `PlaylistEntry` Datenmodell (alle Eigenschaften außer `SortOrder`)
- `IPlaylistService` Interface mit bestehenden CRUD-Methoden
- `PlaylistService` Implementierung mit `GetPlaylistEntriesPagedAsync` für sortierte Rückgabe
- `PlaylistsController` mit 9 bestehenden Endpoints
- Basis-Testinfrastruktur mit TestBases und Test-Utilities

**Was fehlt noch:**

| Komponente | Status | Details |
|-----------|--------|---------|
| `PlaylistEntry.SortOrder` Eigenschaft | FEHLT | long?-Eigenschaft muss hinzugefügt werden (Datenbank-Migration erforderlich) |
| Datenbank-Index für Manual-Mode | FEHLT | Composite Index `(PlaylistId, SortOrder)` erforderlich |
| `IPlaylistService.ReorderPlaylistEntryAsync` | FEHLT | Neue Interface-Methode erforderlich |
| `IPlaylistService.BatchReorderPlaylistEntriesAsync` | FEHLT | Neue Interface-Methode erforderlich |
| `IPlaylistService.ChangeSortModeAsync` | FEHLT | Neue Interface-Methode mit Konflikt-Behandlung erforderlich |
| `PlaylistService` Erweiterungen | FEHLT | Implementierung der 3 neuen Methoden |
| Sorting-Logik für Manual-Mode | TEILWEISE | `GetPlaylistEntriesPagedAsync` sortiert aktuell nach `AddedAt` für Manual-Mode, benötigt Sortierung nach `SortOrder` |
| `AddMediaToPlaylistAsync` Erweiterung | TEILWEISE | Muss SortOrder-Zuweisung bei Manual-Mode implementieren |
| API-Endpoints für Reordering | FEHLT | 3 neue Endpoints erforderlich (PUT, POST, PATCH) |
| `DtoPlaylistEntry.SortOrder` | FEHLT | Eigenschaft muss zum DTO hinzugefügt werden |
| Request/Response DTOs | FEHLT | 4 neue DTOs für Reorder-Requests und Konflikt-Responses |
| Blazor UI-Komponenten | FEHLT | Drag-&-Drop, Quick-Action-Buttons, Modal-Dialog |
| Unit-Tests für Schritt 4 | FEHLT | Alle neuen Methoden benötigen Tests |
| Integrationstests für Schritt 4 | FEHLT | Drag-&-Drop, Modal-Dialog, Batch-Workflows |

**Test-Ausgangszustand:** 
- **Testlauf-Status:** Nicht ausführbar (Visual Studio sperrt Build-Artefakte)
- **Bekannte Testfehler:** Keine (Tests nicht durchlaufen)
- **Testlücken:** Komplett für Schritt 4 (Details siehe [Tests](inventory/tests.md))

---

## Details

Detaillierte Analysen für jede Komponente:

- [Datenmodelle](inventory/models.md) – Aktuelle PlaylistEntry- und Playlist-Strukturen
- [Logikklassen](inventory/logic.md) – PlaylistService und Controller-Implementierungen
- [Interfaces](inventory/interfaces.md) – IPlaylistService Contract und neue Methoden-Signaturen
- [DTOs](inventory/dtos.md) – DtoPlaylistEntry und erforderliche neue DTOs
- [Tests](inventory/tests.md) – Test-Infrastruktur und Ausgangszustand

---

## Kritische Erkenntnisse für die Implementierung

### 1. Datenbank-Migration erforderlich
- **Spalte hinzufügen:** `PlaylistEntry.SortOrder` (long?, nullable)
- **Index anlegen:** Composite Index `(PlaylistId, SortOrder)` für effiziente Manual-Mode-Abfragen
- **Initialisierung:** Bestehende Einträge mit `SortOrder = null` initialisieren

### 2. Sortier-Logik muss erweitert werden
Die aktuelle `GetPlaylistEntriesPagedAsync` (Zeile 836-838):
```csharp
var sortedEntries = playlist.SortMode == PlaylistSortMode.ByReleaseDate
    ? await SortPlaylistEntriesByReleaseDateAsync(validEntries, cancellationToken)
    : validEntries.OrderBy(e => e.AddedAt).ToList();
```

Muss zu:
```csharp
var sortedEntries = playlist.SortMode switch
{
    PlaylistSortMode.ByReleaseDate => await SortPlaylistEntriesByReleaseDateAsync(validEntries, cancellationToken),
    PlaylistSortMode.Manual => validEntries.OrderBy(e => e.SortOrder).ThenBy(e => e.AddedAt).ToList(),
    _ => validEntries.OrderBy(e => e.AddedAt).ToList()
};
```

### 3. Service-Methoden erfordern komplexe Logik
- **`ReorderPlaylistEntryAsync`**: Ownership + Mode-Check vor Update
- **`BatchReorderPlaylistEntriesAsync`**: Duplikat-Erkennung innerhalb Batch, atomare Transaktion
- **`ChangeSortModeAsync`**: Bidirektionale Modus-Wechsel mit SortOrder-Population/Clearing

### 4. Exception-Handling erforderlich
Neue Exception-Typen oder Codes für:
- `PlaylistAccessDeniedException` (schon vorhanden, renutzen)
- `InvalidOperationException` mit spezifischen Nachrichten für Mode-Validierung
- HTTP 409 Conflict bei fehlender Bestätigung für `Manual → ByReleaseDate` Wechsel

### 5. API-Auftrag für 3 neue Endpoints
| Endpoint | Methode | Request Body | Response |
|----------|---------|--------------|----------|
| `/api/playlists/{id}/entries/{entryId}/order` | PUT | `{ "newSortOrder": <long> }` | 200 OK, 400 Bad Request, 403 Forbidden, 404 Not Found |
| `/api/playlists/{id}/entries/batch-reorder` | POST | `{ "reorderOperations": [...] }` | 200 OK, 409 Conflict, 403 Forbidden, 400 Bad Request, 404 Not Found |
| `/api/playlists/{id}/sort-mode` | PATCH | `{ "newSortMode": "Manual\|ByReleaseDate", "confirmLossOfManualOrder": bool }` | 200 OK, 409 Conflict (mit `isLossOfDataConfirmationRequired`) |

### 6. DTO-Erweiterungen
- `DtoPlaylistEntry` muss `SortOrder?: long` enthalten
- 4 neue Request/Response DTOs für Reorder-Operationen

### 7. UI-Komponenten-Anforderungen
- `PlaylistDetail.razor` benötigt Drag-&-Drop-Handling (nur für Manual-Mode)
- Schnellaktions-Buttons „An Anfang" / „An Ende" pro Eintrag
- Modal-Dialog für Bestätigungsdialog bei Sortiermodus-Wechsel

---

## Abhängigkeiten und Verweise

- **Betroffene Quellcode-Dateien (Hauptveränderungen):**
  - `VideoWebPlayer/Data/PlaylistEntry.cs` (Eigenschaft hinzufügen)
  - `VideoWebPlayer/Data/Configurations/PlaylistEntryConfiguration.cs` (Index)
  - `VideoWebPlayer/Services/IPlaylistService.cs` (3 Methoden-Signaturen)
  - `VideoWebPlayer/Services/PlaylistService.cs` (umfassende Erweiterung)
  - `VideoWebPlayer/Controllers/PlaylistsController.cs` (3 neue Endpoints)
  - `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs` (Eigenschaft)
  - `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` (UI-Logik)

- **Datenbankmigrationen:**
  - Neue EF Core Migration `AddSortOrderToPlaylistEntries`

- **Bestehende Hilfsfunktionen (wiederverwenden):**
  - `PlaylistService.GetOwnedPlaylistAsync()` für Ownership-Checks
  - `PlaylistService.ParseSortMode()` für Enum-Parsing

---

**Dokumentation erstellt:** 2026-09-07  
**Branch:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-4-manuelle-sortierung  
**Commit:** daad1d00868686480d7fb767dcccb0ab1ff4fe8f
