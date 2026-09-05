# Anforderungsübersetzung: Playlist "Öffnen"-Aktion und Detailseite ergänzen

## Fachliche Zusammenfassung

Die Playlist-Verwaltung von Entwicklungsschritt 1 ist zu ergänzen um eine Detailansicht für einzelne Playlists. Bislang können Anwender Playlists aus der Übersicht (PlaylistsList.razor) nur bearbeiten oder löschen; eine Öffnen-Aktion und eine entsprechende Detail-Route fehlen. Die neue Detailseite zeigt die Stammdaten einer Playlist (Name, Beschreibung, Sortiermodus) an und ermöglicht von dort aus das Bearbeiten und Löschen. Die Anzeige der Inhalte einer Playlist (Titel-Liste, automatische Sortierung, Infinity-List) bleibt bewusst ausgespart und ist Gegenstand eines späteren Entwicklungsschritts.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- **`VideoWebPlayer/Components/Playlists/PlaylistsList.razor`** (Erweiterung)
  - Ergänzung einer "Öffnen"-Aktion pro Zeile (zusätzlich zu "Bearbeiten" und "Löschen")
  - Navigation zur neuen Detail-Route beim Klick auf "Öffnen"

- **`VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`** (neue Komponente)
  - Route: `@page "/playlists/{id:long}"`
  - Anzeige der Stammdaten: Name, Beschreibung, Sortiermodus, Erstellungs- und Aktualisierungszeitpunkt
  - Buttons für "Bearbeiten" und "Löschen" analog zur PlaylistsList
  - Navigation zurück zur Playlist-Übersicht
  - Fehlerbehandlung für nicht existierende oder fremde Playlists (403 Zugriff verweigert)

### Client-Service
- **`VideoWebPlayer.Client/VideoWebPlayerClient.cs`** (evtl. Erweiterung)
  - Die Methode `RequestPlaylistAsync(long playlistId)` existiert bereits und wird wiederverwendet
  - Keine Änderung erforderlich, sofern die Methode den Zugriff auf fremde Playlists bereits mit 403 ablehnt

### Backend (bereits vorhanden)
- **`VideoWebPlayer/Controllers/PlaylistsController.cs`**
  - Der Endpoint `GetPlaylist(long id)` (Route: `GET /api/playlists/{id}`) existiert bereits
  - Rückgabe: `200 OK` mit `DtoPlaylist` bei erfolgreicher Authentifizierung und Autorisierung
  - Rückgabe: `403 Forbidden` bei Zugriff auf fremde Playlist
  - Rückgabe: `401 Unauthorized` bei fehlender Anmeldung

### DTOs und Modelle
- **`VideoWebPlayer.Client.Models/DtoPlaylist`** (bereits vorhanden)
  - Enthält: `Id`, `Name`, `Description`, `SortMode`, `CreatedAt`, `UpdatedAt`
  - Keine Änderung erforderlich

### Datenbankmodell
- **`VideoWebPlayer/Data/Playlist`** (keine Änderung erforderlich)

### Tests
- Evtl. E2E-Tests für die neue Detail-Route in `VideoWebPlayer.Tests/PlaylistsE2ETests.cs` (oder ähnlich)

## Implementierungsansatz

### Vorgehen

1. **Neue Razor-Komponente `PlaylistDetail.razor` erstellen:**
   - Route: `@page "/playlists/{id:long}"` (mit long-Konvertierung für die ID)
   - Im `OnInitializedAsync()`:
     - Authentifizierung prüfen (analog zu PlaylistsList)
     - Playlist laden via `Client.RequestPlaylistAsync(id)`
     - Fehlerbehandlung: Bei 403 (fremde Playlist) oder 401 (nicht angemeldet) entsprechende Meldung anzeigen
   - Anzeige der Stammdaten in lesender Form (kein Formular)
   - Buttons für "Bearbeiten" (öffnet PlaylistForm modal) und "Löschen" (analog zu PlaylistsList)
   - Button für "Zurück zur Übersicht"

2. **PlaylistsList.razor erweitern:**
   - Zusätzlicher Button "Öffnen" neben "Bearbeiten" und "Löschen"
   - Beim Klick: Navigation via `NavigationManager.NavigateTo($"/playlists/{playlist.Id}")`
   - Die bestehenden Buttons "Bearbeiten" und "Löschen" bleiben unverändert

3. **Validierung und Fehlerbehandlung:**
   - Bei HTTP-Status 403 (Zugriff verweigert): Meldung "Zugriff auf diese Playlist verweigert" anzeigen
   - Bei HTTP-Status 401 (nicht angemeldet): Anwender zur Anmeldung leiten
   - Bei anderen Fehlern: Generische Fehlermeldung anzeigen

### Abhängigkeiten
- `Microsoft.AspNetCore.Components.NavigationManager` (für Navigation)
- Bestehende Client-Methode `RequestPlaylistAsync` im `VideoWebPlayerClient`
- Bestehende Backend-Endpoint `GET /api/playlists/{id}` im `PlaylistsController`

### Wiederverwendung bestehender Mechanismen
- Das Löschen von der Detail-Seite kann den bestehenden `Client.DeletePlaylistAsync(id)` nutzen (wie in PlaylistsList)
- Das Bearbeiten von der Detail-Seite öffnet die bestehende `PlaylistForm`-Komponente (wie in PlaylistsList)
- Authentifizierungsmuster: `AuthenticationStateProvider.GetAuthenticationStateAsync()` und `Client.EnsureAuthorizationTokenAsync()` (wie in PlaylistsList)

## Konfiguration

Keine Konfiguration erforderlich. Die Funktionalität nutzt die bestehende Konfiguration aus Entwicklungsschritt 1 (`Playlists.MaxPlaylistsPerUser`).

## Nebenbeobachtungen (geringer Aufwand)

### 1. Kommentar-Korrektur in ApplicationDbContext.cs
- **Ort:** `VideoWebPlayer/Data/ApplicationDbContext.cs`, Zeile 103
- **Aktuell:** `/// Tabelle fuer einzeln freigeschaltete Medieneintraege.`
- **Korrektur:** Das Ersetzungszeichen "fuer" durch "für" ersetzen und "Medieneintraege" durch "Medieneinträge"
- **Ziel:** `/// Tabelle für einzeln freigeschaltete Medieneinträge.`

### 2. Dokumentations-Korrektur in docs/help/playlists.md
- **Ort:** `docs/help/playlists.md`, Abschnitt "Zugriff und Berechtigungen" (Zeile 40-41)
- **Aktuell:** Text besagt "wird abgelehnt" ohne zu unterscheiden zwischen 403 und 404
- **Korrekt:** Bei Zugriff auf eine fremde Playlist antwortet die API mit **403 Forbidden**, nicht mit 404 Not Found
- **Ursache:** Dies ist eine intentionale Sicherheitsmaßnahme, um nicht die Existenz fremder Playlists preiszugeben
- **Korrektur:** Dokumentation präzisieren: "Der Zugriff auf eine fremde Playlist wird mit HTTP 403 (Forbidden) abgelehnt, unabhängig davon, ob die Playlist existiert."

## Offene Fragen

1. Soll die Detail-Seite beim Speichern/Löschen automatisch zur Übersicht zurück navigieren, oder soll sie mit aktualisierten Daten bleiben?
   - **Annahme:** Bei erfolgreicher Änderung (Bearbeiten oder Löschen) Rückleitung zur Übersicht (analog zu PlaylistsList-Verhalten)

2. Soll die Detail-Seite nur angemeldete Nutzer zugreifen? Oder soll unbekannte Route zunächst zur Login-Seite leiten?
   - **Annahme:** Analog zu PlaylistsList wird zunächst die Komponente geladen, der Authentifizierungscheck erfolgt im `OnInitializedAsync()`, und bei Fehler wird eine Fehlermeldung angezeigt (nicht sofortiges Redirect)

3. Anzahl der Actionbuttons auf der Detail-Seite: Sollen "Bearbeiten" und "Löschen" beide angeboten werden, oder nur als Backup (z.B. nur Löschen, Bearbeiten via Fallback)?
   - **Annahme:** Beide Buttons werden angeboten, analog zu PlaylistsList

4. Soll die Detailseite auch für lesende Vorschau von fremden Playlists zugänglich sein (nach Implementierung einer späteren Sharing-Funktionalität)?
   - **Annahme:** Nein, für Schritt 1 ist Zugriff nur auf eigene Playlists vorgesehen; bei fremden Playlists erfolgt 403 und Fehlermeldung
