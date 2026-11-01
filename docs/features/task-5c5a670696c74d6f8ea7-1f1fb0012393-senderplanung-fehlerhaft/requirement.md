# Übersetzte Anforderung: Senderplanung fehlerhaft

Quelle: `issue.md` (Aufgaben-ID `5c5a6706-96c7-4d6f-8ea7-1f1fb0012393`), Abschnitt „Anforderung"
samt Testszenarien, Soll-Entscheidungen und Prüfgrundsätzen. Branch:
`task/5c5a670696c74d6f8ea71f1fb0012393-senderplanung-fehlerhaft`.

## Fachliche Zusammenfassung

Die Zusammenstellung des Sendeplans (`ProgramScheduleService.EnsureCoverageAsync` /
`ReconcileAsync`) weicht an zwei Stellen vom Soll-Verhalten ab und wird dafür korrigiert:
Erstens darf der 24-h-Rückblick (`OccurrenceLookback`) nur noch laufende Ausstrahlungen
materialisieren — eine zum Planungszeitpunkt bereits beendete Occurrence verbraucht keine
Materialposition der Ringe/Cursor mehr. Zweitens muss der Planbestand verdrängungsfrei
bleiben, wenn die Materialmenge wächst (neues Genre an der Regel, neuer Film im Bestand):
kein bereits geplanter `ProgramSlot` mit `Status == Scheduled` wird verworfen, verschoben
oder ersetzt. Das übrige Soll-Verhalten (Karenzzeit, Overshoot über das Periodenende,
Clip-Füllung, Altersfenster, lokale Sendezeit, Rotationsfortschritt) ist durch die
konkreten Testszenarien festgeschrieben und gilt als verbindliche Regressionssuite.

## Akzeptanzkriterien (aus der Anforderung abgeleitet)

Vorgegebenes Szenario-Setup (für alle Tests gleich): Regel „Primetime" —
`RuleType` Genre-/Formatrotation, `Recurrence` täglich, `TimeOfDay` 20:15 lokal,
`SlotDuration` 225 Minuten (nominales Fenster 20:15–00:00), uneingeschränkte Gültigkeit,
Materialauswahl 21 Genres über eine Quelle, zwei `ProgramRuleClipGroup`s (eine eigene
Clip-Gruppe plus System-Clip-Gruppe „Trailer", `ProgramClipGroup.TrailerGroupId`),
`PlaceholderGrace` 15 Minuten, Planungshorizont 7 Tage.

1. **Grundverhalten / Karenzzeit:** Jeden Abend um 20:15 wird eine Primetime-Ausstrahlung
   eingeplant. Ein begonnener Film läuft immer zu Ende, auch über das nominale
   Periodenende 00:00 hinaus (Overshoot). Ein neuer Film darf nur starten, wenn bis zum
   Periodenende mindestens `PlaceholderGrace` (15 Minuten) Restlaufzeit verbleibt; bei
   **exakt** 15 Minuten Rest startet noch ein Film, erst darunter wird der Rest mit
   Clips gefüllt. Außerhalb der Primetime stehen nur noch innerhalb der Primetime
   gestartete Filme im Plan.
2. **Rollierende Generierung (Szenario 1.a):** Über den vollen 7-Tage-Horizont wird der
   Zeitverlauf durch mehrere Läufe von `ReconcileAsync` + `EnsureCoverageAsync` mit
   fortschreitendem `utcNow`-Parameter simuliert. Bereits geplante Titel bleiben bei
   jedem Folgelauf unverändert; es entstehen ausschließlich neue Planungen am Planende.
   Kein geplanter Slot wird verdrängt, verschoben oder durch anderen Inhalt ersetzt.
3. **Restlaufzeit < Karenzzeit (1.b):** Bleibt nach zwei Filmen weniger als 15 Minuten
   bis 00:00, werden genau zwei Filme verplant; die Restlaufzeit wird mit Clips aus den
   konfigurierten Clip-Gruppen gefüllt — kein dritter Film startet.
4. **Restlaufzeit > Karenzzeit (1.c):** Bleiben mehr als 15 Minuten, wird ein dritter
   Film verplant, der noch innerhalb der Primetime startet und über 00:00 hinaus zu
   Ende läuft. Nach seinem tatsächlichen Ende folgen **keine** Clips der abgelaufenen
   Periode mehr; die nächste Primetime-Ausstrahlung startet am Folgetag sauber um 20:15.
5. **Materialwachstum ohne Verdrängung (1.d, 1.e):** Wird der Regel während der
   Wochensimulation ein weiteres Genre hinzugefügt bzw. wächst die Materialmenge um
   einen neuen Film in einem der 21 Genres, bleiben alle bereits verplanten Slots
   erhalten — keine Verdrängung; nur neue Planungen berücksichtigen die erweiterte
   Materialmenge.
6. **Regel während laufender Periode angelegt (1.f):** Wird die Regel z. B. um 20:45
   erstellt (Tages-Occurrence 20:15 liegt in der Vergangenheit, die Ausstrahlung liefe
   noch), wird der Plan rückwirkend ab 20:15 materialisiert — die Ausstrahlung erscheint
   mit ihrem echten Start.
7. **Altersfenster (1.g):** Ein zur Startzeit nicht altersgerechter Titel (z. B. FSK 18
   um 20:15, Nachtfenster ab 23:00) wird übersprungen und darf starten, sobald sein
   Freigabe-Zeitpunkt die tatsächliche Startzeit erlaubt. Ein legal gestarteter Titel
   läuft über die Fenstergrenze hinaus zu Ende — die Prüfung gilt am Start, nicht
   während der Laufzeit. Geordneter Strang (`PreservesOrder`: `TVShowEpisode`) —
   gesperrter Kopf-Titel blockiert die Fortsetzung (Deferral); ungeordneter Ring
   (Filme) — Titel wird übersprungen, die Position gilt als verbraucht.
8. **Titel ohne Altersangabe (1.h):** Abhängig von `ProgramScheduleUnknownRatingMode`:
   `Unrestricted` → der Titel darf um 20:15 starten; `RestrictToNightWindow` → derselbe
   Titel darf erst ab der Freigabezeit des Nachtfensters starten. Beide Modi sind
   eigene Testfälle.
9. **Trailer-Vorlauf (1.i):** Zu einem geplanten Film laufen dessen Trailer im
   Lead-Fenster **vor** dem Film; Trailer zu nicht geplanten Filmen erscheinen nie im
   Sendeplan.
10. **Clip-Verteilung (1.j):** Die Restlücke unter der Karenzzeit wird Round-Robin aus
    beiden konfigurierten Clip-Gruppen gefüllt; zwischen Clip-Füllung und Periodenende
    startet kein Primetime-Film (kein Clip darf einen Film-Start vortäuschen).
11. **Rotationsfortschritt (1.k):** Tag 2 startet die Genre-Rotation nicht von vorn —
    die Strang-Cursor (`ProgramRuleCursor` je `StrandKey`) schreiten über die Woche
    fort; die strukturelle Slot-Position der Stränge bleibt je Abend gleich. Bei
    Erschöpfung eines Rings: sauberer Wrap-around ohne Positions- oder Episodensprung.
12. **Lokale Sendezeit (1.l):** Die Ausstrahlung beginnt jeden Tag um 20:15 lokaler
    Zeit — einschließlich des Tags der Zeitumstellung. Kein doppelter Slot, keine
    Lücke, keine verschobene Sendezeit.
13. **Rückblick nur laufende Sendungen (Soll-Entscheidung):** Der 24-h-Rückblick
    materialisiert nur noch laufende Ausstrahlungen (Start vor `utcNow`, Ende nach
    `utcNow`). Eine Occurrence, deren Ausstrahlung zum Planungszeitpunkt bereits beendet
    wäre, verbraucht keine Materialposition — sie darf die Rotation nicht vorverbrauchen.
14. **Prüfgrundsätze:** Geprüft wird der gesamte erzeugte Slot-Bestand (jeder Slot mit
    Start, Ende, Inhalt, Regelbezug), nicht nur Positivbeispiele. Der Negativraum wird
    mitbehauptet: zwischen Ende einer Ausstrahlung (inkl. Overshoot) und dem nächsten
    20:15 startet kein Film der Primetime-Regel neu (nur Clip-Füller bzw. Fortsetzung
    des begonnenen Films); ein wegen der Karenzzeit nicht gestarteter Film taucht an
    dem Abend nirgends auf. Die Tests laufen gegen echtes SQLite und die produktiven
    Services; der Zeitverlauf wird ausschließlich über den `utcNow`-Parameter gesteuert
    — keine Testdoublen in der Planlogik.

## Betroffene Klassen und Komponenten

- **Logik/Services:** `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`)
  — voraussichtlich betroffene Stellen: `EnsureCoverageAsync` (Occurrence-Aufbau inkl.
  `OccurrenceLookback` über `BuildOccurrences`, Materialisierung in
  `MaterializeOccurrence`), `ReconcileAsync` samt Reproduzierbarkeitsprüfung
  `AiringReproducible` / `FlatAiringTailReproducible` / `InterleavedAiringTailReproducible`,
  Ringaufbau und -mischung (`RuleState.SetItems`, `MixUnorderedRing`, `ItemOrderKey`,
  `StrandOrderKey`, `OtherStrandKey`), Cursor-Führung (`NextIndexFor`, `SelectStrandCursor`,
  `PersistCursors`, `RewindCursor`/`RewindRing`), Titelauswahl (`SelectAiringItems`,
  `SelectAiringItemsInterleaved`, `CommitTaken`/`CommitScan`) sowie Clip-Füllung
  (`InsertClipFill`, `FillGaps`, `ClipGroupState`, `BuildClipGroupStates`,
  `ExpandClipGroupStatesAsync`, `ExpandTrailerStatesAsync`, `FindLeadMatchOffset`).
- **Interface:** `IProgramScheduleService` — Signaturen bleiben (`utcNow`-Parameter ist
  der vereinbarte Zeitsteuerungspunkt); ggf. XML-Doku der veränderten Semantik.
- **Datenmodell:** bestehende Entitäten `ProgramRule` (`PlaceholderGrace`, `SlotDuration`,
  `ClipGroups`, `Genres`, `MediaSourceFilters`), `ProgramSlot` (`Status`, `IsClipFill`,
  weiche Referenzen `ProgramRuleId`/`MovieId`/`TVShowEpisodeId`/`MediaItemId`),
  `ProgramRuleCursor` (`StrandKey`, `ProgramCursorContentType`), `ProgramClipGroup`
  (`TrailerGroupId`). Keine Schemaänderung aus der Anforderung ableitbar.
- **Konfiguration/Enums (nur lesend betroffen):** `ProgramScheduleOptions`
  (`MinStartRemainSeconds`/`EffectiveMinStartRemain`, `MaxOccurrenceLookbackDays`,
  `TrailerLeadDays`), `ProgramScheduleUnknownRatingMode`
  (`Unrestricted`/`RestrictToNightWindow`), Altersfenster-Logik
  (`ProgramScheduleAgeWindowRule`, `ProgramScheduleAgeWindowSettings`,
  `ProgramScheduleAgeWindow`).
- **Worker:** `ProgramScheduleWorker` — Aufrufreihenfolge `PurgeExpiredRulesAsync` →
  `CleanupSelfDisplacedSlotsAsync` → `ReconcileAsync` → `EnsureCoverageAsync` ist die
  Referenz für die Test-Simulation (`StepAsync` im Test-Helper).
- **Tests:** bereits vorhandene, uncommittete Testartefakte —
  `VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs` (Seeds, Wochensimulation `StepAsync`,
  Vollbestand-/Negativraum-Assertions) und fünf Testdateien
  `ProgramScheduleServiceTests_PrimetimeRolling.cs`, `_PrimetimeChanges.cs`,
  `_PrimetimeGrace.cs`, `_PrimetimeAgeWindow.cs`, `_PrimetimeTrailer.cs`
  (16 Tests). Diese sind die verbindliche Soll-Spezifikation; bei Abweichung wird die
  Programmlogik korrigiert, nicht der Test.

## Implementierungsansatz

- **Korrektur 1 — Rückblick:** In `EnsureCoverageAsync` werden Occurrences aktuell mit
  `OccurrenceLookback` (24 h) hinter `coverageStart` aufgebaut und materialisiert. Soll:
  Eine Occurrence im Rückblick wird nur noch materialisiert, wenn ihre Ausstrahlung zum
  Planungszeitpunkt noch läuft (Start < `utcNow`, erwartetes Ende > `utcNow`). Eine
  bereits beendete Occurrence erzeugt weder Slots noch Cursor-Verbrauch — die Strang-
  und Clip-Ringe dürfen dafür nicht weitergeschaltet werden (Cursor-Verbrauch erfolgt
  über `Commit`/`CommitScan` in `MaterializeOccurrence`).
- **Korrektur 2 — Verdrängungsfreiheit bei Materialwachstum:** `ReconcileAsync` markiert
  Slots als `Displaced`, deren Ausstrahlung nicht mehr reproduzierbar ist
  (`AiringReproducible`). Bei Genre-Regeln ist der gemeinsame Film-Strang (`other`) ein
  ungeordneter Ring, dessen Reihenfolge per Hash-Mischung (`MixUnorderedRing`/
  `ItemOrderKey`) über die Item-Menge bestimmt wird; ein neues Item verändert diese
  Ordnung, sodass die Reproduzierbarkeitsprüfung fehlschlagen und der Suffix verdrängt
  werden kann. Die Korrektur muss Bestandsslots gegen Mengenwachstum immun machen —
  ob über einfügungsstabile Ringpositionen, eine wachstumstolerante
  Reproduzierbarkeitsprüfung oder Cursor-Ankerung ist Entwurfsentscheidung des
  Umsetzungsplans; Bedingung: kein `Scheduled`-Slot wird verworfen, neue Planungen
  nutzen die erweiterte Menge.
- Alle übrigen Szenarien sind bereits Soll und dienen als Regressionsschutz — die
  Korrekturen dürfen sie nicht brechen (insbesondere Karenzzeit-Grenzfall exakt
  15 Minuten, Overshoot, Clip-Round-Robin, Altersfenster-Semantik geordnet/ungeordnet,
  Trailer-Lead, DST-Stabilität, rückwirkende Planung ab `TimeOfDay`).
- Die Zeitsteuerung erfolgt ausschließlich über den `utcNow`-Parameter von
  `EnsureCoverageAsync`/`ReconcileAsync`; die `DateTime.UtcNow`-Stellen im Service sind
  nur Metadaten (`CreatedAt`/`UpdatedAt`) bzw. Regel-Diagnose und bleiben unangetastet.
  Ein injizierter `TimeProvider` ist nicht nötig (vorhandene Test-Helper
  `ManualTimeProvider`/`ControllableTimeProvider` wären nur für Worker-Tests relevant).
- Lauf-Reihenfolge der Simulation entspricht dem Worker: erst `ReconcileAsync`, dann
  `EnsureCoverageAsync` mit demselben `utcNow`.

## Konfiguration

Keine neue Konfiguration erforderlich. Verbindlich wirksame bestehende Einstellungen:
`ProgramRule.PlaceholderGrace` (pro Regel, Default `DefaultPlaceholderGrace` = 15 min,
wirksam als `RuleState.ClipGrace` nur bei Slot-Dauer-Regeln mit Clip-Gruppen),
`ProgramScheduleOptions.MinStartRemainSeconds` (anwendungsweit, Default 30 s),
`MaxOccurrenceLookbackDays` (Reconcile-Rückblick, Default 7 Tage), `TrailerLeadDays`
(Default 7 Tage), `ProgramScheduleUnknownRatingMode` (Anwendungseinstellung über
`Setup`/`ProgramSettingsService`), Planungshorizont über
`ProgramSettingsService.GetProgramScheduleHorizonAsync` (Tests: 7 Tage). Die Sendezeit
ist lokale Zeit (`TimeZoneInfo.Local` in `BuildOccurrences`).

## Offene Fragen

- Für Korrektur 2 ist offen, an welcher Stelle die Verdrängungsfreiheit hergestellt wird
  (einfügungsstabile Ringordnung vs. wachstumstolerante Reproduzierbarkeitsprüfung) —
  bewusst als Entwurfsentscheidung in die Umsetzungsplanung verwiesen; Randbedingung:
  deterministische, prozessinvariante Ordnung (aktuell FNV-1a-Hash-Mischung) und
  Bestandsschutz persistierter `ProgramRuleCursor`-Positionen müssen erhalten bleiben.
- Klarzustellen in der Planung: ob die Lookback-Änderung ausschließlich die
  Materialisierung in `EnsureCoverageAsync` betrifft oder auch die Nutzung von
  Lookback-Occurrences als Reproduktionsanker in `ReconcileAsync`
  (`MaxOccurrenceLookbackDays`-Fenster) angepasst werden muss; laut Anforderung gilt
  „beendete Occurrences verbrauchen keine Materialposition" — ein rein lesender Anker
  ohne Cursor-Verbrauch wäre davon nicht erfasst.
