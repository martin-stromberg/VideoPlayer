# Anforderung — Issue #315

**Titel:** TV-Sender: Re-Bewertung invalidiert unveränderte Ausstrahlung (HeadPickInconsistent) und verwirft laufende Titel
**Quelle:** https://github.com/martin-stromberg/VideoPlayer/issues/315

## Problem

Die 15-minütige periodische Re-Bewertung (`ReconcileAsync`) invalidiert eine laufende
Ausstrahlung einer Genre-/Formatrotations-Regel wiederholt vollständig mit
`HeadPickInconsistent`, obwohl sich an den Regeln nichts geändert hat. Jede
Invalidierung verdrängt die komplette verbleibende Ausstrahlung, die sofort identisch
neu materialisiert wird (Selbstverdrängungs-Duplikate — beim Start bereits 328
bereinigt) und dabei den gerade laufenden Titel mitten in der Ausstrahlung verwirft.
Betroffene Kopf-Slots sind Clip-Slots; die ungeschriebenen Prefix-Picks eines Strangs
werden von der Reproduzierbarkeitsprüfung nicht rekonstruiert, weil der Strang im
Rest der Gruppe keinen geschriebenen Beitrag hat.

Folgeproblem: Nach dem ungewollten Slot-Wechsel zeigt der Player nur den Ladekreis —
die HLS/FFmpeg-Strecke verkraftet den Wechsel mitten in der Ausstrahlung nicht sauber.

## Erwartetes Verhalten

1. Ohne Regeländerung bleibt die verplante Ausstrahlung stabil — die Re-Bewertung
   füllt faktisch nur den freien Bereich am Planende.
2. Bereits ausgestrahlte Sendeplätze werden nicht erneut bewertet/neu geplant.
3. Der aktuell abgespielte Titel darf nicht verworfen werden — einzige legitime
   Ausnahme: geänderte Alterseinschränkungen, die den laufenden Titel ungültig machen.
4. Ein notwendiger Slot-Wechsel mitten in der Ausstrahlung muss die Stream-Strecke
   in einem abspielbaren Zustand hinterlassen (kein Dauer-Ladekreis).

## Akzeptanzkriterien

- Test: Re-Bewertung mit unveränderten Regeln lässt eine laufende Ausstrahlung
  (inkl. des aktiven Slots) unverändert — keine `HeadPickInconsistent`-Invalidierung.
- Test: Bereits ausgestrahlte Sendeplätze werden nicht erneut bewertet/neu geplant.
- Test: Der aktuell ausgestrahlte Titel wird nicht verworfen — außer einer geänderten
  Altersfenster-Einstellung macht ihn ungültig.
- Test: Ein notwendiger Slot-Wechsel mitten in der Ausstrahlung hinterlässt die
  HLS-Strecke in einem abspielbaren Zustand.
- Ursache von `HeadPickInconsistent` bei unveränderten Regeln gefunden und behoben.

## Nicht-Ziele

- Keine Änderung an der Materialisierungs-Strategie (Ring-Reihenfolge, Cursor-Modell).
- Keine Schemaänderung (keine Migration nötig — Stand bisher).
