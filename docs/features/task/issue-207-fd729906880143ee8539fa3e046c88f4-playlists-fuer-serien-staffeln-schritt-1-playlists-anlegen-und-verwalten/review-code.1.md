# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### docs/help/playlists.md (Abschnitt "Zugriff und Berechtigungen")

- **Dokumentation / Sachliche Korrektheit** — Der Satz „Der Zugriff auf eine fremde Playlist wird
  mit HTTP 403 (Forbidden) abgelehnt, unabhängig davon, ob die Playlist existiert." (Zeile 40–41)
  gibt das tatsächliche Verhalten falsch wieder. Laut `PlaylistService.GetPlaylistAsync`
  (`VideoWebPlayer/Services/PlaylistService.cs`, Zeilen 54–64) und `PlaylistsController.GetPlaylist`
  (`VideoWebPlayer/Controllers/PlaylistsController.cs`, Zeilen 60–86) gilt:
  - Existiert die angefragte Playlist-ID nicht, liefert der Service `null` und der Controller
    antwortet mit **404 Not Found**.
  - Existiert die Playlist, gehört aber einem anderen Anwender, wirft der Service eine
    `PlaylistAccessDeniedException` und der Controller antwortet mit **403 Forbidden**.

  Der Statuscode unterscheidet sich also genau danach, ob die fremde Playlist existiert oder nicht
  ("403 bei existierender fremder Playlist, 404 bei nicht existierender ID") — das Gegenteil dessen,
  was der Text behauptet ("unabhängig davon, ob die Playlist existiert"). Dadurch ist über den
  Statuscode erkennbar, ob eine fremde Playlist-ID existiert, was die Dokumentation aktuell verdeckt
  bzw. falsch darstellt. Die zugehörigen neuen E2E-Tests
  (`VideoWebPlayer.Tests/PlaylistsE2ETests.cs`, `Detail_Page_Foreign_Playlist_Shows_403_Error` und
  `Detail_Page_Nonexistent_Playlist_Shows_404_Error`) bestätigen genau dieses (unterscheidbare)
  Verhalten und stehen damit im Widerspruch zur Dokumentation.

  Empfehlung: Den Absatz präzisieren, z. B.:
  „Alle Playlist-Funktionen erfordern eine Anmeldung. Der Zugriff auf eine Playlist, die einem
  anderen Anwender gehört, wird mit HTTP 403 (Forbidden) abgelehnt. Existiert die angefragte
  Playlist-ID gar nicht, wird stattdessen HTTP 404 (Not Found) zurückgegeben. Da sich diese beiden
  Fälle im Statuscode unterscheiden, lässt sich über die Antwort erkennen, ob eine fremde
  Playlist-ID existiert." Die vorherige, ältere Formulierung („unabhängig davon, ob die Playlist
  existiert") war zumindest im Anspruch korrekt (keine Unterscheidbarkeit); die neue Formulierung
  behauptet fälschlich denselben Anspruch, obwohl das neu eingeführte Verhalten (403 vs. 404)
  diesen Anspruch gerade verletzt. Die Doku sollte das tatsächliche, unterscheidbare Verhalten
  benennen statt es (unzutreffend) als "unabhängig von der Existenz" darzustellen.

### VideoWebPlayer.Tests/PlaylistsE2ETests.cs (`PlaylistsE2ETests`)

- **Testqualität — schwache/nicht-deterministische Assertion** — `Load_Detail_Page_Unauthenticated_Shows_Error`
  prüft `Assert.True(redirectedToLogin || showsError)` statt eines konkreten, eindeutig erwarteten
  Verhaltens. Der bereits vorhandene Test `Unauthenticated_Access_Shows_Error` (für die
  Listenansicht) prüft dagegen deterministisch genau ein erwartetes Element
  (`#playlists-load-error`). Die Oder-Verknüpfung im neuen Test lässt offen, welches Verhalten
  tatsächlich spezifiziert ist, und der Test würde auch dann noch grün bleiben, wenn sich das
  Verhalten unbeabsichtigt zwischen den beiden Alternativen verschiebt.

  Empfehlung: Das tatsächlich gewünschte Verhalten festlegen (Redirect zum Login **oder**
  Fehleranzeige auf der Detailseite — nicht beides als akzeptabel) und den Test auf genau dieses
  eine Verhalten umstellen, analog zu `Unauthenticated_Access_Shows_Error`.

- **Doppelter Code** — Die sechs neuen Testmethoden (`Load_Detail_Page_ValidPlaylist_ShowsMetadata`,
  `Open_Button_In_List_Navigates_To_Detail`, `Detail_Page_Edit_Opens_Form_And_Saves`,
  `Detail_Page_Delete_Shows_Confirmation_And_Deletes`, `Detail_Page_Back_Button_Navigates_To_List`,
  `Detail_Page_Foreign_Playlist_Shows_403_Error`) wiederholen jeweils denselben, nahezu identischen
  Block zum Anlegen einer Playlist über die UI (Navigation zu `/playlists`, Klick auf
  `#create-playlist-button`, Warten auf `#playlist-name-input`, Ausfüllen, Klick auf
  `#playlist-save-button`, `WaitForTimeoutAsync`, Lokalisieren der neuen Zeile). Dieser Block war
  zuvor bereits in mehreren Tests dupliziert; die neuen Tests vergrößern die bestehende Duplikation
  weiter, ohne die Gelegenheit zu nutzen, sie in eine gemeinsame Hilfsmethode auszulagern.

  Empfehlung: Eine private Hilfsmethode einführen, z. B.
  `private async Task<ILocator> CreatePlaylistViaUiAsync(string name, string? description = null)`,
  die Navigation, Erstellung und Rückgabe der Zeilen-Locator kapselt, und alle betroffenen Tests
  (neue wie bestehende) darauf umstellen.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` (neu)
- `VideoWebPlayer/Components/Playlists/PlaylistsList.razor` (geändert)
- `VideoWebPlayer/Data/ApplicationDbContext.cs` (geändert)
- `VideoWebPlayer.Tests/PlaylistsE2ETests.cs` (geändert)
- `docs/help/playlists.md` (geändert)

Zur Verifikation des tatsächlichen Zugriffsverhaltens zusätzlich mit gelesen (unverändert im
aktuellen Arbeitsstand, aber Grundlage für den Befund zu `docs/help/playlists.md`):
- `VideoWebPlayer/Controllers/PlaylistsController.cs`
- `VideoWebPlayer/Services/PlaylistService.cs`
