# Abnahmeprüfung – Entwicklungsschritt 3

## Ergebnis

**Status:** Abweichungen gefunden

## Abweichungen

- [ ] **Die Freischaltungsprüfung stuft Filme, Staffeln und Episoden grundsätzlich als nicht freigeschaltet ein; sie werden dadurch immer ausgegraut dargestellt.** Die Anforderung verlangt, dass *Titel, für die der Anwender keine Freischaltung besitzt*, ausgegraut werden — im Umkehrschluss dürfen Titel, für die er eine Freischaltung besitzt, nicht ausgegraut sein. `PlaylistService.IsEntryAccessible` (`VideoWebPlayer/Services/PlaylistService.cs`, Zeilen 660–671) liefert jedoch für die Medientypen `Movie`, `TVShowSeason` und `TVShowEpisode` unbedingt `false`:

  ```csharp
  return parsedType switch
  {
      MediaType.MovieCollection => unlockedMovieCollectionIds.Contains(entry.MediaId),
      MediaType.TVShow => unlockedTVShowIds.Contains(entry.MediaId),
      _ => false
  };
  ```

  Da `PlaylistDetail.razor` (Zeile 104) genau dieses Flag auf die Ausgrau-Klassen `opacity-50 text-muted` abbildet, erscheint in einer Serien-/Staffel-Playlist — dem tragenden Anwendungsfall dieses Projekts — jede Staffel- und Episodenzeile ausgegraut, auch für den vollständig berechtigten Besitzer der Playlist. Das Verhalten ist in `docs/help/playlists-api.md` (Abschnitt „Hinweis zu `IsAccessible`") ausdrücklich als beabsichtigt dokumentiert („Einträge der Typen `Movie`, `TVShowSeason` und `TVShowEpisode` liefern daher immer `false`"), widerspricht aber der Anforderung. Im Projekt existiert bereits die korrekte Auflösung über die Elternbeziehung: `ItemsController.IsUnlockedAsync` (`VideoWebPlayer/Controllers/ItemsController.cs`, Zeilen 372–386) leitet für einen `Movie` die `MovieCollectionId` und für eine `TVShowEpisode` über die Staffel die `TVShowId` ab und prüft damit `UnlockedMediaEntries`. Die Playlist-Einträge tragen mit `ParentMediaType`/`ParentMediaId` sogar bereits die dafür nötige Information.

- [ ] **Die Freischaltungsprüfung ignoriert den Quellenzugriff (`MediaSourceUsers`) und graut damit auch Titel aus, die der Anwender starten kann.** Die maßgebliche Zugriffsregel der Anwendung ist `ItemsController.EnsureAccessAsync` (`VideoWebPlayer/Controllers/ItemsController.cs`, Zeilen 503–516): Zugriff besteht, wenn die Medienquelle für den Benutzer freigegeben ist **oder** der Inhalt einzeln freigeschaltet wurde (`if (!hasSourceAccess && !isUnlocked) throw ...`). `PlaylistService.LoadUnlockedMediaIdsAsync`/`IsEntryAccessible` fragen ausschließlich `IUnlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync`/`GetUnlockedTVShowIdsForUserAsync` ab, also nur die Einzelfreischaltungen aus `UnlockedMediaEntries`. Eine Serie oder Filmsammlung aus einer dem Anwender freigegebenen Quelle — der Regelfall der Rechtevergabe — wird dadurch als „nicht freigeschaltet" ausgegraut, obwohl sie über `ItemsController` abrufbar und streambar ist. Die Projekt-Anforderung schreibt hierzu ausdrücklich vor: „Zugriff auf Videos: Reutilisiere bestehende Zugriffskontrolle für Videos (MediaSource-Zugriff, Freischaltung)" (`requirement.md`, Zeile 213); der `MediaSource`-Anteil fehlt vollständig.

## Hinweise

### Nachgeprüfte Abweichungen der vorherigen Runde

- **Abweichung 1 (fehlendes Titelbild) ist behoben.** `DtoPlaylistEntry.ResolvedPictureId` (`VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`) wird serverseitig aufgelöst; jeder der fünf `MediaTypeHandlers` besitzt ein `LoadPictureIdsAsync` mit der Kette Poster → Banner → Fanart, gebulkt pro Medientyp (`PlaylistService.LoadPictureIdsForMediaRefsAsync`). `PlaylistDetail.razor` (Zeile 106) rendert daraus ein `<img class="media-poster playlist-entry-image">` mit Platzhalter-Fallback über `onerror` bzw. `GetImageUrl`. Abgedeckt durch `PlaylistServiceTests_GetEntries.GetEntries_ResolvesResolvedPictureId_FromMediaEntity`, `..._NoPictureSet_ResolvedPictureIdIsNull` und den E2E-Test `PlaylistDetail_DisplaysImageForEachEntry`.

- **Abweichung 2 (Platzhalter `IsAccessible = true`) ist nur teilweise behoben.** Der fest verdrahtete Wert ist beseitigt, die Prüfung läuft echt über `IUnlockedMediaService` und ist als Bulk-Abfrage (zwei Abfragen pro Anfrage, kein N+1) umgesetzt. Die *Semantik* der Prüfung ist jedoch unvollständig — siehe die beiden Abweichungspunkte oben.

- **Der Hinzufügen-Pfad ist konsistent mitgezogen.** `AddMediaToPlaylistAsync` → `BuildAddResultAsync` → `BuildEntryDtosAsync` verwendet exakt dieselbe DTO-Aufbaulogik wie `GetPlaylistEntriesAsync` und `GetPlaylistEntriesPagedAsync`, inklusive Bild- und Freischaltungsauflösung mit demselben `userId`. Bestätigt durch `PlaylistServiceTests_AddMedia.AddMedia_TVShowUnlockedForCurrentUser_TopLevelEntryIsAccessible`, `..._ResolvesResolvedPictureId_FromMediaEntity` u. a. Die oben genannten Semantikfehler wirken sich damit allerdings ebenfalls einheitlich auf alle drei Pfade aus.

- **Abweichung 3 (`disabled` am Entfernen-Button) ist behoben.** In `PlaylistDetail.razor` (Zeile 113) besitzt der Entfernen-Button keine `disabled`-Bindung mehr; der E2E-Test `PlaylistDetail_RemoveButton_EnabledForAllEntries` sichert das ab. Eine Start-/Abspielaktion existiert in der Eintragsliste weiterhin nicht — laut `steps.md`/`project-plan.md` ist die „Wiedergabe aus einer Playlist heraus" Schritt 5, weshalb dies hier kein Abweichungspunkt ist. Der Aspekt „lassen sich nicht starten" ist damit in Schritt 3 lediglich mangels vorhandener Startaktion trivial erfüllt und muss in Schritt 5 tatsächlich durchgesetzt werden.

### Nicht beschädigte Anforderungsaspekte

- **Infinity-List/Virtual Scrolling ist unverändert funktionsfähig.** `PlaylistDetail.razor` nutzt `<Virtualize ItemsProvider="ItemsProviderAsync">` mit `<Placeholder>` und scrollbarem Container (`PlaylistDetail.razor.css`: `max-height: 480px; overflow-y: auto`); `ItemsProviderAsync` lädt über `GET api/playlists/{id}/entries/paged` bedarfsgesteuert nach und serialisiert die Ladevorgänge über ein `SemaphoreSlim`. Die Playwright-Tests `PlaylistDetail_LoadsFirstPage_OnInitialize` (rendert nachweislich nur eine Teilmenge von 25 Einträgen), `PlaylistDetail_LoadsNextPage_OnScrollNearEnd` und `PlaylistDetail_StopsLoading_WhenHasNextPageFalse` belegen das im echten Browser.

- **Automatische Sortierung mit Fallback-Kette ist unverändert korrekt.** `SortPlaylistEntriesByReleaseDateAsync` sortiert `ReleaseDate` → `ParentId` → `SequenceNumber` → `AddedAt`, also Erscheinungsdatum, ersatzweise Serien-/Staffel-/Episodenreihenfolge, ersatzweise Aufnahmezeitpunkt. Im Modus `Manual` wird nach `AddedAt` sortiert; eine manuelle Umsortierung wird nicht angeboten (erwartungsgemäß, Schritt 4). Abgedeckt durch die Testklasse `PlaylistServiceTests_GetEntriesPaged`.

- **Sanity-Check.** `dotnet build VideoWebPlayer/VideoWebPlayer.csproj` läuft ohne Fehler und Warnungen durch; `dotnet test --filter "FullyQualifiedName~Playlist"` meldet 134 von 134 bestandenen Tests. Die Nachbesserung hat also keine bestehenden Tests beschädigt — die vorhandenen Tests schreiben allerdings die oben beanstandete Semantik fest (`GetEntries_EntryNotUnlocked_IsNotAccessible` prüft nur `TVShow`; kein Test deckt einen Film oder eine Episode mit bestehender Berechtigung ab).

### Weitere Beobachtungen ohne Abweichungscharakter

- **Einträge ohne Erscheinungsdatum werden ganz nach vorne einsortiert.** Die Sortierung erfolgt in-memory über LINQ-to-Objects, wo `null` vor allen Werten liegt. Die Anforderung legt die Platzierung dieser Gruppe nicht fest; die Fallback-Reihenfolge innerhalb der Gruppe ist korrekt.

- **Die Staffel-Reihenfolge wird aus der Datensatz-Id abgeleitet, nicht aus einer Staffelnummer** (`MediaTypeHandlers[MediaType.TVShowSeason].GetHierarchySequenceAsync` gruppiert je Serie und ordnet nach `s.Id`). Das trifft nur zu, solange die Id-Reihenfolge der fachlichen Staffelreihenfolge entspricht. Für `TVShow`, `Movie` und `MovieCollection` liefert `NoHierarchyAsync` keine Hierarchieinformation, sodass die Kette dort direkt auf `AddedAt` durchfällt.

- **Skalierungsverhalten des Nachladens.** `GetPlaylistEntriesPagedAsync` ruft `LoadValidPlaylistEntriesAsync` auf, das *alle* Einträge der Playlist lädt (inklusive Waisenerkennung), und sortiert anschließend die vollständige Menge im Speicher, bevor die angeforderte Seite herausgeschnitten wird. Titel-, Bild- und Freischaltungsauflösung sind korrekt auf die Seite begrenzt, das Laden und Sortieren nicht. Jeder Scroll-Nachladevorgang liest und sortiert damit erneut die gesamte Eintragsmenge — funktional korrekt, aber im Spannungsfeld zu „auch bei sehr vielen Einträgen flüssig bedienbar".

- **Fehlerfall der Erstladung.** Schlägt `LoadInitialPageAsync` fehl, bleibt `hasMorePages` auf dem Initialwert `true`; die Bedingung `allEntries.Count == 0 && !hasMorePages` unterdrückt dadurch den Leerzustand, und der Hinweis „Weitere Eintraege werden beim Scrollen geladen." steht neben der Fehlermeldung.

- **`DtoPlaylistEntry.IsAccessible` ist weiterhin mit `= true` vorinitialisiert.** Der Server setzt den Wert in `BuildEntryDtosAsync` immer explizit, sodass das aktuell folgenlos ist; als Default für ein Zugriffsflag ist `false` (fail-closed) dennoch die sicherere Wahl.

**Prüfungsumfang:** `git diff task/issue-207-…-playlists-fuer-serien-staffeln...HEAD` (30 Dateien, Commits `b135889`, `b000aa3`, `ad7e428`, `19ceb29`, `936346b`) sowie Lesen von `VideoWebPlayer/Services/PlaylistService.cs`, `VideoWebPlayer/Controllers/PlaylistsController.cs`, `VideoWebPlayer/Controllers/ItemsController.cs`, `VideoWebPlayer/Controllers/SourcesController.cs`, `VideoWebPlayer/Services/UnlockedMediaService.cs`, `VideoWebPlayer/Services/IUnlockedMediaService.cs`, `VideoWebPlayer/Configuration/PlaylistSettings.cs`, `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor(.css)`, `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`, `VideoWebPlayer.Client/Models/DtoPlaylistEntriesPagedResult.cs` sowie der Testklassen `PlaylistServiceTests_AddMedia`, `PlaylistServiceTests_GetEntries`, `PlaylistServiceTests_GetEntriesPaged`, `PlaylistDetailE2ETests` und der Helper. Die Einschätzung der vorherigen Runde (`acceptance-schritt-3.1.md`) wurde nicht übernommen, sondern unabhängig am Code nachvollzogen.
