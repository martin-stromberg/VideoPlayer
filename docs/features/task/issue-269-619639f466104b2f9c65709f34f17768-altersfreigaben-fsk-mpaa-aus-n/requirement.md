# Übersetzte Anforderung: Altersfreigaben (FSK & MPAA) aus NFO-Dateien bzw. TMDB/IMDb erfassen

## Fachliche Zusammenfassung

Das Datenmodell wird um eine persistierte Altersfreigabe für `Movie` und `TVShow` erweitert.
Beim Scan wertet der `MediaSourceClassifier` die ohnehin gelesenen NFO-Dateien (`<name>.nfo`,
`movie.nfo`, `tvshow.nfo`) zusätzlich auf die Elemente `<mpaa>` bzw. `<certification>` aus und
normalisiert die Rohwerte (z. B. „FSK 12“, „Rated PG-13“, „Germany:16“) auf bekannte
Freigabestufen (FSK 0/6/12/16/18, MPAA G/PG/PG-13/R/NC-17); der Rohwert bleibt zusätzlich
erhalten. Fehlt die NFO-Angabe, kann — sofern technisch vertretbar und konfiguriert — eine
Online-Quelle (TMDB bevorzugt; IMDb hat keine freie offizielle API und ist ggf. nur
dokumentarisch als Fallback zu behandeln) die Freigabe ergänzen, ohne den Scan zu behindern.
NFO-Angaben haben Vorrang vor der Online-Quelle; manuell gepflegte Einträge werden nicht
überschrieben. Die Freigabe wird in den Medien-Detailansichten angezeigt und so modelliert,
dass sie später filterbar ist (perspektivisch Jugendschutz-/Freigaberegeln pro Benutzer).

## Akzeptanzkriterien

1. **NFO-Erfassung:** `<mpaa>`/`<certification>` aus den NFO-Dateien werden beim Scan erfasst,
   normalisiert und persistiert — inklusive EF-Migration, Eintrag in
   `VideoWebPlayerBackupData.OptionalRestoreColumns` und Alt-Backup-Regressionstest.
2. **Anzeige:** Die Altersfreigabe erscheint in der UI an einer für den Anwender sinnvollen
   Stelle (Medien-Detailansichten; optional Badge/Filter in Listen).
3. **Online-Ergänzung:** Fehlt die NFO-Angabe und ist die gewählte Online-Quelle konfiguriert
   und erreichbar, wird die Freigabe von TMDB/IMDb ergänzt; ist die Quelle nicht erreichbar
   oder nicht konfiguriert, bleibt das Feld leer, statt den Scan zu behindern.
4. **Priorität:** Die NFO-Angabe hat Vorrang vor der Online-Quelle; manuell gepflegte Werte
   werden nicht überschrieben.

## Betroffene Klassen und Komponenten

- **Datenmodell:** `Movie` (`VideoWebPlayer/Data/Movie.cs`) und `TVShow`
  (`VideoWebPlayer/Data/TVShow.cs`) — neue Eigenschaft(en) für die Altersfreigabe
  (normalisierter Wert plus Rohwert). Die gemeinsame Basisklasse `MediaBaseEntry`
  (`VideoWebPlayer/Data/MediaBaseEntry.cs`) ist ein möglicher Ort, falls die Freigabe auch für
  `TVShowEpisode`/`MovieCollection` gelten soll (Abwägung — siehe Offene Fragen).
- **Logik/Services:**
  - `MediaSourceClassifier` (`VideoWebPlayer/Services/MediaSourceClassifier.cs`): Die
    NFO-Auswertung erfolgt über `Movie.LoadFromXml(XElement)` und
    `TVShow.LoadFromXml(XElement)` (ggf. `TVShowEpisode.LoadFromXml(XElement)`), aufgerufen aus
    `ProcessCollectionAsMovieAsync`, `CreateOrUpdateTVShow` und `ProcessEpisodesForTVShowAsync`.
  - Neue, isoliert testbare Normalisierungslogik für Freigabewerte (eigene kleine
    Klasse/Helper, z. B. `AgeRatingNormalizer`).
  - Ggf. neuer Service samt Interface für die Online-Quelle (z. B. TMDB) — es existiert heute
    keine TMDB-/IMDb-Anbindung; der vorhandene `HttpClient` im `MediaSourceClassifier` lädt
    lediglich Schauspieler-Bilder aus in den NFOs angegebenen URLs.
  - `MediaMetadataEditorService` (`VideoWebPlayer/Services/MediaMetadataEditorService.cs`) und
    `MediaMetadataUpdateRequest` (`VideoWebPlayer.Client/Models/DtoMovie.cs`), falls die
    Freigabe im Admin-Editiermodus pflegbar sein soll.
- **DTOs/API:** `DtoMovie`, `DtoTVShow` (ggf. `DtoTVShowEpisode`, `MediaEntryDto` für
  Badge/Filter) in `VideoWebPlayer.Client/Models/`; `ItemsController`
  (`VideoWebPlayer/Controllers/ItemsController.cs`) für Detail- und Metadaten-Endpunkte.
- **UI:** `Components/Pages/Movies/MovieCollectionDetails.razor` und
  `Components/Pages/TV/TVShowDetails.razor` — die Meta-Zeile neben Jahr/Genres ist die
  natürliche Anzeigestelle; `Components/Shared/Media/MetadataEditModel.cs` samt Edit-Form,
  falls editierbar; optional Badge in Listen (`MediaBox.razor`, `MediaBaseEntryList.razor`).
- **Persistenz/Backup:** neue EF-Migration unter `VideoWebPlayer/Migrations/`;
  `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` → `OptionalRestoreColumns`
  für die neuen Spalten (z. B. `Movies.<Rating>`, `TVShows.<Rating>`).
- **Konfiguration:** neue Optionsklasse unter `VideoWebPlayer/Configuration/` plus
  `appsettings.json`-Abschnitt (analog `MediaProbe`, `Playlists`, `EpisodeBackgroundImage`)
  und/oder `Setup`-Entität (`VideoWebPlayer/Data/Setup.cs`) für den API-Key — siehe Offene
  Fragen und `docs/SECRETS_MANAGEMENT.md`.
- **Tests:** `MediaSourceClassifier*Tests` (NFO-Parsing, Normalisierung, Priorität,
  `IsManuallyEdited`-Schutz), `VideoWebPlayerBackupDataTests_*` (Alt-Backup ohne die neuen
  Spalten), Tests des Online-Quellen-Service mit gemockter HTTP-Schicht, ggf.
  `ItemsController*`/`MetadataEditor*`-Tests bei Editierbarkeit.

## Implementierungsansatz

- Erweiterungspunkt ist die bestehende NFO-Auswertung: `LoadFromXml(XElement)` liest heute
  `<title>`, `<genre>`, `<plot>` u. a.; dort werden `<mpaa>`/`<certification>` ergänzt — bei
  Filmen pro Videodatei (`<name>.nfo`, Fallback `movie.nfo`), bei Serien über `tvshow.nfo`.
- Der bestehende `IsManuallyEdited`-Schutz greift eintragsweit: Der Scan ruft `LoadFromXml`
  nur für nicht manuell editierte Einträge auf und erfüllt damit „kein Überschreiben manuell
  gepflegter Werte“ auf Eintragsebene.
- Die Normalisierung mappt Rohwerte (z. B. „FSK 12“, „Rated PG-13“, „Germany:16“) auf ein
  normalisiertes Modell und speichert den Rohwert separat (Anforderung: „Rohwert zusätzlich
  erhalten“). Für spätere Filterbarkeit/Zugriffsregeln ist eine vergleichbare Darstellung
  (z. B. System + Stufe) der reinen Textablage vorzuziehen.
- Online-Quelle: Vor der Umsetzung ist die Machbarkeit zu prüfen (Teil der Anforderung) —
  TMDB (`release_dates` für Filme, `content_ratings` für Serien) benötigt einen API-Key;
  IMDb hat keine freie offizielle API → Ergebnis dokumentieren, ggf. Verzicht. Der Abruf ist
  strikt optional und fehlertolerant (Timeout/Fehler/nicht konfiguriert ⇒ Feld bleibt leer,
  kein Exception-Pfad im Scan). Die Online-Abfrage läuft nur, wenn nach der NFO-Auswertung
  kein Wert vorliegt (NFO-Priorität).
- Bestandsdaten: Bereits klassifizierte Einträge erhalten die Freigabe über den NFO-Pfad erst
  beim nächsten Scan mit `Changed`-Flag; analog zur Schauspieler-Nacherfassung
  (`BackfillMissingActorsAsync`, Markierung über `ActorsClassifiedAt`) ist eine einmalige
  Nacherfassung aus vorhandenen NFOs denkbar (Annahme — im Plan zu entscheiden).

## Konfiguration

- Falls eine Online-Quelle umgesetzt wird: neuer Options-Abschnitt (z. B. `Tmdb:Enabled`,
  `Tmdb:ApiKey`, `Tmdb:BaseUrl`, Timeout/Retry) als `IOptions` unter
  `VideoWebPlayer/Configuration/` mit Registrierung in `ServiceCollectionExtensions` und
  Abschnitt in `appsettings.json`; der Schlüssel wird nicht im Repository abgelegt
  (siehe `docs/SECRETS_MANAGEMENT.md`). Alternativ ist die `Setup`-Entität denkbar, wenn der
  Schlüssel zur Laufzeit über die Admin-Einrichtung editierbar sein soll — Abwägung in den
  Offenen Fragen.
- Standardverhalten ohne Konfiguration: Online-Ergänzung deaktiviert; der NFO-Pfad ist voll
  funktionsfähig und benötigt keine Konfiguration.

## Offene Fragen

1. **Granularität:** Nur `Movie`/`TVShow`, oder auch `TVShowEpisode` (Episoden-NFOs können
   eigene `<mpaa>`-Werte tragen) bzw. `MovieCollection`? Erben Episoden/Seasons die
   Serienfreigabe nur in der Anzeige?
2. **Feldmodellierung:** Ein normalisierter String plus Rohwert (zwei Spalten) oder eine
   stärker strukturierte Form (System + Stufe, z. B. Enum/`int`-Mindestalter), damit spätere
   Zugriffsregeln Bereichsvergleiche („≤ FSK 12“) abbilden können?
3. **Editierbarkeit:** Soll die Freigabe im Admin-Editiermodus
   (`MediaMetadataEditorService`, `MetadataEditModel`, `MediaMetadataUpdateRequest`) manuell
   pflegbar sein? Die Anforderung „kein Überschreiben manuell gepflegter Werte“ wäre dann über
   die eintragsweite `IsManuallyEdited`-Sperre abbildbar.
4. **Fremdsystem:** TMDB mit API-Key (Machbarkeit, Rate-Limits, Key-Ablage/Secrets) vs.
   Verzicht auf die Online-Quelle; IMDb dokumentarisch als nicht angebunden führen?
5. **Altbestand:** Reicht „beim nächsten Scan mit `Changed`-Flag“, oder soll analog
   `BackfillMissingActorsAsync` einmalig aus vorhandenen NFOs nachgezogen werden (ggf. eigene
   Markierungsspalte wie `ActorsClassifiedAt`)?
6. **Länderauswahl:** Welche Freigabe gewinnt bei mehreren Länderangaben (NFO-Rohwert
   „Germany:16“ vs. TMDB-Länderliste — bevorzugt DE/FSK, sonst US/MPAA, oder Rohwert
   unverändert)?
7. **Listen-Darstellung:** Soll die Freigabe bereits jetzt als Filter/Badge in Listen
   erscheinen (Anforderung: „ggf.“) oder vorerst nur in den Detailansichten?
