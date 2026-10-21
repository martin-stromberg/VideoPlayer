# Projektplan-Gegenprüfung

## Ergebnis

**Status:** Projektplan vollständig

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderungspunkt | Abgedeckt durch Schritt(e) | Status |
|--------------------|-----------------------------|--------|
| Scheduler erzeugt aus administrierbaren Regeln einen fortlaufenden, lückenlosen Programmplan aus Sende-Slots (Start, Ende, Video) | 3 | Abgedeckt |
| Regelarten: Serien-Sendezeit, Film-Primetime, Genre-/Formatrotation, Wiederholung, Feiertags-Special, Platzhalter/Füller | 2 | Abgedeckt |
| Wiederholungsmuster: täglich, wöchentlich (Wochentag), einmalig, Feiertag | 2 | Abgedeckt |
| Regelattribute: Sendezeit, Länge/Slot-Anzahl, Priorität, Aktiv-Flag, genau eine Inhaltsreferenz je Regel (Serie/Film/Filmsammlung/Genre/Benutzer-Playlist) | 2 | Abgedeckt |
| Regelverwaltung: CRUD nur für Administratoren, serverseitig abgesichert, Auswahl über Such-/Auswahldialoge statt interner IDs | 2 | Abgedeckt |
| Sender-Entity kanalfähig angelegt, Auslieferung zunächst genau ein Sender (Annahme 1, Offene Frage 8) | 2 + Vorgehensentscheidung „Sender-Anzahl" | Abgedeckt |
| Sendeplatz-Entity: geplante Sendezeit, polymorphe Videozuordnung (Film/Episode), Status inkl. Verdrängt, optionaler Regelbezug | 3, 4 | Abgedeckt |
| Videolaufzeit pro Film/Episode erfassen (neues Dauer-Feld, beim Scan und nachträglich für Bestand, per ffprobe) | 1 | Abgedeckt |
| Videos ohne ermittelbare Laufzeit: erkennbar, von Planung ausgeschlossen, stören Scan/Betrieb nicht | 1, 3 | Abgedeckt |
| Serien-Fortsetzung: nächste Episode in Sendedreihenfolge (Staffel/Episode), Fortsetzungsstand je Regel/Serie geführt (Annahme 4, `ScheduleProgress`) | 3 | Abgedeckt |
| Feiertags-Specials: fester deutscher Feiertagskalender, Bundesland konfigurierbar (Offene Frage 5) | 3 + Vorgehensentscheidung „Feiertage" | Abgedeckt |
| Konfliktauflösung zwischen Regeln über Priorität | 3 | Abgedeckt |
| Lückenfüllung über Füller-/Platzhalter-Regeln; Platzhalter sind reguläre Mediatheken-Videos (Annahme 3) | 3 + Vorgehensentscheidung „Platzhalter" | Abgedeckt |
| Rollierende, automatisierte Planfortschreibung über konfigurierbaren Horizont (ca. 48 h) im Hintergrund | 3 | Abgedeckt |
| Planänderung bei Regeländerung/-deaktivierung/-löschung: gültige Slots (inkl. laufender/gesendeter) bleiben, ungültige werden verdrängt und ersetzt | 4 | Abgedeckt |
| Verdrängte Serienfolgen werden zur nächst passenden Gelegenheit desselben Regelkontexts wieder eingeplant | 4 | Abgedeckt |
| Reaktion auf Medienbestand: neue Episoden werden berücksichtigt; gelöschtes eingeplantes Video → Slot verdrängt, keine harte Kaskade, Lücke regelkonform neu gefüllt (Offene Frage 4) | 4 | Abgedeckt |
| Nachvollziehbarkeit der Nachführung für den Anwender (sichtbar, welche Sendeplätze verdrängt wurden) | 4 | Abgedeckt |
| Programmauskunft: aktuelles Programm mit Position/Offset, Restdauer und Nachfolger zu jedem Zeitpunkt | 5 | Abgedeckt |
| Auskunft rein zeitbasiert und zustandslos; korrekter Stand nach Server-Neustart ohne Prozesszustand (Offene Frage 10) | 5, 6 + Vorgehensentscheidung „Stream-Charakter" | Abgedeckt |
| Programmauskunft als einheitliche fachliche Grundlage für Anzeige und Stream-Auslieferung; begrifflich getrennt von Benutzer-Playlists (Namenskonflikt `IPlaylistService`) | 5 + Vorgehensentscheidung „Begriffswelt" | Abgedeckt |
| Gemeinsamer, zeitgesteuerter Programmpunkt für alle Zuschauer, kein per-Benutzer-Stream/Timeshift (Annahme 5) | Vorgehensentscheidung „Stream-Charakter", 6 | Abgedeckt |
| Streaming-Pipeline: Entscheidung echte Live-Segmentierung per dauerhaftem FFmpeg-Prozess, Umschaltung am Slot-Übergang (statt virtuellem Stream; Offene Frage 1) | 6 + Vorgehensentscheidung „Streaming-Architektur" | Abgedeckt |
| HLS als Stream-Format (Annahme 2; DASH nachgelagert/nicht umgesetzt) | 6 + Vorgehensentscheidung „Streaming-Architektur" | Abgedeckt |
| Auslieferung von HLS-Wiedergabeliste und Segmenten über eigene Endpunkte, nur authentifiziert (private Mediathek) | 6 | Abgedeckt |
| Auth für `<video>`/hls.js ohne Header: Wiederverwendung des `access_token`-Abfrageparameter-Musters; gekoppelte Geräte berücksichtigt (Offene Frage 6) | 6 | Abgedeckt |
| Serverseitige Durchsetzung der Zugriffsrechte auf Inhalte (keine UI-Ausblendung als Absicherung) | 3, 6 + Vorgehensentscheidung „Zugriffsregelung" | Abgedeckt |
| Sender plant nur für das gesamte Publikum freigegebene Inhalte (Offene Frage 3, bei gemeinsamem Stream einzig konsistente Lösung) | 3 + Vorgehensentscheidung „Zugriffsregelung" | Abgedeckt |
| FFmpeg/ffprobe als neue externe Abhängigkeit: Bereitstellung der Binaries, Lizenzprüfung, Build-/CI-Anpassung | 1, 6 + Vorgehensentscheidung „Externe Abhängigkeit FFmpeg" | Abgedeckt |
| Neue Datenbestände: EF-Migration und Backup-Registrierung mit Regressionstest für Alt-Sicherungen | 1, 2, 3 | Abgedeckt |
| Status-/Vorschau-Schnittstelle für angemeldete Benutzer: „Was läuft jetzt, was kommt gleich?", begrenzte Vorschau; voller Plan nur Administration (Offene Frage 9) | 5, 7 | Abgedeckt |
| Frontend: Live-Ansicht mit Videoplayer, native HLS-Wiedergabe + hls.js-Fallback inkl. Lizenz-/Build-/CI-Prüfung (Offene Frage 7) | 7 | Abgedeckt |
| Frontend: Anzeige „läuft gerade / kommt als Nächstes" + begrenzte Programmvorschau; Navigation; verständliche Fehlerrückmeldung bei fehlendem Zugriff/nicht verfügbarem Stream | 7 | Abgedeckt |
| Konfiguration Anwendungsebene: Planungshorizont, Generierungsintervall, Primetime-Fenster, Bundesland/Feiertagskalender, Segmentlänge | Vorgehensentscheidung „Konfiguration", 3, 6 | Abgedeckt |
| Konfiguration Anwendungsebene: Segment-Speicherort und Segment-Retention inkl. automatisiertem Aufräumen abgelaufener Segmente | 6 + Vorgehensentscheidung „Konfiguration" | Abgedeckt |
| Konfiguration Authentifizierungsmodus für Segmente (Cookie vs. signierte URL/Token) | 6 + Vorgehensentscheidung (tokenbasiertes Abrufmuster festgelegt) | Abgedeckt |
| Konfiguration Administrator-Ebene: Regelwerk (Sendezeiten, Wiederholungen, Inhaltsauswahl, Priorität, Aktiv-Flag) und Platzhalterauswahl | 2 | Abgedeckt |
| Konfiguration pro Slot: Fortsetzungsstand (`ScheduleProgress`) | 3 | Abgedeckt |
| Konfiguration pro Slot: manuell erzeugte Sendeplätze ohne Regel (in der Anforderung nur „falls vorgesehen"/optional) | — | Abgedeckt (optionaler Punkt, im Plan bewusst nicht enthalten — siehe Hinweise) |
| Tests (Unit-/Integrations-/UI-Tests zu Planung, Auskunft, Auth, Alt-Backup) | Schritte 1–3 nennen Alt-Backup-Regressionstest explizit; übrige Testabdeckung folgt der Lifecycle-DoD je Schritt | Abgedeckt |

## Abhängigkeitsprüfung

| Prüfpunkt | Befund |
|-----------|--------|
| Zyklen in Abhängigkeiten | Keine gefunden |
| Alle Abhängigkeitsverweise auflösbar | Ja: Schritt 3 → 1, 2; Schritt 4 → 3; Schritt 5 → 3; Schritt 6 → 5; Schritt 7 → 6 — ausschließlich frühere, existierende Schrittnummern |
| Reihenfolge konsistent zu inhaltlichen Abhängigkeiten | Ja: Schritt 3 benötigt Laufzeiten (1) und Regeln (2); Schritt 4 setzt den erzeugten Plan (3) voraus; Schritt 5 setzt den Plan (3) voraus; Schritt 6 baut auf der Auskunft (5) auf und benötigt transitiv Plan (3), Regeln (2) und Laufzeiten/FFmpeg-Basis (1); Schritt 7 benötigt den ausgelieferten Stream (6) und nutzt transitiv die Auskunft (5). Kein Schritt steht vor einem Schritt, von dem er inhaltlich abhängt. |

## Hinweise

- Die in der ersten Prüfungsrunde (`project-plan-check.1.md`) festgestellte Lücke „Segment-Speicherort/Retention" ist geschlossen: Die Vorgehensentscheidung „Konfiguration" nennt nun Speicherort und Retention, und Schritt 6 beschreibt den konfigurierbaren Ablageort samt automatisiertem Aufräumen abgelaufener Segmente.
- „Manuell erzeugte Sendeplätze ohne Regel" (requirement.md, Konfiguration pro Slot; `ScheduleRuleId` optional) ist in der Anforderung ausdrücklich als „falls vorgesehen" gekennzeichnet und daher nicht als Lücke gewertet. Bei Bedarf nachplanen oder explizit ausschließen.
- Schritt 3 erzeugt mit Sendeplatz und Fortsetzungsstand eigene neue Datenbestände; Migration und Backup-Registrierung sind dort nur über die „Betroffenen Bereiche" (Datenhaltung und Backup) impliziert, nicht ausformuliert wie in den Schritten 1 und 2. Fachlich abgedeckt — beim Lifecycle von Schritt 3 sicherstellen, dass die DoD-Anforderung (Migration + `OptionalRestoreTables`/`OptionalRestoreColumns` + Alt-Backup-Regressionstest) explizit aufgenommen wird.
- „Caching-Header" bei der Segmentauslieferung (requirement.md, Implementierungsansatz 4) ist ein technisches Umsetzungsdetail und wurde nicht als fachliche Lücke gewertet; im Lifecycle von Schritt 6 mitaufnehmen.
- Die Token-Lebensdauer des `access_token`-Abrufmusters (requirement.md, Konfiguration „Authentifizierungsmodus") ist im Plan nicht als konfigurierbarer Parameter benannt; der Modus selbst ist entschieden. Falls eine abweichende Lebensdauer für Live-Segmente nötig wird, im Lifecycle von Schritt 6 klären.
- DASH bleibt gemäß Annahme 2 der Anforderung bewusst nachgelagert; die Entscheidung für HLS ist konsistent.
