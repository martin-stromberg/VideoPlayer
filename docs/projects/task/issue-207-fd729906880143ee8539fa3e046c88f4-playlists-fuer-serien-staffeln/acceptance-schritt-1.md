# Abnahmeprüfung – Entwicklungsschritt 1

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Behebung der Abweichung aus dem 1. Durchlauf

Die einzige Abweichung des ersten Durchlaufs (fehlendes „Öffnen" einer Playlist aus der Übersicht,
siehe `acceptance-schritt-1.1.md`) ist im Code nachweisbar behoben:

- **Detailseite:** `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` mit
  `@page "/playlists/{id:long}"` zeigt die Stammdaten der Playlist (Name, Beschreibung,
  Sortiermodus sowie Erstellt-/Aktualisiert-Zeitpunkt) und bietet die Aktionen „Bearbeiten",
  „Loeschen" und „Zurueck zur Uebersicht" an.
- **Öffnen-Aktion in der Übersicht:** `PlaylistsList.razor` enthält je Zeile einen Button
  `.playlist-open-button` („Oeffnen"), der über `NavigationManager.NavigateTo($"/playlists/{id}")`
  auf die Detailseite navigiert — zusätzlich zu den bestehenden Aktionen „Bearbeiten" und „Loeschen".
- **Backend:** `GET api/playlists/{id}` in `VideoWebPlayer/Controllers/PlaylistsController.cs` mit
  `IPlaylistService.GetPlaylistAsync`; Clientmethode `RequestPlaylistAsync(long)` in
  `VideoWebPlayer.Client/VideoWebPlayerClient.cs`.
- Der Scoping-Hinweis ist eingehalten: Die Detailseite zeigt bewusst keine Titel-Liste (Gegenstand
  von Schritt 3), sondern nur die Stammdaten plus Bearbeiten/Löschen.

### Keine Beschädigung der übrigen Anforderungsaspekte durch die Nachbesserung

- **Löschbestätigung:** `PlaylistDeleteConfirmationDialog.razor` („Moechten Sie die Playlist … wirklich
  loeschen?") ist unverändert und wird jetzt an **beiden** Stellen verwendet — in `PlaylistsList.razor`
  (`ConfirmDeleteAsync`/`CancelDelete`) und neu auch auf der Detailseite (`showDeleteConfirmation`).
  Ein Löschen ohne Bestätigung ist an keiner Stelle möglich; erst `Confirm` ruft
  `Client.DeletePlaylistAsync` auf.
- **Privatheit/Zugriffsschutz:** `PlaylistService.cs` ist inhaltlich unverändert. Alle Endpunkte laufen
  über `[BearerTokenCheck]` und `CheckLogedIn()`; `GetPlaylistsAsync` filtert auf `UserId`,
  `GetPlaylistAsync`/`UpdatePlaylistAsync`/`DeletePlaylistAsync` werfen bei fremdem Besitzer
  `PlaylistAccessDeniedException` → HTTP 403. Die neue Detailseite ruft ausschließlich den
  abgesicherten Endpunkt auf und zeigt bei 403 „Zugriff auf diese Playlist verweigert.", bei 404
  „Playlist nicht gefunden." — es werden keine fremden Daten gerendert.
- **Pflichtfeld Name:** Serverseitig `ValidateName` → „Playlist-Name ist erforderlich." (HTTP 400),
  clientseitig zusätzlich `PlaylistForm.Validate()`. Unverändert; das Formular wird von der
  Detailseite mit demselben Parametersatz (`PlaylistId`) eingebunden wie aus der Übersicht.
- **Sortiermodus-Vorbelegung:** Weiterhin `PlaylistSortMode.ByReleaseDate` als Default in der Entität,
  `HasDefaultValue(...)` in `PlaylistConfiguration.cs`, `defaultValue: 0` in der Migration, Default in
  `DtoPlaylist`/`DtoCreatePlaylistRequest` sowie als Initialwert in `PlaylistForm.razor`.
  Bei Update ohne `SortMode` bleibt der bisherige Modus erhalten (`ParseSortMode(..., playlist.SortMode)`).
- **Sortiermodus änderbar:** Auswahlfeld in `PlaylistForm.razor` in Anlege- und Bearbeitungsfall;
  die Detailseite öffnet dasselbe Formular.
- **Kein Kopieren/Duplizieren, keine Historie:** Es existieren weiterhin weder entsprechende
  Endpunkte/Servicemethoden noch eine Versions-/Historientabelle.
- **Optionales Maximum, standardmäßig deaktiviert:** `PlaylistSettings.MaxPlaylistsPerUser` (`int?`),
  registriert über `services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"))`,
  `appsettings.json` mit `"MaxPlaylistsPerUser": null`; Prüfung nur bei gesetztem Wert.
- **Keine vorbefüllten Playlists:** Migration `20260905081404_AddPlaylistsTable` legt nur die leere
  Tabelle plus Index an, ohne Seed-Daten.
- **Dokumentation:** `docs/help/playlists.md` beschreibt jetzt „anlegen, öffnen, bearbeiten und
  löschen" sowie die Detailseite; die im 1. Durchlauf bemängelte falsche Aussage zur
  403/404-Unterscheidung wurde korrigiert (Commit `e9711b2`) und stimmt nun mit dem Code überein.

### Weitere Beobachtungen (keine Anforderungsverstöße)

- **Fortbestehende Punkte aus dem 1. Durchlauf:**
  - Namenseindeutigkeit je Anwender wird über den eindeutigen Index `IX_Playlists_UserId_Name` und
    eine Duplikatprüfung (HTTP 409) erzwungen — eine Restriktion über die Anforderung hinaus.
  - Weder `PlaylistsList.razor` noch die neue `PlaylistDetail.razor` tragen `@attribute [Authorize]`.
    Die Routen `/playlists` und `/playlists/{id}` sind direkt aufrufbar; nicht angemeldete Aufrufer
    sehen eine Fehlermeldung statt einer Weiterleitung zum Login (E2E-Test
    `Load_Detail_Page_Unauthenticated_Shows_Error` zementiert dieses Verhalten). Die Daten selbst
    bleiben durch die API-Prüfung geschützt.
  - Fehlermeldung „Ein Playlist mit diesem Namen existiert bereits." (korrekt: „Eine Playlist") in
    `PlaylistService.cs` und `PlaylistForm.razor`.
- **Encoding-Artefakt weiterhin vorhanden:** Commit `c1251f7` („Umlaute im neuen Kommentar
  wiederherstellen") hat die *umliegenden bestehenden* Zeilen in
  `VideoWebPlayer/Data/ApplicationDbContext.cs` auf korrekte Umlaute zurückgesetzt, die *neue* Zeile
  enthält aber unverändert ein Ersetzungszeichen: `/// Tabelle f�r Playlists.` (Bytes
  `EF BF BD` statt `C3 BC`). Rein kosmetisch, betrifft nur einen XML-Doc-Kommentar.
- **Uneinheitliche Umlaut-Schreibweise im UI:** Die Playlist-Oberfläche verwendet ASCII-Transliteration
  („Oeffnen", „Loeschen", „Zurueck zur Uebersicht", „Moechten Sie … wirklich loeschen?"), während der
  Rest der Anwendung echte Umlaute nutzt (z. B. `ActorDetails.razor`: „Zurück zur Übersicht",
  `MediaSourceAdmin.razor`: „Löschen"). Kein Anforderungsverstoß, aber eine sichtbare Inkonsistenz.
- **`DeletePlaylistAsync` umgeht die zentrale Reauthentifizierung:** Anders als alle übrigen Verben
  ruft `VideoWebPlayer.Client/VideoWebPlayerClient.cs` in `DeletePlaylistAsync` `httpClient.DeleteAsync`
  direkt auf, statt über `SendWithReauthorizationAsync` zu gehen. Dadurch entfällt der 401-Retry nach
  Token-Refresh und die geworfene `HttpRequestException` trägt keinen `StatusCode`. Bei abgelaufenem
  Token schlägt ein Löschvorgang daher mit einer generischen Meldung fehl, statt automatisch erneut
  zu authentifizieren. Fachlich unkritisch (die Bestätigung und der Zugriffsschutz greifen weiterhin),
  aber technisch inkonsistent.
- **Verifikation:** `dotnet build VideoPlayer.sln` läuft ohne Fehler und ohne Warnungen;
  `dotnet test --filter "FullyQualifiedName~Playlist&Category!=E2E"` meldet 36 bestandene Tests.
  Zusätzlich existieren Playwright-E2E-Tests in `VideoWebPlayer.Tests/PlaylistsE2ETests.cs` und neu in
  `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs` (Öffnen-Navigation, Metadatenanzeige, Bearbeiten,
  Löschen mit Bestätigung, Zurück-Navigation, Fehlerfälle für nicht angemeldet/fremd/nicht existent);
  diese wurden nicht ausgeführt.
