# Bestandsaufnahme: Nachbesserung Entwicklungsschritt 6 – Korrektionen zum Weiterschauen mit Playlist-Bezug (Runde 1)

Diese Bestandsaufnahme analysiert den Code für die Nachbesserung von Entwicklungsschritt 6, der die Möglichkeit einführt, dass dasselbe Video mehrfach in der Weiterschauen-Liste erscheinen kann. Bei der Abnahmeprüfung wurden vier Fehler gefunden (zwei schwerwiegend): Dictionary-Schlüsselung, Blazor-Key-Kollision, Datenbankconstraint-Fehler beim Löschen und fehlende Wiedergabeposition.

## Zusammenfassung

### Was ist vorhanden

1. **Datenbankmodell und Konfiguration (teilweise):**
   - `ContinueWatchingEntry` mit eindeutiger ID (Primärschlüssel) in der Datenbank
   - Bedingte Unique-Indizes auf `(UserId, MovieId/TVShowEpisodeId, PlaylistId)` in `ContinueWatchingEntryConfiguration`
   - Zusätzliche Unique-Indizes für playlist-lose Einträge mit Filter `[PlaylistId] IS NULL`
   - Foreign-Key-Aktion auf Playlist mit `OnDelete(DeleteBehavior.SetNull)`

2. **Services und Logik (teilweise):**
   - `ContinueWatchingService.GetListAsync()` lädt die Weiterschauen-Liste und befüllt `ContinueWatchingDto`
   - `ContinueWatchingService.EnrichPlaylistInfoAsync()` befüllt `PlaylistName` und `PlaylistEntryId`
   - `PlaylistService.DeletePlaylistAsync()` löscht Playlists

3. **UI-Komponenten:**
   - `ContinueWatchingList.razor` mit Dictionaries und MediaBox-Rendering
   - `PlaylistDetail.razor` mit VideoPlayer-Integration

4. **Tests (bestanden):**
   - 585 Tests erfolgreich, 0 Fehler, 0 übersprungen
   - PlaylistService-Tests mit SQLite In-Memory (durchsetzt Constraints)
   - ContinueWatchingService-Tests mit EF-InMemory (durchsetzt Constraints NICHT)

### Was fehlt oder ist fehlerhaft

**Problem 1 – Fehlende `ContinueWatchingDto.Id`-Eigenschaft:**
- `ContinueWatchingDto` hat keine Eigenschaft `Id` für die Datenbank-ID der `ContinueWatchingEntry`
- `ContinueWatchingService.GetListAsync()` befüllt diese nicht
- Folge: Dictionaries in `ContinueWatchingList.razor` (Zeilen 18–21, 50–90) werden nach `it.Entry.Id` (Medien-ID) indiziert → Kollisionen bei mehreren Varianten desselben Videos

**Problem 2 – Blazor-Key-Duplikate:**
- `@key="it.Entry.Id"` in `ContinueWatchingList.razor` (Zeile 147) ist nicht eindeutig pro Datenbankzeile
- Führt zu Blazor-Circuit-Absturz mit "More than one sibling of component 'MediaBox' has the same key value"
- **Root Cause:** Direkte Folge von Problem 1

**Problem 3 – Unique-Index-Konflikt beim Playlist-Löschen:**
- `PlaylistService.DeletePlaylistAsync()` (Zeile 130–136) ruft blind `SaveChangesAsync()` auf
- Wenn eine Playlist gelöscht wird und `OnDelete(DeleteBehavior.SetNull)` die `PlaylistId` auf NULL setzt:
  - Kollidiert ein gerade auf NULL gesetzter Eintrag mit einem bereits existierenden playlist-losen Eintrag
  - Resultat: `DbUpdateException` mit "UNIQUE constraint failed"
- **Nicht durch bestehende Tests nachgewiesen:** `ContinueWatchingServiceTestBase` nutzt EF-InMemory, das keine Constraints durchsetzt

**Problem 4 – Fehlende Wiedergabeposition im Playlist-Kontext:**
- `DtoPlaylistPlaybackStart` hat keine Eigenschaft `StartPositionSeconds`
- `PlaylistDetail.razor` (Zeilen 86–97) übergibt keine Wiedergabeposition an `VideoPlayer`
- Im Gegensatz zu `ContinueWatchingList.razor` (Zeilen 83, 88), wo `PositionSeconds` als URL-Parameter übergeben wird
- Folge: Beim Fortsetzen über Playlist-Kontext beginnt Wiedergabe immer bei 0:00

### Test-Ausgangszustand

**Alle Tests bestanden im Ausgangslauf:**
- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -v quiet`
- Ergebnis: 585 erfolgreich, 0 Fehler, 0 übersprungen, Dauer 2 min 46 s
- Nachweis: [tests.md](inventory/tests.md#test-ausgangslauf)

**Testlücken:**
- Kein bUnit-Test für `ContinueWatchingList.razor` mit mehrfachen Varianten derselben Media (Problem 1 & 2)
- Kein SQLite-Integrationtest für `PlaylistService.DeletePlaylistAsync` mit konkurrierendem playlist-losem Eintrag (Problem 3)
- Kein Test für `DtoPlaylistPlaybackStart.StartPositionSeconds` (Problem 4)
- Strukturelle Lücke: `ContinueWatchingServiceTestBase` nutzt EF-InMemory, das keine Constraints durchsetzt

## Details

- [Datenmodelle](inventory/models.md)
- [Business Logic und Services](inventory/logic.md)
- [Tests und Testinfrastruktur](inventory/tests.md)

## Betroffene Dateien (Zusammenfassung)

| Kategorie | Datei | Probleme |
|-----------|-------|----------|
| DTO | `VideoWebPlayer.Client/Models/ContinueWatchingDto.cs` | Problem 1: Fehlende `Id`-Eigenschaft |
| DTO | `VideoWebPlayer.Client/Models/DtoPlaylistPlaybackStart.cs` | Problem 4: Fehlende `StartPositionSeconds`-Eigenschaft |
| UI | `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor` | Problem 1 & 2: Dictionary-Schlüsselung und @key-Duplikate (Zeilen 18–21, 50–90, 147) |
| UI | `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` | Problem 4: VideoPlayer ohne StartPositionSeconds (Zeilen 86–97) |
| Service | `VideoWebPlayer/Services/ContinueWatchingService.cs` | Problem 1: GetListAsync befüllt `Id` nicht (Zeilen 100–152) |
| Service | `VideoWebPlayer/Services/PlaylistService.cs` | Problem 3: DeletePlaylistAsync ohne Konfliktbehandlung (Zeilen 130–136) |
| Config | `VideoWebPlayer/Data/Configurations/ContinueWatchingEntryConfiguration.cs` | Problem 3: OnDelete(DeleteBehavior.SetNull) mit bedingten Unique-Indizes kollidiert (Zeile 51–55) |
| Tests | `VideoWebPlayer.Tests/Helpers/ContinueWatchingServiceTestBase.cs` | Strukturelle Lücke: EF-InMemory durchsetzt keine Constraints (Zeile 35–37) |
