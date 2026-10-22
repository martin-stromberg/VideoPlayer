# Umsetzungsplan: Schritt 23 — Regel-Features N19–N21

## Übersicht

Drei Nachbesserungen am TV-Sender: (N19) Genre-Regeln füllen ihr Zeitfenster künftig
serien-übergreifend interleaved — je Serie ein Fortschritts-Strang mit eigenem persistierten
Cursor — statt eine Serie zu strippen; (N20) zwei neue Wiederholungsarten „Werktag" (Mo–Fr)
und „Wochenende" (Sa+So); (N21) der Regel-Editor wird vom eingebetteten Panel zum
Overlay-Dialog nach App-Konvention.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Strang-Modell | `RuleState` hält `List<StrandState>`; jeder Strang = eigener Ring (`Items` + `NextIndex` + `LastPlaced`). `RuleState.Items` bleibt als flache Sicht (Stränge konkateniert) für `FillGaps`/`FindItemIndex`. | Vorgabe der Anforderung: „je Serie eigener Fortschritts-Cursor". Flachsicht vermeidet Umbau der Füller-/Reconcile-Basispfade. |
| Strang-Bildung | Nur bei `MaterialType == Genre` und `RuleType != Filler` werden Items gruppiert: Episoden je TV-Serie (`StrandKey = "show:{TVShowId}"`), alle übrigen Inhalte (Filme) in einem gemeinsamen Strang (`"other"`). Strang-Reihenfolge = Reihenfolge des ersten Auftretens in der Expansion. ≤1 Strang → Flachmodus. | Serien-Interleaving ist die Kernanforderung; Filme sind Einzelinhalte und laufen als durchgängiger Strang in Reihenfolge weiter (kein täglicher Film-Loop). Anwendbarkeit über `MaterialType` (nicht `RuleType`) deckt auch Genre-Material unter Repeat/HolidaySpecial ab. |
| Fenster-Füllung | `SelectAiringItemsInterleaved`: Round-Robin über die Stränge in fester Reihenfolge, beginnend je Occurrence bei Strang 0. Pro Strang: überlanger Kopf wird konsumiert (wie heute, nur solange der Strang in dieser Ausstrahlung noch nichts beigesteuert hat; max. `Items.Count` Konsums), nach erstem Beitrag beendet ein nicht passendes Item den Strang für diese Ausstrahlung. | Stabile Position über Tage: Strang-Reihenfolge und Startpunkt sind fix; je Strang fortlaufende Episodenreihenfolge über den eigenen Cursor. |
| Cursor-Persistenz | `ProgramRuleCursor` erhält nullable Spalte `StrandKey`; Unique-Index wird `(ProgramRuleId, StrandKey)`. Flachmodus schreibt wie bisher `NULL`. | Minimaler Schema-Eingriff, abwärtskompatibel (alte Cursor-Zeilen gelten als Flach-Cursor), nullable Spalte braucht keinen Restore-Default. |
| Reconcile-Prüfung | `AiringReproducible` bekommt einen Interleaved-Pfad: Hypothesen über die Präfix-Positionszahl `p` (zyklische Strang-Zuordnung `q mod m`), Köpfe je Strang = erster Airing-Index − Strang-Präfixzahl, Occurrence-Start = `airing[0].Start − Σ Präfixdauern`; Verifikation per Simulation (gleiche Auswahlfunktion wie bei der Erzeugung). Stränge ohne Airing-Beitrag dürfen im Präfix nicht auftreten (konservativ → im Zweifel verdrängen + neu materialisieren). | Spiegelt die bisherige k-Hypothesen-Logik (`writtenOffset`-Semantik) auf Stränge. Fehlschlag ⇒ Verdrängen ist die sichere Richtung. |
| Editor-Overlay | `#rule-editor` als `admin-dialog` in `admin-dialog-overlay`, Klick auf den Hintergrund = Abbrechen (`CancelEdit`), `stopPropagation` im Dialog, eigne Breite via Modifier-Klasse (Editor ist deutlich größer als 720px-Dialogs). Block wird aus dem Tab-Pane ans Ende der Section verlegt (vor den bestehenden Dialogen). | Exakt die App-Konvention der Bestätigungsdialoge; Fokus/Background-Verhalten identisch. |
| „Lokalisierung" (N20) | UI ist durchgehend deutsch (kein ResX/IStringLocalizer); Beschriftungen in `TVSenderAdmin` ergänzt („Werktags (Mo–Fr)", „Wochenende (Sa+So)"), zweisprachige Pflichtdoku = `RELEASE_NOTES.md` (EN+DE). | Kein Lokalisierungs-Framework im Projekt — Anforderung wird über die bestehenden Label-Helper abgedeckt. |

## Programmabläufe

### Genre-Ausstrahlung mit Serien-Interleaving (N19)

1. `EnsureCoverageAsync`/`ReconcileAsync` bauen je Regel einen `RuleState` wie bisher; die
   Expansion (`ExpandGenreAsync`) liefert zusätzlich je Item den `StrandKey` (Episoden:
   `show:{ShowId}`, sonst `other`).
2. `RuleState` bildet die Stränge: Genre-Material + Nicht-Füller → Gruppierung nach
   `StrandKey` in Erstauftretens-Reihenfolge; sonst ein einzelner Strang (Flachmodus).
   Je Strang wird `NextIndex` aus der zugehörigen `ProgramRuleCursor`-Zeile
   (`StrandKey` = Strang-Schlüssel, `NULL` für Flachmodus) via `NextIndexFor` gesetzt.
3. `MaterializeOccurrence`/`MaterializeOccurrenceEvicting` rufen bei `IsInterleaved`
   `SelectAiringItemsInterleaved` auf: Round-Robin über die Stränge bis `wanted` Items
   gewählt oder `SlotDuration` erreicht; Fenstergrenze (`BoundaryAfter`) gilt unverändert.
   Ergebnis: Item-Liste + je Strang (Konsum-, Übernahme-Anzahl); Commit erst nach
   Konfliktfreiheit (atomar wie bisher).
4. `PersistCursors` schreibt je Strang mit `LastPlaced` eine Cursor-Zeile
   (`ProgramRuleId`, `StrandKey`).
5. `ReconcileAsync`: `AiringReproducible` wählt bei `IsInterleaved` den Strang-Pfad;
   `RewindCursor` spult je Strang auf den frühesten verdrängten Inhalt des Strangs zurück.
6. `ResetScheduleAsync` löscht unverändert alle Cursor der Regeln (Strang-Zeilen eingeschlossen).

Beteiligte Klassen: `ProgramScheduleService` (`RuleState`, `StrandState`, `SelectAiringItems`,
`MaterializeOccurrence*`, `PersistCursors`, `NextIndexFor`, `RewindCursor`,
`AiringReproducible`, `ExpandGenreAsync`), `ProgramRuleCursor`, `ProgramRuleCursorConfiguration`.

### Werktag-/Wochenende-Wiederholung (N20)

1. `ProgramRuleRecurrence` um `Weekday = 4`, `Weekend = 5` erweitern.
2. `BuildOccurrences`: `Weekday` ⇒ `day.DayOfWeek` ∈ Mo–Fr; `Weekend` ⇒ Sa/So.
3. `TVSenderAdmin`: Recurrence-Select um zwei Optionen ergänzen; `RecurrenceLabel` um zwei
   Fälle. `ProgramRuleService` braucht nur `Enum.IsDefined` (automatisch) — explizit keine
   Zusatzfelder nötig; `CreateRuleAsync`/`UpdateRuleAsync` setzen `DayOfWeek`/`SingleDate`
   nur bei Weekly/Once (bereits so).

Beteiligte: `ProgramRuleRecurrence`, `ProgramScheduleService.BuildOccurrences`,
`ProgramRuleService`, `TVSenderAdmin.razor`.

### Regel-Editor als Overlay (N21)

1. In `TVSenderAdmin.razor` den `@if (editRule != null)`-Block aus dem `tab-regeln`-Pane
   entfernen und — unverändert im Inhalt — als
   `<div class="admin-dialog-overlay" @onclick="CancelEdit"><div class="admin-dialog rule-editor-dialog" id="rule-editor" @onclick:stopPropagation>…` ans Ende der `section`
   (vor den Bestätigungsdialogen) setzen.
2. `app.css`: `.rule-editor-dialog { width: min(960px, 100%); }` (größeres Panel; max-height/
   Scrollen erbt `.admin-dialog`); `.rule-editor-card` entfällt.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `StrandState` (in `ProgramScheduleService`) | private class | Ring eines Serien-/Sammel-Strangs: `Key`, `Items`, `NextIndex`, `LastPlaced`, `PeekAt`, `Advance`. |

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service)

- **`SchedulableItem`:** neues Feld `StrandKey` (`string`, Default `""`; nur bei Genre-Expansion gesetzt).
- **`RuleState`:** neu `List<StrandState> Strands`, `bool IsInterleaved`, Umstellung auf Strang-Commit (`Commit(strand, consumed, taken)`); `Items`/`NextIndex`/`PeekAt`/`Commit`/`RewindTo`/`LastPlaced` bleiben für Flachmodus/Füller. Strang-Aufbau im Ctor (inkl. Cursor-Auflösung je Strang) — Signatur bekommt die Cursor-Zeilen der Regel (statt einer).
- **`SelectAiringItems` (neu, statisch):** Interleaved-Variante `SelectAiringItemsInterleaved(strands, rule, occurrenceStartUtc, softEndUtc)` → `(List<SchedulableItem> Selected, List<(int StrandIndex, int Consumed, int Taken)> PerStrand)`.
- **`MaterializeOccurrence`/`MaterializeOccurrenceEvicting`:** Auswahl interleaved bei `IsInterleaved`, Commit je Strang erst nach Konfliktprüfung.
- **`NextIndexFor`:** unveränderte Logik, aber je Strang mit dessen Cursor-Zeile aufgerufen.
- **Cursor-Maps:** `Dictionary<long, ProgramRuleCursor>` → Lookup per `(ProgramRuleId, StrandKey)` (z. B. `ILookup`/`Dictionary<(long,string?)>`) in beiden Läufen.
- **`PersistCursors`:** je Strang eine Zeile.
- **`RewindCursor`:** je Strang der früheste verdrängte Inhalt.
- **`AiringReproducible`:** Dispatch auf `AiringReproducibleInterleaved` wenn `state.IsInterleaved`.
- **`ExpandGenreAsync`:** `StrandKey` auf den erzeugten Items setzen.
- **`BuildOccurrences`:** `Weekday`/`Weekend`-Fälle.
- **`ExpandTrailerStatesAsync`:** Items-Ersetzung über `RuleState`-Methode, die Stränge neu aufbaut (Trailer = Füller → Flachmodus; defensiv generisch).

### `ProgramRule` (Datenmodell)

- Kommentar bei `Recurrence` um die neuen Arten ergänzen (kein Schema-Eingriff).

### `ProgramRuleCursor` (Datenmodell)

- **Neue Eigenschaft:** `StrandKey` (`string?`) — Serien-Strang-Schlüssel; `null` = bisheriger
  Gesamt-Cursor der Regel.

### `ProgramRuleCursorConfiguration` (EF-Konfiguration)

- `StrandKey` nullable mappen; Unique-Index von `(ProgramRuleId)` auf `(ProgramRuleId, StrandKey)` ändern.

### `TVSenderAdmin.razor` (Komponente)

- Recurrence-Select: Optionen „Werktags (Mo–Fr)" (`Weekday`), „Wochenende (Sa+So)" (`Weekend`).
- `RecurrenceLabel`: `Weekday` → „Werktags (Mo–Fr)", `Weekend` → „Wochenende (Sa+So)".
- Editor-Block → Overlay wie oben beschrieben.

### `wwwroot/app.css`

- `.rule-editor-dialog` (Breite); `.rule-editor-card` entfernen.

### `VideoWebPlayerBackupData` (Backup)

- `OptionalRestoreColumns` += `ProgramRuleCursors.StrandKey` (nullable → kein Default nötig).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramRuleCursorStrandKey` | `ProgramRuleCursors.+StrandKey (TEXT NULL)`, Index `IX_ProgramRuleCursors_ProgramRuleId` → `(ProgramRuleId, StrandKey)` unique | Strang-Cursor je Regel. Generierung per `dotnet ef migrations add` (Konfiguration Release, da `bin\Debug` durch die laufende Benutzer-App gesperrt ist); falls EF-Tool scheitert: Migration + Model-Snapshot manuell nach Muster `AddProgramRuleGenreAndSourceLinks`. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramRuleInput.Recurrence` | `Enum.IsDefined` deckt neue Werte; keine Zusatz-Pflichtfelder für `Weekday`/`Weekend` | — (Bestehende Validierung greift weiter) |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Cursor-Daten (Bestand):** Genre-Regeln mit bisherigem NULL-Cursor starten im Mehr-Strang-
  Fall neu je Strang (alter Cursor wird nicht gemappt) — akzeptabler einmaliger Drehsprung.
- **`AiringReproducible` (Interleaved):** bewusst konservativ; unklare Präfix-Fälle verdrängen
  und materialisieren neu statt invalide Airings zu behalten.
- **`FillGaps`:** Füller bleiben Flachmodus — kein Verhaltensdelta.
- **Tests, die Cursor-Einzeiligkeit erwarten** (`SingleAsync` auf `ProgramRuleCursors`):
  bleiben für Nicht-Genre-Regeln korrekt (Flachmodus → weiterhin eine Zeile).
- **E2E/bUnit:** Editor-Assertions auf Card/„kein Overlay" müssen invertiert werden.
- **`diagnosis`/Serien-Fortschritt-Logtexte:** keine Änderung.

## Umsetzungsreihenfolge

1. **Enum + Occurrences + UI-Auswahl + Labels (N20)**
   - Voraussetzungen: keine.
   - `ProgramRuleRecurrence`, `BuildOccurrences`, `TVSenderAdmin` (Select + `RecurrenceLabel`).
2. **`ProgramRuleCursor.StrandKey` + Konfiguration + Migration + Backup-Registrierung**
   - Voraussetzungen: keine.
   - Spalte, Index, `dotnet ef migrations add`, `OptionalRestoreColumns`.
3. **`SchedulableItem.StrandKey` + `ExpandGenreAsync`**
   - Voraussetzungen: 2 (damit Cursor-Schlüssel persistierbar sind).
4. **`RuleState`/`StrandState` + Cursor-Lookup je Strang + `PersistCursors`/`NextIndexFor`**
   - Voraussetzungen: 2, 3.
5. **`SelectAiringItemsInterleaved` + `MaterializeOccurrence`/`MaterializeOccurrenceEvicting`**
   - Voraussetzungen: 4.
6. **`AiringReproducible` (Interleaved) + `RewindCursor` je Strang**
   - Voraussetzungen: 5.
7. **Editor-Overlay (N21) + CSS**
   - Voraussetzungen: keine.
8. **Neue Tests (N19/N20/N21) + Anpassung betroffener Tests**
9. **Build (Debug via Scratch-OutDir + Release), volle Suite, Doku**

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` | `ProgramScheduleServiceTests_Genres` (oder neue `..._Interleaving`) | Genre-Regel, 2+ Serien, mehrere Tage: je Tag/Window steht jede Serie auf derselben Position (gleiche lokale Slot-Zeiten), Episoden je Serie fortlaufend (E1→E2), keine Doppelbelegung (kein Slot teilt sich Start-/End-Intervalle). |
| `EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow` | dto. | Pro Serie aufsteigende Episodennummern über die Tage hinweg. |
| `EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` | dto. | Zweiter Extend-Lauf setzt je Serie am eigenen Cursor fort (kein Neustart, kein Überspringen). |
| `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` | `ProgramScheduleServiceTests_Reconcile` oder neu | Idempotenz: Reconcile auf interleaved Plan → 0 verdrängt. |
| `Reconcile_GenreRule_Interleaved_DisplacedAirings_RewindPerShow` | dto. | Verdrängte Slots einer Serie spulen nur deren Strang zurück. |
| `EnsureCoverage_WeekdayRule_OnlyMondayToFriday` / `EnsureCoverage_WeekendRule_OnlySaturdaySunday` | `ProgramScheduleServiceTests_Generation` | Occurrence-Tage korrekt (Startdatum so gewählt, dass beide Wochentypen im Horizont liegen). |
| `ValidateRuleInput_*_WeekdayWeekend_NoDayOfWeekOrDateRequired` | `ProgramRuleServiceTests_Validation` | Anlage ohne `DayOfWeek`/`SingleDate` zulässig; `DayOfWeek`/`SingleDate` werden nicht persistiert. |
| `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests_*` (neu oder bestehende Datei erweitern) | Alt-Backup ohne `StrandKey` stellt wiederher, `StrandKey` bleibt `null`. |
| `RuleEditor_RendersAsOverlay` (Umbau von `RuleEditor_RendersAsCard_InsideRulesTab`) | `TVSenderAdminTests` | Editor = `.admin-dialog` in `.admin-dialog-overlay`; Klick auf Overlay schließt (Abbrechen); Inhalte (Feld `#rule-name`) vorhanden. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminTests.RuleEditor_RendersAsCard_InsideRulesTab` | Editor ist Overlay — Assertions invertieren (Overlay vorhanden, `Closest("#tab-regeln")` entfällt). |
| `TVSenderAdminE2ETests` (Regel-Anlage-Fluss ~Z. 178) | `Assert.Equal(0, … ".admin-dialog-overlay")` wird ungültig — auf „Overlay sichtbar" umstellen. |
| Genre-Tests mit >1 Serie und `episodeCount` > Serienzahl (falls vorhanden): Erwartete Reihenfolge prüfen — Interleaving ändert die Abfolge. | Verhaltensänderung N19. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Regel-Editor öffnet sich als Overlay über der Liste, lässt sich füllen/speichern und schließt | `TVSenderAdminE2ETests` (bestehender Regel-Anlage-Fluss, angepasst) | N21 | Benutzerfluss — Overlay-Schicht im echten Browser. |
| — | Serien-Interleaving / Werktag-Wochenende | — | N19/N20 | Kein eigener E2E nötig: rein serverseitige Planlogik ohne sichtbare Interaktion; Nachweis über SQLite-Integrationstests (occurrences/slots) ist der angemessene Funktionsnachweis; der Plan-Tab zeigt nur das Ergebnis an (kein geänderter UI-Fluss). |

## Offene Punkte

Keine.
