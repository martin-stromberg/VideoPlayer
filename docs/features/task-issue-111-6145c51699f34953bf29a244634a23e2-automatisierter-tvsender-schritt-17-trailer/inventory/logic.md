# Bestandsaufnahme: Logik

## `MediaSourceScanner`
Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanAllSourcesAsync` | public | Legt Root-`MediaCollection`s je Quelle an/markiert Scan |
| `ScanNextMediaCollection` | public | Scannt die fälligste Collection |
| `ScanMediaCollectionAsync` | public | Scannt eine bestimmte Collection |
| `ScanCollectionTreeAsync` | public | Scannt Collection + alle Kinder |
| `ScanMediaCollectionInternalAsync` | private | Liest Verzeichniseinträge, legt/aktualisiert `MediaCollection`s (Ordner) und `MediaItem`s (Dateien); setzt `Classifyable` |

Der Scanner ist inhaltsagnostisch: **jede** Datei (auch `*-trailer.*` und Dateien in
`trailers/`-Ordnern) wird als `MediaItem` persistiert → Trailer sind bereits
`MediaItem`s mit eigenem Duration-Probe-Pfad. Trailer-Erkennung gehört folglich in den
Classifier, nicht in den Scanner.

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` (1799 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync` | public | `ProcessMediaItemsAsync` + `ProcessMediaCollectionsAsync` |
| `ClassifyMediaItemsAsync` / `ClassifyMediaCollectionsAsync` / `ClassifyCollectionTreeAsync` | public | Teilklassifizierungen |
| `ProcessMediaItemsAsync` | private | Setzt `ClassifiedAt`/`Changed=false` je Item |
| `ProcessMediaCollectionsAsync` | private | do-while über geänderte Collections (absteigende Id); je Collection `ProcessCollectionAsTVShowAsync` + `ProcessCollectionAsMovieAsync`; markiert danach `ParentMediaCollection.Changed = true` → **Eltern wird (re-)klassifiziert, sobald ein Kind fertig ist** |
| `ProcessCollectionAsMovieAsync` | private | Iteriert Video-`MediaItem`s der Collection; `*.nfo` mit `<movie>` → `Movie` + `MovieMediaItem` (ohne Trailer-Flag); MovieCollection-Zuordnung; `AssignActorsToMovieAsync`, `AssignPicturesToMovieAsync` |
| `ProcessCollectionAsTVShowAsync` → `ProcessEpisodesForTVShowAsync` | private | `tvshow.nfo` + `episodedetails`-NFOs → Show/Season/Episode; **inkl. Items der Kind-Collections** |
| `AssignPicturesToMovieAsync` | private | Stem-Match `{film}-poster` usw. über `MovieMediaItems` |
| `BackfillMissingActorsAsync` | public | Nachträgliche Actor-Erfassung; nimmt `movie.MovieMediaItems.FirstOrDefault()` als NFO-Träger |

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (1277 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Extend-only-Plangenerierung: Regeln laden → `ExpandMaterialAsync` je Regel → `BuildOccurrences` → `MaterializeOccurrence` → `FillGaps` → `PersistCursors` |
| `ReconcileAsync` | public | Re-Bewertung: verdrängt invalide Airings/Füller, rematerialisiert (`MaterializeOccurrenceEvicting`), `FillGaps` |
| `GetScheduleAsync` | public | Liest geplante Slots |
| `GetRuleDiagnosesAsync` | public | Expansion je Regel mit Diagnose-Zählern (Admin-only) |
| `ExpandMaterialAsync` | private | Switch über `MaterialType` → `Expand{Show,Movie,MovieCollection,Genre,Playlist}Async` |
| `MovieMediaItemsAsync` / `EpisodeMediaItemsAsync` | private | Repräsentatives `MediaItemRef` je Inhalt (probed-first, dann niedrigste Id) |
| `TryMovieItem` / `ToItem` | private | Kandidat → `SchedulableItem` (Duration-/Access-Gate, Zähler) |
| `FillGaps` | private | Lücken zwischen belegten Intervallen; Round-Robin über Füller-Regeln (Priorität absteigend), `PeekAt(0)` + Fit-Check (Horizon-Overshoot erlaubt) + `Commit(1)` |
| `RuleState` | private Klasse | `Items` (Ring), `NextIndex`, `PeekAt(offset)` modulo, `Commit(n)`, `RewindTo(idx)` |
| `SchedulableItem` | private record | `(ContentType, ContentId, MediaItemId, MovieId?, TVShowEpisodeId?, Duration)` |

Wichtig: `ExpandMaterialAsync` läuft aktuell **vor** der Materialisierung der
Nicht-Füller-Ausstrahlungen — für die Trailer-Auswahl („Film im Plan") muss die
Trailer-Expansion aufgeschoben werden, bis die neuen Movie-Slots materialisiert sind.

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes` | public | `Filler` → `{Movie, MovieCollection, Genre, Playlist}`; `Repeat`/`HolidaySpecial` → `AllMaterialTypes` (`Enum.GetValues`, neue Werte automatisch enthalten) |
| `ValidateRuleInputAsync` | private | u. a. `GetAllowedMaterialTypes(...).Contains(...)` + `exists`-Switch mit Entity-Lookup je Materialart |
| `CreateRuleAsync` / `UpdateRuleAsync` | public | Admin-only (`RequireAdminAsync`), Genre-/Quellen-Links nur für Genre-Material |
| `MaterialIdFor` / `ReplaceGenreLinksAsync` | private | `MaterialId` = erstes Genre bei Genre-Material |

## `ProgramGuideService`
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs`

`ToEntry` löst `MovieId`→Filmtitel, `TVShowEpisodeId`→Episodentitel auf; `IsFiller` aus
`slot.RuleType`. Trailer-Slots mit gesetztem `MovieId` zeigen den Filmnamen.

## `ItemsController`
Datei: `VideoWebPlayer/Controllers/ItemsController.cs`

`FindMediaItemAsync("movie")` → erstes `MovieMediaItem.MediaItem` — muss Trailer-Links
ausblenden, sonst würde ggf. die Trailer-Datei statt des Films abgespielt.

## `TVSenderAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (1208 Zeilen, Schritt-16-Struktur mit Tabs)

- Regel-Editor: `MediaSearchSelector` (TVShow/Movie/MovieCollection), Genre-Checkboxen
  (`rule-material-genre`), Quellen-Checkboxen, Playlist-`<select>` (`rule-material-playlist`).
- `AllowedMaterialTypes` steuert, welche Auswahl angeboten wird.
- `MaterialLabel` löst Entity-Namen je `MaterialType` auf.
- Sendeplan-Tabelle über `SlotTitle(slot)` (MovieId→Name, Episode→Titel).
