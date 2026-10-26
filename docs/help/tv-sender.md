# TV-Sender

Der automatische TV-Sender erzeugt aus dem Medienbestand ein fortlaufendes Fernsehprogramm. Dieser
Bereich beschreibt die Administration unter `Einrichtung` → `TV-Sender` (`/admin/tv-sender`) —
ausgeliefert wird zunächst genau ein Sender. Der Sendeplan wird fortlaufend im Hintergrund erzeugt,
ist über die Programmauskunft für angemeldete Benutzer abfragbar und wird als HLS-Live-Stream in
der Live-TV-Ansicht ausgespielt.

## Freischaltung

Der TV-Sender ist ab Werk **deaktiviert**: Navigation, Seiten (`/live-tv`, `/admin/tv-sender`),
Programmauskunft- und Stream-Endpunkte (`/api/program-guide/*`, `/api/live-stream/*`) sowie die
Hintergrund-Worker (Sendeplan, Segmentierung, Laufzeitermittlung) bleiben inaktiv, bis ein
Administrator unter `Einrichtung` → `Allgemein` den Schalter **„Automatisierten TV-Sender
freischalten"** setzt. Ohne Freigabe antworten die Endpunkte mit 404 und zeigen die Seiten einen
Hinweis — auch der interne, token-gesicherte Media-Stream-Endpunkt
(`/api/internal/media-stream/*`), über den Laufzeitermittlung und Segmentierer die Mediendateien
lesen. Die Prüfung greift auch bei noch gültigem Stream-Token: Beim Sperren beantwortet der
interne Endpunkt neue und Folge-Anfragen sofort mit 404; ein dadurch abgebrochener
ffmpeg-Lesezugriff gilt als HTTP-Transportfehler, sodass der nächste Segmentierer-Versuch auf
dieselbe Sendeposition auf die Pipe-Eingabe (`pipe:0`) zurückfällt (siehe „Live-Stream (HLS)").
Die Einstellung wirkt ohne Neustart; beim Sperren stoppt ein laufender ffmpeg-Lauf.

Zusätzlich gilt eine **Freigabe pro Benutzer**: Unterhalb des Schalters wählt der Administrator
in der Checkbox-Liste **„Diese Benutzer dürfen den TV-Sender sehen"** die freigegebenen Konten
aus. Nur diese Benutzer sehen den „Live-TV"-Eintrag in der Navigation und können `/live-tv`
sowie die Endpunkte nutzen — nicht freigegebene Benutzer erhalten dort 403 („kein Zugriff").
Administratoren haben immer Zugriff und brauchen keinen Eintrag in der Liste (ihre Markierung
ist fest gesetzt). Ohne Auswahl sehen nur Administratoren den Sender. Das Löschen eines
Benutzerkontos entfernt dessen Freigabezeile automatisch.

Die Seite ist in fünf Bereiche gegliedert, die über die Reiter (Tabs) oben erreichbar sind:
**Regeln** (Programmregeln-Tabelle; der Regel-Editor öffnet sich als Overlay-Panel über der
Liste), **Sendeplan** (kommende
Sendeplätze und manueller Generierungsanstoß), **Einstellungen** (Sender, Planung und
Segmentierung), **Laufzeitermittlung** (Fortschritt der automatischen
Videolaufzeit-Ermittlung) und **Clip-Gruppen** (Verwaltung benannter Platzhalter-Clip-Gruppen
für die Restzeit-Auffüllung im Sendeplatz — siehe Abschnitt
[„Clip-Gruppen mit Karenzzeit im Sendeplatz"](#clip-gruppen-mit-karenzzeit-im-sendeplatz)).

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
- **Wiederholung** — Täglich, Wöchentlich (mit Wochentag), Einmalig (mit Datum), An Feiertagen,
  Werktags (Mo–Fr) oder Wochenende (Sa+So).
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
  Enthält das Genre-Material **mehrere Serien**, füllt der Sendeplan das Zeitfenster
  serien-übergreifend im Wechsel: Jede Serie behält ihre Sendeplatz-Position innerhalb des
  Fensters über die Tage hinweg und setzt ihre Folgen in aufsteigender Reihenfolge fort —
  statt eine einzelne Serie blockweise zu „strippen", bekommt pro Ausstrahlung jede Serie
  der Reihe nach ihren Sendeplatz (z. B. 12:00 Serie A, 12:45 Serie B — am Folgetag die
  nächste Folge jeder Serie an derselben Stelle). Filme des Genres laufen als gemeinsamer
  Strang in Veröffentlichungsreihenfolge mit.
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

Die Karten im Tab „Sendeplan" sind nach Kalendertag gegliedert (Tages-Header). Innerhalb eines
Tages markiert eine gestrichelte Trennlinie mit Regelname den Blockwechsel, sobald eine andere
Programmregel den Sendeplatz erzeugt hat. Alle angezeigten Sendezeiten folgen der lokalen
Zeitzone des Servers — gespeichert werden sie intern in UTC; die Sendezeit einer Regel wird
ebenfalls in der Server-Zeitzone interpretiert.

Der Server erzeugt aus den aktiven Programmregeln automatisch einen rollierenden, lückenlosen
Sendeplan. Zwei Einstellungen im Tab „Einstellungen" (Gruppe „Planung") steuern die Erzeugung:

- **Planungshorizont (Stunden)** — wie weit voraus geplant wird (Standard: 48 Stunden).
- **Generierungsintervall (Minuten)** — in welchem Abstand der Hintergrundlauf den Plan
  fortschreibt (Standard: 15 Minuten; Änderungen greifen ohne Neustart).

Dabei gilt:

- **Konflikte** zwischen überschneidenden Ausstrahlungen entscheidet die Priorität der Regel
  (bei Gleichstand die ältere Regel); die unterlegene Ausstrahlung entfällt komplett.
- **Videos werden niemals gekürzt:** Ein einmal eingeplantes Video läuft immer vollständig —
  auch über das Ende seines Regelfensters hinaus (Überlauf). Beginnt z. B. 15 Minuten vor dem
  nächsten Sendefenster ein 45-minütiger Film, läuft er zu Ende und die folgende Ausstrahlung
  verschiebt sich entsprechend hinter sein Ende. Ausnahme: Liegt das nächste Fenster einer
  **höherprioren** Regel, wird ein Video, das vorher nicht zu Ende kommen kann, gar nicht erst
  eingeplant — es wartet auf die nächste eigene Sendezeit. Bleibt vor einer Fenstergrenze
  weniger als die konfigurierbare **Mindest-Restlücke** frei
  (Konfigurationsschlüssel `ProgramSchedule:MinStartRemainSeconds`, Standard: 30 Sekunden),
  wird ebenfalls nichts eingeplant — die Restlücke füllen Füller.
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

#### Clip-Gruppen mit Karenzzeit im Sendeplatz

Neben der „Clip-Quelle" für Füller-Regeln lassen sich Platzhalter-Clips auch in benannten
**Clip-Gruppen** verwalten und gezielt an Regeln hängen — etwa „Funki vor Werbung",
„Senderpause" oder „Feiertags-Jingles".

##### Gruppen verwalten (Tab „Clip-Gruppen")

Der Tab **„Clip-Gruppen"** führt die Gruppenliste mit Name und Clip-Anzahl. Über
**„Neue Gruppe"** wird eine Gruppe im Dialog angelegt; **„Umbenennen"** und **„Löschen"**
ändern sie nachträglich. Das Löschen muss bestätigt werden und entfernt auch die
Zuordnungen der Gruppe zu allen Regeln. Jede Änderung an Gruppen oder Mitgliedschaften
stößt die Plan-Neubewertung sofort an — ohne auf das Generierungsintervall zu warten.

Über **„Clips verwalten"** öffnet sich die Mitgliederliste einer Gruppe (je Clip: Name,
Quelle, Laufzeit, Schaltfläche **„Entfernen"**). Im Abschnitt **„Clips hinzufügen"** werden
Kandidaten über ein **Suche**-Feld (Suchtext im Dateinamen) und einen **Quelle**-Filter
ausgewählt; die Schaltfläche **„Suchen"** lädt die Trefferliste. Schon zugeordnete Dateien
tragen die Markierung **„Bereits zugeordnet"**, die übrigen lassen sich per
**„Hinzufügen"** übernehmen; bei langen Trefferlisten erscheint **„Weitere laden"**.

Als Mitglieder kommen wie bei der Clip-Quelle ausschließlich **uneingeordnete
Videodateien** in Frage (keine `.nfo`, keine Film-/Serien-Zuordnung); findet die Suche
nichts Passendes, weist ein Hinweis darauf hin. Dateien, die inzwischen einem Film oder
einer Episode zugeordnet wurden, lassen sich nicht mehr zuordnen und fallen im Sendeplan
automatisch aus der Rotation. Ein Clip darf mehreren Gruppen angehören. Zwei
Einschränkungen gelten zusätzlich:

- Ein Clip ohne ermittelte Laufzeit trägt die Markierung **„keine Laufzeit"** — er lässt
  sich zuordnen, wird bei der Restzeit-Auffüllung aber nicht berücksichtigt. Die
  Laufzeitermittlung läuft im Hintergrund nach (Tab „Laufzeitermittlung").
- Wie überall im Sendeplan werden nur Clips eingeplant, deren Quelle für das gesamte
  (Nicht-Administrator-)Publikum freigegeben ist.

##### Reservierte Gruppe „Trailer"

Das System führt automatisch die Clip-Gruppe **„Trailer"** — in der Liste mit dem Zusatz
„(System — automatisch befüllt)" gekennzeichnet. Sie lässt sich jeder passenden Regel
zuordnen, kann aber weder umbenannt noch gelöscht oder manuell befüllt werden; ihre
Schaltfläche **„Ansehen"** zeigt nur einen Hinweis auf die automatische Befüllung: Der
Plan zieht daraus Trailer, deren Film innerhalb des Vorlauffensters
(`ProgramSchedule:TrailerLeadDays`) ausgestrahlt wird — Vorlauf-Werbung direkt im
Sendeplatz. Steht gerade kein Film im Vorlauffenster, liefern die übrigen zugeordneten
Gruppen das Füllmaterial. Der Name „Trailer" ist für frei anlegbare Gruppen gesperrt. Ein
Trailer-Clip zeigt in der Programmauskunft die Daten seines Films.

##### Zuordnung und Karenzzeit an der Regel

Im Regel-Editor erscheint bei allen Regelarten außer „Platzhalter/Füller" die
Checkbox-Liste **„Clip-Gruppen (Restzeit-Auffüllung)"** — beliebig viele Gruppen sind
wählbar, „Trailer" trägt den Zusatz „(automatisch befüllt)". Ist noch keine Gruppe
angelegt, verweist ein Hinweis auf den Tab „Clip-Gruppen".

Die Auswahl wirkt nur bei gesetzter **Sendeplatz-Länge** („Länge (Minuten)"): Ohne Länge
erscheint der Hinweis „Clip-Gruppen und Karenzzeit wirken nur bei gesetzter
Länge (Minuten)." und das Zahlenfeld **„Karenzzeit (Minuten)"** bleibt ausgeblendet.
Das Feld erscheint außerdem erst, sobald mindestens eine Clip-Gruppe gewählt ist — ohne
Gruppe wäre die Eingabe wirkungslos, ein Hinweis weist darauf hin. Ein Hinweistext am Feld
erklärt die Wirkung: die Restzeit vor dem Sendeplatz-Ende, ab der kein weiterer Titel mehr
gestartet, sondern mit Clips der gewählten Gruppen aufgefüllt wird (Standard: 15 Minuten;
sie muss größer als 0 und kleiner als die Sendeplatz-Länge sein — das wird beim Speichern
geprüft). In der Regel-Tabelle zeigt der Material-Eintrag zugeordnete Gruppen als Suffix
„— Clips: …".

##### Wirkung im Sendeplan

- Endet die Titel-Ausstrahlung eines Sendeplatzes so, dass bis zum Periodenende **weniger
  als die Karenzzeit** frei bleibt, wird kein weiterer Titel mehr gestartet (Rest gleich
  oder größer der Karenzzeit → der nächste Titel startet wie bisher und läuft über das
  Periodenende hinaus zu Ende). Stattdessen verteilt der Plan die Restzeit gleichmäßig auf
  Clips der zugeordneten Gruppen — **vor dem ersten Titel, zwischen den Titeln und nach
  dem letzten**.
- Clips rotieren über die Gruppen im Wechsel (Round-Robin); jede Gruppe merkt sich ihren
  Fortschritt über die Läufe hinweg, so dass keine Wiederholung direkt aufeinander folgt.
- Der **letzte Clip** darf wie üblich über das Periodenende hinausragen; reicht der freie
  Raum für keinen Clip, bleibt die Restzeit eine normale Lücke für die Füller-Regeln. In
  belegte Fenster oder reservierte Fenster höherpriorer Regeln ragen Clips niemals hinein.
- Ist an einer Regel **keine** Clip-Gruppe hinterlegt, verhält sich der Sendeplatz exakt
  wie bisher (Titel-Overshoot über das Periodenende hinaus).

Technische Einzelheiten zu Ablauf und Datenmodell: [TV-Sender — Technischer Ablauf](tv-sender-ablauf-technisch.md),
[TV-Sender — Datenmodell](tv-sender-datenmodell.md).

### Nachführung bei Änderungen

Der Sendeplan wird nicht nur periodisch fortgeschrieben, sondern nach jeder planrelevanten
Änderung neu bewertet: Beim Anlegen, Bearbeiten, (De)Aktivieren oder Löschen einer Programmregel,
bei jeder Änderung an Clip-Gruppen oder deren Clip-Mitgliedschaften,
beim Eintritt neuer Episoden oder Filme in eine Sammlung, beim Löschen einer Medienquelle und am
Ende jedes Medien-Scans wacht der Hintergrundlauf sofort auf; das Generierungsintervall dient als
Sicherheitsnetz für Änderungen ohne Auslöser (z. B. direkte Datenbank-Löschungen).

Dabei gilt:

- **Noch gültige Sendeplätze bleiben** — auch laufende und bereits gesendete; vergangene
  Sendeplätze werden grundsätzlich nie verändert oder gelöscht. Eine Sendung, die beim
  Fortschreiben des Plans bereits lief und deshalb nur mit ihrem Rest im Plan steht, gilt
  ebenfalls weiter als gültig — sie wird nicht erneut verdrängt und neu geplant.
- **Lange Sendungen (über 24 Stunden) bleiben gültig**: Der Plan-Lauf schaut für die
  Gültigkeitsprüfung regulär 24 Stunden zurück. Beginnt eine noch laufende oder künftige
  Ausstrahlung weiter in der Vergangenheit — z. B. ein mehrtägiger Marathon-Block oder eine
  Kette nahtlos anschließender Sendungen derselben Regel —, wird das Prüffenster bei Bedarf
  einmalig bis zum konfigurierbaren Maximum verbreitert (Konfigurationsschlüssel
  `ProgramSchedule:MaxOccurrenceLookbackDays`, Standard: 7 Tage, nach oben auf 30 Tage
  begrenzt), damit ihr realer Beginn noch
  belegbar ist. Sendungen, deren realer Beginn älter als dieses Maximum ist, gelten als nicht
  mehr nachvollziehbar und werden verdrängt und neu eingeplant.
- **Ungültige Sendeplätze werden als „verdrängt" markiert**, nicht gelöscht — z. B. weil die
  erzeugende Regel geändert, deaktiviert oder gelöscht wurde, ihr referenzierter Inhalt nicht
  mehr planbar ist oder eine höherpriorisierte Regel das Zeitfenster übernimmt.
- **Verdrängte Serienfolgen** werden zur nächsten passenden Gelegenheit desselben Regelkontexts
  erneut eingeplant — in Sendedreihenfolge, ohne dass eine Folge übersprungen wird (außer die
  Folge selbst ist nicht mehr planbar).
- **Entstehende Lücken** werden regelkonform neu gefüllt, insbesondere über die Füller-Regeln;
  Restlücken kleiner als der kürzeste Füller bleiben wie bei der Erzeugung bestehen.
- **Nachvollziehbarkeit**: Die Karte „Sendeplan" auf derselben Seite zeigt die kommenden
  Sendeplätze als Panels mit Poster, Genre-Badge, Titel, Episoden-Info (Staffel/Episode),
  Beschreibung, erzeugender Regel und Status — der laufende Sendeplatz trägt ein LIVE-Badge
  mit Fortschrittsbalken; fehlen Poster oder Beschreibung in den Metadaten, erscheinen
  Platzhalter. Verdrängte Sendeplätze sind als „Verdrängt" gekennzeichnet und bleiben als
  Historie erhalten.
- **Fenstergrenzen**: Sendungen überziehen ihre Länge höchstens bis zum Beginn der nächsten
  Sendung einer anderen Regel — nie in deren Fenster hinein. Ein Inhalt, der nicht mehr in den
  Rest des Sendefensters passt, wird übersprungen statt die Folgesendung zu verdrängen. Ragt
  eine bereits eingeplante Sendung über eine solche Grenze hinaus (z. B. nach einer Regel-
  Änderung), wird nur ihr überstehender Rest verdrängt.

### Aufräumen des Sendeplans

Im Tab „Sendeplan" räumen zwei Schaltflächen (nur für Administratoren) den Plan auf:

- **„Verdrängte bereinigen"** löscht nach einer Bestätigung alle Sendeplätze mit dem Status
  „Verdrängt" — also die Historie der Nachführung — und zeigt die Anzahl der entfernten
  Einträge an. Der wirksame Plan bleibt unverändert.
- **„Plan zurücksetzen"** löscht nach einer Bestätigung mit Warnhinweis den **gesamten**
  Sendeplan — alle geplanten und verdrängten Sendeplätze sowie die Fortsetzungsstände aller
  Regeln (d. h. Serien beginnen wieder am Anfang ihrer Rotation). Der Plan wird beim nächsten
  Generierungslauf komplett neu aufgebaut; „Jetzt generieren" stößt ihn sofort an.

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
ffprobe). Ist ffprobe gar nicht auffindbar, erscheint oberhalb des Fortschritts die Warnung
„ffprobe wurde nicht gefunden — die Laufzeitermittlung pausiert." samt Schaltfläche
**„ffmpeg/ffprobe herunterladen"** — siehe Abschnitt
[„ffmpeg-Bereitstellung und Lizenz"](#ffmpeg-bereitstellung-und-lizenz). Die Anzeige
aktualisiert sich alle 15 Sekunden selbst.

Dauerhaft fehlgeschlagene Dateien lassen sich von hier aus wieder in die Ermittlung
zurückholen — etwa nachdem eine defekte Datei repariert oder ersetzt wurde, ohne dass der
Medien-Scan die Änderung bemerkt hat: In der Tabelle „Fehlgeschlagene Dateien" setzt die
Schaltfläche **„Erneut ermitteln"** (Spalte „Aktionen") die angezeigte Datei einzeln zurück,
**„Alle zurücksetzen"** neben dem Zähler „fehlgeschlagen" setzt nach einem
Bestätigungsdialog **alle** dauerhaft fehlgeschlagenen Dateien zurück — auch die, die wegen
der Listenkappung nicht angezeigt werden (darauf weist die Fußzeile der Tabelle hin). Ein
Zurücksetzen löscht Versuchszähler und Zeitstempel des letzten Versuchs; die Datei wird beim
nächsten Lauf der Ermittlung erneut geprüft und landet, falls sie weiterhin defekt ist, nach
erneutem Erreichen der Versuchsgrenze wieder in der Fehlerliste. Eine Statusmeldung in der
Karte bestätigt die Aktion. Hintergrund und Konfiguration der Ermittlung:
[Videolaufzeit](videolaufzeit.md).

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

Drei Einstellungen der Karte „Segmentierung" im Tab „Einstellungen" steuern die Ausgabe:

- **Segmentlänge (Sekunden)** — Zieldauer je HLS-Segment (Standard: 6 Sekunden, zulässig 1–300).
- **Segment-Verzeichnis** — Speicherort für Segmente und Wiedergabeliste; leer = Standard
  `Data/LiveStream` unterhalb des Anwendungsverzeichnisses, relative Pfade werden gegen das
  Anwendungsverzeichnis aufgelöst.
- **Segment-Aufbewahrung (Minuten)** — abgelaufene Segmente werden automatisch entfernt
  (Standard: 30 Minuten); beim Start des Hintergrundlaufs wird das Verzeichnis vollständig
  geleert — bereits **vor** der internen Startverzögerung, damit Reste einer alten
  Auslieferung in der Warm-up-Phase nicht mehr auslieferbar sind.

Die Auslieferung erfolgt ausschließlich authentifiziert: `GET /api/live-stream/{datei}`
akzeptiert den Bearer-Token im `Authorization`-Header oder als `access_token`-Abfrageparameter
— anonyme Aufrufe werden mit 401 abgelehnt. Bei Auslieferung der Wiedergabeliste
(`live.m3u8`, Antworttyp `application/vnd.apple.mpegurl`) wird jede
Segment-Adresse um den übergebenen `access_token` ergänzt, damit Player, die relative
Segment-URIs ohne die Abfrageparameter der Playlist-URL auflösen, autorisiert bleiben.
Alle Antworten — Wiedergabelisten, echte Segmente und Standby-Auslieferungen — tragen
`Cache-Control: no-store`.

Solange keine echte Wiedergabeliste mit mindestens einem referenzierten Segment existiert,
beantwortet `live.m3u8` die Anfrage statt `404` mit der Standby-Wiedergabeliste (siehe
„Standby-Clip (Warm-up-Phase)" unten) und `standby_*.ts`-Segmente werden aus dem Speicher
ausgeliefert — auch dann noch, wenn die echte Wiedergabeliste schon steht, damit ein
Zuschauer die zuletzt beworbene Standby-Folge zu Ende spielen kann. Der Übergang ist
nahtlos: Die erste echte Wiedergabeliste nach einer Standby-Auslieferung trägt einmalig
`#EXT-X-DISCONTINUITY` vor dem ersten Segmenteintrag. Nur wenn gar keine Standby-Assets
verfügbar sind, gilt wie bisher `404` für ungültige oder fehlende Dateinamen.

Bei der Segment-Auslieferung hält der Server die `.ts`-Dateien so geöffnet, dass sie parallel
weiter beschrieben und gelöscht werden dürfen: Die automatische Aufbewahrung
(„Segment-Aufbewahrung", siehe oben) und `ffmpeg` selbst räumen das rollierende Fenster also
auch dann auf, wenn ein Zuschauer ein abgelaufenes Segment gerade noch lädt — die Übertragung
läuft vollständig zu Ende. Unter Windows würde ein schlichter Lesezugriff das Löschen sonst
blockieren und im ungünstigen Fall den Segmentierer abbrechen. Segment-Antworten tragen
bewusst keine `Last-Modified`-/`ETag`-Prüfmarken: Die Segmentnamen sind eindeutig und
zeitgestempelt, ihr Inhalt ändert sich nie, sodass bedingte Abrufe ohnehin nicht greifen.

### Standby-Clip (Warm-up-Phase)

Der Standby-Stream überbrückt jeden Zustand ohne produzierte Segmente — die Warm-up-Phase
nach einem Server-Start oder Update ebenso wie Sendelücken, ein fehlendes `ffmpeg` oder
eine nicht auffindbare Mediendatei: Statt einer Fehleranzeige sehen Zuschauer einen kurzen
Clip in Endlosschleife (die Standby-Wiedergabeliste rotiert ihre wenigen Segmente). Ohne
Auswahl spielt der eingebettete Funki-Clip „Gleich geht's los!" — fertig vorsegmentierte
Daten, die fest in der Anwendung liegen und deshalb auch im Zustand „`ffmpeg` fehlt"
laufen.

#### Eigenen Standby-Clip wählen

In der gleichnamigen Karte im Tab „Einstellungen" zeigt ein Chip den aktiven Clip —
„Standard-Clip (Gleich geht's los)" oder Name und Quelle des gewählten Clips. Über
**„Ändern"** öffnet sich die Auswahl: **„Quelle"** („Alle Quellen" oder eine einzelne
Medienquelle) und **„Suche"** (Live-Suche im Dateinamen) filtern die Kandidatenliste
(Spalten Name, Quelle, Laufzeit; Clips ohne ermittelte Laufzeit tragen den Hinweis
„keine Laufzeit"). **„Auswählen"** übernimmt einen Clip (Markierung „Ausgewählt"),
**„Weitere laden"** ergänzt lange Trefferlisten, **„Standard-Clip verwenden"** setzt die
Auswahl zurück. Angeboten werden nur **nicht klassifizierte Videodateien** — dieselbe
Regel wie bei den Clip-Gruppen-Kandidaten (Videodatei ohne Film-/Serien-Zuordnung);
findet die Suche nichts Passendes, erscheint der Hinweis „Keine zuordenbaren Clips
gefunden". Die Wahl wird erst mit **„Speichern"** übernommen und dabei serverseitig auf
die Clip-Regel geprüft.

Der gewählte Clip wird einmalig per `ffmpeg` in kurze Segmente transkodiert (höchstens
60 Sekunden Laufzeit — ein versehentlich langer Clip bläht den Speicher nicht auf)
und im Verzeichnis `Data/LiveStreamStandby/` neben der Anwendung zwischengespeichert:
Ein Neustart läuft ohne erneutes Transkodieren aus dem Cache, eine ersetzte Quelldatei
wird am Änderungsstempel erkannt und neu eingelesen. Die Vorbereitung läuft im
Hintergrund — bis der eigene Satz steht (und bei jedem Fehlschlag dauerhaft), beantwortet
der bisherige bzw. der Standard-Satz die Anfragen. Fehlt `ffmpeg`, ist das Medium
unauffindbar oder gelöscht oder scheitert die Vorbereitung, bleibt also zuverlässig ein
Standby-Stream aktiv. Eine gelöschte Auswahl bleibt als weiche Referenz gespeichert und
wird im Chip als „Zuvor gewählter Clip ist nicht mehr vorhanden — Standard-Clip wird
verwendet" angezeigt.

## Live-TV-Ansicht

Über den Navigationspunkt **„Live-TV"** (für angemeldete Benutzer) erreichen Zuschauer den
Sender. Die Ansicht zeigt den laufenden Stream im Videoplayer sowie daneben (auf schmalen
Bildschirmen darunter) kompakte Programm-Karten: „Jetzt läuft" (Poster, LIVE-Badge,
Fortschrittsbalken, Beschreibung), „Gleich" (nächste Sendung) und eine begrenzte
Vorschau der nächsten Stunden; der vollständige Sendeplan bleibt der Administration
vorbehalten. Die Ansicht aktualisiert sich selbstständig — zu Sendebeginn wechselt sie ohne
Neuladen in die Wiedergabe.

Die Wiedergabe nutzt dort, wo der Browser es kann, die eingebaute HLS-Wiedergabe (z. B. Safari);
andernfalls springt die freie Bibliothek **hls.js** ein. Der Abruf erfolgt authentifiziert
über den Benutzer-Token — der Server bleibt die verbindliche Zugriffskontrolle.

Verständliche Hinweise ersetzen technische Fehlerbilder:

- **„Aktuell läuft keine Sendung."** — Sendelücke; die nächste Sendung wird genannt, sobald bekannt.
- **„Der Live-Stream ist momentan nicht verfügbar. Es wird automatisch erneut versucht."** —
  echte Auslieferungsfehler (z. B. Netzwerkabbruch oder keine verfügbaren Standby-Assets);
  die Seite versucht selbstständig weiter. In der Warm-up-Phase und in allen Zuständen
  ohne produzierte Segmente — auch während einer Sendelücke, in der die Programm-Karte
  „Aktuell läuft keine Sendung." meldet — zeigt der Player dagegen den Standby-Clip
  (siehe oben), ohne dass ein Hinweis erscheint.
- **„Live-TV wird von diesem Browser leider nicht unterstützt."** — der Browser kann weder
  eingebaut noch über hls.js wiedergeben.
- **„Die Programminformationen konnten nicht geladen werden."** / **„Sie haben keinen Zugriff
  auf den TV-Sender."** — vorübergehender Fehler bzw. fehlende Berechtigung.

### ffmpeg-Bereitstellung und Lizenz

`ffmpeg` wird wie `ffprobe` **nicht mit ausgeliefert**, sondern gesucht in der Reihenfolge:
konfigurierbarer Pfad (`MediaProbe:FfmpegPath` in `appsettings.json`), Konventionsverzeichnisse
`tools/ffmpeg/` sowie das Anwendungsverzeichnis, danach `PATH`. Ist `ffmpeg` nicht auffindbar,
pausiert nur der Live-Stream — der übrige Betrieb (Sendeplan, Programmauskunft, Verwaltung)
bleibt unberührt und eine Warnung wird protokolliert.

#### Nachinstallation per Download-Schaltfläche

Der einfachste Weg, die fehlenden Programme nachzurüsten, führt über die Verwaltungsseite
selbst — ohne Paketverwaltung oder manuelle Dateiablage. Fehlt `ffmpeg`, zeigt die Karte
**„Segmentierung"** im Tab **„Einstellungen"** die Warnung „ffmpeg wurde nicht gefunden — der
Live-Stream pausiert."; fehlt `ffprobe`, erscheint im Tab **„Laufzeitermittlung"**
entsprechend „ffprobe wurde nicht gefunden — die Laufzeitermittlung pausiert.". Beide
Hinweise tragen direkt die Schaltfläche **„ffmpeg/ffprobe herunterladen"**. Die Aktion steht
ausschließlich Administratoren offen; die Berechtigung wird serverseitig erzwungen.

Die Schaltfläche öffnet zunächst einen Bestätigungsdialog, der Vorgang und Lizenzhinweis
erläutert: Das Paket wird im Ordner `tools/ffmpeg/` neben der Anwendung abgelegt, eine dort
vorhandene Version wird ersetzt und die übrigen Einstellungen bleiben unverändert; die Pakete
sind statisch gebaut und mit `--enable-gpl` erstellt — es gelten die Lizenzbedingungen der
jeweiligen ffmpeg-Distribution (siehe unten). Erst **„Herunterladen"** im Dialog startet den
Download.

Der Download lädt das zum Server passende, statisch gebaute ffmpeg-Paket herunter. Es enthält
stets **beide** Programme — ein Lauf installiert `ffmpeg` und `ffprobe` gemeinsam, auch wenn
nur eines davon fehlt. Ablageort ist ausschließlich das Konventionsverzeichnis `tools/ffmpeg/`
neben der Anwendung; konfigurierte Pfade (`MediaProbe:FfprobePath`/`MediaProbe:FfmpegPath`),
das Anwendungsverzeichnis und `PATH` werden nie verändert. Unter Linux setzt die Installation
das Ausführungsbit selbst. Der Ordner `tools/ffmpeg/` gehört nicht zum Auslieferungspaket —
er bleibt bei Programmupdates unverändert erhalten. Nach Abschluss finden die Suchwege beider
Funktionen die Programme ohne Neustart: Laufzeitermittlung und Live-Stream laufen im nächsten
Arbeitstakt automatisch wieder an und die Warnhinweise verschwinden.

Unterstützt werden Windows x64 und Linux x64; auf anderen Plattformen meldet der Download,
dass das System nicht unterstützt wird, und verweist auf die manuelle Installation. Die
Bezugsquelle je Plattform ist in der Server-Konfiguration unter `MediaProbe:DownloadSources`
hinterlegt (Schlüssel `win-x64` und `linux-x64` in `appsettings.json`) und lässt sich dort für
Umgebungen ohne direkten Internetzugang auf eine eigene Adresse umbiegen; voreingestellt sind
die von ffmpeg.org verlinkten statischen Builds (gyan.dev für Windows, johnvansickle.com für
Linux).

Während des Laufs zeigt eine Fortschrittskarte den Stand an — die übertragene Datenmenge,
soweit die Paketgröße bekannt ist, in Prozent; ein laufender Download sperrt weitere Starts.
Erfolg und Fehler erscheinen als Meldung mit Schaltfläche „Schließen" — im Erfolgsfall „ffmpeg
und ffprobe wurden installiert. Laufzeitermittlung und Live-Stream laufen wieder." Die
Meldungen decken die üblichen Fehlerbilder verständlich ab:

- **Kein Download möglich** („Das Paket konnte nicht heruntergeladen werden. Bitte die
  Internetverbindung prüfen und den Download erneut starten.") — auch, wenn der Server zu
  lange keine Daten mehr sendet.
- **Zielordner nicht beschreibbar** („Der Server kann den Zielordner „tools/ffmpeg/" neben
  der Anwendung nicht beschreiben. …") — Schreibrechte prüfen oder alternativ einen eigenen
  Pfad für ffmpeg/ffprobe in `appsettings.json` hinterlegen.
- **Plattform nicht unterstützt** („Dieses System wird nicht unterstützt. …") — ffmpeg und
  ffprobe manuell installieren bzw. eigenen Speicherort konfigurieren.
- **Paket unverwendbar** („Das Paket enthält kein ffmpeg/ffprobe-Programm …" bzw. „Das
  heruntergeladene Paket konnte nicht entpackt werden.") — bzw. „Es ist keine gültige
  Download-Quelle für dieses System hinterlegt." (`MediaProbe:DownloadSources` prüfen).

Technische Einzelheiten eines Fehlers stehen zusätzlich im Server-Protokoll. Der manuelle Weg
(eigene Installation, konfigurierter Pfad, Ablage in `tools/ffmpeg/` von Hand, `PATH`) bleibt
unverändert möglich — siehe [Videolaufzeit → ffprobe einrichten](videolaufzeit.md).

**Lizenz.** Übliche gepackte Builds (z. B. die
`ffmpeg`-Pakete für Windows von gyan.dev) sind mit `--enable-gpl` gebaut; die Lizenz des
verwendeten Builds ist beim Bezug zu prüfen (GPL-Builds erfordern bei Weitergabe des
`ffmpeg`-Binärpakets selbst die GPL-Bedingungen — der Server ruft `ffmpeg` als eigenständigen
Prozess auf und steht dadurch nicht unter der GPL). Hinweise für Pipe-Eingaben von
Netzquellen: MP4-Dateien ohne `moov`-Atom am Dateianfang können über eine Pipe nicht demuxen
werden — seit die entfernten Quellen bevorzugt über den HTTP-Range-Eingang laufen (siehe
oben, `LiveStream:UseHttpInput`), trifft das nur noch zu, wenn der HTTP-Eingang deaktiviert
ist oder als Fallback greift; lokale Medienquellen (`LocalDirectory`), die über den Dateipfad
gelesen werden, sind generell nicht betroffen.
