# Übersetzte Anforderung: TV-Sender — Platzhalter-Clips innerhalb der Sendeperiode verteilen und Zeitüberlappungen vermeiden

**Quelle:** Issue #291 (Fehler in der mit #277 eingeführten Verteilung; #289 betrifft nur die kompakte Darstellung)

## Fachliche Zusammenfassung

Der Clip-Füll-Pass `InsertClipFill`/`FillAiringSegments` im `ProgramScheduleService` verteilt Platzhalter-Clips (`ProgramClipGroup`-Ringe) in die n+1 Segmente einer Ausstrahlung einer `ProgramRule` mit `SlotDuration`. Die Umsetzung aus #277 weicht in drei Punkten vom geforderten Verhalten ab:

1. **Falsches Periodenende:** `FillAiringSegments` berechnet das Periodenende als `airing[0].Start + SlotDuration`. `airing[0].Start` ist der *reale* Start des ersten geschriebenen Sendeplatzes — der kann hinter dem nominellen Occurrence-Start liegen (verschobener Start hinter einem überschießenden Vorgänger via `ShiftPastInvaders`, oder Teil-Ausstrahlung, deren Kopf vor `coverageStart` endete). Dadurch wird `rest` zu groß und die Periode läuft rechnerisch zu spät — Clips werden Stunden nach dem echten Periodenende eingeplant.
2. **Überlappungen durch das Neu-Verlegen der Titel:** Beim Neuverlegen werden Titel ohne jede Grenzprüfung hinter die eingeschobenen Clips gesetzt. Ragt die verschobene Kette in belegte Intervalle (`blocked`) oder reservierte Bound-Fenster höherer Occurrences, entstehen überlappende Sendeplätze.
3. **Erster Tail-Clip ignoriert `tailLimit`:** Im Tail-Segment wird der erste Clip gegen `periodEnd` statt `tailLimit` geprüft (`pos + item.Duration <= (placed > 0 ? tailLimit : periodEnd)`) — er kann belegten Raum zwischen `pos` und `periodEnd` überdecken.

Gefordert ist die zeitlich konsistente Verteilung: Clips nur in freiem Raum innerhalb der Periode (Occurrence-Start + `SlotDuration`), keine Überlappungen, keine neuen Clip-Blöcke nach dem Periodenende; der nach #277 erlaubte Überhang eines bereits begonnenen Clips bleibt bestehen.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - `FillAiringSegments` — Kern der Korrektur (Periodenende, Grenzprüfung der Neuverlegung)
  - `TailLimit` / neue Begrenzungs-Hilfsfunktion — Ermittlung des freien Raums hinter der Ausstrahlung
  - `InsertClipFill` — unveränderte Orchestration, ggf. Signatur- bzw. Kontexterweiterung
  - `ClipFillContext` — um die Occurrence-Starts je Regel ergänzen (Herleitung des nominellen Periodenstarts)
  - Aufrufer `EnsureCoverageAsync` und `ReconcileAsync` — Occurrence-Starts aus `occurrences` bzw. `window.Occurrences` in den Kontext geben
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs` bzw. neue Testdatei — Regressionstest gemäß Akzeptanzkriterien
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` — vorhandene Seed-Helfer reichen (CreateRuleAsync mit `clipGroupIds`, `placeholderGrace`, `slotDuration`; `CreateMovieAsync`, `CreateMediaItemAsync`, `AddClipToGroupAsync`)

Keine Datenmodell- oder Schemaänderungen, keine EF-Migration, keine UI-Änderungen.

## Implementierungsansatz

- Occurrence-Starts je Regel im `ClipFillContext` bereitstellen (`IReadOnlyDictionary<long, List<DateTime>>`, aus `occurrences`/`window.Occurrences` inkl. Lookback). In `FillAiringSegments` den nominalen Periodenstart als letzten Occurrence-Start `<= airing[0].Start` bestimmen; `periodEnd = periodStart + SlotDuration`. Findet sich kein Occurrence-Start, wird die Ausstrahlung unverändert übernommen (keine Clip-Füllung).
- Frei-Raum-Grenze für die Neuverlegung: nächster belegter Start bzw. nächster Bound-Start nach dem ursprünglichen Ende der Ausstrahlung (analog `TailLimit`, aber ohne Prioritätsfilter — Bound-Starts jeder Priorität begrenzen die Kette). Verteilt wird nur `min(rest, freier Raum)`; Titel werden verschoben, ohne diese Grenze zu überschreiten — kein Titel darf in belegten oder einer anderen Occurrence vorbehaltenen Raum ragen.
- Tail-Segment: Clips müssen vor dem Periodenende **und** vor der Ketten-Grenze beginnen (`pos < periodEnd && pos < Grenze`), ihr Ende bleibt durch `tailLimit` (belegt/höherpriores Bound/Horizont) gedeckelt — Überhang eines begonnenen Clips bleibt möglich, Überlappung mit Belegtem ausgeschlossen.
- Regressionstest: Regel 20:15 Uhr, 240 Minuten, mehrere Titel, viele kurze Clips; prüft Verteilung über die Segmente, Überlappungsfreiheit gegenüber belegtem Raum einer zweiten Regel innerhalb der Periode sowie das Einhalten des Periodenendes über Mitternacht — inkl. des Falls, dass der Lauf mitten in der Periode startet (Teil-Ausstrahlung).

## Konfiguration

Keine neue Konfiguration. `PlaceholderGrace`, `ProgramScheduleOptions` (Horizont, `MinStartRemainSeconds`, `MaxOccurrenceLookbackDays`) bleiben unverändert wirksam.

## Offene Fragen

Keine — Beobachtung, Sollverhalten und Akzeptanzkriterien sind im Issue eindeutig.
