# Scan- und Klassifizierungs-Pipeline

Wie aus Dateien in einer Medienquelle Filme, Serien und Bilder werden. Für die
Demoinstallation relevant, weil Variante (a) der Anforderung — Dateiablage +
Scan — genau diese Pipeline benutzt.

## Aufrufkette

```
MediaSourceScanService (BackgroundService, Loop)
  └─ MediaSourceScanCoordinator.RunFullScanAsync(operationName, maxCollectionsPerRun)
       ├─ MediaSourceScanner.ScanAllSourcesAsync      → Root-Collections anlegen/aktualisieren
       ├─ MediaSourceScanner.ScanNextMediaCollection  → je eine fällige Collection (Loop bis Cap)
       ├─ MediaSourceClassifier.ClassifyMediaItemsAsync        → MediaItems markieren
       └─ MediaSourceClassifier.ClassifyMediaCollectionsAsync  → NFOs → Entitäten + Bilder

Manuell: MediaSourceAdmin.razor:221 (Komplettscan, ungedeckelt),
         MediaSourceExplorer.razor:251 (RescanCollectionTreeAsync je Teilbaum)
```

## `MediaSourceScanService`

Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

- Background-Loop: `_initialDelay` 10 s, `_loopDelay` 1 min (Zeilen 51–52).
- Scan nur, wenn `now - lastRun >= scanProcessInterval` —
  `ProgramSettingsService.GetScanIntervalsAsync` liefert
  `ScanProcessIntervalMinutes` aus `Setup`, Default **60 min**
  (`ProgramSettingsService.cs:110-114`, Setup-Default Zeile 40-42).
- Komplettscan gedeckelt auf `MaxCollectionsPerAutomaticRun = 64` (Zeile 15).

## `MediaSourceScanner`

Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs`

| Methode | Zeilen | Beschreibung |
|---------|--------|--------------|
| `ScanAllSourcesAsync` | 52–98 | Setzt `LastScannedAt`, legt Root-`MediaCollection` an (`ScanDueAt = now`) oder plant Rescan bei geändertem `CreatedAt` |
| `ScanNextMediaCollection` | 105–122 | Scannt die älteste fällige Collection (`ScanDueAt <= now`) |
| `ScanMediaCollectionAsync` | 130–146 | Scannt eine Collection nach Id |
| `ScanCollectionTreeAsync` | 154–204 | BFS über den Teilbaum |
| `ScanMediaCollectionInternalAsync` | 206–296 | Diffet `ReadDirectoryEntries` gegen DB: neue Collections/`MediaItems` anlegen, geänderte als `Changed` markieren (Duration zurückgesetzt); setzt `Classifyable` hochpropagierend, wenn alle Kinder classifyable sind |

Nicht lesbare Collections werden mit `Classifyable = true` übersprungen statt
abzubrechen (Zeilen 216–227).

## `MediaSourceClassifier`

Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` (2092 Zeilen)

Serialisierung: prozessweites statisches `_classificationRunning`-Flag —
parallel laufende Klassifizierung wird übersprungen (Zeilen ~90-95; deshalb
`[Collection(MediaSourceClassifierCollection.Name)]` in den Tests).

### Video-/Bildkonventionen

| Konstante | Zeile | Werte |
|-----------|-------|-------|
| `fileExtensions_Video` | 42 | `VideoFileExtensions.All` = **`.mp4`, `.avi`, `.mkv`, `.mpeg`** (`Utils/VideoFileExtensions.cs:15`) |
| `PictureTypes` | 43 | `poster`, `banner`, `fanart`, `thumb` |
| `MovieCollectionPictureNames` | 44 | `folder`, `banner`, `poster`, `fanart` |
| `TVShowPictureTypes` | 46 | `poster`, `banner`, `fanart`, `thumb`, `""` |
| `TVShowImageExtensions` | 47 | `.jpg`, `.jpeg`, `.png`, `.webp` |
| `TrailerFolderName`/`TrailerSuffix` | 776–777 | Ordner `trailers`, Dateiendung `-trailer` (Plex/Jellyfin-Konvention) |

### TVShow-Klassifizierung

- Auslöser: `tvshow.nfo` in der Collection (`ProcessCollectionAsTVShowAsync`,
  Zeilen 453–483). XML-Felder `title`, `genre` u. a. → `TVShow` via
  `CreateOrUpdateTVShow` (485–545).
- Episoden (`ProcessEpisodesForTVShowAsync`, 562–770): jede Videodatei mit
  gleichnamiger `.nfo` (`<episodedetails>`); `<season>` → Staffelname
  „Specials" bei 0, sonst `Staffel NN`; `<episode>` Pflicht (0 → skip);
  `<title>`, `<aired>`, `<premiered>`, `<plot>` → `TVShowEpisode`-Felder.
  Staffeln werden **pro Episode-Collection** angelegt (`TVShowSeason.CollectionId`
  = Collection der Datei — Unterordner „Staffel 1" o. ä. abbildbar).
- Verknüpfung: `TVShowEpisodeMediaItem`; `RecentEntry`-Service-Einträge bei
  Neuanlage.

### Film-Klassifizierung

- `ProcessCollectionAsMovieAsync` (790–998): Videodateien mit gleichnamiger
  `.nfo`, XML-Root `<movie>` → `Movie` (`movie.LoadFromXml(xml)`),
  `MovieMediaItem`-Verknüpfung; Trailer-Regeln beachten (keine eigene
  Film-Anlage).
- Filme einer Collection werden zu einer `MovieCollection` gebündelt
  (Name = gemeinsames Präfix, Fallback Collection-Name; 915–968).
- Genres: `GetOrCreateGenresAsync` (1504+) mit Template-Auflösung
  (`GenreCanonicalResolver`).

### Bilder (wichtig für Anzeigebilder der Demoinstallation)

| Regel | Ort |
|-------|-----|
| Film-Bilder: `<filmname>-<type>.<ext>` neben der Videodatei (`poster`, `banner`, `fanart`) | `AssignPicturesToMovieAsync` 1057–1098 |
| MovieCollection-Bilder: `folder`/`banner`/`poster`/`fanart` + Fallback erstes Filmbild | 1140–1212 |
| TVShow: `poster.jpg`/`banner.jpg`/`fanart.jpg`/`thumb.jpg` im **Show-Root**; Fallback aus Staffeln | `AssignPicturesToTVShowAsync` 1214–1284 |
| Staffel: `season<X>-<type>`-Art-Namen plus Episoden-Fallback | `AssignPicturesToTVShowSeasonAsync` 1287–1360 |
| Episode: `<episodenname>-<type>`-Bilder; leerer Typ `""` = gleichnamige Bilddatei | `AssignPicturesToTVShowEpisodeAsync` 1362–1408 |
| `GetOrCreatePicture` | 1100–1138: `Picture`-Entität aus `MediaItem`; **Bild-Bytes werden per `ReadFileStreamAsync` in `Picture.Data` geladen** — Bilder müssen als Dateien in der Quelle liegen |

Zusätzlich `SetPictureAndMarkBackgroundForUpdateAsync` (1410+): markiert
Episoden für die lazy Hintergrundbild-Generierung
(`EpisodeBackgroundImageService`, siehe ffmpeg-images.md).

## Konsequenz für Demodaten (Variante (a))

Die minimale Dateistruktur, die diese Pipeline zu einer screenshot-tauglichen
Bibliothek führt:

```
<demo-media>/
  Filme/
    der-sternenfalz.mp4            (kleine Dummy-Datei, echter Container für Wiedergabe)
    der-sternenfalz.nfo            (<movie><title>…<genre>…<plot>…)
    der-sternenfalz-poster.jpg
    der-sternenfalz-fanart.jpg
  Serien/
    Nebelwerk/                     (tvshow.nfo, poster.jpg, banner.jpg)
      Staffel 1/
        s01e01.mp4
        s01e01.nfo                 (<episodedetails><season>1<episode>1<title>…)
        s01e01-poster.jpg
```

Beweis im Bestand: `LocalMediaSourcePipelineE2ETests` legt genau so eine
Struktur (`.mkv` + `tvshow.nfo` + `episode01.nfo`, Dateiinhalt nur `„video"`)
und die klassifizierte `TVShow` ist danach über die API abrufbar.

Für „sofort sichtbar": `ApplyAsync` müsste nach dem Anlegen der Quelle
`MediaSourceScanCoordinator.RunFullScanAsync` (oder gezielt
`RescanCollectionTreeAsync` auf die Root-Collection) anstoßen — synchron oder
als Hintergrundtask. Alternativ greift Variante (b), direkte Persistenz der
Entitäten, ohne Scan; sie muss dann `MediaItem`+`Movie`/`TVShow*`+
Link-Tabellen+`Picture` konsistent selbst befüllen.
