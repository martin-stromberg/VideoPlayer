# Code-Review: Maskottchen-Interstitials (N12, Schritt 18)

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Änderungen

### `ProgramScheduleService.ExpandMediaSourceClipsAsync`

- Existenzprüfung der Quelle → `MaterialMissing` (wie bei den übrigen Expansionen).
- Zwei `Contains`-Unterabfragen über die geladene Item-Id-Menge — je Quelle klein
  (Clip-Quellen sind dediziert); `AsNoTracking` für die Lesepfade.
- Filter: Video-Extension (`VideoFileExtensions.IsVideoFileName`), `Duration != null`
  (`CountNoDuration` für Diagnose-Konsistenz), `access.Allows(sourceId, null, null)` —
  entspricht exakt den Skip-Reason-Konventionen der anderen Expansionen.
- `SchedulableItem(ProgramCursorContentType.MediaItem, item.Id, item.Id, null, null, …)`:
  `MovieId`/`TVShowEpisodeId` `null` — `ProgramSlot` unterstützt das bereits; `FillGaps`
  benötigt keinen Sonderpfad (nur `Trailer` hat Vorlauf-Sonderlogik).
- Sortierung nach `Id` = stabiler Ring; Cursor setzt per `LastContentType`/`LastContentId` fort.

### `ProgramRuleService`

- `AllMaterialTypesExceptTrailer` schließt `MediaSourceClips` mit aus — Kommentar
  aktualisiert; `GetAllowedMaterialTypes(Filler)` listet es explizit;
  `ValidateRuleInputAsync` prüft `MediaSources.AnyAsync` (soft reference bleibt weich).

### `ProgramGuideService` / `TVSenderAdmin`

- Titel-Fallback nur für Slots ohne Inhaltsreferenz (`bareItemIds`-Teilmenge) — kein
  zusätzliches Query, wenn keine Clip-Slots vorhanden sind; `MediaType`/`MediaId` bleiben
  `null` (DTO-Vertrag bereits nullable).
- Dropdown re-rendert mit `selected`-Markierung beim Bearbeiten einer bestehenden Regel;
  `mediaSources` wird ohnehin in `OnInitializedAsync` geladen (Wiederverwendung, kein
  zusätzlicher Fetch).
- `SlotTitle`-Fallback: MediaItem-Name vor „(Inhalt nicht gefunden)".

### Tests

- 8 Service-Tests (`ProgramScheduleServiceTests_ClipSource`), 4 Validierungs-Tests
  (`ProgramRuleServiceTests_ClipSource`), 1 Guide-Test, 1 bUnit-Test,
  E2E-Erweiterung im konsolidierten `Admin_CreateEditDeactivateDeleteRule` (zweite Regel
  im selben App-Lauf — keine neue Instanz, keine neue E2E-Methode).
- Gegenprobe erwogen: ohne den Link-Filter würden eingeordnete Items planbar — Test
  `ExcludesClassifiedItems` fängt genau das.

### Risiken geprüft

- SQL-`Contains` auf lokaler Liste: überschaubar (Items einer Quelle); identisches Muster
  in `ExpandGenreAsync`.
- Enum-Erweiterung in gespeicherten `int`-Spalten: abwärtskompatibel; alte Stände lesen
  unbekannte Werte wie bei jeder Enum-Erweiterung (bestehendes Verhalten).
- Kein neuer DbContext-Lebenszyklus, keine neuen Events, kein Pipeline-Eingriff;
  `ReconcileAsync`/`GetRuleDiagnosesAsync` laufen über dieselbe `ExpandMaterialAsync`.

## Befunde

Keine.
