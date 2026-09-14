# Tests – Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

### Basisdaten zum Test-Lauf

- **Zeitpunkt (mit Zeitzone):** 2026-09-14, 10:30 UTC (Testlauf läuft noch, wird nachträglich aktualisiert)
- **Branch und Commit-ID:** `task/issue-207-fd729906880143ee8539-fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-10-playlist-abbildungen` (Commit: 0ca2d00)
- **Uncommittete Änderungen im getesteten Stand:** Nur neues Verzeichnis `docs/features/task/` (leer, keine Codeänderungen)
- **Testumgebung:** Windows 11 Pro 10.0.26200, .NET 10.0, dotnet test mit TRX-Logger
- **Ermittelte Testsuiten und Quellen der Testbefehle:** 
  - Befehl: `dotnet test --logger "trx;LogFileName=TestResults.trx" --verbosity minimal`
  - VideoWebPlayer.Tests Projekt
  - MarkdownLinkCheck.Tests Projekt (Nebenprodukt)

### Bestehende Test-Klassen und Methoden (für Playlist-Feature relevant)

#### Unit- und Integration-Tests für Playlists

**VideoWebPlayer.Tests/Services/PlaylistBackfillServiceTests.cs**
- Tests für automatische Nachlieferung von Playlist-Einträgen
- Relevant für das Verständnis von Playlist-Entry-Abfragen

**VideoWebPlayer.Tests/Services/ContinueWatchingServicePlaylistTests.cs**
- Tests für Continue-Watching mit Playlist-Bezug
- Relevant für die Verwaltung von Playlist-übergreifenden Funktionen

#### bUnit-Tests (UI-Komponenten)

**VideoWebPlayer.Tests/Components/PlaylistDetailTests.cs**
- Tests für PlaylistDetail.razor Komponente
- Relevant für zukünftige UI-Tests des Cover-Upload-Dialogs

**VideoWebPlayer.Tests/Components/PlaylistEntriesListTests.cs**
- Tests für PlaylistEntriesList.razor Komponente

**VideoWebPlayer.Tests/Components/PlaylistDetailGenreTests.cs**
- Tests für Genre-Editor in PlaylistDetail
- Zeigt das Muster für Genre-Override-Funktionalität (Schritt 9)

#### E2E-Tests

**VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs**
- End-to-End Tests für Playlist-Detail-Navigation und -Verwaltung

**VideoWebPlayer.Tests/PlaylistsE2ETests.cs**
- E2E Tests für Playlists-Übersicht

**VideoWebPlayer.Tests/PlaylistEntriesE2ETests.cs**
- E2E Tests für Playlist-Eintrag-Verwaltung

**VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs**
- Tests für manuelle Umordnung von Einträgen

**VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs**
- Tests für Wiedergabe von Playlists

**VideoWebPlayer.Tests/PlaylistMediaSearchE2ETests.cs**
- Tests für Suche und Hinzufügen von Media zu Playlists

#### Tests für Hintergrund-Bildgenerierung (als Vorbild)

**VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGeneratorTests.cs**
- Unit-Tests für `EpisodeBackgroundImageGenerator`
- Relevant als Vorbild für zukünftige `PlaylistCoverGeneratorTests`

**VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageServiceTests.cs**
- Integration-Tests für `EpisodeBackgroundImageService`
- Zeigt Muster für Lazy-Generation und DB-Speicherung von generierten Bildern

**VideoWebPlayer.Tests/EpisodesBackgroundImageAccessTokenE2ETests.cs**
- E2E Tests für Zugriff auf Episode-Hintergrundbilder

**VideoWebPlayer.Tests/EpisodesControllerBackgroundImageTests.cs**
- Tests für EpisodesController Endpoints
- Vorbild für zukünftige Playlist-Cover-Endpoints

#### Weitere relevante Tests

**VideoWebPlayer.Tests/EpisodesControllerBackgroundImageTests.cs**
- Tests für Episode-Hintergrundbild-API Endpoints
- Zeigt das Pattern für Bild-Auslieferungs-Endpoints

### Testlücken und Ausführungsprobleme

Vor der Umsetzung von Schritt 10 gibt es folgende Testlücken:

1. **PlaylistCoverGeneratorTests** – NICHT VORHANDEN
   - Tests für Collage-Erzeugung aus Playlist-Einträgen
   - Benötigt: Unit-Tests für Prioritätsreihenfolge, Fallbacks, Minimal-/Maximal-Bildanzahl

2. **PlaylistServiceCoverTests** – NICHT VORHANDEN
   - Integration-Tests für Cover-Upload und -Generierung
   - Benötigt: Tests für Validierung, DB-Speicherung, Zugriffsprüfung

3. **PlaylistCoverValidatorTests** – NICHT VORHANDEN
   - Unit-Tests für Dateiformat- und -größen-Validierung

4. **PlaylistListTests / PlaylistDetailTests** (bUnit) – ERWEITERUNG ERFORDERLICH
   - Aktuelle Tests prüfen Cover-Funktionalität noch nicht
   - Benötigt: Tests für Cover-Anzeige, Upload-Dialog, Regenerierungs-Button

5. **BackupCompatibilityTests** – ERWEITERUNG ERFORDERLICH
   - Müssen um Tests für neue `Playlist.CoverPictureId` und `Playlist.CoverPictureIsUserUploaded` Spalten erweitert werden

6. **Keine Controller-Tests für Playlist-Cover-Endpoints**
   - Benötigt: Tests für `/api/playlists/{id}/cover/upload`, `/regenerate`, `GET`, `DELETE`

### Testlauf-Protokoll (abgeschlossen am 2026-09-14 ~14:45 UTC)

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test --logger "trx;LogFileName=TestResults.trx" --verbosity minimal` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 658 | 0 | 0 | [VideoWebPlayer.Tests.trx](test-results/VideoWebPlayer.Tests.trx), [MarkdownLinkCheck.Tests.trx](test-results/MarkdownLinkCheck.Tests.trx) |

**Detaillierte Ergebnisse:**
- **MarkdownLinkCheck.Tests**: 6 erfolgreiche, 0 Fehler, Dauer: 181 ms
- **VideoWebPlayer.Tests**: 652 erfolgreiche, 0 Fehler, Dauer: 2 m 42 s
- **Gesamt**: 658 erfolgreiche, 0 Fehler, 0 übersprungen
- **Gesamtdauer**: 2 m 42 s

### Nachgewiesene bestehende Testfehler

Keine Testfehler nachgewiesen. Alle Tests bestanden erfolgreich im Ausgangszustand.

