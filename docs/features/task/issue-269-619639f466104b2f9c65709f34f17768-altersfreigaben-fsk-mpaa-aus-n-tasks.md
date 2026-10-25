# Tasks: Altersfreigaben (FSK & MPAA) aus NFO-Dateien bzw. TMDB erfassen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `AgeRatingNormalizer` (static) + `AgeRatingResult`-Record unter `VideoWebPlayer/Utils/` anlegen (FSK 0/6/12/16/18, MPAA G/PG/PG-13/R/NC-17, Länderpräfixe „Germany:“/„DE:“; unbekannt ⇒ Raw-Fallback + `MinAge = null`) | Offen | — |
| 2 | Datenmodell | `Movie`: `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`, `AgeRatingClassifiedAt` anlegen | Offen | — |
| 3 | Datenmodell | `Movie.LoadFromXml`: `<mpaa>`/`<certification>` auswerten und via `AgeRatingNormalizer` setzen | Offen | — |
| 4 | Datenmodell | `TVShow`: `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`, `AgeRatingClassifiedAt` anlegen | Offen | — |
| 5 | Datenmodell | `TVShow.LoadFromXml`: `<mpaa>`/`<certification>` auswerten und via `AgeRatingNormalizer` setzen | Offen | — |
| 6 | Datenmodell | EF-Migration `AddAgeRatings` erzeugen (8 nullable Spalten auf `Movies`/`TVShows`) | Offen | — |
| 7 | Backup | `VideoWebPlayerBackupData.OptionalRestoreColumns` um die 8 neuen `Movies.*`/`TVShows.*`-Spalten erweitern | Offen | — |
| 8 | Online-Quelle | `TmdbOptions` unter `VideoWebPlayer/Configuration/` anlegen (`Enabled`, `ApiKey`, `BaseUrl`, `PreferredCountry`, `TimeoutSeconds`, Effective-Getter) | Offen | — |
| 9 | Online-Quelle | `ITmdbAgeRatingService`-Interface + `TmdbAgeRating`-Record anlegen | Offen | — |
| 10 | Online-Quelle | `TmdbAgeRatingService` implementieren (named HttpClient „Tmdb“, `release_dates`/`content_ratings`, Titelsuche ohne ID, Länderwahl DE→US→erste, fehlertolerant `null` statt Exception) | Offen | — |
| 11 | Konfiguration | DI-Registrierung in `ServiceCollectionExtensions`: `Configure<TmdbOptions>`, `AddHttpClient("Tmdb")`, `AddSingleton<ITmdbAgeRatingService, TmdbAgeRatingService>` | Offen | — |
| 12 | Konfiguration | `appsettings.json`: `Tmdb`-Abschnitt mit Defaults (`Enabled: false`, `ApiKey: null`) und `AutoUpdate:ProtectedFiles`-`JsonKeys` um die `Tmdb:*`-Keys erweitern | Offen | — |
| 13 | Scan | `MediaSourceClassifier`: optionaler Ctor-Parameter `ITmdbAgeRatingService?`, `ExtractTmdbId`, `TryApplyOnlineAgeRatingAsync` | Offen | — |
| 14 | Scan | `ProcessCollectionAsMovieAsync`: `AgeRatingClassifiedAt`-Marker + Online-Fallback bei fehlendem `AgeRatingRaw` (unter `!IsManuallyEdited`) | Offen | — |
| 15 | Scan | `CreateOrUpdateTVShow`: `AgeRatingClassifiedAt`-Marker + Online-Fallback analog | Offen | — |
| 16 | Backfill | `TryReadNfoAsync(MediaCollection, …)` aus `TryReadActorNfoAsync` extrahieren (Collection-Walk, Kandidatenliste) | Offen | — |
| 17 | Backfill | `BackfillMissingAgeRatingsAsync` implementieren (Movies + TVShows mit `AgeRatingClassifiedAt == null`, `IsManuallyEdited`-Skip, `movie.nfo`/`tvshow.nfo`-Fallbacks, Marker setzen, Fehler pro Eintrag + `DiscardPendingChanges`) | Offen | — |
| 18 | Backfill | `ActorBackfillWorker.ExecuteAsync` ruft zusätzlich `BackfillMissingAgeRatingsAsync` (gleiche Gate-Lease, eigener try/catch) | Offen | — |
| 19 | API/DTO | `DtoMovie` + `DtoTVShow`: `AgeRating` (string?) und `AgeRatingMinAge` (int?) ergänzen | Offen | — |
| 20 | API/DTO | `MediaMetadataUpdateRequest.AgeRating` (string?) ergänzen | Offen | — |
| 21 | Editieren | `MediaMetadataEditorService`: `UpdateMovieAsync`/`UpdateTVShowAsync` setzen Freigabe (normalisiert; leer ⇒ löschen); `ValidateAgeRatingValue` + `ValidateNoAgeRating` für andere Typen | Offen | — |
| 22 | UI | `MetadataEditModel.AgeRating` (string, `[StringLength(64)]`) ergänzen | Offen | — |
| 23 | UI | `MovieCollectionDetails.razor`: `editSupportsAgeRating`, `InputSelect` mit kanonischen Stufen im Edit-Form, `BeginEdit`-Vorbelegung, `SaveEditAsync`-Request | Offen | — |
| 24 | UI | `TVShowDetails.razor`: `editSupportsAgeRating`, `InputSelect`, `BeginEdit`-Vorbelegung, `SaveEditAsync`-Request | Offen | — |
| 25 | UI | `MovieCollectionDetails.razor`: Freigabe-Badge in der Film-Meta-Zeile (nur wenn nicht leer) | Offen | — |
| 26 | UI | `TVShowDetails.razor`: Freigabe-Badge in Serien-Meta-Zeile + vererbte Anzeige in Staffel-/Episoden-Meta-Zeilen (`show.AgeRating`) | Offen | — |
| 27 | UI | `.agerating-badge`-CSS-Klasse in `VideoWebPlayer/wwwroot/app.css` ergänzen | Offen | — |
| 28 | Tests | `AgeRatingNormalizerTests` (Theorien für FSK-/MPAA-/Länderpräfix-/unbekannte Werte) | Offen | — |
| 29 | Tests | `LoadFromXml`-Tests für `Movie` und `TVShow` (`<mpaa>`, `<certification>`-Fallback, fehlendes Element) | Offen | — |
| 30 | Tests | `LocalMediaSourceClassifierTests_AgeRating`: NFO-Scan→Persistenz Film + Serie, `IsManuallyEdited`-Schutz, TMDB-Fallback (Fake-Service), NFO-Priorität vor Online | Offen | — |
| 31 | Tests | `MediaSourceClassifierAgeRatingBackfillTests`: Altbestand-Befüllung, Marker, manuelle Einträge, `movie.nfo`/`tvshow.nfo`-Fallbacks | Offen | — |
| 32 | Tests | `TmdbAgeRatingServiceTests` mit gefaketem `HttpMessageHandler`: DE-Bevorzugung, US-Fallback, Titelsuche, deaktiviert/kein Key ⇒ null, HTTP-Fehler/Timeout ⇒ null | Offen | — |
| 33 | Tests | `MediaMetadataEditorServiceTests`: Rating setzen/löschen, unbekannter Wert abgelehnt, Rating für `moviecollection`/`tvshowseason`/`tvshowepisode` abgelehnt | Offen | — |
| 34 | Tests | `VideoWebPlayerBackupDataTests_AgeRatings`: Alt-Backup ohne die 8 Spalten restauriert fehlerfrei (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Offen | — |
| 35 | Tests | `FakeTmdbAgeRatingService`-Hilfsklasse unter `VideoWebPlayer.Tests/Helpers/` | Offen | — |
| 36 | E2E-Tests | Scan→Anzeige: Film-NFO mit `<mpaa>` → Badge in `MovieCollectionDetails` (neuer Fall in `LocalMediaSourcePipelineE2ETests` oder neue `AgeRatingE2ETests`) | Offen | — |
| 37 | E2E-Tests | Serien-Freigabe in `TVShowDetails` sichtbar + vererbte Anzeige in Episodenansicht | Offen | — |
| 38 | E2E-Tests | Admin setzt Freigabe über Edit-Dropdown und speichert → Badge sichtbar (`MovieCollectionEditE2ETests`-Erweiterung bzw. `AgeRatingE2ETests`) | Offen | — |
| 39 | Dokumentation | `docs/help/medien-editiermodus.md` + `docs/help/medienquellen/*` (NFO-Felder `<mpaa>`/`<certification>`) ergänzen | Offen | — |
| 40 | Dokumentation | `docs/SECRETS_MANAGEMENT.md`: Tabellenzeile `Tmdb:ApiKey` ergänzen | Offen | — |
| 41 | Dokumentation | `README.md` + `docs/RELEASE_NOTES.md` aktualisieren | Offen | — |
