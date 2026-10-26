← [Zurück zur Übersicht](index.md)

# Medienquellen — API

## Übersicht

Das Feature exponiert keine neuen HTTP-Endpunkte und erweitert das Client-DTO `DtoMediaSource` bewusst nicht (kein Consumer benötigt den Quelltyp). Die relevante Schnittstelle ist die interne Code-Abstraktion `IMediaSourceReader`, über die sämtlicher Dateizugriff auf Medienquellen läuft.

## Authentifizierung

Nicht relevant — interne Schnittstelle ohne HTTP-Exposure. Die bestehenden Endpunkte (`ItemsController.StreamMediaItem`, `Download`) behalten ihre Authentifizierung über `access_token` bzw. Authorization-Header unverändert bei.

## Rate Limiting

Nicht relevant.

## Endpunkte / Methoden

### `IMediaSourceReader` (`VideoWebPlayer/Services/IMediaSourceReader.cs`)

**Beschreibung:** Abstraktion des Dateizugriffs auf eine Medienquelle. Implementiert von `SftpMediaSourceReader`, `LocalMediaSourceReader` und `MediaSourceReaderDispatcher` (letzterer wählt anhand `MediaSource.SourceType` und ist als `IMediaSourceReader` im DI-Container registriert).

**Methoden:**

| Name | Signatur | Beschreibung |
|------|----------|--------------|
| `ReadRootDirectory` | `IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)` | Liefert die Root-`MediaCollection` der Quelle |
| `ReadDirectoryEntries` | `IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)` | Direkte Unterverzeichnisse (`MediaCollection`) und Dateien (`MediaItem`) einer Collection |
| `FileExistsAsync` | `Task<bool> FileExistsAsync(MediaCollection collection, string fileName)` | Existenzprüfung, Dateiname case-insensitiv (`OrdinalIgnoreCase`) |
| `ReadFileAsync` | `Task<string?> ReadFileAsync(MediaCollection collection, string fileName)` | Dateiinhalt als String (UTF-8) oder `null` |
| `ReadFileStreamAsync` | `Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)` | Dateiinhalt als `Stream` oder `null` |
| `OpenFileStream` | `Stream? OpenFileStream(MediaCollection collection, string fileName)` | Seekbarer Dateistream für Streaming/Download (`FileStream` bzw. `SftpStreamWrapper`) oder `null` |

**Rückgabe-Konvention:** Alle Methoden behandeln „Datei existiert nicht", „Pfad außerhalb des Quell-Roots" und „Reparse Point" einheitlich als `false`/`null` — keine Exception an den Aufrufer.

**Fehler:**

| Fall | Verhalten |
|------|-----------|
| Datei/Verzeichnis nicht vorhanden | `null`/`false` bzw. leere Ergebnismenge |
| Pfad-Traversal außerhalb des Quell-Roots (nur `LocalMediaSourceReader`) | `null`/`false` |
| Reparse Point / Verzeichnis als Dateiziel (nur `LocalMediaSourceReader`) | `null`/`false` |
| `DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException` bei `ReadDirectoryEntries` | Exception an den Aufrufer — `MediaSourceScanner` fängt sie pro Collection ab und terminiert `ScanDueAt` neu |

### `ITmdbAgeRatingService` (`VideoWebPlayer/Services/ITmdbAgeRatingService.cs`)

**Beschreibung:** Optionale Online-Quelle für Altersfreigaben, implementiert von `TmdbAgeRatingService` (Singleton, named `HttpClient` „Tmdb" via `IHttpClientFactory`). Der Service ist immer registriert, bleibt aber ohne `Tmdb:Enabled` und `Tmdb:ApiKey` inert und liefert dann `null`.

**Methoden:**

| Name | Signatur | Beschreibung |
|------|----------|--------------|
| `GetMovieAgeRatingAsync` | `Task<TmdbAgeRating?> GetMovieAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken)` | Filme: direkter Lookup per TMDB-ID aus `uniqueid[@type='tmdb']`/`<tmdbid>`, sonst Titelsuche mit Jahr → `release_dates` |
| `GetTVShowAgeRatingAsync` | `Task<TmdbAgeRating?> GetTVShowAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken)` | Serien: analog über `content_ratings` bzw. Titelsuche |

**Rückgabe-Konvention:** `TmdbAgeRating(Country, Certification)` oder `null`. Länderwahl: `TmdbOptions.PreferredCountry` (Default „DE") → „US" → erste Angabe. Jeglicher Fehler (deaktiviert, fehlender `ApiKey`, HTTP-Status, Timeout, `HttpRequestException`, Parsefehler) → `null` + `LogDebug`/`LogWarning`, kein Exception-Pfad, kein Retry.

**Konfiguration (`Tmdb`-Abschnitt, `TmdbOptions`):**

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Tmdb:Enabled` | `bool` | `false` | Online-Ergänzung aktiviert |
| `Tmdb:ApiKey` | `string?` | `null` | TMDB-API-Key — nicht im Repository; Ablage via `appsettings.Local.json`/Umgebungsvariable/User Secrets (`docs/SECRETS_MANAGEMENT.md`); in `AutoUpdate:ProtectedFiles`-`JsonKeys` eingetragen |
| `Tmdb:BaseUrl` | `string` | `https://api.themoviedb.org/3` | API-Basis-URL (überschreibbar für Tests/Mirror) |
| `Tmdb:PreferredCountry` | `string` | `DE` | Bevorzugte Länderfreigabe (Fallback „US", dann erste Angabe) |
| `Tmdb:TimeoutSeconds` | `int` | `10` | Timeout je TMDB-Aufruf (≤ 0 ⇒ Default) |

### `AgeRatingNormalizer` (`VideoWebPlayer/Utils/AgeRatingNormalizer.cs`)

Statische Hilfsklasse: `Normalize(string? raw)` → `AgeRatingResult(DisplayValue, MinAge)`; `ExtractFromXml(XElement)` → `(Raw, Rating)` liest `<mpaa>` (primär)/`<certification>` (Fallback). `CanonicalOptions` liefert die Auswahlwerte für `AgeRatingSelect`. Regeln siehe `business-rules.md`.

## Betroffene bestehende Endpunkte

### `ItemsController.StreamMediaItem` / `ItemsController.Download`

Unveränderte Routen und Authentifizierung. Interner Unterschied: Der Videostream wird über `_reader.OpenFileStream(mediaCollection, fileName)` geholt — bei lokalen Quellen ein `FileStream`, bei SFTP weiterhin `SftpStreamWrapper`. `enableRangeProcessing: true` bleibt bestehen; `null` führt zu `NotFound`.

### `ItemsController` — Detail- und Metadaten-Endpunkte

- `GET /api/items/{type}/{id}`: `DtoMovie` und `DtoTVShow` enthalten zusätzlich `AgeRating` (kanonischer Anzeigewert) und `AgeRatingMinAge` (vergleichbares Mindestalter, `null` bei unbekannter Stufe) — additive Felder, abwärtskompatibel; `AgeRatingRaw` wird nicht exponiert.
- `POST /api/items/metadata`: `MediaMetadataUpdateRequest.AgeRating` wird für `movie`/`tvshow` akzeptiert (kanonischer Wert, bereits gespeicherter Bestandswert oder leer = löschen; Länge ≤ 64) und für `moviecollection`/`tvshowseason`/`tvshowepisode` mit `ArgumentException` → `400` abgelehnt. Details zum Editierpfad siehe `docs/API.md` und `docs/help/medien-editiermodus.md`.
