# Abnahmeprüfung – Korrektur Playlist-Detailansicht

Unabhängige Prüfung (Claude, ohne Kontext des Implementierers) der Commits `cf170b1`, `7bf3cb1`, `d243b50`,
`009f1f6`, `f7b28d2` auf dem Branch `task/issue-207-...-korrektur-detailansicht`. Geprüft wurde der tatsächliche
Code und das tatsächliche Aussehen in einem echten Chromium (Playwright gegen eine gehostete App mit SQLite, eigene
Probe-Tests, nach der Prüfung gelöscht), nicht der Selbstbericht.

## Ergebnis

**Status:** Erfüllt (das Informationsleck und die zwei kleinen Restpunkte wurden nach der Prüfung vom Orchestrator behoben, siehe „Behebung der Abweichungen“ am Ende; diese Behebungen wurden nicht mehr von einem separaten Prüf-Agenten gegengeprüft, sind aber durch Tests belegt, die ohne die Behebung fehlschlagen)

Die elf Kundenpunkte D1–D11 sind im Browser erfüllt und sehen sauber aus. Eine Abweichung mittlerer Schwere betrifft die
Rechte (Informationsleck bei gesperrten Titeln), dazu drei kleinere Punkte. Die Abweichung ist am Code belegt und
mit einer kleinen Änderung behebbar.

## Abweichungen

- [ ] **Mittel – Informationsleck: Handlung/Erscheinungsdatum/Episodennummer gesperrter Titel.**
  `PlaylistService.BuildEntryDtosAsync` füllt `ReleaseDate`, `Plot` und `EpisodeNumber` für alle Einträge, unabhängig von
  `IsAccessible`. Ein Betrachter einer fremden öffentlichen Playlist ohne Freischaltung/Quellenzugriff bekommt so die
  Handlung des gesperrten Titels geliefert **und die Oberfläche zeigt sie im Kopfbereich an** (Beleg: Nutzer B ohne
  Zugriff wählt den gesperrten Eintrag: „Handlung von Sichtbarer Film.", Jahr 2018, daneben der Hinweis „nicht
  freigeschaltet"; `GET /api/playlists/{id}/entries/paged` enthält `plot` und `releaseDate`). Dieselben Daten liefert
  `GET /api/items/Movie/{id}` für B nicht (`EnsureAccessAsync` verweigert, Antwort 500 statt der Daten). Damit
  umgeht die Playlist die serverseitige Zugriffsregel „gesperrte Titel ausgegraut, nicht abspielbar". Titel, Poster-Id
  und Elterntitel wurden schon vorher mitgeliefert (das Poster-Endpunkt prüft ebenfalls keinen Quellenzugriff), neu
  ist die Handlung. Behebung: `Plot` (und besser auch `ReleaseDate`, `EpisodeNumber`) bei `!IsAccessible` auf `null`
  lassen, Regressionstest gegen echtes SQLite (Betrachter ohne Zugriff), Dokumentation in `playlists-api.md` ergänzen.
- [ ] **Niedrig – „Anwenden" speichert nicht zwingend das gesehene Bild.** „Anwenden" ruft `regenerate` auf (die
  Collage wird neu berechnet). Für unveränderte Inhalte ist das nachweislich deterministisch (zwei Vorschauen
  byteidentisch, Vorschau == gespeichertes Bild). Ändert sich die Playlist zwischen Vorschau und Anwenden (zweiter
  Tab/Sitzung), wird ein anderes Bild gespeichert als das gesehene (im Versuch 20 387 Byte gesehen, 28 299 Byte
  gespeichert). Das ist in `playlists-api.md` ausdrücklich dokumentiert und in der Praxis selten; trotzdem weicht es
  von „genau das Vorschaubild wird gespeichert" ab. Optional: Vorschau-Bytes serverseitig als „erzeugt" übernehmen.
- [ ] **Niedrig – Text-Datei als `.jpg`:** Das Panel zeigt zunächst ein kaputtes Vorschaubild mit aktivem Button
  „Hochladen"; die verständliche Meldung („Fehler beim Hochladen: Datei ist kein gültiges Bild.") kommt erst nach dem
  Klick. Ohne Speichern, Panel bleibt offen. Zu große Datei, GIF und 6000×6000 px werden verständlich gemeldet.
- [ ] **Niedrig – Extremfälle des Kopfbereichs:** Die Höhe ist wie bei Serien `min-height: clamp(360px, 52vw, 620px)`;
  sie ist bildunabhängig (siehe D6), wächst aber wie dort bei sehr viel Text (255 Zeichen Name plus 2000 Zeichen
  Beschreibung: 1522 px bei 1400 px Breite). Ein 200 Zeichen langer Name ohne Leerzeichen wird bei 400 px am rechten
  Rand abgeschnitten (kein horizontales Scrollen). Gleiches Verhalten wie die Serienseite, daher nur ein Hinweis.

## Beleg je Punkt

- **D1 Erfüllt.** Ohne Auswahl zeigt der Kopfbereich Name, Sortierung, Beschreibung, Genres, Datumszeile
  (`PlaylistDetail.razor` Z. 140–191); Screenshot 1400 px.
- **D2 Erfüllt.** „Erstellt:  20.09.2026, 14:09 Uhr    Aktualisiert:  20.09.2026, 14:09 Uhr" mit Beschriftung,
  Doppelpunkt, Abstand 1,75 rem, auch bei 400 px als zwei Zeilen lesbar.
- **D3 Erfüllt.** Ein Bild-Symbol-Button öffnet `PlaylistCoverPanel` (Rahmen 1 px `rgb(66,71,84)`, Schatten). Datei →
  Vorschau, Button „Hochladen"; „Aus den Titeln erzeugen" → Vorschau, Button „Anwenden"; Vorschau speichert nichts
  (DB und Bildanzahl vor/nach identisch, nach Neuladen kein Bild); Playlist ohne Bildtitel: „Keine Bilder verfügbar.
  Fügen Sie Titel mit Bild hinzu …", Button gesperrt; bei hochgeladenem Bild Warnung „wird ersetzt und kann nicht
  wiederhergestellt werden" vor dem Anwenden; Entfernen: hochgeladen mit Rückfrage (Abbrechen ändert nichts),
  erzeugt ohne; Abbrechen ändert nichts; bei 400 px ohne Überlauf.
- **D4 Erfüllt.** Rahmen (1 px, Schatten, Hintergrund) im Browser gemessen und gesehen bei Bearbeiten, Löschen,
  Genre-Editor, Bild-Panel, Sortiermodus-Bestätigung, Weiterschauen-Bestätigung, Bild-Entfernen-Bestätigung. Mit dem
  CSS des Basisbranchs war der Rahmen `0px none`, Hintergrund transparent (Ursache bestätigt).
- **D5 Erfüllt.** Bild als `<img>` unter dem `.tvshow-header-overlay`; Verlaufswerte des Overlays identisch zur
  Serienseite (gleiche Klasse, `getComputedStyle` gleich), auch bei rein weißem Bild bleiben Titel, Beschreibung und
  Buttons lesbar.
- **D6 Erfüllt.** Über echten Upload per Panel (hoch 400×3000, breit 4000×300, klein 16×16, ohne Bild): Kopfhöhe
  621 / 469 / 361 px bei 1400 / 900 / 400 px, jeweils identisch zur Serienseite und in allen Bildfällen gleich.
  Extremtexte siehe Abweichungen (Hinweis).
- **D7 Erfüllt.** Aktionsleiste `position: absolute`, 20 px von oben, 24 px vom rechten Rand, innerhalb des Kopfbereichs.
- **D8 Erfüllt.** Kacheln ohne Buttons (0 in der Liste); Klick wählt, Kopfbereich zeigt Poster, Titel, Jahr, Art,
  Zugehörigkeit, Handlung, Hinzugefügt-Datum; Abspielen im Kopf startet Wiedergabe (`?entryId=`, Player); Löschen-Symbol
  nur beim Besitzer entfernt den Titel (inkl. Weiterschauen-Sicherheitsabfrage, Abbrechen/Bestätigen geprüft);
  gesperrter Titel wählbar, ohne Abspielen-Button mit Hinweis; Doppelklick spielt ab (gesperrt: nichts);
  Zurück-Pfeil, Escape, Enter, Leertaste funktionieren; Deep-Link `?entryId=` startet Wiedergabe ohne Auswahl;
  Infinite-Scroll mit 45 Einträgen korrekt (20 → 45, 45 unterschiedliche IDs).
- **D9 Erfüllt.** Leere Playlist öffnet im Hinzufügen-Modus, nicht leere in der Liste; Hinzufügen bleibt im Modus
  (zwei Titel nacheinander); letzter Titel entfernt → Hinzufügen; Wechsel zum Hinzufügen hebt die Auswahl auf;
  Betrachter sieht weder Umschalter noch Hinzufügen-Bereich.
- **D10 Erfüllt.** Privat: Schloss, `aria-pressed=false`, Name „Privat – nur für Sie sichtbar …"; öffentlich: Globus auf
  Akzentgrund, `aria-pressed=true`, Name „Öffentlich – für alle Anwender sichtbar …"; nur Admin-Besitzer sieht ihn.
- **D11 Erfüllt.** Gleiche Klassen; gemessen gleich: `min-height` 620 px, h1 51,2 px/800, Meta 16 px, Plot 16 px, Farben
  gleich. Abweichung nur beabsichtigt (Zurück-Pfeil und Aktionsleiste rechts).

## Rechte und Informationsleck (Punkt 5)

- `POST /api/playlists/{id}/cover/preview`: ohne Anmeldung 401, Nutzer B (kein Besitzer, auch als Administrator) auf
  private UND öffentliche Playlist 403, Besitzer 200; Datenbank (Cover-Id, Bildanzahl) nach allen Angriffen
  unverändert. Der Besitzer wird vor dem Erzeugen geprüft, es werden keine Bytes fremder Playlists geliefert.
- Mutationstest: Besitzerprüfung in `PreviewPlaylistCoverAsync` entfernt → 8 Tests schlagen fehl (Service-Matrix
  `PreviewCover`, Controller-Matrix `PreviewPlaylistCover` für Fremdnutzer/Admin/privat/öffentlich, Vorschau-Tests);
  danach `git checkout`. Die Schritt-11-Matrizen decken den neuen Endpunkt also ab.
- Leck (b): siehe Abweichung „Mittel". Leistung (c): `LoadEntryDetailsAsync` lädt je Medientyp konstant viele
  Abfragen (Datum, Handlung, bei Episoden Reihenfolge), kein N+1.
- Lesemodus (Punkt 6, zweiter Nutzer, Nicht-Admin und Admin ohne Besitz): Bild-Button, Panel, Bearbeiten, Löschen,
  Sortier-Umschalter, Genres bearbeiten/zurücksetzen, Modus-Umschalter, Hinzufügen-Bereich, Veröffentlichen,
  Titel-Löschen (auch bei gewähltem Titel), Verschieben-Buttons und `draggable` je 0; Abspielen funktioniert
  (mit Quellenzugriff); Zurück führt zu `/playlists/public`; private Playlist: „Zugriff … verweigert".

## Nebenwirkungen des Dialog-Rahmens (Punkt 8)

Einziger weiterer Nutzer von `.admin-dialog-overlay` ist der Bearbeiten-Dialog der Genre-Verwaltung. Vorher/Nachher
(App-CSS des Basisbranchs per Playwright-Route gegen das neue CSS, dieselbe Seite): `/admin`, `/admin/users`,
`/admin/backups`, `/admin/security`, `/admin/updates` pixelidentisch; `/admin/genres`, `/admin/mediasources`,
`/admin/program-settings` unterscheiden sich in den Screenshot-Hashes, sehen aber gleich aus (Program-Settings
nebeneinander gesehen; vermutlich Rendering-Rauschen), der Genre-Dialog hat nur den neuen feinen Ring/Schatten.
Hell/Dunkel: die App hat nur ein dunkles Design.

## Tests und Dokumentation (Punkte 10–12)

- Umlaut-Mutation: `Oeffentlich` in `PlaylistCoverPanel.razor` eingefügt → `PlaylistUiUmlautTests` schlägt fehl
  (2 Tests), danach zurückgesetzt.
- Bestandstests per Diff: entfernte Assertions betreffen nur entfernte UI (alte Bild-/Play-/Entfernen-Buttons und
  Dialoge) und wurden durch gleichwertige neue ersetzt; die Betrachter-Rechtetests wurden erweitert, nicht
  abgeschwächt.
- Volle Suite `dotnet test`: 1140 Tests, 1138 bestanden, 2 Fehler beim ersten Lauf
  (`MediaSearchSelectorTests.Search_HttpRequestFails_...` bUnit-Timeout,
  `PlaylistEntriesE2ETests.AddTVShow_CascadesSeasonsAndEpisodes` 5-s-Timeout). Beide Klassen zusammen 3× einzeln
  wiederholt: 12/12 grün, 3 von 3 Läufen. Bewertung: flackernd unter Last, kein echter Fehler. Release- und
  Debug-Build fehlerfrei.
- Doku und `docs/RELEASE_NOTES.md` beschreiben das gebaute Verhalten inkl. Vorschau-/Anwenden-Weg, Modus und
  Rechte; die Doku erwähnt das Leck der Handlung gesperrter Titel nicht.

## Hinweise

- Screenshots wurden im Browser angesehen (Kopfbereich in allen Bildfällen, Panel, Auswahl, gesperrt, Dialoge,
  400 px, helles Bild). Aufgefallen: der native Datei-Knopf „Choose File / No file chosen" ist englisch (Browser-
  Sprache, wie im Genre-Dialog); Genre-Editor ohne Genres ist leer ohne Hinweistext.
- Alle Probe-Tests und Screenshots wurden gelöscht; nur dieser Bericht ist committet. `docs/features/task/` und
  `.claude/` unangetastet.

## Behebung der Abweichungen

1. **Informationsleck (mittel) – Handlung, Erscheinungsdatum und Episodennummer gesperrter Titel:** `PlaylistService.BuildEntryDtosAsync` füllt `ReleaseDate`, `Plot` und `EpisodeNumber` jetzt nur noch, wenn der ANFRAGENDE den Titel freigeschaltet hat (`IsAccessible`). Ein gesperrter Titel liefert weiter Titel, Zugehörigkeit und Bild (ausgegraut, nicht abspielbar). Das gilt auch für den Besitzer, für den dieselbe Regel wie auf den Film- und Serienseiten gilt. Neue Tests in `PlaylistServiceTests_EntryDetails`: gesperrter Film, gesperrte Episode und der Betrachter einer öffentlichen Playlist (Freischaltung des Besitzers wirkt nicht für den Betrachter; mit eigener Freischaltung erhält der Betrachter die Angaben). **Alle drei schlagen gegen den Stand ohne die Behebung fehl und bestehen mit ihr.** Zwei bestehende Tests der freigeschalteten Fälle hatten bisher keine Freischaltung angelegt und waren nur wegen des Lecks grün; sie legen die Freischaltung jetzt an. Doku (`playlists-api.md`) nennt die Regel.
2. **Kaputtes Vorschaubild bei einer Nicht-Bilddatei:** Die Vorschau im Bild-Panel blendet ein nicht darstellbares Bild jetzt aus (`onerror`), statt ein defektes Bild zu zeigen. Die verständliche Meldung kommt weiterhin von der Server-Prüfung beim Hochladen (unverändert).
3. **Langer Name ohne Leerzeichen bei 400 px:** `overflow-wrap: anywhere` für Überschrift und Handlung im Playlist-Kopfbereich. Nicht erneut im Browser nachgestellt.

Bewusst unverändert: „Anwenden“ ruft `regenerate` auf; ändert sich der Playlist-Inhalt zwischen Vorschau und Anwenden, wird ein anderes Bild gespeichert als das gesehene (in der API-Doku beschrieben, Abnahme: geringe Abweichung). Das native englische „Choose File“ des Datei-Feldes und der leere Genre-Editor ohne Hinweis bleiben.

Testlauf danach: 1143 Tests, 1142 grün. Der eine Ausfall (`MediaSearchSelectorTests.SearchTermInput_DebounceWorks_RespectsDelay`, zeitabhängiger bUnit-Test) bestand danach dreimal einzeln (6 von 6); derselbe Testtyp flackerte schon im Prüflauf des Reviewers. `dotnet build VideoPlayer.sln` in Debug und Release: 0 Fehler.
