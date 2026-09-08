# Anforderung – Schritt 5: Wiedergabe aus einer Playlist heraus

## Fachliche Zusammenfassung

Das System soll es Anwendern ermöglichen, Titel aus einer Playlist heraus abzuspielen, wobei der Playlist-Kontext während der Wiedergabe sichtbar bleibt. Die Wiedergabe unterstützt automatisches Weiterschalten zum nächsten Titel beim Ende des aktuellen sowie manuelles Navigieren zum nächsten oder vorherigen Titel. Dabei wird stets die aktuell gültige Sortierreihenfolge der Playlist beachtet, Titel ohne Freischaltung werden übersprungen, und die Wiedergabe endet ohne Fehler beim Erreichen des Endes.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- **`Playlist`** (bestehend, Erweiterung)
  - Neue Eigenschaft: `CurrentEntryId` (long?, nullable) – ID des aktuell abgespielten oder zuletzt abgespielten `PlaylistEntry` (wird genutzt, um die aktuelle Position beim Fortsetzen oder manuellen Weiterschalten zu bestimmen)
- **`PlaylistEntry`** (bestehend, keine neuen Spalten nötig — siehe Annahmen)

### Logikklassen / Services
- **`PlaylistService`** (bestehend, Erweiterung)
  - Neue Methode: `GetNextPlaylistEntryAsync(playlistId, currentEntryId)` – Liefert den nächsten abzuspielenden `PlaylistEntry` basierend auf der aktuellen Sortierreihenfolge; überspringt Einträge ohne Freischaltung
  - Neue Methode: `GetPreviousPlaylistEntryAsync(playlistId, currentEntryId)` – Liefert den vorherigen abzuspielenden `PlaylistEntry`
  - Neue Methode: `StartPlaylistAsync(playlistId, entryId?)` – Initiiert die Wiedergabe einer Playlist ab einem spezifischen oder dem ersten Eintrag; gibt den Playlist-Kontext und den Starttitel zurück
  - Neue Methode: `AdvancePlaylistAsync(playlistId, currentEntryId)` – Wird aufgerufen, wenn ein Titel automatisch zu Ende geht; liefert den nächsten Eintrag oder `null`, wenn das Ende erreicht ist

- **`IMediaPlaybackService`** (bestehend, neue Überladung oder neue Schnittstellenmethode)
  - Neue Methode oder Überladung: `PlayMediaAsync(playlistEntryId, playlistContext?)` – Erweitert die bestehende Wiedergabefunktion um optionalen Playlist-Kontext (Playlist-ID, aktuelle Position innerhalb Playlist)

### Interfaces
- **`IPlaylistContextProvider`** (neu) – Verwaltet den aktuellen Playlist-Kontext während einer Wiedergabe-Sitzung
  - Methode: `SetPlaylistContext(playlistId, currentEntryId, totalCount)` – Setzt den aktiven Playlist-Kontext
  - Methode: `GetPlaylistContext()` – Liefert den aktuellen Kontext oder `null`, wenn nicht aus Playlist gespielt wird
  - Methode: `ClearPlaylistContext()` – Löscht den Kontext (z. B. beim Starten eines Videos außerhalb einer Playlist)

### UI-Komponenten
- **`PlaylistDetail.razor`** (bestehend, Erweiterung)
  - Neue Aktion: Doppelklick auf Eintrag oder dedizierter Play-Button startet Wiedergabe aus dieser Position
  - Neue Anzeige: Aktuelle Titel-Position innerhalb Playlist (z. B. „Titel 3 von 12")
  
- **`VideoPlayer.razor`** oder entsprechender Wiedergabe-Component (bestehend, Erweiterung)
  - Neue Anzeige: Playlist-Badge mit Playlist-Name und Position (z. B. „[Meine Favoriten: 3/12]")
  - Neue Schaltflächen: „Nächster (Playlist)" und „Vorheriger (Playlist)" neben oder als Alternative zu globalen Navigationsschaltflächen
  - Neue Aktion: Behandlung von Titel-Ende-Ereignis mit automatischem Weiterschalten zur nächsten Position

- **`PlaylistsController`** (bestehend, neue Endpunkte)
  - POST `/api/playlists/{id}/play?entryId={entryId}` – Startet Wiedergabe einer Playlist ab einem Eintrag
  - POST `/api/playlists/{id}/play/next` – Navigiert zum nächsten Titel
  - POST `/api/playlists/{id}/play/previous` – Navigiert zum vorherigen Titel

### Tests
- **Unit-Tests für `PlaylistService`**
  - Testen von `GetNextPlaylistEntryAsync` mit verschiedenen Sortierungen (Erscheinungsdatum, manuell) und Freischaltungen
  - Testen von `GetPreviousPlaylistEntryAsync`
  - Testen des Übergangs zu `null` beim Erreichen des Endes
  - Testen des Überspringens nicht freigeschalteter Titel

- **Unit-Tests für `IPlaylistContextProvider`**
  - Testen des Setzen/Abrufen/Löschens des Kontextes

- **Integrations- / E2E-Tests**
  - Wiedergabe aus Playlist starten, Kontext-Anzeige verifizieren
  - Manuelles Weiterschalten und Kontextveränderung
  - Automatisches Weiterschalten bei Titel-Ende
  - Überspringen nicht freigeschalteter Titel
  - Endeverhalten (keine Fehlermeldung)

## Implementierungsansatz

### Abhängigkeiten von bereits bestehenden Komponenten

1. **Sortierreihenfolge (`GetOrderedEntriesAsync`):** Die bereits implementierte Methode des `PlaylistService` liefert Einträge in der gültigen Sortierreihenfolge. Diese wird unverändert für das Ermitteln des nächsten/vorherigen Eintrags genutzt.

2. **Freischaltungsprüfung (`PlaylistEntryAccessResolver`, `MediaHierarchyRegistry`, `IUnlockedMediaService`):** Die zentrale Freischaltungslogik wird bei der Auswahl des nächsten Titels genutzt; Titel ohne Zugriff werden übersprungen.

3. **Einzeltitel-Wiedergabe:** Es gibt bereits eine allgemeine Methode zum Abspielen einzelner Medieninhalte. Diese wird erweitert oder gehüllt mit Playlist-Kontextinformationen.

### Technischer Vorgang

1. **Start einer Playlist-Wiedergabe:**
   - Anwender wählt einen Titel in der Playlist-Detailansicht aus (Doppelklick oder Play-Button)
   - Der `PlaylistsController`-Endpunkt `/playlists/{id}/play?entryId={entryId}` wird aufgerufen
   - Der `PlaylistService.StartPlaylistAsync()` wird aufgerufen, der:
     - Die Berechtigung prüft (nur der Besitzer)
     - Den Eintrag prüft (muss in dieser Playlist existieren)
     - Den Kontext setzt: `IPlaylistContextProvider.SetPlaylistContext(playlistId, entryId, totalCount)`
     - Den Eintrag auf Freischaltung prüft; wenn nicht freigeschaltet, den nächsten freigeschalteten Titel sucht
     - Eine `DtoPlaylistPlaybackStart` mit Playlist-Name, Position und ersten Titel zurückgibt

2. **Während der Wiedergabe:**
   - Der Video-Player zeigt den Playlist-Badge an, der den Namen und die Position der Playlist anzeigt (z. B. „[Meine Favoriten: 3/12]")
   - Der Kontext wird über `IPlaylistContextProvider` bereitgestellt (kann von beliebigen UI-Komponenten abgerufen werden)

3. **Manuelles Weiterschalten (Benutzer klickt „Nächster"):**
   - Der Benutzer klickt die Schaltfläche „Nächster (Playlist)" im Video-Player
   - Der Endpunkt POST `/playlists/{id}/play/next` wird aufgerufen
   - Der `PlaylistService.GetNextPlaylistEntryAsync(playlistId, currentEntryId)` wird aufgerufen, der:
     - Die geordnete Liste aller Einträge abruft
     - Die aktuelle Position findet
     - Alle nachfolgenden Einträge durchläuft und den ersten freigeschalteten Eintrag zurückgibt
     - `null` zurückgibt, wenn kein weiterer Eintrag vorhanden ist
   - Der Kontext wird aktualisiert
   - Der neue Titel wird abgespielt (über die bestehende Einzeltitel-Wiedergabe)

4. **Automatisches Weiterschalten (Titel endet):**
   - Das Video-Ende-Ereignis wird erkannt (über den bestehenden Player)
   - Ein Rückruf (`onMediaEnd` oder ähnlich) wird ausgelöst
   - Der `PlaylistService.AdvancePlaylistAsync(playlistId, currentEntryId)` wird aufgerufen, der intern `GetNextPlaylistEntryAsync()` nutzt
   - Der nächste Titel wird automatisch abgespielt; wenn kein weiterer vorhanden, stoppt die Wiedergabe ohne Fehlermeldung

5. **Vorgänger-Navigation:**
   - Analog zu „Nächster", aber mit `GetPreviousPlaylistEntryAsync()`

6. **Wiedergabe außerhalb einer Playlist:**
   - Wenn ein Video direkt (z. B. aus der Medienbibliothek) abgespielt wird, wird `IPlaylistContextProvider.ClearPlaylistContext()` aufgerufen oder gar nicht erst gesetzt
   - Die bestehende Einzeltitel-Wiedergabe läuft unverändert ab

### Verarbeitung von Sammel-Einträgen (Serien, Staffeln, Filmsammlungen)

**Klarstellung nötig:** Laut Projektplan wurden beim Hinzufügen (Schritt 2) Sammel-Einträge zu ihren Einzeltiteln aufgelöst und diese mit Parent-Referenz gespeichert. Für die Wiedergabe bedeutet dies:

- **Annahme:** Direkt in der Playlist gespeicherte Sammel-Einträge (z. B. ein `PlaylistEntry` mit `MediaType="TVShow"`) können als Blattknoten betrachtet werden; sie sind **nicht** selbst abspielbar
- **Verhalten:** Wenn beim Weiterschalten ein Sammel-Eintrag erreicht wird, wird er übersprungen (wie nicht freigeschaltete Titel)
- **Bestandsaufnahme erforderlich:** Es ist zu prüfen, ob Schritt 2 bereits garantiert, dass nur einzelne, abspielbare Titel (Filme, Episoden) in Playlists enden, oder ob Sammel-Einträge theoretisch noch vorhanden sein können

## Konfiguration

Konfigurierbare Parameter (über `appsettings.json` oder `ApplicationSettings`):

- **`Playlists:PlaybackAutoAdvanceTimeoutMs` (int, optional, Default: 500):** Verzögerung (in Millisekunden) zwischen Erkennung des Titel-Endes und automatischem Start des nächsten Titels, um sicherzustellen, dass der Player vollständig gestoppt hat

- **`Playlists:SkipUnlockedEntries` (bool, optional, Default: true):** Steuert, ob nicht freigeschaltete Titel automatisch übersprungen werden (true) oder die Wiedergabe stoppt (false)

## Offene Fragen / Klarifikationspunkte

1. **Sammel-Einträge in Playlists:**
   - Können Serien, Staffeln oder Filmsammlungen als direkte `PlaylistEntry`-Elemente vorkommen, oder werden sie garantiert bei der Eingabe (Schritt 2) zu ihren Einzeltiteln aufgelöst?
   - Wenn sie vorkommen: Wie soll die Wiedergabe mit ihnen umgehen? (Übersprung, Fehler, oder Expansion zur Laufzeit?)

2. **Playlist-Kontext-Persistierung:**
   - Soll der aktuelle Wiedergabezustand einer Playlist (welcher Titel gerade abgespielt wird) in der Datenbank oder Session gespeichert werden, um beim Neuladen / Seitenwechsel wiederhergestellt zu werden?
   - Oder ist der Kontext nur für die Dauer einer Browsersession aktiv?

3. **"Nächster Titel"-Verhalten bei Freischaltungsänderungen:**
   - Wenn ein Benutzer während einer laufenden Wiedergabe Zugriff auf einen bislang nicht freigeschalteten Titel verliert (z. B. Freischaltung entzogen), soll der übersprungen werden?
   - Wird die Freischaltung bei jeder Navigation erneut aktuell geprüft oder gecacht?

4. **Vorheriger Titel — Rückwärtsumsortierung:**
   - Die Implementierung von `GetPreviousPlaylistEntryAsync()` erfordert, dass die Einträge in umgekehrter Reihenfolge durchlaufen werden. Dies ist technisch klar, wird aber bestätigt, dass dies gewünscht ist und keine Bedingung wie ein minimales Zeitfenster für das Weiterschalten nach hinten besteht.

5. **Ende der Playlist:**
   - Wenn die Playlist zu Ende geht, soll nur die Wiedergabe stoppen, oder auch der Playlist-Badge verschwinden?
   - Können Benutzer nach dem Ende einen anderen Titel aus derselben Playlist abspielen (d. h. sie neu starten)?

6. **Auftritt nicht abzuspielender Einträge:**
   - Falls Sammel-Einträge in der Playlist vorkommen und nicht übersprungen werden können, wie soll der Benutzer darauf aufmerksam gemacht werden, dass ein Titel „existiert, aber nicht abspielbar ist"? (Visuelle Markierung, Meldung beim Weiterschalten, Überspringen?)
