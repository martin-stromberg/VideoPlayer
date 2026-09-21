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
