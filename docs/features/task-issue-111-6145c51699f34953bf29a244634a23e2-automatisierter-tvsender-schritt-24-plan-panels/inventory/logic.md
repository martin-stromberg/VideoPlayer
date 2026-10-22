# Logik — Bestandsaufnahme

## `ProgramGuideService` (`IProgramGuideService`)
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs` · `VideoWebPlayer/Services/IProgramGuideService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetNowAsync(utcNow, ct)` | public | Jetzt/Gleich-Auskunft; setzt `PositionSeconds`/`RemainingSeconds` beim laufenden Slot |
| `GetUpcomingAsync(utcNow, preview, ct)` | public | Begrenzte Vorschau (max. `MaxPreviewHours` = 12 h), `ArgumentOutOfRangeException` außerhalb |
| `GetChannelIdAsync(ct)` | private | Erster Sender (rein lesend, legt nichts an) |
| `LoadTitlesAsync(slots, ct)` | private | Batch-Auflösung der Titel: Movies / Episoden (Show+Staffel+Nr+Name zusammengesetzt) / Bare-Items — kein Plot, kein Poster, kein Genre |
| `ToEntry(slot, titles, utcNow)` | private static | `ProgramSlot` → `DtoProgramGuideEntry` |

Aktuell werden pro Slot nur `Name` (Movie), das zusammengesetzte Episoden-Titel-Format
`{Show} — {Season}, E{Number}: {Episode}` und der `MediaItem.Name` geladen.
`PosterPictureId`, `Plot`, `GenreNames` werden nicht gelesen.

## `ProgramGuideController`
Datei: `VideoWebPlayer/Controllers/ProgramGuideController.cs`

| Route | Methode | Beschreibung |
|-------|---------|--------------|
| `GET /api/program-guide/now` | `GetNow` | Jetzt/Gleich, `[BearerTokenCheck]`, `CheckLoggedIn()` |
| `GET /api/program-guide/upcoming?hours=` | `GetUpcoming` | 1–12 h, `BadRequest` außerhalb |

## `PicturesController`
Datei: `VideoWebPlayer/Controllers/PicturesController.cs`

| Route | Methode | Beschreibung |
|-------|---------|--------------|
| `GET /api/pictures/{id}` | `GetPicture` | Liefert `Picture.Data`; unbekannte/leere Id → Platzhalter-PNG; Playlist-Cover zugriffsbeschränkt |

Wird von allen Kartenbildern via `/api/pictures/{pictureId}?access_token={token}` genutzt
(`<img>` kann keinen Authorization-Header senden).

## `EpisodesController`
Datei: `VideoWebPlayer/Controllers/EpisodesController.cs`

| Route | Methode | Beschreibung |
|-------|---------|--------------|
| `GET /api/episodes/{episodeId}/background-image` | `GetBackgroundImage` | Generiertes Episoden-Standbild (Querformat), lazy-generierend; Zugriffsprüfung `IsAccessibleAsync` |

## `MediaPictureUrlResolver`
Datei: `VideoWebPlayer/Components/Playlists/MediaPictureUrlResolver.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `Resolve(long? pictureId, string? authorizationToken)` | `/api/pictures/{id}?access_token={token}` oder `/images/placeholder.png` bei `null` |

## `TVSenderAdmin` (Blazor-Komponente)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

| Element | Beschreibung |
|---------|--------------|
| `schedule` (`List<ProgramSlot>`) | Sendeplan-Liste, Tabelle `#tvsender-schedule-table`, Zeilen `.tvsender-slot-row`, verdrängte `.tvsender-slot-displaced` |
| `ReloadScheduleAsync` | Lädt Slots (`ProgramScheduleService.GetScheduleAsync(channel.Id, UtcNow-2h, 200)`) + Wörterbücher `slotRuleNames`, `slotMovieNames`, `slotEpisodeTitles`, `slotItemNames` — nur Namen |
| `SlotTitle(slot)` | Titel-Fallback-Kette Episode → Movie → Item-Name → „(Inhalt nicht gefunden)" |
| `RuleName(slot)` | Regelname bzw. „(Regel gelöscht)" |
| `GenerateNowAsync` / `CleanupDisplacedAsync` / `ResetScheduleAsync` | Toolbar-Aktionen des Sendeplan-Tabs |
| `Client` (`VideoWebPlayerClient`) | Bereits injiziert; `EnsureAuthorizationTokenSilentlyAsync` in `OnInitializedAsync` → `Client.AuthorizationToken` für Bild-URLs verfügbar |

## `LiveTv` (Blazor-Komponente)
Datei: `VideoWebPlayer/Components/Pages/LiveTv.razor` + `LiveTv.razor.css`

| Element | Beschreibung |
|---------|--------------|
| `guide` (`DtoProgramGuideNow`) | Jetzt/Gleich; `#live-tv-now`, `#live-tv-now-title`, `#live-tv-next` |
| `upcoming` (`DtoProgramGuideEntry[]`) | `PreviewHours = 6`; Liste `ul.livetv-upcoming-list` → `li.livetv-upcoming-row` mit `data-slot-id` |
| `RefreshGuideAsync` | Pollt alle 15 s Now + Upcoming via `IProgramGuideApiClient` |
| `FormatTime`, `FormatDuration`, `ProgressPercent` | Anzeige-Helfer |
| `StreamUrl` | `/api/live-stream/live.m3u8?access_token=...` — Muster für Token-in-URL |
| Player | `liveTv.attach` via JS/HLS; Sendelücke `#live-tv-no-program` |

## `IProgramGuideApiClient` / `VideoWebPlayerClient.ProgramGuide`
Dateien: `VideoWebPlayer.Client/IProgramGuideApiClient.cs`, `VideoWebPlayer.Client/VideoWebPlayerClient.ProgramGuide.cs`

| Methode | Beschreibung |
|---------|--------------|
| `RequestProgramGuideNowAsync(ct)` | `api/program-guide/now` |
| `RequestProgramGuideUpcomingAsync(hours, ct)` | `api/program-guide/upcoming?hours=` |

Ändert sich nicht — die DTO-Erweiterung fließt automatisch durch die JSON-Deserialisierung.

## CSS-Konventionen
Datei: `VideoWebPlayer/wwwroot/app.css`

- Seitentokens `:root` → `--vp-*` (dunkelrot/blau); Admin-Bereich `.admin-console` → `--admin-*`.
- `program-card` existiert **nicht**; Admin-Stile liegen zentral in `app.css`, `LiveTv` hat zusätzlich `LiveTv.razor.css` (scoped).
- Bestehende Verdrängt-Darstellung: `.admin-console .table .tvsender-slot-displaced td` (durchgestrichen) — muss auf Karten übertragen werden.
