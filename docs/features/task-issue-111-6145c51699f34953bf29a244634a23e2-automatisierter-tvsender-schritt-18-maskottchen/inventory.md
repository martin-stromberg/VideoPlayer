# Bestandsaufnahme: Maskottchen-Interstitials als Füller (N12, Schritt 18)

Analyse des TV-Sender-/Sendeplan-Bereichs bezogen auf N12: kurze Maskottchen-/Kennzeichen-Clips
als Füllmaterial einplanen, ohne dass sie als reguläre Inhalte erscheinen, plus tatsächliche
Clip-Produktion außerhalb der App.

## Zusammenfassung

- **Füller-Mechanik existiert vollständig** (`ProgramRuleType.Filler` + `FillGaps` mit
  Ring-Rotation über `RuleState`/Cursor, Prioritätsreihenfolge, Lücken-Passung,
  Horizont-Überlauf). Eine neue Materialart lässt sich über den `ExpandMaterialAsync`-Switch
  einhängen.
- **Playlist-Material reicht nicht**: `ExpandPlaylistAsync` verarbeitet nur `Movie`- und
  `TVShowEpisode`-Einträge — Maskottchen-Clips müssten als Filme klassifiziert werden und
  würden in der Mediathek auftauchen (N12: sollen gerade nicht).
- **Kein Marker nötig**: Die Film-Einordnung (`ProcessCollectionAsMovieAsync`) erfordert zwingend
  eine `<datei>.nfo` mit `<movie>`-Wurzel — Videodateien ohne NFO bleiben rohe `MediaItem`s,
  unsichtbar in der Mediathek, werden aber vom `MediaDurationProbeService` geprobt
  (`VideoExtensionPredicate` wirkt auf `MediaItem`-Ebene). Der in N12 erwogene
  `IsTrailer`-analoge Interstitial-Marker entfällt damit (Begründung im Plan/Handover).
- **Minimal fehlende App-Funktion**: eine Materialart „uneingeordnete Clips einer Medienquelle"
  — `MaterialId` → `MediaSources.Id`, Expansion = Video-`MediaItem`s der Quell-Collections ohne
  Film-/Episoden-Verknüpfung, mit `Duration`, `AccessContext`-Freigabe. `ProgramSlot.MovieId`/
  `TVShowEpisodeId` sind bereits nullable; `LiveStreamMediaResolver` arbeitet auf `MediaItemId`
  → kein Schema-/Stream-Eingriff, **keine Migration, keine Backup-Änderung**.
- **Titel-Fallback fehlt**: Slots ohne `MovieId`/`TVShowEpisodeId` zeigen „Unbekannt" bzw.
  „(Inhalt nicht gefunden)" — `MediaItem.Name`-Fallback in `ProgramGuideService` und
  `TVSenderAdmin` ergänzen.
- **Umgebung für Clip-Produktion**: `ffmpeg`/`ffprobe` 9.0.2 (gyan.dev full build, drawtext/
  freetype, x264), Python 3.13 + Pillow 12.3, Schriften unter `C:\Windows\Fonts` — alles
  vorhanden, kein KI-Videozugang.
- Test-Ausgangszustand: 2110 grün / 1 dokumentierter Baseline-Flake (im Retry grün) /
  1 Alt-Skip — siehe [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
