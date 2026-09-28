# Korrektur Kundenrückmeldung 2 (Playlist-Übersicht und -Detailansicht)

Grundlage: die vom Kunden überarbeitete Datei `docs/features/task/customer-feedback.md` (nicht verändert, nicht
committet). Geprüft wurde nicht anhand von Code oder bestehenden Tests, sondern anhand des tatsächlich
gerenderten Ergebnisses: Die Anwendung lief unter Playwright/Chromium gegen eine echte, gehostete
`Program`-Instanz (Testbenutzer wie in `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`), Screenshots in
Desktop- (1440x900) und Mobilgröße (390x844). Die Anwendung hat nur ein (dunkles) Erscheinungsbild, deshalb
kein Hell/Dunkel-Vergleich.

Ablage der Screenshots und der Hilfsskripte (nicht im Repository):

```
C:\Users\Martin\AppData\Local\Temp\claude\D--Repositories-softwareschmiede-fd729906-8801-43ee-8539-fa3e046c88f4\
  e838bc0e-97f1-46db-9b8f-7f49b0cca26b\scratchpad\
    shots\    (Ausgangszustand vor der Korrektur)
    shots2\   (nach Übersicht/Moduswechsel)
    shots3\   (nach fester Kopfhöhe, vor der Mobil-Nachbesserung)
    shots4\   (Endstand)
    ZzScreenshotHarness.cs, ZzDiagnose.cs   (Hilfsklassen, aus dem Repo entfernt)
    app.css.new                             (Sicherung für die Gegenprobe)
```

## Vorbemerkung: was tatsächlich offen war

Fünf der zehn offenen Punkte (3, 4, 5, 8, 9) waren im Stand des Branches bereits erfüllt. Das ist im Browser
nachgewiesen (Screenshots unten), nicht nur im Code. Warum der Kunde sie trotzdem als offen führt, konnte ich
nicht reproduzieren — plausibel ist ein älterer Stand bzw. ein zwischengespeicherter `app.css` auf seiner
Seite, denn genau die Punkte, die reine CSS-Änderungen aus dem Merge `d5fe761` betreffen, sind betroffen. Ich
melde diese Punkte deshalb ausdrücklich als „im aktuellen Stand erfüllt und im Browser bestätigt", nicht als
„von mir behoben", und habe sie durch zusätzliche Regressionstests abgesichert.

Echte Fehler gefunden und behoben: Punkte 1, 2, 10 sowie — beim Nachmessen zu Punkt 7 — eine bisher unbekannte
Abhängigkeit der Kopfhöhe vom Textumfang.

---

## 1. Filterbuttons und „Neue Playlist" — behoben

**Ursache.** Jeder der drei Filterbuttons hatte einen *eigenen* 1px-Rahmen (mit `margin-left: -1px` überlappt),
der aktive zusätzlich einen `box-shadow`-Ring plus roten Schlagschatten. Optisch waren das drei einzelne
Buttons, kein Panel. Die Schaltfläche „Neue Playlist" war ein Bootstrap-`btn btn-primary`, also flächig rot mit
Bootstrap-Rundung — ein vierter Button in einem ganz anderen Stil. Belegt in
`shots\02-uebersicht-desktop.png`.

**Änderung** (`VideoWebPlayer/wwwroot/app.css`):

- Rahmen, Rundung und Hintergrund gehören jetzt der Gruppe `.playlist-filter-group` (`overflow: hidden`), die
  Schaltbereiche selbst sind rahmenlos und transparent.
- Die senkrechte Trennlinie ist das `::before` des jeweils folgenden Bereichs; an den Kanten des aktiven
  Bereichs wird sie ausgeblendet, damit dessen Fläche sauber abschließt.
- Der aktive Bereich ist flächig in der Akzentfarbe hinterlegt (kein Schatten-Ring mehr).
- `.playlist-create-icon-button` überschreibt die Bootstrap-Vorgaben vollständig und hat exakt dieselbe Höhe
  (42px), Breite (52px), Rundung, Rahmenfarbe, Hintergrundfarbe und Hover-Reaktion wie die Schaltbereiche;
  Hover/Fokus sind an `.metadata-icon-btn` der übrigen App angeglichen.

**Belege.** `shots2\02-uebersicht-desktop.png`, `shots2\02-uebersicht-mobil.png` (alle vier Schaltflächen auf
einer Ebene, ein Panel mit Trennlinien). Neue E2E-Tests in `VideoWebPlayer.Tests/PlaylistsOverviewLayoutE2ETests.cs`:
`FilterButtonsAndCreateButton_ShareOneLine_AndTheSameHeightAndShape` (gleiche Oberkante ±1px, gleiche Höhe
±1px, gleicher `border-radius`), `FilterBar_IsOnePanelWithThreeSwitchAreasAndVerticalRules`
(`border-width` der Gruppe ≥ 1px, der Schaltbereiche = 0px, lückenloses Aneinanderhängen, sichtbares
`::before`, genau ein aktiver Bereich in `rgb(229, 9, 20)`),
`NarrowScreen_KeepsTheActionRowOnOneLine_AndTilesReadable`.

**Gegenprobe.** Mit dem alten `app.css` schlägt `FilterBar_IsOnePanelWithThreeSwitchAreasAndVerticalRules` fehl
(die Schaltbereiche haben dort einen eigenen Rahmen).

## 2. Kacheln der Übersicht — behoben

**Ursache.** `PlaylistTile.razor` rendert(e) im Overlay neben dem Titel noch Beschreibung, Genres,
Sortiersymbol, den Text „Öffentlich" und eine Datumszeile. Dafür war `.playlist-card .media-card-overlay` auf
`flex-direction: column` umgestellt, der Titel auf zwei Zeilen geklemmt und ohne Silbentrennung — ein langer
Name war abgeschnitten. Das Platzhaltersymbol war mit `width/height: 36%` der Kachel überdimensioniert.
Belegt in `shots\02-uebersicht-desktop.png` und `shots\02-uebersicht-mobil.png`.

**Änderung.**

- `PlaylistTile.razor`: Titel ist die einzige Beschriftung, im Verlaufs-Overlay am unteren Rand, in denselben
  Klassen wie die Film-/Serienkacheln (`.media-box-link`, `.media-box`, `.media-card-overlay`, `.media-titles`,
  `.media-title-text`). Beschreibung, Genres, Sortiersymbol, Öffentlich-Text und Datumszeile entfallen; die
  nicht mehr benötigte `TruncateDescription`-Hilfsmethode wurde entfernt.
- Kennzeichen oben rechts: Fremd-Symbol wie bisher, für eine eigene öffentliche Playlist jetzt ein
  Globus-Symbol an derselben Stelle (gemeinsame Klasse `.playlist-tile-badge`), rein grafisch mit
  `title`/`aria-label`.
- Titel: `lang="de"` plus `hyphens: auto`, `overflow-wrap: anywhere`, bis zu drei Zeilen.
- Platzhaltersymbol: 64px statt 36 % der Fläche, Deckkraft 0.35.
- Raster `repeat(auto-fill, minmax(min(100%, 260px), 1fr))` — auf dem Telefon eine Kachel je Zeile statt zwei
  gequetschter.
- Das Kachelbild wird mit derselben Dämpfung dargestellt wie `.media-poster` bei Filmen/Serien.

**Bewusste Abweichung vom Film-/Serienvorbild.** Die Kachel bleibt breit (16/9) statt Hochformat 2/3. Grund:
Erzeugte Playlist-Cover sind Banner (`PlaylistSettings.GeneratedCoverWidthPixels/HeightPixels` = 1600x520); ein
2/3-Poster würde davon nur einen schmalen senkrechten Streifen zeigen und den Titel noch stärker in der Breite
einengen — genau das, was der Kunde beanstandet hat. Übernommen wurden dafür alle inhaltlichen Merkmale des
Vorbilds: Bild über die ganze Fläche (`object-fit: cover`), Titel im Verlaufs-Overlay unten, keine weiteren
Texte.

**Belege.** `shots2\02-uebersicht-desktop.png` (langer Name über drei Zeilen mit Trennstrichen vollständig
lesbar), `shots2\02-uebersicht-mobil.png`. bUnit: `Tile_ShowsOnlyTheTitle_NoDatesNoStatusTextNoDescriptionNoSortInfo`,
`TileBadges_SitInTheTopRightCorner_AndAreSymbolsOnly` (in `PlaylistsOverviewTests.cs`). E2E:
`Tile_CoverFillsTheWholeTile_TitleAtTheBottom_NoFurtherTexts` (Bildgröße = Kachelgröße ±2px, `object-fit:
cover`, Kacheltext exakt gleich dem Namen, Kennzeichen oben rechts),
`LongTitle_WrapsOverSeveralLines_AndUsesTheFullTileWidth` (≥ 2 Zeilen, `scrollHeight ≤ clientHeight`,
Titelbreite ≥ 80 % der Kachelbreite, `overflow-wrap: anywhere`, `hyphens: auto`),
`PlaceholderSymbol_StaysSmallComparedToTheTile` (≤ 50 % der Kachelhöhe, Deckkraft < 0.6).

**Gegenprobe.** Mit dem alten `app.css` schlagen `LongTitle_...` und `PlaceholderSymbol_...` fehl.

## 3. Rahmen des Bild-Overlay-Panels — im aktuellen Stand bereits erfüllt

**Befund.** Das Panel hat einen sichtbaren Rahmen (`.admin-dialog`: `1px solid var(--admin-border)`) und einen
deckenden Hintergrund samt Schatten. Die ursprüngliche Ursache (die `--admin-*`-Farbvariablen waren nur
innerhalb von `.admin-console` definiert, außerhalb ergab `var()` damit „kein Rahmen") ist im Branch bereits
behoben.

**Beleg.** `shots4\17-bild-panel.png`, `shots2\18-bild-panel-vorschau-generiert.png`. Abgesichert durch den
bestehenden Test `EveryOverlayPanelOfThePlaylistFeature_HasAVisibleFrameAndOpaqueBackground`
(`borderTopWidth ≥ 1px`, Rahmenfarbe und Hintergrund nicht transparent). Keine Änderung nötig.

## 4. Breite des Vorschaubildes im Bild-Panel — im aktuellen Stand bereits erfüllt, jetzt abgesichert

**Befund.** `.playlist-cover-panel-image` ist auf `max-width: 100%` und `max-height: 260px` begrenzt, der
Vorschaubereich auf `overflow: hidden`. Gemessen im Browser (`shots4\coverpanel.txt`):
Panel `scrollWidth 638 = clientWidth 638`, `scrollHeight 579 = clientHeight 579`; Vorschaubereich
`596/596` bzw. `194/194` — also kein waagerechter und kein senkrechter Scrollbalken.

**Neu.** Regressionstest `CoverPanel_PreviewNeverExceedsThePanel_AndCausesNoScrollbars` in
`PlaylistDetailLayoutE2ETests.cs` als `[Theory]` mit 4000x300 und 400x3000, jeweils für das aktuelle Bild und
für eine frisch erzeugte Vorschau: Bildbreite/-höhe ≤ Vorschaubereich und `scrollWidth ≤ clientWidth`,
`scrollHeight ≤ clientHeight` für Panel und Vorschaubereich.

**Beleg.** `shots4\17-bild-panel.png`, `shots2\18-bild-panel-vorschau-mobil.png`.

## 5. Rahmen von Bearbeiten-Panel und Löschbestätigung — im aktuellen Stand bereits erfüllt

**Befund.** Bearbeiten-Panel, Löschbestätigung, Sortiermodus-Bestätigung, Bild-entfernen-Bestätigung,
Weiterschauen-Bestätigung und Genre-Editor nutzen alle `ConfirmationDialog`/`.admin-dialog` und haben damit
denselben sichtbaren Rahmen und Hintergrund.

**Beleg.** `shots3\15-bearbeiten-panel.png`, `shots2\16-loeschen-bestaetigung.png`; bestehender Test
`EveryOverlayPanelOfThePlaylistFeature_HasAVisibleFrameAndOpaqueBackground` prüft alle sechs Dialoge, plus
`GenreAdminDialog_StillHasItsFrame` als Gegenprobe für die Administrationsseite. Keine Änderung nötig.

## 6. Bild dezenter, Informationen über dem Bild — teilweise erfüllt, Dämpfung ergänzt

**Ursachenanalyse (gemessen, nicht vermutet).** Ein Stacking-Problem gibt es nicht. Der Browser meldet für den
Kopfbereich (`shots\diag-header.json`): Platzhalter und `<img>` `position: absolute`, `inset: 0`, `z-index: 0`,
Rechteck exakt deckungsgleich mit dem Kopfbereich (1439x620), `object-fit: cover`; das Overlay
`.tvshow-header-overlay` liegt mit `z-index: 1` darüber, der Inhalt `.tvshow-header-content` mit `z-index: 2`.
Die Informationen liegen also bereits über dem Bild. (Der im Ausgangs-Screenshot wie ein separates Kästchen
wirkende Bereich war der orangefarbene Innenteil des synthetischen Testbildes, nicht ein falsch platziertes
Bild.)

**Was wirklich fehlte.** Das Bild war ungedämpft. Gedämpft hat es nur der Verlauf des Overlays, und der ist am
rechten Rand fast durchsichtig (`rgba(19,19,19,0.1)` bei 100 %) — eine farbige Collage wirkt dort grell,
anders als die Poster der Film-/Serienkacheln, die zusätzlich einen Dämpfungsfilter tragen.

**Änderung.** `.playlist-header .playlist-header-cover-image` und `.playlist-card .playlist-card-cover-image`
erhalten denselben Filter wie `.media-poster`: `saturate(1.04) contrast(1.04) brightness(0.88)`.

**Belege.** Vorher `shots\11-detail-breitbild-desktop.png`, nachher `shots2\11-detail-breitbild-desktop.png`
und `shots4\19-detail-langer-text-desktop.png`; Vergleichsbild der Serienseite
`shots\04-serie-detail-desktop.png`. Abgesichert durch den bestehenden Test
`Header_ImageIsDimmedLikeOnTheSeriesPage_AndActionButtonsSitOnTheImage` (identischer Overlay-Verlauf wie auf
der Serienseite, Bild deckungsgleich mit dem Kopfbereich, `object-fit: cover`, alle Aktionsbuttons innerhalb
des Kopfbereichs).

## 7. Höhe des Kopfbereichs — echter Fehler gefunden und behoben

**Befund zur Bildabhängigkeit.** Die Höhe hing bereits nicht vom Bild ab: gemessen 621px für „kein Bild",
1600x500, 400x3000 und 2600x2600 (`shots\heights.txt`).

**Echter Fehler.** Die Höhe hing vom *Textumfang* ab: `.tvshow-header` gibt nur eine `min-height` vor. Mit
einem langen Namen und einer langen Beschreibung wuchs der Kopfbereich auf **1378,31px statt 621px** — gemessen
durch den neuen Test `Header_KeepsItsHeight_WithAVeryLongTitleAndDescription`, der ohne die Korrektur genau mit
dieser Meldung fehlschlägt.

**Änderung** (`app.css`):

- `.playlist-header { height: clamp(440px, 52vw, 620px); }` — eine feste Höhe statt einer Mindesthöhe.
- `.playlist-header .tvshow-header-overlay { height: 100%; overflow: hidden; }` und
  `.playlist-header .tvshow-header-content { max-height: 100%; overflow: hidden; }` als Sicherheitsnetz:
  ohne das würde die Ausrichtung am unteren Rand (`align-items: flex-end`) bei Überlänge ausgerechnet den
  Namen oben abschneiden.
- Name auf zwei, Beschreibung/Handlung auf drei Zeilen begrenzt (auf Bildschirmen ≤ 640px zwei), damit nichts
  halb abgeschnitten stehen bleibt. Der vollständige Text bleibt im Markup und im Bearbeiten-Panel.

**Bewusste Abweichung.** Die Untergrenze ist 440px statt der 360px der Serienseite. Mit 360px wurde auf einem
390px breiten Bildschirm der Name oben abgeschnitten (`shots3\19-detail-langer-text-mobil.png` zeigt genau
das). Ab etwa 846px Breite ist die Höhe identisch zur Serienseite; bei 1400px ist sie es exakt (Test
`Header_HasTheSameFixedHeight_ForExtremeCoverImages_AsOnTheSeriesPage` vergleicht direkt mit der Serienseite).

**Belege/Messwerte.** `shots4\heights.txt`: `ohne=620 breit=620 hoch=620 riesig=620`, `langer-text-desktop=620`.
Screenshots: `shots4\19-detail-langer-text-desktop.png`, `shots4\19-detail-langer-text-mobil.png`,
`shots2\10..13-*.png`. Tests: bestehende Höhentests plus der neue Textlängentest.

## 8. Abspielen/Entfernen aus der Titelliste entfernt, Auswahl zeigt Informationen im Kopfbereich — im aktuellen Stand bereits erfüllt

**Befund.** Die Kacheln tragen keine Abspielen-/Entfernen-Buttons mehr. Ein Klick wählt den Titel aus (Kachel
markiert, `role="option"`/`aria-selected`, Enter/Leertaste/Escape), der Kopfbereich zeigt Poster, Titel, Jahr,
Art („Episode 3"), Zugehörigkeit, Handlung und Hinzufügedatum, mit Abspielen-Button im Overlay wie bei
Episoden. Die Wiedergabe behält den Playlist-Kontext (Badge „Name: 1/1", `?entryId=`). Gesperrte Einträge
zeigen weiterhin weder Handlung noch Datum noch Episodennummer.

**Belege.** `shots2\14-detail-titel-ausgewaehlt-desktop.png`, `shots4\14-detail-titel-ausgewaehlt-mobil.png`,
`shots2\21-fremde-detail-gesperrter-titel.png`. Tests: `PlaylistDetailHeaderTests`,
`PlaylistEntriesListSelectionTests.Tiles_HaveOptionSemantics_AndNoPlayOrRemoveButtons`,
`PlaylistDetailInteractionE2ETests`. Keine Änderung nötig.

## 9. Löschen-Symbol im Kopfbereich auf dem Bild — im aktuellen Stand bereits erfüllt

**Befund.** Der Entfernen-Button sitzt in `.metadata-action-bar` innerhalb des Kopfbereichs (also auf dem
Bild), nur für den Besitzer und nur bei ausgewähltem Titel. Die Sicherheitsabfrage bei Weiterschauen-Bezug
(Schritt 7) bleibt erhalten.

**Belege.** `shots2\14-detail-titel-ausgewaehlt-desktop.png`, `shots4\14-detail-titel-ausgewaehlt-mobil.png`.
Tests: `Owner_SelectingAnEntry_ShowsRemoveButtonInHeader_ViewerNever`,
`Owner_RemoveFromHeader_ContinueWatchingConflict_AsksFirst_ThenConfirmedRemovalSucceeds`. Durch Punkt 10 steht
der Entfernen-Button jetzt gemeinsam mit dem Bereichswechsel in derselben Leiste.

## 10. Modus-Buttons in den Kopfbereich, immer nur einer — behoben

**Ursache.** Der Umschalter war eine Gruppe aus zwei beschrifteten Schaltflächen (`#playlist-content-mode-group`)
im Inhaltsbereich; beide waren immer sichtbar, die aktive rot hinterlegt. Belegt in
`shots\11-detail-breitbild-desktop.png`.

**Änderung.**

- `PlaylistEntriesList.razor`: Die Gruppe entfällt. Neu ist der Parameter `OnModeChanged` (meldet den aktuellen
  Bereich, auch einmalig nach dem ersten Laden, das über die leere Playlist entscheidet) und die öffentliche
  Methode `ToggleModeAsync()`.
- `PlaylistDetail.razor`: Ein einzelner Symbolbutton in der Aktionsleiste auf dem Bild, nur für den Besitzer
  (die Leiste wird für andere gar nicht gerendert). Er zeigt — wie der Veröffentlichen-Button — immer nur das
  Symbol des Bereichs, in den er wechselt: Plus für „Titel hinzufügen", Liste für „Titel der Playlist
  auflisten". Die Element-Ids bleiben `#playlist-mode-add-button` bzw. `#playlist-mode-entries-button`, es
  existiert aber immer nur eine davon.
- Verhalten unverändert: leere Playlist startet im Hinzufügen-Bereich, sonst Titelliste; der Wechsel zum
  Hinzufügen hebt eine Titelauswahl auf; nach dem Entfernen des letzten Titels wird das Hinzufügen angeboten;
  Nur-Lese-Ansicht bekommt keinen Umschalter.
- Der Umschalter bleibt auch bei ausgewähltem Titel erreichbar (Entscheidung: er ist eine Navigation zwischen
  Bereichen, kein Bearbeiten des Titels; die Playlist-Aktionen bleiben wie bisher ausgeblendet).
- CSS: `.playlist-mode-group`/`.playlist-mode-button` entfernt; auf Bildschirmen ≤ 480px sind die nun fünf
  Symbolbuttons auf 40px verkleinert und der Abstand verringert, damit sie nebeneinander passen.

**Belege.** `shots2\11-detail-breitbild-desktop.png` und `shots4\19-detail-langer-text-mobil.png` (fünf
Buttons auf dem Bild, kein Umschalter mehr im Inhaltsbereich), `shots4\14-detail-titel-ausgewaehlt-mobil.png`.
Tests (neu, in `PlaylistDetailHeaderTests.cs`): `ModeToggle_SitsInTheHeaderActionBar_AndShowsOnlyTheButtonForTheOtherArea`,
`ModeToggle_EmptyPlaylist_StartsInAddMode_AndOffersOnlyTheWayBackToTheList`,
`ModeToggle_WithASelectedTitle_SwitchesAndClearsTheSelection`; in `PlaylistEntriesListSelectionTests.cs`
`ContentArea_HasNoModeToggleOfItsOwnAnyMore`, `SwitchingModes_ShowsExactlyOneAreaAtATime_AndReportsTheNewOne`,
`ReadOnly_NeverSwitchesToTheAddArea_OnlyTheList`. Angepasst: `PlaylistDetailInteractionE2ETests` (prüft jetzt,
dass immer genau eine der beiden Schaltflächen existiert), `PlaylistDetailPublicTests`,
`PlaylistDetailLayoutE2ETests.NarrowScreen_...` (fünf Buttons innerhalb des Bildschirms).

---

## Anpassung der gemeinsamen E2E-Hilfsklasse (Folge von Punkt 10)

`PlaylistsE2ETestBase.ShowAddModeAsync()` klickte bedingungslos `#playlist-mode-add-button`. Seit Punkt 10
existiert immer nur **eine** der beiden Schaltflächen — bei einer leeren Playlist (Hinzufügen-Bereich) also
gerade *nicht* diese. Dadurch liefen im vollständigen Testlauf neun bis zehn Playlist-E2E-Tests in eine
30-Sekunden-Zeitüberschreitung („waiting for Locator(`#playlist-mode-add-button`)"). Das war kein Flackern:
Ein Vergleichslauf des unveränderten Branches war vollständig grün (1208/1208), der geänderte hatte
reproduzierbar dieselben Ausfälle.

Behoben in `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`:

- `ShowAddModeAsync()` wartet zuerst auf „Suchfeld **oder** Hinzufügen-Schaltfläche" (die Detailseite
  entscheidet den Bereich erst nach dem Laden der ersten Seite) und klickt nur, wenn das Suchfeld fehlt.
- `ShowEntriesAsync()` wartet analog und wartet nach dem Klick auf die tatsächlich erschienene Titelliste bzw.
  den Leer-Zustand, statt sich nur auf eine feste Wartezeit zu verlassen.
- `SelectSearchResultAsync()` wartet nach der Auswahl eines Suchergebnisses auf die Statusmeldung des Servers
  (`#playlist-entries-status`), bevor es weitergeht. Ohne das rutschte ein langsamer Roundtrip gelegentlich
  durch und die Titelliste war noch leer (`AddMovie_WithSourceAccess_AppearsAccessible` fiel dadurch unter
  Last gelegentlich aus).

Alle drei Änderungen machen die Hilfsmethoden strenger, nicht schwächer; kein Test wurde deaktiviert oder
abgeschwächt.

## Akzeptierte Punkte — nicht regressiert

Umlaute, zusammengeführte Übersicht ohne Dopplung, Filterleiste mit drei Filtern, Plus-Symbol,
Datums-Abstände im Kopfbereich, Bild-Symbol-Button mit Overlay und Entfernen, Aktionsbuttons auf dem Bild,
Trennung von Auflisten und Hinzufügen, wechselndes Veröffentlichen-Symbol: alle zugehörigen Tests laufen
unverändert (`PlaylistUiUmlautTests`, `PlaylistsOverviewTests`, `PlaylistsOverviewE2ETests`,
`PlaylistDetailLayoutE2ETests`, `PlaylistDetailPublicTests`, `PlaylistDetailCoverTests`) und die Punkte sind in
`shots2\02-uebersicht-desktop.png` und `shots4\*` sichtbar erfüllt.

## Bekannte Restrisiken

- **Gekürzte Texte im Kopfbereich.** Name (2 Zeilen) und Beschreibung (3 Zeilen, mobil 2) werden mit „…"
  abgekürzt. Das ist der Preis für die feste Höhe. Der vollständige Text steht im Markup (Vorlesehilfen) und
  im Bearbeiten-Panel, ist aber im Kopfbereich nicht mehr vollständig lesbar.
- **Kopfhöhe unterhalb ~846px Bildschirmbreite** ist mit 440px höher als die Mindesthöhe der Serienseite
  (360px). Bewusste Entscheidung, siehe Punkt 7.
- **`hyphens: auto`** benötigt die Silbentrennungsmuster des Browsers für Deutsch. Chromium, Firefox und Safari
  haben sie; fehlen sie, greift weiterhin `overflow-wrap: anywhere`, der Titel bricht dann ohne Trennstrich um.
- **`-webkit-line-clamp`/`-webkit-box`** ist ein verbreiteter, aber nicht standardisierter Mechanismus. Er wird
  im Repository bereits für `.media-title-text` genutzt; fällt er aus, wächst im schlimmsten Fall der Text über
  den Kopfbereich hinaus und wird von dessen `overflow: hidden` beschnitten.
- **Kachelformat 16/9 statt 2/3.** Bewusste Abweichung vom Film-/Serienvorbild, siehe Punkt 2. Falls der Kunde
  ausdrücklich Hochformat wünscht, müssten auch die Abmessungen des erzeugten Covers
  (`PlaylistSettings.GeneratedCoverWidthPixels/HeightPixels`) geändert werden.
- **Playwright unter Last.** Die Playlist-E2E-Tests arbeiten weiterhin an mehreren Stellen mit festen
  Wartezeiten. Von drei vollständigen Läufen nach der Korrektur waren zwei grün (je 1222/1222, ~2:50); im
  dritten fielen `MediaSearchSelectorTests.EventCallback_OnMediaSelected_InvokedWithCorrectParameters` (ein
  bUnit-Test, den diese Änderung nicht berührt) und `PlaylistPlaybackE2ETests.PlaylistSkipCollectionEntriesE2ETest`
  aus; beide liefen einzeln dreimal hintereinander durch. Das ist das in `AGENTS.md` beschriebene Flackern
  unter Last, kein durch diese Änderung verursachter Fehler (der echte Fehler in der Hilfsklasse — siehe oben —
  war reproduzierbar und ist behoben). Kein Test wurde deaktiviert oder abgeschwächt.
- **Nicht reproduzierbar:** warum der Kunde die Punkte 3, 4, 5, 8 und 9 als offen führt. Falls er sie erneut
  beanstandet, sollte geklärt werden, welchen Stand seine Installation ausliefert und ob sein Browser einen
  alten `app.css` zwischenspeichert.
