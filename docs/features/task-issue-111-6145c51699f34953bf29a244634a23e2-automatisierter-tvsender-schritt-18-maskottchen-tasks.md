# Tasks: Maskottchen-Interstitials als Füller (N12, Schritt 18)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Enums | `ProgramRuleMaterialType.MediaSourceClips = 6` hinzufügen | Fertig | — |
| 2 | Enums | `ProgramCursorContentType.MediaItem = 4` hinzufügen | Fertig | — |
| 3 | Schedule | `ExpandMediaSourceClipsAsync` + `ExpandMaterialAsync`-Arm in `ProgramScheduleService` | Fertig | `ProgramScheduleServiceTests_ClipSource.*` |
| 4 | Regel-Service | `GetAllowedMaterialTypes(Filler)` + `AllMaterialTypesExceptTrailer`-Ausschluss | Fertig | `GetAllowedMaterialTypes_Filler_IncludesClipSource`, `…_Repeat_ExcludesClipSource` |
| 5 | Regel-Service | `ValidateRuleInputAsync`-`exists`-Switch um `MediaSourceClips` | Fertig | `CreateRule_ClipSource_RequiresExistingSource` |
| 6 | Programmauskunft | `MediaItem.Name`-Fallback in `ProgramGuideService.LoadTitlesAsync`/`ToEntry` | Fertig | `GetNowAsync_ClipSlot_FallsBackToMediaItemName` |
| 7 | Admin-UI | `SlotTitle`/`ReloadScheduleAsync` MediaItem-Fallback | Fertig | bUnit/Planansicht |
| 8 | Admin-UI | `#rule-material-clipsource`-Dropdown + `OnClipSourceSelected` + `MaterialLabel`/`MaterialKindLabel` | Fertig | bUnit `MaterialPicker_ShowsClipSourceSelect_ForFiller`, E2E-Erweiterung |
| 9 | Tests | Neue Testklasse `ProgramScheduleServiceTests_ClipSource` (Expansion, Filter, Zugriff, Rotation, Missing) | Fertig | — |
| 10 | Tests | Validierungs-/bUnit-Tests für neue Materialart | Fertig | — |
| 11 | E2E | `Admin_CreateEditDeactivateDeleteRule` um Füller-Regel mit Clip-Quelle erweitern | Fertig | — |
| 12 | Drehbücher | 6 Drehbuch-Markdowns unter `docs/projects/…/maskottchen/drehbuecher/` | Fertig | — |
| 13 | Produktion | `generate_clips.py` (Pillow-Frames + WAV-Jingle + ffmpeg) | Fertig | ffprobe-Verifikation |
| 14 | Produktion | 6 MP4-Clips unter `docs/projects/…/maskottchen/clips/` erzeugen | Fertig | ffprobe-Ausgaben |
| 15 | Doku | `docs/help/tv-sender.md` (Maskottchen/Clip-Quelle), `docs/RELEASE_NOTES.md`, ggf. `README.md` | Fertig | — |
