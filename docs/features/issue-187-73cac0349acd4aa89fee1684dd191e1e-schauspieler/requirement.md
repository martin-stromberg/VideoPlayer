# Anforderung: Schauspieler

**Aufgaben-ID:** 73cac034-9acd-4aa8-9fee-1684dd191e1e  
**Branch:** `task/issue-187-73cac0349acd4aa89fee1684dd191e1e-schauspieler`  
**Erstellt:** 2026-08-30

## Hintergrund

Aktuell werden Schauspieler-Informationen aus den Metadatendateien der Videos nicht erfasst. Es gibt keine Möglichkeit, in der Anwendung zu sehen, welche Schauspieler in welchen Filmen, Filmsammlungen, Serien, Staffeln oder Episoden mitwirken, und keine Möglichkeit, gezielt nach Schauspielern zu suchen oder zu filtern.

## Ziel

Schauspieler aus den Video-Metadaten automatisch erfassen, persistieren, durchsuchen und in einer eigenen Übersicht inklusive Detailansicht präsentieren.

## Akzeptanzkriterien

### Datenerfassung

- Beim Erfassen/Scannen von Videos werden Schauspieler aus den Metadatendateien (z. B. `.nfo`) ausgelesen und in der Datenbank gespeichert.
- Für jeden Schauspieler werden Name und eindeutige Identifikation gespeichert.
- Die Zuordnung von Schauspielern zu Filmen bzw. Episoden wird persistiert.
- Backup & Restore sichern und stellen Schauspielerdaten inklusive deren Zuordnungen wieder her.

### Nacherfassung des Altbestands

- Für bereits erfasste Videos wird eine Kennung eingeführt, die angibt, ob die Schauspieler-Erfassung für dieses Video bereits erfolgt ist.
- Beim Programmstart läuft im Hintergrund eine Aktualisierung, die für den Altbestand die Schauspieler nachträglich erfasst.
- Die bestehenden Methoden des regulären Scans werden wiederverwendet, um Doppelcode zu vermeiden.
- Wird die Nacherfassung unterbrochen, wird sie beim nächsten Start mit den noch offenen Videos fortgesetzt.

### Menü & Übersicht

- Es gibt einen neuen Menüpunkt "Schauspieler" im Programmmenü.
- Die Übersicht zeigt eine Liste der Schauspieler, analog zur bestehenden Ansicht der Quellen.
- Eine Suche nach Schauspielern ist möglich.
- Statt Genre-Filter werden Filter nach Anfangsbuchstaben angeboten – aber nur für Buchstaben, zu denen tatsächlich Schauspieler vorhanden sind.

### Detailansicht Schauspieler

- Detailansicht zeigt Bild und Namen des Schauspielers.
- Sie listet Filmsammlungen bzw. Filme sowie Serien, Staffeln oder Episoden, in denen der Schauspieler mitwirkt.
- **Zuordnungslogik Filmsammlungen:**
  - Wirkt der Schauspieler in *allen* Filmen einer Filmsammlung mit, wird nur die Sammlung gelistet.
  - Wirkt er nur in *einem* Film mit, wird nur dieser Film gelistet.
  - Wirkt er in mehreren, aber nicht allen Filmen einer Sammlung: Wenn der Anteil mindestens 50 % beträgt, wird die Sammlung angezeigt (betroffene Filme in der Detailansicht genannt); andernfalls werden die einzelnen Filme gelistet.
  - Der 50-%-Schwellenwert ist konfigurierbar bzw. leicht anpassbar.
- **Zuordnungslogik Serien (analog):**
  - Mitwirkung in allen Episoden einer Staffel → nur Staffel.
  - Mitwirkung in allen Staffeln einer Serie → nur Serie.
  - Ansonsten jeweils konkreteste Ebene (Episode/Staffel).

## Offene Punkte / Risiken

- Die genaue Prozent-Schwelle ist noch mit dem Anforderer abzustimmen; 50 % ist Vorschlag.
- Verhalten bei nachträglichen Änderungen am Metadaten-Bestand (neue Filme in einer Sammlung) muss geklärt werden, da die Schwell-Berechnung neu bewertet werden muss.
- Bildquelle für Schauspieler-Porträt ist nicht in issue.md spezifiziert (Metadaten, extern, Fallback?)
