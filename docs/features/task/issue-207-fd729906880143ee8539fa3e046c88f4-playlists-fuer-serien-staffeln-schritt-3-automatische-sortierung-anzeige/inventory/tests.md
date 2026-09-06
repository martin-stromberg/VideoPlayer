# Bestandsaufnahme: Tests

## Testklassen

### `PlaylistDetailE2ETests`
Datei: `VideoWebPlayer.Tests\PlaylistDetailE2ETests.cs`

Erbt von: `PlaylistsE2ETestBase`

**Kategorie:** `[Trait("Category", "E2E")]`

| Testmethode | Was wird getestet |
|------------|-----------------|
| `Load_Detail_Page_Unauthenticated_Shows_Error()` | Fehleranzeige für unauthentifizierte Zugriffe |
| `Load_Detail_Page_ValidPlaylist_ShowsMetadata()` | Anzeige von Playlist-Metadaten (Name, Beschreibung, Sortierung, Datumsfelder) |
| `Open_Button_In_List_Navigates_To_Detail()` | Navigation vom Überblick zur Detail-Seite |
| `Detail_Page_Edit_Opens_Form_And_Saves()` | Bearbeitungs-Form funktioniert |
| `Detail_Page_Delete_Shows_Confirmation_And_Deletes()` | Lösch-Bestätigung und Durchführung |
| `Detail_Page_Back_Button_Navigates_To_List()` | Zurück-Navigation zur Liste |
| `Detail_Page_Foreign_Playlist_Shows_403_Error()` | Zugriffskontrolle (andere Benutzer sehen Fehler) |
| `Detail_Page_Nonexistent_Playlist_Shows_404_Error()` | 404-Fehler für nicht existente Playlists |
| `PlaylistDetail_LoadsFirstPage_OnInitialize()` | Virtualisierung: Erste Seite wird geladen (Zeile 159) |
| `PlaylistDetail_LoadsNextPage_OnScrollNearEnd()` | Virtualisierung: Nächste Seite wird beim Scrollen geladen (Zeile 177) |
| `PlaylistDetail_StopsLoading_WhenHasNextPageFalse()` | Virtualisierung: Stoppt bei letzter Seite (Zeile 206) |
| **`PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()`** | **Baseline-Test: Aktuell sind ALLE Einträge freigeschaltet, keiner sollte `.opacity-50` haben** (Zeile 227) |

**Wichtiger Test für Anforderung (Zeile 227-241):**
```csharp
[Fact]
public async Task PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()
{
    // ... Setup mit 3 Filmen
    
    // Assertion: Prüft dass KEINE Zeile die Klasse .opacity-50 hat
    await Expect(Page.Locator(".playlist-entry-row.opacity-50")).ToHaveCountAsync(0);
}
```

**Kommentar im Test (Zeile 223-225):**
> "Verifies the baseline appearance of accessible entries: since license/access checking is not yet implemented, `DtoPlaylistEntry.IsAccessible` is currently always `true`, so no entry is expected to carry the reduced-opacity styling reserved for inaccessible content."

---

### `PlaylistEntriesE2ETests`
Datei: `VideoWebPlayer.Tests\PlaylistEntriesE2ETests.cs`

| Testmethode | Was wird getestet |
|------------|-----------------|
| `AddMovie_HappyPath_AppearsInList()` | Film hinzufügen und in Liste anzeigen |
| `RemoveMovie_HappyPath_DisappearsFromList()` | Film entfernen und aus Liste verschwinden |
| `AddMovie_Duplicate_ShowsSuccessMessage()` | Duplikat-Handling mit Erfolgsmitteilung |
| `AddTVShow_CascadesSeasonsAndEpisodes()` | Kaskaden-Logik: TV-Show mit allen Staffeln und Episoden hinzufügen |

**Hilfsmethode:**
- `AddEntryViaUiAsync(string mediaType, long mediaId)` - Allgemeine Methode zum Hinzufügen von Einträgen über UI

---

### `PlaylistsE2ETests`
Datei: `VideoWebPlayer.Tests\PlaylistsE2ETests.cs`

Tests für Playlist-CRUD-Operationen auf der Übersichtsseite.

---

## Hilfsmethoden und Test-Fixtures

### Basisklasse: `PlaylistsE2ETestBase`

**Setup-Methoden:**
- `CreatePlaylistViaUiAsync(string name, string? description)` - Erstellt Playlist über UI
- `SeedMoviesIntoPlaylistAsync(string playlistName, int count)` - Fügt mehrere Filme zur Playlist hinzu (für Virtualisierungs-Tests)
- `SeedMovieAsync(string movieName)` - Erstellt einzelnen Testfilm
- `SeedTvShowWithSeasonsAsync(string showName, params (string seasonName, int episodeCount)[])` - Erstellt Serie mit Staffeln und Episoden

**Erbe von (wahrscheinlich):**
- `PlaywrightTestBase` oder ähnlich (Playwright E2E-Testing)

---

## Weitere Test-Dateien mit Bezug zu Freischaltung

### `UnlockedMediaE2ETests`
Datei: `VideoWebPlayer.Tests\UnlockedMediaE2ETests.cs`

Tests für den `IUnlockedMediaService` Freischaltungsmechanismus. Diese Tests zeigen:
- Wie Freischaltungen in der DB gesetzt werden
- Wie die UI auf Freischaltungs-Status reagiert
- Könnte als Vorlage für Tests zur Playlist-Freischaltung dienen

### `UnlockedSourceE2ETests`
Datei: `VideoWebPlayer.Tests\UnlockedSourceE2ETests.cs`

Tests für Quellen-Freischaltungen (nicht direkt relevant für Playlists, aber zeigt Freischaltungs-Architektur).

---

## Test-Strategie für Anforderung Phase 2 (Freischaltung)

Basierend auf bestehenden Tests sollte der angepasste Test:

1. **Neue Freischaltung hinzufügen:**
   - Eine Playlist mit mehreren Einträgen erstellen
   - Mindestens einen Eintrag für den aktuellen Benutzer NICHT freigeschalten
   - Dies erfordert Datenbank-Manipulation im Test (Freischaltung entfernen für einen Eintrag)

2. **Ausgrau-Verhalten prüfen:**
   ```csharp
   // Prüfe: Freigeschaltete Einträge haben NICHT .opacity-50
   await Expect(Page.Locator(".playlist-entry-row[data-media-id='X']"))
       .Not.ToHaveClassAsync(new Regex("opacity-50"));
   
   // Prüfe: Nicht freigeschaltete Einträge HABEN .opacity-50
   await Expect(Page.Locator(".playlist-entry-row[data-media-id='Y']"))
       .ToHaveClassAsync(new Regex("opacity-50"));
   ```

3. **Button-Status prüfen:**
   - Nach Anforderung Phase 3 sollte der "Entfernen"-Button NICHT mehr deaktiviert sein
   - Test anpassen: `disabled` sollte nicht mehr an `IsAccessible` gebunden sein

---

## Hinweise für Implementierung

1. **Test-Datenbank-Setup:**
   - Bestehende Test-Fixtures können als Vorlage dienen
   - `SeedMovieAsync()` etc. sind verfügbar
   - Neue Methode zum Setzen von Freischaltungen in Test-Helper notwendig

2. **Playwright-Selektoren:**
   - `.playlist-entry-row[data-media-id='X']` - Targeting nach Zeile
   - `.opacity-50` - CSS-Klassen-Selektor
   - `.playlist-entry-remove-button` - Button-Selektor

3. **Aktueller Baseline-Test ist korrekt:**
   - Der Test `PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()` prüft den aktuellen Zustand
   - Nach Implementierung der Freischaltungs-Logik wird ein zusätzlicher Test nötig, der mindestens einen nicht freigeschalteten Eintrag prüft
