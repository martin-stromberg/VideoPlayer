# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer/Components/Playlists/PlaylistDetail.razor (`PlaylistDetail`) und VideoWebPlayer/Components/Playlists/PlaylistsList.razor (`PlaylistsList`)

- **Doppelter Code** — `PlaylistDetail.razor` übernimmt aus `PlaylistsList.razor` zwei nahezu
  identische Codeblöcke wortwörtlich:
  1. Das komplette `OnInitializedAsync`-Boilerplate zur Token-Beschaffung (Aufruf von
     `AuthStateProvider.GetAuthenticationStateAsync()`, `try`/`catch` um
     `Client.EnsureAuthorizationTokenAsync(authState.User)` inklusive identischem Kommentartext
     „Kein Token verfuegbar (z. B. nicht angemeldet). …") — nur der abschließende Aufruf
     (`LoadPlaylistsAsync()` vs. `LoadPlaylistAsync()`) unterscheidet sich.
  2. Das Lösch-Bestätigungsdialog-Markup (`admin-dialog-overlay` / `admin-dialog` mit Header
     „Playlist loeschen", Bestätigungstext, `#confirm-delete-playlist-button` sowie
     `CancelDelete()`) ist strukturell identisch in beiden Komponenten dupliziert.

  Der leere `catch (Exception)`-Block (nur Kommentar, keine Behandlung/Protokollierung) wird durch
  diese Duplikation nun an zwei Stellen wiederholt, statt zentral einmal definiert zu sein.

  Empfehlung: Die Token-Beschaffung in eine gemeinsame Basis (z. B. eine
  `PlaylistPageComponentBase`-Basisklasse oder eine Hilfsmethode auf `VideoWebPlayerClient`, die
  intern das Schlucken des Fehlers übernimmt, z. B. `EnsureAuthorizationTokenSilentlyAsync(user)`)
  auslagern und von beiden Komponenten wiederverwenden. Das Lösch-Bestätigungsdialog-Markup in eine
  eigene, parametrisierbare Komponente (z. B. `PlaylistDeleteConfirmationDialog`) extrahieren und
  in `PlaylistsList.razor` sowie `PlaylistDetail.razor` einbinden.

- **Namenskonventionen und Einheitlichkeit** — Für fachlich gleichwertige Aktionen verwenden die
  beiden Komponenten unterschiedliche Benennungsschemata für ihre Event-Handler-Methoden:
  `PlaylistsList.razor` nutzt `OpenEditForm(long)`, `RequestDelete(DtoPlaylist)`,
  `ConfirmDeleteAsync()`, `HandleSavedAsync()`, `HandleCancel()`; `PlaylistDetail.razor` nutzt für
  die analogen Aktionen `OnEditClick()`, `OnDeleteClick()`, `OnDeleteConfirm()`, `OnFormSave()`,
  `OnFormCancel()`. Beide Komponenten gehören zum selben Feature und werden im selben Branch
  eingeführt bzw. erweitert, sodass ein einheitliches Namensschema erwartet werden kann.

  Empfehlung: Ein einheitliches Namensschema für Event-Handler in den Playlist-Komponenten
  festlegen (z. B. durchgängig `On*`-Präfix oder durchgängig das in `PlaylistsList.razor`
  etablierte Schema) und beide Dateien darauf angleichen.

### VideoWebPlayer/Components/Playlists/PlaylistDetail.razor (`PlaylistDetail`)

- **Namenskonventionen und Einheitlichkeit** — Die asynchronen Methoden `OnFormSave()` (Zeile 141)
  und `OnDeleteConfirm()` (Zeile 157) sind vom Typ `async Task`, tragen aber nicht das Suffix
  `Async`, während im selben Branch (z. B. `LoadPlaylistAsync`, `LoadPlaylistsAsync`,
  `ConfirmDeleteAsync`, `HandleSavedAsync`, `EnsureAuthorizationTokenAsync`) asynchrone Methoden
  durchgängig mit dem Suffix `Async` benannt werden — auch innerhalb derselben Datei
  (`LoadPlaylistAsync` trägt das Suffix, `OnFormSave`/`OnDeleteConfirm` nicht).

  Empfehlung: `OnFormSave` in `OnFormSaveAsync` und `OnDeleteConfirm` in `OnDeleteConfirmAsync`
  umbenennen (inklusive der Bindung im Markup `@onclick="OnDeleteConfirm"`).

### VideoWebPlayer/Data/ApplicationDbContext.cs (`ApplicationDbContext`)

- **Namenskonventionen und Einheitlichkeit** — Zeile 103 wurde von „Tabelle fuer einzeln
  freigeschaltete Medieneintraege." auf „Tabelle für einzeln freigeschaltete Medieneinträge."
  geändert (echte Umlaute statt ASCII-Transliteration). Innerhalb derselben Datei folgen jedoch
  mehrere strukturell identische Doc-Kommentare demselben Muster „Tabelle fuer …." weiterhin mit
  ASCII-Transliteration (Zeile 139 „Tabelle fuer Gesehen-Markierungen.", Zeile 147 „Tabelle fuer
  Backup-Einstellungen.", Zeile 151 „Tabelle fuer Backup- und Restore-Historie.", Zeile 155
  „Tabelle fuer Update-Einstellungen."). Dadurch wird derselbe wiederkehrende Kommentar-Formulierungstyp
  innerhalb derselben Datei nun inkonsistent geschrieben (ein Ausreißer mit echten Umlauten
  gegenüber vier Vorkommen mit ASCII-Transliteration).

  Empfehlung: Zeile 103 wieder an die im übrigen Klassenkörper durchgängig verwendete
  ASCII-Transliteration angleichen („Tabelle fuer einzeln freigeschaltete Medieneintraege.") oder,
  falls ein Wechsel zu echten Umlauten gewünscht ist, dies konsistent für alle vier
  „Tabelle fuer …"-Kommentare in dieser Datei nachziehen statt nur für einen.

### VideoWebPlayer.Tests/PlaylistsE2ETests.cs (`PlaylistsE2ETests`)

- **Doppelter Code (vorheriger Befund nur teilweise behoben)** — Die neu eingeführte
  Hilfsmethode `CreatePlaylistViaUiAsync` wird ausschließlich von den neuen
  Detailseiten-Tests verwendet. Die bereits vorher vorhandenen Tests
  `Create_List_Edit_And_Delete_Playlist_HappyPath` (Zeilen 111–121),
  `UserB_Does_Not_See_UserA_Playlists` (Zeilen 211–217) sowie der erste Anlage-Schritt in
  `DuplicateName_ShowsConflictError` (Zeilen 172–176) enthalten weiterhin denselben, jetzt in
  `CreatePlaylistViaUiAsync` gekapselten Block (Navigation zu `/playlists`, Klick auf
  `#create-playlist-button`, Warten auf `#playlist-name-input`, Ausfüllen, Klick auf
  `#playlist-save-button`, `WaitForTimeoutAsync`, Lokalisieren der neuen Zeile) unverändert inline.
  Damit existieren in derselben Datei nun zwei parallele Wege, eine Playlist über die UI
  anzulegen — der neue Helper und die weiterhin bestehenden Inline-Blöcke —, obwohl die
  Empfehlung aus der vorherigen Review-Iteration ausdrücklich forderte, „alle betroffenen Tests
  (neue wie bestehende)" auf die Hilfsmethode umzustellen.

  Empfehlung: Die genannten bestehenden Tests ebenfalls auf `CreatePlaylistViaUiAsync` umstellen,
  sodass die Anlage-Logik nur noch an einer Stelle existiert.

- **Struktur und Verantwortlichkeiten — God-Klasse** — Die Klasse `PlaylistsE2ETests` deckt nach
  dieser Erweiterung zwei klar abgrenzbare fachliche Themenbereiche in einer einzigen, 430 Zeilen
  umfassenden Testklasse ab: (1) Listenansicht/CRUD/Autorisierung
  (`Create_List_Edit_And_Delete_Playlist_HappyPath`, `EmptyName_ShowsValidationError`,
  `DuplicateName_ShowsConflictError`, `Unauthenticated_Access_Shows_Error`,
  `UserB_Does_Not_See_UserA_Playlists`) und (2) die neu hinzugekommene Detailseite
  (`Load_Detail_Page_Unauthenticated_Shows_Error`, `Load_Detail_Page_ValidPlaylist_ShowsMetadata`,
  `Open_Button_In_List_Navigates_To_Detail`, `Detail_Page_Edit_Opens_Form_And_Saves`,
  `Detail_Page_Delete_Shows_Confirmation_And_Deletes`, `Detail_Page_Back_Button_Navigates_To_List`,
  `Detail_Page_Foreign_Playlist_Shows_403_Error`, `Detail_Page_Nonexistent_Playlist_Shows_404_Error`).
  Für die übrigen Playlist-Testklassen im selben Projekt ist eine Trennung nach Themenbereich
  bereits etabliertes Muster (`PlaylistsControllerTests_Auth`, `_Create`, `_Delete`, `_Read`,
  `_Update` sowie `PlaylistServiceTests_Create`, `_Delete`, `_Read`, `_Update`); `PlaylistsE2ETests`
  folgt diesem Muster bislang nicht und wächst mit dieser Änderung weiter zu einer einzigen,
  thematisch gemischten Klasse.

  Empfehlung: Die acht neuen, die Detailseite betreffenden Tests in eine eigene Klasse (z. B.
  `PlaylistDetailE2ETests`) auslagern, analog zur bestehenden Aufteilung nach Themenbereich bei
  den Controller- und Service-Tests. Gemeinsame Infrastruktur (Web-Application-Factory-Setup,
  Playwright-Lifecycle, `SeedUsersAsync`, `LoginAsync`, `CreatePlaylistViaUiAsync`) über eine
  gemeinsame Basisklasse teilen.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` (neu)
- `VideoWebPlayer/Components/Playlists/PlaylistsList.razor` (geändert)
- `VideoWebPlayer/Data/ApplicationDbContext.cs` (geändert)
- `VideoWebPlayer.Tests/PlaylistsE2ETests.cs` (geändert)
- `docs/help/playlists.md` (geändert)

Zum Abgleich zusätzlich mit gelesen (unverändert im aktuellen Arbeitsstand, als Referenz für
Namenskonventionen und bestehende Testklassen-Struktur):
- `VideoWebPlayer/Components/Playlists/PlaylistForm.razor`
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Auth.cs`,
  `_Create.cs`, `_Delete.cs`, `_Read.cs`, `_Update.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Create.cs`,
  `_Delete.cs`, `_Read.cs`, `_Update.cs`
