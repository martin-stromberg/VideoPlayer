# Umsetzungsplan: Wiederholte Verdrängung gleicher Titel bei Sendeplan-Neuberechnung verhindern (Issue #290)

## Übersicht

Der Sendeplan des automatischen TV-Senders erzeugt bei wiederholten `ReconcileAsync`-Läufen Serien von `Displaced`-Kopien desselben Titels. Betroffen ist ausschließlich `VideoWebPlayer/Services/ProgramScheduleService.cs` (Abgleich/Reproduzierbarkeit, Materialexpansion), ergänzend die Signal-Seite (`MediaDurationProbeWorker`). Das Zielbild: Bei identischen Eingaben ist ein zweiter Lauf ein No-Op (`SlotsDisplaced == 0`, `SlotsCreated == 0`); echte Änderungen ersetzen betroffene Slots genau einmal. Keine Schema-, Interface- oder UI-Änderungen.

## Verifizierte Befunde (Code-Prüfung, `ProgramScheduleService.cs`)

- **Repräsentantenwechsel — verifizierte Verdrängungsursache:** `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` wählen je Inhalt das Item nach `OrderBy(Duration is null ? 1 : 0).ThenBy(MediaItemId)` (Z. 2150–2154, 2163–2167). Schreibt `MediaDurationProbeService.ProbePendingAsync` einem bisher unbemessenen Item eine `Duration`, kann der Repräsentant wechseln. `FindItemIndex` (Z. 952–962) lokalisiert den Slot dann per Content-Fallback (`MovieId`/`TVShowEpisodeId`) auf den **neuen** Ringeintrag; der Slot-für-Slot-Abgleich verlangt aber `slot.MediaItemId == item.MediaItemId` und `slot.End == start + item.Duration` (Z. 730 bzw. Z. 932) → Ausstrahlung invalid → Suffix-Verdrängung + Rematerialisierung mit dem neuen Repräsentanten. Je Probe-Batch wiederholbar → exakt die im Issue beschriebene „Verdrängt"-Serie.
- **Zeitbasis-Bruch — verifiziert:** `ExpandGenreAsync` ruft `GenreCanonicalResolver.ResolveCanonicalNames(allRows, DateTime.UtcNow)` (Z. 2327) auf und ignoriert den an `ExpandMaterialAsync` übergebenen `coverageStart`; der Overload ohne `coverageStart` fällt auf `DateTime.UtcNow` zurück (Z. 2110–2111) und wird von `EnsureCoverageAsync` (Z. 114, dessen `coverageStart = max(now, lastEnd)` davon abweichen kann) und `GetRuleDiagnosesAsync` genutzt. Lauf und Validierung teilen so nicht garantiert dieselbe Zeitbasis; saisonale/versteckte Genres kippen am Tages-/Fensterwechsel.
- **Invader-Evidenz — verifiziertes Oszillationsrisiko:** `_invaderStarts` wird ausschließlich aus `Scheduled`-Slots gebaut (Z. 1062–1064). Wird der Invader-Slot selbst verdrängt oder anders rematerialisiert, ist die Verschiebung im Folgelauf nicht mehr beweisbar → erneute Verdrängung → neues Verschiebebild (Oszillation, im Code selbst kommentiert Z. 203–210 und Z. 338–340).
- **Signal-Kaskade — verifiziert:** `ProbePendingAsync` feuert `NotifyScheduleChanged` einmal je Aufruf bei geschriebener `Duration` (`MediaDurationProbeService.cs` Z. 126–129); `MediaDurationProbeWorker.ExecuteAsync` ruft es in einer `do … while (processed > 0)`-Schleife ohne Pause auf (Z. 76–80) → ⌈Backlog/BatchSize⌉ Neubewertungen pro Worker-Durchlauf, jede gegen eine andere Repräsentantenlage.
- **Geklärt (kein Auslöser):** `TVSenderAdmin.OnInitializedAsync` feuert kein Signal — ein Seitenaufruf verändert den Plan nicht selbst (nur parallel laufende Hintergrunddienste).
- **Ursachenannahme (beschlossen):** Die verifizierte Repräsentantenwechsel-Kaskade gilt als Hauptursache des Produktiv-Befunds. Die Diagnosetests aus der Umsetzungsreihenfolge entscheiden lokal; ergänzend werden `[ProgramSchedule] Re-Bewertung`-Logzeilen mit `MediaDurationProbeWorker`-Batches korreliert, um die Kaskade produktiv nachzuweisen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Reproduzierbarkeitsprüfung bei Medienitem-Wechsel | **Toleranter Slot-Vergleich über Alternativ-Medienitems:** `SchedulableItem` trägt alle gemessenen Verlinkungen des Inhalts (`MediaItemAlternates`); der Slot-Vergleich akzeptiert jedes dieser Items und prüft die Slot-Länge gegen **dessen** Laufzeit | Die Ausstrahlung ist fachlich unverändert, solange die am Slot hinterlegte Datei noch zum selben Inhalt verlinkt und vermessen ist — ein Repräsentantenwechsel allein ist keine planrelevante Änderung. Alternative „Repräsentantenwahl stabilisieren" (nur nach `MediaItemId`) verworfen: ein unbemessenes niedrigere-Id-Item würde den Inhalt unplanbar machen und die bewusste Bevorzugung vermessener Items (Z. 2139–2141) zurückdrehen. |
| Zeitbasis der Expansion | `coverageStart`/`utcNow` des Laufs als Pflichtparameter bis in `ExpandGenreAsync`/`GenreCanonicalResolver` durchreichen; `DateTime.UtcNow`-Fallback im `ExpandMaterialAsync`-Overload entfernen | Ein einheitlicher Zeitanker macht Expansion und Validierung konsistent und deterministisch testbar. Saison-Flip am Tages-/Fensterwechsel bleibt legitime einmalige Änderung. |
| Bündelung der Probe-Signale | `BeginScan()`-Scope des vorhandenen `IProgramScheduleSignal` um die Backlog-Schleife des `MediaDurationProbeWorker` | Nutzt den vorhandenen Koaleszierungs-/Unterdrückungsmechanismus (bewährt bei `MediaSourceScanService`), kein neuer Schalter, echte Änderungen wirken weiter am Durchlaufende zeitnah. Ergänzt, ersetzt nicht die Abgleich-Korrektur. |
| Invader-Oszillation | **Diagnose zuerst** (Reproduktionstest), dann Korrektur; beschlossene Variante bei nachgewiesener Oszillation: `_invaderStarts` zusätzlich aus den `Displaced`-Zeilen des geladenen Fensters speisen (Evidenz nur, keine Belegung). Alternativen (z. B. Verschiebungs-Provenienz am Slot) nur, falls dieser Ansatz widerlegt wird | Ob das Oszillationsrisiko real zuschlägt, ist ohne Reproduktion nicht sicher — der Test entscheidet. Displaced-Evidenz ist die kleinste Änderung: die historische Belegung hat die Verschiebung zum Materialisierungszeitpunkt bewiesen und bleibt als Zeile erhalten. |
| `TVSenderAdmin`-Anzeige der `Displaced`-Historie | **Keine Filterung oder Gruppierung** — die volle Historie bleibt unverändert sichtbar; kein UI-Ausbau in dieser Anforderung | Die Historie ist fachlich gewollt; `DeleteDisplacedSlotsAsync` räumt sie manuell ab. Nach der Idempotenz-Korrektur entstehen keine neuen Serien mehr — bestehende Zeilen bleiben legitime Historie. |
| Ganze Läufe per Eingabe-Fingerprint überspringen | **Nicht umsetzen** | Die Akzeptanzkriterien verlangen Stabilität des Ergebnisses (fachliche Idempotenz), nicht Laufzeitersparnis; ein Fingerprint birgt Fehlklassifikations-Risiko (vergessene Eingabe → nie neu bewertet). |

## Programmabläufe

### Tolerante Reproduzierbarkeitsprüfung (Fix Repräsentantenwechsel)

1. `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` liefern je Inhalt weiterhin denselben Repräsentanten (`Duration != null` bevorzugt, dann kleinste `MediaItemId`), zusätzlich die Liste aller verlinkten Items mit `Duration != null` als `MediaItemVariant`-Alternativen (Rückgabetyp `MediaItemChoice`).
2. `TryMovieItem`/`ToItem` und alle Aufrufer (`ExpandShowAsync`, `ExpandMovieAsync`, `ExpandMovieCollectionAsync`, `ExpandGenreAsync`, sowie alle übrigen `MediaItemRef`-Konsumenten) übernehmen den neuen Rückgabetyp; `SchedulableItem` erhält `MediaItemAlternates` (leer, wo kein Repräsentantenkonzept existiert).
3. `FindItemIndex` bleibt unverändert — die Ringposition ist eine Eigenschaft des Inhalts, nicht der Datei.
4. Neue Hilfsfunktion `SlotMatchesItem(slot, item, expectedStart)` in `FlatAiringTailReproducible` (Z. 724–736) und `InterleavedAiringTailReproducible` (Z. 927–940): Der Slot passt, wenn `slot.MediaItemId` dem Repräsentanten entspricht **oder** in `item.MediaItemAlternates` enthalten ist, `slot.Start == expectedStart` und `slot.End == expectedStart + <Laufzeit des am Slot hinterlegten Items>`; die Positionskette wandert mit `start = slot.End` weiter.
5. Verdrängt wird weiterhin, wenn das hinterlegte Item entlinkt/gelöscht wurde (nicht mehr unter den Alternativen) oder seine Laufzeit nicht zur Slot-Länge passt — echte Änderung, dann genau einmal.

Beteiligte Klassen: `ProgramScheduleService` (`MovieMediaItemsAsync`, `EpisodeMediaItemsAsync`, `TryMovieItem`, `ToItem`, `SchedulableItem`, `MediaItemVariant`, `MediaItemChoice`, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`, `SlotMatchesItem`)

### Zeitbasiskonsistenz der Expansion

1. `ReconcileAsync` ruft `ExpandMaterialAsync` weiterhin mit `coverageStart` (Z. 194); `EnsureCoverageAsync` (Z. 114) und `GetRuleDiagnosesAsync` übergeben ihre eigene Zeitbasis (`coverageStart` bzw. `utcNow`) statt des `DateTime.UtcNow`-Fallbacks — der Overload ohne `coverageStart` entfällt oder der Parameter wird Pflicht.
2. `ExpandGenreAsync` erhält `coverageStart` als Parameter und reicht ihn an `GenreCanonicalResolver.ResolveCanonicalNames` weiter (Z. 2327).
3. `ExpandTrailersAsync` erhält `coverageStart` bereits; unverändert.

Beteiligte Klassen: `ProgramScheduleService` (`ExpandMaterialAsync`, `ExpandGenreAsync`, `EnsureCoverageAsync`, `GetRuleDiagnosesAsync`), `GenreCanonicalResolver` (nur Aufrufstelle)

### Gebündelte Probe-Signale

1. `MediaDurationProbeWorker.ExecuteAsync` löst `IProgramScheduleSignal` aus dem DI-Scope (oder optionalem Ctor-Parameter) und öffnet vor der `do … while`-Backlog-Schleife `BeginScan()`.
2. `ProbePendingAsync` feuert weiterhin `NotifyScheduleChanged` je Batch — unter dem Scope unterdrückt (`ProgramScheduleSignal` Z. 53–59).
3. `Dispose` des Scopes am Schleifenende weckt `ProgramScheduleWorker` (und `LiveStreamWorker`) genau einmal pro Probe-Durchlauf statt ⌈Backlog/BatchSize⌉-mal.

Beteiligte Klassen: `MediaDurationProbeWorker`, `IProgramScheduleSignal`/`ProgramScheduleSignal`, `MediaDurationProbeService` (unverändert)

### Diagnose und Korrektur der Invader-Oszillation

1. Reproduktionstest: Regel A belegt den Nominalstart einer Regel-B-Ausstrahlung (Verschiebung über `ShiftPastInvaders`); anschließend wird der Invader durch eine echte Änderung verdrängt — Folgeläufe von `ReconcileAsync` müssen die verschobene Ausstrahlung weiter beweisen können.
2. Schlägt der Test (Oszillation nachgewiesen): `_invaderStarts` zusätzlich aus den `Displaced`-Zeilen desselben Lade-Fensters speisen (zweiter Ladevorgang bzw. `LoadScheduledAsync` um `Status == Displaced` erweitern, nur als Evidenz — `Airings`/`FillerSlots`/`Scheduled` bleiben Scheduled-only). Grün → abgesichert.
3. Bleibt der Test auch mit Displaced-Evidenz rot (Ansatz widerlegt): Alternative wie eine Verschiebungs-Provenienz am Slot prüfen.
4. Besteht der Test bereits heute: Ursache ausgeschlossen, Schritt dokumentiert das; keine Codeänderung an `_invaderStarts`.

Beteiligte Klassen: `ProgramScheduleService` (`ReconcileWindow.Rebuild`, `_invaderStarts`, `LoadScheduledAsync`)

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MediaItemVariant` | internes Record (in `ProgramScheduleService.cs`) | Eine gemessene Alternativ-Datei eines Inhalts: `(MediaItemId, Duration)` — Grundlage des toleranten Slot-Vergleichs |
| `MediaItemChoice` | internes Record (in `ProgramScheduleService.cs`) | Ergebnis der Repräsentantenwahl je Inhalt: `(Representative: MediaItemRef, Alternates: IReadOnlyList<MediaItemVariant>)` |

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service)

- **Geänderte Methoden:**
  - `MovieMediaItemsAsync` (Z. 2144–2155) / `EpisodeMediaItemsAsync` (Z. 2157–2168) — Rückgabetyp `Dictionary<long, MediaItemRef>` → `Dictionary<long, MediaItemChoice>`; Auswahl des Repräsentanten unverändert, zusätzlich `Alternates` aller Links mit `Duration != null`.
  - `TryMovieItem` / `ToItem` — befüllen `SchedulableItem.MediaItemAlternates`; Signatur folgt dem neuen Dictionary-Typ, alle Aufrufer werden angepasst.
  - `FlatAiringTailReproducible` (Z. 724–736) / `InterleavedAiringTailReproducible` (Z. 927–940) — Slot-für-Slot-Vergleich über `SlotMatchesItem` (Repräsentant oder passende Alternative statt strenger `MediaItemId`-Gleichheit).
  - `ExpandGenreAsync` (Z. 2300–2401) — neuer Parameter `coverageStart`; `ResolveCanonicalNames(allRows, coverageStart)` statt `DateTime.UtcNow` (Z. 2327).
  - `ExpandMaterialAsync` (Z. 2110–2137) — Overload ohne `coverageStart` entfällt bzw. Parameter wird Pflicht; `EnsureCoverageAsync` (Z. 114) und `GetRuleDiagnosesAsync` übergeben ihre Zeitbasis.
  - `ReconcileWindow.Rebuild` (Z. 1056–1077) — je nach Diagnoseergebnis: `_invaderStarts` zusätzlich aus `Displaced`-Zeilen des Fensters (nur Evidenz, kein Einfluss auf `Airings`/`Scheduled`).
  - `ReconcileAsync` — je nach Diagnoseergebnis: zweiter Ladevorgang für `Displaced`-Evidenz im selben Fenster.
- **Neue Methoden:** `SlotMatchesItem` (private static) — Slot-vs-`SchedulableItem`-Vergleich über Repräsentant + `MediaItemAlternates` inkl. laufzeitkonsistenter End-Prüfung.
- **Geänderte Typen:** `SchedulableItem` — neue Eigenschaft `MediaItemAlternates` (`IReadOnlyList<MediaItemVariant>`, Default leer).

### `MediaDurationProbeWorker` (BackgroundService)

- **Neue Ctor-Parameter:** optional `IProgramScheduleSignal? signal = null` (Muster wie `ProgramScheduleWorker`; Fallback: Auflösung aus dem DI-Scope).
- **Geänderte Methoden:** `ExecuteAsync` — `BeginScan()`-Scope um die `do … while (processed > 0)`-Backlog-Schleife (Z. 76–80); Signal je Worker-Durchlauf statt je Batch.

### `ProgramScheduleTestDb` (Test-Fixture, `VideoWebPlayer.Tests/Helpers`)

- **Neue Methoden:**
  - `AssertReconcileNoOpAsync` — schlägt fehl, wenn `summary.SlotsDisplaced > 0` **oder** `summary.SlotsCreated > 0` (Dump der betroffenen Slots; Ergänzung zu `AssertNoDisplacedAsync`, das `SlotsCreated` ignoriert).
  - `LinkMovieMediaItemAsync(movieId, mediaItemId)` / `LinkEpisodeMediaItemAsync(episodeId, mediaItemId)` — zweite Mediendatei an einen bestehenden Inhalt hängen (Reproduktion des Repräsentantenwechsels).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Füller-Validierung:** `ReconcileWindow`-Füller-Prüfung (`state.Items.Any(i => i.MediaItemId == slot.MediaItemId)`, Z. 271–283) bleibt streng — Clip-Quell- und Trailer-Füller kennen kein Repräsentantenkonzept; unverändertes Verhalten.
- **Alle `MediaItemRef`-Konsumenten:** Der geänderte Rückgabetyp von `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` zieht Compile-Anpassungen in `ExpandShowAsync`, `ExpandMovieAsync`, `ExpandMovieCollectionAsync`, `ExpandGenreAsync`, `ExpandTrailersAsync`, `ExpandPlaylistAsync`, `ExpandMediaSourceClipsAsync` und `ExpandClipGroupItemsAsync` nach sich — überall `Alternates` leer, wo kein Repräsentant existiert.
- **`SlotDuration`-Ausstrahlungen:** Die Neusimulation zählt Picks über die Repräsentanten-Laufzeit; weicht die am Slot hinterlegte Alternative in der Laufzeit ab, kann die Take-Anzahl nicht reproduziert werden → einmalige Verdrängung statt Toleranz. Bewusst konservativ gewählt — der Hauptfall (gleiche Datei-Laufzeit, `EpisodeCount`-Regeln) ist abgedeckt; Restfall = echte Änderung.
- **Saisonale Genre-Fenster:** Ein Flip am Tages-/Fensterwechsel bleibt legitime einmalige Verdrängung — kurschlüssige Saison-Fenster können weiter periodisch verdrängen (Konfigurationsproblem, nicht Teil dieses Plans).
- **Signal-Bündelung:** Die einmalige Neubewertung am Durchlaufende sieht mehrere neue `Duration`s auf einmal — bei echten Repräsentanten-Flips höchstens eine Ersetzung je betroffenem Slot; ohne die tolerante Prüfung würde die Serie nur verschoben, nicht verhindert (Abhängigkeit: Bündelung allein genügt nicht).
- **`ProgramClipGroupService.RemoveClipFromGroupAsync` (feuert auch ohne Fund) und `ProgramSettings.razor` `SaveAsync` (feuert bei jedem Speichern):** bleiben unverändert — nach der Idempotenz-Korrektur sind anlasslose Wecksignale harmlose No-Op-Läufe.
- **Bestehende Verdrängungstests:** Die tolerantere Prüfung könnte Erwartungen abschwächen — `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` bleibt grün, weil ein **gelöschtes/entlinktes** Item nicht unter den Alternativen steht; alle Tests, die echte Änderungen erwarten, bleiben gültig.

## Umsetzungsreihenfolge

1. **Assert-Helfer und Baseline-Diagnosetests (fail-first)**
   - Voraussetzungen: `ProgramScheduleTestDb`-Fixture (vorhanden).
   - `AssertReconcileNoOpAsync` anlegen; `Reconcile_IdenticalUtcNow_SecondRunIsNoOp` (flache Regel) und `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp`: `ReconcileAsync` zweimal mit identischem `utcNow` — zweiter Lauf muss `SlotsDisplaced == 0` und `SlotsCreated == 0` liefern. Ergebnis dokumentiert, ob eine Baseline-Nicht-Idempotenz vorliegt oder die Ursache in den Szenarien steckt. Ergänzend werden `[ProgramSchedule] Re-Bewertung`-Logzeilen mit `MediaDurationProbeWorker`-Batches korreliert (produktiver Nachweis der Repräsentantenwechsel-Kaskade als Hauptursache).

2. **Szenario-Reproduktionstests (fail-first, Ursachen-Beweis)**
   - Voraussetzungen: Schritt 1; `LinkMovieMediaItemAsync`/`LinkEpisodeMediaItemAsync` (in diesem Schritt anzulegen).
   - `Reconcile_MovieSecondMediaItemProbed_SlotStaysScheduled`: Film mit zwei Items, eines unbemessen; nach Seed einer `Duration` für das zweite Item muss der Slot `Scheduled` bleiben — heute rot (verifizierte Ursache).
   - `Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns`: Invader-Verschiebung, dann Verdrängung des Invaders → Folgeläufe stabil. Ergebnis entscheidet Schritt 7.

3. **Alternativ-Medienitems in der Expansion**
   - Voraussetzungen: keine externen; fachlich Schritt 2 als Nachweis.
   - `MediaItemVariant`/`MediaItemChoice` anlegen; `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` auf `MediaItemChoice` umstellen; `TryMovieItem`/`ToItem` + alle Aufrufer anpassen; `SchedulableItem.MediaItemAlternates` befüllen.

4. **Toleranter Slot-Vergleich**
   - Voraussetzungen: Schritt 3.
   - `SlotMatchesItem` implementieren und in `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible` einsetzen; Tests aus Schritt 2 müssen grün werden.

5. **Zeitbasiskonsistenz**
   - Voraussetzungen: keine externen; unabhängig von Schritt 3/4.
   - `ExpandGenreAsync`-Parameter `coverageStart`; `ExpandMaterialAsync`-Overload ohne `coverageStart` beseitigen; `EnsureCoverageAsync`/`GetRuleDiagnosesAsync` reichen ihre Zeitbasis durch. Begleittest `Reconcile_SeasonalGenre_UtcNowBasis_DeterministicExpansion`.

6. **Probe-Signal-Bündelung**
   - Voraussetzungen: `IProgramScheduleSignal.BeginScan` (vorhanden).
   - `MediaDurationProbeWorker`: optionaler Signal-Parameter + `BeginScan`-Scope um die Backlog-Schleife; Worker-Test `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake`.

7. **Invader-Korrektur (nur falls Schritt 2 rot)**
   - Voraussetzungen: Ergebnis des Diagnosetests aus Schritt 2.
   - `_invaderStarts` aus `Displaced`-Historie des Fensters ergänzen (beschlossene Variante, Evidenz nur — keine Belegung); wird dieser Ansatz widerlegt, Verschiebungs-Provenienz am Slot prüfen; Diagnosetest wird grün.

8. **E2E-Regressionstest**
   - Voraussetzungen: `TVSenderAdminE2ETestBase` (vorhanden); Schritte 3–5 empfohlen (der Test prüft das Endverhalten).
   - `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries`: zweimal „Jetzt generieren" → keine „Verdrängt"-Serie in der Übersicht.

9. **Verifikation nach DoD**
   - Voraussetzungen: Schritte 1–8.
   - `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle `dotnet test VideoWebPlayer.Tests`-Suite grün; Kaputtmachen-Beweis der neuen Tests (abgesicherte Stelle gezielt brechen → Test muss fehlschlagen); Bericht nennt etwaige Flaker.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AssertReconcileNoOpAsync` | `ProgramScheduleTestDb` (Hilfsmethode) | `SlotsDisplaced == 0` **und** `SlotsCreated == 0` mit Slot-Dump im Fehlerfall |
| `LinkMovieMediaItemAsync` / `LinkEpisodeMediaItemAsync` | `ProgramScheduleTestDb` (Hilfsmethode) | Zweite Mediendatei an bestehenden Inhalt (Reproduktion Repräsentantenwechsel) |
| `Reconcile_IdenticalUtcNow_SecondRunIsNoOp` | `ProgramScheduleServiceTests_Reconcile` | Zweiter Lauf, identisches `utcNow`, flache Regel → 0/0 |
| `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp` | `ProgramScheduleServiceTests_Interleaving` | Zweiter Lauf, identisches `utcNow`, mehrsträngige Genre-Regel → 0/0 |
| `Reconcile_MovieSecondMediaItemProbed_SlotStaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | Repräsentantenwechsel nach nachträglicher `Duration` verdrängt nicht; Slot bleibt `Scheduled` |
| `Reconcile_EpisodeSecondMediaItemProbed_SlotStaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | Serien-Pendant des Repräsentantenwechsels |
| `Reconcile_UnlinkedMediaItem_StillDisplacesOnce` | `ProgramScheduleServiceTests_Reconcile` | Entlinktes/gelöschtes Item → genau einmal `Displaced` + Ersatz; Folgelauf stabil (echte Änderung) |
| `Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns` | `ProgramScheduleServiceTests_Boundary` | Diagnose: verschobene Ausstrahlung bleibt nach Verdrängung des Invaders beweisbar |
| `Reconcile_SeasonalGenre_UtcNowBasis_DeterministicExpansion` | `ProgramScheduleServiceTests_Genres` | Genre-Auflösung folgt dem Lauf-`utcNow`, nicht der Wanduhr (innerhalb/außerhalb Saison-Fenster) |
| `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake` | neue `MediaDurationProbeWorkerTests` | ≥2 pending Items, `BatchSize` 1 → genau ein `WaitAsync`-Abschluss je Durchlauf (echtes `ProgramScheduleSignal`) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_*` (Verdrängungserwartungen) | Zu prüfen, ob ein Bestandstest eine Verdrängung über einen Repräsentantenwechsel (statt Entlinken/Löschen) herstellt — solche Fälle müssten auf echte Änderungen umgestellt werden; bei Durchsicht keine betroffene Methode identifiziert, vorsorglich gelistet |
| `MediaDurationProbeServiceTests` (`ProbePendingAsync_*`) | Keine Anpassung — `ProbePendingAsync` feuert weiterhin je Aufruf; Bündelung liegt im Worker |

### E2E-Tests (Pflicht)

Keine neue oder geänderte Benutzerinteraktion — der Bug ist jedoch in der Admin-Übersicht sichtbar; ein E2E-Regressionstest deckt den Happy Path ab:

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Zweimal „Jetzt generieren" ohne Eingabeänderung → keine „Verdrängt"-Serie desselben Titels in der Programm-Übersicht | `TVSenderAdminE2ETests` (`TVSenderAdminE2ETestBase` vorhanden) | Bei unveränderten Eingaben entstehen keine `Displaced`-Serien |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine bestehenden E2E-Tests betroffen |

## Offene Punkte

Keine.
