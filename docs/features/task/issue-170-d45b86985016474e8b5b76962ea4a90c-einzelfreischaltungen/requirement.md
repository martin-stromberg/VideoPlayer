# Übersetzte Anforderung: Einzelfreischaltungen

## Ziel

Administratoren sollen einzelne Serien oder Filmsammlungen (im Folgenden: „Elemente“) gezielt für andere Anwender freischalten können, ohne die gesamte Quelle freizugeben.

## Auslöser / Problem

Bisher können Administratoren offenbar nur ganze Quellen für Anwender freigeben. Es fehlt die Möglichkeit, einzelne Elemente innerhalb einer Quelle granular für andere Anwender verfügbar zu machen.

## Beteiligte Rollen

- **Administrator:** Kann Elemente für andere Anwender freischalten.
- **Anwender:** Sieht freigeschaltete Elemente entsprechend seiner Berechtigungen.

## Funktionale Anforderungen

1. **Freischalten einzelner Elemente**
   - Auf der Detailansicht jedes Elements (Serie oder Filmsammlung) wird neben dem bestehenden Favorisieren-Symbol ein zusätzliches Symbol zum Freischalten für andere Anwender angezeigt.
   - Ein Klick auf das Symbol schaltet das Element für andere Anwender frei bzw. hebt die Freischaltung wieder auf (Toggle-Verhalten).

2. **Berücksichtigung in „Neu hinzugefügt“**
   - Freigeschaltete Elemente sollen in der Liste der neu hinzugefügten Titel berücksichtigt werden.

3. **Menüzugänglichkeit der Quelle**
   - Wenn ein Element freigeschaltet ist, soll die zugehörige Quelle für den Anwender im Menü aufrufbar sein, auch wenn die Quelle selbst nicht direkt freigegeben wurde.

4. **Sichtbarkeit in der Quellen-Auflistung**
   - Freigeschaltete Elemente sollen in der Auflistung ihrer Quelle sichtbar sein.
   - Wenn die Quelle selbst **nicht** direkt freigegeben ist, werden in der Auflistung dieser Quelle ausschließlich die explizit freigeschalteten Elemente angezeigt.

## Nicht-funktionale Anforderungen

- Die Freischaltung muss im bestehenden Berechtigungsmodell der Anwendung verankert sein.
- Die UI-Änderung darf das bestehende Layout der Detailansicht nur minimal verändern.
- Die Anforderung betrifft bestehende Daten; es dürfen keine bestehenden Freigaben verloren gehen.

## UI/UX

- Platzierung: Neben dem Favorisieren-Symbol in der Detailansicht eines Elements.
- Interaktion: Toggle per Klick.
- Visuelles Feedback: Symbol-/Farbzustand zeigt an, ob das Element freigeschaltet ist.

## Akzeptanzkriterien

- Ein Administrator kann ein Element über das neue Symbol freischalten und wieder entfreigeben.
- Ein freigeschaltetes Element erscheint in der Liste der neu hinzugefügten Titel.
- Die Quelle des freigeschalteten Elements ist für berechtigte Anwender im Menü sichtbar/aufrufbar.
- In der Quellen-Auflistung sieht ein Anwender nur freigeschaltete Elemente, wenn die Quelle selbst nicht freigegeben ist.
- Sind Quelle und einzelne Elemente freigegeben, werden alle freigegebenen Inhalte angezeigt.
