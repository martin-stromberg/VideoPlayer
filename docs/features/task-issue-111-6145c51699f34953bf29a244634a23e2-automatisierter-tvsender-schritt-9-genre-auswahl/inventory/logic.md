# Logik — Bestandsaufnahme

## `ProgramRuleService` (Services/ProgramRuleService.cs)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes(ruleType)` | public | Genre-Material erlaubt bei `GenreRotation` (nur Genre), `Repeat`, `HolidaySpecial` (alle Materialarten), `Filler` (Movie/MovieCollection/Genre/Playlist) |
| `GetOrCreateChannelAsync` | public | Liefert/legt den einen Sender an |
| `RenameChannelAsync`, `GetRulesAsync`, `CreateRuleAsync`, `UpdateRuleAsync`, `SetRuleActiveAsync`, `DeleteRuleAsync` | public | CRUD mit `RequireAdminAsync`; feuern `IProgramScheduleSignal.NotifyScheduleChanged()` |
| `ValidateRuleInputAsync` | private | Serverseitige Validierung inkl. Materialart-zu-Regelart-Check und Existenzprüfung `MaterialId` (bei Genre: `Genres.AnyAsync(Id == MaterialId)`) |

Eingabemodell `ProgramRuleInput` (record): `Name, RuleType, Recurrence, DayOfWeek, SingleDate, TimeOfDay, EpisodeCount, SlotDuration, MaterialType, MaterialId, Priority, IsActive` — ein einzelnes `MaterialId`, keine Listen.

## `ProgramScheduleService` (Services/ProgramScheduleService.cs)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` / `ReconcileAsync` | public | Planerzeugung (extend-only) / Nachführung; beide expandieren je Regel via `ExpandMaterialAsync` und führen `RuleState`+`ProgramRuleCursor` |
| `ExpandMaterialAsync` | private | Dispatch auf `MaterialType`; `Genre` → `ExpandGenreAsync(rule.MaterialId, ...)` |
| `ExpandGenreAsync(genreId, access, summary, ct)` | private | Filme über `MovieGenres` (Sort: `ReleaseDate ?? PremieredAt`, `Name`, `Id`), Serien über `TVShowGenres` → alle Episoden (Sort: Show-Name, Season, `Number`); Danach Freigabe-Filter (`AccessContext.Allows`) und `Duration != null` via `TryMovieItem`/`ToItem` |
| `LoadAccessContextAsync` | private | Freigabe-Snapshot: planbar = für jeden Nicht-Admin sichtbar (Quellfreigabe ODER Einzel-Unlock von Serie/Filmsammlung); `Everything` bei 0 Nicht-Admins |
| `NextIndexFor`/`PersistCursors`/`RewindCursor` | private/static | Cursor-Semantik über `(ContentType, ContentId)` — unverändert bei Genre-Union nutzbar, da Movie/Episode-Ids bestehen bleiben |

Es gibt **keinen** Quell-Filter und **keine** Alternativnamen-/Hidden-Auflösung in der Expansion.

## `MediaMetadataEditorService.GetGenreOptionsAsync` (Services/MediaMetadataEditorService.cs)
Lädt **alle** `Genres`, gruppiert case-insensitiv nach getrimmtem `Name`, liefert `DtoGenreOption{Id, Name}` des jeweils ersten Vertreters — kein `IsHidden`-, kein Alternativnamen-, kein Fenster-Filter, kein Quellbezug. Aufgerufen von `ItemsController.GetGenres` (`GET api/items/genres`).

Konsumenten von `Client.RequestGenreOptionsAsync`: `TVSenderAdmin.razor` (Regel-Formular), `MovieCollectionDetails.razor`, `TVShowDetails.razor` (Metadaten-Editor), `PlaylistGenreEditor.razor`, `PlaylistsList.razor`.

## `MediaSourceClassifier.GetOrCreateGenresAsync` (Services/MediaSourceClassifier.cs, privat)
Scan-Genre-Auflösung pro Quelle: Name → Genre-Zeile in derselben `MediaSourceId`; besitzt die Zeile Alternativnamen (≠ Eigenname), wird **rekursiv** auf die Alternativnamen aufgelöst (statt der Zeile selbst werden die Ziel-Genres verknüpft); Duplikate per `GroupBy(Id)` entfernt. Nicht gefundene Namen werden als neue Genre-Zeilen angelegt.

## `GenreService.GetSeasonalGenresAsync` (Services/GenreService.cs)
Saisonale Sichtbarkeit: nur Zeilen mit **beiden** Daten gelten als saisonal begrenzt; das Fenster wird jahresweise vorgeschoben (`while (end < now) { start=+1J; end=+1J }`), aktiv wenn `start <= now <= end`. Zeilen ohne Fenster gelten als nicht saisonal eingeschränkt. `MarkGenresAsChangedAsync` setzt `Setup.GenresChanged`.

## `SourceGenresController` (Controllers/SourceGenresController.cs)
`GET api/SourceGenres` (pro Quelle des Users) und `GET api/SourceGenres/{sourceId}` (mit Quellzugriffsprüfung über `MediaSourceUsers` + `IUnlockedMediaService`); Letzteres filtert `!g.IsHidden` — **Muster für den IsHidden-Filter**.

## `GenreAdmin.razor`
Dedup `GroupBy(g.Name.Trim().ToLower()).First()`; Bearbeitung schreibt `IsHidden`/`StartDate`/`EndDate`/`GenreNames` auf **alle** Zeilen gleichen Namens (`Where(g => g.Name == editGenre.Name)`) — Status ist fachlich namensbezogen gedacht, die Live-Daten können aber pro Quelle abweichen.

## `TVSenderAdmin.razor` (Components/Pages/Admin/)
- `genreOptions` via `Client.RequestGenreOptionsAsync()`; `rule-material-genre`-`<select>` setzt `MaterialType=Genre`, `MaterialId`, `MaterialDisplayName`.
- `MediaSearchSelector` deckt `TVShow`/`Movie`/`MovieCollection` ab; Playlist-Auswahl analog per `<select>`.
- Regel-CRUD direkt über injiziertes `IProgramRuleService` (serverseitig, `userId` aus Auth-State).
- `MaterialLabel` löst `MaterialId` über `DbContext` in einen Anzeigenamen auf.

## `VideoWebPlayerBackupData` (Services/Backups/)
`OptionalRestoreTables`/`OptionalRestoreColumns`/`OptionalRestoreIntDefaults`-Listen steuern Alt-Backup-Toleranz; `LegacyBackupArchiveBuilder` (Tests) baut/verstümmelt Archive für Regressionstests.
