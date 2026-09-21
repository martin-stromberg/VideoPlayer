# Abnahme: Korrektur Kundenrückmeldung 3 (Fingerabdruck für CSS/JS, Platzhalter ohne Symbol, Episodenbild im Kopfbereich)

**Geprüfter Stand:** `0e6ef59` auf Branch
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-kundenrueckmeldung-3`
**Basis:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln` (`8f5bf28`)
**Prüfer:** Claude (Softwareschmiede Bot), unabhängig vom Implementierer
**Datum:** 21.09.2026

Alle Belege (Screenshots, Messprotokolle) liegen außerhalb des Repositories unter
`C:\Users\Martin\AppData\Local\Temp\claude\D--Repositories-softwareschmiede-fd729906-8801-43ee-8539-fa3e046c88f4\e838bc0e-97f1-46db-9b8f-7f49b0cca26b\scratchpad\`
(im Folgenden `<SCRATCH>`); die Bilder im Unterordner `shots\`, die Messprotokolle als `probe-log-*.txt`.
Geprüft wurde mit einer frisch gestarteten App (eigene SQLite-Datenbank je Lauf), echten Testdaten
(Playlist mit/ohne Bild, Episoden + Film, langer Titel, öffentliche Playlist eines anderen Benutzers mit
gesperrter Episode) und einem echten Chromium über Playwright in drei Größen: 1440x900,
2150x1345 (Nachbildung 67 % Zoom) und 390x844 (Mobil).

---

## Ergebnis

**Status:** Erfüllt mit Abweichungen

Alle offenen `[ ]`-Punkte der Kundendatei sind im gerenderten Ist-Stand erfüllt — ich habe jeden einzeln
im Browser gesehen und vermessen. Die Hypothese zum gecachten alten `app.css` ist belastbar bestätigt
(Abschnitt 1). Unabhängig davon bleiben Abweichungen, die nichts mit den Kundenpunkten zu tun haben,
aber behoben werden sollten — insbesondere **M1** (serverseitig fehlende Berechtigungsprüfung beim
Episoden-Hintergrundbild, vorbestehend, durch diese Änderung aber neu in Gebrauch).

---

## 1. Hypothese „alter, gecachter `app.css`" — bestätigt

### 1.1 Aufbau des Versuchs

Ein echter Chromium mit **persistentem Profil** (also echtem Platten-Cache) wurde in zwei Phasen benutzt,
beide Male auf derselben Herkunft `http://127.0.0.1:5099`:

1. **Cache füllen wie früher:** Ein kleiner eigener HTTP-Server lieferte unter `/app.css` ein **historisches**
   `app.css` aus (`git show 4120cf1:VideoWebPlayer/wwwroot/app.css` bzw. in einem zweiten Lauf
   `1a8620a`), mit `Last-Modified` (120 Tage alt) und **ohne** `Cache-Control` — genau so, wie die
   Anwendung die Datei vor dieser Iteration ausgeliefert hat. Eine Seite verlinkte sie als
   `<link rel="stylesheet" href="app.css">`.
2. **Echte App (HEAD)** auf demselben Port, **dasselbe Browserprofil**, Anmeldung, Aufruf von
   `/playlists` und `/playlists/{id}`.

### 1.2 Belege

| Beobachtung | Beleg |
| --- | --- |
| Antwort ohne `Cache-Control`, mit `Last-Modified` wird heuristisch gecacht | zweiter Besuch der Stub-Seite: **kein** weiterer CSS-Request am Server (`CSS-Requests am Stub = 1`) — `<SCRATCH>\probe-log-cache-4120cf1.txt` |
| Ohne Fingerabdruck greift der **alte** Cache-Eintrag, obwohl der neue Server `Cache-Control: no-cache` schickt | Link im geladenen Dokument auf `app.css` (ohne `?v=`) umgestellt → `transferSize=0` (aus dem Cache, **ohne Netzwerkanfrage**), alte Regel `.playlist-cover-icon` **vorhanden**, neue Regel `.playlist-tile-badge` **fehlt** |
| Das sieht kaputt aus und deckt sich mit den Kundenbildern | `shots\cache-ohne-fingerabdruck-detail.png`: Kopfbereich = Bildhöhe, Bild im Textfluss, Playlist-Angaben aus dem Bild geschoben — dieselbe Störung wie in `TestResults\screenshots\playlist-details-mit-bild.png` und `playlist-details-ohne-bild.png`. `shots\cache-alt-1a8620a-uebersicht.png`: Kennzeichen-Symbol unformatiert links oben über die volle Kachelbreite (gemessen `x=26 y=194 w=257 h=16`) — dasselbe Bild wie `TestResults\screenshots\playlist-overview.png`; Filterleiste 136x40 statt 158x44, Kacheltitel wieder gekürzt („Bildergalerie der allerbesten…"). |
| Mit Fingerabdruck wird die neue Datei geladen und alles sieht richtig aus | `shots\cache-mit-fingerabdruck-uebersicht.png` / `-detail.png`, alte Regel `.playlist-cover-icon` **fehlt**, neue Regel vorhanden, Filterleiste 158x44, Kennzeichen 30x30 rechts oben |

**Wichtig:** Der Versuch zeigt zugleich, dass das im Basisbranch ergänzte `Cache-Control: no-cache`
**allein nicht genügt** hätte: Ein Browser, der die Datei vorher ohne `Cache-Control` bekommen hat, hält
den alten Eintrag weiter für frisch und fragt gar nicht erst nach. Erst der geänderte URL-Schlüssel
(`?v=…`) erzwingt das Nachladen. Die Änderung ist also die richtige.

### 1.3 Fingerabdruck, Verlinkung, Produktion

- Fingerabdruck = erste 6 Bytes des SHA-256 als Hex (12 Zeichen). Nachgerechnet: `app.css` →
  `351b426aeff1`, `VideoWebPlayer.styles.css` → `3ee63ee92c08` — identisch zu dem, was die Seite
  ausliefert. Ändert sich die Datei, ändert sich der Wert (eigene Gegenprobe siehe 4.3).
- **Kein 404, kein Doppelt-Laden:** Ressourcenzählung der geladenen Seite ergibt jede Datei genau einmal:
  `{"bootstrap.min.css":1,"app.css?v=351b426aeff1":1,"VideoWebPlayer.styles.css?v=3ee63ee92c08":1,
  "scroll.js?v=…":1, … ,"blazor.web.js":1}`. Direktabruf von `/app.css`, `/VideoWebPlayer.styles.css`,
  `/js/scroll.js`, `/js/playlistDragDrop.js`, `/bootstrap/bootstrap.min.css` → alle `200`, `cache-control: no-cache`.
- **Scoped-CSS-Bundle in Development/Testing:** `VideoWebPlayer.styles.css` kommt dort über die
  Static-Web-Assets; der `WebRootFileProvider` findet es (Fingerabdruck wurde gebildet, Abruf `200`).
- **Publish-/Produktionsfall selbst geprüft:** `dotnet publish -c Release` erzeugt beide Dateien physisch in
  `publish\wwwroot\` (`app.css` 70 613 Bytes, `VideoWebPlayer.styles.css` 14 011 Bytes), die SHA-256-Präfixe
  stimmen mit den Fingerabdrücken überein. Die **veröffentlichte** Anwendung wurde gestartet
  (`ASPNETCORE_ENVIRONMENT=Production`) und liefert im Markup
  `app.css?v=351b426aeff1`, `VideoWebPlayer.styles.css?v=3ee63ee92c08` sowie alle sechs Skripte mit
  Fingerabdruck; alle Abrufe `200` mit `Cache-Control: no-cache`.

### 1.4 Einschränkung der Hypothese (ehrlich)

Der Screenshot `TestResults\screenshots\Bildauswahl-Overlay-Panel.png` (weißes Panel, **blauer**
„Hochladen"-Button, Bootstrap-Standardaussehen) lässt sich mit *keiner* der beiden getesteten historischen
`app.css`-Fassungen nachstellen: die Regeln `.admin-dialog` (Rahmen, dunkler Grund) und `.btn-primary`
(rot) sind in alt und neu **byteidentisch**. Ein blauer Primary-Button entsteht nur, wenn `app.css`
**überhaupt nicht** wirkt (nur Bootstrap). Plausibelste Erklärungen: eine noch ältere gecachte Fassung, ein
Lauf ohne geladenes `app.css` oder ein Bild aus einer früheren Runde. Das ändert nichts am Befund für
Übersicht und Detailansicht, sollte aber beim Kunden abgeklärt werden (siehe Empfehlung E3).

---

## 2. Status je offenem Kundenpunkt

Alle Angaben aus dem eigenen Browserlauf, Messprotokoll `<SCRATCH>\shots\probe-log.txt`.

### Übersicht

**Z. 28 – Filter- und Plus-Button: eine Ebene, gleicher Stil, Filterleiste als ein Panel mit drei
Schaltbereichen und senkrechten Trennlinien → ERFÜLLT**

- 1440x900: Filtergruppe `x=1194 y=110 w=158 h=44`; die drei Schaltbereiche `52x42` bei `y=111`, lückenlos
  aneinander (x = 1195 / 1247 / 1299); Plus-Button `52x42` bei `y=111`. Also gleiche Höhe, gleiche Ebene.
- 2150x1345 und 390x844: ebenfalls alle vier auf einer Ebene, kein horizontaler Überlauf
  (`scrollWidth == clientWidth` in allen drei Größen).
- Optisch ein Panel mit rot gefülltem aktivem Bereich und senkrechten Trennlinien:
  `shots\uebersicht-1440x900.png`, `shots\uebersicht-filter-all|own|public.png`, `shots\uebersicht-mobil-390x844.png`.

**Z. 31 – kein Listensymbol, Kachel wie Film-/Serienkacheln, Bild füllt die Fläche → ERFÜLLT**

- `document.querySelectorAll('.playlist-cover-placeholder svg').length == 0` in allen drei Größen.
- Kachel `330x185`, Coverbild `328x183` (1 px Rahmen), `object-fit: cover` — das Bild füllt die Kachel.
- Playlist ohne Bild: reiner Farbverlauf (`shots\uebersicht-1440x900.png`, Kachel „Ohne Bild").

**Z. 33 – Öffentlich-Symbol oben rechts → ERFÜLLT** (Deutung siehe Abschnitt 3)

- Eigene öffentliche Playlist: Kennzeichen `x=316 y=202 w=30 h=30` bei Kachel `x=25…355, y=192…377`
  → rechte obere Ecke. Fremde Playlist: dieselbe Stelle (`x=1377 y=202`).
- In allen drei Filterzuständen korrekt: `Alles` zeigt eigene Playlists zuerst und die fremde zuletzt
  (`Raumschiff Enterprise/false | Ohne Bild/false | Bildergalerie…/false | Fremde Sammlung/true`),
  keine Doppelung der eigenen öffentlichen Playlist, Klassen `playlist-public-badge` bzw.
  `playlist-foreign-badge`.

### Detailansicht

**Z. 43 – Overlay-Panel mit Rahmen → ERFÜLLT**
`#playlist-cover-panel`: `border: 1px solid rgb(66, 71, 84)`, Grund `rgb(23, 31, 51)` — deutlich vom
Seiteninhalt abgesetzt (`shots\cover-panel.png`).

**Z. 45 – Vorschau auf Panelbreite begrenzt, keine Scrollleisten → ERFÜLLT**
Panel `640x531`, Vorschaubild `462x260` (CSS `max-width: 100%; max-height: 260px; object-fit: contain`).
`scrollWidth/clientWidth = 638/638`, `scrollHeight/clientHeight = 529/529` — kein Balken. Seite:
`1440/1440`. Mobil (390x844): Panel `356/356`, Seite `390/390` (`shots\cover-panel-mobil.png`).
Die Buttons entsprechen dem Rest der App: `#playlist-cover-apply-button` („Hochladen"/„Anwenden")
`btn btn-primary` mit `rgb(229, 9, 20)` (rot), daneben `btn-outline-light`, `btn-outline-danger`, `btn-ghost`.

**Z. 47 – Bearbeiten-Panel und Löschbestätigung mit Rahmen → ERFÜLLT**
Beide nutzen `admin-dialog-overlay` / `admin-dialog` mit demselben 1-px-Rahmen:
`shots\bearbeiten-panel.png`, `shots\loeschbestaetigung.png`.

**Z. 49 – Bild dezenter, Informationen über dem Bild → ERFÜLLT**
`filter: saturate(1.04) contrast(1.04) brightness(0.88)` auf dem Kopfbild (gleiche Dämpfung wie die
Film-/Serienkacheln); Bild `z-index: 0`, Textebene `.tvshow-header-overlay` `z-index: 1` mit Verlauf.
Name, Sortierhinweis, Beschreibung und Datumsangaben stehen lesbar auf dem Bild
(`shots\detail-mit-bild-1440x900.png`).

**Z. 51 – feste Kopfhöhe, unabhängig vom Bild → ERFÜLLT**
Kopfbereich **620 px** mit Bild *und* ohne Bild, bei 1440x900 und bei 2150x1345; 440 px bei 390x844
(`clamp(440px, 52vw, 620px)`). Zum Vergleich der Serien-Kopfbereich: 621 px
(`shots\referenz-serie-detail.png`). Das Bild liegt absolut mit `object-fit: cover`, zieht die Höhe also
nicht mehr.

**Z. 55 – keine Buttons in der Titelliste, Auswahl zeigt Bild + Infos im Kopfbereich → ERFÜLLT**
`document.querySelectorAll('.playlist-entry-row button').length == 0`.
Episode ausgewählt: `#playlist-detail-entry-background` füllt den Kopfbereich (`1439x619`, geladen mit
`naturalWidth 1280`), kein kleines Poster mehr; Titel, Jahr, „Episode 1", Handlung und Hinzugefügt-Datum
darüber (`shots\episode-ausgewaehlt-1440.png`, `-zoom67.png`, `-mobil.png`) — dasselbe Muster wie
`shots\referenz-serie-detail.png`.
Film ausgewählt: Poster `130x194` links neben Titel/Jahr/„Film"/Handlung, kein Episodenhintergrund
(`shots\film-ausgewaehlt-1440.png`).

**Z. 56 – Entfernen als Symbol-Button im Kopfbereich auf dem Bild, kein großer freier Bereich oben
→ ERFÜLLT (im Muster der Serienseite)**
Bei Auswahl: Papierkorb `x=1372 y=84 w=44 h=44` und Moduswechsel `+` in der Aktionsleiste auf dem Bild
(Kopfbereich beginnt bei `y=64`), Abspielen-Button `72x72` ebenfalls auf dem Bild. Der frühere große freie
Bereich am oberen Rand war eine Folge des alten Stylesheets (vgl. 1.2); jetzt ist der Kopfbereich genauso
aufgebaut wie auf der Serienseite: feste Höhe, Inhalt unten ausgerichtet, Aktionsbuttons oben rechts auf
dem Bild.

### Moduswechsel (bereits abgehakt, mitgeprüft)

Im Kopfbereich wird immer nur **ein** Umschalt-Button gezeigt (`playlist-mode-add-button` bzw.
`playlist-mode-entries-button`), leere Playlist startet im Hinzufügen-Modus
(`shots\detail-leer-langer-titel.png`, `shots\modus-hinzufuegen.png`).

### Gesamteindruck

Gemessen am Maßstab des Kunden („sieht aus wie gewollt aber nicht gekonnt") ist der Ist-Stand ein
deutlicher Sprung: Übersicht und Detailansicht sind vom Aufbau her nicht mehr von der Serien-/Film-Welt
der App zu unterscheiden (gleiche Kachelform, gleicher Kopfbereich, gleiche Aktionsleiste, gleiche
Dialoge). Die verbleibenden Schönheitsfehler sind klein (M5/M6) und nicht von der Art, die der Kunde
bemängelt hat.

---

## 3. „Symbol öffentlich … am oberen rechten Rand. Gemeint ist damit das andere rechts."

**Ist-Stand (selbst gemessen und gesehen):**

- **Eigene öffentliche Playlist:** Globus-Symbol in einem runden dunklen Feld, **rechts oben** in der Kachel
  (`.playlist-tile-badge.playlist-public-badge`, `x=316 y=202` bei Kachel `x=25…355 / y=192…377`).
- **Fremde (freigegebene) Playlist:** Personen-Symbol, **an genau derselben Stelle**
  (`.playlist-tile-badge.playlist-foreign-badge`).
- Beide schließen einander aus; eine fremde Playlist bekommt nie zusätzlich das Öffentlich-Symbol.
- Bei aktivem Filter „Alles" stehen die eigenen Playlists zuerst, die fremden danach; die Symbole bleiben
  unverändert rechts oben. Im Filter „Öffentliche" erscheinen die eigene öffentliche (Globus) und die fremde
  (Personen) nebeneinander — die Unterscheidung ist also allein am Symbol ablesbar, nicht an der Position.
- **Links oben** steht in keinem Zustand etwas.

**Plausibelste Deutung:** Der Satz ist ironisch gemeint — „das andere rechts" ist die gängige Redewendung
für „du hast links genommen, ich meinte rechts". Auf dem Kundenbild
(`TestResults\screenshots\playlist-overview.png`) steht der Globus tatsächlich **links oben**; genau so
rendert die neue Kachel mit dem alten, gecachten `app.css` (nachgestellt in
`shots\cache-alt-1a8620a-uebersicht.png`, unformatiertes `<span>` im Textfluss oben links). Der Kunde hat
also das Symbol links gesehen und auf die rechte Ecke bestanden. Diese Forderung ist im Ist-Stand erfüllt;
es ist **keine** Code-Änderung nötig, sondern nur ein Nachladen ohne alten Cache. Eine zweite, deutlich
unwahrscheinlichere Lesart („das *andere* Symbol soll nach rechts", also das Fremd-Symbol) wäre ebenfalls
schon erfüllt, weil beide Symbole an derselben rechten oberen Stelle sitzen.

**Empfehlung:** Kein Code ändern; beim Kunden mit einem harten Neuladen (Strg+F5) bestätigen lassen.

---

## 4. Weitere Prüfungen

### 4.1 Zugriffskontrolle beim Episoden-Hintergrund

**Oberfläche: in Ordnung.** In der öffentlichen Playlist eines anderen Benutzers mit einer für den
Betrachter gesperrten Episode wird **kein** `#playlist-detail-entry-background` gerendert, die Seite enthält
den Handlungstext der gesperrten Episode nicht (`document.documentElement.innerHTML.includes('GEHEIME-HANDLUNG…') === false`),
kein Erscheinungsjahr, dafür den Hinweis „Dieser Titel ist für Sie nicht freigeschaltet …"
(`shots\fremde-playlist-gesperrte-episode.png`). Der Kopftext lautet vollständig:
`Geheime Episode / Episode / Dieser Titel ist für Sie nicht freigeschaltet und kann nicht abgespielt werden. / Hinzugefügt: …`

**Endpunkt: nicht abgesichert — siehe M1.** Mit dem Zugriffstoken des Betrachters direkt abgefragt:

| Aufruf | Ergebnis |
| --- | --- |
| `GET /api/episodes/{gesperrt}/background-image?access_token={Token A}` | **200**, 635 Bytes = **exakt das Bild der gesperrten Episode** |
| `GET /api/episodes/{erlaubt}/background-image?access_token={Token A}` | 200, 5367 Bytes (zur Unterscheidung bewusst andere Bildgröße gesät) |
| ohne Token | 401 |
| mit falschem Token | 401 |

Zum Vergleich: die Serienseite derselben gesperrten Serie verweigert korrekt
(„Die Serie konnte nicht geladen werden.", `shots\fremde-serie-direktaufruf.png`). Die Lücke liegt allein
im Controller.

### 4.2 Code-Review des Diffs (`git diff <Basis>...HEAD`)

Geänderter Produktivcode: `Components/App.razor`, `Services/StaticAssetVersioner.cs` (neu),
`Extensions/ServiceCollectionExtensions.cs`, `Components/Playlists/PlaylistCoverPlaceholder.razor`,
`Components/Playlists/PlaylistDetail.razor`, `wwwroot/app.css` (14 Zeilen entfernt).

Gut gelöst:

- `StaticAssetVersioner` ist klein, testbar (zweiter Konstruktor mit `IFileProvider`), fällt bei fehlender
  Datei auf den unveränderten Pfad zurück und wirft nicht. Der Query-String stört die
  `StaticFileMiddleware` nicht (ETag/Last-Modified bleiben erhalten).
- Der Episodenhintergrund wird über die bereits vorhandene `TVShowDetails.BuildEpisodeBackgroundImageUrl`
  gebildet, also ohne zweite URL-Definition.
- Die Auswahl-Logik ist auf Episoden **und** `IsAccessible` eingeschränkt; Filme behalten ihr Poster.
- Dokumentation (`docs/help/playlists.md`) und Release Notes sind mitgezogen.

Anmerkungen: siehe M2–M4.

### 4.3 Gegenproben (Tests wirklich wirksam?)

Ich habe die abgesicherten Stellen jeweils kurz kaputtgemacht und geprüft, dass die Tests fehlschlagen
(danach sofort zurückgesetzt; `git status` anschließend sauber, Arbeitsverzeichnis unverändert):

| Mutation | Erwartung | Ergebnis |
| --- | --- | --- |
| `StaticAssetVersioner.Url` gibt immer `path` zurück | Fingerabdruck-Tests rot | `Url_AppendsAFingerprintOfTheFileContent`, `Url_IsStableForTheSameContent_AndChangesWhenTheContentChanges` und der E2E-Test `StylesheetAndScriptLinks_CarryAContentFingerprint` schlagen fehl (3 von 4) |
| `SelectedEntryBackgroundUrl`: Bedingung `IsAccessible: true` entfernt | Sperr-Test rot | `SelectingALockedEpisode_LoadsNoBackgroundImage` schlägt fehl (1 von 25 im Testtyp) |

### 4.4 Build und Tests (selbst ausgeführt)

- `dotnet build VideoPlayer.sln -c Release` → **0 Fehler** (245 Warnungen, alle vorbestehend: xUnit-Analyzer).
- `dotnet build VideoPlayer.sln -c Debug` nach Rücksetzen der Mutationen → 0 Fehler.
- `dotnet test VideoWebPlayer.Tests` (voll) → **1229 erfolgreich, 2 Fehler, 1231 gesamt** (2 min 56 s).
  Fehlgeschlagen unter Last:
  - `MediaSourceSwitchE2ETests.User_Can_Switch_Source_From_Menu_And_Sees_Only_Selected_Source_Titles`
  - `PlaylistEntriesE2ETests.AddTVShow_CascadesSeasonsAndEpisodes`
  Beide **dreimal einzeln wiederholt: jeweils grün (3/3)**. Einstufung: bekanntes Flackern der
  Playwright-Tests unter Parallellast, kein Sachfehler. Kein Test wurde abgeschwächt oder deaktiviert.

---

## Abweichungen

- [ ] **M1 (Sicherheit, mittel, vorbestehend — aber jetzt in Gebrauch):**
      `GET /api/episodes/{id}/background-image` prüft in `VideoWebPlayer/Controllers/EpisodesController.cs`
      nur `CheckLogedIn()` und **keine** Quellenfreigabe/Freischaltung. Jeder angemeldete Benutzer kann das
      Hintergrundbild jeder Episode abrufen.
      *Reproduktion:* Benutzer B legt eine öffentliche Playlist mit einer Episode auf einer Medienquelle an,
      die Benutzer A nicht freigeschaltet ist. A meldet sich an und ruft
      `/api/episodes/{id}/background-image?access_token={Token von A}` auf → **200** mit genau den Bilddaten
      dieser Episode (nachgewiesen über eine bewusst abweichende Bildgröße: 635 Bytes gesperrt gegen
      5367 Bytes erlaubt). Ohne Token bzw. mit falschem Token: 401.
      *Bewertung:* Die Oberfläche lädt das Bild für gesperrte Titel korrekt nicht — das ist aber genau die
      „ausgeblendete Schaltfläche" aus AGENTS.md §7. Da dieser Branch die Playlist-Detailseite als zweiten
      Verbraucher des Endpunkts ergänzt, gehört die Prüfung serverseitig nachgezogen (analog zu
      `PlaylistEntryAccessResolver` / `IUnlockedMediaService.IsAccessible`), mit Regressionstest
      (403/404 für eine nicht freigeschaltete Episode).

- [ ] **M2 (klein):** Der Fingerabdruck wird je Prozess **einmal** berechnet und unbegrenzt zwischengespeichert
      (`ConcurrentDictionary` im Singleton, kein `IFileProvider.Watch`). Wird `app.css` im laufenden Betrieb
      ersetzt (In-Place-Update ohne Neustart), bleibt der alte `?v=`-Wert stehen. Praktisch entschärft durch
      `Cache-Control: no-cache` (der Browser revalidiert dann doch), und der Auto-Updater startet die
      Anwendung neu — sollte aber im Kommentar der Klasse ausdrücklich stehen.

- [ ] **M3 (klein):** Die neue CSS-Klasse `playlist-header-entry-background` hat in `app.css` **keine Regel**;
      sie wirkt nur, weil das Element zusätzlich `playlist-header-cover playlist-header-cover-image` trägt.
      Die gewünschte Schichtung (Episodenbild über dem Playlist-Cover) entsteht allein aus der
      DOM-Reihenfolge bei gleichem `z-index: 0`. Entweder eine echte Regel ergänzen (z. B. `z-index: 1`)
      oder die Klasse entfernen, sonst ist die Absicht im CSS nicht nachvollziehbar.

- [ ] **M4 (klein, Regression):** Schlägt das Laden des Episodenbilds fehl, blendet `onerror` das `<img>` aus —
      ein Poster als Ersatz wird **nicht** gerendert, weil `#playlist-detail-entry-poster` nur bei
      `SelectedEntryBackgroundUrl is null` im Markup steht. Der Kopfbereich zeigt dann nur noch das
      Playlist-Cover, die Episode gar kein Bild. Vorher gab es immer das Poster.
      *Reproduktion:* Episode ohne Hintergrund-, Banner- und Fanart-Bild auswählen, deren Endpunkt einen
      Fehler liefert (z. B. Endpunkt blockieren).

- [ ] **M5 (kosmetisch):** Im Kopfbereich der Detailansicht wird ein sehr langer Playlist-Name nach zwei
      Zeilen mit „…" gekürzt (`shots\detail-leer-langer-titel.png`: „Bildergalerie der allerbesten
      Donnerstagabendunterhal…"). Der Kunde hat das Kürzen des Titels auf der **Kachel** ausdrücklich
      bemängelt; es ist gut möglich, dass er es hier ebenfalls beanstandet. Bewusste Entscheidung laut
      Release Notes — trotzdem vorab ansprechen.

- [ ] **M6 (kosmetisch):** Hat eine Playlist keine Genres, steht in der Genre-Zeile nur der
      Bearbeiten-Stift allein und ohne Bezug unter der Beschreibung
      (`shots\detail-ohne-bild-1440.png`, `shots\detail-mit-bild-1440x900.png`). Wirkt unfertig.

---

## Hinweise und Empfehlungen

- **E1:** M1 vor dem Merge beheben; die übrigen Punkte sind aus meiner Sicht nachrangig.
- **E2:** Der Fingerabdruck deckt `app.css`, `VideoWebPlayer.styles.css` und die sechs Skripte ab — **nicht**
  `bootstrap/bootstrap.min.css` (unverändert ohne `?v=`, aber ebenfalls mit `no-cache` ausgeliefert) und
  nicht die per JavaScript nachgeladenen Ressourcen. Das ist vertretbar, weil Bootstrap sich nicht ändert;
  sollte aber bewusst so bleiben und nicht zufällig.
- **E3:** Dem Kunden mitgeben, dass seine Screenshots mit einem veralteten Stylesheet entstanden sind und
  er die Seite einmal hart neu laden soll (Strg+F5). Für das Bild-Panel (`Bildauswahl-Overlay-Panel.png`)
  zusätzlich nachfragen, ob es nach dem Neuladen noch weiß ist — dieses eine Bild lässt sich mit der
  Cache-Hypothese nicht vollständig erklären (Abschnitt 1.4).
- **E4:** Die beiden flackernden Playwright-Tests (4.4) betreffen nicht diesen Branch, tauchen aber
  regelmäßig auf. Ein eigener Blick auf deren Wartezeiten wäre unabhängig davon sinnvoll.
- **E5:** `docs/features/task/customer-feedback.md` wurde wie vereinbart nur gelesen, nicht verändert und
  nicht abgehakt — das Abhaken bleibt beim Kunden.
- Belege liegen bewusst außerhalb des Repositories (`<SCRATCH>`); es wurden keine Skripte oder Bilder
  eingecheckt. Der für die Prüfung angelegte temporäre Testtyp wurde nach dem Lauf wieder entfernt,
  Produktivcode wurde nicht verändert.
