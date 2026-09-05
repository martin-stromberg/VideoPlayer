# Aufgabenliste: Playlist-Detailseite mit "Öffnen"-Aktion

Branch: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-1-playlists-anlegen-und-verwalten`

## Umsetzungsaufgaben

| Status | Aufgabe | Beschreibung | Testnachweis |
|--------|---------|-------------|------------|
| Erledigt | PlaylistDetail.razor Component erstellen | Neue Razor-Komponente mit Route `/playlists/{id:long}`, Authentifizierungsprüfung, Playlist-Ladung, Read-only Anzeige der Stammdaten und Buttons für Bearbeiten/Löschen/Zurück | `PlaylistsE2ETests.Load_Detail_Page_ValidPlaylist_ShowsMetadata` |
| Erledigt | OnInitializedAsync() implementieren | Authentifizierungsprüfung via `AuthenticationStateProvider`, Playlist laden via `Client.RequestPlaylistAsync(id)`, Exception-Handling für 403/401/404 | `PlaylistsE2ETests.Load_Detail_Page_Unauthenticated_Shows_Error`, `PlaylistsE2ETests.Detail_Page_Foreign_Playlist_Shows_403_Error`, `PlaylistsE2ETests.Detail_Page_Nonexistent_Playlist_Shows_404_Error` |
| Erledigt | UI-Struktur mit Read-only Stammdaten | Anzeige: Name, Beschreibung, SortMode, CreatedAt, UpdatedAt in strukturierter Form | `PlaylistsE2ETests.Load_Detail_Page_ValidPlaylist_ShowsMetadata` |
| Erledigt | Buttons implementieren | "Bearbeiten", "Löschen", "Zurück zur Übersicht" mit Event-Handlersfor Benutzerinteraktionen | `PlaylistsE2ETests.Detail_Page_Edit_Opens_Form_And_Saves`, `PlaylistsE2ETests.Detail_Page_Delete_Shows_Confirmation_And_Deletes`, `PlaylistsE2ETests.Detail_Page_Back_Button_Navigates_To_List` |
| Erledigt | State-Management | Variablen für `playlist`, `isLoading`, `loadError`, `showForm`, `showDeleteConfirmation`, `editingPlaylistId` | Kein direkter Test; durch Kombinationstests abgedeckt |
| Erledigt | PlaylistForm-Modal für Bearbeitung | Integration der bestehenden `PlaylistForm.razor` mit `PlaylistId` Parameter | `PlaylistsE2ETests.Detail_Page_Edit_Opens_Form_And_Saves` |
| Erledigt | Lösch-Bestätigungsdialog | Dialog analog PlaylistsList mit Bestätigungs-Mechanik | `PlaylistsE2ETests.Detail_Page_Delete_Shows_Confirmation_And_Deletes` |
| Erledigt | PlaylistsList.razor um "Öffnen"-Button erweitern | Neue Schaltfläche in Aktionen-Spalte mit Navigation zur Detailseite | `PlaylistsE2ETests.Open_Button_In_List_Navigates_To_Detail` |
| Erledigt | ApplicationDbContext.cs Kommentar korrigieren | Zeile 103: `/// Tabelle für einzeln freigeschaltete Medieneinträge.` (Umlaute) | Kein direkter Test; Dokumentationskorrektur |
| Erledigt | docs/help/playlists.md aktualisieren | Abschnitt "Zugriff und Berechtigungen": HTTP 403 bei fremden Playlists | Kein direkter Test; Dokumentationskorrektur |

## E2E-Tests

Alle neuen Tests in `VideoWebPlayer.Tests/PlaylistsE2ETests.cs`:

| Test | Status | Abgedecktes Szenario |
|------|--------|----------------------|
| `Load_Detail_Page_Unauthenticated_Shows_Error()` | Erledigt | Unangemeldeter Benutzer sieht Fehlermeldung oder wird zur Login-Seite geleitet |
| `Load_Detail_Page_ValidPlaylist_ShowsMetadata()` | Erledigt | Authentifizierter Benutzer sieht Playlist-Stammdaten auf Detailseite |
| `Open_Button_In_List_Navigates_To_Detail()` | Erledigt | "Öffnen"-Button navigiert zur Detailseite |
| `Detail_Page_Edit_Opens_Form_And_Saves()` | Erledigt | Bearbeitung von der Detailseite funktioniert und zeigt aktualisierte Daten |
| `Detail_Page_Delete_Shows_Confirmation_And_Deletes()` | Erledigt | Löschung mit Bestätigung funktioniert; Navigation zur Übersicht nach Löschen |
| `Detail_Page_Back_Button_Navigates_To_List()` | Erledigt | "Zurück zur Übersicht"-Button navigiert zur Playlist-Liste |
| `Detail_Page_Foreign_Playlist_Shows_403_Error()` | Erledigt | Zugriff auf fremde Playlist zeigt Fehlermeldung "Zugriff auf diese Playlist verweigert" |
| `Detail_Page_Nonexistent_Playlist_Shows_404_Error()` | Erledigt | Zugriff auf nicht-existierende Playlist zeigt Fehlermeldung "Playlist nicht gefunden" |

## Zusammenfassung

Alle 10 Umsetzungsaufgaben sind abgeschlossen. Die 8 neuen E2E-Tests sind implementiert und abdecken:
- ✅ Happy Path: Navigation, Bearbeitung, Löschung
- ✅ Fehlerbehandlung: 401 (unauthentifiziert), 403 (fremde Playlist), 404 (nicht vorhanden)
- ✅ Sicherheit: Eigentumsschutz, Isolation zwischen Benutzern
- ✅ UI-Konsistenz: Knöpfe, Dialoge, Datenanzeige

Keine offenen Aufgaben.
