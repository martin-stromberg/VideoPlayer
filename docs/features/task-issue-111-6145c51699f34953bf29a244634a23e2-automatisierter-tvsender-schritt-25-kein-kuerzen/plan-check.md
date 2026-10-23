# Plan-Check — N22

**Status: Plan vollständig**

## Geprüfte Risiken / Selbstkritik

1. **Loop-Terminierung:** Bisherige `consumed <= count`-Schranke entfällt. Bei `SlotDuration`
   (`wanted = int.MaxValue`) und Items mit `Duration <= 0` (defekter Probe-Wert) drohte eine
   Endlosschleife — der Plan verlangt deshalb einen expliziten Fortschritts-Abbruch
   („voller Ring ohne Zeitfortschritt → break") in beiden Selektionsfunktionen. ✅ im Plan
   ergänzt (Umsetzungshinweis).
2. **`AiringReproducible` muss verschobene Airings akzeptieren** — kritischster Punkt:
   Ohne Anker-Lockerung (`o <= airing[0].Start − skipped` statt `==`) und Simulation am
   reellen Start würde Reconcile jedes legitime Shift-Airing verdrängen → Oszillation.
   Plan deckt beide Varianten (flat + interleaved) ab. ✅
3. **`minRemain` ist instanz-konfiguriert, `SelectAiringItems`/`AiringReproducible` statisch** →
   `TimeSpan`-Parameter wird durch alle statischen Helfer weitergereicht (Feld `_minStartRemain`
   analog `_trailerLead`). ✅
4. **Invasor-Quellen:** Nicht nur `blocked`/Bestand, auch `newSlots` (höherpriore Ausstrahlung
   desselben Laufs) können den Occurrence-Start überragen → Invasor-Erkennung vor der Auswahl
   in beiden Materialisierungspfaden. ✅ im Plan.
5. **`DisplaceAiringSuffix` wird mit dem Invasor-Zweig vollständig tot** — im Reconcile sind
   Loser mit `Start < occ.Start` unmöglich (Ranges beginnen hinter Invasoren) → entfernen
   statt tot mitführen. ✅
6. **Kaskaden-Drift** (Überstand verschiebt Folge-Occurrence, die selbst überzieht) ist
   gewollt und durch Fenster-Besitz begrenzt: ein Airing darf nur im eigenen Fenster
   (`bound` ab nominalem Start) schieben, sonst entsteht nichts. ✅
7. **Kein UI-Befund:** Keine `.razor`-Änderung → Usability-Review als „nicht anwendbar"
   dokumentiert.
8. **Test-Abdeckung deckt alle Anforderungs-Pflichtfälle ab** (3-min-Rest-Überlauf, Schwelle,
   Shift-Stabilität, Invasor bleibt) plus harte Grenze und Options-Normalisierung.

## Restrisiko (bewusst akzeptiert)

- `BoundStarts` zählt Trailer-Regeln zur Occurrence-Materialisierung nicht (leere Expansion) —
  Überstand in Trailer-Fenster ist zulässig; im Reconcile bounden sie. Asymmetrie wie bisher.
