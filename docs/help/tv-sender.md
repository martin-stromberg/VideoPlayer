# TV-Sender

Der automatische TV-Sender erzeugt aus dem Medienbestand ein fortlaufendes Fernsehprogramm. Dieser
Bereich beschreibt die Administration unter `Einrichtung` → `TV-Sender` (`/admin/tv-sender`) —
ausgeliefert wird zunächst genau ein Sender. Die eigentliche Sendeplan-Erzeugung, die
Programmauskunft, der HLS-Live-Stream und die Live-TV-Ansicht folgen in späteren Schritten.

## Sender

Beim ersten Öffnen der Seite wird der Sender automatisch als „TV-Sender" angelegt. Der Name ist
frei änderbar („Speichern"); ein zweiter Sender kann derzeit nicht angelegt werden — das
Datenmodell ist bereits kanalfähig, die Verwaltung beschränkt sich aber bewusst auf den einen
ausgelieferten Sender.

## Bundesland für Feiertage

Programmregeln mit der Wiederholung „An Feiertagen" folgen dem deutschen Feiertagskalender. Das
gewählte Bundesland (Auswahlfeld, Standard: nur bundesweit gültige Feiertage) legt fest, welche
landesspezifischen Feiertage zusätzlich gelten. Die Einstellung wird über die
Programmeinstellungen (`Setups.HolidayFederalState`) gespeichert.

## Programmregeln

Programmregeln beschreiben wiederkehrende Sendungen. Je Regel wird gepflegt:

- **Name** — frei wählbar, je Sender eindeutig.
- **Regelart** — Serien-Sendezeit, Film zur Primetime, Genre-/Formatrotation, Wiederholung,
  Feiertags-Special oder Platzhalter/Füller.
- **Wiederholung** — Täglich, Wöchentlich (mit Wochentag), Einmalig (mit Datum) oder An Feiertagen.
- **Sendezeit** — gewünschte Uhrzeit der Ausstrahlung.
- **Umfang** — Länge in Minuten und/oder Anzahl der Folgen (bei Serien-Sendezeit Pflicht).
- **Programmmaterial** — genau ein Inhalt je Regel, ausgewählt über Such- bzw. Auswahldialoge statt
  interner IDs: Serie (Serien-Sendezeit), Film oder Filmsammlung (Primetime, Wiederholung, Special,
  Füller), Genre (Genre-/Formatrotation) oder eine vorhandene Benutzer-Playlist. Welche Materialart
  zulässig ist, richtet sich nach der Regelart und wird serverseitig geprüft.
- **Priorität** — ganzzahlig; bei sich überschneidenden Regeln gewinnt der höhere Wert.
- **Status** — aktive Regeln gehen in die Sendeplan-Erzeugung ein, inaktive bleiben erhalten,
  werden aber ignoriert.

Regeln lassen sich anlegen, bearbeiten, (de)aktivieren und löschen; das Löschen muss bestätigt
werden. Die Seite ist ausschließlich Administratoren zugänglich; die Berechtigung wird
serverseitig erzwungen, nicht nur in der Oberfläche.
