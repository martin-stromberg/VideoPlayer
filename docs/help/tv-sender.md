# TV-Sender

Der automatische TV-Sender erzeugt aus dem Medienbestand ein fortlaufendes Fernsehprogramm. Dieser
Bereich beschreibt die Administration unter `Einrichtung` → `TV-Sender` (`/admin/tv-sender`) —
ausgeliefert wird zunächst genau ein Sender. Der Sendeplan wird fortlaufend im Hintergrund erzeugt
und ist über die Programmauskunft für angemeldete Benutzer abfragbar; HLS-Live-Stream und die
Live-TV-Ansicht folgen in späteren Schritten.

Die Seite ist in vier Bereiche gegliedert, die über die Reiter (Tabs) oben erreichbar sind:
**Regeln** (Programmregeln-Tabelle und einklappbarer Regel-Editor), **Sendeplan** (kommende
Sendeplätze und manueller Generierungsanstoß), **Einstellungen** (Sender, Planung und
Segmentierung) und **Laufzeitermittlung** (Fortschritt der automatischen
Videolaufzeit-Ermittlung).

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
  Zusätzlich gibt es für Füller-Regeln die Materialart **„Trailer anstehender Filme"**
  (Checkbox ohne weitere Auswahl): Damit werden Trailer-Dateien eingeplanter Filme bevorzugt als
  Füllmaterial gesendet — siehe Abschnitt „Trailer" weiter unten.
- **Priorität** — ganzzahlig; bei sich überschneidenden Regeln gewinnt der höhere Wert.
- **Status** — aktive Regeln gehen in die Sendeplan-Erzeugung ein, inaktive bleiben erhalten,
  werden aber ignoriert.

Regeln lassen sich anlegen, bearbeiten, (de)aktivieren und löschen; das Löschen muss bestätigt
werden. Die Seite ist ausschließlich Administratoren zugänglich; die Berechtigung wird
serverseitig erzwungen, nicht nur in der Oberfläche.

## Sendeplan

Der Server erzeugt aus den aktiven Programmregeln automatisch einen rollierenden, lückenlosen
Sendeplan. Zwei Einstellungen im Tab „Einstellungen" (Gruppe „Planung") steuern die Erzeugung:

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

### Trailer

Der Sender erkennt Trailer-Dateien automatisch beim Medien-Scan — nach der üblichen
Plex/Jellyfin-Konvention:

- eine Datei mit **„-trailer"-Endung** neben der Filmdatei (`Film.mp4` → `Film-trailer.mp4`),
- sowie alle Videodateien in einem Unterordner namens **„trailers"** im Film-Verzeichnis.

Erkannte Trailer werden dem Film zugeordnet, erhalten wie jede Videodatei eine ermittelte
Laufzeit und tauchen **niemals als regulärer Film** im Programm oder der Medienübersicht auf —
sie stehen ausschließlich als Füllmaterial zur Verfügung.

Mit der Materialart **„Trailer anstehender Filme"** in einer Füller-Regel bevorzugt der
Sendeplan Trailer, deren Film innerhalb der nächsten Tage ausgestrahlt wird
(Vorlauf-Werbung-Effekt). Passt an einer Position kein Trailer, greifen die übrigen
Füller-Regeln als Rückfall. Das Vorlauffenster steuert der Betreiber über den
Konfigurationsschlüssel `ProgramSchedule:TrailerLeadDays` (Standard: 7 Tage).

### Maskottchen-/Kennzeichen-Clips (Interstitials)

Kurze Sender-Kennzeichen oder Maskottchen-Clips (z. B. „Funki, das Glühbirnchen") können als
Füllmaterial rotieren — ohne als reguläre Filme in der Mediathek aufzutauchen:

1. **Dedizierte Medienquelle** anlegen (z. B. lokales Verzeichnis `Maskottchen/`) und die
   Clips dort ablegen. **Keine `.nfo`-Dateien** mitliefern — nur Dateien mit NFO werden zu
   Filmen/Serien eingeordnet, die Clips bleiben damit „uneingeordnet" und unsichtbar in der
   Bibliothek (Laufzeit wird trotzdem ermittelt, das gilt für jede Videodatei).
2. Die Quelle **für alle Anwender freigeben** — Füller werden nur eingeplant, wenn sie für
   die gesamte Nicht-Admin-Zuhörerschaft sichtbar sind.
3. **Füller-Regel** anlegen (Regelart „Platzhalter/Füller") und als Programmmaterial
   **„Clip-Quelle"** die Maskottchen-Quelle wählen. Alle uneingeordneten Videodateien der
   Quelle rotieren dann als Füller — pro Durchlauf in fester Reihenfolge, danach von vorn.
   Bereits eingeordnete Inhalte derselben Quelle (Filme/Episoden) bleiben außen vor.

In Programmauskunft, Live-TV und Sendeplan-Anzeige tragen die Sendeplätze den Dateinamen des
Clips als Titel. Fertige Beispielclips samt Drehbüchern und dem Generatorskript liegen im
Projektverzeichnis unter `docs/projects/…/maskottchen/` (siehe dortige `README.md`).

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

### Planungsdiagnose und manueller Anstoß

Die Spalte **„Planbar"** in der Programmregeln-Tabelle zeigt pro Regel, wie viele Inhalte des
gewählten Materials aktuell planbar sind — gerechnet mit derselben Logik wie die
Sendeplan-Erzeugung (Genre-Vereinigung, Quell-Filter, Freigabe für das gesamte Publikum,
bekannte Laufzeit). Ist nichts planbar, nennt die Zelle den Grund: „Material nicht gefunden
(verwaist)", „ohne Mediendatei", „ohne Laufzeit (Ermittlung läuft)" bzw. „ohne Laufzeit
(gescheitert)" — sobald die Versuchsgrenze der Laufzeitermittlung erreicht ist — oder „nicht
für alle freigegeben". Die Diagnose wird bei jedem Laden der Seite und nach jeder Regel-
Änderung neu berechnet; sie verändert den Sendeplan selbst nicht.

Die Schaltfläche **„Jetzt generieren"** im Tab „Sendeplan" stößt die Plan-Neubewertung
sofort an, ohne auf das Generierungsintervall zu warten — nützlich nach Konfigurations- oder
Bestandsänderungen. Die Sendeplan-Anzeige und die Diagnosen aktualisieren sich kurz darauf
selbst; bei großen Bibliotheken kann die Generierung länger dauern, dann zeigt ein erneutes
Laden der Seite den Endstand.

### Fortschritt der Laufzeitermittlung

Der Tab **„Laufzeitermittlung"** zeigt den Stand des automatischen
Ermittelns der Videolaufzeiten — ohne dass ein Blick in Log-Dateien nötig ist: Zähler für
ermittelte, ausstehende und dauerhaft fehlgeschlagene Dateien, ein Fortschrittsbalken, der
Zeitpunkt der letzten Aktivität sowie die Anzahl der Ermittlungen in der letzten Stunde.
Über **„▾ Details"** an den Zählern „ausstehend" und „fehlgeschlagen" klappen Tabellen der
betroffenen Dateien auf — jeweils mit Name, Quelle, Anzahl der Versuche und Zeitpunkt des
letzten Versuchs (bei langen Listen gekappt, mit Hinweis auf die übrigen Einträge).
Eine Liste „Zuletzt ermittelt" führt die jüngst geprüften Dateien mit Ergebnis (Laufzeit oder
Fehlversuch). Ein Statussatz fasst zusammen, ob die Ermittlung läuft, abgeschlossen ist oder
ausstehende Dateien ohne Aktivität verharren (Hinweis auf ein möglicherweise fehlendes
ffprobe). Die Anzeige aktualisiert sich alle 15 Sekunden selbst.

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
tragen eindeutige, zeit- und slot-codierte Namen. Die Wiedergabeliste läuft dabei über die
Job-Grenze hinweg weiter: der neue Lauf hängt an die bestehende `live.m3u8` an
(`append_list`), führt deren Media-Sequence fort und markiert den Übergang mit
`#EXT-X-DISCONTINUITY` sowie jedem Segment einen `EXT-X-PROGRAM-DATE-TIME`-Zeitstempel —
Player wie hls.js spielen dadurch ohne Bild-Einfrieren über den Sendeplatz-Wechsel hinweg,
auch bei Drift- oder Crash-Neustarts.
Bei Sendelücken oder fehlender Mediendatei ruht die Segmentierung; weicht der laufende Prozess
erkennbar vom Programmpunkt ab, wird er neu gestartet. Alle Zuschauer sehen daher zum selben
Zeitpunkt dieselbe Programmposition. Stirbt der `ffmpeg`-Prozess unerwartet, protokolliert der
Server Exit-Code und das Ende der `stderr`-Ausgabe als Warnung im Log (Stream-Tokens und
Zugangsdaten werden maskiert) und startet den Lauf am nächsten Synchronisationstakt an der
dann aktuellen Programmposition neu — der Stream springt also auf die Live-Position, statt die
verlorene Zeit nachzuholen.

Medien aus **entfernten Quellen** (z. B. SFTP) werden `ffmpeg` bevorzugt als seekfähige
HTTP-Adresse gereicht: der Server stellt die Datei über seinen internen, token-gesicherten
Range-Endpunkt (`api/internal/media-stream/{token}`) bereit und übergibt `ffmpeg` die
Loopback-URL — `ffmpeg` liest dann mit HTTP-Range-Anfragen, kann für `-ss` springen und kommt
auch mit MP4-Dateien zurecht, deren `moov`-Atom am Dateiende liegt. Das Token lebt nur im
Speicher, autorisiert genau diese eine Datei und ist auf die Restlaufzeit des Sendeplatzes
plus Sicherheitspuffer bemessen; es erscheint maskiert im Log. Ist keine Adresse baubar oder
scheitert ein Lauf am HTTP-Transport, fällt der nächste Versuch automatisch auf die bisherige
Methode zurück, die den Inhalt in die Standardeingabe des Prozesses pumpt (`pipe:0`). Das
Verhalten lässt sich mit `LiveStream:UseHttpInput` (`appsettings.json`, Standard `true`)
abschalten — `false` nutzt immer den Pipe-Weg.

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
werden — seit die entfernten Quellen bevorzugt über den HTTP-Range-Eingang laufen (siehe
oben, `LiveStream:UseHttpInput`), trifft das nur noch zu, wenn der HTTP-Eingang deaktiviert
ist oder als Fallback greift; lokale Medienquellen (`LocalDirectory`), die über den Dateipfad
gelesen werden, sind generell nicht betroffen.
