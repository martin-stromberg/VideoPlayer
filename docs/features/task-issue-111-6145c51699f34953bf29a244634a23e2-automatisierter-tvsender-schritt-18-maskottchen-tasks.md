# Tasks: Maskottchen-Interstitials als Füller (N12, Schritt 18)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Enums | `ProgramRuleMaterialType.MediaSourceClips = 6` hinzufügen | Offen | — |
| 2 | Enums | `ProgramCursorContentType.MediaItem = 4` hinzufügen | Offen | — |
| 3 | Schedule | `ExpandMediaSourceClipsAsync` + `ExpandMaterialAsync`-Arm in `ProgramScheduleService` | Offen | `ProgramScheduleServiceTests_ClipSource.*` |
| 4 | Regel-Service | `GetAllowedMaterialTypes(Filler)` + `AllMaterialTypesExceptTrailer`-Ausschluss | Offen | `GetAllowedMaterialTypes_Filler_IncludesClipSource`, `…_Repeat_ExcludesClipSource` |
| 5 | Regel-Service | `ValidateRuleInputAsync`-`exists`-Switch um `MediaSourceClips` | Offen | `CreateRule_ClipSource_RequiresExistingSource` |
| 6 | Programmauskunft | `MediaItem.Name`-Fallback in `ProgramGuideService.LoadTitlesAsync`/`ToEntry` | Offen | `GetNowAsync_ClipSlot_FallsBackToMediaItemName` |
| 7 | Admin-UI | `SlotTitle`/`ReloadScheduleAsync` MediaItem-Fallback | Offen | bUnit/Planansicht |
| 8 | Admin-UI | `#rule-material-clipsource`-Dropdown + `OnClipSourceSelected` + `MaterialLabel`/`MaterialKindLabel` | Offen | bUnit `MaterialPicker_ShowsClipSourceSelect_ForFiller`, E2E-Erweiterung |
| 9 | Tests | Neue Testklasse `ProgramScheduleServiceTests_ClipSource` (Expansion, Filter, Zugriff, Rotation, Missing) | Offen | — |
| 10 | Tests | Validierungs-/bUnit-Tests für neue Materialart | Offen | — |
| 11 | E2E | `Admin_CreateEditDeactivateDeleteRule` um Füller-Regel mit Clip-Quelle erweitern | Offen | — |
| 12 | Drehbücher | 6 Drehbuch-Markdowns unter `docs/projects/…/maskottchen/drehbuecher/` | Offen | — |
| 13 | Produktion | `generate_clips.py` (Pillow-Frames + WAV-Jingle + ffmpeg) | Offen | ffprobe-Verifikation |
| 14 | Produktion | 6 MP4-Clips unter `docs/projects/…/maskottchen/clips/` erzeugen | Offen | ffprobe-Ausgaben |
| 15 | Doku | `docs/help/tv-sender.md` (Maskottchen/Clip-Quelle), `docs/RELEASE_NOTES.md`, ggf. `README.md` | Offen | — |
