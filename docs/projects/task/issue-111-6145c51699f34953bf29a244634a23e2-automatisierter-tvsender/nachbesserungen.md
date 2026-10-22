# Nachbesserungen / Rückmeldungen — Automatisierter TV-Sender

Gesammelte Verbesserungswünsche aus der Nutzung der Testversion.
Status: **offen**, noch nicht geplant/umgesetzt.

## N1 — Mehrfachauswahl bei Genres (Priorität: hoch)

**Beobachtet:** Im Regel-Formular (`TVSenderAdmin.razor`, Material-Dropdown `rule-material-genre`)
ist pro Regel genau **ein** Genre als Programmmaterial wählbar (`ProgramRule.MaterialId` =
eine Genre-ID).

**Gewünscht:** Bei allen Regelarten, die Genre-Material erlauben (Genre-/Formatrotation,
Wiederholung, Feiertags-Special, Platzhalter/Füller), muss eine **Mehrfachauswahl von Genres**
möglich sein — z. B. „Familie + Animation" gemeinsam in einer Rotation.

**Betroffene Stellen:**
- Datenmodell `ProgramRule`: `MaterialType`/`MaterialId` (1:n → Join-Tabelle `ProgramRuleGenre`
  oder vergleichbar), EF-Konfiguration, Migration, `OptionalRestoreTables`/`OptionalRestoreColumns`
- `ProgramRuleService` (Validierung, Input-Modell, `GetAllowedMaterialTypes`)
- `TVSenderAdmin.razor` (Mehrfach-Auswahldialog statt Einfach-Dropdown)
- `ProgramScheduleService.ExpandGenreAsync` (Vereinigung mehrerer Genres, Duplikat-Behandlung,
  Cursor-/Rotationssemantik über die Vereinigungsmenge)
- Tests + Abnahme analog den bisherigen Schritten

## N2 — Quell-Filter bei Genre-Material (Priorität: hoch)

**Beobachtet:** Bei Genre-Auswahl werden implizit **alle Medienquellen** berücksichtigt —
die Episoden/Filme des Genres aus sämtlichen eingerichteten Quellen.

**Gewünscht:** Bei Genre-Material muss (ebenfalls per **Mehrfachauswahl**) festgelegt werden
können, **welche Quellen (`MediaSource`)** für diese Regel betrachtet werden. Beispiel:
„Genre Familie, aber nur aus Quelle A und B, nicht aus dem Archiv-Server."

**Betroffene Stellen:**
- Datenmodell: Quell-Filter je Regel (Join-Tabelle `ProgramRuleSources` oder an die
  Genre-Verknüpfung gekoppelt — Designentscheidung bei Umsetzung klären: Filter pro Regel
  oder pro Genre-Eintrag)
- `TVSenderAdmin.razor`: zweite Mehrfach-Auswahl (Quellen-Liste) bei Genre-Material
- `ProgramScheduleService.ExpandGenreAsync` + ggf. weitere Expand-Pfade: Items auf die
  gewählten `MediaSourceId`s einschränken (siehe `MediaItem`/`TVShowEpisode` → Quellzuordnung)
- Interaktion mit Freigabe-Filter (`IUnlockedMediaService`): Quell-Filter zusätzlich,
  nicht ersetzend
- Sinnvoller Default: **keine Auswahl = alle Quellen** (Abwärtskompatibilität zu N1/N2
  für bestehende Regeln — Migration mit leerem Filter = uneingeschränkt)

## Hinweise zur Einordnung

- Beide Punkte betreffen das **gleiche Formularfeld** (Programmmaterial bei Genre) und das
  **gleiche Datenmodell** (`ProgramRule.MaterialId` als Einzelwert) — sinnvoll gemeinsam
  in einem Schritt umzusetzen, da sonst doppelte Migrationen/Formular-Umbauten anfallen.
- Offene Designfrage bei Umsetzung: gilt die Quell-Differenzierung nur für Genre-Material
  oder generell pro Regel (auch Serie/Film/Sammlung/Playlist)? Anforderung spricht zunächst
  nur von Genres.

## N3 — Genre-Auswahl berücksichtigt Status und Alternativnamen (Priorität: hoch)

**Beobachtet:** Das Genre-Dropdown im Regel-Formular wird über `api/items/genres`
(`ItemsController` → `MediaMetadataEditorService.GetGenreOptionsAsync`) befüllt. Diese
Methode gruppiert **alle** Genres ausschließlich nach Roh-`Name` — weder `Genre.IsHidden`
(Status „Ausgeblendet") noch `Genre.AlternateNames` werden berücksichtigt.

**Gewünscht:** Die Genre-Auswahl muss denselben Status-/Alternativnamen-Regeln folgen wie
der Rest der Anwendung (vgl. `GenreAdmin.razor`, `GetOrCreateGenresAsync` in
`MediaSourceClassifier`, `SourceGenresController` mit `Where(g => !g.IsHidden)`):

- Ein Genre mit Status **„Ausgeblendet"** (`IsHidden = true`) darf nicht unter seinem
  eigenen Namen wählbar sein.
- Hat das ausgeblendete Genre **Alternativnamen**, gilt die Auswahl dem Ziel-Genre dieser
  Umleitung. Beispiel aus der Praxis: Genre „Family" ist ausgeblendet und hat den
  Alternativnamen „Familie" → in der Auswahl darf **nur „Familie"** stehen (Auswahl des
  kanonischen Genres), nicht „Family".
- Angezeigt wird also die Menge der **sichtbaren, kanonischen Genres** nach Auflösung der
  Alternativnamen — analog der Scan-Logik, die „Family" beim Einlesen auf „Familie" umlenkt.

**Betroffene Stellen:**
- `MediaMetadataEditorService.GetGenreOptionsAsync` (bzw. ggf. eine eigene,
  TV-Sender-spezifische Optionsquelle — Achtung: derselbe Endpoint wird auch vom
  Metadaten-Editor genutzt; Änderung dort absichtlich oder separat entwerfen)
- Alternativnamen-Auflösung beim Mapping `MaterialId` → Inhalte: Der Scanner verknüpft
  Inhalte über die Umleitung mit dem kanonischen Genre, **Altbestand** kann aber noch
  `MovieGenre`/`TVShowGenre`-Zeilen auf das ausgeblendete Genre „Family" besitzen —
  `ExpandGenreAsync` sollte Inhalte auflösungskonform einschließen (inkl. verknüpfter
  Alt-Zuordnungen auf die Alternativnamen des gewählten Genres bzw. umgekehrt)
- Zusammenspiel mit **N1** (Mehrfachauswahl: die Liste der kanonischen Genres) und **N2**
  (Genres sind quellbezogen: `Genre.MediaSourceId` — die Dedupe nach Name über Quellen
  hinweg muss mit dem Quell-Filter konsistent bleiben)
- `Genre.StartDate`/`EndDate`-Sichtbarkeitsfenster analog mitbehandeln (zeitweise
  ausgeblendete Genres wie in der Genre-Verwaltung)
- Tests: verstecktes Genre mit Alternativname → nur Ziel-Genre wählbar; verstecktes Genre
  ohne Alternativname → nicht wählbar; zeitlich eingeschränktes Genre → Status gemäß
  Sichtbarkeitsfenster

## N4 — ffprobe-Timeout bei entfernten (pipe:0) Quellen + kein Retry bei Probe-Fehlern (Priorität: kritisch)

**Beobachtet (Live-Befund, 2026-10-21):** Der `MediaDurationProbeWorker` läuft, aber
quasi jeder ffprobe-Aufruf über `pipe:0` (SFTP-gestreamte Quelle) läuft ins
**120s-Timeout** — Log voller „ffprobe-Aufruf für 'pipe:0' hat das Timeout von 120s
überschritten." Stand: 111.783 MediaItems, davon 49 erfolgreich, 29 failed,
111.705 ausstehend → bei ~30 Items/Stunde nie fertig. Vermutete Ursache: ffprobe muss
bei Containern ohne vorgelagertes `moov`-Atom (typisches MP4) die **ganze Datei** lesen
— über SFTP-Pipe zu langsam für 120 s.

**Verschärfend:** Probe-Fehler (inkl. Timeout) markieren das Item dauerhaft als
fehlgeschlagen (`DurationProbedAt` gesetzt, `Duration` NULL, kein Retry) → der Bestand
wird sukzessive als „unplanbar" gebranntmarkt, obwohl die Ursache nur Latenz ist.

**Gewünscht:**
- ffprobe für Remote-Quellen robuster machen: längeres Timeout für pipe:0 (konfigurierbar),
  ggf. `-show_format`-Fastpath / Begrenzung der einzulesenden Daten, oder
  Dateipfad-Variante wo möglich (Netzlaufwerk/SMB), alternativ lokaler Temp-Cache.
- **Retry-Strategie für Probe-Fehler**: Timeouts/IO-Fehler nicht permanent markieren —
  z. B. Retry-Zähler mit Backoff oder „Probe zurücksetzen"-Funktion, damit temporäre
  Fehler den Bestand nicht dauerhaft aus der Planung entfernen.
- Batch-Größe/Parallelisierung prüfen (aktuell ~1 Item pro 2 min bei Timeout ist
  effektiv ein Dauer-Backlog).

## N5 — Planungsdiagnose in der UI sichtbar machen (Priorität: mittel)

**Beobachtet:** Die Admin-Karte „Sendeplan" zeigt einfach „keine Einträge". Warum die
Regel nichts plant, steht nur im Log („liefert kein planbares Material — verwaist, ohne
Laufzeit oder nicht freigegeben").

**Gewünscht:**
- Pro Regel in der Übersicht eine **Diagnose**: Anzahl planbarer Inhalte bzw. Grund der
  Leere (keine Laufzeit ermittelt / nicht für alle freigegeben / Material gelöscht).
- Optionaler **„Jetzt generieren"-Button**, der `IProgramScheduleSignal` auslöst, damit
  man nach Konfig-Änderungen nicht auf das Intervall warten muss.
- Ergänzend N2/N3: Das Genre-Dropdown dedupliziert über Quellen hinweg und löst auf eine
  beliebige Genre-Id auf (hier: „Familie" → Genre 15 auf Quelle 2 = nur Filme; die
  eigentlichen Familien-Serien liegen unter „Familie"=64 auf Quelle 1 bzw. „Family"=115/131
  auf Quelle 5/6, teils noch unversteckt). Ohne Quell-Filter und Auflösung der
  Alternativnamen ist die Auswahl faktisch unbestimmt.

## N6 — Fortschritt der Laufzeitermittlung sichtbar machen (Priorität: mittel)

**Beobachtet:** Der Anwender kann nicht erkennen, ob der Duration-Backfill
(`MediaDurationProbeWorker`) voranschreitet oder hängt. Erkennbar ist der Fortschritt
aktuell nur über Log-Datei oder DB-Abfrage (`DurationProbedAt`-Zähler).

**Gewünscht:** Eine Fortschritts-/Statusanzeige für die Laufzeitermittlung, z. B. auf der
TV-Sender-Adminseite oder als Ticker/Statuszeile: Anzahl geprüft/ausstehend/fehlgeschlagen,
zuletzt ermittelte Laufzeiten (Rolling-Log „was wurde gerade erfasst"), letzter
Aktivitätszeitpunkt, ggf. Rate. Damit ist ohne Log lesbar, ob der Prozess läuft.
Datenquelle: `MediaItem.DurationProbedAt`/`Duration`/`DurationProbeAttempts` —
Aggregatabfrage ist günstig (gezählte Spalten), kein neuer Persistenzbedarf.

**Bezug:** ergänzt N5 (Planungsdiagnose pro Regel); beide zusammen decken „warum plant
die Regel nichts" (N5) und „schreitet die Laufzeitermittlung voran" (N6) ab.

## N7 — Neue Genre-Zeilen erben die globale Sichtbarkeit/Alternativnamen nicht (Priorität: hoch, BUG)

**Beobachtet (Code-verifiziert):** `GenreAdmin.razor` verwaltet Genres **global pro
Name** (`GroupBy(g.Name.ToLower())`; `SaveGenre` wendet `IsHidden`/`StartDate`/`EndDate`/
`AlternateNames` auf ALLE gleichnamigen Zeilen aller Quellen an). `GetOrCreateGenresAsync`
in `MediaSourceClassifier` erzeugt beim Scan jedoch neue `Genre`-Zeilen quellbezogen mit
`IsHidden = false` und **ohne** `AlternateNames` — die Admin-Intention gilt nur für
rückwirkend vorhandene Zeilen, nicht für neu hinzukommende Quellen/Rescans.

**Live-Beleg in der Test-DB:** „Family" auf Quelle 1+2 ausgeblendet→„Familie" (vor den
Quellen 3/5/6 gesetzt), auf Quelle 3/5/6 danach gescannt → dort sichtbar, keine
Alternativnamen → „Family" erscheint trotz globaler Ausblendung in Auswahlen/Anzeigen.

**Zweiter Aspekt (case):** Anzeige gruppiert case-insensitiv (`ToLower()`), `SaveGenre`
filtert `g.Name == editGenre.Name` exakt — „family" vs. „Family" würde divergieren.

**Gewünscht:**
- Bei der Genre-Erzeugung im Scan (`GetOrCreateGenresAsync` o. ä.) muss die **globale
  Konfiguration eines gleichnamigen Genres** (Name, case-insensitiv) übernommen werden:
  `IsHidden`, `StartDate`/`EndDate`, `AlternateNames` — sodass „Family" auf jeder neuen
  Quelle sofort ausgeblendet und auf „Familie" umgelenkt ist.
- Alternativ-/Begleitlösung prüfen: Sichtbarkeit/Alternativnamen kanonisch **pro Name**
  auswerten (statt pro Zeile) — Design-Entscheidung bei Umsetzung; die einfachste
  konsistente Variante ist die Vererbung beim Erzeugen, weil `SaveGenre` schon global
  pro Name arbeitet.
- `SaveGenre`-Case-Mismatch mitbeheben (Filter case-insensitiv wie die Gruppierung).
- **Tests (fehlen aktuell — explizit gefordert):** Genre „Family" versteckt+Alternativname
  auf Quelle 1 → Scan/Genre-Anlage auf Quelle 2 mit „Family"-getaggtem Inhalt → neue Zeile
  muss versteckt sein und Alternativnamen tragen → Inhalt landet beim kanonischen Genre.

**Bezug:** ursächlich für den beobachteten Effekt aus N3-Kontext („Family" weiterhin
auswählbar) — N3 filtert korrekt pro Zeile, aber die fehlende Vererbung macht die
Ausblendung faktisch unwirksam für neue Quellen. Wirkt sich auch auf `GenreCanonicalResolver`
und die bestehenden Genre-Listen/Filter aus (nicht nur TV-Sender).

## N8 — Remote-Probes hängen: pipe:0 liest ganze Dateien (Priorität: kritisch, BUG)

**Beobachtet (Live-Befund, 2026-10-21):** Fortschrittsanzeige bleibt bei „54 von 15386
ermittelt" stehen. Log zeigt: Probes über `pipe:0` (SFTP-Stream) laufen fast alle in das
600-s-`StreamTimeoutSeconds`, vereinzelt `Exit 1: moov atom not found`. Ursache: ffprobe
muss bei MP4s mit `moov`-Atom am Dateiende die **komplette Datei** durch die Pipe lesen —
über SFTP zu langsam. Retry hilft nicht: jeder Versuch kostet wieder 600 s und scheitert
erneut → Fortschritt stagniert sichtbar, Versuche verbrennen Attempts.

**Gewünscht:** Remote-Probes **seekbar** statt Streaming-Pipe. ffmpeg kann `sftp://`-URLs
nativ (libssh, unterstützt Seek) — mit `MediaSource.Username`/`Password` direkt als
`sftp://user:pass@host:port/path` proben → ffprobe liest nur moov/Header, Sekunden statt
Minuten. Alternativ eigener HTTP-Endpoint mit Range-Support + `access_token`
(quellunabhängig). Sicherheit: Passwort im Prozess-Aufruf ist für denselben
Benutzer sichtbar — Entscheidung dokumentieren, Passwort auf keinen Fall in Logs.
Nebenbei: `moov atom not found` nach vollständigem Lesen ist eher permanent (kaputte/
unkonventionelle Datei) — Klassifikation prüfen, damit solche Dateien nicht 10×600 s
verschwenden.

**Bezug:** verschärft N4 (Timeout/Retry allein reichte nicht — die Zugriffsart selbst ist
das Problem). Betrifft auch den Live-Stream (Schritt 6, gleiche Pipe-Einschränkung —
dort dokumentiert).

## Nachtrag zu N8 — sftp://-Ansatz scheitert in der Praxis (live verifiziert)

Der Schritt-12-Ansatz `sftp://` wurde live getestet: **jede** libssh-Verbindung wird beim
Handshake zurückgesetzt (`Connection failed: Socket error: connection reset by peer`),
obwohl SSH.NET denselben Server (`mstromberg.ddns.net:10022`) problemlos erreicht. Die im
ffmpeg-Build gebündelte libssh ist zu alt/inkompatibel (keine korrigierbaren Client-Optionen,
`known_hosts`-Handling nicht exponiert). sftp:// bleibt als Pfad bestehen (auf kompatiblen
Servern funktioniert er), ist aber kein verlässlicher Fix.

## N9 — Remote-Probes über eigenen Range-fähigen HTTP-Endpunkt (Priorität: kritisch)

**Gewünscht:** Remote-Items (und generell alles, was die App streamen kann) via
`http://127.0.0.1:{port}/api/items/{type}/{id}/stream?access_token={token}` proben —
der Endpunkt hat bereits `enableRangeProcessing: true`, ffprobe kann also seeken und liest
nur moov/Header statt der ganzen Datei. Umgeht libssh komplett, nutzt denselben
SSH.NET-Read-Pfad wie Wiedergabe — funktioniert source-typ-unabhängig.

**Zu klären bei Umsetzung:**
- Auth: interner, kurzlebiger Token für den server-internen Aufruf (nicht das
  Benutzer-Token; `DeviceTokenService`/`access_token`-Mechanismus prüfen und passend
  wiederverwenden bzw. internes Pendant).
- Loopback-URL/Port aus Konfiguration des eigenen Web-Hosts (`ASPNETCORE_URLS`/Setup).
- Mapping `MediaItem` → `{type}/{id}` für den Stream-Endpunkt.
- ffprobe http-Protokoll: `Range`-Requests funktionieren; Timeouts (`timeout`/`rw_timeout`)
  sinnvoll setzen; Response muss Range unterstützen (Integrationstest mit echtem ffprobe
  gegen Testserver falls möglich).
- Prüfen: schlägt der Ansatz auch fürs Streaming (Schritt 6, pipe:0-Grenze) vor?
  Erstmal nur Probe-Seite.

## N10 — Saisonale Genres sind im Regel-Formular vor ihrem Fenster nicht wählbar (Priorität: hoch)

**Beobachtet (Live-Befund):** Genre „Halloween" (sichtbar, `StartDate=23.10.`/`EndDate=08.11.`)
ist vor dem 23.10. nicht in der Material-Auswahl des Regel-Formulars — der
`GenreCanonicalResolver` filtert das Sichtbarkeitsfenster auch für die *Konfigurations*-
Auswahl. Der Admin kann eine Halloween-Regel damit erst innerhalb des Fensters anlegen —
zu spät für Vorausplanung.

**Gewünscht:** `GetGenreMaterialOptionsAsync` bietet Genres **unabhängig vom
Sichtbarkeitsfenster** an (IsHidden-/Alternativnamen-Auflösung bleibt). Fenster-Genres
dürfen gern markiert werden (z. B. „Halloween (23.10.–08.11.)"). Das Fenster wirkt
weiterhin korrekt auf die *Expansion*: außerhalb der Saison liefert das Material nichts,
innerhalb schon — so kann die Regel dauerhaft existieren und saisonal greifen.

## N11 — Trailer als Vorlauf-Füllmaterial zu eingeplanten Filmen (Priorität: mittel)

**Beobachtet:** Keine Trailer-Verbindung im Datenmodell/Scanner — `grep -rni "trailer"` im
Produktcode ohne Treffer. Der Anwender wünscht: Ist ein Film im Sendeplan, soll sein
zugehöriger Trailer in den Tagen davor als Füllmaterial zur Verfügung stehen (klassischer
Vorlauf-Werbung-Effekt).

**Zu klären/umzusetzen:**
- **Verknüpfung Trailer↔Film:** übliche Dateikonventionen (`-trailer`/`trailer`-Suffix
  neben der Filmdatei, `Trailers/`-Ordner — Plex/Jellyfin-Konvention) beim Scan erkennen
  und als eigene Beziehung persistieren, oder explizite Zuordnung über UI. Entscheidung
  dokumentieren; Konvention ist wartungsärmer.
- Trailer-Items brauchen ebenfalls `Duration` (läuft über denselben Probe-Pfad) und
  dürfen nicht als reguläre Filme im Plan landen (Scan-Klassifizierung beachten).
- **Planlogik:** Füller-Auswahl gewichtet Trailer bevorzugt, deren Film in den nächsten
  X Tagen im Sendeplan steht (Vorlauf konfigurierbar, z. B. 7 Tage) — neue Materialart
  oder Füller-Regel-Option „Trailer anstehender Filme".

## N12 — Maskottchen-/Kennzeichen-Clips als Füller (Priorität: niedrig)

**Anforderung:** Kurze Clips mit eigenem Sender-Maskottchen (Heinzelmännchen-Prinzip) als
Lückenfüller/Kennzeichen.

**Produktion:** **Keine Zusatzsoftware in der App nötig.** Clips sind gewöhnliche
Videodateien — produziert werden sie außerhalb (Blender/After Effects/AI-Video-Tools oder
einfache gerenderte Sequenzen) und als Dateien in die Mediathek gelegt. In der App genügt
die bestehende Füller-Regel: Clips in eigene Sammlung/Playlist „Maskottchen" → Füller-Regel
mit diesem Material (ggf. eigene Quelle ohne Serien/Film-Einordnung, damit sie nicht als
reguläre Inhalte erscheinen).

**Umzusetzen (klein):** ggf. Kennzeichnung „Interstitial/Kennzeichen" damit Maskottchen-
Clips nicht als normale Filme auftauchen + Bevorzugung als Füller (Rotation innerhalb der
Clip-Sammlung). Duration-Pfad gilt automatisch.

## N13 — Fehlgeschlagene Laufzeitmessungen einsehbar machen (Priorität: mittel)

**Beobachtet:** Die Fortschrittskarte zeigt den Zähler „fehlgeschlagen" — aber nicht,
welche Dateien betroffen sind. Der Anwender kann also nicht beurteilen/beheben, was
scheitert (korrupte Datei vs. Pfadproblem vs. moov-los).

**Gewünscht:** Info-/Aufklapp-Schaltfläche am Fehlgeschlagen-KPI (und sinnvoll auch
„ausstehend") in der Fortschrittskarte (`#tvsender-probe-card`), die die Liste der
betroffenen Dateien anzeigt: Name, Quelle, Anzahl Versuche, Zeitpunkt des letzten
Versuchs — analog `MediaDurationProbeRecentItem`. Datenquelle vorhanden:
`MediaItem` mit `Duration null` + `DurationProbeAttempts >= MaxAttempts`
(failed) bzw. `DurationProbedAt null` (pending); `GetProgressAsync` analoge Abfrage.
Optional: „Probe zurücksetzen" pro Eintrag (Attempts zurücksetzen → erneuter Versuch),
falls das Fehlerbild korrigierbar ist (z. B. Datei ersetzt).

## N14 — TV-Sender-Adminseite neu strukturieren (Priorität: mittel, UX)

**Beobachtet:** `/admin/tv-sender` ist unübersichtlich geworden — die Seite trägt inzwischen
vier Belange nacheinander: Sender-Einstellungen (Name, Bundesland, Horizont, Intervall,
Segment-Parameter), Regel-Liste + Regel-Editor, Sendeplan-Karte, Fortschrittskarte der
Laufzeitermittlung. Alles untereinander auf einer langen Seite.

**Gewünscht:** Neu strukturieren — Optionen:
- **Tabs/Sektionen**: z. B. „Regeln", „Sendeplan", „Einstellungen", „Laufzeitermittlung" —
  nav-seitig ein Link, inhaltlich klar getrennt (Muster evtl. aus anderen Admin-Seiten
  übernehmen, Konsistenz mit `GenreAdmin`/`Backups` prüfen).
- Oder **einklappbare Karten** mit sinnvollen Defaults (Regeln offen, Rest zu).
- Regel-Editor ggf. als Dialog statt Inline-Block; Diagnose-Spalte + „Jetzt generieren"
  gehören thematisch an den Sendeplan, Fortschrittskarte ist Infrastruktur-Status.
- Usability-Review + bUnit/Playwright-Abdeckung der neuen Struktur.

**Vorgehensentscheidung (2026-10-21):** Zuerst **Variante 2** — Agent erstellt eine
statische HTML-Vorschau der umstrukturierten Seite zur visuellen Abnahme durch den
Anwender, bevor die Umsetzung in TVSenderAdmin.razor erfolgt. Falls das Ergebnis nicht
tragfaehig ist, kann der Anwender alternativ einen Stitch-Entwurf liefern (Variante 1).

**Erweiterung (2026-10-21):** Die Aufgabe umfasst die tatsaechliche Clip-Produktion — im
Rahmen des Schritts werden (a) kleine Drehbuecher fuer Maskottchen-Szenen geschrieben und
(b) die Videos mit passenden Tools generiert und in die Mediathek eingebracht.
Werkzeug-Offenheit: KI-Videogeneratoren brauchen Zugang/Credentials (Runway/Pika/Sora o.ae.
— Verfuegbarkeit und Kosten vorab klaeren); als garantiert verfuegbare Alternative dient
prozedurale Generierung mit vorhandenen Mitteln (ffmpeg-Animationen/zoompan, ggf. Blender
als Skript-Renderer, SVG/PNG-Frame-Sequenzen). Drehbuecher + fertige Clips landen als
Assets im Projektverzeichnis bzw. einer Maskottchen-Sammlung in der Mediathek.

Stand 2026-10-21: Es existiert **kein** Zugang zu KI-Videogeneratoren. Werkzeug-Auswahl und
-pruefung sind Teil des Schritts (Evaluierung freier Zugaenge vs. prozedurale Generierung
mit ffmpeg/Blender als Fallback), Entscheidung wird mit dem Anwender im Schritt getroffen.

## N15 — Live-Stream restartet ffmpeg alle ~16 s (Pipe bricht, kein stderr im Log) (Priorität: hoch)

**Beobachtet (Live-Befund, 2026-10-22):** Live-TV zeigt „Stream momentan nicht
verfügbar, wird erneut versucht". Log zeigt: `FfmpegLiveStreamSegmenter` startet für
Sendeplatz 5 alle ~14–17 s neu (`Die Pipe wurde beendet` in `PumpInputAsync` —
ffmpeg verlässt stdin = ffmpeg-Prozess beendet sich), Position rückt je Versuch ~30 s
vor. Degradiert statt läuft; kein Warmlauf.

**Befund:** ffmpeg beendet mitten im Lauf; dessen stderr wird gedrained, aber **nicht
geloggt** → Ursache unsichtbar (Diagnosability-Lücke). Unter dem Verdacht steht der
pipe:0-Pump über SFTP (denselben Pfad nutzt N8/N9 für Probes nicht mehr).

**Umzusetzen:**
1. **stderr des Segmenter-ffmpeg bei unerwartetem Exit loggen** (Exit-Code + Tail) —
   sonst ist der Fehler blind.
2. **Seekfähige Eingabe statt pump:0**: dasselbe interne Range-Endpoint wie N9
   (`api/internal/media-stream/{token}`) auch als ffmpeg-Input für den Segmenter —
   ffmpeg liest mit Range-Requests, kann seeken, kein Volldurchlauf nötig.
   Nebenprodukt: `-ss` wird wirksam und moov-am-Ende-Probleme entfallen.
3. Neustart-Strategie prüfen: exakt +30 s-Sprünge wirken wie positions-basierte
   Wiederholung — dokumentieren/korrigieren, falls Doppel-Belegung droht.

## N16 — Planer verdrängt Kandidaten en masse statt stabilen Plan (Priorität: kritisch, BUG)

**Beobachtet (Live-Befund, 2026-10-22):** Regel 4 „Vorbendserien" (täglich 16:00, 4 h,
GenreRotation, Genres Quelle 1+2, Filter Quelle 1) hat **35 Slots, alle `Displaced`** —
kein einziger `Scheduled`. Heute 14–18 Uhr komplett leer; Regel-4-Slots liegen erst
morgen 14:43–18:30 und **überlappen sich** (8 Items starten ~14:43–14:45). Gesamtzahlen
pathologisch: **4.520 `Displaced` vs. 38 `Scheduled`** — Regel 7 hat 4.374 verdrängte,
geballt morgen 04:00–07:41. Material ist planbar (35 Items verknüpft, kein
„kein planbares Material" für Regel 4 im Log).

**Verdacht:** `ReconcileAsync`/`MaterializeOccurrenceEvicting` (Evict-Pfad) schreibt
Kandidatenbatches, die der nächste Lauf als Konflikt verdrängt, statt stabil zu bleiben —
Oszillation statt Konvergenz. Cluster-Overlaps deuten zusätzlich auf eine Platzierung,
die Kandidaten nicht kumulativ legt. `coverageStart`-/Horizont-Logik: heute 16:00 blieb
komplett leer, obwohl das Fenster noch in der Zukunft lag.

**Umzusetzen:** Planner-Debug — reproduzierbarer Test: GenreRotation-Regel + material,
`EnsureCoverageAsync` zweimal hintereinander → 2. Lauf darf bestehende Slots nicht
verdrängen (idempotent). Eviction so einschränken, dass nur echte Konflikte (höhere
Priorität) verdrängen; Overlap-Cluster unterbinden; Regel-4-Leer-Fall heute erklären.

## N17 — Slot-Wechsel: Player hängt an Playlist-Grenze (kein EXT-X-DISCONTINUITY) (Priorität: hoch)

**Beobachtet (Live-Befund, 2026-10-22):** Beim Wechsel Sendeplatz 6→7 (14:30:50) startete
der neue ffmpeg-Lauf serverseitig sauber (`-ss 0`, `-t 2733`), schreibt aber `live.m3u8`
**komplett neu**: neue Segment-Dateinamen (`_7_`), Media-Sequence von vorn, Zeitbasis
springt zurück. Im Player friert das letzte Bild ein, Ladeanimation — hls.js kann die
neue Playlist nicht an die Pufferung anschließen (kein `#EXT-X-DISCONTINUITY`, kein
sequenzieller Übergang). Pause+Neustart geht, weil die Playlist frisch geladen wird.

**Umzusetzen:** Nahtloser Slot-Wechsel für hls.js-Clients, z. B.:
- `#EXT-X-DISCONTINUITY` an der Übergangsstelle serverseitig in die ausgelieferte
  Playlist injizieren (LiveStreamController beim Rewrite), sobald die laufende
  ffmpeg-Generation wechselt — üblichste Lösung für zusammengesetzte Live-Pläne;
- oder Segment-/Sequence-Nummern über Job-Grenzen hinweg fortlaufen lassen
  (Start-Sequence des neuen Jobs an die alte anschließen);
- oder ganz anders: ein ffmpeg-Dauerlauf mit Input-Umschaltung (concat/live-switch) —
  größerer Umbau, nur wenn Markerweg nicht reicht.
- Player-seitig (liveTv.js) prüfen: hls.js-Fehlerbehandlung beim Playlist-Wechsel,
  ggf. `liveSyncPosition`/Recovery-Verhalten.
- Regressionstest: zwei aufeinanderfolgende Slots durchspielen (E2E mit echtem ffmpeg +
  zwei Dummy-Videos), Playlist-Übergang muss EXT-X-DISCONTINUITY enthalten.

## N18 — „Bereinigen"-Funktion für verdrängte Sendeplätze (Priorität: mittel)

**Gewünscht (Anwender):** Button „Verdrängte bereinigen" im Sendeplan-Tab
(`TVSenderAdmin.razor`), der alle `ProgramSlots` mit `Status=Displaced` löscht
(`DELETE FROM ProgramSlots WHERE Status=1` über `IProgramRuleService`/`ProgramScheduleService`)
— mit Bestätigungsdialog + Ergebnis-Anzahl. Sicherheit: nur Admin, serverseitig.
Optional: zusätzlich „Gesamten Plan zurücksetzen" (alle Slots + Cursor) für
Debug/Fallback-Fälle — separate, markierte Aktion.

## Nachtrag zu N16 — Rest-Befund nach Fix (2026-10-22)

Nach Neustart mit Fix: **keine Oszillation mehr** (0 neue Displaced nach 16:00) — aber
Regel 4 erzeugt **gar keine** Slots mehr (10.205 planbare Items, 0 scheduled/displaced
seit Neustart). Mögliche Rest-Ursachen: Occurrence teilweise vergangen heute wird
verworfen; `coverageStart`-/Blocked-Logik; Cursor `LastContentId=8961`. Zu prüfen:
`EnsureCoverageAsync` für ein „heute 16:00, jetzt 16:30"-Szenario — kommt die
Occurrence überhaupt zur Materialisierung (`DroppedConflicts`-/`DroppedPast`-Counter,
Log-Level hochdrehen)?
