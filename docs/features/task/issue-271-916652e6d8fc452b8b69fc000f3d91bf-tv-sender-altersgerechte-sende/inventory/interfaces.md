# Interfaces и контрактные типы (issue-271)

## `IProgramScheduleService`
Файл: `VideoWebPlayer/Services/IProgramScheduleService.cs`

| Метод | Параметры | Возврат | Назначение |
|-------|-----------|---------|------------|
| `EnsureCoverageAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<ProgramScheduleRunSummary>` | Extend-only заполнение плана до горизонта |
| `ReconcileAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<ProgramScheduleReconcileSummary>` | Переоценка: displacement + рематериализация |
| `GetScheduleAsync` | `long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken` | `Task<List<ProgramSlot>>` | Слоты для админ-вида |
| `DeleteDisplacedSlotsAsync` | `string userId, long channelId, CancellationToken` | `Task<int>` | Admin-only: чистка `Displaced` |
| `ResetScheduleAsync` | `string userId, long channelId, CancellationToken` | `Task<ProgramScheduleResetResult>` | Admin-only: полный сброс |
| `GetRuleDiagnosesAsync` | `string userId, long channelId, CancellationToken` | `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` | Admin-only: per-rule диагноз планируемости |

### `ProgramRuleDiagnosis` (record struct, строки ~64–72)

| Параметр | Тип | Смысл |
|----------|-----|-------|
| `RuleId` | `long` | Правило |
| `MaterialMissing` | `bool` | Материал не резолвится (verwaist; для genre также — ни одного канонического имени/строки в фильтре) |
| `CandidateCount` | `int` | Всего кандидатов в экспансии |
| `SchedulableCount` | `int` | Планируемых (probed duration + Freigabe для всех) |
| `NoMediaItemCount` | `int` | Без media item |
| `ProbePendingCount` | `int` | Duration ещё не пробена (retry) |
| `ProbeExhaustedCount` | `int` | Probe-бюджет исчерпан |
| `NoAccessCount` | `int` | Не открыто всей аудитории |

**Расширение:** новый счётчик/причина «kein altersgerechtes Material im Fenster» — например `NoAgeRatedSlotCount`/`WindowBlockedCount`; потребители: `GetRuleDiagnosesAsync` (ProgramScheduleService.cs ~421–429) → `TVSenderAdmin.DiagnosisLabel` (~1632).

### Прочие record-контракты в том же файле
- `ProgramScheduleRunSummary` (~15–22): `SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds`
- `ProgramScheduleReconcileSummary` (~35–43): + `SlotsDisplaced`
- `ProgramScheduleResetResult` (~79): `SlotsDeleted, CursorsDeleted`

## `IAgeRated`
Файл: `VideoWebPlayer/Data/IAgeRated.cs`

| Свойство | Тип | Назначение |
|----------|-----|------------|
| `AgeRating` | `string?` | Каноническое отображаемое значение |
| `AgeRatingRaw` | `string?` | Исходное значение |
| `AgeRatingMinAge` | `int?` | Сравнимое минимальное возрастное значение — вход Fenster-логики |
| `AgeRatingClassifiedAt` | `DateTime?` | Маркер классификации |

Реализовано: `Movie`, `TVShow`. **Не** реализовано: `TVShowEpisode`, `MediaItem`, `ProgramClipGroup`, `PlaylistEntry`, `MovieMediaItem` (trailer-link) — у них Freigabe нет.

## `ITmdbAgeRatingService`
Файл: `VideoWebPlayer/Services/ITmdbAgeRatingService.cs` (описан в `docs/help/medienquellen/api.md`)

`GetMovieAgeRatingAsync` / `GetTVShowAgeRatingAsync` → `Task<TmdbAgeRating?>`; опциональная online-обратная связь, `null` при любой ошибке — влияет только на заполнение `AgeRatingRaw`, не на план.

## Не существует
- Fenster-контракт/record (например `ProgramScheduleAgeWindow { MinAge, EarliestLocalTime }`).
- Enum режима неизвестной Freigabe.
- Поле Freigabe в `SchedulableItem`/`ProgramRuleDiagnosis`.
