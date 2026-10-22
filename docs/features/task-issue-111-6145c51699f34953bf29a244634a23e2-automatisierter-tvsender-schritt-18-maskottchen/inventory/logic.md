# Bestandsaufnahme: Logik

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Erzeugt Sendeplätze bis Horizont: Occurrences materialisieren → Trailer-Expansion (verzögert) → `FillGaps` |
| `ReconcileAsync` | public | Validierungssweep: Ausstrahlungs-Simulation, Suffix-Verdrängung, Cursor-Rücksetzung, Neu-Füllung |
| `GetScheduleAsync` | public | Admin-Planansicht (Slots ab Zeitpunkt, limitiert) |
| `GetRuleDiagnosesAsync` | public | Admin-only; führt `ExpandMaterialAsync` je Regel mit `SummaryCounters` aus → Diagnose-Zähler |
| `ExpandMaterialAsync` | private | Switch über `MaterialType` → `ExpandShow/Movie/MovieCollection/Genre/Playlist/TrailersAsync` |
| `ExpandTrailersAsync` | private | Trailer-Links (`IsTrailer`), Dauer-/Zugriffs-Filter, Sortierung nach Film-Ausstrahlung |
| `FillGaps` | private | Füller-Ring nach Priorität; `MaterialType == Trailer` → Vorlauf-Prüfung, sonst `PeekAt(0)`; Lücke muss passen (Horizont-Kante darf überlaufen) |
| `LoadAccessContextAsync`/`AccessContext.Allows` | private | planbar = für **jeden** Nicht-Admin sichtbar (Quelle voll geteilt ODER Unlock-Wurzel); ohne Nicht-Admins alles frei |
| `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` | private | repräsentative Datei je Inhalt; `!IsTrailer`-Filter auf Film-Seite |
| `NextIndexFor`/`PersistCursors` | private | Cursor-Fortsetzung über `ProgramRuleCursor` (`ContentType`+`ContentId`) |
| `RuleState`/`SchedulableItem` | private | Ring über expandierte Items (`PeekAt`/`Commit`/`RewindTo`); Item trägt `ContentType`,`ContentId`,`MediaItemId`,`MovieId`,`TVShowEpisodeId`,`Duration` |

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes` | public | Regelart → Materialtypen; `Filler` = Movie, MovieCollection, Genre, Playlist, Trailer |
| `ValidateRuleInputAsync` | private | Materialtyp-Zulässigkeit + Existenzprüfung (`AnyAsync`-Switch; `Trailer` → `true`, da global) |
| `CreateRuleAsync`/`UpdateRuleAsync` | public | Admin-only CRUD; feuert `IProgramScheduleSignal` |
| `GetGenreMaterialOptionsAsync` | public | Genre-Optionsliste (kanonisiert, Quell-Filter) — nur Genre-Material |

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProcessCollectionAsMovieAsync` | private | Movie-Einordnung **nur bei vorhandener `<datei>.nfo` mit `<movie>`-Wurzel** — Videodateien ohne NFO bleiben rohe `MediaItem`s |
| `ProcessEpisodesForTVShowAsync` | private | Episoden via `tvshow.nfo` + Episoden-NFO |
| `IsTrailerFileName`/`IsTrailerFolder` | private | Konvention `-trailer`-Suffix / `trailers`-Ordner → keine eigenen Movie-Sätze |

**Konsequenz für N12:** Clips ohne `.nfo` werden nie klassifiziert → nicht als reguläre Filme
sichtbar, aber als `MediaItem` geprobt (siehe unten). Ein `IsTrailer`-analoger Marker ist für
das Verbergen nicht erforderlich.

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `VideoExtensionPredicate` | private | Auswahl aller `MediaItem`s mit Video-Endung — **unabhängig von der Einordnung** |
| Probe-Queue | — | `Duration == null` + Retry-Logik; schreibt `Duration`/`DurationProbedAt`/`DurationProbeAttempts` |

## `ProgramGuideService`
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetNowAsync`/`GetUpcomingAsync` | public | Programmauskunft; `LoadTitlesAsync` löst Titel batchweise (Movies + Episodes) |
| `ToEntry` | private | `DtoProgramGuideEntry`; ohne `MovieId`/`TVShowEpisodeId` bleibt Titel `"Unbekannt"`, `MediaType`/`MediaId` `null` |

## `TVSenderAdmin` (Razor-Seite)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

| Element | Beschreibung |
|---------|--------------|
| `#rule-material-picker` | `MediaSearchSelector` (TVShow/Movie/MovieCollection) + Playlist-`<select>` + Trailer-Checkbox |
| `OnPlaylistSelected`/`OnTrailerToggled`/`OnMaterialSelected` | Setzen `MaterialType`/`MaterialId`/`MaterialDisplayName` |
| `MaterialLabel`/`MaterialKindLabel` | Anzeige von Materialname/-art (Switch über `ProgramRuleMaterialType`) |
| `ReloadScheduleAsync`/`SlotTitle` | Titel-Auflösung der Planansicht (Movies/Episodes; sonst „(Inhalt nicht gefunden)") |
| `playlistOptions` | via `Client.RequestPlaylistsAsync()`; Quell-Liste käme analog über `DbContext.MediaSources` |

## `LiveStreamMediaResolver` (Kontext)
Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs` — löst Slots über `MediaItemId`
auf; funktioniert für nicht eingeordnete Items unverändert.
