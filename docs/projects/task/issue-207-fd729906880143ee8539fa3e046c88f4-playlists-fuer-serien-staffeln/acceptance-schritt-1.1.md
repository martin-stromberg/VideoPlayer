# Abnahmeprüfung – Entwicklungsschritt 1

## Ergebnis

**Status:** Abweichungen gefunden

## Abweichungen

- [ ] **Playlist aus der Übersicht öffnen fehlt.** Die Anforderung verlangt eine Übersicht, aus der ein Anwender eine Playlist „öffnen, bearbeiten oder löschen" kann. `VideoWebPlayer/Components/Playlists/PlaylistsList.razor` bietet je Zeile ausschließlich die Aktionen „Bearbeiten" (öffnet den Bearbeitungsdialog `PlaylistForm.razor`) und „Loeschen". Eine Aktion zum Öffnen einer Playlist existiert nicht; es gibt auch keine Detail-/Playlist-Route (einzige Playlist-Route ist `@page "/playlists"`, weitere Razor-Seiten zu Playlists sind nicht vorhanden). Auch die Hilfeseite `docs/help/playlists.md` beschreibt die Übersicht nur mit „anlegen, bearbeiten oder löschen". Das Öffnen einer Playlist ist damit in diesem Schritt gar nicht umgesetzt (inhaltlich adressiert der Projektplan die Inhaltsanzeige erst in Schritt 3, siehe Hinweise).

## Hinweise

Alle übrigen fachlichen Aspekte der Anforderung sind im Code nachweisbar umgesetzt:

- **Anlegen, Umbenennen, Löschen:** `VideoWebPlayer/Services/PlaylistService.cs` (`CreatePlaylistAsync`, `UpdatePlaylistAsync`, `DeletePlaylistAsync`), `VideoWebPlayer/Controllers/PlaylistsController.cs` (POST/PUT/DELETE `api/playlists`), UI in `PlaylistsList.razor` und `PlaylistForm.razor`.
- **Name, optionale Beschreibung, Sortiermodus:** `VideoWebPlayer/Data/Playlist.cs` mit `Name` (Pflicht), `Description` (nullable) und `SortMode`; `VideoWebPlayer/Data/PlaylistSortMode.cs` enthält genau die beiden Werte `ByReleaseDate` und `Manual`.
- **Sortiermodus beim Anlegen wählbar und später änderbar:** Auswahlfeld in `PlaylistForm.razor` sowohl im Anlege- als auch im Bearbeitungsfall; `UpdatePlaylistAsync` übernimmt den geänderten Modus.
- **Vorbelegung automatische Sortierung:** Default `PlaylistSortMode.ByReleaseDate` in der Entität, `HasDefaultValue(...)` in `PlaylistConfiguration.cs`, `defaultValue: 0` in der Migration, Default im DTO `DtoCreatePlaylistRequest`/`DtoPlaylist` sowie im Formular-Initialwert.
- **Löschbestätigung:** Modaler Bestätigungsdialog in `PlaylistsList.razor` („Moechten Sie die Playlist ... wirklich loeschen?") mit `ConfirmDeleteAsync`/`CancelDelete`; erst die Bestätigung ruft `DeletePlaylistAsync` auf.
- **Privatheit:** Alle Endpunkte laufen über `[BearerTokenCheck]` und `CheckLogedIn()`; jede Service-Operation filtert bzw. prüft auf `UserId` und wirft sonst `PlaylistAccessDeniedException` (HTTP 403). `GetPlaylistsAsync` liest ausschließlich Playlists des angemeldeten Benutzers.
- **Kein Kopieren/Duplizieren, keine Historie:** Es existieren weder entsprechende Endpunkte/Servicemethoden noch eine Versions- oder Historientabelle.
- **Pflichtfeld Name mit verständlicher Meldung:** Serverseitig `ValidateName` → „Playlist-Name ist erforderlich." (HTTP 400), clientseitig zusätzlich in `PlaylistForm.Validate()`.
- **Optionales Maximum, standardmäßig deaktiviert:** `PlaylistSettings.MaxPlaylistsPerUser` (`int?`), Registrierung über `services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"))`, `appsettings.json` mit `"MaxPlaylistsPerUser": null`; Prüfung nur bei `MaxPlaylistsPerUser is int maxPlaylists`.
- **Keine vorbefüllten Playlists für Bestandsinstallationen:** Die Migration `20260905081404_AddPlaylistsTable` legt ausschließlich eine leere Tabelle plus Index an, ohne Seed-Daten.

Weitere Beobachtungen (kein Anforderungsverstoß, aber prüfenswert):

- **Zusätzliche Einschränkung Namenseindeutigkeit:** Die Anforderung fordert keine eindeutigen Playlist-Namen. Der Code erzwingt sie dennoch über einen eindeutigen Index `IX_Playlists_UserId_Name` (`PlaylistConfiguration.cs`, Migration) sowie eine Duplikatprüfung in Create/Update (HTTP 409). Ein Anwender kann damit keine zwei gleichnamigen Playlists anlegen — eine Restriktion über die Anforderung hinaus.
- **Existenz fremder Playlists erkennbar:** `GetPlaylistAsync`/`UpdatePlaylistAsync`/`DeletePlaylistAsync` unterscheiden zwischen 404 (existiert nicht) und 403 (gehört einem anderen Anwender). Damit lässt sich über geratene IDs feststellen, ob eine fremde Playlist existiert. `docs/help/playlists.md` behauptet dagegen, der Zugriff werde abgelehnt „unabhängig davon, ob die Playlist existiert" — Doku und Code weichen hier voneinander ab.
- **Keine UI-seitige Autorisierung der Seite:** `PlaylistsList.razor` trägt kein `[Authorize]`. Der Navigationseintrag liegt zwar in `AuthorizeView`, die Route `/playlists` ist aber direkt aufrufbar; nicht angemeldete Aufrufer sehen dann eine Fehlermeldung statt einer Weiterleitung zum Login. Die Daten selbst bleiben durch die API-Prüfung geschützt.
- **Sprachliche Details:** Fehlermeldung „Ein Playlist mit diesem Namen existiert bereits." (korrekt: „Eine Playlist"), in `PlaylistService.cs` und `PlaylistForm.razor`. In `VideoWebPlayer/Data/ApplicationDbContext.cs` enthält der neue Kommentar „Tabelle f�r Playlists." ein Ersetzungszeichen statt „ü" (Encoding-Artefakt).
- **Einordnung der Abweichung:** Laut `docs/projects/task/.../steps.md` behandeln erst Schritt 2/3 das Befüllen und Anzeigen von Playlist-Inhalten. Das „Öffnen" aus der Übersicht ist im Wortlaut der Schritt-1-Anforderung dennoch enthalten und aktuell nirgends umgesetzt.
- **Verifikation:** `dotnet build VideoPlayer.sln` läuft ohne Fehler/Warnungen; `dotnet test --filter "FullyQualifiedName~Playlist&Category!=E2E"` meldet 36 bestandene Tests (Service-, Controller- und Auth-Tests). Zusätzlich existieren Playwright-E2E-Tests in `VideoWebPlayer.Tests/PlaylistsE2ETests.cs` (nicht ausgeführt).
