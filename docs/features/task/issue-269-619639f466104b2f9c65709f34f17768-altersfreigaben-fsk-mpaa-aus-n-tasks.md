# Tasks: Altersfreigaben (FSK & MPAA) aus NFO-Dateien bzw. TMDB erfassen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `AgeRatingNormalizer` (static) + `AgeRatingResult`-Record unter `VideoWebPlayer/Utils/` anlegen (FSK 0/6/12/16/18, MPAA G/PG/PG-13/R/NC-17, Länderpräfixe „Germany:“/„DE:“; unbekannt ⇒ Raw-Fallback + `MinAge = null`) | Fertig | `AgeRatingNormalizerTests` (25 Fälle) |
| 2 | Datenmodell | `Movie`: `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`, `AgeRatingClassifiedAt` anlegen | Fertig | Build + `MovieAgeRatingTests` |
| 3 | Datenmodell | `Movie.LoadFromXml`: `<mpaa>`/`<certification>` auswerten und via `AgeRatingNormalizer` setzen | Fertig | `MovieAgeRatingTests` |
| 4 | Datenmodell | `TVShow`: `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`, `AgeRatingClassifiedAt` anlegen | Fertig | Build + `TVShowAgeRatingTests` |
| 5 | Datenmodell | `TVShow.LoadFromXml`: `<mpaa>`/`<certification>` auswerten und via `AgeRatingNormalizer` setzen | Fertig | `TVShowAgeRatingTests` |
| 6 | Datenmodell | EF-Migration `AddAgeRatings` erzeugen (8 nullable Spalten auf `Movies`/`TVShows`) | Fertig | Migration `20261025181945_AddAgeRatings` |
| 7 | Backup | `VideoWebPlayerBackupData.OptionalRestoreColumns` um die 8 neuen `Movies.*`/`TVShows.*`-Spalten erweitern | Fertig | `VideoWebPlayerBackupDataTests_AgeRatings` |
| 8 | Online-Quelle | `TmdbOptions` unter `VideoWebPlayer/Configuration/` anlegen (`Enabled`, `ApiKey`, `BaseUrl`, `PreferredCountry`, `TimeoutSeconds`, Effective-Getter) | Fertig | `TmdbAgeRatingServiceTests` |
| 9 | Online-Quelle | `ITmdbAgeRatingService`-Interface + `TmdbAgeRating`-Record anlegen | Fertig | Build |
| 10 | Online-Quelle | `TmdbAgeRatingService` implementieren (named HttpClient „Tmdb“, `release_dates`/`content_ratings`, Titelsuche ohne ID, Länderwahl DE→US→erste, fehlertolerant `null` statt Exception) | Fertig | `TmdbAgeRatingServiceTests` (10 Fälle) |
| 11 | Konfiguration | DI-Registrierung in `ServiceCollectionExtensions`: `Configure<TmdbOptions>`, `AddHttpClient("Tmdb")`, `AddSingleton<ITmdbAgeRatingService, TmdbAgeRatingService>` | Fertig | Build + E2E |
| 12 | Konfiguration | `appsettings.json`: `Tmdb`-Abschnitt mit Defaults (`Enabled: false`, `ApiKey: null`) und `AutoUpdate:ProtectedFiles`-`JsonKeys` um die `Tmdb:*`-Keys erweitern | Fertig | — |
| 13 | Scan | `MediaSourceClassifier`: optionaler Ctor-Parameter `ITmdbAgeRatingService?`, `ExtractTmdbId`, `TryApplyOnlineAgeRatingAsync` | Fertig | `LocalMediaSourceClassifierTests_AgeRating` |
| 14 | Scan | `ProcessCollectionAsMovieAsync`: `AgeRatingClassifiedAt`-Marker + Online-Fallback bei fehlendem `AgeRatingRaw` (unter `!IsManuallyEdited`) | Fertig | `LocalMediaSourceClassifierTests_AgeRating` |
| 15 | Scan | `CreateOrUpdateTVShow`: `AgeRatingClassifiedAt`-Marker + Online-Fallback analog | Fertig | `LocalMediaSourceClassifierTests_AgeRating` |
| 16 | Backfill | `TryReadNfoAsync(MediaCollection, …)` aus `TryReadActorNfoAsync` extrahieren (Collection-Walk, Kandidatenliste) | Fertig | `MediaSourceClassifierAgeRatingBackfillTests` + Actor-Backfill-Tests grün |
| 17 | Backfill | `BackfillMissingAgeRatingsAsync` implementieren (Movies + TVShows mit `AgeRatingClassifiedAt == null`, `IsManuallyEdited`-Skip, `movie.nfo`/`tvshow.nfo`-Fallbacks, Marker setzen, Fehler pro Eintrag + `DiscardPendingChanges`) | Fertig | `MediaSourceClassifierAgeRatingBackfillTests` (5 Fälle) |
| 18 | Backfill | `ActorBackfillWorker.ExecuteAsync` ruft zusätzlich `BackfillMissingAgeRatingsAsync` (gleiche Gate-Lease, eigener try/catch) | Fertig | Build |
| 19 | API/DTO | `DtoMovie` + `DtoTVShow`: `AgeRating` (string?) und `AgeRatingMinAge` (int?) ergänzen | Fertig | `AgeRatingE2ETests` (DTO über API) |
| 20 | API/DTO | `MediaMetadataUpdateRequest.AgeRating` (string?) ergänzen | Fertig | `MediaMetadataEditorServiceTests` |
| 21 | Editieren | `MediaMetadataEditorService`: `UpdateMovieAsync`/`UpdateTVShowAsync` setzen Freigabe (normalisiert; leer ⇒ löschen); `ValidateAgeRatingValue` + `ValidateNoAgeRating` für andere Typen | Fertig | `MediaMetadataEditorServiceTests` (7 neue Fälle) |
| 22 | UI | `MetadataEditModel.AgeRating` (string, `[StringLength(64)]`) ergänzen | Fertig | Build + E2E |
| 23 | UI | `MovieCollectionDetails.razor`: `editSupportsAgeRating`, `InputSelect` mit kanonischen Stufen im Edit-Form, `BeginEdit`-Vorbelegung, `SaveEditAsync`-Request | Fertig | `AgeRatingE2ETests` |
| 24 | UI | `TVShowDetails.razor`: `editSupportsAgeRating`, `InputSelect`, `BeginEdit`-Vorbelegung, `SaveEditAsync`-Request | Fertig | `AgeRatingE2ETests` |
| 25 | UI | `MovieCollectionDetails.razor`: Freigabe-Badge in der Film-Meta-Zeile (nur wenn nicht leer) | Fertig | `AgeRatingE2ETests` |
| 26 | UI | `TVShowDetails.razor`: Freigabe-Badge in Serien-Meta-Zeile + vererbte Anzeige in Staffel-/Episoden-Meta-Zeilen (`show.AgeRating`) | Fertig | `AgeRatingE2ETests` |
| 27 | UI | `.agerating-badge`-CSS-Klasse in `VideoWebPlayer/wwwroot/app.css` ergänzen | Fertig | E2E-Sichtprüfung |
| 28 | Tests | `AgeRatingNormalizerTests` (Theorien für FSK-/MPAA-/Länderpräfix-/unbekannte Werte) | Fertig | 25 Fälle grün |
| 29 | Tests | `LoadFromXml`-Tests für `Movie` und `TVShow` (`<mpaa>`, `<certification>`-Fallback, fehlendes Element) | Fertig | `MovieAgeRatingTests` + `TVShowAgeRatingTests` |
| 30 | Tests | `LocalMediaSourceClassifierTests_AgeRating`: NFO-Scan→Persistenz Film + Serie, `IsManuallyEdited`-Schutz, TMDB-Fallback (Fake-Service), NFO-Priorität vor Online | Fertig | 5 Fälle grün |
| 31 | Tests | `MediaSourceClassifierAgeRatingBackfillTests`: Altbestand-Befüllung, Marker, manuelle Einträge, `movie.nfo`/`tvshow.nfo`-Fallbacks | Fertig | 5 Fälle grün |
| 32 | Tests | `TmdbAgeRatingServiceTests` mit gefaketem `HttpMessageHandler`: DE-Bevorzugung, US-Fallback, Titelsuche, deaktiviert/kein Key ⇒ null, HTTP-Fehler/Timeout ⇒ null | Fertig | 10 Fälle grün |
| 33 | Tests | `MediaMetadataEditorServiceTests`: Rating setzen/löschen, unbekannter Wert abgelehnt, Rating für `moviecollection`/`tvshowseason`/`tvshowepisode` abgelehnt | Fertig | 7 Fälle grün |
| 34 | Tests | `VideoWebPlayerBackupDataTests_AgeRatings`: Alt-Backup ohne die 8 Spalten restauriert fehlerfrei (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Fertig | 1 Fall grün |
| 35 | Tests | `FakeTmdbAgeRatingService`-Hilfsklasse unter `VideoWebPlayer.Tests/Helpers/` | Fertig | Verwendet in #30 |
| 36 | E2E-Tests | Scan→Anzeige: Film-NFO mit `<mpaa>` → Badge in `MovieCollectionDetails` | Fertig | `AgeRatingE2ETests.MovieNfoRating_ShowsBadgeOnMovieDetails` |
| 37 | E2E-Tests | Serien-Freigabe in `TVShowDetails` sichtbar + vererbte Anzeige in Episodenansicht | Fertig | `AgeRatingE2ETests.TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt` |
| 38 | E2E-Tests | Admin setzt Freigabe über Edit-Dropdown und speichert → Badge sichtbar | Fertig | `AgeRatingE2ETests.Admin_SetsAgeRating_ViaEditDropdown` |
| 39 | Dokumentation | `docs/help/medien-editiermodus.md` + `docs/help/medienquellen/*` (NFO-Felder `<mpaa>`/`<certification>`) ergänzen | Fertig | — |
| 40 | Dokumentation | `docs/SECRETS_MANAGEMENT.md`: Tabellenzeile `Tmdb:ApiKey` ergänzen | Fertig | — |
| 41 | Dokumentation | `README.md` + `docs/RELEASE_NOTES.md` aktualisieren | Fertig | — |
