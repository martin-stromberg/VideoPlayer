# Offene Aufgaben

Erstellt am: 2026-09-05
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] Doppelter Code zwischen `PlaylistDetail.razor` und `PlaylistsList.razor`: Das komplette `OnInitializedAsync`-Boilerplate zur Token-Beschaffung (inkl. identischem `catch`-Kommentar „Kein Token verfuegbar …") sowie das Lösch-Bestätigungsdialog-Markup (`admin-dialog-overlay`/`admin-dialog`, `#confirm-delete-playlist-button`, `CancelDelete()`) sind wortwörtlich dupliziert. Empfehlung: Token-Beschaffung in eine gemeinsame Basis/Hilfsmethode auslagern (z. B. `PlaylistPageComponentBase` oder `EnsureAuthorizationTokenSilentlyAsync` auf `VideoWebPlayerClient`); Lösch-Bestätigungsdialog in eine eigene, parametrisierbare Komponente (z. B. `PlaylistDeleteConfirmationDialog`) extrahieren und in beiden Komponenten verwenden.
- [ ] Uneinheitliche Event-Handler-Namenskonventionen zwischen `PlaylistsList.razor` (`OpenEditForm`, `RequestDelete`, `ConfirmDeleteAsync`, `HandleSavedAsync`, `HandleCancel`) und `PlaylistDetail.razor` (`OnEditClick`, `OnDeleteClick`, `OnDeleteConfirm`, `OnFormSave`, `OnFormCancel`) für fachlich gleichwertige Aktionen. Empfehlung: einheitliches Namensschema für Event-Handler in beiden Playlist-Komponenten festlegen und angleichen.
- [ ] Fehlendes `Async`-Suffix bei den asynchronen Methoden `OnFormSave()` und `OnDeleteConfirm()` in `PlaylistDetail.razor`, obwohl beide `async Task` sind und der Rest des Branches (auch dieselbe Datei, z. B. `LoadPlaylistAsync`) das Suffix konsequent nutzt. Empfehlung: in `OnFormSaveAsync` bzw. `OnDeleteConfirmAsync` umbenennen (inkl. Markup-Bindung `@onclick="OnDeleteConfirm"`).
- [ ] Inkonsistente Umlaut-Schreibweise in `VideoWebPlayer/Data/ApplicationDbContext.cs` Zeile 103 („Tabelle für einzeln freigeschaltete Medieneinträge.", echte Umlaute) gegenüber vier strukturell identischen Nachbar-Kommentaren mit ASCII-Transliteration (Zeilen 139, 147, 151, 155: „Tabelle fuer …"). Empfehlung: entweder Zeile 103 wieder an die ASCII-Transliteration angleichen oder konsistent alle vier weiteren „Tabelle fuer …"-Kommentare in derselben Datei auf echte Umlaute umstellen.
- [ ] Duplikation in `VideoWebPlayer.Tests/PlaylistsE2ETests.cs` nur teilweise behoben: Die Hilfsmethode `CreatePlaylistViaUiAsync` wird nur von den neuen Detailseiten-Tests verwendet. Die bereits vorher vorhandenen Tests `Create_List_Edit_And_Delete_Playlist_HappyPath`, `UserB_Does_Not_See_UserA_Playlists` sowie der erste Anlage-Schritt in `DuplicateName_ShowsConflictError` enthalten weiterhin denselben Block inline. Empfehlung: auch diese bestehenden Tests auf `CreatePlaylistViaUiAsync` umstellen.
- [ ] `PlaylistsE2ETests` ist zu einer 430-Zeilen-Klasse mit zwei fachlich getrennten Themenbereichen (Listenansicht/CRUD vs. Detailseite) gewachsen, während für Controller-/Service-Tests im selben Projekt bereits eine Aufteilung nach Themenbereich etabliertes Muster ist (`PlaylistsControllerTests_Auth`/`_Create`/`_Delete`/`_Read`/`_Update`, `PlaylistServiceTests_Create`/`_Delete`/`_Read`/`_Update`). Empfehlung: die acht neuen, die Detailseite betreffenden Tests in eine eigene Klasse (z. B. `PlaylistDetailE2ETests`) auslagern; gemeinsame Infrastruktur über eine gemeinsame Basisklasse teilen.

## Fehlgeschlagene Tests

Keine.
