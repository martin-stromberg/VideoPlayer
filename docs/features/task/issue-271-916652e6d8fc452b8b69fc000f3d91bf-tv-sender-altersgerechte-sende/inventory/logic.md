# Логика: `ProgramScheduleService` и связанные сервисы (issue-271)

## `ProgramScheduleService`
Файл: `VideoWebPlayer/Services/ProgramScheduleService.cs` (2693 строки)

### Публичные методы

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `EnsureCoverageAsync` | `(DateTimeOffset utcNow, CancellationToken)` (~68–147) | Extend-only прогон: заполняет `[coverageStart, now+horizon]` из активных правил. Поток: правила → `ExpandMaterialAsync` → `RuleState` → `BuildOccurrences` → `BoundStarts` → per-occurrence `MaterializeOccurrence` → trailer/clip-group late expansion → `InsertClipFill` → `FillGaps` → `PersistCursors` → `SaveChanges`. |
| `ReconcileAsync` | `(DateTimeOffset utcNow, CancellationToken)` (~150–388) | Переоценка: грузит окно слотов (lookback → `_maxOccurrenceLookback` при триггере/`HasRescuableInvalid`), `ReconcileWindow.Reload` = Rebuild+Validate → suffix-displacement невалидных airings, filler-проверка, `RewindCursor` → рематериализация через `MaterializeOccurrenceEvicting` → `InsertClipFill`/`FillGaps` → persist. |
| `GetScheduleAsync` | `(channelId, fromUtc, maxCount)` (~391) | Слоты для админ-вида. |
| `GetRuleDiagnosesAsync` | `(userId, channelId)` (~400–433) | Admin-only (`RequireAdminAsync`). Для каждого правила (вкл. неактивные) гонит `ExpandMaterialAsync` со свежими `SummaryCounters` → `ProgramRuleDiagnosis`. **Точка расширения для Jugendschutz-диагноза.** |
| `DeleteDisplacedSlotsAsync` | `(userId, channelId)` (~436) | Admin-only: удаляет `Displaced`-слоты. |
| `ResetScheduleAsync` | `(userId, channelId)` (~447) | Admin-only: полный сброс слотов и курсоров. |

### Материализация occurrence

| Метод | Строки | Что делает / где Fenster-фильтр встраивается |
|-------|--------|----------------------------------------------|
| `MaterializeOccurrence` | ~1407–1462 | EnsureCoverage-путь. `ShiftPastInvaders` → `SelectItems` → запись `ProgramSlot`. Конфликт ⇒ `DroppedConflicts++`, курсор не трогается. |
| `MaterializeOccurrenceEvicting` | ~987–1057 | Reconcile-путь: тот же поток + возвращает проигравших kept-слотов (`evicts`-правило). |
| `SelectItems` | ~1472–1492 | Общий вход выборки: flat → `SelectAiringItems`, interleaved → `SelectAiringItemsInterleaved`; возвращает `(Taken, TakenCounts)` — без коммита. |
| `SelectAiringItems` | ~1588–1625 | Кольцевая выборка flat: `wanted` (`EpisodeCount`/`SlotDuration`/1), bound-правила («a started item always completes»; `minStartRemain`; hard-bound = должен завершиться в окне). **Здесь же место пропуска items, чья Freigabe не в Fenster: `items[(headIndex + ring) % count]` — item можно пропустить в кольце (`ring++` без `selected++`), тогда позиция курсора для пропущенного не «съедается» при коммите.** |
| `SelectAiringItemsInterleaved` | ~1516–1578 | Round-robin по стрэнгам с `StrandStartOffset` от реального дня старта; `done[s]` при bound-отказе (стрэнг отдыхает, item не пропускается — важно для Fenster: пропуск должен отличаться от «strand rests»). |
| `CommitTaken` | ~1495–1506 | Коммит взятых items после конфликт-проверки (flat: `state.Commit(n)`, interleaved: per-strand counts). Если Fenster пропускает items без коммита — курсор естественно не двигается, титул предложится заново. |
| `BoundStarts` / `BoundaryAfter` | ~1630–1650 | Окно владения occurrence до следующего occurrence; soft vs hard bound. |
| `ShiftPastInvaders` | ~1241–1257 | Сдвиг начала за хвост уже идущей передачи. |

### Кольцевое состояние

| Тип | Строки | Поля/методы |
|-----|--------|-------------|
| `SchedulableItem` (record) | ~1061–1068 | `ContentType, ContentId, MediaItemId, MovieId, TVShowEpisodeId, Duration, StrandKey=""` — **нет Freigabe-поля; добавление `AgeRatingMinAge`/`AgeRating` здесь — центральная точка** |
| `StrandState` (class) | ~1078–1096 | `Key, Items, NextIndex, LastPlaced`; `PeekAt(offset)`, `Commit(count)`, `RewindTo(index)` |
| `ClipGroupState` | ~1101–1116 | `Group, StrandKey="clipgroup:{id}", Ring`; `IsTrailer` |
| `RuleState` | ~1118–1191 | `Rule, Items, Strands, ClipGroups, IsInterleaved, ClipGrace`; flat-фасад (`NextIndex/Commit/RewindTo` → `Strands[0]`); `SetItems` строит стрэнги (genre → `show:{id}`/`other`, hash-order `StrandOrderKey`) |
| `SummaryCounters` | ~1259–1279 | `SlotsCreated, FillerSlotsCreated, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds` + диагностические `Candidates, MaterialMissing, NoMediaItem, ProbePending, ProbeExhausted` — **новый счётчик для Fenster добавляется сюда** |
| `AccessContext` | ~1284–1296 | `Everything/FullySharedSourceIds/UnlockedShowIdsForAll/UnlockedCollectionIdsForAll`; `Allows(sourceId, showId, collectionId)` — Freigabe-фильтрация (отдельная от Fenster) |

### Экспансия материала (`ExpandMaterialAsync`-семейство)

Все `Expand…` возвращают `List<SchedulableItem>` и таллят skip-причины в `SummaryCounters` — именно здесь Freigabe должна подгружаться:

| Метод | Строки | Freigabe-источник |
|-------|--------|-------------------|
| `ExpandMaterialAsync` | ~1799–1826 | Диспатч по `MaterialType`; при пустом результате `OrphanedRules++` |
| `ExpandShowAsync` | ~1896–1918 | Эпизоды `e.Id, e.TVShowSeasonId, e.MediaSourceId` — **Freigabe на `TVShow` (нужно догружать)** |
| `ExpandMovieAsync` | ~1940–1952 | `Movie` целиком — `movie.AgeRatingMinAge` доступно |
| `ExpandMovieCollectionAsync` | ~1954–1974 | `Movies` по коллекции — доступно |
| `ExpandGenreAsync` | ~1985–2083 | Movies + episodes (episodes без Freigabe — `e.TVShowSeason.TVShowId` есть в проекции) |
| `ExpandPlaylistAsync` | ~2241–2312 | Movies dict + episodeShowIds — аналогично |
| `ExpandTrailersAsync` | ~2110–2153 | Trailer-links: `MovieId, MediaItemId, Duration…, MediaSourceId, MovieCollectionId` — MovieId есть → можно join'нуть `Movies.AgeRatingMinAge` при необходимости; сам трейлер Freigabe не несёт |
| `ExpandMediaSourceClipsAsync` / `ExpandClipGroupItemsAsync` → `FilterClipCandidatesAsync` | ~2185–2457 | Неклассифицированные видео-файлы — `IAgeRated` отсутствует |
| `TryMovieItem` / `ToItem` | ~1879–1894 / ~1923–1938 | Фабрики `SchedulableItem`: `Candidates`, `CountNoDuration`, `SkippedNoAccess` — точка внесения Freigabe в item |
| `ExpandTrailerStatesAsync` / `ExpandClipGroupStatesAsync` / `BuildClipGroupStates` | ~2087–2391 | Отложенная экспансия trailer-material и клип-групп |

### Прочее

| Метод | Строки | Описание |
|-------|--------|----------|
| `BuildOccurrences` | ~1373–1405 | Occurrence-старт по локальному времени `TimeZoneInfo.Local` — **источник локальной Sendezeit для Fenster** |
| `FillGaps` | ~1652–1743 | Филлер: `PeekAt(0)`/`PeekAt(offset)` + `Commit(take)` — филлер-путь обходит `SelectAiringItems`; Fenster-проверка для филлера отдельная (Annahme: та же Regelung) |
| `InsertClipFill`/`FillAiringSegments`/`NextClipItem` | ~2464–2692 | Раздача клипов по сегментам периода; `fits`-предикат геометрический — Fenster можно добавить в предикат |
| `RewindCursor`/`RewindRing` | ~839–888 | Откат курсора при displacement |
| `PersistCursors` | ~1763–1795 | Запись `LastPlaced` по стрэнгам |
| `LoadAccessContextAsync` | ~1307–1360 | Читает `GetProgramScheduleContentScopeAsync` (`AllContent` → `Everything`) |
| `LoadMovieAiringsAsync`/`BuildMovieAiringsMap` | ~2203–2239 | Карта airtimes по фильму для trailer-Vorlauf |
| `GroupAirings`, `ReconcileWindow`, `AiringReproducible(Flat/Interleaved)`, `FindItemIndex`, `ReproContext` | ~477–979 | Валидация воспроизводимости airings при reconcile — **важно**: Fenster-пропуск в выборке меняет «expected» последовательность; если Freigabe/Fenster учитывается в `SelectAiringItems`, проверка воспроизводимости должна быть согласована (иначе старые слоты станут невалидными массово) |
| `StrandOrderKey`, `SelectStrandCursor`, `StrandStartOffset`, `NextIndexFor`, `ClipMediaItemIds`, `CountNoDuration`, `GapSeconds`, `TailLimit`, `ChainLimit`, `PeriodStart`, `FindLeadMatchOffset`, `RequireAdminAsync` | вспомогательные | — |

## `ProgramSettingsService`
Файл: `VideoWebPlayer/Services/ProgramSettingsService.cs` (555 строк)

Образец Enum-настройки для Jugendschutz (`ProgramScheduleContentScope`):

| Метод | Строки | Описание |
|-------|--------|----------|
| `GetProgramScheduleContentScopeAsync` | 392–398 | `Enum.IsDefined` → значение, иначе `SharedOnly` (нормализация порчи) |
| `UpdateProgramScheduleContentScopeAsync` | 407–416 | Валидация `Enum.IsDefined` → `ArgumentException` |
| `GetProgramScheduleHorizonAsync` / `GetProgramScheduleIntervalAsync` | 345–362 | Числовые Getters с дефолтами (48h/15min) |
| `UpdateProgramScheduleSettingsAsync` | 371–383 | Валидация диапазонов |
| `GetHolidayFederalStateAsync` / `UpdateHolidayFederalStateAsync` | 317–338 | `HolidayFederalStates`-whitelist |
| `GetOrCreateSetupAsync` / `CreateSetupAsync` | 32–98 | Single-row `Setups` (UX_Setups_SingleRow, race-safe) |

## `TVSenderAdmin.razor`
Файл: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (2221 строка)

| Элемент | Строки | Описание |
|---------|--------|----------|
| Вкладка «Einstellungen», карта «Planung» | ~547–565 | `tvsender-horizon-hours`, `tvsender-interval-minutes`, `tvsender-content-scope`-select — **место для Fenster-настроек** |
| `contentScope`-поле + загрузка | 1004, 1115 | `ProgramScheduleContentScope`-паттерн связи UI↔service |
| `SaveChannelSettingsAsync` | ~1778–1799 | `UpdateHolidayFederalStateAsync` + `UpdateProgramScheduleSettingsAsync` + `UpdateProgramScheduleContentScopeAsync` + live-stream + standby → `ScheduleSignal.NotifyScheduleChanged()` |
| `ruleDiagnoses` + `GetRuleDiagnosesAsync` | 1040, 1365 | Diagnose state |
| `DiagnosisLabel` | ~1632–1657 | Строки диагноза: MaterialMissing → «verwaist»; иначе `SchedulableCount`/`NoMediaItem`/`ProbePending`/`ProbeExhausted`/`NoAccess` → **добавить Grund «kein altersgerechtes Material im Fenster»** |
| Редактор правил (`RuleEditModel`, `StartCreate`/`StartEdit`) | ~1801–1829+ | Поля правила — только если решится regelspezifisches Fenster |

## `AgeRatingNormalizer`
Файл: `VideoWebPlayer/Utils/AgeRatingNormalizer.cs` (102 строки)

| Элемент | Описание |
|---------|----------|
| `MpaaRatings` | `NC-17→18, PG-13→13, PG→8, R→17, G→0` |
| `ValidFskAges` | `0, 6, 12, 16, 18` |
| `CanonicalOptions` | `(Value, Label)`-список для UI — единственный источник ступеней |
| `Normalize(raw)` | → `AgeRatingResult(DisplayValue, MinAge)`; Länderpräfix `X:`/`Rated ` срезаются; неизвестное → trimmed raw + `MinAge=null` |
| `ExtractFromXml(xml)` | `<mpaa>` primär → `<certification>` fallback → `(Raw, Rating)` |

`AgeRatingResult` — record `(DisplayValue, MinAge)` (см. `VideoWebPlayer/Utils/` — используется `Movie/TVShow.LoadFromXml`, `MediaSourceClassifier`, Backfill).

**Не существует:** Fenster-таблица (Freigabe → früheste Sendezeit), Enum режима неизвестной Freigabe, поле `AgeRatingMinAge` в `SchedulableItem`, Fenster-счётчики в `SummaryCounters`, Jugendschutz-Grund в `ProgramRuleDiagnosis`/`DiagnosisLabel`, методы в `ProgramSettingsService`.
