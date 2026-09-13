# Bestandsaufnahme: Korrekturen Playlist-Wiedergabe (Schritt 5, Runde 2)

Diese Bestandsaufnahme analysiert die vorhandenen Implementierungen und Tests für die Anforderung "Korrektionen Playlist-Wiedergabe (Schritt 5)", die drei kritische Fehler in der Playlist-Wiedergabefunktion adressiert.

## Zusammenfassung

### Vorhandene Funktionalität
- **Wiedergabe-Navigation:** Die Grundmechanik der Playlist-Navigation (Next/Previous/Advance) funktioniert bereits
- **Auto-Advance:** Automatisches Weiterschalten am Ende eines Videos ist implementiert
- **Filterung:** Nicht-abspielbare Einträge (TVShow/Staffel/Sammlung) und nicht-zugängliche Einträge werden in Navigation korrekt übersprungen
- **Fehlerbehandlung:** Fehler bei Navigation werden in UI angezeigt
- **Zugriffsvalidierung:** Playlist-Eigentümerschaft wird validiert

### Identifizierte Fehler (Anforderung Punkt 1–3)

#### Punkt 1: Falsche Endeverarbeitung beim Navigieren am Anfang
- **Problem:** `VideoPlayer.ApplyPlaylistNavigationResultAsync` setzt `playlistEndReached = true` für ALLE `null`-Ergebnisse, egal ob Vorwärts- oder Rückwärtsnavigation
- **Datei:** `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor` Zeile ~313
- **Fehler-Nachweis:** Test `GetPreviousPlaylistEntryAsync_ReturnsNullAtBeginning` gibt `null` zurück (korrekt), aber UI zeigt "Ende der Playlist erreicht." (falsch)
- **Ursache:** Keine Unterscheidung der Navigationsrichtung in `ApplyPlaylistNavigationResultAsync`

#### Punkt 2: Abspielen gesperrter Einträge führt in Sackgasse
- **Problem 1:** "Abspielen"-Button wird auf nicht-zugänglichen Einträgen angeboten
- **Datei:** `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor` Zeile ~306–307
- **Fehler:** `IsPlayableEntry` prüft nur `PlaylistEntryMediaTypeResolver.IsPlayable`, nicht `entry.IsAccessible`
- **Problem 2:** `StartPlaybackAsync` bei 403-Fehler setzt `loadError` statt separaten `playbackError`
- **Datei:** `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` Zeile ~163
- **Fehler:** Fehler beim Starten der Wiedergabe wird als "Fehler beim Laden der Playlist" präsentiert
- **Problem 3:** Doppelklick auf gesperrte/nicht-abspielbare Zeilen ist nicht deaktiviert
- **Datei:** `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor` Zeile ~59
- **Fehler:** `@ondblclick` ist nicht conditional, funktioniert auf allen Zeilen

#### Punkt 3: Doppelklick auf Sammel-Einträge startet falschen Inhalt
- **Problem:** `PlaylistService.ResolveExplicitStartEntry` validiert nicht, ob Eintrag abspielbar ist
- **Datei:** `VideoWebPlayer/Services/PlaylistService.cs` Zeile ~924–934
- **Fehler:** Akzeptiert non-playable MediaTypes (TVShow, Staffel, Collection)
- **Vergleich:** `ResolveFirstPlayableEntry` (Zeile 943) prüft korrekt `PlaylistEntryMediaTypeResolver.IsPlayable`

### Test-Ausgangszustand

- **Branch:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-5-wiedergabe-aus-playlist`
- **Commit:** `1aebed0` (feat: Wiedergabe aus Playlist)
- **Test-Suites:** 18 Tests in `PlaylistServiceTests_Playback`, 1 Test in `PlaylistEntriesListTests`
- **Testabdeckung:** Existierende Tests decken Navigation und Filterung ab
- **Testlücken:** 
  - Keine Tests für `ResolveExplicitStartEntry` mit nicht-abspielbaren Einträgen
  - Keine Tests für `PlaylistDetail.StartPlaybackAsync` Fehlerbehandlung
  - Keine Tests für `IsPlayableEntry` mit `IsAccessible` Prüfung
  - Keine E2E-Tests für die drei Fehlerszenarien

Siehe [Test-Detaildokumentation](inventory/tests.md) für vollständige Test-Übersicht und Ausgangsergebnisse.

## Details

- [Datenmodelle](inventory/models.md) — Struktur und Eigenschaften von DtoPlaylistNavigationResult und DtoPlaylistEntry
- [Logik-Klassen und Komponenten](inventory/logic.md) — VideoPlayer, PlaylistService, PlaylistEntriesList, PlaylistDetail Implementierungen
- [Hilfsmechanismen und Utilities](inventory/helpers.md) — PlaylistEntryMediaTypeResolver und Exception-Typen
- [Tests und Testabdeckung](inventory/tests.md) — Test-Suiten, Testlücken und Ausgangszustand (552 Tests bestanden)
