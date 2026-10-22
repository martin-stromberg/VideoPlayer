# Plan-Review: Maskottchen-Interstitials (N12, Schritt 18)

## Ergebnis

**Status:** Vollständig umgesetzt

## Prüfung der Planelemente

| Planelement | Umgesetzt | Nachweis |
|-------------|-----------|----------|
| `ProgramRuleMaterialType.MediaSourceClips = 6` | ja | `VideoWebPlayer/Data/ProgramRuleMaterialType.cs` |
| `ProgramCursorContentType.MediaItem = 4` | ja | `VideoWebPlayer/Data/ProgramCursorContentType.cs` |
| `ExpandMediaSourceClipsAsync` + Switch-Arm | ja | `ProgramScheduleService.cs` (~Z. 978, 1343-1392): Quell-Existenz → `MaterialMissing`; Video-Extension-Filter; Ausschluss verknüpfter Items; `Duration`/`AccessContext`-Prüfung; `SchedulableItem(…, MediaItemId, null, null, …)` |
| `GetAllowedMaterialTypes(Filler)` + Ausschluss aus Repeat/HolidaySpecial | ja | `ProgramRuleService.cs` Z. 19-20, 47 |
| `ValidateRuleInputAsync`-Existenzprüfung | ja | `ProgramRuleService.cs` Z. 283 (`MediaSources.AnyAsync`) |
| `MediaItem.Name`-Fallback Programmauskunft | ja | `ProgramGuideService.cs` (`LoadTitlesAsync`/`ToEntry`) |
| `MediaItem.Name`-Fallback Admin-Sendeplan | ja | `TVSenderAdmin.razor` (`ReloadScheduleAsync`/`SlotTitle`) |
| `#rule-material-clipsource`-Dropdown + Labels | ja | `TVSenderAdmin.razor` Z. 241-250, `OnClipSourceSelected`, `MaterialLabel` „Clip-Quelle" |
| Keine Migration/Backup-Änderung | ja | nur Enum-Werte; keine Schemaänderung |
| Drehbücher (6) | ja | `docs/projects/…/maskottchen/drehbuecher/01-06` |
| Generator + MP4s | ja | `generator/generate_clips.py`; 6 Clips in `clips/` (ffprobe: h264, 1280×720, 25 fps, AAC, 10–20 s) |
| Hilfe/Release-Notes/README | ja | `docs/help/tv-sender.md` §„Maskottchen-/Kennzeichen-Clips", `RELEASE_NOTES.md` (EN+DE), `README.md` |
| Tests (Service/Validierung/Guide/bUnit/E2E) | ja | 14 neue Tests — alle grün |

## Selbstkritische Nachprüfung der Designentscheidungen

- **Marker-Verzicht:** Verifiziert — `ProcessCollectionAsMovieAsync` verlangt zwingend eine
  `<movie>`-NFO; Dateien ohne NFO erzeugen nie `Movie`-Sätze (Klassifikationspfad geprüft,
  `inventory/logic.md`). Der Expand-Filter (`MovieMediaItems`/`TVShowEpisodeMediaItems` ohne
  Link) deckt zusätzlich den Fall „Quelle enthält doch eingeordnete Inhalte" ab. Ein
  `IsInterstitial`-Flag hätte sämtliche Bibliotheksabfragen erweitert — begründet verworfen.
- **Cursor-Rotation:** `ProgramCursorContentType.MediaItem` läuft über `NextIndexFor`/
  `PersistCursors` unverändert; Test `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns`
  belegt nahtlose Rotation über Läufe.
- **Restlücken:** Clips sind kurz (10–20 s) — kleine Restlücken werden wie bei allen Füllern
  toleriert; Horizont-Überlauf-Regel greift (`FillGaps`).
- **Nicht abgedeckte Fälle (bewusst):** Clips mit `.nfo` werden zu Filmen — Dokumentation
  weist ausdrücklich auf „keine .nfo" hin; ein Versehen des Anwenders ist reversibel
  (NFO löschen + Rescan).

## Offene Aufgaben

Keine.
