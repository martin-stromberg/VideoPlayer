# TV-Sender

Der automatische TV-Sender erzeugt aus dem Medienbestand ein fortlaufendes Fernsehprogramm. Dieser
Bereich beschreibt die Administration unter `Einrichtung` → `TV-Sender` (`/admin/tv-sender`) —
ausgeliefert wird zunächst genau ein Sender. Der Sendeplan wird fortlaufend im Hintergrund erzeugt
und ist über die Programmauskunft für angemeldete Benutzer abfragbar; HLS-Live-Stream und die
Live-TV-Ansicht folgen in späteren Schritten.

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
  Ausnahme Genre: Hier sind **mehrere Genres** wählbar (Checkbox-Liste); der Sendeplan bildet die
  deduplizierte Vereinigung aller gewählten Genres. Zusätzlich kann die Expansion auf ausgewählte
  **Medienquellen** eingeschränkt werden — ohne Auswahl gelten alle Quellen. Wählbar sind nur
  sichtbare, kanonische Genres: Versteckte Genres ohne gültige Alternativnamen-Zuordnung stehen
  nicht zur Wahl; Alternativnamen werden zum kanonischen Genre aufgelöst, sodass auch Inhalte
  einfließen, deren Genre-Zuordnung noch auf eine versteckte Alternativ-Zeile zeigt.
  Saisonale Genres (Start-/Enddatum) sind nur während ihres Sichtbarkeitsfensters wählbar.
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

## Programmauskunft

Die Programmauskunft beantwortet zu jedem Zeitpunkt rein aus Uhrzeit und Sendeplan, was gerade
läuft und was als Nächstes kommt. Sie ist zustandslos — sie hängt weder davon ab, ob oder wer
zuschaut, noch von Prozesszustand, und ist nach einem Server-Neustart sofort korrekt. Als
verdrängt markierte Sendeplätze gelten dabei nicht als Programm und tauchen in der Auskunft nicht
auf.

Zwei abgesicherte Endpunkte stehen angemeldeten Benutzern (und gekoppelten Geräten) zur Verfügung
— Bearer-Token im `Authorization`-Header oder als `access_token`-Abfrageparameter; anonyme
Aufrufe werden mit 401 abgelehnt:

- `GET /api/program-guide/now` — „Jetzt/Gleich": die laufende Sendung mit Position im Video
  (`positionSeconds`), Restdauer (`remainingSeconds`) und Laufzeit (`durationSeconds`) sowie die
  nächste Sendung. Deckt eine Sendelücke den Zeitpunkt nicht ab, ist `now` leer — die nächste
  Sendung wird trotzdem benannt.
- `GET /api/program-guide/upcoming?hours=N` — begrenzte Programmübersicht: laufende Sendung plus
  alle Sendungen, die innerhalb der nächsten `N` Stunden beginnen (Vorgabe 6, Maximum 12 —
  darüber antwortet der Server mit 400).

Jeder Eintrag trägt Titel (Filmname bzw. „Serie — Staffel, E-Folge: Episodenname"), Medientyp
(`Movie`/`TVShowEpisode`), Start/Ende in UTC und eine Füller-Markierung (`isFiller`); kann ein
Inhalt nicht mehr aufgelöst werden (z. B. gelöschter Film), erscheint er als „Unbekannt". Der
vollständige Sendeplan inklusive verdrängter Sendeplätze bleibt ausschließlich der Administration
vorbehalten (Karte „Sendeplan" auf der TV-Sender-Seite).

Die Programmauskunft ist die einheitliche fachliche Grundlage für die spätere Live-TV-Anzeige und
die Stream-Auslieferung; sie ist begrifflich von den Benutzer-Playlists getrennt.

## Live-Stream (HLS)

Der laufende Sendeplan wird als durchgehender HLS-Live-Stream ausgeliefert. Ein Hintergrundlauf
synchronisiert dazu fortlaufend einen `ffmpeg`-Segmentierungsprozess mit der Sendung, die laut
Programmauskunft gerade on air ist — rein zeitbasiert: Jede Entscheidung wird aus Uhrzeit und
Sendeplan neu berechnet, es wird kein Prozesszustand gespeichert, sodass sich der Stream nach
einem Server-Neustart von selbst wieder auf die richtige Sendung und Position einstellt. Der
Start erfolgt zeitversetzt (`-ss` auf die aktuelle Programmposition); an jedem Sendeplatz-
Übergang wird der Prozess sauber beendet und auf dem Folgeslot neu gestartet — neue Segmente
tragen eindeutige, zeit- und slot-codierte Namen, die Wiedergabeliste beginnt je Sendung sauber.
Bei Sendelücken oder fehlender Mediendatei ruht die Segmentierung; weicht der laufende Prozess
erkennbar vom Programmpunkt ab, wird er neu gestartet. Alle Zuschauer sehen daher zum selben
Zeitpunkt dieselbe Programmposition.

Drei Einstellungen in der Sender-Karte steuern die Ausgabe:

- **Segmentlänge (Sekunden)** — Zieldauer je HLS-Segment (Standard: 6 Sekunden, zulässig 1–300).
- **Segment-Verzeichnis** — Speicherort für Segmente und Wiedergabeliste; leer = Standard
  `Data/LiveStream` unterhalb des Anwendungsverzeichnisses, relative Pfade werden gegen das
  Anwendungsverzeichnis aufgelöst.
- **Segment-Aufbewahrung (Minuten)** — abgelaufene Segmente werden automatisiert entfernt
  (Standard: 30 Minuten); beim Start des Hintergrundlaufs wird das Verzeichnis vollständig
  geleert.

Die Auslieferung erfolgt ausschließlich authentifiziert: `GET /api/live-stream/{datei}`
akzeptiert den Bearer-Token im `Authorization`-Header oder als `access_token`-Abfrageparameter
— anonyme Aufrufe werden mit 401 abgelehnt. Bei Auslieferung der Wiedergabeliste
(`live.m3u8`, Antworttyp `application/vnd.apple.mpegurl`, `Cache-Control: no-store`) wird jede
Segment-Adresse um den übergebenen `access_token` ergänzt, damit Player, die relative
Segment-URIs ohne die Abfrageparameter der Playlist-URL auflösen, autorisiert bleiben. Ungültige
oder fehlende Dateinamen — einschließlich des Zustands „es läuft gerade kein Stream" — werden
mit 404 beantwortet.

## Live-TV-Ansicht

Über den Navigationspunkt **„Live-TV"** (für angemeldete Benutzer) erreichen Zuschauer den
Sender. Die Ansicht zeigt den laufenden Stream im Videoplayer sowie darunter „Jetzt läuft"
(Titel, Uhrzeiten, Restdauer und Fortschritt), „Gleich" (nächste Sendung) und eine begrenzte
Vorschau der nächsten Stunden; der vollständige Sendeplan bleibt der Administration
vorbehalten. Die Ansicht aktualisiert sich selbstständig — zu Sendebeginn wechselt sie ohne
Neuladen in die Wiedergabe.

Die Wiedergabe nutzt dort, wo der Browser es kann, die eingebaute HLS-Wiedergabe (z. B. Safari);
andernfalls springt die freie Bibliothek **hls.js** ein. Der Abruf erfolgt authentifiziert
über den Benutzer-Token — der Server bleibt die verbindliche Zugriffskontrolle.

Verständliche Hinweise ersetzen technische Fehlerbilder:

- **„Aktuell läuft keine Sendung."** — Sendelücke; die nächste Sendung wird genannt, sobald bekannt.
- **„Der Live-Stream ist momentan nicht verfügbar. Es wird automatisch erneut versucht."** —
  z. B. noch keine Segmente geschrieben; die Seite versucht selbstständig weiter.
- **„Live-TV wird von diesem Browser leider nicht unterstützt."** — der Browser kann weder
  eingebaut noch über hls.js wiedergeben.
- **„Die Programminformationen konnten nicht geladen werden."** / **„Sie haben keinen Zugriff
  auf den TV-Sender."** — vorübergehender Fehler bzw. fehlende Berechtigung.

### ffmpeg-Bereitstellung und Lizenz

`ffmpeg` wird wie `ffprobe` **nicht mit ausgeliefert**, sondern gesucht in der Reihenfolge:
konfigurierbarer Pfad (`MediaProbe:FfmpegPath` in `appsettings.json`), Konventionsverzeichnisse
`tools/ffmpeg/` sowie das Anwendungsverzeichnis, danach `PATH`. Ist `ffmpeg` nicht auffindbar,
pausiert nur der Live-Stream — der übrige Betrieb (Sendeplan, Programmauskunft, Verwaltung)
bleibt unberührt und eine Warnung wird protokolliert. Übliche gepackte Builds (z. B. die
`ffmpeg`-Pakete für Windows von gyan.dev) sind mit `--enable-gpl` gebaut; die Lizenz des
verwendeten Builds ist beim Bezug zu prüfen (GPL-Builds erfordern bei Weitergabe des
`ffmpeg`-Binärpakets selbst die GPL-Bedingungen — der Server ruft `ffmpeg` als eigenständigen
Prozess auf und steht dadurch nicht unter der GPL). Hinweise für Pipe-Eingaben von
Netzquellen: MP4-Dateien ohne `moov`-Atom am Dateianfang können über eine Pipe nicht demuxen
werden — lokale Medienquellen (`LocalDirectory`), die über den Dateipfad gelesen werden, sind
davon nicht betroffen.
