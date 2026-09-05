# Umsetzungsplan: Playlist-Detailseite mit "Öffnen"-Aktion

## Übersicht

Die Playlist-Verwaltung wird um eine Detailseite erweitert, die Stammdaten einer einzelnen Playlist anzeigt. Anwender können eine Playlist aus der Übersicht (PlaylistsList.razor) über eine neue "Öffnen"-Aktion zur Detailseite navigieren, wo sie diese bearbeiten, löschen oder zur Übersicht zurückkehren können. Die Implementierung nutzt vollständig bestehende Backend-Endpoints, Services und Komponenten (PlaylistForm); nur die neue Detailkomponente `PlaylistDetail.razor` und die Erweiterung der PlaylistsList.razor um den "Öffnen"-Button sind erforderlich.

## Designentscheidungen

Keine — folgt bestehenden Mustern.

Die Detailseite folgt dem etablierten Muster von `PlaylistsList.razor`:
- Authentifizierungsprüfung in `OnInitializedAsync()`
- Fehlerbehandlung mit inline Fehlermeldungen statt Redirects
- Wiederverwendung der `PlaylistForm`-Komponente für Bearbeitungen
- Navigation via `NavigationManager.NavigateTo()` für Link-Aktionen

## Programmabläufe

### Laden und Anzeigen einer Playlist-Detail-Seite

1. Anwender navigiert zur Route `/playlists/{id:long}` über den "Öffnen"-Button in PlaylistsList oder direkt per URL
2. `PlaylistDetail.razor` wird geladen
3. In `OnInitializedAsync()`:
   - `AuthenticationStateProvider.GetAuthenticationStateAsync()` wird aufgerufen, um Authentifizierungsstatus zu prüfen
   - `Client.EnsureAuthorizationTokenAsync()` wird aufgerufen, um das Authentifizierungs-Token zu laden
   - `Client.RequestPlaylistAsync(id)` wird aufgerufen, um die Playlist zu laden (GET `/api/playlists/{id}`)
4. Ergebnis:
   - **Bei Erfolg (200 OK):** `DtoPlaylist` wird zurückgegeben und angezeigt (Name, Beschreibung, Sortiermode, CreatedAt, UpdatedAt)
   - **Bei 403 Forbidden (fremde Playlist):** `HttpRequestException` wird geworfen; Komponente fängt sie ab und zeigt "Zugriff auf diese Playlist verweigert"
   - **Bei 401 Unauthorized:** Komponente fängt Exception ab und leitet zur Login-Seite um (Standardverhalten)
   - **Bei 404 Not Found:** `null` wird zurückgegeben; Komponente zeigt "Playlist nicht gefunden"
5. Read-only Anzeige der Stammdaten in einer strukturierten Form (kein HTML-Formular)
6. Buttons:
   - "Bearbeiten" — öffnet `PlaylistForm` modal mit `PlaylistId`
   - "Löschen" — zeigt Lösch-Bestätigungsdialog (analog PlaylistsList)
   - "Zurück zur Übersicht" — navigiert zu `/playlists` via `NavigationManager.NavigateTo("/playlists")`
7. Nach erfolgreichem Speichern oder Löschen:
   - `PlaylistForm` oder Lösch-Dialog schließt
   - Detailseite aktualisiert sich mit neuen Daten oder navigiert zurück zur Übersicht

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor` (neu), `PlaylistForm.razor` (wiederverwendet), `VideoWebPlayerClient`, `AuthenticationStateProvider`, `NavigationManager`

### Navigieren zur Playlist-Detail-Seite über "Öffnen"-Button

1. Anwender sieht in `PlaylistsList.razor` pro Zeile einen neuen "Öffnen"-Button
2. Beim Klick wird `NavigationManager.NavigateTo($"/playlists/{playlist.Id}")` aufgerufen
3. Der Browser navigiert zur Detail-Route

Beteiligte Klassen/Komponenten: `PlaylistsList.razor` (erweiterung), `NavigationManager`

### Bearbeiten einer Playlist von der Detail-Seite

1. Anwender klickt "Bearbeiten" auf der Detailseite
2. `PlaylistForm` wird modal geöffnet mit `PlaylistId` gesetzt
3. PlaylistForm lädt die aktuelle Playlist via `Client.RequestPlaylistAsync(PlaylistId.Value)`
4. Formular wird mit aktuellen Daten gefüllt
5. Anwender bearbeitet Felder und klickt "Speichern"
6. PlaylistForm ruft `Client.UpdatePlaylistAsync()` auf
7. Bei Erfolg:
   - `OnSave` Event wird ausgelöst
   - PlaylistForm wird geschlossen
   - Detailseite lädt Playlist neu (um aktualisierte Daten anzuzeigen)
8. Bei Fehler:
   - Fehlermeldung wird inline angezeigt (analog PlaylistsList)

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `PlaylistForm.razor`, `VideoWebPlayerClient`

### Löschen einer Playlist von der Detail-Seite

1. Anwender klickt "Löschen" auf der Detailseite
2. Bestätigungsdialog wird angezeigt (analog PlaylistsList)
3. Bei Bestätigung:
   - `Client.DeletePlaylistAsync(id)` wird aufgerufen
   - Bei Erfolg (204 No Content):
     - Dialog wird geschlossen
     - `NavigationManager.NavigateTo("/playlists")` wird aufgerufen (Rückkehr zur Übersicht)
   - Bei Fehler:
     - Fehlermeldung wird angezeigt

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `VideoWebPlayerClient`, `NavigationManager`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PlaylistDetail.razor` | Razor-Komponente | Detailseite für eine einzelne Playlist mit Anzeige der Stammdaten und Buttons für Bearbeiten, Löschen und Zurückkehren |

## Änderungen an bestehenden Klassen

### `PlaylistsList.razor` (Razor-Komponente)

- **Neue UI-Element:** "Öffnen"-Button pro Zeile (neben "Bearbeiten" und "Löschen")
  - Beim Klick: `NavigationManager.NavigateTo($"/playlists/{playlist.Id}")`
  - Styling und Positionierung analog zu "Bearbeiten"-Button

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Alle Validierungen erfolgen auf der Backend-Seite in `PlaylistService` und sind bereits implementiert.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

Keine bekannten Seiteneffekte.

Die neue Detailseite ist ein reiner Zusatz ohne Auswirkungen auf bestehende Abläufe. Die Erweiterung von PlaylistsList um den "Öffnen"-Button ist nicht-invasiv und beeinträchtigt nicht die bestehenden "Bearbeiten"- und "Löschen"-Funktionen.

## Umsetzungsreihenfolge

1. **`PlaylistDetail.razor` Component erstellen**
   - Voraussetzungen: `VideoWebPlayerClient`, `AuthenticationStateProvider`, `NavigationManager`, `PlaylistForm.razor` (alle bereits vorhanden)
   - Beschreibung: 
     - Route `@page "/playlists/{id:long}"` mit long-Konvertierung für die ID
     - `OnInitializedAsync()` implementieren:
       - Authentifizierungsprüfung (`AuthenticationStateProvider.GetAuthenticationStateAsync()`, `Client.EnsureAuthorizationTokenAsync()`)
       - Playlist laden via `Client.RequestPlaylistAsync(id)`
       - Exception-Handling für 403, 401, 404
     - UI-Struktur:
       - Read-only Anzeige der Stammdaten: Name, Beschreibung, Sortiermode, CreatedAt, UpdatedAt
       - Buttons: "Bearbeiten", "Löschen", "Zurück zur Übersicht"
     - State-Management:
       - `playlist` — geladene Playlist
       - `isLoading` — während Laden
       - `loadError` — Fehler beim Laden
       - `showForm` — zeigt PlaylistForm modal
       - `showDeleteConfirmation` — zeigt Lösch-Bestätigungsdialog
     - Event-Handler:
       - `OnEditClick()` — setzt `showForm = true` und `editingPlaylistId = id`
       - `OnDeleteClick()` — setzt `showDeleteConfirmation = true`
       - `OnFormSave()` — schließt Form, lädt Playlist neu
       - `OnFormCancel()` — schließt Form
       - `OnDeleteConfirm()` — ruft `Client.DeletePlaylistAsync(id)` auf, navigiert bei Erfolg zur Übersicht
       - `OnBackClick()` — navigiert zu `/playlists`

2. **PlaylistsList.razor um "Öffnen"-Button erweitern**
   - Voraussetzungen: `NavigationManager` (bereits vorhanden)
   - Beschreibung:
     - In der Aktionen-Spalte (neben "Bearbeiten" und "Löschen"):
       - Neuer Button "Öffnen" mit Styling analog zu "Bearbeiten"
       - `OnClick`-Handler ruft `NavigationManager.NavigateTo($"/playlists/{playlist.Id}")` auf

3. **Korrektur in ApplicationDbContext.cs (Nebenbeobachtung)**
   - Voraussetzungen: Keine
   - Beschreibung:
     - Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`, Zeile 103
     - Kommentar ändern: `/// Tabelle fuer einzeln freigeschaltete Medieneintraege.` → `/// Tabelle für einzeln freigeschaltete Medieneinträge.`

4. **Korrektur in docs/help/playlists.md (Nebenbeobachtung)**
   - Voraussetzungen: Keine
   - Beschreibung:
     - Datei: `docs/help/playlists.md`, Abschnitt "Zugriff und Berechtigungen" (Zeile 40-41)
     - Dokumentation präzisieren: Bei Zugriff auf eine fremde Playlist antwortet die API mit HTTP **403 Forbidden**, nicht 404 Not Found, um die Existenz fremder Playlists nicht preiszugeben

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Load_Detail_Page_Unauthenticated_Shows_Error` | `PlaylistsE2ETests` | Unangemeldeter Benutzer sieht Fehlermeldung oder wird zur Login-Seite geleitet |
| `Load_Detail_Page_ValidPlaylist_ShowsMetadata` | `PlaylistsE2ETests` | Authentifizierter Benutzer öffnet eine Playlist-Detailseite und sieht Stammdaten (Name, Beschreibung, Sortiermode, Zeitstempel) |
| `Open_Button_In_List_Navigates_To_Detail` | `PlaylistsE2ETests` | Klick auf "Öffnen"-Button in PlaylistsList navigiert zur Detailseite |
| `Detail_Page_Edit_Opens_Form_And_Saves` | `PlaylistsE2ETests` | Klick "Bearbeiten" öffnet PlaylistForm modal; Benutzer bearbeitet und speichert; Detailseite zeigt aktualisierte Daten |
| `Detail_Page_Delete_Shows_Confirmation_And_Deletes` | `PlaylistsE2ETests` | Klick "Löschen" zeigt Bestätigung; nach Bestätigung wird Playlist gelöscht und zur Übersicht navigiert |
| `Detail_Page_Back_Button_Navigates_To_List` | `PlaylistsE2ETests` | Klick "Zurück zur Übersicht" navigiert zur Playlist-Übersicht |
| `Detail_Page_Foreign_Playlist_Shows_403_Error` | `PlaylistsE2ETests` | Zugriff auf fremde Playlist zeigt Fehlermeldung "Zugriff auf diese Playlist verweigert" |
| `Detail_Page_Nonexistent_Playlist_Shows_404_Error` | `PlaylistsE2ETests` | Zugriff auf nicht-existierende Playlist zeigt Fehlermeldung "Playlist nicht gefunden" |

### Betroffene bestehende Tests

Keine. Die neuen Komponenten beeinflussen nicht die bestehenden Tests für PlaylistsList, PlaylistForm oder PlaylistsController.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Benutzer öffnet Detailseite über "Öffnen"-Button | `PlaylistsE2ETests::Open_Button_In_List_Navigates_To_Detail` | „Zusätzlicher Button 'Öffnen' neben 'Bearbeiten' und 'Löschen'" | E2E testet die komplette Navigation über die UI und JavaScript-Events, die Unit-Tests nicht ausreichen |
| Pflicht | Detailseite zeigt Stammdaten korrekt an | `PlaylistsE2ETests::Load_Detail_Page_ValidPlaylist_ShowsMetadata` | „Anzeige der Stammdaten: Name, Beschreibung, Sortiermodus, Erstellungs- und Aktualisierungszeitpunkt" | Nur E2E kann die tatsächliche Rendering und Datenanbindung über die komplette HTTP-/Client-Chain testen |
| Pflicht | Bearbeiten von der Detail-Seite funktioniert | `PlaylistsE2ETests::Detail_Page_Edit_Opens_Form_And_Saves` | „Buttons für 'Bearbeiten' [...] analog zur PlaylistsList" | E2E testet die Interaktion zwischen PlaylistDetail und PlaylistForm sowie die Aktualisierung der Anzeige |
| Pflicht | Löschen von der Detail-Seite funktioniert mit Bestätigung | `PlaylistsE2ETests::Detail_Page_Delete_Shows_Confirmation_And_Deletes` | „Buttons für [...] 'Löschen' analog zur PlaylistsList" | E2E testet den Dialog-Flow und die Navigation nach dem Löschen |
| Pflicht | Zurück-Navigation zur Übersicht funktioniert | `PlaylistsE2ETests::Detail_Page_Back_Button_Navigates_To_List` | „Navigation zurück zur Playlist-Übersicht" | E2E testet die Routing-Logik und Client-Navigation |
| Pflicht | Fremde Playlists werden mit 403 abgelehnt | `PlaylistsE2ETests::Detail_Page_Foreign_Playlist_Shows_403_Error` | „Fehlerbehandlung für nicht existierende oder fremde Playlists (403 Zugriff verweigert)" | E2E testet die Exception-Behandlung und Fehleranzeige über den kompletten Ablauf |
| Pflicht | Unauthentifizierte Zugriffe werden behandelt | `PlaylistsE2ETests::Load_Detail_Page_Unauthenticated_Shows_Error` | „Fehlerbehandlung [...] (401 Unauthorized bei fehlender Anmeldung)" | E2E testet die Authentifizierungsprüfung am Komponentenstart |
| Sekundär | Nicht-existierende Playlists zeigen 404-Fehler | `PlaylistsE2ETests::Detail_Page_Nonexistent_Playlist_Shows_404_Error` | „Fehlerbehandlung für nicht existierende oder fremde Playlists" | E2E testet Edge Case: direkte URL zu nicht-existierender Playlist |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. Die bestehenden E2E-Tests in `PlaylistsE2ETests` testen die PlaylistsList und PlaylistForm; die neue Detailseite ist orthogonal und erfordert keine Anpassungen bestehender Tests.

## Offene Punkte

Keine.

Alle fachlichen Fragen aus der Anforderung wurden durch die Anforderung selbst beantwortet oder folgen etablierten Konventionen:

1. ✅ **Navigation nach Änderungen:** Anforderung legt fest, dass bei erfolgreicher Änderung zur Übersicht zurück navigiert wird
2. ✅ **Authentifizierung:** Anforderung legt fest, dass Komponente geladen wird und im `OnInitializedAsync()` authentifiziert wird (analog PlaylistsList)
3. ✅ **Actionbuttons:** Anforderung legt fest, dass beide Buttons angeboten werden (Bearbeiten und Löschen)
4. ✅ **Zugriff auf fremde Playlists:** Anforderung legt fest, dass nur Zugriff auf eigene Playlists, 403 bei fremden
