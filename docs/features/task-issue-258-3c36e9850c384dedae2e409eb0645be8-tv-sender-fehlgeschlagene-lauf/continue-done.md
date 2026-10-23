# Offene Aufgaben

Erstellt am: 2026-10-23
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [x] `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs` (Z. 139–141) — Der Kommentar in `ExecuteAsync_FeatureNotReleased_LeavesItemsUnprobed` behauptet, die 3s-Wartezeit decke „einen zweiten Zyklus" ab — bei `IntervalSeconds = 60` läuft im Testfenster aber nur ein Durchlauf; die Flag-Auswertung pro Zyklus ist damit nicht abgedeckt. Empfehlung: `IntervalSeconds = 1` in den Test-Options setzen oder den Kommentar korrigieren.
  - **Erledigt:** `IntervalSeconds = 1` gesetzt und Test in `ExecuteAsync_FeatureFlagEvaluatedPerCycle` umbenannt/erweitert: nach 3 s Off-Phase (mehrere Zyklen ohne Probe) wird das Flag zur Laufzeit eingeschaltet und verifiziert, dass der Worker es im nächsten Takt ohne Neustart auswertet und das Item misst. Sabotage-Check (Flag nur einmal beim Start lesen) schlägt den Test erwartbar fehl.

## Usability-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.
