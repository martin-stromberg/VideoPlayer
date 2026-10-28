# Anforderung: Wiederholte Verdrängung gleicher Titel bei Sendeplan-Neuberechnung verhindern

Quelle: GitHub-Issue #290, Branch `task/issue-290-45bd50bf6fec48118ee38823c67a41be-tv-sender-wiederholte-verdraen`

## Fachliche Zusammenfassung

Der Sendeplan des automatischen TV-Senders (`ProgramSlot`-Zeilen, Status `ProgramSlotStatus.Scheduled`/`Displaced`) wird bei wiederholten Auswertungen instabil: `ProgramScheduleService.ReconcileAsync` markiert bereits geplante Sendeplätze erneut als `Displaced` und materialisiert anschließend Ersatz für dieselben Positionen — ohne sachliche Eingabeänderung. In der Admin-Übersicht entstehen dadurch Serien mehrerer „Verdrängt"-Kopien desselben Titels direkt vor einem „Geplant"-Eintrag. Zu beheben sind zwei Seiten: die Auslösung der Neubewertung (Worker-Tick + `IProgramScheduleSignal`-Quellen) und die Nicht-Idempotenz des Abgleichs zwischen gespeicherten und erneut berechneten Plan-Einträgen. Ziel: Bei unveränderten Regeln, Medien und Zeitfenstern erzeugt ein erneuter `ReconcileAsync`-Lauf keine weiteren `Displaced`-Einträge und keine neuen Ersatz-Slots; bei echten Änderungen werden Einträge genau einmal nachvollziehbar ersetzt.

## Betroffene Klassen und Komponenten

### Logik / Services (Kern der Behebung)

- `VideoWebPlayer/Services/ProgramScheduleService.cs` — Hauptarbeitsplatz:
  - `ReconcileAsync` (Neubewertung: Validierung → `Displace`/`DisplaceRuleFuture` → `RewindCursor` → Re-Materialisierung über `MaterializeOccurrenceEvicting` → `FillGaps` → `PersistCursors`).
  - `ReconcileWindow` (`Reload`/`Rebuild`/`Validate`, `InvalidGroups`, `HasRescuableInvalid`, `Airings`, `FillerSlots`, `_invaderStarts`) — Abgleich gespeicherter `Scheduled`-Slots gegen die aktuelle Regel-/Medienlage.
  - `AiringReproducible` / `FlatAiringTailReproducible` / `InterleavedAiringTailReproducible` — Reproduzierbarkeitsprüfung; scheitert sie zu Unrecht, beginnt die Verdrängungs-/Wiedereinfüge-Schleife.
  - `ShiftPastInvaders`, `BoundaryAfter`, `SelectAiringItems`/`SelectItems`, `StrandStartOffset` — zeit- und belegungsabhängige Auswahl, die ein Slot aus einem früheren Lauf exakt reproduzieren muss.
  - Materialexpansion (`ExpandMaterialAsync`, `MovieMediaItemsAsync`, `EpisodeMediaItemsAsync`, `ExpandGenreAsync` mit `GenreCanonicalResolver`, `ExpandTrailersAsync`, `ExpandMediaSourceClipsAsync`) — die Mitgliedschafts- und Ring-Grundlage (`MixUnorderedRing`, `StrandOrderKey`, `ItemOrderKey`), gegen die `Validate` prüft.
- `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — ruft `ReconcileAsync` + `EnsureCoverageAsync` bei jedem Intervall-Tick (`ProgramSettingsService.GetProgramScheduleIntervalAsync`, Fallback 15 min) und zusätzlich bei jedem `IProgramScheduleSignal`-Aufwecken.
- `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (`IProgramScheduleSignal`) — Weckleitung; Quellen sind u. a. `ProgramRuleService` (Regel-CRUD), `ProgramClipGroupService`, `MediaDurationProbeService.ProbePendingAsync` (Signal je Batch mit geschriebener `Duration`; `MediaDurationProbeWorker` läuft im `MediaProbeOptions.EffectiveIntervalSeconds`-Takt, Standard 60 s, und arbeitet Backlog in Schleife ab), `MediaSourceScanService` (Scanende über `BeginScan`), `ApplicationDbContext` (SaveChanges-Hook `NotifyScheduleChanged` bei Backfill-Markierungen neuer Medien), Admin-UI (`TVSenderAdmin.razor` „Jetzt generieren"/Reset/Regel-Speichern, `ProgramSettings.razor` Einstellungs-Speichern).
- `VideoWebPlayer/Services/MediaDurationProbeService.cs` / `MediaDurationProbeWorker.cs` — kontinuierliche Signalquelle während der Laufzeit-Nachmessung.

### Datenmodell

- `VideoWebPlayer/Data/ProgramSlot.cs`, `VideoWebPlayer/Data/ProgramSlotStatus.cs` (`Scheduled`, `Displaced`), `ProgramRuleCursor` — voraussichtlich ohne Schemaänderung; `Displaced`-Zeilen bleiben Historie (`DeleteDisplacedSlotsAsync` räumt sie manuell ab).

### Interfaces

- `VideoWebPlayer/Services/IProgramScheduleService.cs` (`ReconcileAsync`, `EnsureCoverageAsync`, `GetScheduleAsync`, `ProgramScheduleReconcileSummary`) — voraussichtlich unverändert; die Idempotenz ist Verhalten, keine neue Schnittstelle.

### UI

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Programm-Übersicht rendert `GetScheduleAsync` (liefert `Scheduled` + `Displaced` gemeinsam) mit Chips „Verdrängt"/„Geplant"; hier wird die Duplikat-Serie sichtbar.

### Tests

- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs` und weitere `ProgramScheduleServiceTests_*.cs` — Bestand; neue Regressionstests für wiederholte `ReconcileAsync`-Läufe mit identischen Eingaben und für den Abgleich geplanter gegenüber verdrängten Einträge (SQLite-gestützt, gemäß DoD). Worker-/Signal-Tests zu `ProgramScheduleWorker`/`ProgramScheduleSignal`.

## Implementierungsansatz

1. **Reproduktion zuerst** (die Ursache ist im Issue bewusst ungeklärt): Einen Testfall bauen, der `ReconcileAsync` zweimal hintereinander mit unveränderten Eingaben laufen lässt — der zweite Lauf muss `SlotsDisplaced == 0` und `SlotsCreated == 0` liefern (echter Horizont-Ausbau gehört `EnsureCoverageAsync`, nicht `ReconcileAsync`). Schlägt er, liegt die Schleife im Abgleich; damit ist die Ursache eingrenzbar.
2. **Hypothesen zum Nicht-Idempotent-Sein des Abgleichs** (als Annahmen zu verifizieren, nicht als gesicherte Ursache):
   - Zeitabhängige Expansions-Eingaben: `ExpandMaterialAsync`-Overload ohne `coverageStart` fällt auf `DateTime.UtcNow` zurück (Zeile ~2111); `GenreCanonicalResolver.ResolveCanonicalNames(allRows, DateTime.UtcNow)` löst saisonale/versteckte Genres tagesabhängig auf — ein Slot kann so ohne Regeländerung „nicht mehr reproduzierbar" werden.
   - Repräsentantenwahl der Mediendatei: `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` bevorzugen Items mit gemessener `Duration` — schließt der `MediaDurationProbeWorker` eine Messung ab, kann das gewählte `MediaItemId` wechseln, die Mitgliedschafts-/Positionsprüfung von `AiringReproducible` scheitern und die Ausstrahlung verdrängt + mit neuer Datei neu materialisiert werden; das Probe-Signal feuert dabei pro Batch erneut → Verdrängungs-Serie während längerer Probe-Läufe.
   - Verschobene Ausstrahlungen: `ShiftPastInvaders` und die `_invaderStarts`-Evidenz erfordern einen belegten Slot am Nominalstart; wird der „Invader" selbst neu materialisiert oder verdrängt, kann der Folgelauf die Verschiebung nicht mehr beweisen und verdrängt erneut (der Code kommentiert dieses Oszillations-Risiko bereits, Zeilen ~205–210 und ~338–340).
   - `StrandStartOffset` rotiert den Start-Strang interleaver Regeln tagesbasiert; ein über Mitternacht verschobener Sendeblock könnte laufabhängig anders validieren.
   - Cursor-Rücklauf: `RewindCursor` setzt `NextIndex` auf den Vorgänger des frühesten verdrängten Inhalts — zu prüfen, ob die danach materialisierte Sequenz im nächsten Lauf beweisbar bleibt.
3. **Auslöser-Seite prüfen**: `ProgramScheduleWorker` bewertet bei jedem Tick und jedem Signal neu — das ist per Design, verstärkt aber jede Nicht-Idempotenz. Zu prüfen ist, ob Signale sachlich begründet sind bzw. zu fein feuern (z. B. `ProbePendingAsync` pro geschriebenem Batch statt gebündelt am Ende des Probe-Durchlaufs; `NotifyScheduleChanged` aus dem `SaveChanges`-Markierungs-Hook bei jedem Medienscan-Chunk). Eine gedrosselte/gebündelte Signalgabe ist zulässig, solange echte Änderungen weiterhin zeitnah wirken — darf die eigentliche Abgleich-Korrektur aber nicht ersetzen.
4. **Zielbild**: `ReconcileAsync` ist gegenüber gespeicherten `Scheduled`-Einträgen idempotent (kein Lauf verdrängt, was der vorige Lauf unter identischen Eingaben erzeugt hat); Verdrängte Historie entsteht nur bei echten Änderungen (Regel-CRUD, Medienzuwachs/-verlust, Freigabe-/Laufzeitänderung, Fenster-/Prioritätsänderung) und dann genau einmal je betroffenem Slot.
5. **Regressionstests**: wiederholtes `ReconcileAsync`/`EnsureCoverageAsync` mit identischen Eingaben; Wiederholte Worker-Signale bzw. Seitenaufruf ohne Änderung; Abgleich geplanter gegenüber verdrängter Einträge (keine „Verdrängt"-Serie desselben Titels hintereinander); mindestens ein Test mit echter Änderung, der die einmalige Ersetzung belegt; kaputtmachen-Beweis der Testwirksamkeit gemäß AGENTS.md §4/§7.

## Konfiguration

Keine neue Konfiguration erforderlich — Idempotenz ist gefordertes Standardverhalten, kein abschaltbares Feature. Bestehende, am Verhalten beteiligte Einstellungen: `ProgramScheduleInterval` (Worker-Takt), `ProgramScheduleHorizon`, `MediaProbeOptions.IntervalSeconds`/`BatchSize`/`RetryDelaySeconds` (Signal-Frequenz der Laufzeitproben), Jugendschutzfenster (`ProgramScheduleAgeWindowRule`). Eine Drosselung/Bündelung von Signalen wäre — falls umgesetzt — Verhalten von `ProgramScheduleSignal`/`MediaDurationProbeService`, kein neuer Schalter.

## Offene Fragen

- Reicht die fachliche Idempotenz (zweiter Lauf bei unveränderten Eingaben tut nichts) aus, oder soll zusätzlich ein „Eingabe-Fingerprint" die `ReconcileAsync`-Auswertung ganz überspringen, wenn sich nichts geändert hat? (Aufwands-/Risikoabwägung; die Akzeptanzkriterien verlangen nur Stabilität des Ergebnisses.)
- Werden die im Issue beobachteten Duplikate vollständig durch die Abgleich-Nicht-Idempotenz erklärt, oder steckt dahinter eine echte, sich wiederholende Eingabeänderung (z. B. laufende Laufzeit-Proben, saisonale Genre-Auflösung, Benutzer-Freigaben)? — intern über Log-Analyse (`[ProgramSchedule] Re-Bewertung: … verdrängt`) und Reproduktion zu klären.
- Soll die Anzeige historischer `Displaced`-Einträge angepasst werden (z. B. Gruppierung oder Filter in `TVSenderAdmin.razor`/`GetScheduleAsync`), oder bleibt die volle Historie gewünscht und es genügt, dass keine neuen Serien mehr entstehen? (Der manuelle Aufräum-Button `DeleteDisplacedSlotsAsync` existiert bereits.)
- Trifft die Annahme zu, dass ein erneuter Seitenaufruf den Plan nicht selbst verändert (`TVSenderAdmin.OnInitializedAsync` feuert kein Signal) — oder liegt ein indirekter Auslöser über die beim Seitenaufruf laufenden Hintergrunddienste vor?
