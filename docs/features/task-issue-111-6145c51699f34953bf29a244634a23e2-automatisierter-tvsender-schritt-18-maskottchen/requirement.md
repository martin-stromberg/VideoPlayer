# Übersetzte Anforderung: Maskottchen-Interstitials als Füller (N12, Schritt 18)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N12
inkl. Erweiterung und Stand (2026-10-21): tatsächliche Clip-Produktion ist Teil des Schritts;
kein Zugang zu KI-Videogeneratoren → Werkzeug-Evaluierung gehört zum Umfang.

## Fachliche Zusammenfassung

Der automatisierte TV-Sender soll kurze Kennzeichen-/Interstitial-Clips mit einem eigenen
Sender-Maskottchen als Lückenfüller einplanen können. Dafür sind zwei Ergebnisse nötig:
(a) app-seitig eine Füller-Materialquelle, die kurze, **nicht als Film/Serie eingeordnete**
Videodateien (rohe `MediaItem`s einer Medienquelle) in den Sendeplan aufnimmt, ohne dass die
Clips als reguläre Inhalte in der Mediathek erscheinen; (b) projektseitig die Produktion von
3–6 fertigen MP4-Clips (ca. 10–30 s, deutsch) nach kleinen Drehbüchern, generiert mit den
tatsächlich verfügbaren Werkzeugen (ffmpeg/Pillow, prozedural), samt kurzer dokumentierter
Werkzeug-Evaluierung und Einbindungs-Anleitung.

## Betroffene Klassen und Komponenten

- Enums:
  - `ProgramRuleMaterialType` — neuer Materialtyp für „uneingeordnete Clips einer Medienquelle"
    (z. B. `MediaSourceClips`), nur für `ProgramRuleType.Filler` zulässig.
  - `ProgramCursorContentType` — neuer Wert für rohe `MediaItem`-Referenzen (Fortsetzungs-Cursor).
- Logikklassen / Services:
  - `ProgramScheduleService` — neue Expansion `ExpandMediaSourceClipsAsync` (Video-`MediaItem`s
    der Quell-Collections ohne `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Verknüpfung, mit
    `Duration` und `AccessContext`-Prüfung); Aufnahme in `ExpandMaterialAsync`.
  - `ProgramRuleService` — `GetAllowedMaterialTypes(Filler)` erweitern; Material-Existenzprüfung
    in `ValidateRuleInputAsync` (gegen `MediaSources`).
  - `ProgramGuideService.LoadTitlesAsync`/`ToEntry` — Titel-Fallback auf `MediaItem.Name` für
    Slots ohne `MovieId`/`TVShowEpisodeId`.
  - `TVSenderAdmin.razor` — Materialauswahl um Quell-Dropdown erweitern;
    `ReloadScheduleAsync`/`SlotTitle` um `MediaItem`-Titel-Fallback; `MaterialLabel`.
- Datenmodell: **keine** neuen Tabellen/Spalten (Enum-Werte sind Ints; `ProgramSlot.MovieId`/
  `TVShowEpisodeId` sind bereits nullable). Kein EF-Migrationsbedarf.
- Projekt-Artefakte (kein App-Code): Drehbuch-Markdowns, Clip-Generator-Skript, produzierte
  MP4s unter `docs/projects/.../maskottchen/`.
- Tests: `ProgramScheduleServiceTests_*` (neue Klasse für Clip-Material: Expansion, Laufzeit-/
  Zugriffsfilter, Cursor-Rotation, Diagnose), `ProgramRuleServiceTests_*` (Validierung),
  `ProgramGuideService`-Titeltest, bUnit `TVSenderAdminTests` (Materialauswahl).

## Implementierungsansatz

- Bestehende Erweiterungspunkte: `ExpandMaterialAsync`-Switch, `GetAllowedMaterialTypes`,
  `ValidateRuleInputAsync`-Switch, `FillGaps`-Generik (rotiert automatisch über `RuleState`),
  Cursor-Verwaltung (`NextIndexFor`/`PersistCursors`).
- Die Laufzeitermittlung (`MediaDurationProbeService`) arbeitet auf `MediaItem`-Ebene — Clips
  ohne NFO-Einordnung werden bereits geprobt; kein Eingriff nötig.
- Die Sichtbarkeitsfrage löst sich ohne Marker: Nur Dateien mit `.nfo` werden zu `Movie`
  eingeordnet (`ProcessCollectionAsMovieAsync`); Clips ohne NFO bleiben rohe `MediaItem`s und
  tauchen nicht in der Mediathek auf. Die Material-Expansion filtert zusätzlich auf
  „keine Film-/Episoden-Verknüpfung", damit eingeordnete Inhalte nicht doppelt planbar sind.
- Clip-Produktion: Python (Pillow) rendert Frames als `rawvideo` an `ffmpeg` (x264/AAC);
  einfache Sinus-Jingles via Python-WAV. Generator + Drehbücher + MP4s liegen versioniert
  im Projektverzeichnis; Einbindung über dedizierte lokale Medienquelle + Füller-Regel in
  `docs/help/tv-sender.md` dokumentiert.

## Konfiguration

Keine neue Konfiguration. Ordnerkonvention wird nicht benötigt (kein Klassifikations-Eingriff);
die Auswahl erfolgt ausschließlich über die Medienquellen-Referenz der Füller-Regel.

## Offene Fragen

Keine — der Auftrag trifft die offenen Entscheidungen vorab:
- Werkzeugwahl ist Teil des Schritts (Evaluierung freier KI-Optionen vs. ffmpeg; Entscheidung
  dokumentieren, kein KI-Zugang vorhanden).
- Ob ein Interstitial-Marker nötig ist, wird im Schritt entschieden — Ergebnis der Analyse:
  nicht nötig (siehe Implementierungsansatz), Begründung im Handover.
