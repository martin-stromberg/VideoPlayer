# Umsetzungsplan: Maskottchen-Interstitials als Füller (N12, Schritt 18)

## Übersicht

Zwei Teile: (1) App-seitig eine neue Füller-Materialart „Clip-Quelle", die uneingeordnete
Videodateien einer Medienquelle (Maskottchen-/Kennzeichen-Clips) lückenfüllend einplant —
ohne Marker, ohne Schemaänderung; (2) Produktion von 6 echten MP4-Interstitial-Clips
(Maskottchen „Funki, das Glühbirnchen") nach eigenen Drehbüchern, prozedural generiert mit
Python/Pillow + ffmpeg, abgelegt im Projektverzeichnis inkl. Einbindungs-Anleitung.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| App-Einbindung der Clips | Neue `ProgramRuleMaterialType.MediaSourceClips` (`MaterialId` → `MediaSources.Id`) statt Bestands-Playlist-Material | Playlist-/Film-/Genre-Material setzt klassifizierte `Movie`-/`TVShowEpisode`-Einträge voraus — Clips müssten als Filme auftauchen (N12: ausdrücklich nicht). Eine Quellen-Referenz deckt „eigene Quelle ohne Serien/Film-Einordnung" exakt ab; die Quell-Auswahl ist im Admin-Dropdown klein und benannt. |
| Verbergen aus der Mediathek | Kein `IsTrailer`-analoger Interstitial-Marker — Clips ohne `.nfo` werden nie klassifiziert | `ProcessCollectionAsMovieAsync` erzeugt `Movie`-Sätze nur mit `<datei>.nfo` (`<movie>`-Wurzel). Ein Movie-Flag müsste in sämtlichen Bibliotheks-Abfragen gefiltert werden — breiter und teurer. Die Expansion filtert zusätzlich Items mit Film-/Episoden-Verknüpfung heraus, sodass bewusst eingeordnete Dateien nicht doppelt planbar sind. |
| Material-Granularität | Medienquelle (nicht einzelne `MediaCollection`) | Quell-Dropdown ist begrenzt/benannt (UI-Muster: Playlist-`<select>`); dedizierte Clip-Quelle ist ohnehin der dokumentierte Pfad; Zugriffsprüfung `AccessContext.Allows(sourceId, …)` liegt auf Quell-Ebene. |
| Expansionsemantik | Alle Video-`MediaItem`s der Quell-Collections **ohne** `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Verknüpfung, mit `Duration`, für alle Nicht-Admins freigegeben | „Uneingeordnete Clips" ist die präzise fachliche Menge; verhindert, dass regulär eingeordnete Filme derselben Quelle doppelt planbar werden. |
| Zulässige Regelarten | Nur `ProgramRuleType.Filler` | Wie `Trailer` reines Füller-Material; `AllMaterialTypesExceptTrailer` wird zu „außer Trailer und Clip-Quelle" (Repeat/HolidaySpecial bekämen sonst automatisch die Auswahl). |
| Titel in Programmauskunft/Admin | `MediaItem.Name`-Fallback wenn `MovieId`/`TVShowEpisodeId` leer | Slots tragen nur `MediaItemId`; sonst „Unbekannt". |
| Clip-Produktion | Prozedural: Python+Pillow rendert Frames (RGBA-Rawvideo via stdin-Pipe) → ffmpeg → H.264/AAC-MP4; Sinus-Jingle als WAV im Skript | Kein KI-Videozugang (laut Stand 2026-10-21); freie Online-Generatoren erfordern Registrierung/Wasserzeichen/Upload fremder Assets; lokale Video-Diffusion (ComfyUI o. ä.) ist ohne GPU/Basismodelle nicht verfügbar und würde GB-Downloads erfordern. ffmpeg+Pillow ist sofort verfügbar, reproduzierbar, versionierbar und ausreichend für Motion-Graphics-Qualität. Evaluierung im Handover dokumentiert. |
| Schema/Backup | Keine Migration, keine `OptionalRestore`-Änderung | Nur neue Enum-Werte (Int-Spalten existieren); `ProgramSlot` trägt nullable `MovieId`/`TVShowEpisodeId` bereits. |

## Programmabläufe

### Füller-Regel mit Clip-Quelle anlegen (Admin)

1. Admin öffnet `/admin/tv-sender`, Tab „Regeln", legt Regel an, wählt Regelart `Filler`.
2. Im `#rule-material-picker` erscheint zusätzlich ein Medienquellen-`<select>`
   (`#rule-material-clipsource`) — sichtbar nur, wenn `MediaSourceClips` erlaubt ist.
3. Auswahl setzt `MaterialType = MediaSourceClips`, `MaterialId = <SourceId>`,
   `MaterialDisplayName = "Clip-Quelle: <Name>"`.
4. `SaveRuleAsync` → `ProgramRuleService` → `ValidateRuleInputAsync`: Zulässigkeit für
   `Filler` + Existenz der Quelle (`MediaSources.AnyAsync`); dann `IProgramScheduleSignal`.

Beteiligte Klassen/Komponenten: `TVSenderAdmin`, `ProgramRuleService`,
`ProgramRuleMaterialType`, `IProgramScheduleSignal`.

### Sendeplan füllt Lücken mit Clips

1. `EnsureCoverageAsync`/`ReconcileAsync`/`GetRuleDiagnosesAsync` rufen `ExpandMaterialAsync`.
2. `MediaSourceClips` → `ExpandMediaSourceClipsAsync`: Existenz der Quelle prüfen
   (`MaterialMissing`), Video-`MediaItem`s der Quell-Collections laden, Items mit
   `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Verknüpfung und ohne `Duration` verwerfen
   (`CountNoDuration`), `AccessContext.Allows(sourceId)` anwenden (`SkippedNoAccess`),
   nach `Id` sortiert als `SchedulableItem(ProgramCursorContentType.MediaItem, …)` liefern.
3. `FillGaps` rotiert über `RuleState` (Cursor-Fortsetzung via `ProgramCursorContentType.MediaItem`);
   Slots entstehen mit `MediaItemId`, `MovieId`/`TVShowEpisodeId` = `null`, `RuleType = Filler`.
4. Ausstrahlung: `LiveStreamMediaResolver` löst `MediaItemId` unverändert auf;
   `ProgramGuideService.ToEntry` zeigt `MediaItem.Name` als Titel-Fallback; Admin-Sendeplan
   (`SlotTitle`) ebenso.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `ProgramRuleCursor`,
`ProgramSlot`, `ProgramGuideService`, `TVSenderAdmin`, `LiveTv.razor` (konsumiert DTO,
unverändert).

### Clip-Produktion (außerhalb der App)

1. Drehbücher als Markdown unter `docs/projects/…/maskottchen/drehbuecher/` (6 Stück).
2. `docs/projects/…/maskottchen/generator/generate_clips.py` zeichnet Frames via Pillow
   (1280×720, 25 fps, RGBA → ffmpeg-stdin `rawvideo`), synthetisiert WAV-Jingles (Sinus,
   `wave`-Modul), ruft `ffmpeg` (`libx264`, `yuv420p`, `aac`) auf → MP4s unter `…/clips/`.
3. `ffprobe`-Verifikation (Dauer, Streams, Auflösung) direkt im Generator-Protokoll.
4. `docs/help/tv-sender.md`: Abschnitt „Maskottchen-Clips (Interstitials)" — Clips in einen
   Ordner legen, dedizierte lokale Quelle „Maskottchen" einbinden, für alle Benutzer teilen,
   Füller-Regel mit Material „Clip-Quelle" anlegen; Hinweis: keine `.nfo` zu den Clips, damit
   sie nicht als Filme eingeordnet werden.

## Neue Klassen

Keine neuen Klassen — nur neue Enum-Werte und eine private Expansionsmethode.

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `generate_clips.py` | Skript (Repo-Artefakt) | Prozedurale Clip-Erzeugung (Pillow-Frames + WAV + ffmpeg-Aufruf) |

## Änderungen an bestehenden Klassen

### `ProgramRuleMaterialType` (Enum)

- **Neue Werte:** `MediaSourceClips = 6` — uneingeordnete Video-`MediaItem`s einer Medienquelle
  (`MaterialId` → `MediaSources.Id`); Füller-Material.

### `ProgramCursorContentType` (Enum)

- **Neue Werte:** `MediaItem = 4` — rohes `MediaItem` einer Clip-Quellen-Regel;
  `LastContentId` = `MediaItem.Id`.

### `ProgramScheduleService` (Service)

- **Geänderte Methoden:** `ExpandMaterialAsync` — neuer Switch-Arm
  `MediaSourceClips => ExpandMediaSourceClipsAsync(rule.MaterialId, access, summary, ct)`.
- **Neue Methoden:** `ExpandMediaSourceClipsAsync(long sourceId, AccessContext access,
  SummaryCounters summary, CancellationToken ct)` — Expansion uneingeordneter Video-Items
  (siehe Ablauf 2), Rückgabe `List<SchedulableItem>`.

### `ProgramRuleService` (Service)

- **Geänderte Methoden:**
  - `GetAllowedMaterialTypes` — `Filler` erhält `MediaSourceClips` zusätzlich;
    `AllMaterialTypesExceptTrailer` schließt `MediaSourceClips` mit aus (Kommentar anpassen).
  - `ValidateRuleInputAsync` — `exists`-Switch um
    `MediaSourceClips => await _db.MediaSources.AnyAsync(m => m.Id == input.MaterialId, ct)`.

### `ProgramGuideService` (Service)

- **Geänderte Methoden:** `LoadTitlesAsync` — drittes Lookup `MediaItems` für Slots ohne
  `MovieId`/`TVShowEpisodeId` (`Id` → `Name`); `ToEntry` — Titel-Fallback auf den
  `MediaItem`-Namen, `MediaType`/`MediaId` bleiben `null`.
- **Geänderte interne Typen:** `TitleLookup` erhält `Items`-Dictionary.

### `TVSenderAdmin` (Razor-Komponente)

- **Neue Elemente:** `#rule-material-clipsource` (`<select>` aller `MediaSources`, nur wenn
  `MediaSourceClips` zulässig), Handler `OnClipSourceSelected`; Feld `clipSourceOptions`
  (befüllt in `OnInitializedAsync` aus `DbContext.MediaSources`, Muster `playlistOptions`).
- **Geänderte Methoden:** `MaterialLabel` (Switch um `MediaSourceClips` → Quellname),
  `MaterialKindLabel` („Clip-Quelle"), `ReloadScheduleAsync` (zusätzliches
  `MediaItem`-Namens-Lookup), `SlotTitle` (Fallback auf `MediaItem.Name`).

## Datenbankmigrationen

Keine. (`MaterialType`/`LastContentType` sind Int-Spalten; `ProgramSlot.MovieId`/
`TVShowEpisodeId` bereits nullable; Backup unverändert.)

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramRuleInput.MaterialType == MediaSourceClips` | nur bei `RuleType.Filler` zulässig | `ArgumentException` (bestehender Zulässigkeits-Check) |
| `ProgramRuleInput.MaterialId` bei `MediaSourceClips` | `MediaSources`-Zeile muss existieren | `KeyNotFoundException` (bestehender Existenz-Check) |
| `GenreIds`/`MediaSourceIds` bei `MediaSourceClips` | leer (bestehende Sammel-Validierung greift) | `ArgumentException` |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Regel-Diagnose:** `GetRuleDiagnosesAsync` läuft über denselben `ExpandMaterialAsync` —
  die neue Materialart erhält Diagnose-Zähler automatisch; `MaterialLabel` in der UI muss den
  neuen Typ auflösen (sonst „Material nicht gefunden"-Anzeige).
- **`AllMaterialTypesExceptTrailer`:** Wird `MediaSourceClips` nicht ausgeschlossen, wäre es
  für `Repeat`/`HolidaySpecial` wählbar — wird explizit ausgeschlossen.
- **Trailer-Airings-Map:** `BuildMovieAiringsMap` filtert auf `MovieId != null` — Clip-Slots
  beeinflussen die Trailer-Vorlauf-Logik nicht.
- **Probe-Fortschrittskarte:** Uneingeordnete Clip-Items zählen wie alle Video-Items in die
  Laufzeitermittlung — gewollt (ohne `Duration` sind sie nicht planbar).
- **Backup-Restore:** `ProgramRule.MaterialType` wird numerisch restauriert; ein Restore in
  einen alten Stand ohne Enum-Wert wäre lediglich ein unbekannter Wert (keine Migration) —
  bestehendes Verhalten wie bei allen Enum-Erweiterungen.

## Umsetzungsreihenfolge

1. **Enum-Werte `MediaSourceClips` + `ProgramCursorContentType.MediaItem`**
   - Voraussetzungen: keine.
2. **`ProgramScheduleService.ExpandMediaSourceClipsAsync` + `ExpandMaterialAsync`-Arm**
   - Voraussetzungen: Schritt 1.
3. **`ProgramRuleService`: `GetAllowedMaterialTypes` + `ValidateRuleInputAsync`**
   - Voraussetzungen: Schritt 1.
4. **Titel-Fallback `ProgramGuideService` + `TVSenderAdmin.SlotTitle`/`ReloadScheduleAsync`**
   - Voraussetzungen: keine (unabhängig).
5. **`TVSenderAdmin`: Clip-Quellen-Dropdown + Labels**
   - Voraussetzungen: Schritt 3 (Allowed-Material-Typen).
6. **Tests (Unit/Service + bUnit + E2E-Erweiterung)**
   - Voraussetzungen: Schritte 2–5; `ProgramScheduleTestDb` existiert.
7. **Drehbücher + Generator + Clip-Produktion**
   - Voraussetzungen: Python 3.13 + Pillow + ffmpeg im PATH (vorhanden).
8. **Doku: `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`, ggf. `README.md`**
   - Voraussetzungen: Schritte 5 + 7.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `FillerRule_ClipSource_FillsGaps` | `ProgramScheduleServiceTests_ClipSource` | Füller-Regel mit Quell-Material füllt Lücke mit uneingeordneten Items (MediaItemId gesetzt, MovieId null) |
| `ClipSource_ExcludesClassifiedItems` | dito | Items mit `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Link werden nicht expandiert |
| `ClipSource_SkipsItemsWithoutDuration` | dito | Items ohne `Duration` (Probe ausstehend/erschöpft) fallen raus; `SkippedNoDuration` zählt |
| `ClipSource_RequiresSharedSource` | dito | Quelle nicht für alle Nicht-Admins geteilt → `SkippedNoAccess`, keine Slots |
| `ClipSource_RotationContinuesAcrossRuns` | dito | Cursor (`ProgramCursorContentType.MediaItem`) setzt Ring über zwei Läufe fort |
| `ClipSource_MissingSource_MaterialMissing` | dito | gelöschte/unbekannte `MaterialId` → `MaterialMissing`/`OrphanedRules`, kein Slot |
| `GetAllowedMaterialTypes_Filler_IncludesClipSource` + `Repeat_ExcludesClipSource` | `ProgramRuleServiceTests_Validation` | Zulässigkeitsmatrix |
| `CreateRule_ClipSource_RequiresExistingSource` | dito | `KeyNotFoundException` bei unbekannter Quell-Id |
| `GetNowAsync_ClipSlot_FallsBackToMediaItemName` | `ProgramGuideServiceTests` | MediaItem-Name statt „Unbekannt" |
| `MaterialPicker_ShowsClipSourceSelect_ForFiller` + Auswahl setzt Material | `TVSenderAdminTests` (bUnit) | Dropdown nur bei Filler; Auswahl → `MediaSourceClips` + Chip-Label |
| `CreateMediaItemAsync` (existiert) + `EnsureSourceAsync`/`GrantSourceAccessAsync` | `ProgramScheduleTestDb` | Seeds; ggf. `CreateCollectionItemAsync` für Item in Collection einer Quelle — vorhandene Helfer prüfen/verwenden |

### Betroffene bestehende Tests

Keine — die Änderungen sind additiv (neuer Enum-Wert, neuer Switch-Arm, zusätzliches UI-Element).
`TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` wird nur erweitert (siehe unten),
nicht geändert in bestehenden Assertions.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin legt Füller-Regel an, wählt „Clip-Quelle" im Material-Picker, speichert → Regel-Zeile zeigt Material-Label | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Erweiterung desselben App-Laufs: zweite Regel anlegen) | Admin kann die neue Materialart über die UI auswählen und speichern | Auswahl benannter Entität über UI — nur E2E belegt, dass Dropdown + Persistenz + Anzeige zusammenspielen |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Erweiterung: nach erster Regel eine Füller-Regel mit Clip-Quellen-Material anlegen (Chip-Label prüfen), ohne neue App-Instanz — E2E-Konsolidierung |

## Offene Punkte

Keine.
