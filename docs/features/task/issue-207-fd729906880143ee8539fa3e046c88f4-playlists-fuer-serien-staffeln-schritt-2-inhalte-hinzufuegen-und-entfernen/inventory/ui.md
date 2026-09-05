# UI-Komponenten (Razor)

## Bestehende Komponenten

### `PlaylistsList.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistsList.razor`

**Zweck:** Listet alle Playlists eines Benutzers auf

**Funktionalität:**
- Laden aller Playlists
- Anzeige in Listenform
- Navigation zu einzelner Playlist (PlaylistDetail)
- Erstellen-Button für neue Playlist

---

### `PlaylistDetail.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`

**Zweck:** Zeigt Details einer einzelnen Playlist an und ermöglicht Bearbeitung/Löschung

**Hauptmerkmale:**
- Route: `/playlists/{id:long}`
- Anzeige von: Name, Beschreibung, Sortierung, Erstellungs-/Aktualisierungsdatum
- Bearbeiten-Button → öffnet `PlaylistForm`
- Löschen-Button → öffnet `PlaylistDeleteConfirmationDialog`
- Zurück-Button → Navigation zu Playlist-Übersicht

**Abhängigkeiten:**
- `VideoWebPlayerClient` (für API-Aufrufe)
- `AuthenticationStateProvider` (für Benutzerauthentifizierung)
- `NavigationManager` (für Navigation)

**Fehlerbehandlung:**
- HTTP 403 (Forbidden) → "Zugriff auf diese Playlist verweigert"
- HTTP 404 → "Playlist nicht gefunden"
- Allgemeine Fehler → "Fehler beim Laden/Löschen der Playlist"

---

### `PlaylistForm.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistForm.razor`

**Zweck:** Modal/Formular zum Erstellen oder Bearbeiten einer Playlist

**Parameter:**
- `PlaylistId` (long?, optional): Wenn gesetzt, bearbeitet; wenn null, erstellt neue Playlist
- `OnSave` (EventCallback): Callback nach erfolgreicher Speicherung
- `OnCancel` (EventCallback): Callback bei Abbruch

**Funktionalität:**
- Eingabefelder für: Name, Beschreibung, Sortiermode
- Validierung
- Speichern / Abbrechen-Buttons

---

### `PlaylistDeleteConfirmationDialog.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistDeleteConfirmationDialog.razor`

**Zweck:** Bestätigungsdialog zum Löschen einer Playlist

**Parameter:**
- `PlaylistName` (string?): Name der zu löschenden Playlist
- `OnConfirm` (EventCallback): Callback bei Bestätigung
- `OnCancel` (EventCallback): Callback bei Abbruch

---

## Zu erweitern für Schritt 2

### `PlaylistDetail.razor` (Erweiterung)

**Zu ergänzen:**
1. Einfache, unsortierte Auflistung der Einträge (PlaylistEntries)
   - Anzeige von: MediaType, MediaId, MediaTitle, ParentMediaTitle, AddedAt
   - Entfernen-Button für jeden Eintrag

2. UI-Feedback für Duplikate
   - Toast-Nachricht oder Alert bei Duplikat-Versuch

3. Eingabe-/Auswahlmöglichkeit zum Hinzufügen von Inhalten
   - Dropdown/Auswahl des Medientyps (Movie, TVShowEpisode, TVShowSeason, TVShow, MovieCollection)
   - Such- oder Auswahlfeld für den Medieninhalt
   - Button zum Hinzufügen

**Alternative (optional):**
- Neue separate Komponente `PlaylistMediaSelector.razor` oder `PlaylistAddMediaDialog.razor` für die Hinzufügen-Funktionalität

---

## Migrationen

### `20260905081404_AddPlaylistsTable`
Datei: `VideoWebPlayer/Migrations/20260905081404_AddPlaylistsTable.cs`

**Zweck:** Erstelle die `Playlists`-Tabelle mit den Spalten:
- `Id` (INTEGER, Primary Key, Autoincrement)
- `UserId` (TEXT, Foreign Key zu AspNetUsers)
- `Name` (TEXT, max. 255, Collation: NOCASE)
- `Description` (TEXT, max. 2000, optional)
- `SortMode` (INTEGER, Default: 0 = ByReleaseDate)
- `CreatedAt` (TEXT)
- `UpdatedAt` (TEXT)

**Indizes:**
- Primary Key auf `Id`
- Unique Index auf `(UserId, Name)`

---

## Zu erstellen für Schritt 2

### Migration für `PlaylistEntry`-Tabelle
**Benötigt:**
- Neue Tabelle `PlaylistEntries` mit allen erforderlichen Spalten
- Composite Unique Index auf `(PlaylistId, MediaType, MediaId)`
- Foreign Key zu `Playlists` mit Cascade-Delete

### Konfiguration für PlaylistEntry (EF Core)
**Datei:** `VideoWebPlayer/Data/Configurations/PlaylistEntryConfiguration.cs`
- EF Core-Konfiguration für `PlaylistEntry`
- Composite Unique Constraint
- Foreign Key-Beziehung zu `Playlist`

### UI-Komponenten
- Eventuell: `PlaylistMediaSelector.razor` oder `PlaylistAddMediaDialog.razor` für getrennte Hinzufügen-Logik

