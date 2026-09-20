# Abnahme: Korrektur Kundenrückmeldung 2 (Playlist-Übersicht und -Detailansicht)

Unabhängige Prüfung (nicht der Implementierer) von `512dbac` auf
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-kundenrueckmeldung-2`
gegen den Basisbranch `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln`.

Geprüft wurde jeder offene Punkt aus `docs/features/task/customer-feedback.md` **selbst im gerenderten
Browser**, nicht anhand des Implementierer-Berichts. Dessen Behauptung, die Punkte 3, 4, 5, 8 und 9 seien
bereits erfüllt, wurde bewusst misstrauisch nachgemessen.

**Anmerkung zur Aufgabenstellung:** Die Kundendatei enthält **10** offene `[ ]`-Punkte (nicht 13) und 11
abgehakte. Die Nummerierung 1–10 unten entspricht der Reihenfolge in der Datei und der des
Implementierer-Berichts.

## Prüfaufbau

- Frischer Build ohne Zwischenstände: `dotnet build VideoPlayer.sln -c Debug --no-incremental` und
  `-c Release --no-incremental`, beide **0 Fehler** (244 Warnungen, unverändert vorbestehend).
- Eigene, wegwerfbare Playwright-Harnische im Testprojekt (`ZzAbnahmeShots.cs`, `ZzAbnahmeShots2.cs`, nach
  der Prüfung gelöscht, nicht committet), gegen eine echte gehostete `Program`-Instanz mit eigener
  SQLite-Datenbank und eigenen Testdaten: Playlists mit/ohne Bild, Bilder 1600x520 / 400x3000 / 2600x2600 /
  4000x300, sehr langer Name (117 Zeichen) und sehr lange Beschreibung, eigene öffentliche Playlist, fremde
  öffentliche Playlist eines zweiten Benutzers, leere Playlist, Playlist mit Filmen und einer Serienepisode,
  Vergleichsserie und Vergleichsfilme für die Medienübersicht.
- Auflösungen: **1440x900, 1280x720, 1024x768, 390x844**. Browser: **Chromium**, zusätzlich **Firefox** und
  **WebKit** (beide installiert und tatsächlich gelaufen).
- Screenshots und Messwerte (nicht im Repository):
  `…\scratchpad\pruef\` — `messungen.txt`, `kopfhoehen.txt`, `dialoge.txt`, `auswahl.txt`, `browser.txt`,
  `filterleiste.txt` sowie die PNG-Dateien `p12-*`, `p35-*`, `p4-*`, `p67-*`, `p89-*`, `p10-*`, `p1-*`,
  `x-firefox-*`, `x-webkit-*`. Alle genannten Screenshots wurden angesehen, nicht nur erzeugt.
- Volle Testsuite `dotnet test VideoWebPlayer.Tests` (Debug, `--no-build`): **1222/1222 grün**, im ersten
  Lauf, keine Wiederholung nötig, kein Flackern beobachtet.
- Gegenproben durch gezieltes Kaputtmachen (danach per `git checkout` zurückgesetzt, siehe „Hinweise").

## Ergebnis

**Status:** Abweichungen gefunden

8 der 10 offenen Punkte sind erfüllt und im Browser bestätigt. Zwei Punkte (1 und 2) sind **teilweise**
erfüllt. Die Behauptung des Implementierers, die Punkte 3, 4, 5, 8 und 9 seien im Ist-Stand bereits erfüllt,
ist nach eigener Messung **zutreffend** — sie sind in Chromium, Firefox und WebKit, Desktop wie Mobil,
sichtbar erfüllt. Die wahrscheinlichste Erklärung für die abweichende Wahrnehmung des Kunden ist ein
**zwischengespeichertes `app.css`**; dafür gibt es einen konkreten technischen Befund (siehe Abweichung A3),
der behoben werden sollte, statt ihn nur als Vermutung stehen zu lassen.

| # | Kundenpunkt (Kurzfassung) | Ergebnis |
|---|---|---|
| 1 | Filterleiste als ein Panel mit drei Schaltbereichen, Trennlinien, alle Buttons gleicher Stil/Ebene | **Teilweise** |
| 2 | Kacheln wie Film-/Serienkacheln: Bild ganzflächig, Titel unten, keine Zusatztexte, Symbol oben rechts | **Teilweise** |
| 3 | Bild-Overlay-Panel braucht einen Rahmen | **Erfüllt** |
| 4 | Vorschaubild auf Panelbreite begrenzt, keine Scrollbalken | **Erfüllt** |
| 5 | Bearbeiten-Panel und Löschbestätigung brauchen einen Rahmen | **Erfüllt** |
| 6 | Playlist-Bild dezenter, Informationen über dem Bild | **Erfüllt** |
| 7 | Kopfbereich mit vorgegebener Höhe, unabhängig vom Bild | **Erfüllt** |
| 8 | Keine Abspielen-/Entfernen-Buttons in der Liste, Auswahl zeigt Titelinfos im Kopfbereich | **Erfüllt** |
| 9 | Entfernen über Löschen-Symbol im Kopfbereich auf dem Bild | **Erfüllt** |
| 10 | Moduswechsel im Kopfbereich auf dem Bild, immer nur ein Symbol | **Erfüllt** |

---

## Befunde je Kundenpunkt

### 1. Filterleiste und „Neue Playlist" — Teilweise

**Erfüllt.** Gemessen bei 1440x900 (`messungen.txt`): Die Gruppe `#playlist-filter-group` trägt den Rahmen
(`1px rgba(255,180,170,0.42)`), die Rundung (`8px`), den Hintergrund und `overflow: hidden`; die drei
Schaltbereiche haben `border-width: 0px` und hängen lückenlos aneinander (x = 1195 / 1247 / 1299, je 52x42).
Der Plus-Button liegt mit y = 111, 52x42, gleichem Radius (`8px`), gleicher Rahmenfarbe und gleichem
Hintergrund direkt daneben auf derselben Ebene (Oberkanten identisch ±0px). Der aktive Bereich ist flächig
`rgb(229, 9, 20)`. Das gilt bei 1280x720, 1024x768 und 390x844 ebenso (`p12-uebersicht-*.png`) sowie in
Firefox und WebKit (`browser.txt`). Der frühere Eindruck „vier verschiedene Buttons" ist weg.

**Abweichung (siehe A1).** Der Kunde verlangt „ein Panel … mit drei Symbolen und **je einer vertikalen
Trennlinie dazwischen**". Die Trennlinien werden aber an den Kanten des aktiven Bereichs ausgeblendet
(`app.css`, `.playlist-filter-button.active::before, .playlist-filter-button.active + .playlist-filter-button::before { background: transparent; }`).
Gemessen (`filterleiste.txt`, Screenshots `p1-filterleiste-aktiv-*.png`, `p1-aktionszeile-eigene.png`):

| aktiver Filter | sichtbare Trennlinien |
|---|---|
| Alle (links) | 1 von 2 |
| **Eigene (Mitte)** | **0 von 2** |
| Öffentliche (rechts) | 1 von 2 |

Im Zustand „Eigene" ist also **keine einzige** Trennlinie zu sehen. Das ist genau der Zustand, in dem der
Kunde die Leiste am ehesten betrachtet, wenn er nur eigene Playlists sieht.

### 2. Kacheln der Übersicht — Teilweise

**Erfüllt.** `PlaylistTile.razor` rendert nur noch den Titel im Verlaufs-Overlay am unteren Rand
(`messungen.txt`: `tileTexts` enthält je Kachel exakt den Playlist-Namen und sonst nichts). Beschreibung,
Genres, Sortiersymbol, das Wort „Öffentlich" und die Datumszeile sind weg. Das Bild füllt die Kachel
(`object-fit: cover`), das Kennzeichen (Globus für eigene öffentliche, Personen-Symbol für fremde) sitzt
30x30 px in der rechten oberen Ecke. Das Platzhaltersymbol ist auf 64px/Deckkraft 0.35 reduziert
(`p12-uebersicht-desktop-1440x900.png`, Kachel „Ohne Bild"). Das Kachelbild trägt denselben Dämpfungsfilter
wie `.media-poster`. Auf dem Telefon eine Kachel je Zeile (`card.width` 330px bei 390px Bildschirm).
Umlaute korrekt („Öffentlich Eigen", „Fremde Öffentliche Playlist").

**Abweichung (siehe A2a).** Ein wirklich langer Name wird weiterhin abgeschnitten. Gemessen bei 1440x900 mit
einem 117 Zeichen langen Namen: `-webkit-line-clamp: 3`, `scrollHeight 78 > clientHeight 58` — die Kachel
zeigt „… und noch…" mit Auslassungspunkten (`p12-uebersicht-desktop-1440x900.png`, zweite Kachel; bei
1024x768 dasselbe). `Playlist.NameMaxLength` erlaubt 255 Zeichen. Release Notes und
`docs/help/playlists.md` behaupten dagegen ausdrücklich, der Titel bleibe „vollständig lesbar". Der
absichernde Test `LongTitle_WrapsOverSeveralLines_AndUsesTheFullTileWidth` verwendet einen nur 65 Zeichen
langen Namen, der in drei Zeilen passt — die Beanstandung des Kunden („bei einem langen Titel noch nicht mal
komplett angezeigt werden kann") ist damit nicht abgesichert.

**Bewusste Abweichung (siehe A2b).** Der Kunde schreibt: „Die Panel sollen so aussehen wie auch die Film oder
Serienauflistungselemente in ihren Übersichten dargestellt werden." Gemessen: Playlist-Kachel `16 / 9`
(330x185), Film-/Serienkachel `2 / 3` (178x267), Titel dort auf 2 Zeilen geklemmt, hier auf 3. Struktur,
Klassen und Overlay sind identisch übernommen, das **Format** nicht. Die Begründung des Implementierers
(erzeugte Cover sind 1600x520-Banner) ist technisch nachvollziehbar und im Gesamteindruck stimmig — es bleibt
aber eine Abweichung von einer wörtlichen Kundenvorgabe, die der Kunde erneut beanstanden kann.

**Gesamteindruck (Maßstab „gewollt aber nicht gekonnt").** Die Übersicht wirkt jetzt aufgeräumt und
konsistent: eine Aktionszeile, gleich große Schaltflächen, Kacheln mit klarer Bildfläche und einer einzigen
Beschriftung. Der frühere amateurhafte Eindruck (riesiges Listensymbol, gequetschter Titel, Metazeile) ist
beseitigt. Der einzige verbleibende Stilbruch gegenüber den Film-/Serienübersichten ist das Kachelformat.

### 3. Rahmen des Bild-Overlay-Panels — Erfüllt

Selbst geöffnet und angesehen (`p35-dialog-bildpanel-breit-desktop-1440x900.png`,
`p35-dialog-bildpanel-hoch-mobil-390x844.png`). Das Panel hat einen **visuell klar erkennbaren** Abschluss:
`border: 1px solid rgb(66,71,84)` auf deckendem `rgb(23,31,51)`, zusätzlich ein heller Ring
`0 0 0 1px rgba(173,198,255,0.22)` und ein Schlagschatten, davor der abdunkelnde Hintergrund
`rgba(6,14,32,0.78)`. Der Selektor greift tatsächlich: `.admin-dialog-overlay` definiert die `--admin-*`-
Variablen auch außerhalb von `.admin-console`, deshalb ist `var(--admin-border)` gültig. Keine
Bootstrap-Modal-Klassen im Spiel, keine Blazor-CSS-Isolation (die Regeln stehen in `wwwroot/app.css`).
Identischer Befund in Firefox und WebKit (`browser.txt`: `border: 1px rgb(66,71,84)`, `bg: rgb(23,31,51)`).

### 4. Breite des Vorschaubildes im Bild-Panel — Erfüllt

Gemessen mit **4000x300** und **400x3000** als aktuelles Bild sowie mit frisch erzeugter Vorschau
(1600x520), je Desktop und Mobil (`dialoge.txt`):

| Fall | Panel sw/cw | Panel sh/ch | Vorschaubereich sw/cw | Bild |
|---|---|---|---|---|
| 4000x300, Desktop | 638/638 | 409/409 | 596/596 | 596x45 |
| erzeugt, Desktop | 638/638 | 579/579 | 596/596 | 596x194 |
| 400x3000, Desktop | 638/638 | 529/529 | 596/596 | 35x260 |
| 4000x300, Mobil | 356/356 | 541/541 | 314/314 | 314x24 |
| 400x3000, Mobil | 356/356 | 661/661 | 314/314 | 35x260 |

`scrollWidth == clientWidth` und `scrollHeight == clientHeight` überall; die Liste überlaufender Vorfahren
(programmatisch über die gesamte Elternkette ermittelt) ist leer; `document.documentElement` hat keinen
waagerechten Überlauf. In den Screenshots ist keine Scrollleiste zu sehen. Gilt ebenso in Firefox und WebKit.

### 5. Rahmen von Bearbeiten-Panel und Löschbestätigung — Erfüllt

Selbst geöffnet und angesehen: Bearbeiten-Panel (`p35-dialog-bearbeiten-desktop-1440x900.png`),
Löschbestätigung (`p35-dialog-loeschen-desktop-1440x900.png`), Bild-entfernen-Bestätigung,
Sortiermodus-Bestätigung und Genre-Editor, je Desktop und Mobil. Alle tragen `.admin-dialog` mit demselben
Rahmen, Hintergrund, Ring und Schatten wie unter Punkt 3 gemessen und sind im Screenshot eindeutig vom
Seiteninhalt abgesetzt.

### 6. Bild dezenter, Informationen über dem Bild — Erfüllt

Gemessen (`kopfhoehen.txt`): Das Kopfbild trägt `filter: saturate(1.04) contrast(1.04) brightness(0.88)` —
derselbe Filter wie `.media-poster` — und liegt mit `z-index: 0` unter dem Verlaufs-Overlay (`z-index: 1`)
und dem Inhalt (`z-index: 2`). Im Screenshot `p67-kopf-breit-desktop-1440x900.png` stehen Name, Sortierung,
Beschreibung und Datumsangaben klar lesbar über dem gedämpften Bild; im Vergleich zur Serienseite
(`p67-serie-desktop-1440x900.png`) ist die Wirkung gleichwertig (die Playlist ist durch den zusätzlichen
Filter sogar etwas dezenter als die Serienseite, die nur den Verlauf hat). Kein Stacking-Problem.

### 7. Höhe des Kopfbereichs — Erfüllt

Gemessen für **kein Bild / 1600x520 / 400x3000 / 2600x2600 / sehr langer Name + sehr lange Beschreibung**
(`kopfhoehen.txt`):

| Auflösung | Playlist (alle fünf Fälle) | Serienseite |
|---|---|---|
| 1440x900 | 620 px | 621 px |
| 1280x720 | 620 px | 621 px |
| 1024x768 | 532,47 px | 533,47 px |
| 390x844 | 440 px | 519,91 px (wächst mit dem Text) |

Die Höhe ist in jedem Fall identisch und hängt weder vom Bild noch vom Textumfang ab; sie entspricht ab
~846px Breite der Serienseite. Auf dem Telefon ist sie mit 440px bewusst höher als die `min-height` der
Serienseite (360px), dafür im Gegensatz zu dieser wirklich fest. In Firefox und WebKit dieselben Werte
(620 / 440).

**Preis dafür (siehe Hinweis H1):** Name (2 Zeilen) und Beschreibung (3 Zeilen, mobil 2) werden mit „…"
abgekürzt — im Test mit 117-Zeichen-Namen `scrollHeight 457` gegen `clientHeight 129`
(`p67-kopf-langertext-desktop-1440x900.png`). Der vollständige Text steht weiterhin im Markup und im
Bearbeiten-Panel. Das ist eine vom Kunden nicht verlangte Einschränkung, aber eine unvermeidliche Folge der
verlangten festen Höhe und in Hilfe und Release Notes korrekt dokumentiert.

### 8. Keine Buttons in der Liste, Auswahl zeigt Informationen im Kopfbereich — Erfüllt

Gemessen (`auswahl.txt`, Screenshot `p89-auswahl-desktop-1440x900.png`):

- Die drei Kacheln der Titelliste enthalten **kein einziges** `<button>` (`rowButtons: [[],[],[]]`);
  `role="option"` / `aria-selected` sind gesetzt.
- Klick auf die Episode zeigt im Kopfbereich: Poster, Titel „Der Anfang vom Ende", Jahr „2019", Art
  „Episode 3", Handlung und „Hinzugefügt: 21.09.2026, 00:37 Uhr" — also Titel, Bild, Handlung, Datum und
  Episodennummer wie bei den Serien.
- Der Abspielen-Button im Kopfbereich funktioniert und behält den Playlist-Kontext: URL
  `…/playlists/1?entryId=1`, Spieler offen, Badge `[Auswahltest: 1/3]`.
- Fremde öffentliche Playlist (als anderer Benutzer): keine Aktionsleiste, keine Buttons an den Kacheln,
  kein Hinweis auf den Besitzer im Seitentext (`bodyHasEmail: false`, `p10-fremde-playlist.png`).

Die Sicherheitsabfrage bei Weiterschauen-Bezug ist unverändert vorhanden und durch die weiterhin grünen
Tests `Owner_RemoveFromHeader_ContinueWatchingConflict_AsksFirst_ThenConfirmedRemovalSucceeds` abgedeckt.

### 9. Entfernen-Symbol im Kopfbereich auf dem Bild — Erfüllt

Gemessen (`auswahl.txt`): Der Entfernen-Button erscheint **nur** bei ausgewähltem Titel und **nur** für den
Besitzer; sein Rechteck (Desktop 1372/84, 44x44; Mobil 326/-657, 40x40) liegt vollständig innerhalb des
Kopfbereichs **und** innerhalb des Rechtecks des Kopfbildes (`removeInsideHeader: true`,
`removeOnImage: true`). Ein Klick entfernt den Titel tatsächlich (3 Einträge vorher, 2 nachher). Im
Nur-Lese-Modus wird die Leiste gar nicht gerendert.

### 10. Moduswechsel im Kopfbereich, immer nur ein Symbol — Erfüllt

Gemessen (`auswahl.txt`, Screenshots `p10-leer-hinzufuegen.png`, `p10-leer-nach-wechsel.png`):

- Im Inhaltsbereich existiert kein Modus-Button mehr (`modeButtonsInContent: []`).
- Im Kopfbereich existiert pro Zustand **genau einer**, und er zeigt das Symbol des Zielbereichs:
  Titelliste → `#playlist-mode-add-button` („Titel hinzufügen", Plus); Hinzufügen-Bereich →
  `#playlist-mode-entries-button` („Titel der Playlist auflisten", Liste). Nie beide gleichzeitig.
- Leere Playlist startet direkt im Hinzufügen-Bereich (`searchVisible: true`) und bietet nur den Weg zurück
  zur Liste; der Wechsel funktioniert in beide Richtungen.
- Nicht-Besitzer sieht keinen Umschalter (`add: false`, `entries: false`).
- Auf 390px Breite passen die fünf Symbolbuttons nebeneinander in die Leiste auf dem Bild
  (`p67-kopf-langertext-mobil-390x844.png`).

### Regressionsprüfung der bereits abgehakten `[x]`-Punkte — keine Regression

| Punkt | Befund |
|---|---|
| Umlaute | „Öffentlich Eigen", „Fremde Öffentliche Playlist", „Hinzugefügt", „Zurück zur Übersicht", „Löschen" korrekt (Screenshots) |
| Übersicht zusammengeführt, keine Dopplung, Fremd-Symbol | Eigene öffentliche Playlist erscheint genau einmal (mit Globus), fremde einmal (mit Personen-Symbol), beide oben rechts |
| Datumsabstände im Kopfbereich | „Erstellt: 18.09.2026, 00:33 Uhr   Aktualisiert: 20.09.2026, 00:33 Uhr" — beschriftet, mit Doppelpunkt und klarem Abstand |
| Bild-Symbol-Button mit Overlay und Entfernen | Ein Bild-Symbol-Button öffnet das Panel; „Hochladen"/„Anwenden" und „Bild entfernen" samt Rückfrage vorhanden |
| Aktionsbuttons auf dem Bild | Alle Kopfbereichs-Buttons liegen innerhalb des Bildrechtecks |
| Veröffentlichen-Symbol wechselt | Privat: Schloss (`playlist-private-active`), öffentlich: Globus; Tooltip nennt Zustand und Wirkung |

---

## Abweichungen

- [ ] **A1 — Filterleiste: Trennlinien verschwinden (Kundenpunkt 1).** Datei `VideoWebPlayer/wwwroot/app.css`,
      Regel `.playlist-filter-button.active::before, .playlist-filter-button.active + .playlist-filter-button::before { background: transparent; }`.
      **Reproduktion:** `/playlists` öffnen, Filter „Eigene" (Mitte) wählen → die Leiste zeigt **null**
      Trennlinien; bei „Alle" bzw. „Öffentliche" nur eine von zwei. Der Kunde hat „je einer vertikalen
      Trennlinie dazwischen" verlangt. Beleg: `filterleiste.txt`, `p1-aktionszeile-eigene.png`,
      `p1-filterleiste-aktiv-playlist-filter-all.png`.
      **Empfehlung:** Trennlinien immer zeichnen, im Kontrast zur gefüllten Fläche (z. B. an der Kante zum
      aktiven Bereich `rgba(255,255,255,0.35)` statt `transparent`), oder — falls das optisch stört — die
      Entscheidung dem Kunden ausdrücklich zur Abnahme vorlegen. Der Test
      `FilterBar_IsOnePanelWithThreeSwitchAreasAndVerticalRules` prüft die Trennlinie nur an
      `#playlist-filter-public` im Zustand „Alle", also genau im einzigen Zustand, in dem sie sichtbar ist;
      er müsste alle drei Zustände abdecken.

- [ ] **A2a — Sehr langer Kacheltitel wird weiterhin abgeschnitten (Kundenpunkt 2).**
      `.playlist-card-title { -webkit-line-clamp: 3; }` in `app.css`.
      **Reproduktion:** Playlist mit einem ≥110 Zeichen langen Namen anlegen, `/playlists` bei 1440x900 oder
      1024x768 öffnen → Titel endet mit „…", `scrollHeight 78 > clientHeight 58`. `NameMaxLength` erlaubt 255
      Zeichen. Die Beanstandung lautete ausdrücklich, der Titel könne „noch nicht mal komplett angezeigt
      werden". Zusätzlich behaupten `docs/RELEASE_NOTES.md` und `docs/help/playlists.md`, der Titel bleibe
      „vollständig lesbar" — das trifft so nicht zu.
      **Empfehlung:** Entweder die Klemmung für die Playlist-Kachel aufheben bzw. deutlich erhöhen und den
      Titel bei Bedarf die Schriftgröße reduzieren lassen, oder — falls das Abkürzen bewusst bleiben soll —
      Release Notes und Hilfe ehrlich formulieren („bis zu drei Zeilen, danach abgekürzt"). Der
      Regressionstest sollte einen Namen nutzen, der die drei Zeilen tatsächlich sprengt.

- [ ] **A2b — Kachelformat weicht vom Film-/Serienvorbild ab (Kundenpunkt 2, bewusste Abweichung).**
      `.playlist-card { aspect-ratio: 16 / 9; }` gegenüber `.media-box { aspect-ratio: 2 / 3; }`.
      Der Kunde hat „so aussehen wie auch die Film oder Serienauflistungselemente" verlangt. Begründung des
      Implementierers (Banner-Cover 1600x520) ist nachvollziehbar und das Ergebnis wirkt in sich stimmig, die
      Entscheidung gehört aber dem Kunden.
      **Empfehlung:** Dem Kunden die Wahl vorlegen; bei Wunsch nach Hochformat müssten auch
      `PlaylistSettings.GeneratedCoverWidthPixels/HeightPixels` geändert werden.

- [ ] **A3 — Kein Cache-Busting für `app.css` (Ursache der „schon erfüllten" Punkte 3/4/5/8/9).**
      `VideoWebPlayer/Components/App.razor:9` bindet `<link rel="stylesheet" href="app.css" />` **ohne**
      Fingerprint/Version ein, und `VideoWebPlayer/Extensions/WebApplicationExtensions.cs:56` nutzt
      `app.UseStaticFiles()` statt `MapStaticAssets`. Selbst gemessen: `GET /app.css` liefert **kein**
      `Cache-Control` (nur `ETag` und `Last-Modified`, siehe `browser.txt`). Browser wenden dann heuristisches
      Caching an und können ein altes Stylesheet **ohne Rückfrage** weiterverwenden — genau das erklärt, warum
      der Kunde reine CSS-Punkte (Rahmen, Vorschaubreite) als offen führt, während sie im frischen Browser
      erfüllt sind. Der Implementierer nennt das nur als Vermutung unter „Restrisiken" und behebt es nicht.
      **Empfehlung:** `MapStaticAssets()` plus `@Assets["app.css"]` (bzw. ein Versionsparameter) einsetzen —
      dann ist die Frage „welchen Stand sieht der Kunde" dauerhaft erledigt statt bei jeder Rückmeldung neu
      zu klären.

## Hinweise

- **H1 — Gekürzte Texte im Kopfbereich (Folge von Punkt 7, kein Mangel).** Name auf 2, Beschreibung auf 3
  (mobil 2) Zeilen geklemmt. Das ist der Preis für die verlangte feste Höhe, ist in Hilfe und Release Notes
  korrekt beschrieben und stand so nicht in der Kundenrückmeldung — sollte dem Kunden aber aktiv genannt
  werden, damit es nicht als nächste Beanstandung zurückkommt.

- **H2 — Gegenproben (selbst durchgeführt, Änderungen danach zurückgesetzt).**
  - `.playlist-filter-button { border: 1px solid var(--vp-border-strong); }` wiederhergestellt →
    `FilterBar_IsOnePanelWithThreeSwitchAreasAndVerticalRules` schlägt fehl
    („#playlist-filter-all hat einen eigenen Rahmen (1px)"). Der Test greift also wirklich.
  - `.playlist-header { height: … }` auf `min-height: …` zurückgedreht →
    `Header_KeepsItsHeight_WithAVeryLongTitleAndDescription` bleibt **grün**. Erst wenn zusätzlich
    `-webkit-line-clamp` des `h1` entfernt wird, schlägt er fehl („Der Kopfbereich waechst mit langem
    Titel/Beschreibung: 1193,58px statt 621px"). Die als Kern von Punkt 7 gemeldete Änderung
    (`height` statt `min-height`) ist also **allein nicht abgesichert**; abgesichert ist nur, dass mindestens
    einer der beiden Schutzmechanismen vorhanden ist. Umgekehrt geprüft: mit `height` und ohne Klemmung
    bleibt die Höhe bei 620px — `height` erfüllt seinen Zweck. Kein Mangel an der Funktion, aber eine Lücke
    in der Testabsicherung.
  - Die CSS-Änderungen wirkten sich in den Testläufen sofort aus (bestätigt durch den fehlschlagenden Lauf),
    die Gegenproben sind also aussagekräftig.

- **H3 — Statischer veränderlicher Zustand im Test.**
  `VideoWebPlayer.Tests/Components/PlaylistEntriesListSelectionTests.cs`:
  `private static readonly List<bool> ReportedModes = new();` wird in `Render(...)` geleert. Solange xUnit die
  Tests einer Klasse nacheinander ausführt, geht das gut; bei Parallelisierung innerhalb der Klasse würde es
  flackern. Sauberer wäre ein Instanzfeld.

- **H4 — Kleinigkeit im Bild-Panel (kein Scrollbalken).** `.playlist-cover-panel-image { max-height: 260px }`
  ist genauso groß wie die `max-height: 260px` des umgebenden `.playlist-cover-panel-preview`, die den
  1px-Rahmen einschließt. Ein hochformatiges Bild wird dadurch um 2px beschnitten (gemessen: Bild 260px in
  einem Innenbereich von 258px, `overflow: hidden`). Optisch nicht auffällig, sauber wäre
  `max-height: 100%` oder 258px.

- **H5 — Lokalisierung.** Das Projekt hat keinerlei Lokalisierungsinfrastruktur (kein `IStringLocalizer`,
  keine Ressourcendateien); alle Oberflächentexte sind projektweit deutsch fest verdrahtet. Die neuen Texte
  („Titel hinzufügen", „Titel der Playlist auflisten") folgen diesem bestehenden Muster — keine neue
  Abweichung, aber weiterhin eine offene Altlast.

- **H6 — Zugriffskontrolle / Datenschutz.** Die Aktionsleiste im Kopfbereich wird für Nicht-Besitzer gar
  nicht gerendert; `PlaylistEntriesList.SetModeAsync` bricht bei `IsReadOnly` ab; der Server lehnt Änderungen
  Fremder unabhängig davon ab (unverändert aus früheren Schritten, Tests grün). In der fremden öffentlichen
  Playlist wurden im gerenderten Seitentext keine Angaben zum Besitzer gefunden. Ein gesperrter Eintrag zeigt
  weiterhin weder Handlung noch Datum noch Episodennummer (bestehende Tests, unverändert grün).

- **H7 — Build und Tests.** `dotnet build VideoPlayer.sln -c Debug --no-incremental` und
  `-c Release --no-incremental`: je 0 Fehler. `dotnet test VideoWebPlayer.Tests` (Debug): **1222/1222 grün**,
  2 min 54 s, im ersten Lauf ohne Fehlschlag — das im Implementierer-Bericht erwähnte Flackern trat hier
  nicht auf. Kein Test ist deaktiviert oder abgeschwächt; die angepasste Hilfsklasse
  `PlaylistsE2ETestBase.ShowAddModeAsync/ShowEntriesAsync/SelectSearchResultAsync` wartet nachweislich
  strenger als vorher (auf tatsächlich erschienene Elemente statt auf feste Zeiten).

- **H8 — Browser.** Firefox und WebKit wurden zusätzlich zu Chromium gefahren (Desktop und Mobil). Feste
  Kopfhöhe (620/440), Filterleiste mit Rahmen und sichtbarer Trennlinie, Panelrahmen
  (`1px rgb(66,71,84)` auf `rgb(23,31,51)`) und scrollbalkenfreie Vorschau verhalten sich identisch;
  `hyphens: auto` und `-webkit-line-clamp` greifen in allen dreien. Belege: `browser.txt`,
  `x-firefox-*.png`, `x-webkit-*.png`.

- **H9 — Zahl der offenen Punkte.** Die Kundendatei enthält 10 offene `[ ]`-Punkte, nicht 13. Die Datei
  `docs/features/task/customer-feedback.md` wurde nur gelesen, nicht verändert und nicht abgehakt.

## Empfehlung

Die fünf vom Implementierer als „bereits erfüllt" gemeldeten Punkte sind es tatsächlich — das war korrekt
berichtet und ist hier unabhängig nachgemessen. Vor der erneuten Vorlage beim Kunden sollten **A1** (immer
sichtbare Trennlinien) und **A3** (Cache-Busting) behoben werden; beides ist klein und beseitigt genau die
zwei Punkte, an denen die Rückmeldung sonst wieder auflaufen wird. **A2a** sollte mindestens in der
Dokumentation ehrlich gestellt werden. **A2b** ist eine Gestaltungsentscheidung und gehört dem Kunden
vorgelegt, nicht vom Projekt entschieden.
