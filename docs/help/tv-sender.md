# TV-Sender

Der automatische TV-Sender erzeugt aus dem Medienbestand ein fortlaufendes Fernsehprogramm. Dieser
Bereich beschreibt die Administration unter `Einrichtung` → `TV-Sender` (`/admin/tv-sender`) —
ausgeliefert wird zunächst genau ein Sender. Der Sendeplan wird bereits fortlaufend im Hintergrund
erzeugt; Programmauskunft, HLS-Live-Stream und die Live-TV-Ansicht folgen in späteren Schritten.

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

## Sendeplan

Der Server erzeugt aus den aktiven Programmregeln automatisch einen rollierenden, lückenlosen
Sendeplan. Zwei Einstellungen in der Sender-Karte steuern die Erzeugung:

- **Planungshorizont (Stunden)** — wie weit voraus geplant wird (Standard: 48 Stunden).
- **Generierungsintervall (Minuten)** — in welchem Abstand der Hintergrundlauf den Plan
  fortschreibt (Standard: 15 Minuten; Änderungen greifen ohne Neustart).

Dabei gilt:

- **Konflikte** zwischen überschneidenden Ausstrahlungen entscheidet die Priorität der Regel
  (bei Gleichstand die ältere Regel); die unterlegene Ausstrahlung entfällt komplett.
- **Serien** werden je Regel in Sendedreihenfolge fortgesetzt; der Fortschritt überlebt Neustarts
  und beginnt nach dem letzten Titel wieder vorn.
- **Lücken** füllen die Regeln der Art „Platzhalter/Füller" — nur Inhalte, die vollständig in die
  Lücke passen; der letzte Füller darf über das Planungsende hinauslaufen.
- Eingeplant werden ausschließlich Inhalte mit bekannter Laufzeit, die für das gesamte
  (Nicht-Administrator-)Publikum freigegeben sind — über den regulären Medienquellenzugriff oder
  eine Einzelfreischaltung der Serie bzw. Filmsammlung.
- Regeln auf nicht mehr vorhandenes Material werden übersprungen und protokolliert.

### Nachführung bei Änderungen

Der Sendeplan wird nicht nur periodisch fortgeschrieben, sondern nach jeder planrelevanten
Änderung neu bewertet: Beim Anlegen, Bearbeiten, (De)Aktivieren oder Löschen einer Programmregel,
beim Eintritt neuer Episoden oder Filme in eine Sammlung, beim Löschen einer Medienquelle und am
Ende jedes Medien-Scans wacht der Hintergrundlauf sofort auf; das Generierungsintervall dient als
Sicherheitsnetz für Änderungen ohne Auslöser (z. B. direkte Datenbank-Löschungen).

Dabei gilt:

- **Noch gültige Sendeplätze bleiben** — auch laufende und bereits gesendete; vergangene
  Sendeplätze werden grundsätzlich nie verändert oder gelöscht.
- **Ungültige Sendeplätze werden als „verdrängt" markiert**, nicht gelöscht — z. B. weil die
  erzeugende Regel geändert, deaktiviert oder gelöscht wurde, ihr referenzierter Inhalt nicht
  mehr planbar ist oder eine höherpriorisierte Regel das Zeitfenster übernimmt.
- **Verdrängte Serienfolgen** werden zur nächsten passenden Gelegenheit desselben Regelkontexts
  erneut eingeplant — in Sendedreihenfolge, ohne dass eine Folge übersprungen wird (außer die
  Folge selbst ist nicht mehr planbar).
- **Entstehende Lücken** werden regelkonform neu gefüllt, insbesondere über die Füller-Regeln;
  Restlücken kleiner als der kürzeste Füller bleiben wie bei der Erzeugung bestehen.
- **Nachvollziehbarkeit**: Die Karte „Sendeplan" auf derselben Seite zeigt die kommenden
  Sendeplätze mit Start/Ende, Inhalt, erzeugender Regel und Status — verdrängte Sendeplätze sind
  dort als „Verdrängt" gekennzeichnet und bleiben als Historie erhalten.
