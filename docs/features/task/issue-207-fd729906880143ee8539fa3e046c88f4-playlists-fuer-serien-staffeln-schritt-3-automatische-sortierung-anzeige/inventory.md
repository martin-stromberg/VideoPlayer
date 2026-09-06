# Bestandsaufnahme: Schritt 3 – Automatische Sortierung und performante Anzeige mit Infinity-List

Diese Bestandsaufnahme analysiert den existierenden Projektcode bezogen auf die Anforderung für Schritt 3 des mehrstufigen Playlist-Rollouts. Der Fokus liegt auf den erforderlichen Komponenten für Virtual Scrolling, paginierte Anzeige und automatische Sortierung nach Erscheinungsdatum.

---

## Zusammenfassung

**Was existiert bereits:**
- ✅ Datenmodelle (`Playlist`, `PlaylistEntry`, Medien-Entitäten) mit allen erforderlichen Feldern für Sortierung und Hierarchie
- ✅ `PlaylistService` mit umfassender Basis-Infrastruktur (Berechtigungen, Orphan-Bereinigung, Titel-Laden, Kaskaden-Logik)
- ✅ `MediaTypeHandler` Pattern ermöglicht typenspezifische Daten-Handler pro Medientyp
- ✅ `PlaylistSortMode` Enum mit `ByReleaseDate` und `Manual` Werten
- ✅ `IPlaylistService` Interface mit CRUD-Methoden
- ✅ API-Controller mit Endpoints für Playlist-Verwaltung
- ✅ Umfassende Unit- und E2E-Tests für bestehende Funktionalität
- ✅ Test-Utilities und Hilfsmethoden für Test-Daten-Erstellung

**Was fehlt für Schritt 3:**
- ❌ Methode `GetPlaylistEntriesPagedAsync()` im Service (erforderlich für Virtual Scrolling/Infinity-List)
- ❌ Methoden für Erscheinungsdatum-Laden pro Medientyp (`LoadReleaseDateAsync()` oder ähnlich)
- ❌ Methoden für Hierarchie-Sequenzen pro Medientyp (`GetHierarchySequenceAsync()` oder ähnlich)
- ❌ Sortierlogik nach Erscheinungsdatum mit Fallback-Kette (Release Date → Hierarchie → AddedAt)
- ❌ Paginierungslogik (Page Number, Page Size, Has Next Page)
- ❌ DTO `DtoPlaylistEntriesPagedResult` für paginierte Antworten
- ❌ API-Endpoint für paginierte Einträge (z. B. `GET /api/playlists/{id}/entries?pageNumber=1&pageSize=20`)
- ❌ Lizenz-/Zugriffs-Status im DTO für Ausgegrenzung von Inhalten
- ❌ Virtual-Scrolling Implementierung in `PlaylistDetail.razor`
- ❌ Tests für Paginierung, Sortierung nach Erscheinungsdatum und Lazy Loading

**Beachte:** Die Medien-Entitäten haben bereits alle erforderlichen Felder für Erscheinungsdatum (`ReleaseDate`, `PremieredAt`) und Hierarchie-Navigationen.

---

## Details

- [Datenmodelle](inventory/models.md)
- [Logikklassen / Services](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)

---

## Kritische Erkenntnisse für die Implementierung

### 1. Erscheinungsdatum-Quellen pro Medientyp

| Medientyp | Primary | Secondary | Fallback |
|-----------|---------|-----------|----------|
| `Movie` | `ReleaseDate` | `PremieredAt` | `CreatedAt` |
| `TVShowEpisode` | `ReleaseDate` (aus "aired" in NFO) | `PremieredAt` | Episode-Nummer in Staffel |
| `TVShowSeason` | `ReleaseDate` oder `PremieredAt` | Erstes Episode-Datum | Staffel-Nummer |
| `TVShow` | `PremieredAt` oder `ReleaseDate` | Erstes Episode-Datum | `CreatedAt` |
| `MovieCollection` | `CreatedAt` oder `ReleaseDate` | `PremieredAt` | `CreatedAt` |

Die Anforderung nennt diese Quellen teilweise als Fragen offen – diese Tabelle basiert auf den existierenden Datenmodellen.

### 2. Hierarchie-Informationen zur Fallback-Sortierung

Die `PlaylistEntry` Entität speichert bereits:
- `ParentMediaType` – Type der Sammlung, durch die dieser Eintrag hinzugefügt wurde
- `ParentMediaId` – ID der Sammlung

Dies ermöglicht die Fallback-Sortierung:
1. Nach Erscheinungsdatum
2. Nach Hierarchie-Typ (TVShow → TVShowSeason → TVShowEpisode)
3. Nach Episode-/Item-Sequenz innerhalb der Sammlung
4. Nach `AddedAt` (Aufnahmezeit in die Playlist)

### 3. Existing `MediaTypeHandler` Pattern ist erweiterbar

Die `PlaylistService` nutzt bereits ein flexibles Pattern über `MediaTypeHandler`:

```csharp
private sealed class MediaTypeHandler
{
    public required Func<...> LoadTitlesAsync { get; init; }
    public Func<...>? LoadCascadeChildrenAsync { get; init; }
}
```

Dieses Pattern kann einfach um weitere Properties erweitert werden:
- `LoadReleaseDateAsync` – lädt Erscheinungsdatum pro Medientyp
- `GetHierarchySequenceAsync` – lädt Hierarchie-Position (Staffel-Nr., Episode-Nr.)

### 4. Test-Infrastruktur ist gut vorbereitet

- `PlaylistServiceTestBase` bietet `CreateTestPlaylistWithEntriesAsync()` für schnelle Test-Setup
- `TestHelpers.CreateTvShowWithSeasonsAsync()` ermöglicht Erstellung von Serien mit Datum-Informationen
- Xunit + EF Core InMemory Database für konsistente Tests

### 5. Berechtigungsprüfung ist zentral

- `GetOwnedPlaylistAsync()` prüft Playlist-Ownership vor jeder Aktion
- Diese Prüfung ist auch in der neuen `GetPlaylistEntriesPagedAsync()` erforderlich
- `PlaylistAccessDeniedException` ist die Standard-Exception für Zugriffsverletzungen

---

## Notwendige Schritte zur Umsetzung von Schritt 3

1. **Service-Layer erweitern:**
   - `GetPlaylistEntriesPagedAsync()` zum Interface `IPlaylistService` hinzufügen
   - Implementation in `PlaylistService` mit Paginierung und Sortierung
   - `MediaTypeHandler` um `LoadReleaseDateAsync()` erweitern
   - `MediaTypeHandler` um Hierarchie-Sequenz-Laden erweitern

2. **DTOs erweitern:**
   - Neues DTO `DtoPlaylistEntriesPagedResult` (oder ähnlich) für paginierte Antworten
   - `DtoPlaylistEntry` um `IsAccessible` oder `LicenseStatus` Feld erweitern (falls Lizenz-Prüfung erforderlich)

3. **API-Endpoints erweitern:**
   - Neuer Endpoint `GET /api/playlists/{id}/entries?pageNumber=1&pageSize=20` in `PlaylistsController`
   - Alternativ: Existierenden Endpoint mit optionalen Query-Parametern erweitern

4. **UI-Komponente erweitern:**
   - `PlaylistDetail.razor` Lazy-Loading Logik hinzufügen
   - Virtual-Scrolling Komponente integrieren (z. B. Radzen Blazor, QuickGrid)
   - Visuelle Unterscheidung für ausgegrenzte Inhalte (opacity, Farbe, disabled Click-Handler)

5. **Tests hinzufügen:**
   - Unit-Tests für Paginierung
   - Unit-Tests für Sortierung nach Erscheinungsdatum mit Fallback-Ketten
   - Unit-Tests für Lizenz-Status / Zugriffsrechte
   - E2E-Tests für Lazy Loading und Virtual Scrolling

---

## Datei-Übersicht für die Implementierung

| Zu modifizierende Datei | Grund |
|-------------------------|-------|
| `VideoWebPlayer/Services/IPlaylistService.cs` | Neue Methode `GetPlaylistEntriesPagedAsync()` |
| `VideoWebPlayer/Services/PlaylistService.cs` | Implementation der neuen Methode + Erweiterung MediaTypeHandler |
| `VideoWebPlayer/Controllers/PlaylistsController.cs` | Neuer API-Endpoint für paginierte Einträge |
| `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` | Virtual-Scrolling + Lazy Loading |
| `VideoWebPlayer/Client/Models/Dto*.cs` | Neue/erweiterte DTOs |
| `VideoWebPlayer.Tests/Services/PlaylistServiceTests_*.cs` | Tests für Paginierung und Sortierung |
| `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_*.cs` | Tests für neuen API-Endpoint |
| `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs` | Tests für UI-Funktionalität |

---

## Konfiguration & Settings

Existierende Konfiguration:
- `VideoWebPlayer/Configuration/PlaylistSettings.cs` – `MaxPlaylistsPerUser`, `MaxPlaylistItemCount`

Für Schritt 3 könnten hinzugefügt werden:
- `DefaultPageSize` – Standard-Seitengröße für Infinity-List (z. B. 20)
- `SortingStrategy` – Konfigurierbare Sortierungslogik

---

## Annahmen aus dem Code

1. **Medien-Daten:** Alle Medien-Entitäten erben von `MediaBaseEntry` mit `ReleaseDate`, `PremieredAt` Feldern
2. **Hierarchie:** Navigations-Properties (`TVShow.Seasons`, `TVShowSeason.Episodes`, `Movie.MovieCollection`) sind im EF Core Mapping vorhanden
3. **Titles-Laden:** Existierende `MediaTypeHandler.LoadTitlesAsync()` pro Medientyp funktioniert zuverlässig
4. **Orphan-Bereinigung:** `PlaylistEntry` mit nicht-existierendem `MediaId` werden automatisch bereinigt (bestehende Logik)
5. **Berechtigungen:** Ownership-Prüfung über `Playlist.UserId == currentUser.Id` ist ausreichend

---

## Abhängigkeiten und Integrationspunkte

**Interne Abhängigkeiten:**
- `ApplicationDbContext` – EF Core Datenbankkontext
- `EventManager` – für zukünftige Events (z. B. PlaylistEntriesLoadedEvent)
- `IAuthService` – für Benutzer-ID und Authentifizierung
- `IPlaylistService` – zentrale Service-Schnittstelle

**Externe Abhängigkeiten (abhängig von Umsetzung):**
- Virtual-Scrolling Library (z. B. Radzen.Blazor, QuickGrid)
- Lizenz-Service (falls Zugriff-Prüfung erforderlich) – **aktuell nicht implementiert**

---

## Offene Fragen aus der Anforderung (unverändert)

1. Lizenzprüfung / Ausgegrenzung: Existiert bereits ein Service zur Prüfung von Benutzer-Lizenzen?
2. Virtual-Scrolling-Library: Welche wird verwendet?
3. Seitengröße / Performance: Standardseitengröße (20, 50, 100)?
4. Sortierung bei manuellen Modus (Schritt 4): Wird `DtoPlaylistEntry` um `SortOrder: int?` erweitert?
5. Ausgegrenzte Inhalte und Kaskadenlogik: Wie werden Episoden behandelt, wenn Serie nicht freigeschaltet?
6. Performance bei großen Playlists: Tests mit 10.000+ Einträgen erforderlich?

