# Enums (issue-271)

## `ProgramScheduleContentScope`
Файл: `VideoWebPlayer/Data/ProgramScheduleContentScope.cs`

| Значение | Смысл |
|----------|-------|
| `SharedOnly = 0` | Планируется только контент, открытый всей не-админ аудитории (restrictive default) |
| `AllContent = 1` | Планируется всё, что раскрывают правила, без проверки Freigabe |

**Образец** для будущего `ProgramScheduleUnknownRatingMode` (или подобного): хранится как `Setup`-колонка, читается/пишется `GetProgramScheduleContentScopeAsync`/`UpdateProgramScheduleContentScopeAsync` с `Enum.IsDefined`-валидацией, выбирается `<select>` в `TVSenderAdmin.razor`, учитывается в `OptionalRestoreColumns` (`Setups.ProgramScheduleContentScope`).

## `ProgramRuleType`
Файл: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Значение | Смысл |
|----------|-------|
| `SeriesFixedTime = 0` | Серия в фиксированное время |
| `MoviePrimeTime = 1` | Фильм в прайм-тайм |
| `GenreRotation = 2` | Жанровая ротация |
| `Repeat = 3` | Повтор |
| `HolidaySpecial = 4` | Праздничный слот |
| `Filler = 5` | Филлер (gap-padding; отдельный путь в `FillGaps`) |

## `ProgramRuleRecurrence`
Файл: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Значение | Смысл |
|----------|-------|
| `Daily = 0` | Ежедневно |
| `Weekly = 1` | Еженедельно (`DayOfWeek`) |
| `Once = 2` | Одноразово (`SingleDate`) |
| `Holiday = 3` | По праздникам (`GermanHolidayCalendar` + Bundesland) |
| `Weekday = 4` | Пн–Пт |
| `Weekend = 5` | Сб–Вс |

## `ProgramRuleMaterialType`
Файл: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Значение | Смысл | Freigabe-носитель |
|----------|-------|-------------------|
| `TVShow = 0` | Серия | `TVShow.AgeRatingMinAge` |
| `Movie = 1` | Фильм | `Movie.AgeRatingMinAge` |
| `MovieCollection = 2` | Коллекция фильмов | `Movie.AgeRatingMinAge` |
| `Genre = 3` | Жанр(ы) | `Movie`/`TVShow` |
| `Playlist = 4` | Плейлист | `Movie`/`TVShow` по элементам |
| `Trailer = 5` | Трейлеры ближайших фильмов | нет `IAgeRated` (связь `MovieId` есть) |
| `MediaSourceClips = 6` | Неклассифицированные клипы источника | нет `IAgeRated` |

## `ProgramCursorContentType`
Файл: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Значение | Смысл |
|----------|-------|
| `Movie = 0` | |
| `TVShowEpisode = 1` | |
| `PlaylistEntry = 2` | |
| `Trailer = 3` | |
| `MediaItem = 4` | |

## `ProgramSlotStatus`
Файл: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Значение | Смысл |
|----------|-------|
| `Scheduled = 0` | Активный слот плана |
| `Displaced = 1` | Выдвинутый reconcile'ом (история/аудит) |

## Связанные, но не правочасть Fenster
- `PlaylistSortMode` (`VideoWebPlayer/Data/PlaylistSortMode.cs`): `Manual`-сортировка плейлиста — к Fenster не относится.

## Не существует
- Enum режима поведения при неизвестной Freigabe (напр. `ProgramScheduleUnknownRatingMode`) — надо создавать по образцу `ProgramScheduleContentScope`.
