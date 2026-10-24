# Übersetzte Anforderung: TV-Sender – Airings > 24 h bleiben in `AiringReproducible` nicht invalid (Issue #261)

Quelle: `issue.md` (Issue #261 „TV-Sender: Airings > 24h bleiben in AiringReproducible invalid", Aufgaben-ID `696fbc13-4076-46bd-ab98-b6453bd37331`, Nachbesserung aus Issue #111 / PR #257).

## Fachliche Zusammenfassung

Die Sendeplan-Nachführung (`ProgramScheduleService.ReconcileAsync`) wertet bestehende Airings zu Unrecht als nicht reproduzierbar, sobald ihr realer Beginn mehr als 24 Stunden vor `coverageStart` liegt: `BuildOccurrences` erzeugt die nominellen Sendetermine (`Occurrences`) einer `ProgramRule` nur im Fenster `coverageStart - OccurrenceLookback` (fest `TimeSpan.FromHours(24)`, `ProgramScheduleService.cs` Z. 26), sodass `AiringReproducible`/`AiringReproducibleInterleaved` für solche Airings keinen passenden `occurrenceStarts`-Treffer mehr finden. Die Folge: fachlich korrekte Airings werden als `ProgramSlotStatus.Displaced` markiert und neu materialisiert. Das Verhalten ist dahin zu korrigieren, dass lange noch laufende Airings (z. B. Marathon-/Special-Blöcke oder sehr lange Filme) reproduzierbar bleiben, ohne die Verdrängung echt veralteter Airings aufzuweichen.

## Akzeptanzkriterien (aus der Anforderung abgeleitet)

1. **Lange Airings bleiben gültig:** Ein bestehendes Airing, dessen realer Beginn mehr als 24 Stunden vor `coverageStart` liegt und das unter den aktuellen Regeln reproduzierbar wäre, wird von `ReconcileAsync` nicht mehr als invalid verdrängt (kein `Displaced`, keine Neu-Materialisierung). Abgesichert durch einen reproduzierenden Test mit einem > 24-h-Airing.
2. **Keine Regression bei echter Veraltung:** Airings, die sich tatsächlich nicht mehr reproduzieren lassen (geänderte `ProgramRule`, geänderte `TimeOfDay`/Verschiebe-Evidenz, inaktive oder gelöschte Regel, geändertes Material), werden weiterhin korrekt als `Displaced` markiert und neu eingeplant.
3. **Beide Prüfpfade:** Die Korrektur greift in `AiringReproducible` (Ein-Strang-Regeln) und `AiringReproducibleInterleaved` (mehrsträngige/Interleaved-Regeln) gleichermaßen.
4. **Konsistente Fenster:** Occurrence-Fenster und Lade-Fenster der geplanten `ProgramSlot`s (`loadFrom = coverageStart - OccurrenceLookback`, Z. 175) bleiben konsistent — ein vergrößertes Reproduzierbarkeitsfenster darf nicht daran scheitern, dass Kopf-Slots oder Verschiebe-Evidenz (`invaderStarts`) des Airings außerhalb des Lade-Fensters liegen.

## Betroffene Klassen und Komponenten

- **Logik/Services:** `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - Konstante `OccurrenceLookback` (Z. 26) — zentrale Stelle des Bugs.
  - `BuildOccurrences` (Z. 1093, Fensterfilter Z. 1119) — erzeugt die `Occurrence`s; wird von `EnsureCoverageAsync` (Z. 113) und `ReconcileAsync` (Z. 188) gemeinsam genutzt.
  - `ReconcileAsync` (Z. 134–356) — `loadFrom`-Berechnung (Z. 175), Aufbau von `occurrenceStarts` (Z. 189–191) und `invaderStarts` (Z. 184–186), `GroupAirings` (Z. 196), `Displace`-Logik (Z. 202–240).
  - `AiringReproducible` (Z. 478) und `AiringReproducibleInterleaved` (Z. 557) — prüfen ausschließlich gegen `occurrenceStarts` aus dem Lookback-Fenster.
  - Hilfsmethoden im selben Kontext: `GroupAirings` (Z. 445), `SelectAiringItems`/`SelectAiringItemsInterleaved`, `BoundaryAfter`, `ShiftPastInvaders`, `RewindCursor`.
- **Interfaces:** `IProgramScheduleService` — voraussichtlich unverändert (interne Logikkorrektur).
- **Konfiguration (optional):** `ProgramScheduleOptions` (`VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`, `appsettings`-Abschnitt `ProgramSchedule`) — mögliche Stelle für eine konfigurierbare Obergrenze des Lookbacks, falls im Lösungsweg vorgesehen.
- **Aufrufer:** `ProgramScheduleWorker` (`VideoWebPlayer/Services/ProgramScheduleWorker.cs` Z. 71–72) — ruft `ReconcileAsync` vor `EnsureCoverageAsync` im Intervall; keine Änderung erwartet.
- **Tests:** `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs` (Reconcile-Suite) sowie ggf. `ProgramScheduleServiceTests_Interleaving.cs` für den Interleaved-Pfad; neuer reproduzierender Test für ein > 24-h-Airing.

## Implementierungsansatz

Zwei in der Anforderung genannte, nicht ausschließende Varianten; die Auswahl ist im Umsetzungsplan zu treffen:

- **Variante A – dynamischer Lookback:** `OccurrenceLookback` wird nicht mehr fest auf 24 h gesetzt, sondern so weit rückwirkend gewählt, wie das älteste noch laufende Airing reicht (z. B. abgeleitet aus dem frühesten `Start` noch relevanter `ProgramSlot`s), ggf. mit konfigurierbarer Obergrenze. Zu beachten: `BuildOccurrences` wird auch von `EnsureCoverageAsync` genutzt — ein vergrößertes Occurrence-Fenster darf dort nicht dazu führen, dass sehr alte Occurrences neu materialisiert werden; ggf. ist der Lookback je Aufrufstelle zu parametrisieren. Parallel ist das Slot-Ladefenster (`loadFrom`, Z. 175) mit zu verbreitern, damit Kopf-Slots und `invaderStarts`-Evidenz des Airings vollständig geladen werden (vgl. Kommentar Z. 170–174).
- **Variante B – Toleranz statt Fenstererweiterung:** Airings, deren rekonstruierter realer Beginn vor dem Occurrence-Fenster liegt, werden gesondert behandelt (z. B. gegen historische Occurrences oder mit abgeschwächter Prüfung toleriert), statt bei fehlendem `occurrenceStarts`-Treffer pauschal invalid zu werten. Risiko: Die `invaderStarts`-/Verschiebe-Logik verhindert bewusst, dass frühere Anker „stale" Airings retten — eine Toleranz darf diese Schutzfunktion nicht aushebeln.

In beiden Varianten gilt: Zuerst einen reproduzierenden Test schreiben, der das Fehlverhalten zeigt (Airing mit realem Beginn > 24 h wird fälschlich verdrängt), dann die Korrektur umsetzen; die bestehenden Reconcile-Regressionstests (veränderte Regel, geänderte Uhrzeit, Cursor-Rewind) müssen grün bleiben.

## Konfiguration

Keine zwingend erforderlich. Falls Variante A mit Obergrenze umgesetzt wird: neue Option in `ProgramScheduleOptions` (z. B. maximale Lookback-Dauer) mit `Effective…`-Normalisierung und Default-Fallback nach dem vorhandenen Muster (`TrailerLeadDays`, `MinStartRemainSeconds`).

## Offene Fragen

1. **Variantenwahl:** Dynamischer Lookback (A), gesonderte Toleranz (B) oder eine Kombination? Die Anforderung nennt beide Varianten gleichwertig — Entscheidung im Umsetzungsplan zu treffen (Implementierungsdetail, keine Kundenrückfrage nötig).
2. **Obergrenze des Lookbacks:** Soll das dynamische Fenster nach oben begrenzt werden (konfigurierbar über `ProgramScheduleOptions` oder feste Konstante), um Lade- und Occurrence-Aufwand bei pathologisch langen Airings zu deckeln?
3. **Geltungsbereich des erweiterten Fensters:** Nur `ReconcileAsync` (Reproduzierbarkeitsprüfung) oder auch `EnsureCoverageAsync`? Letzteres würde sehr alte Occurrences neu materialisieren — vermutlich unerwünscht, daher vermutlich je Aufrufstelle zu unterscheiden (Annahme, im Plan zu verifizieren).
