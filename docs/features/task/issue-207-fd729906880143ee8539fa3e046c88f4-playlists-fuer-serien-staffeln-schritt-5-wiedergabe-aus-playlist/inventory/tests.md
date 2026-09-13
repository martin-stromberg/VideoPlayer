# Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-13T13:00:00 UTC
- **Branch:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-5-wiedergabe-aus-playlist`
- **Commit-ID:** `1aebed0` (feat: Wiedergabe aus Playlist)
- **Uncommittete Änderungen:** Zwei untracked Dateien in `docs/features/task/` und `docs/projects/task/`
- **Testumgebung:** .NET 10.0
- **SDK-Version:** .NET SDK 10.0

### Ermittelte Testsuiten und Quellen der Testbefehle

- **Testtool:** xUnit (über `dotnet test`)
- **Projekt:** `VideoWebPlayer.Tests`
- **Befehl:** `dotnet test VideoWebPlayer.Tests --logger "console" --verbosity normal`
- **Arbeitsverzeichnis:** `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4`

### Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Dauer | Nachweis |
|------|--------|-------------------|-----------|-------------|----------------|--------------|-------|----------|
| 1 | dotnet test VideoWebPlayer.Tests --logger "console" --verbosity minimal | D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4 | 0 | 552 | 0 | 0 | 2 m 48 s | [test-run-output.log](test-results/test-run-output.log) |

**Status:** Alle Tests bestanden erfolgreich

### Nachgewiesene bestehende Testfehler

**Keine Testfehler nachgewiesen.** Alle 552 Tests bestanden erfolgreich im Ausgangszustand. 

**Wichtig für die Anforderung:** Dies bedeutet NICHT, dass die in der Anforderung beschriebenen Fehler nicht existieren. Die existierenden Tests decken die fehlerhaften Szenarien (Punkte 1–3) nicht ab:
- Keine Tests prüfen, dass "Vorheriger am Anfang" KEINE "Ende erreicht"-Meldung zeigt
- Keine Tests prüfen, dass Doppelklick auf nicht-abspielbare/gesperrte Zeilen keinen falschen Content startet
- Keine Tests prüfen `ResolveExplicitStartEntry` mit nicht-abspielbaren Einträgen (TVShow, Staffel, Collection)

---

## Testklassen

### `PlaylistServiceTests_Playback`
**Datei:** `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Playback.cs`

| Test-Methode | Was wird getestet |
|--------------|-------------------|
| `GetNextPlaylistEntryAsync_ByReleaseDateMode_ReturnsNextUnlockedEntry` | Nächster Eintrag wird korrekt zurückgegeben im Release-Date-Modus |
| `GetNextPlaylistEntryAsync_ManualMode_ReturnsNextUnlockedEntry` | Nächster Eintrag wird korrekt zurückgegeben im Manual-Modus |
| `GetNextPlaylistEntryAsync_SkipsCollectionEntries` | Collection-Einträge (TVShow/Staffel/MovieCollection) werden übersprungen |
| `GetNextPlaylistEntryAsync_SkipsLockedEntries` | Gesperrte Einträge werden übersprungen |
| `GetNextPlaylistEntryAsync_ReturnsNullAtEnd` | Gibt null zurück, wenn am Ende der Playlist |
| `GetNextPlaylistEntryAsync_CurrentEntryNotInPlaylist_ThrowsInvalidOperationException` | Wirft Exception wenn aktueller Eintrag nicht in Playlist |
| `GetPreviousPlaylistEntryAsync_ReturnsPreviousUnlockedEntry` | Vorheriger Eintrag wird korrekt zurückgegeben |
| `GetPreviousPlaylistEntryAsync_SkipsCollectionAndLockedEntries` | Überspringt Collection und gesperrte Einträge rückwärts |
| `GetPreviousPlaylistEntryAsync_ReturnsNullAtBeginning` | Gibt null zurück, wenn am Anfang der Playlist |
| `StartPlaylistAsync_ValidatesPlaylistOwnership` | Validiert Eigentümerschaft der Playlist |
| `StartPlaylistAsync_PlaylistNotFound_ThrowsKeyNotFoundException` | Wirft Exception wenn Playlist nicht gefunden |
| `StartPlaylistAsync_ValidatesEntryBelongsToPlaylist` | Validiert dass Eintrag zur Playlist gehört |
| `StartPlaylistAsync_ExplicitEntryNotAccessible_ThrowsPlaylistAccessDeniedException` | Wirft Exception wenn angefordeter Eintrag nicht zugänglich ist |
| `StartPlaylistAsync_StartsAtFirstIfNoEntryIdProvided` | Startet beim ersten Eintrag wenn keine ID angegeben |
| `StartPlaylistAsync_NoPlayableAccessibleEntries_ThrowsInvalidOperationException` | Wirft Exception wenn keine abspielbaren Einträge |
| `StartPlaylistAsync_ReturnsPlaybackStartDTO` | Gibt korrekte Wiedergabe-Start-Informationen zurück |
| `AdvancePlaylistAsync_CallsGetNextInternally` | Ruft intern GetNext auf |
| `AdvancePlaylistAsync_ReturnsNextEntryOrNull` | Gibt nächsten Eintrag oder null zurück |

**Status:**
- Alle Tests auf dem Ausgangszustand ausgeführt. Ergebnisse werden aktualisiert.
- **Relevant für Anforderung:** `StartPlaylistAsync_ExplicitEntryNotAccessible_ThrowsPlaylistAccessDeniedException` prüft nur Zugriffsrecht, nicht Abspielbarkeit

### `PlaylistEntriesListTests`
**Datei:** `VideoWebPlayer.Tests/Components/PlaylistEntriesListTests.cs`

| Test-Methode | Was wird getestet |
|--------------|-------------------|
| `PlaylistEntriesList_OnMediaSelectedAsync_CallsAddEntryAsync` | Integration: Media-Auswahl triggert AddEntry-API |

**Status:**
- Nur ein Test vorhanden (Integration Test)
- **Relevant für Anforderung:** Kein Test für `IsPlayableEntry` mit `IsAccessible` Prüfung

---

## Hilfsmethoden und Test-Fixtures

### `PlaylistServiceTestBase`
**Datei:** `VideoWebPlayer.Tests/Services/PlaylistServiceTestBase.cs` (indirekt über Vererbung)

Stellt Hilfsmethoden für Test-Setup bereit:
- `CreatePlaylistWithMultipleSortOrdersAsync` – Erstellt Playlist mit mehreren Einträgen
- `CreateTestPlaylistWithMixedEntriesAsync` – Erstellt Playlist mit gemischten Media-Typen (Movie, TVShow, Episode, etc.)
- `CreateTestPlaylistWithEntriesAsync` – Erstellt Playlist mit spezifischen Einträgen
- `CreateTestMediaEntryAsync` – Erstellt Test-Media-Eintrag
- `GrantMediaSourceAccessForUserAsync` – Gewährt Benutzerzugriff auf Media-Source

---

## Testlücken und Ausführungsprobleme

### Fehlende Tests

Basierend auf der Anforderung sind folgende Tests **NICHT VORHANDEN**:

1. **PlaylistServiceTests_Playback.cs:**
   - Test: `StartPlaylistAsync` mit nicht-abspielbarer Eintrag-ID (TVShow/Staffel/Sammlung)
   - Erwartetes Verhalten: Wirft InvalidOperationException, nicht PlaylistAccessDeniedException
   - Status: **NICHT IMPLEMENTIERT** – wird als kritisch für Punkt 3 der Anforderung benötigt

2. **PlaylistDetailTests.cs:**
   - Test-Klasse existiert **NICHT**
   - Erforderlich: Tests für `StartPlaybackAsync` Fehlerbehandlung
   - Erwartete Tests:
     - `StartPlaybackAsync_WithHttpError403_SetPlaybackError` – Prüft dass Fehler in `playbackError` gespeichert wird
     - `StartPlaybackAsync_ErrorHandling_DoeNotAffectLoadError` – Prüft dass `loadError` unverändert bleibt

3. **PlaylistEntriesListTests.cs:**
   - `IsPlayableEntry` mit `IsAccessible == false` – Test fehlt
   - `IsPlayableEntry` mit nicht-abspielbarem MediaType – Test könnte erweitert werden
   - `ondblclick` Verhalten auf nicht-abspielbaren Zeilen – Test fehlt

4. **E2E-Tests:**
   - `VideoPlayerPreviousAtStartE2ETest` – Test fehlt
   - `PlaylistLockedEntryPlayButtonE2ETest` – Test fehlt
   - `PlaylistDoubleClickCollectionEntryE2ETest` – Test fehlt

### Testausführung Status

✅ **Erfolgreich abgeschlossen:** 2026-09-13 13:03:xx UTC
- **Befehl:** `dotnet test VideoWebPlayer.Tests --logger "console" --verbosity minimal`
- **Ergebnis:** 552 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen
- **Dauer:** 2 Minuten 48 Sekunden
- **Exit Code:** 0 (Erfolg)
