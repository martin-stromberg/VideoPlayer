# Tasks: Altersfreigaben (FSK & MPAA) aus NFO-Dateien bzw. TMDB erfassen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `AgeRatingNormalizer` (static) + `AgeRatingResult`-Record unter `VideoWebPlayer/Utils/` anlegen (FSK 0/6/12/16/18, MPAA G/PG/PG-13/R/NC-17, Länderpräfixe „Germany:“/„DE:“; unbekannt ⇒ Raw-Fallback + `MinAge = null`) | Erledigt | `AgeRatingNormalizerTests` (25 Fälle) |
| 2 | Datenmodell | `Movie`: `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`, `AgeRatingClassifiedAt` anlegen | Erledigt | `MovieAgeRatingTests` |
| 3 | Datenmodell | `Movie.LoadFromXml`: `<mpaa>`/`<certification>` auswerten und via `AgeRatingNormalizer` setzen | Erledigt | `MovieAgeRatingTests` |
| 4 | Datenmodell | `TVShow`: `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`, `AgeRatingClassifiedAt` anlegen | Erledigt | `TVShowAgeRatingTests` |
| 5 | Datenmodell | `TVShow.LoadFromXml`: `<mpaa>`/`<certification>` auswerten und via `AgeRatingNormalizer` setzen | Erledigt | `TVShowAgeRatingTests` |
| 6 | Datenmodell | EF-Migration `AddAgeRatings` erzeugen (8 nullable Spalten auf `Movies`/`TVShows`) | Erledigt | Migration `20261025181945_AddAgeRatings` + `VideoWebPlayerBackupDataTests_AgeRatings` |
| 7 | Backup | `VideoWebPlayerBackupData.OptionalRestoreColumns` um die 8 neuen `Movies.*`/`TVShows.*`-Spalten erweitern | Erledigt | `VideoWebPlayerBackupDataTests_AgeRatings.ReadFromAsync_LegacyBackupWithoutAgeRatingColumns_RestoresSuccessfully` |
| 8 | Online-Quelle | `TmdbOptions` unter `VideoWebPlayer/Configuration/` anlegen (`Enabled`, `ApiKey`, `BaseUrl`, `PreferredCountry`, `TimeoutSeconds`, Effective-Getter) | Erledigt | `TmdbAgeRatingServiceTests` |
| 9 | Online-Quelle | `ITmdbAgeRatingService`-Interface + `TmdbAgeRating`-Record anlegen | Erledigt | `TmdbAgeRatingServiceTests` + `FakeTmdbAgeRatingService` (Interface-Verbrauch) |
| 10 | Online-Quelle | `TmdbAgeRatingService` implementieren (named HttpClient „Tmdb“, `release_dates`/`content_ratings`, Titelsuche ohne ID, Länderwahl DE→US→erste, fehlertolerant `null` statt Exception) | Erledigt | `TmdbAgeRatingServiceTests` (10 Fälle) |
| 11 | Konfiguration | DI-Registrierung in `ServiceCollectionExtensions`: `Configure<TmdbOptions>`, `AddHttpClient("Tmdb")`, `AddSingleton<ITmdbAgeRatingService, TmdbAgeRatingService>` | Erledigt | `AgeRatingE2ETests` (App-Start + Scan über DI) |
| 12 | Konfiguration | `appsettings.json`: `Tmdb`-Abschnitt mit Defaults (`Enabled: false`, `ApiKey: null`) und `AutoUpdate:ProtectedFiles`-`JsonKeys` um die `Tmdb:*`-Keys erweitern | Erledigt | Kein direkter Test |
| 13 | Scan | `MediaSourceClassifier`: optionaler Ctor-Parameter `ITmdbAgeRatingService?`, `ExtractTmdbId`, `TryApplyOnlineAgeRatingAsync` | Erledigt | `LocalMediaSourceClassifierTests_AgeRating.ClassifyAllAsync_NoNfoRating_TmdbFallback` |
| 14 | Scan | `ProcessCollectionAsMovieAsync`: `AgeRatingClassifiedAt`-Marker + Online-Fallback bei fehlendem `AgeRatingRaw` (unter `!IsManuallyEdited`) | Erledigt | `LocalMediaSourceClassifierTests_AgeRating.ClassifyAllAsync_MovieNfoMpaa_PersistsAgeRating` + `…_ManuallyEditedMovie_KeepsExistingRating` |
| 15 | Scan | `CreateOrUpdateTVShow`: `AgeRatingClassifiedAt`-Marker + Online-Fallback analog | Erledigt | `LocalMediaSourceClassifierTests_AgeRating.ClassifyAllAsync_TvShowNfoCertification_PersistsAgeRating` |
| 16 | Backfill | `TryReadNfoAsync(MediaCollection, …)` aus `TryReadActorNfoAsync` extrahieren (Collection-Walk, Kandidatenliste) | Erledigt | `MediaSourceClassifierAgeRatingBackfillTests` + bestehende Actor-Backfill-Tests |
| 17 | Backfill | `BackfillMissingAgeRatingsAsync` implementieren (Movies + TVShows mit `AgeRatingClassifiedAt == null`, `IsManuallyEdited`-Skip, `movie.nfo`/`tvshow.nfo`-Fallbacks, Marker setzen, Fehler pro Eintrag + `DiscardPendingChanges`) | Erledigt | `MediaSourceClassifierAgeRatingBackfillTests` (5 Fälle) |
| 18 | Backfill | `ActorBackfillWorker.ExecuteAsync` ruft zusätzlich `BackfillMissingAgeRatingsAsync` (gleiche Gate-Lease, eigener try/catch) | Erledigt | Kein direkter Test (indirekt via `MediaSourceClassifierAgeRatingBackfillTests`) |
| 19 | API/DTO | `DtoMovie` + `DtoTVShow`: `AgeRating` (string?) und `AgeRatingMinAge` (int?) ergänzen | Erledigt | `AgeRatingE2ETests` (DTO über API → Badge) |
| 20 | API/DTO | `MediaMetadataUpdateRequest.AgeRating` (string?) ergänzen | Erledigt | `MediaMetadataEditorServiceTests.UpdateMovieAsync_SetsAgeRating` |
| 21 | Editieren | `MediaMetadataEditorService`: `UpdateMovieAsync`/`UpdateTVShowAsync` setzen Freigabe (normalisiert; leer ⇒ löschen); `ValidateAgeRatingValue` + `ValidateNoAgeRating` für andere Typen | Erledigt | `MediaMetadataEditorServiceTests` (7 neue Fälle) |
| 22 | UI | `MetadataEditModel.AgeRating` (string, `[StringLength(64)]`) ergänzen | Erledigt | `AgeRatingE2ETests.Admin_SetsAgeRating_ViaEditDropdown` |
| 23 | UI | `MovieCollectionDetails.razor`: `editSupportsAgeRating`, `InputSelect` mit kanonischen Stufen im Edit-Form, `BeginEdit`-Vorbelegung, `SaveEditAsync`-Request | Erledigt | `AgeRatingE2ETests.Admin_SetsAgeRating_ViaEditDropdown` |
| 24 | UI | `TVShowDetails.razor`: `editSupportsAgeRating`, `InputSelect`, `BeginEdit`-Vorbelegung, `SaveEditAsync`-Request | Erledigt | Kein direkter Test (E2E deckt Film-Pfad ab; gleiche Code-Struktur) |
| 25 | UI | `MovieCollectionDetails.razor`: Freigabe-Badge in der Film-Meta-Zeile (nur wenn nicht leer) | Erledigt | `AgeRatingE2ETests.MovieNfoRating_ShowsBadgeOnMovieDetails` |
| 26 | UI | `TVShowDetails.razor`: Freigabe-Badge in Serien-Meta-Zeile + vererbte Anzeige in Staffel-/Episoden-Meta-Zeilen (`show.AgeRating`) | Erledigt | `AgeRatingE2ETests.TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt` |
| 27 | UI | `.agerating-badge`-CSS-Klasse in `VideoWebPlayer/wwwroot/app.css` ergänzen | Erledigt | `AgeRatingE2ETests` (Locator `.agerating-badge`) |
| 28 | Tests | `AgeRatingNormalizerTests` (Theorien für FSK-/MPAA-/Länderpräfix-/unbekannte Werte) | Erledigt | 25 Fälle |
| 29 | Tests | `LoadFromXml`-Tests für `Movie` und `TVShow` (`<mpaa>`, `<certification>`-Fallback, fehlendes Element) | Erledigt | `MovieAgeRatingTests` (4 Fälle) + `TVShowAgeRatingTests` (3 Fälle) |
| 30 | Tests | `LocalMediaSourceClassifierTests_AgeRating`: NFO-Scan→Persistenz Film + Serie, `IsManuallyEdited`-Schutz, TMDB-Fallback (Fake-Service), NFO-Priorität vor Online | Erledigt | 5 Fälle |
| 31 | Tests | `MediaSourceClassifierAgeRatingBackfillTests`: Altbestand-Befüllung, Marker, manuelle Einträge, `movie.nfo`/`tvshow.nfo`-Fallbacks | Erledigt | 5 Fälle |
| 32 | Tests | `TmdbAgeRatingServiceTests` mit gefaketem `HttpMessageHandler`: DE-Bevorzugung, US-Fallback, Titelsuche, deaktiviert/kein Key ⇒ null, HTTP-Fehler/Timeout ⇒ null | Erledigt | 10 Fälle |
| 33 | Tests | `MediaMetadataEditorServiceTests`: Rating setzen/löschen, unbekannter Wert abgelehnt, Rating für `moviecollection`/`tvshowseason`/`tvshowepisode` abgelehnt | Erledigt | 7 Fälle |
| 34 | Tests | `VideoWebPlayerBackupDataTests_AgeRatings`: Alt-Backup ohne die 8 Spalten restauriert fehlerfrei (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Erledigt | 1 Fall |
| 35 | Tests | `FakeTmdbAgeRatingService`-Hilfsklasse unter `VideoWebPlayer.Tests/Helpers/` | Erledigt | Verwendet in `LocalMediaSourceClassifierTests_AgeRating` |
| 36 | E2E-Tests | Scan→Anzeige: Film-NFO mit `<mpaa>` → Badge in `MovieCollectionDetails` | Erledigt | `AgeRatingE2ETests.MovieNfoRating_ShowsBadgeOnMovieDetails` |
| 37 | E2E-Tests | Serien-Freigabe in `TVShowDetails` sichtbar + vererbte Anzeige in Episodenansicht | Erledigt | `AgeRatingE2ETests.TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt` |
| 38 | E2E-Tests | Admin setzt Freigabe über Edit-Dropdown und speichert → Badge sichtbar | Erledigt | `AgeRatingE2ETests.Admin_SetsAgeRating_ViaEditDropdown` |
| 39 | Dokumentation | `docs/help/medien-editiermodus.md` + `docs/help/medienquellen/*` (NFO-Felder `<mpaa>`/`<certification>`) ergänzen | Erledigt | Kein direkter Test |
| 40 | Dokumentation | `docs/SECRETS_MANAGEMENT.md`: Tabellenzeile `Tmdb:ApiKey` ergänzen | Erledigt | Kein direkter Test |
| 41 | Dokumentation | `README.md` + `docs/RELEASE_NOTES.md` aktualisieren | Erledigt | Kein direkter Test |
