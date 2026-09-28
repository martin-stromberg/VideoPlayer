# Abnahmeprüfung – Entwicklungsschritt 3

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

Geprüfter Stand: Branch `task/issue-207-...-schritt-3-automatische-sortierung-anzeige`, HEAD `c4962bf`, Diff gegen `task/issue-207-...-playlists-fuer-serien-staffeln`.

**1. Id-Raum-Trennung in `CheckEntryAccessible` (VideoWebPlayer/Services/PlaylistService.cs, Zeilen 805–828) ist korrekt umgesetzt.**
Die aufgelöste Freischaltungs-Id wird jetzt anhand des Medientyps des Eintrags genau gegen einen der beiden Id-Räume geprüft: `Movie` und `MovieCollection` gegen `unlockedMovieCollectionIds`, `TVShow`, `TVShowSeason` und `TVShowEpisode` gegen `unlockedTVShowIds`. Die Vereinigungsprüfung ist vollständig entfallen. Der `else`-Zweig der Fallunterscheidung kann keine unbeabsichtigten Typen erfassen, da `TryParseKnownMediaType` nur die fünf in `MediaTypeHandlers` registrierten Typen zulässt (`Movie`, `MovieCollection`, `TVShow`, `TVShowSeason`, `TVShowEpisode`); ein nicht parsbarer Typ führt zu `isUnlocked = false`.
Die Id-Auflösung selbst passt zum jeweils geprüften Raum: `ResolveUnlockMediaIdAsync` liefert für `Movie` die `MovieCollectionId`, für `TVShowSeason`/`TVShowEpisode` die `TVShowId`, für `TVShow`/`MovieCollection` die eigene Id. Damit entspricht die Prüfung dem kanonischen Muster in `ItemsController` (dort Zeilen 157/190: `mediaSourceIds.Contains(...) || unlockedMovieCollectionIds.Contains(mc.Id)` bzw. `|| unlockedTVShowIds.Contains(m.Id)`).

**2. Die bereits bestätigten Aspekte sind unbeschädigt.**
Die Regel `hasSourceAccess || isUnlocked` bleibt für alle fünf Medientypen erhalten (`hasSourceAccess` unverändert über `resolvedMediaSourceId`/`mediaSourceIds`). Titel-, Titelbild- und Zugehörigkeitsauflösung (`LoadTitlesForMediaRefsAsync`, `LoadPictureIdsForMediaRefsAsync`, `GetParentTitle`), Infinity-List (`GetPlaylistEntriesPagedAsync` + `Virtualize`/`ItemsProviderAsync` in `PlaylistDetail.razor`) und die Sortier-Fallback-Kette (`SortPlaylistEntriesByReleaseDateAsync`: Erscheinungsdatum → Hierarchie → `AddedAt`) wurden vom Fix nicht berührt; der Commit ändert ausschließlich den `isUnlocked`-Block und ergänzt einen Test.

**3. Der Regressionstest deckt den Fehlerfall real ab (nicht nur formal grün).**
`GetEntries_MovieCollectionIdCollidesWithUnlockedTVShowId_MovieIsNotAccessible` (VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntries.cs) erzeugt eine freigeschaltete `TVShow` und eine nicht freigeschaltete `MovieCollection`, sichert die tatsächliche Kollision per `Assert.Equal(show.Id, collection.Id)` ab und vergibt dem Testnutzer bewusst keinen Medienquellen-Zugriff — die Erwartung `IsAccessible == false` hängt damit ausschließlich an der Id-Raum-Trennung.
Verifiziert durch Gegenprobe: mit temporär zurückgenommenem Service-Hunk (nur zur Prüfung, Arbeitsverzeichnis danach wieder sauber) schlägt genau dieser Test fehl (1 Fehler / 44), mit dem Fix sind alle 44 Tests grün. Der gesamte Playlist-Testumfang ohne E2E läuft grün (125/125).

**4. Analoge Stellen im geänderten Code: keine weitere zugriffsrelevante Id-Raum-Vermischung.**
Alle übrigen Zuordnungen im geänderten Code sind konsequent nach Medientyp geschlüsselt (`GroupMediaIdsByType`, `titlesByType`, `pictureIdsByType`, `mediaSourceMappings`, `hierarchyMappings`, `existingIdsByType`, `MediaRef`), sodass Ids nie typübergreifend verglichen werden. `mediaSourceIds` stammt aus einem einzigen Id-Raum (`MediaSourceUsers`). Der neue Controller-Endpunkt `GET {id}/entries/paged` führt keine eigene Id-Prüfung durch.
Eine nicht zugriffsrelevante Vermischung besteht im Sortier-Tiebreak: `SortPlaylistEntriesByReleaseDateAsync` sortiert nach `ParentId`, wobei dieser Wert für Episoden die `TVShowSeasonId`, für Staffeln die `TVShowId` ist (für Filme/Serien/Sammlungen `null`). Bei Einträgen ohne Erscheinungsdatum können sich dadurch Staffeln und Episoden verschiedener Serien theoretisch verschachteln. Das betrifft ausschließlich die Reihenfolge innerhalb der Ersatzsortierung (deterministisch, hierarchiebasiert) und weder Freischaltung noch Anzeige; die Fallback-Kette war in den Runden 1–3 bereits abgenommen. Empfehlung für spätere Ausbaustufen: `ParentId` je Medientyp auf einen gemeinsamen Serien-Bezug (Serien-Id) normalisieren und Episoden über Staffelnummer statt Staffel-Id ordnen.

**5. Beobachtung außerhalb dieses Schritts (kein Mangel des Schritts).**
`ItemsController.IsUnlockedAsync` (Zeile 386) prüft im Sammelzweig weiterhin `u.MovieCollectionId == entry.Id || u.TVShowId == entry.Id` und vermischt damit im bestehenden Altcode genau die beiden Id-Räume; zusätzlich wird dort für eine `TVShowSeason` die Staffel-Id statt der Serien-Id geprüft. Die Playlist-Anzeige ist nach dem Fix strenger und korrekter als diese Referenzstelle. Die Bereinigung des Altcodes gehört nicht zu Schritt 3, sollte aber als eigener Punkt vorgemerkt werden.
