# UI-Komponenten (Razor)

## `PlaylistsList.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistsList.razor`

Hauptseite für die Playlist-Verwaltung. Zeigt alle Playlists des aktuellen Benutzers in einer Tabelle an.

**Route:** `@page "/playlists"`

**Funktionalität:**
- Laden aller Playlists für den aktuellen Benutzer via `Client.RequestPlaylistsAsync()`
- Tabellarische Anzeige mit Spalten: Name, Beschreibung, Sortierung, Erstellt, Aktualisiert, Aktionen
- Buttons pro Zeile:
  - "Bearbeiten" - öffnet `PlaylistForm` modal mit `PlaylistId` zum Bearbeiten
  - "Löschen" - zeigt Lösch-Bestätigungsdialog
- Button "Neue Playlist erstellen" - öffnet `PlaylistForm` modal ohne `PlaylistId` (Neuanlage)
- Fehlerbehandlung: Zeigt Fehlermeldung wenn Laden fehlschlägt
- Leer-Zustand: Zeigt "Keine Playlists vorhanden" wenn Liste leer ist

**State:**
- `playlists` - Liste der geladenen Playlists
- `isLoading` - Laden-Indikator
- `loadError` - Fehler beim Laden
- `showForm` - zeigt `PlaylistForm` modal
- `editingPlaylistId` - ID der zu bearbeitenden Playlist (null = neu)
- `playlistPendingDelete` - Playlist ausstehend zur Löschung

**Abhängigkeiten:**
- `VideoWebPlayerClient` - API-Calls
- `AuthenticationStateProvider` - Authentifizierungsprüfung
- `EventManager` - publiziert Status-Events

**Authentifizierung:**
- Prüft in `OnInitializedAsync()` ob Benutzer angemeldet ist
- Ruft `Client.EnsureAuthorizationTokenAsync()` auf
- Bei Fehler wird Fehlermeldung angezeigt

## `PlaylistForm.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistForm.razor`

Modal-Komponente zum Erstellen und Bearbeiten von Playlists.

**Parameter:**
- `PlaylistId` (`long?`) - ID der zu bearbeitenden Playlist; null = neu erstellen
- `OnSave` (`EventCallback`) - wird aufgerufen nach erfolgreichem Speichern
- `OnCancel` (`EventCallback`) - wird aufgerufen bei Abbruch

**Funktionalität:**
- **Beim Rendern (OnInitializedAsync):**
  - Wenn `PlaylistId` gesetzt: ladet Playlist via `Client.RequestPlaylistAsync(PlaylistId.Value)`
  - Bei Fehler: zeigt "Playlist konnte nicht geladen werden."
- **Formular mit Feldern:**
  - Playlist-Name (`InputText`, erforderlich, max. 255 Zeichen)
  - Beschreibung (`InputTextArea`, optional, max. 2000 Zeichen)
  - Sortierung (`InputSelect`, Optionen: "Nach Erscheinungsdatum", "Manuell")
- **Validierung (Methode `Validate()`):**
  - Name nicht leer
  - Name max. 255 Zeichen
  - Beschreibung max. 2000 Zeichen
  - Zeigt Fehler inline
- **Speichern (Methode `SaveAsync()`):**
  - Wenn `PlaylistId` gesetzt: ruft `Client.UpdatePlaylistAsync()` auf
  - Wenn `PlaylistId` null: ruft `Client.CreatePlaylistAsync()` auf
  - Bei Erfolg: ruft `OnSave.InvokeAsync()`
  - Bei Fehler: zeigt Fehlermeldung (speziell für "existiert bereits")
- **Buttons:**
  - "Speichern" - speichert und schließt (disabled während Speichern)
  - "Abbrechen" - ruft `OnCancel.InvokeAsync()`

**State:**
- `playlist` - aktuelles Formular-Datenmodell (DtoPlaylist)
- `isLoading` - Laden-Indikator beim Öffnen einer bestehenden Playlist
- `isSaving` - Speichern-Indikator
- `nameError`, `descriptionError`, `errorMessage` - Fehlermeldungen

**Abhängigkeiten:**
- `VideoWebPlayerClient` - API-Calls zum Laden und Speichern
- `PlaylistSortModeValues` - Konstanten für Sortieroptionen
