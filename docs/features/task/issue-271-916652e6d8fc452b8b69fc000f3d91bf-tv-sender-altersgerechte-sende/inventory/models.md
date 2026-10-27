# Данные: модели и связанные поля (issue-271)

Дата-снимок: ветка `task/issue-271-916652e6d8fc452b8b69fc000f3d91bf-tv-sender-altersgerechte-sende`, коммит `f8ae065`.

## `ProgramRule`
Файл: `VideoWebPlayer/Data/ProgramRule.cs`

| Свойство | Тип | Описание / назначение |
|----------|-----|-----------------------|
| `Id` | `long` | PK |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | FK на канал |
| `Name` | `string` (required, `NameMaxLength` = 255) | Имя правила |
| `RuleType` | `ProgramRuleType` | Тип слота (серия, прайм-тайм, жанр, повтор, праздничный, филлер) |
| `Recurrence` | `ProgramRuleRecurrence` | Периодичность |
| `DayOfWeek` | `DayOfWeek?` | Для `Weekly` |
| `SingleDate` | `DateOnly?` | Для `Once` |
| `TimeOfDay` | `TimeOnly` | Желаемое локальное время начала — именно оно переводится в UTC в `BuildOccurrences` (`TimeZoneInfo.Local`), тот же источник «локальной отправной точки», что нужен Jugendschutzfenster |
| `EpisodeCount` | `int?` | Эпизодов за выход (альтернатива `SlotDuration`) |
| `SlotDuration` | `TimeSpan?` | Длина периода вещания |
| `MaterialType` | `ProgramRuleMaterialType` | Тип материала |
| `MaterialId` | `long` | Мягкая ссылка на материал (без FK — «verwaist» обрабатывается диагностикой) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Выбор жанров для `Genre`-материала |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Фильтр медиаисточников для `Genre` |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Назначенные клип-группы |
| `PlaceholderGrace` | `TimeSpan` (default `DefaultPlaceholderGrace` = 15 мин) | Каренц — остаток периода уходит в клипы |
| `Priority` | `int` | Разрешение конфликтов (выше — сильнее) |
| `IsActive` | `bool` (default `true`) | Участвует в планировании |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Метки |

Полей с altersbezogener (Freigabe/Фенстер) логикой нет — новое поле потребует EF-миграции + `OptionalRestoreColumns` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`, строки ~51–112) + регрессионный тест со старым бэкапом.

## `Setup`
Файл: `VideoWebPlayer/Data/Setup.cs`

Релевантные поля (все читаются через `ProgramSettingsService` с GetOrCreate/нормализацией; образец для новых настроек — `ProgramScheduleContentScope`, строки 101–108):

| Свойство | Тип | Описание |
|----------|-----|----------|
| `TvSenderEnabled` | `bool` | Фича-флаг TV-канала |
| `HolidayFederalState` | `string?` | Бундесланд для праздничных правил |
| `ProgramScheduleHorizonHours` | `int` (default 48) | Горизонт планирования |
| `ProgramScheduleIntervalMinutes` | `int` (default 15) | Интервал воркера |
| `ProgramScheduleContentScope` | `ProgramScheduleContentScope` (default `SharedOnly`) | **Образец для Enum-настройки Jugendschutz**: enum-колонка + `Get…` (нормализация `Enum.IsDefined` → дефолт) + `Update…` (валидация `Enum.IsDefined`) + селект в `TVSenderAdmin.razor` |
| `LiveStreamSegmentLengthSeconds` | `int` | HLS-сегмент |
| `LiveStreamSegmentDirectory` | `string?` | Каталог сегментов |
| `LiveStreamSegmentRetentionMinutes` | `int` | Ретенция |
| `LiveStreamStandbyMediaItemId` | `long?` | Standby-клип (мягкая ссылка) |

Полей для Jugendschutzfenster (ранняя Sendezeit по ступени, режим неизвестной Freigabe) нет — новые колонки → миграция + `OptionalRestoreColumns` (Setups-поля перечислены там же).

## `Movie` / `TVShow` — `IAgeRated`
Файлы: `VideoWebPlayer/Data/Movie.cs` (строки 62–74), `VideoWebPlayer/Data/TVShow.cs` (48–60), интерфейс `VideoWebPlayer/Data/IAgeRated.cs`

| Свойство | Тип | Описание |
|----------|-----|----------|
| `AgeRating` | `string?` | Каноническое отображаемое значение («FSK 12», «PG-13»; при неизвестной ступени — trimmed raw) |
| `AgeRatingRaw` | `string?` | Неизменённый исходник («Rated PG-13», «DE:16») |
| `AgeRatingMinAge` | `int?` | **Сравнимое минимальное возрастное значение** — единственный поле, пригодное для Fenster-логики; `null` при неизвестной ступени |
| `AgeRatingClassifiedAt` | `DateTime?` | Маркер обработанности (backfill) |

`IAgeRated` реализован **только** на `Movie` и `TVShow`. `TVShowEpisode` Freigabe не несёт — наследует от `TVShow` (episodes в `ExpandShowAsync`/`ExpandGenreAsync` запрашиваются без Freigabe — для Fenster нужно подгружать `e.TVShowSeason.TVShow.AgeRatingMinAge` или `ShowId→TVShow` маппинг). `MediaItem`, `ProgramClipGroup`, Playlist-Entries, Trailer-Links (`MovieMediaItems`) и Standby-Clip `IAgeRated` не реализуют — для них поведение в Fenster определяется настройкой «неизвестная Freigabe».

`AgeRatingRaw` заполняется в `LoadFromXml` (NFO `<mpaa>`/`<certification>`) и опционально через TMDB; `IsManuallyEdited` защищает от перезаписи (см. `docs/help/medienquellen/business-rules.md`).

## `ProgramSlot`
Файл: `VideoWebPlayer/Data/ProgramSlot.cs`

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Id` | `long` | PK |
| `TVChannelId` | `long` | Канал |
| `Start` / `End` | `DateTime` | UTC-границы слота (Kind = UTC, см. `ProgramSlotTests_DateTimeKind`) |
| `MediaItemId` | `long` | Проигрываемый медиа-элемент (мягкая ссылка) |
| `MovieId` / `TVShowEpisodeId` | `long?` | Контент-ссылка (для cursor-fallback и trailer-airings map) |
| `ProgramRuleId` | `long?` | Породившая правило |
| `RuleType` | `ProgramRuleType` | Копия типа правила (filler-фильтрация при reconcile) |
| `Status` | `ProgramSlotStatus` | `Scheduled`/`Displaced` |
| `IsClipFill` | `bool` | Слот — клип-филл из clip-группы |
| `CreatedAt` | `DateTime` | |

Никаких Freigabe-полей — Fenster-логика применяется при выборе материала, не на слоте.

## `ProgramRuleCursor`
Файл: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

| Свойство | Тип | Описание |
|----------|-----|----------|
| `ProgramRuleId` | `long` | FK |
| `StrandKey` | `string?` | Стрэнг (`null` = flat, `show:{id}`/`other` = interleaved, `clipgroup:{id}` = клип-кольцо) |
| `LastContentType` | `ProgramCursorContentType` | Тип последнего размещённого контента |
| `LastContentId` | `long` | Его id |
| `UpdatedAt` | `DateTime` | Для детерминированного winner-пика `SelectStrandCursor` |

Важно для Fenster: курсор кольцевой — пропущенный из-за Fenster титул **не должен** продвигать курсор (сейчас `Commit`/`CommitTaken` происходит только для реально размещённых items — см. logic.md).
