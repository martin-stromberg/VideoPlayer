# Umsetzungsplan: Platzhalter-Blöcke im Sendeplan kompakt darstellen

## Übersicht

Der Admin-Tab „Sendeplan" (`TVSenderAdmin.razor`) fasst zusammenhängende Läufe von
Clip-Füll-Slots (Restzeit-Auffüllung aus `FillAiringSegments`, Issue #277) zu je einer
Block-Karte zusammen — Zeitraum, Gesamtdauer, Clip-Anzahl — mit einem Symbol-Button
(`aria-label`), der eine Overlay-Detailansicht aller Clips in Abspielreihenfolge öffnet.
Clip-Füll-Slots werden dazu persistiert markiert (neue Spalte `ProgramSlot.IsClipFill`,
nur bei der Erzeugung gesetzt). Einplanung, Abspielreihenfolge, Programmauskunft und
Auslieferung bleiben unverändert; die Gruppierung entsteht ausschließlich beim
Anzeigeaufbau.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Clip-Erkennung (zentrale Frage) | **Variante A:** persistiertes `ProgramSlot.IsClipFill` (bool, Default `false`), gesetzt ausschließlich in `FillAiringSegments` — gleiches denormalisiertes-Snapshot-Muster wie `ProgramSlot.RuleType` | Die Anzeige läuft über `GetScheduleAsync`, das `ProgramSlot`-Entities liefert — das Flag ist ohne Zusatzabfrage dabei, kein Join, kein N+1. Variante B (Laufzeit-Join über `ProgramClipGroupItems` ⨝ `ProgramRuleClipGroups`) hinge vom aktuellen, veränderlichen Gruppenbestand ab: entfernte Mitgliedschaften oder gelöschte Regeln (`ProgramRuleId` → `null`, weiche Referenz) würden bestehende Slots nachträglich „ent-markieren" und Blöcke mitten im Plan aufbrechen lassen. Zudem bräuchte die Anzeige eine zweite Abfrage je Regel/Sendetag. Preis von A: EF-Migration + `OptionalRestoreColumns`-Eintrag + Alt-Backup-Test — von der Definition of Done ohnehin gefordert. |
| Kennzeichen-Typ | `bool IsClipFill`, kein `ProgramSlotKind`-Enum | Es gibt genau einen neuen Slot-Zustand; ein Enum wäre spekulative Erweiterung (YAGNI) und verteuert Migration/Backup-Defaults. |
| Backfill bestehender Slots | Kein Daten-Backfill — Spalte kommt mit `defaultValue: false` | Ein SQL-Backfill über heutige Gruppenmitgliedschaften wäre falsch, sobald sich Mitgliedschaften seit Slot-Erzeugung geändert haben (derselbe Schwachpunkt wie Variante B). Der Sendeplan ist rollierend (Horizont ~48 h): alte, unmarkierte Clip-Slots erscheinen weiterhin als Einzelkarten und rollen innerhalb weniger Tage aus dem sichtbaren Fenster. |
| Scope | Nur Admin-Tab „Sendeplan" (`TVSenderAdmin.razor`); `LiveTv.razor`, `ProgramGuideService`, `DtoProgramGuideEntry` und `ProgramGuideController` unverändert | Das Issue sagt „Sendeplan" — das ist der Name des Admin-Tabs. Die öffentliche Vorschau zeigt bewusst die tatsächliche Abspielreihenfolge; eine Gruppierung dort müsste DTO/Controller/Client erweitern und wäre ein eigenes Feature. `GetNowAsync`/`LiveStreamCoordinator` bleiben per Randbedingung unangetastet. |
| Füller-Regel „Clip-Quelle" (`ProgramRuleMaterialType.MediaSourceClips`) | Nicht gruppiert | Diese Slots werden nicht von `InsertClipFill`/`FillAiringSegments` erzeugt — sie sind reguläres Regel-Material (`RuleType = Filler`), keine Restzeit-Auffüllung im #277-Sinn. Die Anforderung begrenzt sich ausdrücklich auf `InsertClipFill`/`FillAiringSegments`-Erzeugnisse. |
| Ein-Clip-Block | Ein einzelner aufeinanderfolgender Clip-Füll-Slot bleibt eine normale Slot-Karte; Block erst ab ≥ 2 Clips | Ziel ist Kompaktheit: Ein Block mit einem Clip kostet einen Klick ohne Informationsgewinn und sähe wie eine gewöhnliche Karte aus. |
| `Displaced`-Clip-Slots | Werden nicht gruppiert — bleiben Einzelkarten (`tvsender-slot-displaced`, durchgestrichen) — und schließen einen laufenden Block ab | `Displaced`-Einträge sind reine Traceability-Anzeige, belegen keine Sendezeit; ein Block „belegter Sendezeit" darf sie nicht optisch einschließen. Als Blocktrenner verhalten sie sich wie jeder andere Nicht-Block-Eintrag — einfache, deterministische Regel. |
| Blockgrenzen | Ein Block endet am (a) Tageswechsel (Tagesgruppierung bleibt äußere Schleife), (b) `ProgramRuleId`-Wechsel, (c) Zeitlücke (`slot.Start != prevClip.End`), (d) jedem Nicht-Scheduled-Clip-Fill-Eintrag | Tages-Header und `tvsender-rule-divider` bleiben erhalten und der „Regel: X"-Kontext je Block konsistent; die Zeitlücken-Regel setzt „durch reguläre Sendungen unterbrochene Läufe bleiben getrennt" auch für dokumentierte Restlücken um (`ClipTooLargeForFreeRoom_LeavesDocumentedGap`). |
| Detailansicht | Overlay-Dialog nach `admin-dialog-overlay`-Muster (wie die fünf Bestätigungsdialoge derselben Seite), nicht Inline-Aufklappen | Vorhandenes, freigegebenes Muster; Clip-Listen können sehr lang sein und brauchen Scrollraum, den ein Dialog bietet, ohne das Karten-Grid zu verschieben; Schließen über Overlay-Klick/Abbrechen ist gewohnt. |
| Ort der Gruppierungslogik | `internal static` Methode `BuildScheduleDisplayItems` + `internal` Display-Item-Records im `@code` von `TVSenderAdmin.razor` (Muster: `internal static` Helfer in `TVShowDetails.razor`, per `InternalsVisibleTo` direkt getestet) | Reine Funktion über die bereits geladene Tages-Slotliste — keine DbContext-Abhängigkeit, direkt unit-testbar, kein neuer Service für eine reine Anzeige-Transformation. |
| Live-/Fortschrittsanzeige auf Blockebene | Block zeigt `badge-live` + `progress-track`, wenn ein enthaltener Clip läuft; Fortschritt über Blockspanne (`first.Start` bis `last.End`) | Sonst verschwände die Live-Information, sobald ein laufender Slot in einen Block kollabiert — sichtbare Regression der heutigen Karte. |
| `maxCount = 200`-Fensterrand | Ein über das Listenende hinauslaufender Clip-Lauf zeigt nur die geladenen Clips; Zeitraum/Dauer untertreiben den Block — akzeptierte Einschränkung | Das Fenster (-2 h bis 200 Slots) deckt den relevanten Planungshorizont ab; ein „gekürzt"-Hinweis ist nicht gefordert. Bei praktischem Bedarf kann `maxCount` erhöht werden (kein Schema-Eingriff). |
| Beschriftung der Block-Karte | Titelzeile „Platzhalter-Clips", Subzeile „N Clips · Gesamtdauer h:mm" plus vorhandene „Regel: …"-Zeile; Icon-Button mit `aria-label="Platzhalter-Clips anzeigen"` im Glyph-/Symbol-Stil der Seite | Im Issue ist nur der zugängliche Name festgelegt; das Wording folgt den übrigen Karten derselben Seite. Feinschliff im Review. |

## Programmabläufe

### Clip-Füll-Markierung bei der Plan-Erzeugung

1. `EnsureCoverageAsync`/`ReconcileAsync` rufen wie bisher `InsertClipFill` → `FillAiringSegments`.
2. In `FillAiringSegments` wird beim `new ProgramSlot { … }` (einzige Erzeugungsstelle
   von Clip-Füll-Slots, `ProgramScheduleService.cs` ~Z. 2656) zusätzlich `IsClipFill = true`
   gesetzt. Alle anderen Slot-Erzeuger (Titel-Materialisierung, Füller) lassen den
   CLR-/DB-Default `false`.
3. Persistierung unverändert über die `rebuilt`-Liste des Laufs — kein eigener
   Speicherpfad, keine Verhaltensänderung der Planung.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `ProgramSlot`,
`ApplicationDbContext` (Migration).

### Sendeplan-Anzeige mit Blöcken

1. `ReloadScheduleAsync` lädt `schedule` via `GetScheduleAsync` wie bisher (`IsClipFill`
   kommt auf den Entities mit) und baut `slotCards` weiter für **alle** Slots auf — die
   Clip-Karteninhalte werden für die Detailansicht wiederverwendet.
2. Der Render-Loop gruppiert weiterhin `schedule` nach lokalem Kalendertag
   (`tvsender-day-header`). Innerhalb eines Tages liefert
   `BuildScheduleDisplayItems(daySlots)` eine geordnete Sequenz von
   `ScheduleSlotItem` (einzelner Slot) und `ScheduleClipBlockItem` (≥ 2 aufeinanderfolgende
   `Scheduled`-Clip-Füll-Slots gleicher `ProgramRuleId` mit lückenloser
   `prev.End == next.Start`-Kette).
3. Die `tvsender-rule-divider`-Logik vergleicht die `ProgramRuleId` des jeweils **ersten**
   Slots eines Anzeige-Eintrags mit der des vorigen Eintrags — Trennlinien erscheinen
   zwischen Blöcken und Karten wie bisher.
4. `ScheduleSlotItem` rendert die unveränderte Slot-Karte. `ScheduleClipBlockItem` rendert
   eine Block-Karte `.program-card.tvsender-clip-block` (`data-first-slot-id`,
   `data-clip-count`): Zeitraum `first.Start`–`last.End` (lokal), Gesamtdauer
   (`last.End - first.Start`), „N Clips", Regel-Subzeile über `RuleName(firstSlot)`,
   Status-Chip „Geplant", Live-Badge + Fortschritt, wenn ein enthaltener Clip `IsRunning`
   erfüllt; Beschreibungszeile über `SlotCard(firstClip).Description` mit
   `ProgramGuideEntryText.NoDescriptionText`-Fallback (kein leeres Feld).
5. Der `admin-chip` „@schedule.Count Einträge" bleibt Slot-Zähler (unverändert) — die
   Blockbildung ist reine Darstellung.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `ProgramSlot`,
`ScheduleDisplayItem`/`ScheduleSlotItem`/`ScheduleClipBlockItem`.

### Detailansicht öffnen und schließen

1. Der Symbol-Button `.tvsender-clipblock-toggle` (`aria-label="Platzhalter-Clips
   anzeigen"`) setzt `clipBlockDetail` auf die geordnete Clip-Liste des Blocks.
2. Ein `admin-dialog-overlay` rendert `admin-dialog` `#tvsender-clipblock-dialog`:
   Header „Platzhalter-Clips (N)", Liste `.tvsender-clipblock-row` je Clip mit
   `data-slot-id` — Start (`ToLocalTime`, `HH:mm`), Dauer (`End - Start`) und Titel via
   `SlotCard(clip)` (Bare-Item-`MediaItem.Name`-Fallback; Trailer-Clips zeigen wie bisher
   den Filmnamen, da `MovieId` gesetzt ist). Reihenfolge = `ProgramSlot.Start` =
   Abspielreihenfolge.
3. Schließen über Schließen-/Abbrechen-Button und Overlay-Klick (`@onclick` auf Overlay,
   `@onclick:stopPropagation` im Dialog) setzt `clipBlockDetail = null` — exakt das
   vorhandene Dialog-Muster.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `ScheduleCardContent`, `SlotCard`.

### Backup-Restore mit und ohne neue Spalte

1. Neue Backups schreiben `ProgramSlots.IsClipFill` mit (Standardpfad — die Tabelle ist
   bereits in `OptionalRestoreTables`).
2. Alt-Backups ohne die Spalte werden über den `OptionalRestoreColumns`-Eintrag +
   `OptionalRestoreBoolDefaults`-Default `false` toleriert — wiederhergestellte Slots
   sind unmarkiert (erscheinen einzeln, kein Datenverlust).

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScheduleDisplayItem` | `internal abstract record` (im `@code` von `TVSenderAdmin.razor`) | Basistyp der Anzeige-Sequenz des Sendeplan-Tages |
| `ScheduleSlotItem` | `internal sealed record` mit `ProgramSlot Slot` | Einzelkarte (Nicht-Clip, Einzel-Clip, Displaced) |
| `ScheduleClipBlockItem` | `internal sealed record` mit `IReadOnlyList<ProgramSlot> Clips` | Block-Karte (≥ 2 zusammenhängende Scheduled-Clip-Füll-Slots); abgeleitete Anzeigewerte (Zeitraum, Dauer, Anzahl, Running) aus der Liste |

Keine neuen Services, Interfaces oder DTOs — `ProgramSlot.IsClipFill` ist eine neue
Eigenschaft, keine Klasse.

## Änderungen an bestehenden Klassen

### `ProgramSlot` (Datenmodellklasse)

- **Neue Eigenschaften:** `IsClipFill` (`bool`, Default `false`) — markiert Slots, die von
  `FillAiringSegments` als Restzeit-Clip-Füllung erzeugt wurden (denormalisierter
  Snapshot wie `RuleType`, überlebt Regel-/Gruppenlöschung).

### `ProgramSlotConfiguration` (EF-Konfiguration)

- `IsClipFill` als `IsRequired()` konfigurieren (Muster der anderen `IsRequired`-Spalten).

### `ProgramScheduleService` (Service)

- **Geänderte Methoden:** `FillAiringSegments` — setzt `IsClipFill = true` am erzeugten
  `ProgramSlot`. Keine sonstige Änderung an Plan-/Reconcile-Logik (`clipIds`-
  Klassifikation, `GroupAirings`, `RewindCursor` bleiben unberührt).

### `TVSenderAdmin.razor` (Komponente, `@code` + Markup)

- **Neue Methoden:** `internal static IReadOnlyList<ScheduleDisplayItem>
  BuildScheduleDisplayItems(IReadOnlyList<ProgramSlot> daySlots)` — reine Gruppierungs-
  funktion mit den oben definierten Blockgrenzen.
- **Neue Methoden:** Block-Anzeigehilfen — `IsBlockRunning(ScheduleClipBlockItem)`
  (irgendein Clip `IsRunning`) und `BlockProgressPercent(ScheduleClipBlockItem)`
  (Position in `first.Start`–`last.End`, geklemmt 0–100, Muster `ProgressPercent`).
- **Geänderte Methoden/Markup:** Render-Loop des Sendeplan-Tabs — statt `foreach` über
  Slots Iteration über `BuildScheduleDisplayItems(day)`; Regel-Trennlinie vergleicht die
  `ProgramRuleId` des ersten Slots je Eintrag; neuer Zweig für die Block-Karte
  `.tvsender-clip-block`.
- **Neues Feld:** `private IReadOnlyList<ProgramSlot>? clipBlockDetail` — Zustand des
  Detail-Dialogs.
- **Neues Markup:** Detail-Overlay `#tvsender-clipblock-dialog` nach
  `admin-dialog-overlay`-Muster mit `.tvsender-clipblock-row`-Liste (Start, Dauer, Titel
  via `SlotCard`); Button `.tvsender-clipblock-toggle` mit `aria-label`.

### `VideoWebPlayerBackupData` (Backup-Whitelist)

- `OptionalRestoreColumns`: Eintrag
  `$"{nameof(ApplicationDbContext.ProgramSlots)}.{nameof(ProgramSlot.IsClipFill)}"`.
- `OptionalRestoreBoolDefaults`: Eintrag `(ProgramSlots, IsClipFill, false)` —
  Muster `ProgramSlots.Status`/`OptionalRestoreIntDefaults`.

### `ProgramScheduleTestDb` (Test-Helfer)

- `CreateSlotAsync`: neuer optionaler Parameter `bool isClipFill = false` — setzt das
  Flag am Seed-Slot; bestehende Aufrufe bleiben kompilierfähig.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramSlotIsClipFill` | `ProgramSlots.IsClipFill` (`bool`, NOT NULL, `defaultValue: false`) | Neue Spalte; kein Daten-Backfill (siehe Designentscheidung — rollierender Plan, falsche Rückschlüsse über aktuelle Gruppenmitgliedschaften vermieden). Erzeugt per `dotnet ef migrations add`; `ApplicationDbContextModelSnapshot` wird aktualisiert. |

## Validierungsregeln

Keine — es entstehen keine neuen oder geänderten Eingaben (reine Anzeige-Gruppierung
plus intern gesetztes Flag).

## Konfigurationsänderungen

Keine — die Anforderung verlangt ausdrücklich keine Konfigurierbarkeit; die Block-
Mindestgröße 2 ist eine feste Anzeigeregel.

## Seiteneffekte und Risiken

- **E2E `TVSenderAdminClipGroupsE2ETests.GeneratedSchedule_ContainsVisibleClipSlots`:**
  Bricht voraussichtlich — der generierte 4-h-Sendeplatz (76-Min-Film, 60-s-Clip)
  erzeugt einen Clip-Füll-Lauf mit ≥ 2 Slots, die dann als Block statt als
  `.tvsender-slot-row` mit Clip-Titel erscheinen. Der Test wird angepasst (Block-Karte +
  Detailansicht, siehe Tests).
- **Alt-Bestände:** In produktiven Datenbanken vorhandene Clip-Füll-Slots bleiben
  `IsClipFill = false` und erscheinen einzeln, bis sie aus dem rollierenden Plan
  herauslaufen — gewolltes Verhalten (kein Backfill).
- **Test-DBs via `EnsureCreated`** (u. a. `ProgramScheduleTestDb`) erhalten die Spalte
  automatisch aus dem Modell; reale SQLite-DBs über `app.MigrateDatabase()`.
- **`maxCount = 200`:** Ein am Listenende angeschnittener Block zeigt nur die geladenen
  Clips (Zeitraum/Dauer aus dem Geladenen). Akzeptierte Einschränkung der Admin-
  Übersicht (siehe Designentscheidungen).
- **Trailer-Clips** (aus `FillAiringSegments`, mit `MovieId`) sind Teil des Blocks und
  erscheinen in der Detailansicht mit ihrem Filmtitel — konsistent zur heutigen
  Slot-Karte.
- **Programmauskunft/Live-TV/Auslieferung:** unberührt — `DtoProgramGuideEntry`,
  `ProgramGuideService`, `LiveStreamCoordinator`, `IProgramScheduleService`-Signaturen
  bleiben unverändert; `IsFiller`-Semantik bleibt wie sie ist.

## Umsetzungsreihenfolge

1. **Datenmodell + Migration:** `ProgramSlot.IsClipFill`, `ProgramSlotConfiguration`,
   Migration `AddProgramSlotIsClipFill` inkl. Snapshot.
   - Voraussetzungen: `dotnet-ef`-Toolchain wie für alle bisherigen Migrationen des
     Repos.
2. **Backup-Registrierung:** `OptionalRestoreColumns` + `OptionalRestoreBoolDefaults`.
   - Voraussetzungen: Schritt 1.
3. **Flag setzen:** `IsClipFill = true` in `FillAiringSegments`.
   - Voraussetzungen: Schritt 1.
4. **Test-Helfer:** `isClipFill`-Parameter in `ProgramScheduleTestDb.CreateSlotAsync`.
   - Voraussetzungen: Schritt 1.
5. **Anzeige-Modell:** `ScheduleDisplayItem`-Records + `BuildScheduleDisplayItems` im
   `@code` von `TVSenderAdmin.razor`.
   - Voraussetzungen: Schritt 1 (Property existiert).
6. **Block-Karte:** Render-Loop-Zweig `.tvsender-clip-block` mit Zeitraum, Dauer, Anzahl,
   Regel-/Status-/Live-Elementen; angepasste Regel-Trennlinien-Logik.
   - Voraussetzungen: Schritt 5.
7. **Detailansicht:** `clipBlockDetail`-State, Toggle-Button mit `aria-label`,
   Overlay-Dialog mit Clip-Liste.
   - Voraussetzungen: Schritt 6.
8. **Styles:** `app.css` — `.tvsender-clip-block`-Kartenanpassung (falls über
   `.program-card` hinaus nötig) und `.tvsender-clipblock-row`-Liste im Dialog.
   - Voraussetzungen: Schritte 6–7.
9. **Tests:** Unit-Tests der Gruppierungsfunktion, Service-Test des Flags, bUnit-
   Komponententests, Backup-Regressionstest.
   - Voraussetzungen: Schritte 2–7 je nach Test.
10. **E2E:** neue Playwright-Klasse + Anpassung des bestehenden Clip-Slot-E2E.
    - Voraussetzungen: Schritte 6–7.
11. **Dokumentation:** `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`, `README.md`
    prüfen.
    - Voraussetzungen: Schritte 6–7.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `BuildScheduleDisplayItems_ConsecutiveClips_FormSingleBlock` | `ScheduleDisplayItemsTests` (neu, `internal` via `InternalsVisibleTo`) | N lückenlose Scheduled-Clip-Fill-Slots gleicher Regel → genau ein `ScheduleClipBlockItem` mit allen Clips in Start-Reihenfolge |
| `BuildScheduleDisplayItems_SingleClip_StaysSlotItem` | `ScheduleDisplayItemsTests` | Einzelner Clip-Fill-Slot → `ScheduleSlotItem` (kein Ein-Clip-Block) |
| `BuildScheduleDisplayItems_NonClipSlot_SplitsRun` | `ScheduleDisplayItemsTests` | Clip-Lauf + regulärer Slot + Clip-Lauf → zwei getrennte Blöcke |
| `BuildScheduleDisplayItems_RuleChange_SplitsBlock` | `ScheduleDisplayItemsTests` | Lückenlose Clips mit wechselnder `ProgramRuleId` → zwei Blöcke |
| `BuildScheduleDisplayItems_TimeGap_SplitsBlock` | `ScheduleDisplayItemsTests` | Clips mit `Start != prev.End` → zwei Blöcke |
| `BuildScheduleDisplayItems_DisplacedClip_RendersSingle_AndSplits` | `ScheduleDisplayItemsTests` | `Displaced`-Clip-Slot → `ScheduleSlotItem`, kein Block-Beitritt |
| `EnsureCoverage_GeneratedClipFillSlots_MarkedIsClipFill` | `ProgramScheduleServiceTests_ClipGroups` (Erweiterung) | Von `FillAiringSegments` erzeugte Slots tragen `IsClipFill`, Titel-Slots nicht (Seeds via `SeedClipAiringAsync`-Muster, echtes SQLite) |
| `ReadFromAsync_LegacyBackupWithoutIsClipFill_RestoresSuccessfully` + `…_CurrentBackup_PreservesIsClipFill` | `VideoWebPlayerBackupDataTests_ProgramSlotIsClipFill` (neu, Muster `…_ProgramScheduleContentScope` mit `LegacyBackupArchiveBuilder`) | Alt-Backup ohne Spalte restored (Default `false`); aktuelles Backup erhält `true` |
| `Sendeplan_ClipFillBlock_RendersSingleBlockCard` | `TVSenderAdminTests_ClipBlocks` (neu, bUnit, `CreateContextAsync`) | Mehrere `isClipFill`-Seeds → eine `.tvsender-clip-block`-Karte mit Zeitraum, Dauer, „N Clips"; keine Einzelkarten der Clips |
| `Sendeplan_ClipBlock_DetailButton_OpensDialogWithAllClipsInOrder` | `TVSenderAdminTests_ClipBlocks` | Klick auf `aria-label`-Button → `#tvsender-clipblock-dialog` listet alle Clips in Start-Reihenfolge (Name via `MediaItem.Name`) |
| `Sendeplan_SeparatedClipRuns_RenderSeparateBlocks` | `TVSenderAdminTests_ClipBlocks` | Zwei durch regulären Slot getrennte Läufe → zwei Block-Karten |
| `Sendeplan_DisplacedClip_NotGrouped` | `TVSenderAdminTests_ClipBlocks` | `Displaced`-Clip-Slot bleibt `tvsender-slot-displaced`-Einzelkarte |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleTestDb.CreateSlotAsync`-Aufrufe (diverse Testklassen) | Keiner — neuer Parameter ist optional mit Default `false` |
| `ProgramScheduleServiceTests_ClipGroups` (Clip-Identifikation via `clipIds`) | Optional ergänzende `IsClipFill`-Assertion im Bestandstest möglich, nicht erforderlich — eigener neuer Test deckt das Flag ab |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin öffnet Sendeplan-Tab: langer Clip-Füll-Lauf (≥ 5 Clips, geseedet als `IsClipFill`-Slots via `Services.CreateScope()` + `ApplicationDbContext`, Muster `TVSenderAdminClipGroupsE2ETests`) erscheint als eine Block-Karte; Klick auf den `aria-label`-Button öffnet die Detailansicht mit allen Clips in Reihenfolge | `TVSenderAdminClipBlocksE2ETests` (neu, Basis `TVSenderAdminE2ETestBase`, Hilfen `OpenAdminPageAsAdminAsync`, `SwitchTabAsync`/`OpenSendeplanTabAsync`) | Akzeptanzkriterium „UI-Test für Block mit vielen Clips + Detailansicht" sowie Blockbildung/Blockeintrag/Detailansicht | Der Fluss (Seed → gerenderte Block-Karte → Klick → Overlay-Liste) ist ein Benutzerablauf im Browser; nur E2E belegt Markup, Button-Wiring und Dialog im echten Render |
| Pflicht | Detailansicht schließt wieder (Schließen-Button/Overlay-Klick) und der Sendeplan bleibt unverändert | `TVSenderAdminClipBlocksE2ETests` (gleicher Test oder zweiter Fall) | Detailansicht | Dialog-Lebenszyklus ist UI-Verhalten |
| Empfohlen | Getrennte Läufe zeigen getrennte Blöcke (Zählung `.tvsender-clip-block`) — bereits bUnit-abgedeckt; im E2E-Happy-Path mitseedbar ohne Mehrkosten | `TVSenderAdminClipBlocksE2ETests` | „Getrennte Blöcke bleiben getrennt" | Verstärkung des Pflicht-Szenarios |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminClipGroupsE2ETests.GeneratedSchedule_ContainsVisibleClipSlots` | Erzeugte Clip-Füll-Slots kollabieren zur Block-Karte — der bisherige Locator `.tvsender-slot-row` mit „funki-clip.mkv" findet nichts mehr. Anpassung: auf `.tvsender-clip-block` warten, Detail-Button klicken, Clip-Name in `#tvsender-clipblock-dialog` asserten (deckt zugleich den Happy-Path der neuen Ansicht mit echt generiertem Plan ab). |

## Offene Punkte

Keine.
