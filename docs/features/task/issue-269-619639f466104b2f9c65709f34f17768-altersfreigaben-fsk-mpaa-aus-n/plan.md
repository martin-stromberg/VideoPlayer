# Umsetzungsplan: Altersfreigaben (FSK & MPAA) aus NFO-Dateien bzw. TMDB erfassen

## Übersicht

`Movie` und `TVShow` erhalten je eine persistierte Altersfreigabe (kanonischer Anzeigewert,
Rohwert, vergleichbares Mindestalter). Beim Scan werten `Movie.LoadFromXml` bzw.
`TVShow.LoadFromXml` die NFO-Elemente `<mpaa>`/`<certification>` aus und normalisieren sie
über einen neuen `AgeRatingNormalizer`; fehlt die NFO-Angabe, ergänzt ein optionaler,
fehlertoleranter TMDB-Service die Freigabe. Der eintragsweite `IsManuallyEdited`-Schutz gilt
unverändert. Die Freigabe wird in den Detailansichten als Badge angezeigt und ist im
Admin-Editiermodus pflegbar; eine einmalige Nacherfassung (Backfill) versorgt den Altbestand.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Feldmodellierung | Drei nullable Spalten je Entity: `AgeRating` (string?, kanonischer Anzeigewert wie „FSK 12“/„PG-13“, Fallback = getrimmter Rohwert), `AgeRatingRaw` (string?, unveränderter Quellwert), `AgeRatingMinAge` (int?, vergleichbares Mindestalter) | Anforderung „normalisiert + Rohwert“ plus „später filterbar … Bereichsvergleiche“; `AgeRatingMinAge` macht „≤ FSK 12“ über FSK/MPAA hinweg vergleichbar, ohne Enum-Mapping an der DB-/JSON-Grenze |
| Felder an `Movie`/`TVShow` statt `MediaBaseEntry` | Deklaration auf den konkreten Entities | Basisklasse würde die Spalten auch auf `TVShowSeason`/`TVShowEpisode`/`MovieCollection` ziehen — die Anforderung nennt nur `Movie`/`TVShow`; scope-minimal, späteres Hochziehen bleibt möglich |
| Normalisierung | Reine Hilfsklasse `AgeRatingNormalizer` (static) + Ergebnis-Record `AgeRatingResult` (`DisplayValue`, `MinAge`) | Isoliert testbar, ohne Abhängigkeiten aus `LoadFromXml`, Scan, Backfill und Editor-Service aufrufbar; kein Zustand, daher static statt DI-Service |
| Online-Quelle | `ITmdbAgeRatingService`/`TmdbAgeRatingService` hinter `TmdbOptions` (`Enabled`, `ApiKey`, `BaseUrl`, `PreferredCountry`, `TimeoutSeconds`); named `HttpClient` „Tmdb“ via `IHttpClientFactory`; Service immer registriert, liefert `null` wenn deaktiviert/nicht konfiguriert | TMDB hat freie API (Anforderung: „TMDB bevorzugt“); Optionsmuster und named-Client-Muster (`FfmpegToolDownloadService.HttpClientName`) sind etabliert; „nicht konfiguriert ⇒ Feld bleibt leer“ wird Service-intern abgebildet, der Scanpfad bleibt unverändert |
| TMDB-Lookup | NFO-`uniqueid[@type='tmdb']`/`<tmdbid>` direkt, sonst Titelsuche mit Jahr; Länderwahl `PreferredCountry` (Default DE) → „US“ → erste Angabe | Eigene Fuzzy-Match-Logik entfällt, wo die NFO die ID schon liefert (Kodi-Exporte tun das üblicherweise); Länderpriorität nach Offenem Punkt 6 |
| API-Key-Ablage | `appsettings`-Abschnitt `Tmdb` (Optionsklasse), Schlüssel via `appsettings.Local.json`/Umgebungsvariable/User Secrets; kein `Setup`-Feld, kein Admin-UI | Betreiber-Konfiguration, keine Laufzeit-Editierbarkeit gefordert; `Setup`-Entity ist für in-App-editierbare Einstellungen — ein Secret gehört nicht in die Datenbank (`docs/SECRETS_MANAGEMENT.md`) |
| Altbestand | `BackfillMissingAgeRatingsAsync` mit Markierungsspalte `AgeRatingClassifiedAt`, aufgerufen aus dem bestehenden `ActorBackfillWorker` | Direktes Vorbild `BackfillMissingActorsAsync`/`ActorsClassifiedAt`; ohne Marker würde der Startlauf die NFOs aller unrated Einträge bei jedem Start erneut lesen |
| Editierbarkeit | `AgeRating` im Admin-Editiermodus (`MediaMetadataUpdateRequest`, `MetadataEditModel`, `MediaMetadataEditorService`) nur für `movie`/`tvshow`; UI = `InputSelect` mit kanonischen Stufen + „Keine“ | AK 4 („manuell gepflegte Werte nicht überschrieben“) ist über `IsManuallyEdited` abbildbar und wird erst durch Editierbarkeit praktisch greifbar; Auswahl benannter Werte statt Freitext |
| Anzeige-Vererbung | Episoden-/Staffel-Meta-Zeilen zeigen `show.AgeRating`; keine eigenen Felder | Episoden-NFOs tragen selten eigene Werte; Anzeige-Vererbung deckt den Anwendungsfall ohne Schema-Erweiterung (Offene Frage 1) |
| IMDb | Keine Anbindung, nur dokumentarisch vermerkt | IMDb hat keine freie offizielle API (Anforderung selbst) |

## Programmabläufe

### NFO-Erfassung beim Film-Scan

1. `MediaSourceClassifier.ProcessCollectionAsMovieAsync` liest wie bisher `<name>.nfo`
   (Root-Element `<movie>` erforderlich).
2. `Movie.LoadFromXml(xml)` liest neu `<mpaa>` (primär) bzw. `<certification>` (Fallback),
   speichert den Elementwert in `AgeRatingRaw` und setzt `AgeRating`/`AgeRatingMinAge` über
   `AgeRatingNormalizer.Normalize`. Fehlt das Element, sind alle drei Felder `null`.
3. Nach `LoadFromXml` (Neuanlage und Update — beide laufen nur bei `!IsManuallyEdited`)
   setzt der Classifier `AgeRatingClassifiedAt = DateTime.UtcNow`.
4. Ist danach `AgeRatingRaw == null` (keine NFO-Angabe), ruft der Classifier
   `TryApplyOnlineAgeRatingAsync` auf: `ExtractTmdbId(xml)` → `tmdbId` bzw. Titel + `Year` →
   `ITmdbAgeRatingService.GetMovieAgeRatingAsync`; Ergebnis wird normalisiert und
   `AgeRatingRaw` wird als Quellenwert gesetzt (`"{Land}:{Certification}"`, z. B. „DE:16“).
5. Fehler/Timeout/nicht konfiguriert → `null`-Ergebnis, `LogDebug`/`LogWarning`, Feld bleibt
   leer; kein Exception-Pfad.

Beteiligte Klassen: `MediaSourceClassifier`, `Movie`, `AgeRatingNormalizer`,
`ITmdbAgeRatingService`, `TmdbOptions`

### NFO-Erfassung beim Serien-Scan

1. `ProcessCollectionAsTVShowAsync` liest `tvshow.nfo` (Collection-Ebene) wie bisher.
2. `CreateOrUpdateTVShow` ruft `TVShow.LoadFromXml(xml)` (nur bei `!IsManuallyEdited`);
   dieselbe `<mpaa>`/`<certification>`-Auswertung und Normalisierung wie beim Film.
3. `AgeRatingClassifiedAt` wird analog gesetzt; Online-Fallback via
   `ITmdbAgeRatingService.GetTVShowAgeRatingAsync` (`content_ratings`-Endpunkt).
4. `ProcessEpisodesForTVShowAsync` bleibt unverändert — Episoden erhalten keine eigenen
   Freigabefelder (Anzeige-Vererbung über die Serie).

Beteiligte Klassen: `MediaSourceClassifier`, `TVShow`, `AgeRatingNormalizer`,
`ITmdbAgeRatingService`

### Online-Ergänzung (TMDB)

1. `TmdbAgeRatingService.GetMovieAgeRatingAsync(title, year, tmdbId, ct)`: Liefert `null`,
   wenn `!Options.Enabled` oder `ApiKey` leer.
2. Mit `tmdbId` → `GET {BaseUrl}/movie/{id}/release_dates?api_key=…`; ohne →
   `GET {BaseUrl}/search/movie?query={title}&year={year}` → erstes Ergebnis →
   `release_dates`.
3. Aus den `results` (`iso_3166_1` + `certification`) wird die Freigabe von
   `PreferredCountry` gewählt, danach „US“, danach die erste Angabe.
4. Serien analog über `/tv/{id}/content_ratings` (`results[].rating`) bzw. `/search/tv`.
5. Rückgabe `TmdbAgeRating(Country, Certification)`; jeglicher Fehler (HTTP-Status,
   Timeout, Parsefehler, `HttpRequestException`) → `null` + Logging. Kein Retry.

Beteiligte Klassen: `TmdbAgeRatingService`, `TmdbOptions`, `IHttpClientFactory` (named
Client „Tmdb“), `AgeRatingNormalizer`

### Backfill des Altbestands

1. `MediaSourceClassifier.BackfillMissingAgeRatingsAsync(ct)`: `Movies` und `TVShows` mit
   `AgeRatingClassifiedAt == null`; Einträge mit `IsManuallyEdited` werden übersprungen
   (Muster `BackfillMissingActorsAsync`).
2. Film: erstes Nicht-Trailer-`MediaItem` über `MovieMediaItems` (`!IsTrailer`), NFO über
   `TryReadNfoAsync` mit Kandidaten `<name>.nfo`, `movie.nfo` (inkl. Parent-Collection-
   Aufstieg — Extraktion des Collection-Walks aus `TryReadActorNfoAsync`).
3. Serie: `tvshow.nfo` über `TryReadNfoAsync` ab der `CollectionId`-Collection.
4. Pro Eintrag: XML parsen → `LoadFromXml`-Auswertung (Normalisierung) → bei fehlendem
   Wert Online-Fallback → `AgeRatingClassifiedAt` setzen → `SaveChangesAsync`; Fehler pro
   Eintrag gefangen + `DiscardPendingChanges`.
5. `ActorBackfillWorker.ExecuteAsync` ruft nach `BackfillMissingActorsAsync` zusätzlich
   `BackfillMissingAgeRatingsAsync` (gleiche `IBackgroundProcessingGate`-Lease, eigener
   try/catch). Der Worker-Name bleibt aus Kompatibilitätsgründen unverändert.

Beteiligte Klassen: `MediaSourceClassifier`, `ActorBackfillWorker`, `IMediaSourceReader`,
`AgeRatingNormalizer`, `ITmdbAgeRatingService`

### Metadaten-Editieren (Admin)

1. Edit-Form (`RenderEditForm` in beiden Detailseiten) zeigt bei `movie`/`tvshow` ein
   `InputSelect` für `editModel.AgeRating` (kanonische Stufen + leer).
2. `SaveEditAsync` setzt `MediaMetadataUpdateRequest.AgeRating` (nur bei Typen mit
   Freigabe; `null` = löschen/nicht setzen).
3. `ItemsController.UpdateMetadata` (unverändert, Admin-Claim serverseitig) →
   `MediaMetadataEditorService.UpdateAsync` → `UpdateMovieAsync`/`UpdateTVShowAsync`:
   `AgeRating`/`AgeRatingMinAge` aus `AgeRatingNormalizer.Normalize(request.AgeRating)`,
   `AgeRatingRaw` = Eingabe; leer ⇒ alle drei `null`. Danach greift `IsManuallyEdited`
   eintragsweit: Scan/Backfill überschreiben den Wert nicht mehr.
4. Für `moviecollection`/`tvshowseason`/`tvshowepisode` wird ein gesetzter `AgeRating`
   mit `ArgumentException` abgelehnt (`ValidateNoAgeRating`, Muster `ValidateNoPlotOrGenres`).

Beteiligte Klassen: `MetadataEditModel`, `MediaMetadataUpdateRequest`,
`MediaMetadataEditorService`, `ItemsController`, `MovieCollectionDetails.razor`,
`TVShowDetails.razor`

### Detailanzeige

1. `MovieCollectionDetails.razor`: Film-Meta-Zeile (Zeilen 48–51) erhält
   `<span class="collection-agerating agerating-badge">@selectedMovie.AgeRating</span>`,
   nur wenn nicht leer. Sammlungs-Meta-Zeile bleibt ohne Freigabe (kein Feld auf
   `MovieCollection`).
2. `TVShowDetails.razor`: Serien-Meta-Zeile (Zeilen 114–117) zeigt `show.AgeRating`;
   Staffel- (105–108) und Episoden-Meta-Zeile (96–99) zeigen ebenfalls `show.AgeRating`
   (Anzeige-Vererbung).
3. CSS: neue `.agerating-badge`-Klasse in `VideoWebPlayer/wwwroot/app.css` (Anker:
   `.collection-meta`/`.tvshow-meta`, Zeile ~1623).

Beteiligte Klassen/Komponenten: `DtoMovie`, `DtoTVShow`, `MovieCollectionDetails.razor`,
`TVShowDetails.razor`, `app.css`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AgeRatingNormalizer` | Klasse (static, `VideoWebPlayer/Utils/`) | Normalisiert Rohwerte („FSK 12“, „Rated PG-13“, „Germany:16“) auf kanonische Stufen + `MinAge`; Mapping FSK 0/6/12/16/18 → Alter direkt, MPAA G→0, PG→8, PG-13→13, R→17, NC-17→18; unbekannt ⇒ `DisplayValue` = getrimmter Rohwert, `MinAge` = null |
| `AgeRatingResult` | Record (Value Object) | `DisplayValue` (string?), `MinAge` (int?) — Ergebnis von `AgeRatingNormalizer.Normalize` |
| `TmdbOptions` | Optionsklasse (`VideoWebPlayer/Configuration/`) | `Enabled` (false), `ApiKey` (null), `BaseUrl` („https://api.themoviedb.org/3“), `PreferredCountry` („DE“), `TimeoutSeconds` (10) + `Effective*`-Getter |
| `ITmdbAgeRatingService` | Interface (`VideoWebPlayer/Services/`) | `GetMovieAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken)`, `GetTVShowAgeRatingAsync(...)` → `Task<TmdbAgeRating?>` |
| `TmdbAgeRatingService` | Klasse (`VideoWebPlayer/Services/`) | Implementierung über named `HttpClient` („Tmdb“, `HttpClientName`-Konstante); fehlertolerant (liefert `null` statt Exception) |
| `TmdbAgeRating` | Record | `Country` (string), `Certification` (string) — Rohwert einer Länderfreigabe |

## Änderungen an bestehenden Klassen

### `Movie` (Datenmodell)

- **Neue Eigenschaften:** `AgeRating` (string?), `AgeRatingRaw` (string?),
  `AgeRatingMinAge` (int?), `AgeRatingClassifiedAt` (DateTime?)
- **Geänderte Methoden:** `LoadFromXml` — liest zusätzlich `<mpaa>`/`<certification>`,
  setzt `AgeRatingRaw` und `AgeRating`/`AgeRatingMinAge` via `AgeRatingNormalizer`

### `TVShow` (Datenmodell)

- **Neue Eigenschaften:** wie `Movie` (`AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge`,
  `AgeRatingClassifiedAt`)
- **Geänderte Methoden:** `LoadFromXml` — wie `Movie`

### `MediaSourceClassifier` (Service)

- **Neue Abhängigkeit:** optionaler Ctor-Parameter `ITmdbAgeRatingService? ageRatingService = null`
  (Muster: bestehende optionale Parameter `_notificationService`/`_writeCoordinator`;
  bestehende Test-Konstruktionen bleiben kompilierfähig)
- **Neue Methoden:**
  - `BackfillMissingAgeRatingsAsync(CancellationToken)` (public) — Nacherfassung Altbestand
  - `TryApplyOnlineAgeRatingAsync(Movie|TVShow, XElement, CancellationToken)` (private) —
    Online-Fallback bei fehlendem `AgeRatingRaw`
  - `ExtractTmdbId(XElement)` (private static) — `uniqueid[@type='tmdb']`/`<tmdbid>`
  - `TryReadNfoAsync(MediaCollection, IEnumerable<string>, CancellationToken)` (private) —
    aus `TryReadActorNfoAsync` extrahierter Collection-Walk; `TryReadActorNfoAsync`
    delegiert darauf
- **Geänderte Methoden:** `ProcessCollectionAsMovieAsync`, `CreateOrUpdateTVShow` —
  Marker-Setzung + Online-Fallback unter dem bestehenden `!IsManuallyEdited`-Schutz

### `ActorBackfillWorker` (BackgroundService)

- **Geänderte Methode:** `ExecuteAsync` — ruft zusätzlich
  `BackfillMissingAgeRatingsAsync` (gleiche Gate-Lease, eigener Fehlerblock)

### `MediaMetadataEditorService` (Service)

- **Geänderte Methoden:** `UpdateMovieAsync`, `UpdateTVShowAsync` — setzen
  `AgeRating`/`AgeRatingMinAge`/`AgeRatingRaw` aus `request.AgeRating` (Normalisierung;
  leer ⇒ Löschen)
- **Geänderte Methoden:** `UpdateMovieCollectionAsync`, `UpdateTVShowSeasonAsync`,
  `UpdateTVShowEpisodeAsync` — `ValidateNoAgeRating(request)`: gesetzter Wert ⇒
  `ArgumentException`
- **Neue Validierung:** `ValidateAgeRatingValue` — `AgeRating` muss leer oder durch den
  Normalizer erkennbar sein (`MinAge != null`), sonst `ArgumentException`; Längenlimit 64

### `DtoMovie` / `DtoTVShow` (DTOs, `VideoWebPlayer.Client/Models/DtoMovie.cs`)

- **Neue Eigenschaften:** `AgeRating` (string?), `AgeRatingMinAge` (int?) — gleichnamig ⇒
  automatische Befüllung durch `ApiBaseController.Create<T>`; kein `AgeRatingRaw` im DTO
  (interner Rohwert, kein Anzeigebedarf)

### `MediaMetadataUpdateRequest` (DTO, gleiche Datei)

- **Neue Eigenschaft:** `AgeRating` (string?)

### `MetadataEditModel` (UI-Formmodell)

- **Neue Eigenschaft:** `AgeRating` (string, `[StringLength(64)]`; kein `[Required]`)

### `MovieCollectionDetails.razor` / `TVShowDetails.razor` (UI)

- **Neues Flag:** `editSupportsAgeRating` (`DtoMovie` bzw. `DtoTVShow`)
- **Geändert:** `RenderEditForm` (Freigabe-`InputSelect`), `BeginEdit` (Vorbelegung),
  `SaveEditAsync` (`AgeRating` im Request), Meta-Zeilen (Badge; bei TV zusätzlich
  Staffel-/Episoden-Zeile mit `show.AgeRating`)

### `VideoWebPlayerBackupData` (Backup)

- `OptionalRestoreColumns` += `Movies.AgeRating`, `Movies.AgeRatingRaw`,
  `Movies.AgeRatingMinAge`, `Movies.AgeRatingClassifiedAt`, `TVShows.*` analog (8 Einträge)

### `ServiceCollectionExtensions` / `appsettings.json` / `wwwroot/app.css`

- `services.Configure<TmdbOptions>(configuration.GetSection("Tmdb"))`;
  `services.AddHttpClient(TmdbAgeRatingService.HttpClientName)`;
  `services.AddSingleton<ITmdbAgeRatingService, TmdbAgeRatingService>()`
- `appsettings.json`: Abschnitt `Tmdb` (Defaults, `ApiKey: null`);
  `AutoUpdate:ProtectedFiles`-`JsonKeys` += `Tmdb:Enabled`, `Tmdb:ApiKey`, `Tmdb:BaseUrl`,
  `Tmdb:PreferredCountry`, `Tmdb:TimeoutSeconds`
- `app.css`: `.agerating-badge`

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddAgeRatings` | `Movies.AgeRating` (string, null), `Movies.AgeRatingRaw` (string, null), `Movies.AgeRatingMinAge` (int, null), `Movies.AgeRatingClassifiedAt` (datetime, null); `TVShows.*` analog | Neue Altersfreigabe-Felder inkl. Backfill-Markierung; alle nullable → keine Defaults/`OptionalRestoreBoolDefaults` nötig |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `MediaMetadataUpdateRequest.AgeRating` bei `movie`/`tvshow` | leer = löschen; sonst muss `AgeRatingNormalizer` einen bekannten Wert erkennen (`MinAge != null`); Länge ≤ 64 | `ArgumentException` „Unbekannte Altersfreigabe“ |
| `MediaMetadataUpdateRequest.AgeRating` bei `moviecollection`/`tvshowseason`/`tvshowepisode` | muss leer sein | `ArgumentException` (Muster `ValidateNoPlotOrGenres`) |
| `TmdbOptions.TimeoutSeconds` | ≤ 0 ⇒ Default 10 (Effective-Getter) | — (normalisiert) |
| `TmdbOptions` ohne `Enabled`/`ApiKey` | Service liefert `null` | — (kein Fehler, Feld bleibt leer) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Tmdb:Enabled` | bool | `false` | Online-Ergänzung aktiviert |
| `Tmdb:ApiKey` | string? | `null` | TMDB API-Key — **nicht im Repo**, Ablage via `appsettings.Local.json`/Umgebungsvariable/User Secrets (`docs/SECRETS_MANAGEMENT.md`); in `AutoUpdate:ProtectedFiles`-`JsonKeys` eintragen |
| `Tmdb:BaseUrl` | string | `https://api.themoviedb.org/3` | API-Basis-URL (überschreibbar für Tests/Mirror) |
| `Tmdb:PreferredCountry` | string | `DE` | Bevorzugte Länderfreigabe (Fallback „US“, dann erste) |
| `Tmdb:TimeoutSeconds` | int | `10` | Timeout je TMDB-Aufruf |

## Seiteneffekte und Risiken

- **DTO-Grenze:** `ApiBaseController.Create<T>` propagiert die neuen Properties automatisch
  — kein Controller-Code, aber das JSON der Detail-Endpunkte ändert sich (additive Felder,
  abwärtskompatibel). Es gibt keinen DTO-Contract-Test, der die Feldliste pinnt.
- **`MetadataEditorUiStateTests`:** Quelltext-Asserts auf beide Detailseiten; die geplanten
  Änderungen sind rein additiv (EditForm, Dirty-Dialog etc. bleiben unverändert) — kein
  Bruch erwartet, aber beim Implementieren prüfen.
- **`ActorBackfillWorker`:** führt beim Start zusätzlich den Rating-Backfill aus; bei sehr
  großen Altbeständen + aktiviertem TMDB entstehen viele HTTP-Aufrufe (Rate-Limits der
  TMDB-API beachten; pro Eintrag max. ein Lookup, Marker verhindert Wiederholung).
- **Filme ohne NFO:** werden heute nicht als `Movie` angelegt (`nfoExists`-Check) — bleibt
  unverändert; die Online-Quelle ergänzt nur bei vorhandenem Eintrag ohne NFO-Angabe, sie
  ersetzt keinen fehlenden NFO-Scan.
- **`IsManuallyEdited`:** greift eintragsweit — ein manuell editierter Film behält auch
  ein im NFO geändertes Rating nicht-aktualisiert (bewusstes Verhalten, Anforderung
  eintragsweit gedeckt).
- **`AgeRatingClassifiedAt` im Scanpfad:** wird bei jeder verarbeiteten, nicht manuellen
  Entität gesetzt; manuell editierte Einträge bleiben unmarkiert und werden im Backfill
  billig übersprungen (Muster `ActorsClassifiedAt`).
- **TMDB-Fehlerszenarien:** bei deaktiviertem/fehlerhaftem Dienst bleibt das Feld leer —
  kein sichtbarer Fehler für Anwender; Logging auf Debug/Warning (Scan nicht behindern,
  AK 3).
- **Secrets:** `Tmdb:ApiKey` darf nicht in `appsettings.json` mit echtem Wert committed
  werden; `docs/SECRETS_MANAGEMENT.md`-Tabelle ergänzen.

## Umsetzungsreihenfolge

1. **`AgeRatingNormalizer` + `AgeRatingResult`** (`VideoWebPlayer/Utils/`)
   - Voraussetzungen: keine.
   - Beschreibung: Reine Normalisierungslogik inkl. Mapping-Tabellen und Länderpräfix-
     Erkennung.
2. **Datenmodell + `LoadFromXml` + Migration + Backup-Einträge**
   - Voraussetzungen: Schritt 1; `dotnet ef` verfügbar (Projektkonvention).
   - Beschreibung: Vier neue Properties je `Movie`/`TVShow`; `LoadFromXml` wertet
     `<mpaa>`/`<certification>` aus; `AddAgeRatings`-Migration; 8 Einträge in
     `OptionalRestoreColumns`.
3. **`TmdbOptions` + `ITmdbAgeRatingService`/`TmdbAgeRatingService` + `TmdbAgeRating`**
   - Voraussetzungen: keine ( `IHttpClientFactory`/`AddHttpClient` bereits in Verwendung).
   - Beschreibung: Optionsklasse, Interface, fehlertolerante Implementierung,
     `TmdbAgeRating`-Record.
4. **DI + Konfiguration**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `ServiceCollectionExtensions` (Configure, named HttpClient, Singleton),
     `appsettings.json`-Abschnitt, `AutoUpdate:ProtectedFiles`-`JsonKeys`.
5. **Scanpfad: Online-Fallback + Marker im `MediaSourceClassifier`**
   - Voraussetzungen: Schritte 2–4.
   - Beschreibung: Ctor-Parameter, `ExtractTmdbId`, `TryApplyOnlineAgeRatingAsync`,
     Marker-Setzung in `ProcessCollectionAsMovieAsync`/`CreateOrUpdateTVShow`.
6. **Backfill**
   - Voraussetzungen: Schritt 5 (`TryReadNfoAsync`-Extraktion erfolgt hier).
   - Beschreibung: `TryReadNfoAsync`-Extraktion, `BackfillMissingAgeRatingsAsync`,
     Aufruf in `ActorBackfillWorker`.
7. **DTOs + Editierpfad**
   - Voraussetzungen: Schritt 1 (Normalizer für Validierung).
   - Beschreibung: `DtoMovie`/`DtoTVShow`-Properties, `MediaMetadataUpdateRequest.AgeRating`,
     `MediaMetadataEditorService`-Updates + Validierungen.
8. **UI**
   - Voraussetzungen: Schritt 7.
   - Beschreibung: `MetadataEditModel.AgeRating`, `InputSelect` in beiden Edit-Formen,
     Meta-Zeilen-Badges (Film, Serie, Staffel/Episode vererbt), `.agerating-badge` in
     `app.css`.
9. **Tests** (siehe unten)
   - Voraussetzungen: Schritte 1–8.
10. **Dokumentation**
    - `docs/help/medien-editiermodus.md`, `docs/help/medienquellen/*` (NFO-Felder),
      `docs/SECRETS_MANAGEMENT.md` (Tabellenzeile `Tmdb:ApiKey`), `README.md`,
      `docs/RELEASE_NOTES.md`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Normalize_*` (Theory: „FSK 12“, „FSK12“, „Rated PG-13“, „PG-13“, „Germany:16“, „DE:6“, „Rated R“, „NC-17“, „TV-14“, null/leer/unbekannt) | `AgeRatingNormalizerTests` (neu, `VideoWebPlayer.Tests`) | Kanonischer Wert + `MinAge` je Rohwert; unbekannt ⇒ Raw-Fallback + `MinAge = null` |
| `LoadFromXml_SetsAgeRating_FromMpaaElement` | `MovieAgeRatingTests` bzw. bestehende Entity-Testklasse (neu) | `Movie.LoadFromXml` mit `<mpaa>FSK 12</mpaa>` → `AgeRating`/`AgeRatingMinAge`/`AgeRatingRaw`; `<certification>`-Fallback; fehlendes Element ⇒ null |
| `LoadFromXml_SetsAgeRating_FromTvShowNfo` | dto. für `TVShow` | gleiche Fälle für `TVShow.LoadFromXml` |
| `ClassifyAllAsync_MovieNfoMpaa_PersistsAgeRating` | `LocalMediaSourceClassifierTests_AgeRating` (neu, `[Collection(MediaSourceClassifierCollection.Name)]`, echte SQLite + Temp-Verzeichnis) | Scan-End-to-End: Film-NFO mit `<mpaa>` → persistierte Felder + `AgeRatingClassifiedAt` gesetzt |
| `ClassifyAllAsync_TvShowNfoCertification_PersistsAgeRating` | dto. | `tvshow.nfo` mit `<certification>` → `TVShow.AgeRating` |
| `ClassifyAllAsync_ManuallyEditedMovie_KeepsExistingRating` | dto. | `IsManuallyEdited`-Film mit gesetztem Rating wird beim Re-Scan nicht überschrieben (AK 4) |
| `ClassifyAllAsync_NoNfoRating_TmdbFallback` | dto. | Fake-`ITmdbAgeRatingService` injiziert; NFO ohne Freigabe → TMDB-Wert gesetzt; `AgeRatingRaw` = „DE:16“-Form |
| `ClassifyAllAsync_NfoRating_TakesPriorityOverTmdb` | dto. | NFO-Freigabe vorhanden ⇒ TMDB-Service wird nicht aufgerufen / Ergebnis ignoriert (AK 4) |
| `BackfillMissingAgeRatingsAsync_FillsFromExistingNfo` | `MediaSourceClassifierAgeRatingBackfillTests` (neu; Fake-Reader-Muster `BackfillSftpMediaSourceReader`) | Altbestand ohne Marker → NFO gelesen, Rating + Marker gesetzt |
| `BackfillMissingAgeRatingsAsync_SkipsManuallyEdited` | dto. | `IsManuallyEdited`-Eintrag bleibt unverändert |
| `BackfillMissingAgeRatingsAsync_SkipsAlreadyClassified` | dto. | Marker ≠ null ⇒ kein erneuter NFO-Zugriff |
| `BackfillMissingAgeRatingsAsync_MovieDotNfoFallback` | dto. | `movie.nfo`-Fallback über `TryReadNfoAsync`-Kandidaten |
| `GetMovieAgeRatingAsync_*` | `TmdbAgeRatingServiceTests` (neu; `HttpMessageHandler`-Fake hinter `IHttpClientFactory`) | Happy Path DE-Bevorzugung, US-Fallback, Titel-Suche ohne ID, Disabled/kein ApiKey ⇒ null, HTTP-Fehler/Timeout/ungültiges JSON ⇒ null ohne Exception |
| `UpdateMovieAsync_SetsAgeRating` / `UpdateTVShowAsync_SetsAgeRating` / `UpdateAsync_ClearsAgeRating` / `UpdateAsync_RejectsUnknownAgeRating` / `UpdateAsync_RejectsAgeRatingForCollection` | `MediaMetadataEditorServiceTests` (Erweiterung) | Setzen/Löschen/Validierung + `IsManuallyEdited` |
| `ReadFromAsync_LegacyBackupWithoutAgeRatingColumns_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests_AgeRatings` (neu) | `LegacyBackupArchiveBuilder.RemoveColumnsAsync` für alle 8 Spalten; Restore ohne Fehler, Werte null (Pflicht nach AGENTS.md DoD) |
| `FakeTmdbAgeRatingService` | `VideoWebPlayer.Tests/Helpers/` (neu) | Hilfs-Double für Classifier-/E2E-Tests |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MetadataEditorUiStateTests` | Keine Anpassung nötig, solange die assertierten Markup-/Code-Zeilen erhalten bleiben (nur additive Änderungen); beim Implementieren verifizieren |
| `MediaSourceClassifier`-Test-Factories (`CreateContextAndServiceAsync` u. ä.) | Neuer optionaler Ctor-Parameter ist abwärtskompatibel; für TMDB-Fälle Fake-Service übergeben — nur betroffene neue Tests |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Lokale Quelle mit Film-NFO (`<mpaa>FSK 16</mpaa>`) scannen → Filmdetail zeigt Freigabe-Badge | `LocalMediaSourcePipelineE2ETests` (neuer Fall) bzw. neue `AgeRatingE2ETests` | AK 1 + AK 2 | Kompletter Benutzerfluss Scan→API→UI; Unit-Tests decken keine Razor-Ausgabe ab |
| Pflicht | Serien-Detailseite zeigt `tvshow.nfo`-Freigabe; Episodenansicht zeigt geerbte Serienfreigabe | dto. | AK 2 | Anzeige-Vererbung ist nur über die gerenderte Seite nachweisbar |
| Pflicht | Admin öffnet Editiermodus am Film, wählt „FSK 12“ im Dropdown, speichert → Badge sichtbar; Nicht-Admin sieht kein Edit-UI (bestehend) | `MovieCollectionEditE2ETests` (Erweiterung) / `AgeRatingE2ETests` | AK 4 | Einziger anwendersichtbarer Pfad des manuellen Pflegens; `IsManuallyEdited`-Schutz serverseitig mitgeprüft |
| Soll | TMDB deaktiviert/nicht konfiguriert → Detailseite ohne Badge, Scan läuft normal | `AgeRatingE2ETests` (nur wenn in einem Lauf realisierbar; sonst durch Service-/Classifier-Tests abgedeckt) | AK 3 | Standardverhalten ohne Konfiguration ist Teil der Anforderung; Fehlertoleranz selbst ist Unit-testbar |

Bestehende E2E-Tests: **Keine Anpassung erwartet.** `MovieCollectionEditE2ETests` und die
Detailseiten-E2E-Tests laufen weiter, da die Edit-Form-Struktur nur erweitert wird.

## Offene Punkte

Die folgenden Punkte stammen aus den „Offenen Fragen“ der Anforderung; der Plan folgt dem
jeweils empfohlenen Vorschlag (bei Abweichung ist die Planung an der genannten Stelle
anzupassen).

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Granularität: auch `TVShowEpisode`/`MovieCollection`? | Nur `Movie`/`TVShow` persistiert; `TVShowDetails` zeigt die Serienfreigabe in Staffel-/Episoden-Meta-Zeilen (Anzeige-Vererbung), keine eigenen Felder |
| 2 | Feldmodellierung: zwei Spalten oder strukturiert? | Drei Spalten `AgeRating`/`AgeRatingRaw`/`AgeRatingMinAge` (string + string + int) + Marker `AgeRatingClassifiedAt`; kein Enum an der DB-/JSON-Grenze |
| 3 | Editierbarkeit im Admin-Editiermodus? | Ja, für `movie`/`tvshow` via `InputSelect` mit kanonischen Stufen + „Keine“; Schutz über eintragsweites `IsManuallyEdited` |
| 4 | Fremdsystem TMDB vs. Verzicht; Key-Ablage? | TMDB als `TmdbOptions` umsetzen (freie API, technisch vertretbar); ApiKey via `appsettings.Local.json`/Umgebungsvariable/User Secrets, nicht in `Setup`-Entity/DB; IMDb nicht angebunden (nur dokumentiert) |
| 5 | Altbestand: nächster Scan oder Backfill? | Einmaliger `BackfillMissingAgeRatingsAsync` mit `AgeRatingClassifiedAt`-Marker, aufgerufen aus `ActorBackfillWorker` (Muster `BackfillMissingActorsAsync`) |
| 6 | Länderauswahl bei mehreren Angaben? | NFO-Präfixe „Germany:/DE:“ → FSK; TMDB-Liste: `PreferredCountry` (Default DE) → „US“ → erste Angabe; Rohwert bleibt erhalten |
| 7 | Listen-Darstellung (Badge/Filter)? | Vorerst nur Detailansichten; Badge/Filter in `MediaBox`/`MediaBaseEntryList` zurückstellen (Anforderung „ggf.“) |
