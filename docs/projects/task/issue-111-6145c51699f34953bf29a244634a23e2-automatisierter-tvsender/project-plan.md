# Projektplan: Automatisierter TV-Sender (Live-Stream aus der Mediathek)

## Übersicht

Das System wird um einen automatisierten „TV-Sender" erweitert: Aus administrierbaren Programmregeln (Serien zu festen Sendezeiten, Filme zur Primetime, Genre-/Formatrotation, Wiederholungen, Feiertags-Specials, Platzhalter) erzeugt das System einen fortlaufenden Sendeplan aus konkreten Sendeplätzen. Eine zeitbasierte Programmauskunft liefert zu jedem Zeitpunkt die laufende Sendung samt Position, Restdauer und Nachfolger; darauf aufbauend wird der Plan als durchgehender Live-Stream (HLS) per echter FFmpeg-Segmentierung authentifiziert ausgeliefert und in der Benutzeroberfläche abspielbar. Betroffen sind die Bereiche Medien-Metadaten (fehlende Videolaufzeit), Administration (Regelverwaltung), Sendeplanung, Streaming-Auslieferung, Authentifizierung und Benutzeroberfläche.

## Grobe Vorgehensentscheidungen

| Bereich | Entscheidung | Begründung |
|---------|-------------|------------|
| Begriffswelt | Der Sender verwendet durchgehend ein eigenes Vokabular („Sender", „Programmregel", „Sendeplatz", „Sendeplan", „Programmauskunft"). Die Begriffe „Playlist" und „Playlist-Engine" werden für diese Funktion nicht verwendet. | `Playlist`/`IPlaylistService` bezeichnen bereits die fertige Benutzer-Playlist-Funktionalität (Issue #207); eine Namenskollision würde Code und Fachsprache vermischen. |
| Lieferung in Scheiben | Sendeplan und Programmauskunft werden vor der Streaming-Auslieferung fertiggestellt und sind ohne sie eigenständig testbar; der Stream baut als letzte Ausbaustufe darauf auf. | Plan und Auskunft hängen fachlich nicht vom Stream ab; die Streaming-Pipeline mit ihrem dauerhaften FFmpeg-Prozess ist der aufwendigste und risikoreichste Teil und wird gezielt zuletzt auf die getestete Basis aufgesetzt. |
| Streaming-Architektur | Der Live-Stream wird **echt segmentiert**: Ein dauerhaft laufender FFmpeg-Prozess erzeugt fortlaufend HLS-Segmente des gerade laufenden Videos und wird am Sendeplatz-Übergang auf das nächste Video umgeschaltet. Ein virtueller Live-Stream über berechnete Wiedergabelisten auf den Originaldateien wird **nicht** umgesetzt. | Entscheidung des Auftraggebers; nur so entsteht ein echter gemeinsamer Live-Stream, bei dem alle Zuschauer zeitgleich exakt denselben Programmpunkt sehen. |
| Externe Abhängigkeit FFmpeg | FFmpeg/ffprobe wird als neue externe Abhängigkeit eingeführt — für die Laufzeitermittlung der Videos (Schritt 1) und die Live-Segmentierung (Schritt 6). Bereitstellung der Binaries, Lizenzprüfung und Anpassung von Build- und CI-Abläufen gehören zum Umfang der jeweiligen Schritte. | Weder eine verlässliche Laufzeitermittlung noch eine Transcodierung sind ohne externes Werkzeug fachlich sauber zu erreichen; die Abhängigkeit ist vom Auftraggeber bestätigt. |
| Zugriffsregelung | Der Sender plant ausschließlich Inhalte, die für das gesamte berechtigte Publikum freigegeben sind; nicht vollständig freigegebene Inhalte werden bereits bei der Planung ausgeschlossen. | Bei einem gemeinsamen Stream für alle Zuschauer ist nur eine einheitliche, serverseitig erzwungene Planung konsistent — ein per-Zuschauer-Filtern eines gemeinsamen Streams ist nicht möglich. |
| Stream-Charakter | Es gibt genau einen gemeinsamen, zeitgesteuerten Programmpunkt für alle Zuschauer — kein benutzerindividueller Stream, kein Timeshift/Pause. Die Auskunftslogik ist rein zeitbasiert und zustandslos. | „Echtes TV-Gefühl" (Annahme der Anforderung); ein Server-Neustart synchronisiert sich allein über die Uhrzeit wieder auf den Plan, ohne Prozesszustand wiederherstellen zu müssen. |
| Sender-Anzahl | Ausgeliefert wird zunächst genau ein automatisierter Sender; das Datenmodell wird dennoch kanalfähig angelegt (Sender als eigenes Objekt). | Annahme der Anforderung; verbaut spätere Mehrkanalfähigkeit nicht, vermeidet aber Mehraufwand in Verwaltung und UI. |
| Platzhalter | Füller-/Platzhalterinhalte sind reguläre Mediatheken-Videos, die über eine eigene Regelart ausgewählt werden. | Kein neuer Inhaltstyp und keine getrennte Verwaltung nötig; Füller durchlaufen dieselbe Planungs- und Zugriffslogik. |
| Feiertage | Feiertags-Specials werden anhand eines fest hinterlegten deutschen Feiertagskalenders erkannt; das Bundesland ist konfigurierbar. | Keine externe Datenquelle nötig; die Bundesland-Konfiguration deckt die regionalen Unterschiede des deutschen Kalenders ab. |
| Konfiguration | Sender-Parameter (Planungshorizont, Generierungsintervall, Primetime-Fenster, Bundesland für den Feiertagskalender, Segmentlänge, Segment-Speicherort und Segment-Retention) werden über die etablierten Einstellungsmechanismen verwaltet. | Konsistent mit bestehenden Konfigurationsmustern der Anwendung; Administratoren müssen keine Dateien bearbeiten. |

## Entwicklungsschritte

### Schritt 1: Videolaufzeit erfassen

**Beschreibung:** Damit ein Sendeplan die Belegung von Sendeplätzen berechnen kann, muss das System die Wiedergabedauer jedes abspielbaren Videos kennen — heute wird beim Einlesen der Mediathek keine Laufzeit gespeichert. In diesem Schritt wird die Mediathek so erweitert, dass für jeden Film und jede Serienepisode eine Laufzeit geführt wird. Die Laufzeit wird per ffprobe/ffmpeg direkt aus den Videodateien ermittelt — beim regulären Medien-Scan für neu hinzukommende Inhalte sowie als automatisierte Nachtrags-Befüllung für den bereits vorhandenen Bestand, ohne dass der Anwender manuell eingreifen muss. Die dafür nötige Bereitstellung des ffprobe/ffmpeg-Binaries samt Lizenz- und Build-/CI-Betrachtung ist Teil dieses Schritts; Begleitdateien (NFO) dienen nicht als Quelle, da sie keine verlässlichen Laufzeitangaben liefern. Videos, deren Laufzeit auch per ffprobe nicht bestimmt werden kann, müssen als solche erkennbar sein, bleiben für die spätere Sendeplanung ausgeschlossen und dürfen weder Scan noch Betrieb stören. Neue Datenbankfelder sind wie üblich durch Migration und Backup-Registrierung abzusichern, sodass ältere Sicherungen weiterhin wiederherstellbar bleiben.

**Abhängigkeiten:** Keine

**Betroffene Bereiche:** Medien-Scan und -Metadaten, Datenhaltung und Backup, externe Abhängigkeit

### Schritt 2: Sender und Programmregeln verwalten

**Beschreibung:** Ein Administrator soll den automatischen TV-Sender konfigurieren können. Er legt Programmregeln an, bearbeitet, aktiviert/deaktiviert und löscht sie. Eine Regel beschreibt eine wiederkehrende Sendung: die Regelart (Serien-Sendezeit, Film zur Primetime, Genre-/Formatrotation, Wiederholung, Feiertags-Special oder Platzhalter/Füller), das Wiederholungsmuster (täglich, wöchentlich an einem Wochentag, einmalig oder an Feiertagen), die gewünschte Sendezeit und Länge bzw. Anzahl der Folgen, das zugeordnete Programmmaterial sowie eine Priorität für Konflikte zwischen Regeln. Als Programmmaterial kann — je nach Regelart genau eines — eine Serie, ein Film, eine Filmsammlung, ein Genre oder eine bestehende Benutzer-Playlist gewählt werden; die Auswahl erfolgt über Such- und Auswahldialoge auf dem sichtbaren Medienbestand, nicht über interne Nummern. Die Regelverwaltung steht ausschließlich Administratoren zur Verfügung und wird serverseitig abgesichert. Der Sender selbst wird als eigenes, benanntes Objekt geführt, damit später weitere Sender möglich sind; ausgeliefert und bedienbar ist zunächst genau ein Sender. Auch hier gilt: Neue Datenbestände erhalten Migration und Backup-Registrierung mit Regressionstest für Alt-Sicherungen.

**Abhängigkeiten:** Keine

**Betroffene Bereiche:** Administration, Datenhaltung und Backup, Berechtigungen

### Schritt 3: Sendeplan aus Regeln erzeugen

**Beschreibung:** Das System erzeugt aus den aktiven Programmregeln einen fortlaufenden, lückenlos belegten Sendeplan aus konkreten Sendeplätzen — jeder Sendeplatz hat Start, Ende und einen zugeordneten Film oder eine Serienepisode. Der Plan wird rollierend für einen konfigurierbaren Horizont (etwa 48 Stunden) erstellt und im Hintergrund regelmäßig fortgeschrieben, ohne dass ein Anwender eingreifen muss. Serien-Sendezeiten schreiten bei jeder Wiederholung zur nächsten Episode in Sendedreihenfolge (Staffel, Episode) fort; der Fortsetzungsstand wird je Regel beziehungsweise Serie geführt. Feiertags-Specials werden anhand eines fest hinterlegten deutschen Feiertagskalenders erkannt; das Bundesland ist konfigurierbar, eine externe Datenquelle wird nicht benötigt. Überschneiden sich konkurrierende Regeln, entscheidet deren Priorität. Verbleibende Lücken füllt das System mit Platzhalter-Inhalten aus den Füller-Regeln. Eingeplant werden nur Inhalte mit bekannter Laufzeit — Videos ohne ermittelbare Dauer werden übersprungen und sind in der Planung fachlich nachvollziehbar — sowie ausschließlich Inhalte, die für das gesamte berechtigte Sender-Publikum freigegeben sind: Da alle Zuschauer denselben Stream sehen, werden nicht vollständig freigegebene Inhalte bereits bei der Planung ausgeschlossen.

**Abhängigkeiten:** 1, 2

**Betroffene Bereiche:** Sendeplanung, Hintergrundverarbeitung, Datenhaltung und Backup

### Schritt 4: Sendeplan bei Änderungen nachführen

**Beschreibung:** Der Sendeplan bleibt konsistent, wenn sich seine Rahmenbedingungen ändern. Ändert, deaktiviert oder löscht ein Administrator eine Programmregel, bewertet das System den Plan neu: Sendeplätze, die weiterhin zu den aktiven Regeln passen, bleiben unverändert bestehen — insbesondere laufende und bereits gesendete Sendeplätze —, Sendeplätze, die durch die Änderung ungültig werden, werden als verdrängt gekennzeichnet und durch den neuen Plan ersetzt. Wird dadurch eine Serienfolge ausgelassen, plant das System sie zur nächst passenden Gelegenheit desselben Regelkontexts wieder ein, sodass die Episodenreihenfolge ohne Sprung fortgesetzt wird. Ebenso reagiert der Plan auf den Medienbestand: Neu hinzugekommene Episoden einer eingeplanten Serie werden bei der Fortsetzung berücksichtigt; wird ein bereits eingeplantes Video gelöscht, wird der betroffene Sendeplatz als verdrängt gekennzeichnet — eine harte Löschkaskade findet nicht statt, damit die Planhistorie nachvollziehbar bleibt — und die entstehende Lücke wird regelkonform, insbesondere über die Füller-Regeln, neu gefüllt. Die Nachführung erfolgt automatisiert und nachvollziehbar — der Anwender sieht, welche Sendeplätze verdrängt wurden.

**Abhängigkeiten:** 3

**Betroffene Bereiche:** Sendeplanung, Hintergrundverarbeitung

### Schritt 5: Laufendes Programm zeitbasiert auskunftsfähig machen

**Beschreibung:** Zu jedem beliebigen Zeitpunkt kann das System aus dem Sendeplan beantworten, welche Sendung gerade läuft, wie weit sie fortgeschritten ist (Position im Video), wie lange sie noch läuft und welche Sendung als Nächstes folgt. Diese Auskunft ist rein zeitbasiert und zustandslos: Sie hängt ausschließlich von Uhrzeit und Sendeplan ab, nicht davon, ob oder wer gerade zuschaut; nach einem Server-Neustart liefert sie ohne Wiederanlauf sofort den korrekten Stand. Angemeldete Benutzer rufen den aktuellen Stand sowie eine begrenzte Vorschau auf die kommenden Stunden („Was läuft jetzt, was kommt gleich?") über eine abgesicherte Schnittstelle ab; der vollständige Sendeplan bleibt der Administration vorbehalten. Die Programmauskunft ist die einheitliche fachliche Grundlage für Anzeige und spätere Stream-Auslieferung; sie trägt einen eigenen Namen und ist begrifflich klar von den bestehenden Benutzer-Playlists abgegrenzt.

**Abhängigkeiten:** 3

**Betroffene Bereiche:** Programmauskunft, API-Schnittstelle, Berechtigungen

### Schritt 6: Live-Stream ausliefern

**Beschreibung:** Der Sender wird als durchgehender Live-Stream per echter Segmentierung abspielbar. Ein dauerhaft laufender Segmentierungs-/Transcodierungsprozess auf Basis von FFmpeg verarbeitet das laut Sendeplan gerade laufende Video fortlaufend zu HLS-Segmenten und wird am Sendeplatz-Übergang nahtlos auf das nächste Video umgeschaltet, sodass alle Zuschauer zeitgleich denselben Programmpunkt sehen. Die Segmentlänge ist konfigurierbar; nach einem Server-Neustart synchronisiert sich der Prozess allein über Uhrzeit und Sendeplan wieder auf den aktuellen Programmpunkt — es wird kein Prozesszustand fortgesetzt. Die erzeugten Segmente werden an einem konfigurierbaren Speicherort abgelegt; da der Prozess dauerhaft fortlaufend Segmente schreibt, räumt das System abgelaufene Segmente automatisiert anhand einer konfigurierbaren Retention auf, sodass kein ungebremstes Wachstum des Speicherorts entsteht. Die HLS-Wiedergabeliste und die Segmente werden über eigene Endpunkte ausgeliefert und sind nur für authentifizierte Benutzer beziehungsweise gekoppelte Geräte abrufbar; da Videoplayer-Aufrufe keine Autorisierungs-Header mitsenden, wird das etablierte tokenbasierte Abrufmuster über den `access_token`-Abfrageparameter wiederverwendet, und die Zugriffsrechte auf die enthaltenen Inhalte werden serverseitig erzwungen. Der Schritt umfasst außerdem die Einführung der neuen externen Abhängigkeit: Bereitstellung des FFmpeg-Binaries für die unterstützten Umgebungen, Lizenzprüfung sowie Anpassung von Build- und CI-Abläufen.

**Abhängigkeiten:** 5

**Betroffene Bereiche:** Streaming-Auslieferung, API-Schnittstelle, Authentifizierung, externe Abhängigkeit

### Schritt 7: Live-TV in der Benutzeroberfläche

**Beschreibung:** Benutzer können den TV-Sender in der Anwendung ansehen. Eine neue Live-Ansicht enthält einen Videoplayer, der den Live-Stream wiedergibt — in Browsern mit nativer HLS-Unterstützung direkt, in übrigen Browsern über die Ergänzungsbibliothek hls.js, die als neue Client-Abhängigkeit eingebunden wird (einschließlich Prüfung von Lizenz, Release-Build und CI-Skripten). Die Ansicht zeigt außerdem an, was gerade läuft und was als Nächstes kommt, und stellt eine begrenzte Vorschau auf die kommenden Stunden dar; der vollständige Sendeplan bleibt der Administration vorbehalten. Der Sender ist über die Navigation erreichbar. Benutzer ohne Zugriff oder bei nicht verfügbarem Stream erhalten eine verständliche Rückmeldung statt eines technischen Fehlerbilds.

**Abhängigkeiten:** 6

**Betroffene Bereiche:** Benutzeroberfläche, Wiedergabe

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|-----------------|----------------------|
| 1 | Videolaufzeit erfassen | Keine | Medien-Scan und -Metadaten, Datenhaltung und Backup, externe Abhängigkeit |
| 2 | Sender und Programmregeln verwalten | Keine | Administration, Datenhaltung und Backup, Berechtigungen |
| 3 | Sendeplan aus Regeln erzeugen | 1, 2 | Sendeplanung, Hintergrundverarbeitung, Datenhaltung und Backup |
| 4 | Sendeplan bei Änderungen nachführen | 3 | Sendeplanung, Hintergrundverarbeitung |
| 5 | Laufendes Programm zeitbasiert auskunftsfähig machen | 3 | Programmauskunft, API-Schnittstelle, Berechtigungen |
| 6 | Live-Stream ausliefern | 5 | Streaming-Auslieferung, API-Schnittstelle, Authentifizierung, externe Abhängigkeit |
| 7 | Live-TV in der Benutzeroberfläche | 6 | Benutzeroberfläche, Wiedergabe |

## Offene Punkte

Keine.
