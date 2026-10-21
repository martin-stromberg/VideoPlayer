# Umsetzungsplan: Sendeplan bei Änderungen nachführen (Schritt 4)

## Übersicht

`ProgramScheduleService` erhält einen Re-Bewertungspfad `ReconcileAsync`, der den bestehenden Sendeplan gegen aktive Regeln und Medienbestand prüft: ungültige Sendeplätze werden als `Displaced` markiert (nicht gelöscht), Serien-Fortsetzungen per `ProgramRuleCursor`-Rücksetzung ohne Sprung wieder eingeplant und Lücken regelkonform/Füller neu gefüllt. Ausgelöst wird die Re-Bewertung über ein neues `IProgramScheduleSignal` (Regel-CRUD, Medien-Adds/Löschungen, Scan-Ende) sowie periodisch durch den `ProgramScheduleWorker` (Signal- oder Interval-Weckung, `PlaylistBackfillWorker`-Muster). In `TVSenderAdmin` zeigt eine neue Sendeplan-Karte die kommenden Sendeplätze inkl. Status „verdrängt".

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Re-Bewertungskern | Wiederverwendung der Generierungsmaschinerie: `ReconcileAsync` = Validierungssweep + Verdrängen + Materialisierungslauf ab `now` über `blocked = nur Scheduled-Slots`. Occurrences, atomare Ausstrahlungen, `FillGaps`, `PersistCursors` bleiben unverändert wiederverwendet. | Ein einziger Planungsalgorithmus statt zweier; Konsistenz mit Schritt-3-Semantik (Priorität, Füller, Cursor) fällt gratis mit. |
| Gültigkeitsprüfung (Validierungssweep) | **Ausstrahlungs-granular** für Nicht-Füller-Slots (zusammenhängende Slots gleicher `ProgramRuleId`), **slot-granular** für Füller. Eine Ausstrahlung ist gültig, wenn (a) erzeugende Regel existiert und aktiv ist, (b) an `airing.Start` ein Occurrence der Regel liegt und (c) die Regel ausgehend vom ersten Slot-Inhalt dieselbe Slot-Sequenz erzeugen würde (Simulation über aktuelle Expansion). Damit werden Dirty-Flags/Alt-Neu-Vergleiche überflüssig — der Sweep ist absturzsicher und erkennt auch Medien-, Freigabe- und Episoden-Änderungen. | Fängt jede planrelevante Regel- und Medienänderung ohne persistente Markierungen ab; neue Episoden, die sich in die Expand-Reihenfolge einschieben, machen die Ausstrahlung automatisch „ungültig" → Neuplanung. |
| Verdrängungsumfang | **Suffix der Regel-Zukunft**: Ab der ersten ungültigen/verdrängten Ausstrahlung einer Regel werden alle späteren `Scheduled`-Ausstrahlungen derselben Regel (Slots mit `End > now`) ebenfalls verdrängt; vergangene (`End <= now`) bleiben `Scheduled` (Historie). Füller-Slots nur individuell. | Der Cursor-basierte Sequenzkontext macht spätere Ausstrahlungen von früheren abhängig; Suffix-Verdrängung verhindert Doppelplanungen und Episodensprünge. |
| Cursor-Rücksetzung | Je Regel mit verdrängten Slots: frühesten verdrängten Slot-Inhalt in der aktuellen Expansion suchen (per `MediaItemId`, Fallback `ContentType`+`ContentId` über `MovieId`/`TVShowEpisodeId`); Cursor auf das Vorgänger-Element setzen (Wrap über Listenende). Nicht mehr expandierbarer Inhalt → nächsten verdrängten Slot versuchen; keiner auflösbar → Cursor bleibt. | Exakt die geforderte „Wiedereinplanung zur nächst passenden Gelegenheit desselben Regelkontexts ohne Sprung" — die nächste freie Ausstrahlung setzt mit den verdrängten Inhalten fort. |
| Prioritäts-Änderungen | **Eviction bei der Materialisierung**: Konfliktiert ein Occurrence mit gehaltenen Slots, wird verdrängt statt verworfen, wenn **alle** konfliktierenden gehaltenen Slots verlieren — Füller verlieren immer, Nicht-Füller bei niedrigerer `Priority` bzw. gleicher `Priority` + höherer Regel-`Id`. Verdrängung wie üblich regel-suffixweise. | Prioritätsänderungen werden ohne Dirty-Flag korrekt nachgezogen; deckt sich mit der Generierungs-Reihenfolge `Priority desc, Id asc`. |
| Verdrängte laufende Sendeplätze | `End > now` schließt laufende ein: ungültige laufende Slots werden `Displaced`; die Occurrence-Materialisierung mit Lookback kann den Fensterbereich neu belegen (neue Slots dürfen wie bisher vor `coverageStart` beginnen — Verdrängter + neuer Plan überlappen sich dort, Verdrängung bleibt sichtbar). | Anforderung: nur noch gültige laufende bleiben; ungültige werden markiert, nicht gelöscht. |
| Auslöser-Mechanik | Neues `IProgramScheduleSignal` (Singleton, koaleszierender 1-Slot-Channel + Scan-Suppression via `BeginScan()`), exakt nach `IPlaylistBackfillSignal`-Muster. `ProgramScheduleWorker` wartet `Task.WhenAny(Signal, Intervall)` und führt pro Lauf `ReconcileAsync` + `EnsureCoverageAsync` aus. | Bewährtes Muster; Entkopplung UI→Worker hält Schreibzugriffe hinter `IBackgroundProcessingGate` serialisiert; periodischer Lauf ist das Sicherheitsnetz für Pfade ohne Signal (Bulk-`ExecuteDeleteAsync`). |
| Trigger-Punkte | `ProgramRuleService` (optionale Signal-Injection): `CreateRuleAsync`, `UpdateRuleAsync`, `SetRuleActiveAsync`, `DeleteRuleAsync` → `NotifyScheduleChanged()`. `ApplicationDbContext` (optionale Signal-Injection wie `_backfillSignal`): im Marker-Hook neben `NotifyMarkersWritten()` ebenfalls `NotifyScheduleChanged()` (neue Episoden/Filme in Sammlungen) sowie nach `DeleteMediaSourceAsync`-Commit. `MediaSourceScanService`: Scan in `BeginScan()`-Scope hüllen. | Alle Anforderungs-Trigger ohne eigenen Marker-Tabellen-Apparat; Medien-Adds nutzen den vorhandenen Change-Tracker-Hook, Medien-Bulk-Löschungen den expliziten Aufruf + periodisches Netz. |
| Status-Modell | Enum `ProgramSlotStatus { Scheduled = 0, Displaced = 1 }`, `ProgramSlot.Status` (required, Default `Scheduled`). Bestehende Zeilen → `Scheduled` per Migration-Default. | Minimal, lesbar, backup-kompatibel (`OptionalRestoreColumns`). |
| Sendeplan-Anzeige | Neue Karte „Sendeplan" in `TVSenderAdmin`: nächste ~200 Slots ab `now − 2 h` mit Start/Ende (lokal), Inhaltstitel (Film-Name bzw. „Serie — S<season>E<num>: <Episodenname>"), Regelname („(gelöscht)"-Fallback) und Status-Chip „Geplant"/„Verdrängt". Daten via `IProgramScheduleService.GetScheduleAsync` (Entities), Titelauflösung im Razor per `DbContext` (bestehendes `MaterialLabel`-Muster). | Erfüllt „nachvollziehbar" mit minimalem UI-Umfang; Titelauflösung folgt etabliertem Muster der Seite. |

## Programmabläufe

### Re-Bewertung (`ReconcileAsync(utcNow, ct)`)

1. Kanal laden (`GetOrCreateChannelAsync`), alle Regeln des Kanals laden (aktive + inaktive — inaktive/gelöschte erzeugen keine Occurrences, dienen aber der Referenz), `horizonEnd = now + Horizon`, `AccessContext`, `federalState`, Cursors.
2. Alle `Scheduled`-Slots des Kanals mit `End > now − OccurrenceLookback` laden (Lookback, damit laufende Ausstrahlungen ihren Kopfslot enthalten), nach `Start` sortiert, zu Ausstrahlungen gruppieren: maximale Runs gleicher `ProgramRuleId` mit `Start == prev.End`.
3. Pro aktiver Regel Expansion wie bisher aufbauen (`ExpandMaterialAsync`, `RuleState`), Occurrences aller aktiven Nicht-Füller-Regeln über `[now − OccurrenceLookback, horizonEnd]` bauen (`BuildOccurrences`).
4. **Validierung** je Ausstrahlung mit mindestens einem Slot `End > now`: ungültig wenn Regel fehlt/inaktiv, kein Occurrence der Regel an `airing.Start`, oder die Inhalts-Simulation (ab Index des ersten Slot-Inhalts in der Expansion, `EpisodeCount`/`SlotDuration`-Semantik wie `MaterializeOccurrence`) nicht exakt die vorliegende Slot-Sequenz (`MediaItemId`+`Start`/`End` je Slot, gleiche Anzahl) ergibt. Füller-Slots (einzeln): ungültig bei fehlender/inaktiver Regel oder `MediaItemId` nicht in der Expansion der Regel.
5. **Verdrängen**: erste ungültige Ausstrahlung je Regel → sie und alle späteren Ausstrahlungen derselben Regel auf `Displaced` setzen (nur Slots `End > now`); ungültige Füller-Slots einzeln. `SlotsDisplaced` zählen.
6. **Cursor-Rücksetzung** je betroffener Regel (Designentscheidung oben).
7. **Neu-Materialisierung**: `blocked` = verbliebene `Scheduled`-Slots (`End > now`, `Start < horizonEnd`); Occurrences in Prioritätsreihenfolge durch `MaterializeOccurrence` laufen lassen — mit Eviction (Designentscheidung): Konfliktslots, die gegen die Occurrence verlieren, werden mitsamt Suffix ihrer Regel verdrängt, sonst `DroppedConflicts++`. Danach `FillGaps`, `PersistCursors`, `SaveChangesAsync`.
8. Rückgabe `ProgramScheduleReconcileSummary` (`SlotsDisplaced`, `SlotsCreated`, `FillerSlotsCreated`, `DroppedConflicts`, `RemainingGapSeconds`, `OrphanedRules`, `SkippedNoDuration`, `SkippedNoAccess`).

### Fortschreibung (`EnsureCoverageAsync` — unverändert bis auf Statusfilter)

- `lastEnd` und `blocked` berücksichtigen nur `Status == Scheduled` (verdrängte belegen keine Abdeckung).

### Worker-Lauf (`ProgramScheduleWorker`)

- Schleife: `Task.WhenAny(_signal.WaitAsync(ct), Task.Delay(intervall))` → Scope + Gate-Lease → `ReconcileAsync(now)` → `EnsureCoverageAsync(now)`. Signal feuert nach Regel-CRUD, Medien-Adds (Marker-Hook), Quellen-Löschung und Scan-Ende.

### Regel-CRUD (Beispiel `UpdateRuleAsync`)

1. Validierung/Admin wie bisher; nach `SaveChangesAsync` → `_scheduleSignal?.NotifyScheduleChanged()`.
2. Worker wacht, Re-Bewertung verdrängt ungültige Zukunft der Regel und plant neu.

### Medienbestand

- Neuer Film/neue Episode in Sammlung/Staffel: `SaveChanges`-Marker-Hook feuert zusätzlich `NotifyScheduleChanged()` → Re-Bewertung sieht neue Expansion (Simulation verdrängt ggf. Ausstrahlungen mit geänderter Reihenfolge, sonst unverändert).
- `DeleteMediaSourceAsync`: nach Commit `NotifyScheduleChanged()`; weitere Bulk-Pfade fängt der periodische Lauf.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramSlotStatus` | Enum (`VideoWebPlayer/Data/ProgramSlotStatus.cs`) | `Scheduled = 0`, `Displaced = 1` |
| `IProgramScheduleSignal` | Interface (`VideoWebPlayer/Services/ProgramScheduleSignal.cs`) | `NotifyScheduleChanged()`, `BeginScan() → IDisposable`, `WaitAsync(ct) → ValueTask` |
| `ProgramScheduleSignal` | Klasse (Singleton, gleiche Datei) | Koaleszierender Channel + Scan-Zähler (Analog `PlaylistBackfillSignal`) |
| `ProgramScheduleReconcileSummary` | readonly record struct (`IProgramScheduleService.cs`) | Rückgabe der Re-Bewertung (Zähler s. Ablauf) |
| `ProgramScheduleServiceTests_Reconcile` | Testklasse | Re-Bewertungs-Szenarien |
| `ProgramScheduleSignalTests` | Testklasse | Koaleszieren/Scan-Suppression |

## Änderungen an bestehenden Klassen

### `ProgramSlot` (Datenmodell)
- **Neue Eigenschaft:** `Status` (`ProgramSlotStatus`, Default `Scheduled`) — Verdrängungs-Kennzeichnung.

### `ProgramSlotConfiguration`
- `builder.Property(s => s.Status).IsRequired();`

### `IProgramScheduleService` / `ProgramScheduleService`
- **Neue Methoden:** `ReconcileAsync(DateTimeOffset utcNow, CancellationToken) → Task<ProgramScheduleReconcileSummary>` (Ablauf oben); `GetScheduleAsync(long channelId, DateTimeOffset fromUtc, int maxCount, CancellationToken) → Task<List<ProgramSlot>>` (Admin-Ansicht, `Start >= fromUtc`, sortiert, inkl. Verdrängter).
- **Geänderte Methoden:** `EnsureCoverageAsync` — `lastEnd`- und `blocked`-Queries auf `Status == ProgramSlotStatus.Scheduled` filtern; `MaterializeOccurrence` — optionaler Eviction-Modus (gehaltene Verlierer-Slots + Regel-Suffix verdrängen statt Ausstrahlung zu verwerfen); interne Hilfen: Ausstrahlungs-Gruppierung, Ausstrahlungs-Validierung/Simulation, `RewindCursor`, Occurrence→Slot-Abgleich.

### `ProgramScheduleWorker`
- **Geändert:** `ExecuteAsync` — `IProgramScheduleSignal` injizieren (optional im Ctor, wie `initialDelay`/`timeProvider`: `IProgramScheduleSignal? signal = null`); Schleife wartet `Task.WhenAny` Signal/Timer; pro Lauf `ReconcileAsync` vor `EnsureCoverageAsync`.

### `ProgramRuleService`
- **Ctor:** optional `IProgramScheduleSignal? scheduleSignal = null`.
- **Geänderte Methoden:** `CreateRuleAsync`, `UpdateRuleAsync`, `SetRuleActiveAsync`, `DeleteRuleAsync` — nach `SaveChangesAsync` `_scheduleSignal?.NotifyScheduleChanged()`.

### `ApplicationDbContext` (+ `ApplicationDbContext.PlaylistBackfillMarking`)
- **Ctor:** optional `IProgramScheduleSignal? programScheduleSignal = null` (hinter `backfillSignal`).
- **Geändert:** in beiden `SaveChanges`-Varianten neben jedem `_backfillSignal?.NotifyMarkersWritten()` zusätzlich `_programScheduleSignal?.NotifyScheduleChanged()`; in `DeleteMediaSourceAsync` nach erfolgreichem `transaction.CommitAsync` `_programScheduleSignal?.NotifyScheduleChanged()`.

### `MediaSourceScanService`
- **Geändert:** zusätzlich `_serviceProvider.GetService<IProgramScheduleSignal>()?.BeginScan()` um den Scan-Block (neben dem Backfill-Scope).

### `ServiceCollectionExtensions`
- `AddSingleton<ProgramScheduleSignal>()` + `AddSingleton<IProgramScheduleSignal>(sp => sp.GetRequiredService<ProgramScheduleSignal>())` (nach den PlaylistBackfill-Registrierungen).

### `TVSenderAdmin.razor`
- Neue Karte „Sendeplan": Tabelle kommender Slots (`GetScheduleAsync(channel.Id, now − 2 h, 200)`), Spalten Start/Ende (lokal), Inhalt, Regel, Status-Chip; Titelauflösung via `DbContext` (Movies/TVShowEpisodes→Show/Season); Laden in `OnInitializedAsync` + Reload nach Regel-Aktionen.

### `VideoWebPlayerBackupData`
- `OptionalRestoreColumns` += `$"{nameof(ApplicationDbContext.ProgramSlots)}.{nameof(ProgramSlot.Status)}"`.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung |
|----------------|----------------------------|--------------|
| `AddProgramSlotStatus` | `ProgramSlots.Status` (int, NOT NULL, default 0) | Verdrängungs-Status; Bestandszeilen = Scheduled. Via `dotnet ef migrations add` erzeugen, Snapshot prüfen. |

## Validierungsregeln

Keine — keine neuen Benutzereingaben (Sendeplan-Karte ist rein lesend, Admin-only über bestehendes Seiten-Gate + serverseitige `RequireAdminAsync`-Pfade).

## Konfigurationsänderungen

Keine — bestehende `Setup`-Werte (Horizont, Intervall, Bundesland) werden wiederverwendet.

## Seiteneffekte und Risiken

- **Bestehender Sendeplan:** `EnsureCoverageAsync` filtert künftig `Scheduled` — ohne die Migration/Status-Belegung gingen verdrängte Slots verloren oder blockierten Lücken weiter; Default-Migration sichert Bestandszeilen.
- **Zusatzlast:** `ReconcileAsync` expandiert wie die Generierung alle Regeln pro Lauf — gleiche Größenordnung, Intervall-gesteuert.
- **Over-Displacement:** Suffix-Verdrängung kann gültige Ausstrahlungen ersetzen (Inhalt identisch, neue Zeile) — bewusst zugunsten von Sequenz-Konsistenz; in Doku/Tests festhalten.
- **Bulk-Löschpfade ohne Signal:** fremde `ExecuteDelete`-Wege werden nur periodisch erkannt — als dokumentierte Grenze (Intervall-Latenz) akzeptiert.
- **Signal-Verlust bei Absturz** zwischen Commit und Worker-Lauf: durch zustandslosen Validierungssweep abgefangen — der nächste periodische Lauf findet dieselben Ungültigkeiten.

## Umsetzungsreihenfolge

1. **Datenmodell + Migration:** `ProgramSlotStatus`, `ProgramSlot.Status`, Konfiguration, `dotnet ef migrations add AddProgramSlotStatus`, `OptionalRestoreColumns`-Eintrag.
   - Voraussetzungen: keine.
2. **Signal:** `IProgramScheduleSignal`/`ProgramScheduleSignal` + DI-Registrierung.
   - Voraussetzungen: keine.
3. **Service-Erweiterung:** `ProgramScheduleReconcileSummary`, `ReconcileAsync` (Sweep/Verdrängen/Rewind/Eviction/Materialisierung/FillGaps), `Status`-Filter in `EnsureCoverageAsync`, `GetScheduleAsync`.
   - Voraussetzungen: Schritt 1 (Status-Feld).
4. **Trigger verdrahten:** `ProgramRuleService`-Signal, `ApplicationDbContext`-Signal (Marker-Hook + Quell-Löschung), `MediaSourceScanService.BeginScan`-Scope, `ProgramScheduleWorker`-Umbau.
   - Voraussetzungen: Schritte 2–3.
5. **Admin-UI:** Sendeplan-Karte in `TVSenderAdmin`.
   - Voraussetzungen: Schritt 3 (`GetScheduleAsync`).
6. **Tests** (s. unten) parallel/je Komponente.
   - Voraussetzungen: jeweilige Schritte.
7. **Alt-Backup-Regressionstest** für `ProgramSlots.Status`.
   - Voraussetzungen: Schritt 1.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `Reconcile_NoChanges_LeavesEverythingScheduled` | `ProgramScheduleServiceTests_Reconcile` | Idempotenz: gesunder Plan unverändert, keine neuen Slots, `SlotsDisplaced == 0` |
| `Reconcile_DeactivatedRule_DisplacesFutureKeepsPast` | dto. | Deaktivierung: Zukunft `Displaced`, Vergangenheit `Scheduled`, Lücke gefüllt |
| `Reconcile_DeletedRule_DisplacesSlotsAndRefillsGap` | dto. | Löschung: Slots `Displaced` (soft ref bleibt), Füller füllt Lücke |
| `Reconcile_ChangedTimeOfDay_ReplansAtNewTime` | dto. | `TimeOfDay`-Änderung → alte Ausstrahlungen `Displaced`, neue Occurrences belegt |
| `Reconcile_ChangedMaterial_ReplansWithNewMaterial` | dto. | Materialwechsel → neue Inhalte eingeplant |
| `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` | dto. | Cursor-Rücksetzung: verdrängte Folgen werden zur nächsten Gelegenheit erneut eingeplant (Reihenfolge ohne Sprung) |
| `Reconcile_DeletedMediaItem_DisplacesSlotAndContinues` | dto. | MediaItem gelöscht → Ausstrahlung `Displaced`, nächste Inhalte rücken nach, keine Löschkaskade (Slot-Zeile bleibt) |
| `Reconcile_NewEpisodeInserted_ReplansInAirOrder` | dto. | Neue Episode vor Cursor-Position → Ausstrahlung invalidiert, neue Episode eingeplant |
| `Reconcile_PriorityRaised_EvictsLowerPriorityAiring` | dto. | Eviction: hochgestufte Regel verdrängt niederpriorige Ausstrahlung |
| `Reconcile_RunningInvalidSlot_DisplacedButKept` | dto. | Laufender ungültiger Slot → `Displaced`, Zeile bleibt |
| `EnsureCoverage_IgnoresDisplacedSlots` | dto. oder `_Generation` | verdrängte Slots blockieren Abdeckung nicht (`blocked`/`lastEnd`-Filter) |
| `GetSchedule_ReturnsUpcomingSlotsWithStatus` | dto. | Listenabfrage für Admin-Ansicht inkl. `Displaced` |
| `Notify*/WaitAsync/BeginScan` | `ProgramScheduleSignalTests` | Koaleszieren, Scan-Suppression, Wake am Scan-Ende |
| `RuleService_*_NotifiesScheduleSignal` | `ProgramRuleServiceTests_Crud` (oder neu `_Signal`) | Create/Update/SetActive/Delete feuern Signal (Fake-Signal) |
| `SaveChanges_NewEpisode_NotifiesScheduleSignal` | Marker-Hook-Test-Kontext | Media-Add feuert Signal |
| Worker wacht auf Signal und führt Re-Bewertung aus | `ProgramScheduleWorkerTests` | Fake-Signal + Seed, `Displaced` beobachtbar |
| Alt-Backup ohne `ProgramSlots.Status` restaurierbar | `VideoWebPlayerBackupDataTests` | `RemoveColumnsAsync`-Muster; `Status` = `Scheduled` nach Restore |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleTestDb.CreateService`/`CreateRuleAsync` | ggf. Signal-Injection/Hilfen für verdrängte Slots (erweitern, rückwärtskompatibel) |
| `ProgramScheduleWorkerTests` | Worker-Ctor erhält optionalen Signal-Parameter (Aufrufe bleiben kompatibel) |
| `TVSenderAdminE2ETests` | Erweiterung: Sendeplan-Karte sichtbar, verdrängter Slot mit Chip „Verdrängt" (Seed via DbContext) |
| `ApplicationDbContext`-Konstruktionen in Tests | optionaler dritter/vierter Parameter — kompatibel |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|---------------------|
| Pflicht | Admin sieht Sendeplan-Karte mit kommenden Sendeplätzen und Status (mindestens ein „Verdrängt"-Eintrag) | `TVSenderAdminE2ETests` (Erweiterung der bestehenden konsolidierten Methode) | Nachvollziehbarkeit: Anwender sieht verdrängte Sendeplätze | Einziger anwendersichtbarer Neufluss; Status-Darstellung nur über UI nachweisbar |

Die Kernlogik (Verdrängen, Wiedereinplanung, Lückenfüllung) ist serverseitig ohne neue Benutzerinteraktion — ihre Nachweise laufen über die Service-Tests; E2E deckt die einzige neue Sichtbarkeit ab.

## Offene Punkte

Keine.
