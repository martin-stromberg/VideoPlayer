# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (Admin-Seite „TV-Sender", Tab „Einstellungen")

- **Widersprüchlicher Zustand / Sackgasse beim Speichern** — Ist der zuvor gewählte
  Standby-Clip gelöscht worden, zeigt der Chip „Zuvor gewählter Clip ist nicht mehr
  vorhanden — Standard-Clip wird verwendet" und suggeriert, alles sei in Ordnung. Intern
  bleibt die verwaiste Auswahl jedoch gesetzt: Klickt die Anwenderin auf „Speichern" —
  auch für eine ganz andere Einstellung auf derselben Karte (Sendername,
  Segmentlänge usw.) — schlägt der Vorgang mit „Das gewählte Standby-Video wurde nicht
  gefunden." fehl, obwohl die Anzeige behauptet, der Standard-Clip werde bereits
  verwendet. Der Ausweg (Picker öffnen → „Standard-Clip verwenden") ist nicht
  naheliegend, weil dieser Zustand ja scheinbar schon gilt.
  Empfehlung: Beim Anzeigen des Fallback-Textes die verwaiste Auswahl still auf
  „Standard" zurücksetzen, oder am Chip eine Aktion wie „Auswahl verwerfen" anbieten —
  jedenfalls muss „Speichern" in dem angezeigten Zustand funktionieren.

- **Fachjargon / interne Begriffe** — Überschrift „Standby-Clip (Warm-up-Phase)" und
  Hilfetext „…die Phase, in der noch keine echte Wiedergabeliste existiert … sowie
  jeden Zustand ohne produzierte Segmente. Angeboten werden nur nicht klassifizierte
  Videodateien." sprechen in Server-Interna („Warm-up", „Wiedergabeliste",
  „produzierte Segmente", „nicht klassifiziert"). Der Leerzustand darunter formuliert
  dasselbe verständlicher („Videodateien ohne Film-/Serien-Zuordnung").
  Empfehlung: Überschrift ohne Klammerzusatz bzw. „Standby-Clip (Überbrückung)";
  Hilfetext anwenderorientiert, z. B. „Wird gezeigt, bis der Live-Stream nach einem
  Server-Start wirklich läuft. Auswählbar sind Videodateien ohne Film-/Serien-Zuordnung."

- **Ungespeicherte Auswahl ohne Hinweis** — „Auswählen" und „Standard-Clip verwenden"
  wirken erst nach dem weit unten stehenden „Speichern"; weder der Chip noch die
  Schaltfläche zeigen an, dass die Änderung noch nicht übernommen ist. In den
  Clip-Gruppen (gleiches Kandidatenmuster) wirkt „Hinzufügen" dagegen sofort — die
  Anwenderin kann so erwarten, dass die Wahl schon gilt, und die Seite mit
  ungespeicherter Auswahl verlassen.
  Empfehlung: Hinweis am Chip, z. B. „Auswahl wird mit „Speichern" übernommen", oder —
  konsequenter — die Wahl sofort speichern wie bei den Clip-Gruppen.

- **Picker-Bedienung** — Der Umschaltknopf heißt auch bei geöffnetem Picker weiter
  „Ändern"; es gibt kein „Schließen"/„Fertig" und kein `aria-expanded`. Die Anwenderin
  muss erraten, dass erneutes „Ändern" die Auswahl wieder zuklappt.
  Empfehlung: Beschriftung bei geöffnetem Picker auf „Auswahl schließen" wechseln und
  `aria-expanded` setzen.

- **Flackernder Leerzustand / fehlendes Lade-Feedback** — Beim Öffnen des Pickers
  erscheint sofort „Keine zuordenbaren Clips gefunden", solange die erste Abfrage noch
  läuft; auch „Weitere laden" gibt während des Nachladens keine Rückmeldung. Das
  übernimmt zwar das Bestandsmuster der Clip-Gruppen, bleibt aber eine irreführende
  Zwischenanzeige.
  Empfehlung: Während des Ladens „Clips werden geladen…" anzeigen und „Weitere laden"
  bis zum Ergebnis sperren.

### LiveTv.razor (Zuschauer-Seite „Live-TV")

Keine Befunde. Das neue `data-error-kind`-Attribut an `#live-tv-stream-error` ist ein
reines Diagnosemerkmal ohne sichtbare Wirkung; die Meldung „Der Live-Stream ist
momentan nicht verfügbar. Es wird automatisch erneut versucht." ist verständlich und
handlungsfrei. In der Warm-up-Phase läuft der Standby-Clip ohne Fehleranzeige —
gewolltes Verhalten; dass die Karte „Jetzt läuft" parallel schon die geplante Sendung
zeigt, entspricht dem Überbrückungsgedanken („Gleich geht's los").

## Geprüfte Interaktionen

- Admin: Tab „Einstellungen" öffnen und Standby-Karte finden → unauffällig
- Admin: Aktuellen Standby-Clip am Chip ablesen („Standard-Clip (Gleich geht's los)" /
  „Name (Quelle)") → unauffällig, keine internen Kennungen
- Admin: „Ändern" → Picker öffnen → Befund (Beschriftung bleibt „Ändern",
  Leerzustand-Flash beim Laden)
- Admin: Quelle filtern („Alle Quellen" / Quellenliste) → unauffällig
- Admin: Live-Suche im Picker (400-ms-Debouncing) → unauffällig
- Admin: Kandidatenliste lesen (Name, Quelle, Laufzeit; „keine Laufzeit" bei fehlendem
  Wert) → unauffällig, keine Ids sichtbar
- Admin: „Auswählen" → Befund (wirkt erst über „Speichern", kein
  Ungespeichert-Hinweis)
- Admin: „Standard-Clip verwenden" (Rücksetzen auf Standard) → Befund (gleicher
  Speicher-Vorbehalt)
- Admin: „Weitere laden" bei >100 Treffern → Befund (kein Lade-Feedback)
- Admin: Zuvor gewählter Clip wurde gelöscht → Befund (Fallback-Text suggeriert
  funktionierenden Standard, „Speichern" schlägt aber fehl)
- Zuschauerin: Warm-up-Phase nach Server-Start → Standby-Clip spielt automatisch ohne
  Fehleranzeige → unauffällig
- Zuschauerin: echte Stream-Störung → Warnhinweis mit automatischem Retry → unauffällig
- Zuschauerin: Sendelücke → „Aktuell läuft keine Sendung." + nächste Sendung mit
  Uhrzeit → unauffällig
- Zuschauerin: nicht freigeschaltet/kein Zugriff/nicht angemeldet → bestehende,
  verständliche Hinweise bzw. Login-Formular → unauffällig

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Components/Pages/LiveTv.razor`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor` (Vergleichsmuster Kandidaten-Auswahl)
- `VideoWebPlayer/ViewModels/LiveTvErrorMessages.cs` (sichtbare Meldungstexte)
- `VideoWebPlayer/Services/ProgramSettingsService.cs` (Fehlertext beim Speichern des Standby-Clips)
- `VideoWebPlayer/wwwroot/js/liveTv.js` (unverändert — kein Diff gegenüber `origin/staging`)
