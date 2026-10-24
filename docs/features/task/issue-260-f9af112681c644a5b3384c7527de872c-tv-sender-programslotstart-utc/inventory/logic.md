# Logik und Lesewege von `ProgramSlot.Start`/`End`

## Schreibweg

### `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MaterializeOccurrence` | `private` | Schreibt `ProgramSlot`-Zeilen; `Start`/`End` aus `Interval`-Werten, die von `TimeZoneInfo.ConvertTimeToUtc` stammen → `Kind = Utc` |
| `GetRuleOccurrences` (Bereich ~Z. 1090–1114) | `private` | `day.ToDateTime(rule.TimeOfDay, DateTimeKind.Unspecified)` → `TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local)` |
| `GetScheduleAsync` | `public` | `AsNoTracking`-Query `s.Start >= fromUtc.UtcDateTime`, `OrderBy(s.Start)` — liefert die Liste für `TVSenderAdmin.razor` |
| Diverse (`Z. 76–345`) | — | Vergleiche `s.End > x`, `s.Start < y`, `MaxAsync(s => s.End)` — UTC-Arithmetik, Kind-agnostisch |

## Lesewege

### `TVSenderAdmin.razor` — fehlerbehaftet
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

| Stelle | Code | Problem |
|--------|------|---------|
| Z. 348 | `schedule.GroupBy(s => s.Start.ToLocalTime().Date)` | `ToLocalTime()` bei `Unspecified` = No-Op → falsche Tagesgruppierung außerhalb UTC |
| Z. 383 | `slot.Start.ToLocalTime().ToString("dd.MM. HH:mm")` / `slot.End.ToLocalTime().ToString("HH:mm")` | zeigt UTC-Rohwerte statt lokaler Zeit |
| Z. 1071–1081 | `IsRunning`, `ProgressPercent` — Vergleiche mit `DateTime.UtcNow` | unbetroffen (reine Vergleiche/Arithmetik) |
| Z. 990 | `schedule = await ProgramScheduleService.GetScheduleAsync(...)` | Ladequelle der Slots |

Weitere `ToLocalTime()`-Aufrufe in derselben Datei (Z. 531, 557, 594, 630) betreffen **andere** Entitäten (Probe-Fortschritt, `MediaItem.LastAttemptAt`, `MediaItem.DurationProbedAt`) — gleiches latentes Muster, außerhalb des Issue-Umfangs.

### `ProgramGuideService` — bereits korrekt (Workaround)
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `ToEntry` (Z. 135–190) | Mappt `ProgramSlot` auf `DtoProgramGuideEntry`; Z. 142–143 `new DateTimeOffset(DateTime.SpecifyKind(slot.Start, DateTimeKind.Utc))` — kompensiert das fehlende Kind manuell |
| `GetNowAsync`, `GetUpcomingAsync` | `AsNoTracking`-Queries auf `ProgramSlots`, reine UTC-Vergleiche |

### `LiveStreamCoordinator` / `LiveTv.razor` — korrekt
- `LiveStreamCoordinator` arbeitet auf `DtoProgramGuideEntry.StartUtc`/`EndUtc` (`DateTimeOffset`, Z. 104, 159) — über `ProgramGuideService` bereits UTC-klar.
- `LiveTv.razor` `FormatTime(DateTimeOffset utc)` → `utc.ToLocalTime()` (Z. 361) — `DateTimeOffset.ToLocalTime()` ist kind-sicher.

### `VideoWebPlayerBackupData` — unbetroffen
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

Export liest via `connection.CreateCommand()` + `DbDataReader` (Z. 371–399), nicht über EF-Materialisierung → ein EF-`ValueConverter` hat auf Backup/Restore **keinen** Einfluss. `ProgramSlots` steht in `OptionalRestoreTables` (Z. 41), `ProgramSlot.Status` in `OptionalRestoreColumns` (Z. 89, 136).

## Beobachtung außerhalb des Issue-Umfangs

Dasselbe `Unspecified`-Muster betrifft potenziell weitere UTC-Spalten anderer Entitäten, die per `ToLocalTime()` angezeigt werden (`MediaItem.DurationProbedAt`, `MediaItem.LastAttemptAt`, Backup-/Pairing-/Update-Zeitstempel). Nicht Teil dieser Anforderung — mögliche Folgeaufgabe.
