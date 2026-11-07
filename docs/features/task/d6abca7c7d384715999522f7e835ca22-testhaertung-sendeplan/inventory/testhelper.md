# Test-Helper: `ProgramScheduleTestDb`

Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` (427 Zeilen, `internal sealed`,
`IDisposable`). In-Memory-SQLite (`mode=memory&cache=shared`) mit echten Fremdschlüsseln;
`EnsureCreatedAsync` statt Migrationen — die System-Clip-Gruppe „Trailer" wird explizit
gesät (`SeedTrailerClipGroupAsync`).

## Seed-/Erzeugungsmethoden

| Methode | Zweck |
|---------|-------|
| `CreateAsync(dbName, ct)` | Fixture + `Setup`-Row (`TvSenderEnabled=true`, `ProgramScheduleUnknownRatingMode=Unrestricted`) + Trailer-Gruppe |
| `CreateClipGroupAsync(name, ct)` | Benannte Clip-Gruppe |
| `AddClipToGroupAsync(groupId, mediaItemId, ct)` | Join-Row `ProgramClipGroupItem` |
| `CreateService(scheduleOptions?, logger?)` | `ProgramScheduleService` mit `ProgramRuleService`/`ProgramSettingsService`/`NullLogger` |
| `CreateGuideService()` | `ProgramGuideService` |
| `CreateSlotAsync(...)` | `ProgramSlot` direkt (für Guide-Tests) |
| `AddUserAsync(email, isAdmin)` | `ApplicationUser` |
| `EnsureSourceAsync(id)` | `MediaSource` |
| `GrantSourceAccessAsync(userId, sourceId)` | `MediaSourceUser` |
| `CreateMediaItemAsync(sourceId, duration)` | `MediaCollection` + `MediaItem` mit `Duration`/`DurationProbedAt` |
| `SeedProbeItemAsync(name, duration, probedAt, attempts, ct)` | Item mit explizitem Probe-Status |
| `CreateMovieAsync(name, sourceId, duration, collectionId, releaseDate, ageRatingMinAge, ageRating)` | `Movie` + `MovieMediaItem`; `ageRating` wird via `AgeRatingNormalizer` normalisiert |
| `CreateShowAsync(name, episodeCount, sourceId, duration, ageRatingMinAge)` | `TVShow` + Staffel + Episoden 1..n je mit Media-Item |
| `CreateGenreAsync(name, sourceId, ...)` | `Genre` (+ Alt-Namen, Saisonfenster) |
| `LinkMovieGenreAsync` / `LinkShowGenreAsync` | Genre-Verknüpfungen |
| `LinkMovieMediaItemAsync` / `LinkEpisodeMediaItemAsync` | Zusätzliche Media-Item-Links (Representative-Wechsel) |
| `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, ...)` | `ProgramRule` inkl. `ApplyValidity`-Normalisierung, `genreIds`/`mediaSourceIds`/`clipGroupIds` |
| `GetChannelAsync(ct)` | `ProgramRuleService.GetOrCreateChannelAsync` |
| `SaveDiagAsync()` | `SaveChanges` mit FK-Diagnose im Fehlertext |
| `SlotsAsync(ct)` | Alle `ProgramSlots`, `AsNoTracking`, `OrderBy(Start)` |

## Assertions

| Methode | Was sie prüft |
|---------|---------------|
| `AssertNoDisplacedAsync(fixture, summary, ct, context?)` | `summary.SlotsDisplaced == 0`; Fehlertext mit Displaced-Dump |
| `AssertReconcileNoOpAsync(fixture, summary, ct, context?)` | `SlotsDisplaced == 0 && SlotsCreated == 0` |
| `AssertNoSelfDisplacedAsync(fixture, ct, context?)` | Kein Displaced/Scheduled-Zwilling (gleiches Intervall + Inhalt) |
| `AssertGapless(slots, from, to)` (B1) | **Nur** `slot.Start <= cursor` pro Slot (Überlappungen passieren den Check!) und `cursor >= to` am Ende — Dokumentation verspricht „without gaps or overlaps" |

## Aufrufer von `AssertGapless` (B1)

`ProgramScheduleServiceTests_Reconcile.cs:55,89,197,497`,
`ProgramScheduleServiceTests_ContentScope.cs:228`,
`ProgramScheduleServiceTests_Filler.cs:34,77`,
`ProgramScheduleServiceTests_Trailer.cs:57`,
`ProgramScheduleServiceTests_Generation.cs:393,497`
