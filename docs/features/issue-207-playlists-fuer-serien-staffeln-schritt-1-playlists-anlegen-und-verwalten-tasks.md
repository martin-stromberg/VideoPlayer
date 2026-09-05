# Tasks: Playlist-Detailseite mit "Öffnen"-Aktion

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI-Komponenten | `PlaylistDetail.razor` Komponente mit Route `@page "/playlists/{id:long}"` erstellen | Offen | — |
| 2 | UI-Komponenten | `PlaylistDetail.razor`: `OnInitializedAsync()` implementieren (Authentifizierung, Playlist laden, Exception-Handling) | Offen | — |
| 3 | UI-Komponenten | `PlaylistDetail.razor`: Read-only UI-Struktur für Stammdaten-Anzeige (Name, Beschreibung, Sortiermode, CreatedAt, UpdatedAt) | Offen | — |
| 4 | UI-Komponenten | `PlaylistDetail.razor`: Buttons "Bearbeiten", "Löschen", "Zurück zur Übersicht" implementieren | Offen | — |
| 5 | UI-Komponenten | `PlaylistDetail.razor`: State-Management (playlist, isLoading, loadError, showForm, showDeleteConfirmation) | Offen | — |
| 6 | UI-Komponenten | `PlaylistDetail.razor`: Event-Handler `OnEditClick()` implementieren (öffnet PlaylistForm modal) | Offen | — |
| 7 | UI-Komponenten | `PlaylistDetail.razor`: Event-Handler `OnDeleteClick()` implementieren (zeigt Lösch-Bestätigung) | Offen | — |
| 8 | UI-Komponenten | `PlaylistDetail.razor`: Event-Handler `OnFormSave()` implementieren (schließt Form, lädt Playlist neu) | Offen | — |
| 9 | UI-Komponenten | `PlaylistDetail.razor`: Event-Handler `OnFormCancel()` implementieren (schließt Form) | Offen | — |
| 10 | UI-Komponenten | `PlaylistDetail.razor`: Event-Handler `OnDeleteConfirm()` implementieren (löscht und navigiert) | Offen | — |
| 11 | UI-Komponenten | `PlaylistDetail.razor`: Event-Handler `OnBackClick()` implementieren (navigiert zu /playlists) | Offen | — |
| 12 | UI-Komponenten | `PlaylistsList.razor` um "Öffnen"-Button pro Zeile erweitern | Offen | — |
| 13 | UI-Komponenten | "Öffnen"-Button in PlaylistsList mit `NavigationManager.NavigateTo($"/playlists/{playlist.Id}")` verbinden | Offen | — |
| 14 | Dokumentation | ApplicationDbContext.cs (Zeile 103): Kommentar korrigieren (ä, ü statt ae, ue) | Offen | — |
| 15 | Dokumentation | docs/help/playlists.md: Dokumentation zu 403 vs. 404 präzisieren (Zugriff auf fremde Playlist) | Offen | — |
| 16 | E2E-Tests | Test: Unauthentifizierte Benutzer sehen Fehlermeldung bei Detail-Seite | Offen | PlaylistsE2ETests::Load_Detail_Page_Unauthenticated_Shows_Error |
| 17 | E2E-Tests | Test: Authentifizierter Benutzer sieht Stammdaten auf Detail-Seite | Offen | PlaylistsE2ETests::Load_Detail_Page_ValidPlaylist_ShowsMetadata |
| 18 | E2E-Tests | Test: "Öffnen"-Button navigiert zur Detail-Seite | Offen | PlaylistsE2ETests::Open_Button_In_List_Navigates_To_Detail |
| 19 | E2E-Tests | Test: Bearbeiten von Detail-Seite öffnet Form und speichert | Offen | PlaylistsE2ETests::Detail_Page_Edit_Opens_Form_And_Saves |
| 20 | E2E-Tests | Test: Löschen von Detail-Seite mit Bestätigung | Offen | PlaylistsE2ETests::Detail_Page_Delete_Shows_Confirmation_And_Deletes |
| 21 | E2E-Tests | Test: "Zurück"-Button navigiert zur Übersicht | Offen | PlaylistsE2ETests::Detail_Page_Back_Button_Navigates_To_List |
| 22 | E2E-Tests | Test: Fremde Playlist zeigt 403-Fehler | Offen | PlaylistsE2ETests::Detail_Page_Foreign_Playlist_Shows_403_Error |
| 23 | E2E-Tests | Test: Nicht-existierende Playlist zeigt 404-Fehler | Offen | PlaylistsE2ETests::Detail_Page_Nonexistent_Playlist_Shows_404_Error |
