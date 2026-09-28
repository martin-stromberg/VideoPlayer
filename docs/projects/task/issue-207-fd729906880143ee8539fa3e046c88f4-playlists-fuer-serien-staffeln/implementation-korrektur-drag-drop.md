# Korrektur Kundenrückmeldung: Umsortieren per Ziehen (Drag & Drop)

Kundenmeldung: In der Playlist-Detailansicht steht im manuellen Sortiermodus der Hinweis "Ziehen Sie einen
Eintrag an eine neue Position ...", aber das Ziehen bewirkt in Microsoft Edge (Chromium, Desktop) nichts;
nur die Schaltflächen "An Anfang"/"An Ende" funktionieren.

Grundlage der Untersuchung war nicht der Code, sondern das laufende Programm: Playwright gegen eine echte,
gehostete `Program`-Instanz (eigene SQLite-Datei, freier Port, wie `PlaylistsE2ETestBase`), mit echten
Maus- und Touch-Eingaben, protokollierten Browser-Ereignissen und dem Chrome DevTools Protocol.

Untersuchungsdateien und Bilder liegen außerhalb des Repositories unter:

```
C:\Users\Martin\AppData\Local\Temp\claude\D--Repositories-softwareschmiede-fd729906-8801-43ee-8539-fa3e046c88f4\
  e838bc0e-97f1-46db-9b8f-7f49b0cca26b\scratchpad\
    probe1.log … probe5.log   (Ereignisprotokolle der Untersuchung)
    screenshots\              (Sichtprüfung, 1440x900)
    fix-backup\               (Sicherung für die Gegenprobe)
```

## 1. Was die Untersuchung ergeben hat

### Was NICHT die Ursache war

Die vorgegebenen Hypothesen wurden einzeln im Browser geprüft, nicht überlegt:

- **(a) `@ondragover:preventDefault` ohne `@ondragover`-Handler registriert keinen Listener** — widerlegt.
  `DOMDebugger.getEventListeners` auf dem `document` listet die von Blazor registrierten globalen Listener
  auf; `dragover` ist dabei (`probe2.log`, Abschnitt "(2) document-Listener: change, click, click, dblclick,
  DOMContentLoaded, …, dragend, dragover, dragstart, dragstart, drop, …"). Ein auf eine Kachel geschicktes
  `dragover` wird auch tatsächlich abgebrochen (`(1) dragover defaultPrevented = True`). Der Drop wurde vom
  Browser also angenommen.
- **(b) Das `<img>` wird selbst zum Drag-Objekt** — kein Abbruch: ein am Poster begonnenes Ziehen lieferte
  `dragstart` auf dem `IMG`, blubberte zur Kachel und ordnete korrekt um (`probe2.log`, Abschnitt (4)).
- **(d) fehlendes `dragenter`** — `dragenter` wird nicht abgebrochen (`(1b) = False`), Chromium, Edge und
  Firefox fragen trotzdem `dragover` und liefern den Drop.
- **(e) CSS (`user-select`, `pointer-events`, Overlays, z-index)** — keine solche Regel vorhanden, das
  Greifen funktionierte an jeder Stelle der Kachel.
- **(h) Skript-URL mit Fingerabdruck** — `js/playlistDragDrop.js?v=00fab44ca790` war geladen und aktiv.
- **(c)/(g) Neurendern während `dragstart`** — im Versuch nicht auslösbar; das Ziehen überstand die
  Blazor-Roundtrips.

Damit lässt sich die Meldung mit einem gewöhnlichen Ziehen lokal NICHT nachstellen: Umsortieren per Ziehen
funktionierte in jedem Browser, den ich fahren kann — Chromium headless und mit Fenster, **echtes Microsoft
Edge (Kanal `msedge`) headless und mit Fenster**, Firefox — sowohl mit Playwrights Maus als auch mit rohen
CDP-Mausereignissen bei abgeschalteter Drag-Interception, also über die native Drag-Maschinerie des Browsers
(`probe3.log`, alle fünf Fälle "umsortiert"). Das ist ehrlich so zu berichten: **den Fehlschlag selbst habe
ich im Browser nicht gesehen.**

### Die belegte Ursache: der Drop hängt an einem früheren Server-Roundtrip

Die bisherige Umsetzung verteilte eine einzige Geste auf zwei voneinander unabhängige SignalR-Nachrichten:

| Ereignis | bisher |
| --- | --- |
| `dragstart` | `@ondragstart` → Server merkt sich `draggedEntry` in einem Feld der Komponente |
| `drop` | `@ondrop` → Server liest `draggedEntry` zurück; **ist es `null`, kehrt der Handler wortlos zurück** |

Die gesamte Wirkung hängt also daran, dass die erste Nachricht vor der zweiten verarbeitet wurde. Trifft das
nicht zu — langsame oder unzuverlässige Verbindung, SignalR-Long-Polling (das die Nachrichten über getrennte
HTTP-Anfragen schickt und keine Reihenfolge garantiert), eine wiederholte Nachricht, ein kurzer
Verbindungsabbruch —, dann passiert **gar nichts: keine Umsortierung, keine Fehlermeldung, kein Eintrag im
Protokoll.** Genau das beschreibt der Kunde.

Das ist im Browser nachgestellt (`probe4.log`): Im laufenden Programm wurde ausschließlich die
SignalR-Nachricht des `dragstart`-Ereignisses um vier Sekunden verzögert, alles andere blieb unverändert.
Ein danach ganz normal mit der Maus ausgeführtes Ziehen ergab:

```
vorher: 1,2,3
verzoegerte Frames: 1
nachher: 1,2,3
Statusmeldung: (keine)
ERGEBNIS: UNVERAENDERT - Drop still verworfen
```

Ob beim Kunden genau dieser Weg zuschlägt, kann ich von hier aus nicht beweisen — die Kundenumgebung ist
mir nicht zugänglich. Belegt ist: die bisherige Konstruktion hat einen Zustand, in dem das Ziehen ohne jede
Rückmeldung wirkungslos ist, und dieser Zustand passt genau auf die Meldung. Hinzu kommen zwei weitere
Schwächen derselben Konstruktion, die unabhängig davon feststehen: natives Drag & Drop reagiert **gar nicht
auf Touch-Eingaben**, und es gab **keinerlei optische Rückmeldung** während des Ziehens (weder gedämpfte
Quelle noch markiertes Ziel), so dass ein danebengegangener Drop — etwa in den Abstand zwischen zwei Kacheln,
wo nichts passiert — für den Anwender nicht von einem defekten Programm zu unterscheiden ist.

## 2. Die Änderung

Natives HTML5-Drag-&-Drop ist ersetzt durch eine Zieh-Geste über Pointer-Ereignisse
(`VideoWebPlayer/wwwroot/js/playlistDragDrop.js`, vollständig neu). Die Geste läuft im Browser ab; der Server
wird genau einmal gerufen, beim Loslassen, mit beiden Ids.

- **Razor** (`VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor`): `draggable`, `@ondragstart`,
  `@ondragover:preventDefault`, `@ondrop`, `@ondragend` und das Serverfeld `draggedEntry` entfallen. Neu:
  die Klasse `playlist-entry-reorderable` auf ziehbaren Kacheln, `draggable="false"` am Poster (Bilder sind
  von Haus aus nativ ziehbar und hätten die Geste abgebrochen), `UpdateReorderHandlersAsync` meldet die
  Liste in `OnAfterRenderAsync` beim Modul an bzw. ab, und `ReorderEntryByDropAsync(gezogeneId, zielId)`
  ist `[JSInvokable]`.
- **Serverseitige Semantik unverändert**: `ReorderEntryByDropAsync` schlägt beide Ids in `allEntries` nach
  und ruft wie bisher `MoveEntryBetweenAsync` mit `NewSortOrder` = `SortOrder` der Zielkachel. An der API,
  am `PlaylistEntryReorderService` und an der Datenbank wurde nichts geändert.
- **Berechtigungen**: Das Modul wird nur angemeldet, wenn `IsReorderable` gilt (Besitzer UND manueller
  Modus) und die Titelliste sichtbar ist; nur Kacheln mit `playlist-entry-reorderable` sind greifbar. Der
  Server weist Umordnungen fremder Benutzer bzw. im Datumsmodus unabhängig davon weiterhin ab.
- **Bedienung**: Maus/Stift ab 6 px Bewegung; Finger nach 400 ms Halten (ein einfaches Wischen scrollt
  weiterhin die Seite, das Scrollen wird erst während des Ziehens unterdrückt). Escape und `pointercancel`
  brechen ab. Am Fensterrand scrollt die Liste selbsttätig weiter (schmaler Streifen von 40 px, Tempo nach
  Randnähe), damit auch entfernte Positionen erreichbar sind — auch bei seitenweise nachgeladenen Listen,
  weil das Ziel bei jedem Scrollschritt neu über `document.elementFromPoint` bestimmt wird.
- **Rückmeldung**: gezogene Kachel gedämpft (`playlist-entry-dragging`), Zielkachel mit farbigem Rahmen,
  leichtem Farbton und Einfügelinie an der Kante, an der der Titel landet (`playlist-entry-drop-target`
  plus `-before`/`-after`); Greifhand-Cursor auf ziehbaren Kacheln.
- **Keine Nebenwirkungen auf Klick/Doppelklick**: Der auf das Loslassen folgende `click` wird in der
  Capture-Phase verschluckt (und ein `dblclick` innerhalb von 400 ms nach einem Ziehen ebenso), damit ein
  Ziehen weder die Auswahl ändert noch die Wiedergabe startet. Ein gewöhnlicher Klick wählt weiterhin aus,
  ein Doppelklick spielt weiterhin ab, die Schaltflächen "An Anfang"/"An Ende" sind unangetastet (ein
  `pointerdown` auf einer Schaltfläche startet keine Geste).
- **Hinweistext** nennt jetzt die Kachel als Ziel und das Halten bei Fingerbedienung.

## 3. Tests

Neu: `VideoWebPlayer.Tests/PlaylistDragDropReorderE2ETests.cs` (Trait E2E), 11 Tests, alle mit **echten
Eingaben** (`Page.Mouse.Down/Move/Up` in zwölf Schritten mit Pausen, bzw. echte Touch-Ereignisse über CDP) —
keine per JS erzeugten `DragEvent`s mehr:

1. Ziehen auf die dritte Kachel ordnet um und bleibt nach `Reload` erhalten
2. Ziehen nach vorn (letzte Kachel auf die erste)
3. Ziehen am Poster-Bild begonnen
4. Ziehen am Titeltext begonnen
5. Während des Ziehens sind Quelle und Ziel markiert
6. Ziehen löst keine Auswahl und keine Wiedergabe aus
7. Ein Klick nach einem Ziehen wählt weiterhin aus
8. Ziehen funktioniert, wenn die erste Nachricht der Geste fünf Sekunden zu spät beim Server ankommt
9. Ziehen mit dem Finger nach kurzem Halten
10. Datumsmodus: Ziehen ändert die Reihenfolge nicht (und es gibt keinen Hinweistext)
11. Fremde öffentliche Playlist: Ziehen ändert die Reihenfolge nicht

Der bisherige synthetische Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder` ist entfallen; seine
Abdeckung (Umordnen + Bestehenbleiben nach Reload) steckt vollständig in Test 1, nun mit echter Maus.
`PlaylistReorderE2ETests` behält Sortiermoduswechsel und Schnellaktionen; der gemeinsame Aufbau
(Playlist mit drei Filmen im manuellen Modus) liegt jetzt als `SetupManualPlaylistWithThreeEntriesAsync` in
`PlaylistsE2ETestBase`.

**Gegenprobe (Fix zurückgenommen):** Mit der alten Fassung von `PlaylistEntriesList.razor` und
`playlistDragDrop.js` (aus `0e6ef59`) und den neuen Tests: **3 Tests rot** — Test 8 (verzögerte erste
Nachricht, also genau die gemeldete Wirkung), Test 9 (Finger) und Test 5 (optische Rückmeldung); die
übrigen 8 blieben grün. Das ist erwartbar und wichtig zu wissen: Playwright fährt natives Drag & Drop in
Chromium über eine eigene Emulation (`Input.setInterceptDrags`), die den Drop in jedem Fall zustellt — ein
reiner "Ziehen funktioniert"-Test kann den gemeldeten Fehler deshalb prinzipiell nicht sehen. Genau deshalb
prüft Test 8 die Ursache (Abhängigkeit von einer früheren Nachricht) statt nur das Ergebnis.

Der zusätzlich eingeführte JS-Code wird durch diese Tests im echten Browser gefahren (Geste, Schwellwerte,
Zielbestimmung, Markierungen, Klick-Unterdrückung, Berechtigungsfälle); ein separater JS-Unittest hätte nur
dieselbe Logik ohne Browser nachgebildet und wurde deshalb nicht angelegt.

bUnit-Komponententests: `PlaylistDetailPublicTests` und `PlaylistEntriesListSelectionTests` melden jetzt die
beiden JS-Aufrufe an; die Bedienelement-Prüfung fragt statt `draggable=true` die Klasse
`playlist-entry-reorderable` ab.

**Ergebnisse:** `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei; die vollständige Suite
`dotnet test VideoWebPlayer.Tests` ist grün (1241 Tests). Die neue Testklasse wurde zusätzlich dreimal
hintereinander vollständig ausgeführt — 3 × 11 grün, kein Flackern. Im ersten vollständigen Lauf war
`PlaylistPlaybackE2ETests.PlaylistPlaybackStartTest` einmal rot (Zeitüberschreitung beim Warten auf eine
Suchergebnis-Kachel, bekanntes Playwright-Flackern unter Last); in den folgenden vollständigen Läufen war
er ohne Änderung grün. Strikte Hooks (`razor-usage-check.py`, `enum-coverage-check.py`,
`no-notimplemented-check.py`, jeweils `--all --strict`) sowie `translation-check.py`,
`csproj-xmldoc-check.py` und `razor-l10n-check.py` laufen durch.

## 4. Sichtprüfung

Desktop 1440x900, Bilder im Scratchpad (nicht im Repository):

| Bild | Inhalt |
| --- | --- |
| `screenshots\01-vor-dem-ziehen.png` | Ausgangszustand, Reihenfolge 1,2,3 |
| `screenshots\02-ziehen-ueber-zweiter-kachel.png` | laufendes Ziehen: Kachel 1 gedämpft, Kachel 2 als Ziel markiert (Rahmen, Farbton, Einfügelinie rechts) |
| `screenshots\03-ziehen-ueber-zielkachel.png` | laufendes Ziehen über der Zielkachel 3 |
| `screenshots\04-nach-dem-loslassen.png` | nach dem Loslassen: Reihenfolge 2,3,1 |
| `screenshots\reihenfolge.txt` | `vorher: 1,2,3` / `nachher: 2,3,1` |

## 5. Dokumentation

- `docs/help/playlists.md`, Abschnitt "Manuelle Sortierung": beschreibt das Ziehen auf die Zielkachel, die
  optische Rückmeldung, das selbsttätige Scrollen am Rand, Escape und ausdrücklich die Fingerbedienung
  (kurz halten, dann ziehen; Wischen scrollt weiter).
- `docs/help/playlists-ablauf-technisch.md`: neuer Abschnitt "Umordnen per Ziehen" mit dem technischen
  Ablauf und der Begründung, warum kein natives Drag & Drop mehr verwendet wird.
- `docs/RELEASE_NOTES.md`: je ein Eintrag unter "What's New" und "Neuerungen".

## 6. Restrisiken

- **Der gemeldete Fehlschlag ist von mir nicht direkt beobachtet worden.** Belegt ist der Mechanismus, der
  genau diese Wirkung erzeugt (verzögerte/vertauschte erste Nachricht), und er ist jetzt konstruktiv
  ausgeschlossen. Sollte der Kunde weiterhin nichts bewegen können, wäre der nächste Schritt, in seinem
  Browser die Konsole und `window.playlistEntryReorder` zu prüfen (ist das Skript geladen?) sowie den
  Verbindungstyp von SignalR (WebSocket oder Long-Polling).
- **Browser:** geprüft mit Chromium (headless und mit Fenster), echtem Microsoft Edge (headless und mit
  Fenster) und Firefox headless, jeweils mit echten Eingaben; die automatisierten Tests laufen gegen
  Chromium. Safari/WebKit wurde nicht geprüft — Pointer Events sind dort seit Jahren vorhanden, ein Rest
  bleibt.
- **Touch:** mit emulierten Touch-Ereignissen im Chromium nachgewiesen (Test 9), nicht auf einem echten
  Mobilgerät. Die Haltezeit von 400 ms ist ein Kompromiss zwischen Scrollen und Ziehen und könnte auf
  echten Geräten noch nachgestellt werden müssen.
- **Sehr lange Listen:** das Ziel wird bei jedem Scrollschritt neu bestimmt, das selbsttätige Scrollen am
  Rand löst auch das Nachladen weiterer Seiten aus. Eine Zielposition, die erst nach mehreren nachgeladenen
  Seiten kommt, ist damit zwar erreichbar, aber mühsam; "An Anfang"/"An Ende" bleiben für solche Sprünge
  der bequemere Weg (so steht es auch in der Hilfe).
- **Tastatur:** unverändert keine Tastaturalternative zum Ziehen (nicht verlangt); die Schaltflächen
  "An Anfang"/"An Ende" sind weiterhin die tastaturbedienbare Möglichkeit.

## 7. Nachbesserung nach Abnahme

Grundlage: `acceptance-korrektur-drag-drop.md` (Prüfstand `80a5585`), Gesamturteil "Abweichungen gefunden",
acht Punkte. Der Prüfer hat dabei etwas gefunden, das mir entgangen war und das die wahrscheinlichste
Erklärung der Kundenmeldung ist: **die alte Fassung wirkte nur, wenn exakt über einer Kachel losgelassen
wurde.** Die Kacheln stehen in einem mehrspaltigen Raster mit 1 rem Abstand; ein Loslassen in der Lücke,
neben der Liste oder unter der letzten Reihe tat wortlos nichts — in Chromium und Edge nachgestellt. Der
alte Hinweistext ("an eine neue Position") legte genau das nahe. Diese Lücke ist jetzt geschlossen (A4).

Belege der Nachbesserung, Punkt für Punkt:

| Punkt | Behebung | Beleg |
| --- | --- | --- |
| **A4** Loslassen neben der Kachel | `findDropTarget` nimmt die Kachel unter dem Zeiger und, wenn dort keine ist, die nächstgelegene — solange der Zeiger nicht weiter als eine Kachelhöhe (80–340 px) neben der Liste steht. Die Markierung wandert dabei über die Lücken mit. Weiter weg gibt es kein Ziel, und das Loslassen erzeugt einen sichtbaren Hinweis statt wortlos nichts zu tun. | `E2E_DropInGapBetweenTiles_MovesEntryToNearestTile`, `E2E_DropFarOutsideTheList_KeepsOrder_AndShowsHint` (beide ohne die Korrektur rot); Bilder `06-ziehen-in-die-luecke.png`, `07-nach-drop-in-die-luecke.png`, `08-hinweis-weit-daneben.png` |
| **A1** unbelegte Ursache als Tatsache | Release Notes (beide Sprachabschnitte), `playlists.md`, `playlists-ablauf-technisch.md`, der Kopfkommentar von `playlistDragDrop.js` und die Testbeschreibung sagen jetzt "nachgestellt … ob der Melder genau das erlebt hat, ist nicht belegt" und nennen beide Kandidaten; "wieder zuverlässig" ist gestrichen. | `git show` der Doku-Änderung; `grep` auf "wieder zuverlässig"/"genau das vom Kunden" liefert nichts mehr |
| **A2** abgelehnte Zusage des Serveraufrufs | `sendReorder` wertet die Zusage aus (`then`/`catch`): bei Ablehnung `console.warn` **und** sichtbarer Hinweis "Die neue Reihenfolge konnte nicht gespeichert werden. Bitte laden Sie die Seite neu."; der synchrone `try/catch` bleibt zusätzlich. | Code; der Hinweis nutzt denselben Weg wie in `08-hinweis-weit-daneben.png` gezeigt |
| **A9** Fingerbedienung auf Handy-Größe | Zwei echte Fehler gefunden: (1) die Schrittweite hing an der Taktzahl des `setInterval`, das unter Last statt alle 16 ms nur alle ~90 ms lief; jetzt wird in px/s gerechnet und mit der vergangenen Zeit multipliziert. (2) **Bootstrap setzt auf `:root` ein `scroll-behavior: smooth`** — jedes `scrollBy` startete eine Animation, die der nächste Takt abbrach; gemessen blieben von ~1700 px/s nur ~150 px/s übrig. Jetzt `scrollTo({ behavior: "instant" })`. Randstreifen bei Finger/Stift 120 px statt 56 px, Geschwindigkeit 400–2000 px/s je nach Randnähe. | `E2E_DragWithTouch_OnPhoneScreen_AutoScrollsToATileBelowTheFold` (390x844, echte Touch-Ereignisse) misst die Scrollstrecke: ohne die Korrektur **51 px in 700 ms** (deckt sich mit den ~70 px/s des Prüfers), mit ihr über 1000 px, und der Titel landet auf der herangescrollten Kachel |
| **A3** stille Rückgaben serverseitig | `MoveEntryToTargetPositionAsync` meldet jetzt jeden Abbruchgrund: nicht (mehr) sortierbar bzw. Eintrag/Ziel nicht mehr geladen ⇒ `Logger.LogWarning` + Statusmeldung + Neuladen der Liste. | Code; das Neuladen ist zusammen mit dem Zusatzpunkt getestet |
| **Zusatz** Neuladen nach gescheitertem Serveraufruf | Nach einem Fehler wird die Liste neu geladen (die Fehlermeldung bleibt stehen). | `E2E_ServerRefusesReorder_ShowsError_AndReloadsList`: der gezogene Eintrag wird während der gedrückten Maustaste aus der Datenbank entfernt; danach erscheint "Fehler beim Umordnen …" und die Liste zeigt nur noch die verbliebenen zwei Kacheln (ohne die Korrektur rot) |
| **A6** Escape und der folgende Klick | `cancelGesture` setzt die Klick-Unterdrückung auch beim Abbruch; sie gilt bis zum nächsten Klick (längstens 2 s) statt nur bis zum nächsten Ereigniszyklus. | `E2E_EscapeDuringDrag_KeepsOrder_AndDoesNotSelectEntry` — zieht innerhalb derselben Kachel (nur so trifft der Klick danach diese Kachel), ohne die Korrektur rot |
| **A7** überlappende Drops | Im Browser beginnt keine neue Geste, solange ein Serveraufruf läuft; die Komponente weist einen trotzdem eintreffenden zweiten Aufruf ab (`isReordering`, mit Protokolleintrag). | Code |
| **A8** Ziel- und Auswahlfarbe | Die Zielkachel ist jetzt in der Zweitfarbe (`--vp-secondary`, blau) statt im Rot der Auswahl. | Bild `05-ziel-und-auswahl-unterscheidbar.png`: die ausgewählte Kachel ist gleichzeitig Ziel und trotzdem eindeutig zu lesen |
| **A5** Testlücken | Neue Tests mit echter Eingabe: Doppelklick spielt ab, Loslassen in der Lücke, Loslassen weit daneben, Escape-dann-Loslassen, Serverfehler mit Neuladen, Fingerbedienung auf Handy-Größe. | siehe unten |

### Tests nach der Nachbesserung

`PlaylistDragDropReorderE2ETests` hat jetzt 13 Tests (neu: Doppelklick spielt ab; Fingerbedienung auf
390x844 mit Auto-Scroll zu einer nicht sichtbaren Kachel). Neu ist die Klasse
`PlaylistDragDropFeedbackE2ETests` (4 Tests) für alles, was schiefgehen kann: Lücke, weit daneben, Escape,
Serverfehler. Die gemeinsamen Zieh-Helfer (`DragEntryOntoEntryAsync`, `BeginEntryDragAsync`,
`ReadEntryOrderAsync`, `RequireBoundingBoxAsync`, `DispatchTouchAsync`, `EntryRowSelector`) liegen jetzt in
`PlaylistsE2ETestBase`, damit beide Klassen sie nutzen können; neu dort auch
`RemoveEntryFromDatabaseAsync` für den Serverfehler-Test.

**Gegenprobe gegen den Stand VOR der Nachbesserung** (`f70ed54`, nur `PlaylistEntriesList.razor`,
`playlistDragDrop.js` und `app.css` zurückgenommen, Tests unverändert):

```
Fehler E2E_DropInGapBetweenTiles_MovesEntryToNearestTile
Fehler E2E_DropFarOutsideTheList_KeepsOrder_AndShowsHint
Fehler E2E_EscapeDuringDrag_KeepsOrder_AndDoesNotSelectEntry
Fehler E2E_ServerRefusesReorder_ShowsError_AndReloadsList
Fehler E2E_DragWithTouch_OnPhoneScreen_AutoScrollsToATileBelowTheFold
   (Das automatische Scrollen war zu langsam: nur 51 px in 700 ms.)
```

Fünf der sechs neuen Tests unterscheiden also "nachgebessert" von "nicht nachgebessert". Der sechste
(`E2E_DoubleClickOnEntry_StartsPlayback`) ist bewusst ein reiner Regressionsschutz: er sichert ab, dass die
Klick-Unterdrückung den Doppelklick nicht mitverschluckt, und läuft in beiden Ständen grün.

**Builds und Testläufe:** `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei; strikte Hooks
(`razor-usage-check.py`, `enum-coverage-check.py`, `no-notimplemented-check.py`, je `--all --strict`) sowie
die pre-commit-Hooks laufen durch. Die beiden Zieh-Testklassen (17 Tests) liefen dreimal hintereinander
vollständig grün.

Bei der vollständigen Suite (jetzt 1247 Tests) bin ich zur Ehrlichkeit verpflichtet: **fünf von sechs
Durchläufen hatten je genau einen Fehlschlag, und zwar jedes Mal einen anderen, mit dem Ziehen nicht
verwandten Test**:

| Durchlauf | Fehlschlag | einzeln wiederholt |
| --- | --- | --- |
| 1 | `PlaylistPlaybackE2ETests.PlaylistPreviousEntryE2ETest` | grün |
| 2 | `PlaylistMediaSearchE2ETests.SelectMovieCollection_AddsCollection` | grün |
| 3 | `PlaylistBackfillSignalTests.NotifyDuringAScan_WakesOnlyWhenTheScanEnds` | grün |
| 4 | `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` | grün |
| 5 | keiner - 1247/1247 grün | — |
| 6 | `Components.MediaSearchSelectorTests.HttpCall_ItemsEndpoint_ReceivesCorrectUrl` | grün (2 s) |

Alle sind zeit- bzw. wartebasiert (Playwright-Zeitüberschreitungen, ein Signal-Test mit Wartezeiten, im
letzten Fall sogar ein reiner bUnit-Test ohne Browser und ohne Server), und der Rechner trägt parallel einen
zweiten Agenten in einem anderen Arbeitsverzeichnis. Kein Test wurde deaktiviert oder abgeschwächt. Ein
Zusammenhang mit dieser Änderung ist nicht erkennbar - keiner der sechs berührt das Ziehen, und der
bUnit-Fall kann es gar nicht -, mit letzter Sicherheit ausgeschlossen ist er nicht. Wer nachprüft, sollte die
Suite mehrfach laufen lassen und auf dieses Muster achten; die beiden Zieh-Testklassen selbst waren in jedem
Lauf grün, auch dreimal hintereinander allein ausgeführt.

### Neue Bilder (Scratchpad, nicht im Repository)

| Bild | Inhalt |
| --- | --- |
| `screenshots\05-ziel-und-auswahl-unterscheidbar.png` | die ausgewählte Kachel (rot) ist gleichzeitig Ziel (blau mit Einfügelinie) |
| `screenshots\06-ziehen-in-die-luecke.png` | Zeiger in der Lücke zwischen zwei Kacheln, die nächstliegende ist markiert |
| `screenshots\07-nach-drop-in-die-luecke.png` | Ergebnis nach dem Loslassen in der Lücke |
| `screenshots\08-hinweis-weit-daneben.png` | Hinweis "Nicht umsortiert: Lassen Sie den Titel auf der Kachel der gewünschten Position los." |

### Was nach der Nachbesserung offen bleibt

- **Die Kundenursache bleibt unbelegt.** Es gibt jetzt zwei nachgestellte Kandidaten (Roundtrip-Abhängigkeit,
  Loslassen neben der Kachel); beide sind ausgeschlossen. Der Prüfer hält den zweiten für wahrscheinlicher,
  ich teile diese Einschätzung nach seinem Nachweis.
- **Zwei-Tab-Betrieb:** ändert ein zweiter Tab die Reihenfolge, arbeitet der erste bis zum nächsten Laden mit
  veralteten Sortierwerten; das Ergebnis kann dann von der erwarteten Position abweichen, ohne dass ein
  Fehler entsteht. Die Liste wird nach jedem Umordnen neu geladen und zeigt danach den tatsächlichen Stand.
  Eine echte Absicherung bräuchte eine Server-Prüfung (z. B. erwartete Position mitsenden) und damit eine
  API-Änderung, die hier ausdrücklich nicht vorgenommen werden sollte.
- **Nicht mit echter Eingabe gesehen:** Firefox und WebKit/Safari (die Nachbesserung ist nur in Chromium
  gefahren; die erste Runde war zusätzlich in echtem Edge und Firefox geprüft), echte Mobilgeräte, echte
  Stift-Eingabe. Die Fingerbedienung ist ausschließlich mit Touch-Emulation belegt.
- **Der Hinweis am unteren Rand** ist bewusst eine reine Browser-Anzeige (kein Blazor-Element), damit er auch
  ohne Serververbindung erscheint. Er verschwindet nach fünf Sekunden von selbst und ist nicht anklickbar.
