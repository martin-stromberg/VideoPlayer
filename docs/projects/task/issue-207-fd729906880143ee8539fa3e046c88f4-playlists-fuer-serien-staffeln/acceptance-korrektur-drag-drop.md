# Abnahme: Korrektur Kundenrückmeldung "Umsortieren per Ziehen (Drag & Drop)"

Geprüfter Stand: `80a5585` (Branch
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-drag-drop-integration`),
Vergleichsstand vor der Änderung: `8422360`.
Prüfer: Claude (Softwareschmiede Bot) — nicht der Implementierer. Eigener Worktree, eigene SQLite-Dateien,
eigene freie Ports. Der Selbstbericht (`implementation-korrektur-drag-drop.md`) wurde ausschließlich als
Behauptung behandelt; alles Folgende stammt aus eigenen Läufen mit echten Eingaben.

Belege (Protokolle, Bilder) liegen außerhalb des Repositories unter
`C:\Users\Martin\AppData\Local\Temp\claude\D--Repositories-softwareschmiede-fd729906-8801-43ee-8539-fa3e046c88f4\e838bc0e-97f1-46db-9b8f-7f49b0cca26b\scratchpad\`:

| Datei | Inhalt |
| --- | --- |
| `probe-old.log` | eigene Ursachensuche an der ALTEN Fassung (`8422360`), zweiter Worktree, Chromium **und echtes Edge**, jeweils mit Fenster, echte Maus |
| `probe-new-maus.log` | Abnahme der neuen Geste, Chromium und echtes Edge mit Fenster, echte Maus, 12 Szenarien |
| `probe-new-langeliste.log` | 45 Einträge, seitenweises Nachladen, Auto-Scroll während des Ziehens |
| `probe-new-stoerungen.log` | verzögerte erste Nachricht, Netzdrosselung, Verbindungsabbruch, Serverfehler, Datumsmodus-Wechsel, zweiter Tab |
| `probe-new-touch.log`, `probe-new-touch2.log` | Touch-Emulation 390x844 (`IsMobile`, `HasTouch`, DPR 3) |
| `probe-new-dblclick.log` | Klärung Doppelklick-Wiedergabe und Klick-Unterdrückung |
| `abnahme-screenshots\` | Bildschirmfotos, u. a. `chromium-02-waehrend-ziehen.png`, `langeliste-autoscroll.png`, `stoerung-verbindungsabbruch.png`, `touch2-waehrend-ziehen.png` |

Die Sonden (`ZzProbe*.cs`) und die Bilder wurden **nicht** ins Repository übernommen.

## Ergebnis

**Status:** Abweichungen gefunden

Die Umsetzung ist fachlich gut und in der Sache deutlich besser als der Vorzustand. Mit echter Maus habe ich
in Chromium **und in echtem Microsoft Edge** bestätigt: Ziehen nach hinten und nach vorn wirkt, die neue
Reihenfolge überlebt einen Reload, Klick wählt aus, Doppelklick spielt ab, Escape bricht ab, die
Schnellaktionen sind unberührt, es gibt erstmals optische Rückmeldung, das Ziehen überlebt eine um 5 s
verzögerte erste SignalR-Nachricht und eine stark gedrosselte Verbindung, Auto-Scroll erreicht Ziele
außerhalb des Sichtbereichs, und Serverfehler erzeugen jetzt eine sichtbare Meldung. Die serverseitige
Zugriffskontrolle ist unverändert und bleibt vollständig abgesichert. `dotnet build -c Release` und die
vollständige Testsuite (1244 Tests) sind grün, ohne Flackern.

Freigabe empfehle ich trotzdem noch nicht. Es bleiben acht Abweichungen; die drei wichtigsten sind:
die Release Notes stellen dem Kunden eine **nicht belegte Ursache als Tatsache** dar (A1), der einzige
Serveraufruf der Geste ist **nicht gegen eine abgelehnte Zusage abgesichert** (A2), und auf dem
Handy-Bildschirm schlägt die Fingergeste in der naheliegenden Bedienung **wortlos fehl** (A9), obwohl Hilfe
und Release Notes sie zusagen.

## Abweichungen

- [ ] **A1 — wichtig (Ehrlichkeit gegenüber dem Kunden): Release Notes und Hilfe stellen eine unbewiesene
  Ursache als Tatsache dar.**
  `docs/RELEASE_NOTES.md`: „Das Umsortieren der Playlist-Einträge per Ziehen funktioniert **wieder
  zuverlässig** … [natives Drag & Drop] bewirkte stillschweigend gar nichts, wenn der erste [Roundtrip]
  verspätet ankam — das Ziehen wirkte dann wirkungslos". Gleichlautend in
  `docs/help/playlists-ablauf-technisch.md` („gemeldete Kundenrückmeldung: …") und im Kopfkommentar von
  `VideoWebPlayer/wwwroot/js/playlistDragDrop.js` („genau das vom Kunden gemeldete Verhalten").
  Der Implementierer schreibt in seinem eigenen Bericht: „**den Fehlschlag selbst habe ich im Browser nicht
  gesehen**". Auch ich konnte ihn nicht reproduzieren (siehe „Eigene Ursachensuche"). Damit wird eine
  Ursache-Wirkung-Beziehung zugesichert, für die kein Nachweis existiert. Tritt der Fehler beim Kunden
  erneut auf, ist der Schaden doppelt.
  *Empfehlung:* auf das Belegte umformulieren, z. B.: „Das Umsortieren per Ziehen wurde neu gebaut. Die
  Geste läuft jetzt vollständig im Browser und wird beim Loslassen genau einmal an den Server gemeldet;
  damit entfällt eine Konstruktion, in der ein Ziehen ohne jede Rückmeldung wirkungslos bleiben konnte.
  Neu sind außerdem die optische Rückmeldung während des Ziehens und die Fingerbedienung." Ohne „wieder"
  und ohne „zuverlässig".

- [ ] **A2 — wichtig (Technik): der einzige Serveraufruf der Geste ist nicht gegen eine abgelehnte Zusage
  abgesichert.** `VideoWebPlayer/wwwroot/js/playlistDragDrop.js`, `onPointerUp`:

  ```js
  try {
      dotNetReference.invokeMethodAsync("ReorderEntryByDropAsync", sourceId, targetId);
  } catch (error) {
      // Die Komponente ist bereits verschwunden (Seitenwechsel waehrend des Ziehens).
  }
  ```

  `invokeMethodAsync` liefert ein Promise; der `try/catch` fängt nur synchrone Fehler. Genau der im
  Kommentar genannte Fall (Komponente weg, Circuit beendet, `JSDisconnectedException`, verworfene
  `DotNetObjectReference`) führt zu einer **abgelehnten Zusage ohne `catch`** — unbehandelte Ablehnung in
  der Konsole, für den Anwender passiert wortlos nichts. Das ist dieselbe Wirkungsklasse, die die Korrektur
  beseitigen sollte, nur an neuer Stelle. Der Kommentar behauptet eine Absicherung, die der Code nicht
  leistet.
  *Empfehlung:* `.catch(...)` anhängen, dort mindestens `console.warn` und eine sichtbare Rückmeldung.

- [ ] **A3 — mittel (Technik): serverseitig bleiben stille Rückgaben.**
  `PlaylistEntriesList.MoveEntryToTargetPositionAsync`:

  ```csharp
  if (!IsReorderable || draggedEntryId == targetEntryId)
      return;
  var entryToMove = allEntries.FirstOrDefault(e => e.Id == draggedEntryId);
  var targetEntry  = allEntries.FirstOrDefault(e => e.Id == targetEntryId);
  if (entryToMove is null || targetEntry is null)
      return;
  ```

  Wird eine der Ids nicht gefunden (Liste zwischen Aufnehmen und Loslassen neu geladen), passiert wortlos
  nichts — ohne Meldung und ohne Protokolleintrag. Für eine Korrektur mit dem erklärten Ziel „nie wieder
  wortlos nichts" sollte hier mindestens ein `Logger.LogWarning` und eine Statusmeldung stehen.

- [ ] **A4 — mittel (Bedienung): Loslassen neben einer Kachel bleibt wortlos wirkungslos.**
  Eigener Nachweis mit echter Maus, Chromium **und** Edge (`probe-new-maus.log`, N5/N6): Loslassen in der
  16 px breiten Lücke zwischen zwei Kacheln oder außerhalb der Liste ⇒ Reihenfolge unverändert,
  Statusmeldung „(keine)". Die Kacheln liegen in einem mehrspaltigen Raster
  (`.episode-list { display: grid; … gap: 1rem }`, bei 1440 px drei Spalten), es gibt also reale
  Trefferlücken. In der neuen Fassung ist das entschärft (die fehlende Ziel-Markierung ist sichtbar), es
  bleibt aber genau die Situation, die ein Anwender als „Ziehen funktioniert nicht" erlebt — und sie ist
  mein Hauptverdacht für die ursprüngliche Kundenmeldung (siehe unten).
  *Empfehlung:* beim Loslassen ohne Ziel die zuletzt markierte Kachel verwenden oder eine kurze
  Hinweismeldung zeigen.

- [ ] **A9 — wichtig (Bedienung, mobil): auf Handy-Größe schlägt die Fingergeste in der naheliegenden
  Bedienung wortlos fehl.** Eigener Nachweis, Chromium-Handy-Kontext 390x844, `IsMobile`, `HasTouch`,
  DPR 3 (`probe-new-touch2.log`):

  ```
  Quelle y=530 h=316, Ziel y=861 h=316, Viewporthoehe 844, scrollY 129
  nach 900 ms Halten: dragging=True
  waehrend des Ziehens: Ziel drop-target=False, scrollY 181 (vorher 129)
  nachher: 1,2,3,4 (vorher 1,2,3,4)
  ```

  Die Geste **startet** korrekt (Kachel wird gedämpft, Seite scrollt nicht unter dem Finger weg) — das ist
  positiv und gegenüber vorher ein echter Gewinn. Aber eine Kachel ist auf dem Handy 316 px hoch, die
  **nächste** Kachel beginnt bereits unterhalb des Sichtbereichs. Wer „kurz halten, dann auf die
  Nachbarkachel ziehen" tut, wie Hilfe und Release Notes es beschreiben, hat gar kein sichtbares Ziel; das
  automatische Scrollen am Rand ist mit ~70 px/s zu langsam, um in einer normalen Geste eine Nachbarkachel
  heranzuholen (52 px in 0,7 s). Ergebnis: keine Umsortierung, **keine Meldung**. Im ersten Touch-Lauf
  (`probe-new-touch.log`, T3) trat dasselbe auf.
  *Empfehlung:* Entweder das automatische Scrollen für Touch deutlich beschleunigen bzw. den Randstreifen
  verbreitern, oder in Hilfe und Release Notes ehrlich sagen, dass auf schmalen Bildschirmen „An
  Anfang"/„An Ende" der praktikable Weg ist. In jedem Fall A4 beheben, damit der Fehlversuch nicht stumm
  bleibt.

- [ ] **A5 — mittel (Testlücke): „Doppelklick spielt weiterhin ab" ist durch keinen Test abgesichert.**
  Ich habe es selbst nachgewiesen (`probe-new-dblclick.log`): im manuellen Modus mit angemeldetem
  Zieh-Modul startet der Doppelklick die Wiedergabe (`?entryId=1`, Videoelement vorhanden), und 150 ms nach
  einem Ziehen wird er — wie beabsichtigt — unterdrückt. Die neue `onDoubleClickCapture`-Unterdrückung
  greift damit in ein Verhalten ein, für das es **keinen positiven Test** gibt: die vorhandenen Tests
  prüfen nur, dass ein Doppelklick auf einen *gesperrten* Eintrag nichts tut und dass ein *Ziehen* nichts
  abspielt.
  *Empfehlung:* E2E-Test „Doppelklick auf eine abspielbare Kachel startet die Wiedergabe" ergänzen.

- [ ] **A6 — klein (Bedienung): Escape bricht ab, das anschließende Loslassen wählt den Eintrag aus.**
  `cancelGesture()` setzt `gesture = null`; `onPointerUp` steigt danach sofort aus und setzt
  `suppressNextClick` **nicht**, der Browser-`click` geht durch und löst `SelectEntryAsync` aus. Die
  Reihenfolge bleibt korrekt unverändert (`probe-new-maus.log`, N4), aber nach einem ausdrücklichen Abbruch
  wechselt der Kopfbereich auf den Titel.
  *Empfehlung:* in `cancelGesture()` bei laufendem `dragging` ebenfalls `suppressNextClick = true` setzen.

- [ ] **A7 — klein (Technik): kein Schutz gegen überlappende Drops und gegen veraltete Zielpositionen.**
  `ReorderEntryByDropAsync` kennt kein „läuft schon". Zwei Gesten kurz hintereinander lösen zwei
  überlappende `MoveEntryBetweenAsync` + `LoadInitialPageAsync` aus; die zweite liest die Zielposition
  gegebenenfalls aus dem noch alten `allEntries`. In meinem Lauf kam das richtige Ergebnis heraus
  (`probe-new-maus.log`, N12), bei langsamer Verbindung ist eine falsche Endreihenfolge ohne Fehlermeldung
  möglich. Dieselbe Klasse zeigte sich im Zwei-Tab-Versuch (`probe-new-stoerungen.log`, F6): der zweite Tab
  ordnete um, der erste zog danach mit einem **veralteten** `SortOrder` des Ziels — hier zufällig ohne
  sichtbaren Schaden.
  *Empfehlung:* ein `isReordering`-Flag (analog `loadPageSemaphore`) und im JS während eines laufenden
  Serveraufrufs keine neue Geste starten.

- [ ] **A8 — klein (Optik): Zielmarkierung und Auswahl sind farblich identisch.**
  `.playlist-entry-row.playlist-entry-drop-target` setzt exakt dieselben Werte wie `.episode-box.selected`
  (`border-color: var(--vp-primary)`, `background: rgba(229, 9, 20, 0.1)`). Zieht man auf die gerade
  ausgewählte Kachel, unterscheidet sie nur die 4 px breite Einfügelinie.

## 1. Eigene Ursachensuche (ohne die These des Implementierers zu übernehmen)

Ich habe die alte Fassung (`8422360`) in einem zweiten Worktree lauffähig gemacht und mit **echten
Mauseingaben** in Chromium **und echtem Microsoft Edge (Kanal `msedge`), jeweils mit Fenster**, unter
Bedingungen gefahren, die der Implementierer nicht abgedeckt hat (`probe-old.log`).

| Szenario (alte Fassung, 1440x900) | Chromium | Edge |
| --- | --- | --- |
| S1 gewöhnliches Ziehen Kachel 1 → 3 | umsortiert | umsortiert |
| S2 **Loslassen in der Lücke zwischen zwei Kacheln (16 px)** | **unverändert, keine Meldung** | **unverändert, keine Meldung** |
| S3 Kachel zuerst anklicken (Auswahl ⇒ Neurender von `PlaylistDetail` inkl. Kopfbild), dann ziehen | umsortiert | umsortiert |
| S4 Ziehen, während der Auswahl-Roundtrip noch läuft | umsortiert | umsortiert |
| S5 Zoom 80 % | umsortiert | umsortiert |
| S6 30 Einträge, Ziehen an den unteren Fensterrand (natives Auto-Scroll) | umsortiert, `scrollY` 393 | umsortiert, `scrollY` 393 |
| S7 Loslassen über der Schaltfläche „An Anfang" der Zielkachel | umsortiert | umsortiert |
| S8 Loslassen auf dem Poster der Zielkachel | umsortiert | umsortiert |

**Was ich damit ausschließen kann** (jedenfalls in einer von mir fahrbaren Umgebung): Neurendern durch die
`@onclick`-Auswahl, ein laufender Auswahl-Roundtrip, `role="option"`/`tabindex`, Poster mit
`loading="lazy"`, `draggable`-Kollision am Bild, Zoom ≠ 100 %, Listen länger als der Viewport, und ein
allgemeiner Edge-Unterschied. Auch **veraltete Skripte im Cache scheiden aus**: das alte
`playlistDragDrop.js` enthielt ausschließlich den `dataTransfer.setData`-Aufruf, den nur Firefox braucht —
in Edge hätte selbst ein komplett fehlendes Skript das Ziehen nicht verhindert.

**Meine eigene, vom Implementierer unabhängige These (deterministisch nachgestellt, S2):**
Die alte Fassung wirkte **ausschließlich beim Loslassen exakt über einer anderen Kachel** und gab dabei
**keinerlei optische Rückmeldung** — weder gedämpfte Quelle noch markiertes Ziel. Der Hinweistext lautete
„Ziehen Sie einen Eintrag **an eine neue Position**", was ein Anwender natürlicherweise als „zwischen zwei
Einträge fallen lassen" liest (das übliche Muster mit Einfügelinie). Die Kacheln liegen in einem
mehrspaltigen Raster mit 1 rem Abstand; jeder Drop in diese Lücken, auf den Hinweistext, neben die Liste
oder unter die letzte Kachelreihe tat wortlos nichts. Wer das zwei-, dreimal erlebt, berichtet exakt:
„Drag & Drop funktioniert nicht, nur die Schaltflächen wirken."

Die vom Implementierer gefundene Ursache (zwei unabhängige SignalR-Nachrichten; geht `dragstart` verloren
oder kommt sie zu spät, verwirft `drop` wortlos) ist ein **realer Konstruktionsfehler**; ich habe ihn im
Code nachvollzogen und über die Gegenprobe bestätigt (siehe Abschnitt 4). Ob er beim Kunden zuschlug, ist
**nicht belegt**.

**Die Kundenursache bleibt ungeklärt.** Beide Kandidaten sind durch die Änderung entschärft: die
Roundtrip-Abhängigkeit konstruktiv, das „danebengezogen"-Problem durch die neue Ziel-Markierung und den
korrigierten Hinweistext („Ziehen Sie einen Eintrag **auf die Kachel der gewünschten Position**") — wobei
A4 zeigt, dass der Fehlversuch selbst weiterhin stumm bleibt.

**Was der Kunde tun sollte, falls es weiterhin nicht geht** (gehört so in die Rückmeldung an ihn):

1. In Edge `F12` → „Konsole", Detailseite neu laden, einen Zieh-Versuch machen, rote Meldungen melden.
2. In der Konsole `window.playlistEntryReorder` eingeben — kommt `undefined`, ist das Skript nicht geladen
   (Proxy, Erweiterung, Tracking-Schutz, Cache).
3. Reiter „Netzwerk", Filter „WS": läuft die Blazor-Verbindung als **WebSocket** (`_blazor?id=…`,
   Status 101) oder ist sie auf **Long-Polling** zurückgefallen?
4. Edge-Version melden (`edge://version`) und einmal im InPrivate-Fenster ohne Erweiterungen testen.
5. Beschreiben, **wo genau** losgelassen wird: direkt auf einer anderen Kachel oder im Zwischenraum.

## 2. Abnahme der neuen Lösung mit echten Eingaben

Eigene Läufe mit `Page.Mouse.Down/Move/Up` in 14 Schritten mit Pausen, Browser **mit Fenster** (nicht
headless), Viewport 1440x900 (`probe-new-maus.log`).

| Prüfpunkt | Chromium | Edge (`msedge`) |
| --- | --- | --- |
| N1 Ziehen nach hinten (1 → 3) | erfüllt | erfüllt |
| N1b Reihenfolge nach `Reload` persistiert | erfüllt | erfüllt |
| N2 Ziehen nach vorn (letzte auf die erste Kachel) | erfüllt | erfüllt |
| N3 kurzer Klick wählt aus (Kopfbereich zeigt Titel) | erfüllt | erfüllt |
| N4 Escape bricht ab, Reihenfolge unverändert | erfüllt (aber A6) | erfüllt (aber A6) |
| N5 Loslassen außerhalb der Liste | unverändert, **ohne Meldung** (A4) | dito |
| N6 Loslassen in der Lücke zwischen zwei Kacheln | unverändert, **ohne Meldung** (A4) | dito |
| N7 „An Anfang" / „An Ende" | erfüllt | erfüllt |
| N8 Tastatur: Enter wählt aus, Escape hebt auf | erfüllt | erfüllt |
| N9 Doppelklick spielt ab | erfüllt (belegt in `probe-new-dblclick.log`) | erfüllt |
| N10 sichtbare Rückmeldung (Bild angesehen) | erfüllt | erfüllt |
| N11 Zoom 80 % | erfüllt | erfüllt |
| N12 zwei Drops kurz hintereinander | Ergebnis korrekt (aber A7) | Ergebnis korrekt |

**Firefox:** Der Firefox-Durchgang meiner Sonde brach bereits im Aufbau (Anmeldung/Playlist anlegen) mit
einer Zeitüberschreitung ab (`probe-new-maus.log`, letzte Zeile: `[firefox] ABBRUCH: Timeout 30000ms
exceeded.`). Das ist mit hoher Wahrscheinlichkeit ein Problem meiner Sonde und nicht des Programms — **ich
kann Firefox deshalb nicht als geprüft melden.**

**Optische Rückmeldung — tatsächlich angesehen:** `abnahme-screenshots\chromium-02-waehrend-ziehen.png`
zeigt die gezogene Kachel („Film 001") gedämpft, die Zielkachel („Film 003") mit rötlichem Farbton und
durchgehender roter Einfügelinie an der **rechten** Kante (der Titel wandert nach hinten). Der Hinweistext
darüber lautet korrekt und eindeutig: „Tipp: Ziehen Sie einen Eintrag auf die Kachel der gewünschten
Position (mit dem Finger: kurz gedrückt halten, dann ziehen), oder nutzen Sie die Schaltflächen ‚An
Anfang'/‚An Ende'."

**Lange Liste, Auto-Scroll, Nachladen** (`probe-new-langeliste.log`, Edge, 45 Einträge): erste Seite 20
Kacheln, nach zweimaligem Scrollen 45 Kacheln nachgeladen; Kachel 1 aufgenommen und am unteren Fensterrand
gehalten ⇒ die Seite scrollte selbsttätig auf `scrollY` 657 und der Titel landete auf Position 12.
Bild: `abnahme-screenshots\langeliste-autoscroll.png` (Zielkachel am unteren Rand markiert). Nach dem
Umordnen zeigt die Liste wieder nur die erste Seite (20 Kacheln) — erwartbar, weil `LoadInitialPageAsync`
neu lädt.

**Datumsmodus und fremde öffentliche Playlist** habe ich nicht in einer eigenen Sonde gefahren, sondern über
die mitgelieferten E2E-Tests `E2E_DateSortMode_DraggingDoesNotChangeOrder` und
`E2E_ForeignPublicPlaylist_DraggingDoesNotChangeOrder` abgenommen; beide arbeiten mit echter Maus und waren
in meinem vollständigen Testlauf grün. Ergänzend habe ich die serverseitige Abweisung selbst nachgestellt
(F5 unten) und die Endpunkt-Tests ausgeführt (siehe Abschnitt 4).

### Umgebungsstörungen (`probe-new-stoerungen.log`, echtes Edge)

| Fall | Ergebnis |
| --- | --- |
| **F1** erste Nachricht der Geste 5 s verzögert (`WebSocket.send` abgefangen) | umsortiert (1,2,3 → 2,3,1) — die gemeldete Wirkung tritt **nicht** mehr auf |
| **F2** CDP-Drosselung 800 ms Latenz, 50/20 kbit/s | umsortiert (4,5,6 → 5,6,4), keine Fehlermeldung |
| **F3** Verbindung während des Ziehens offline, dann Loslassen | **keine** Reconnect-Anzeige, **keine** Meldung, Reihenfolge zunächst unverändert; **nach** der Wiederverbindung wurde die Umsortierung nachgeholt (7,8,9 → 8,9,7). Kein Datenverlust, aber mehrere Sekunden ohne jede Rückmeldung (Bild `stoerung-verbindungsabbruch.png`) |
| **F4** Eintrag zwischen Aufnehmen und Loslassen serverseitig gelöscht | **sichtbare Meldung**: „Fehler beim Umordnen: Playlist-Eintrag wurde nicht gefunden." Die Liste wird danach allerdings **nicht** neu geladen, der gelöschte Eintrag bleibt als Kachel stehen |
| **F5** Playlist zwischenzeitlich serverseitig auf Datumsmodus | **sichtbare Meldung**: „Fehler beim Umordnen: Playlist befindet sich nicht im manuellen Sortiermodus." (serverseitige Durchsetzung belegt) |
| **F6** zweiter Tab auf derselben Playlist | Ziehen im zweiten Tab wirkt; der erste Tab arbeitet danach mit einem veralteten `SortOrder` des Ziels (siehe A7) |

**Antwort auf die Kernfrage „gibt es weiterhin einen Weg, dass ein normales Ziehen wortlos nichts bewirkt?"**
Ja, drei: Loslassen neben einer Kachel (A4), Fingerbedienung auf Handy-Größe (A9) und die unbehandelte
Promise-Ablehnung (A2). Bei einem **fehlgeschlagenen Serveraufruf** dagegen erscheint jetzt eine Meldung
(F4/F5) — das ist gegenüber vorher ein echter Fortschritt; nur das konsistente Neuladen fehlt (F4).

## 3. Mobil / Touch

Chromium-Handy-Kontext 390x844, `IsMobile`, `HasTouch`, DPR 3, echte Touch-Ereignisse über CDP
(`probe-new-touch.log`, `probe-new-touch2.log`):

- **Antippen wählt aus** — erfüllt.
- **Senkrechtes Wischen scrollt weiterhin die Seite** — erfüllt (`scrollY` 0 → 235), kein Scroll-Blocker.
- **Langes Halten löst die Kachel** — erfüllt: nach 900 ms trägt die Kachel `playlist-entry-dragging`.
- **Ziehen ordnet um** — **nicht erfüllt in der naheliegenden Bedienung**, siehe A9: die Nachbarkachel liegt
  auf Handy-Größe bereits außerhalb des Sichtbereichs, das automatische Scrollen ist zu langsam, und der
  Fehlversuch bleibt stumm.
- Kein Text-Auswahl-Popup und kein Kontextmenü im Weg (`user-select: none` auf den Kacheln greift);
  während des Ziehens scrollte die Seite nicht unter dem Finger weg (nur das beabsichtigte Rand-Scrollen).

## 4. Code-Review (`git diff 8422360..HEAD`)

**JS-Lebenszyklus** — sauber gelöst und nachvollzogen:

- `attach(element, reference)` ist gegen Mehrfachaufrufe geschützt (`listElement === element` ⇒ nur die
  Referenz wird ersetzt), eine andere Liste löst die bisherige über `detach()` ab. **Keine**
  Doppelregistrierung bei Neurender.
- `detach()` entfernt jeden der neun Listener wieder und bricht eine laufende Geste ab; kein Listener-Leck.
- `UpdateReorderHandlersAsync` meldet exakt bei Wechsel von `shouldAttach` an bzw. ab (Besitzer UND
  manueller Modus UND sichtbare Titelliste UND nicht leer). Der Wechsel in den Bereich „Titel hinzufügen"
  und zurück wird korrekt abgedeckt, weil das Listen-`div` dabei neu entsteht und `attach` es als anderes
  Element erkennt.
- `DisposeAsync` meldet zuerst ab und gibt **danach** die `DotNetObjectReference` frei — Reihenfolge stimmt.
- `InvalidOperationException` / `JSDisconnectedException` / `JSException` werden in
  `UpdateReorderHandlersAsync`, `DisconnectObserverAsync` und `DisposeAsync` abgefangen.
- Die `DotNetObjectReference` ist aus der Konsole nicht greifbar: `scroll.js` hält sie in einer Closure,
  `playlistDragDrop.js` in einer Modulvariablen. Im Lesemodus gelangt sie gar nicht erst ins JS.
- **Ausnahme: A2** (unbehandelte Promise-Ablehnung beim einzigen Serveraufruf).

**Sicherheit / Zugriffskontrolle** — unverändert und weiterhin serverseitig erzwungen:
`PlaylistService.MoveEntryBetweenAsync` → `GetOwnedPlaylistAsync` (Besitzer),
`PlaylistEntryReorderService.EnsureManualSortMode` (Datumsmodus ⇒ 409), Eintragssuche mit
`e.PlaylistId == playlist.Id` (fremde Eintrags-Id ⇒ 404), `targetSortOrder < 0` ⇒ 400. Zusätzlich schlägt
die Komponente **beide** Ids in `allEntries` nach; gesendet wird immer der `SortOrder` des serverseitig
geladenen Ziel-Eintrags, aus dem Browser lässt sich also **keine** beliebige Zielposition einschleusen. Das
Ausblenden der Greif-Affordanz ist korrekt nur Darstellung. Ein direkter Aufruf der Server-Methode mit
fremden Ids war über die Oberfläche nicht möglich (Objektreferenz nicht erreichbar); die Endpunkt-Ebene ist
durch `PlaylistsControllerTests_Reorder` (403 / 409 / 404 / 400 für `MoveEntryBetween`) und
`PlaylistServiceTests_PublicAccessMatrix` abgedeckt — beide habe ich selbst ausgeführt, und F5 belegt die
Durchsetzung zusätzlich im laufenden Programm.

**Eingabevalidierung** — `Number(...)` + `Number.isFinite`, Gleichheitsfall abgefangen, `long` serverseitig.
In Ordnung.

**Doppel-Drops / Race** — siehe A7.

**Tests — Gegenprobe selbst nachgefahren.** Ich habe die 11 neuen E2E-Tests unverändert in den zweiten
Worktree mit der **alten** Fassung kopiert und dort ausgeführt:

```
Fehler VideoWebPlayer.Tests.PlaylistDragDropReorderE2ETests.E2E_DragWithMouse_FirstMessageDelayed_StillReordersEntry
Fehler VideoWebPlayer.Tests.PlaylistDragDropReorderE2ETests.E2E_DragWithTouch_AfterLongPress_ReordersEntry
Fehler VideoWebPlayer.Tests.PlaylistDragDropReorderE2ETests.E2E_WhileDragging_SourceAndTargetTileAreMarked
Fehler!  : Fehler: 3, erfolgreich: 8, gesamt: 11, Dauer: 2 m 40 s
```

Die Angabe des Implementierers („3 Tests rot", und zwar genau diese drei) ist damit **bestätigt**.

*Bewertung, ob die Tests die Kundenwirkung wirklich absichern:* **nur eingeschränkt.** 8 der 11 Tests
laufen auch mit der alten, unveränderten Fassung grün — sie unterscheiden „repariert" nicht von „nicht
repariert". Abgesichert sind faktisch drei Dinge: die verzögerte erste Nachricht (ein Mechanismus, der als
Kundenursache **nicht belegt** ist), die Fingerbedienung und die optische Rückmeldung. Die von mir als
wahrscheinlichste Kundenursache vermutete Wirkung (Loslassen neben der Kachel, A4) ist durch **keinen**
Test abgedeckt, ebenso wenig der Doppelklick (A5). Der Hinweis des Implementierers, dass Playwright natives
Drag & Drop über `Input.setInterceptDrags` emuliert und ein reiner „Ziehen funktioniert"-Test den Fehler
deshalb nicht sehen kann, ist zutreffend — meine eigenen Läufe mit echter Maus in beiden Browsern zeigen
aber, dass das native Ziehen auch ohne diese Emulation funktionierte.

Kein Test ist deaktiviert oder abgeschwächt; der entfallene synthetische `DragEvent`-Test ist zu Recht
entfallen (er rief die Blazor-Handler direkt auf und hätte den Fehler prinzipiell nie sehen können).

**Hinweistext in der Oberfläche** — korrekt, eindeutig, mit Fingerbedienung ergänzt. Gut, und genau die
Formulierung, die meine eigene Ursachenthese adressiert.

**Dokumentation** — `docs/help/playlists.md` und `docs/help/playlists-ablauf-technisch.md` beschreiben den
Ablauf zutreffend. Inhaltliche Beanstandungen: die als Tatsache dargestellte Ursache (A1) und die ohne
Einschränkung zugesagte Fingerbedienung (A9).

## 5. Build und Tests (selbst ausgeführt)

- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler** (245 Warnungen, sämtlich vorbestehend: xUnit1051,
  xUnit1026).
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (vollständig, inkl. E2E):
  **1244 Tests, 0 Fehler, 0 übersprungen, Dauer 3 min 25 s.** Kein einziger Test flackerte in diesem Lauf;
  eine Einzelwiederholung war deshalb nicht erforderlich. (Der vom Implementierer genannte einmalige
  Ausfall von `PlaylistPlaybackE2ETests.PlaylistPlaybackStartTest` trat bei mir nicht auf.)

## Hinweise

- **Die Kundenursache ist weiterhin nicht belegt** — weder vom Implementierer noch von mir. Beide
  Erklärungen (Roundtrip-Abhängigkeit; Loslassen neben der Kachel ohne jede Rückmeldung) sind plausibel,
  beide sind durch die Änderung entschärft. Das gehört in dieser Klarheit in die Antwort an den Kunden
  (A1), zusammen mit der oben genannten Eingrenzungsliste.
- **Empfohlene Reihenfolge der Nachbesserung:** A1 (Formulierung), A2 (`catch` am Serveraufruf), A4
  (Rückmeldung bei Drop ohne Ziel) und A9 (mobil ehrlich beschreiben oder Auto-Scroll beschleunigen) vor
  einer Freigabe; A3, A5, A6, A7, A8 können danach folgen.
- **Nicht geprüft und offen:** Firefox (Sonde brach im Aufbau ab), Safari/WebKit, echte Mobilgeräte,
  echte Stift-Eingabe.
- Alle Sonden, Protokolle und Bilder liegen ausschließlich im Scratchpad-Ordner; im Repository wurde außer
  diesem Bericht nichts angelegt oder verändert.

---

# Nachprüfung (Stand `aed99a1`)

Geprüfter Stand: `aed99a1` auf
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-drag-drop-integration`
(`1aa92e8` Korrektur, `ece4586` Tests, `53d2461` Doku). Vergleichsstand der Gegenprobe: `f70ed54` für die
drei Produktivdateien bei unveränderten Tests.
Prüfer: Claude (Softwareschmiede Bot), erneut unabhängig, eigener Worktree, eigene SQLite-Dateien, eigene
freie Ports. Der Nachbesserungsbericht des Implementierers wurde wieder nur als Behauptung behandelt.

Belege dieser Runde (Scratchpad, nicht im Repository):

| Datei | Inhalt |
| --- | --- |
| `recheck-maus.log` | 11 Szenarien mit echter Maus in **Chromium, echtem Microsoft Edge und Firefox**, jeweils mit Fenster, 1440x900 |
| `recheck-fehler.log` | Verbindungsabbruch, Serverfehler, Datumsmodus-Wechsel während der Geste |
| `recheck-a2.log` | erzwungene Ablehnung des Serveraufrufs (A2) und deren Folgen |
| `recheck-touch.log` | Handy 390x844 (`IsMobile`, `HasTouch`, DPR 3), echte Touch-Ereignisse über CDP |
| `recheck-dblclick.log` | Doppelklick startet die Wiedergabe |
| `nachpruefung-screenshots\` | u. a. `T3-touch-autoscroll.png`, `chromium-R4-weit-daneben.png`, `E2-serverfehler.png`, `*-R7-ziel-und-auswahl.png` |

## Ergebnis der Nachprüfung

**Status:** Abweichungen gefunden (deutlich reduziert) — **Freigabe: noch nicht, erst nach Behebung von N1.**

Sieben der acht Abweichungen der ersten Runde sind behoben, und zwar belegt mit echter Eingabe in drei
Browsern. Die Nachbesserung geht über das Verlangte hinaus: bei A9 hat der Implementierer zwei echte
Ursachen gefunden — die Schrittweite hing an der Taktzahl statt an der vergangenen Zeit, und **Bootstrap
setzt auf `:root` ein `scroll-behavior: smooth`**, wodurch jedes `scrollBy` eine Animation startete, die der
nächste Takt sofort abbrach. Beides habe ich nachvollzogen (`grep` auf `bootstrap.min.css` liefert
`scroll-behavior:smooth`), und die gemessene Scrollstrecke stieg von 51 px auf **1532 px in ~2 s**.

Offen bleibt ein Teil von A2, der sich beim schärferen Nachstellen als nur halb behoben erwiesen hat und
zusätzlich eine neue Nebenwirkung trägt (N1).

## Punkt für Punkt

### A4 — Loslassen neben der Kachel: **behoben**

Echte Maus, alle drei Browser (`recheck-maus.log`):

| Szenario | Chromium | Edge | Firefox |
| --- | --- | --- | --- |
| R2 Loslassen in der 16-px-Lücke zwischen zwei Kacheln | Nachbarkachel markiert, umsortiert | dito | dito |
| R4 Loslassen weit daneben (15,40) | nichts markiert, Reihenfolge unverändert, **Hinweis sichtbar** | dito | dito |

Der Hinweis lautet wörtlich „Nicht umsortiert: Lassen Sie den Titel auf der Kachel der gewünschten Position
los." (Bild `chromium-R4-weit-daneben.png`). Die Zielmarkierung wandert über die Lücken mit: in R2 war bei
Zeiger in der Lücke die nächstliegende Kachel markiert, und das Loslassen setzte den Titel genau dorthin
(z. B. `4,5,6 -> 5,4,6`).

*Beobachtung (kein Mangel, aber erwähnenswert):* Die Toleranz ist eine Kachelhöhe (80–340 px), auf dem
Schreibtisch rund 172 px. In R4b habe ich 40 px **oberhalb** der Liste losgelassen (auf dem Hinweistext):
dort wird die nächstliegende Kachel genommen und umsortiert — in allen drei Browsern. Wer einen Titel nach
oben aus der Liste herauszieht, um „doch nicht" zu sagen, ordnet also um. Der dokumentierte Abbruch ist
Escape, und der wirkt (R5). Die Hilfe spricht von „knapp neben einer Kachel"; eine ganze Kachelhöhe ist
großzügiger, als das klingt.

*Nicht verwertbar:* Mein Szenario R3 („60 px neben der rechten Listenkante") liegt bei 1440 px Breite
außerhalb des Sichtfensters (Listenkante ≈ 1415 px). Chromium und Edge ordneten trotzdem um, Firefox tat
nichts. Das ist ein Fehler meiner Sonde, kein Befund; aussagekräftig sind R2 und R4.

### A1 — Ehrlichkeit der Kundendokumentation: **behoben**

`docs/RELEASE_NOTES.md` (beide Sprachabschnitte), `docs/help/playlists.md`,
`docs/help/playlists-ablauf-technisch.md` und der Kopfkommentar von `playlistDragDrop.js` sagen jetzt
übereinstimmend, der Mechanismus sei im Browser **nachgestellt**, „**ob der Melder genau das erlebt hat, ist
nicht belegt**", und nennen ausdrücklich beide Kandidaten (Roundtrip-Abhängigkeit und Loslassen neben der
Kachel). „funktioniert wieder zuverlässig" ist gestrichen, die deutschen Release Notes beginnen mit „wurde
neu gebaut". Auch die Einschränkung für schmale Bildschirme steht dort. Geprüft durch Lesen des
vollständigen Doku-Diffs.

### A2 — abgelehnte Zusage des Serveraufrufs: **nur teilweise behoben** → N1

Der Code ist richtig: `sendReorder` hängt `then`/`catch` an die Zusage und meldet im Fehlerfall über
`console.warn` **und** einen sichtbaren Hinweis. Zwei Versuche mit echter Eingabe:

- **Verbindung hart geschlossen** (alle WebSockets während der Geste geschlossen, dann losgelassen —
  `recheck-fehler.log`, E1): Der Aufruf ging **nicht** verloren. SignalR verband neu und holte ihn nach,
  die Reihenfolge war danach korrekt (1,2,3 → 2,3,1). Kein Hinweis nötig, keiner erschienen. Gutes Verhalten.
- **Senden schlägt fehl** (`WebSocket.prototype.send` wirft im Moment des Loslassens — `recheck-a2.log`):
  Die Umsortierung ist **verloren** (1,2,3 vorher wie nachher, auch nach einem Neuladen), und es erscheint
  **kein Hinweis und keine Konsolenmeldung**. Die Zusage von `invokeMethodAsync` wird in diesem Fall
  offenbar weder erfüllt noch abgelehnt — sie bleibt schwebend, der `catch` läuft nie.

### N1 (NEU, mittel) — nach einem ins Leere gelaufenen Aufruf ist das Ziehen dauerhaft und wortlos tot

Reproduktion (`recheck-a2.log`, echte Maus, echtes Edge):

1. Playlist mit drei Titeln im manuellen Sortiermodus öffnen.
2. Eine Kachel aufnehmen und über eine andere ziehen.
3. Kurz vor dem Loslassen schlägt das Senden über die Blazor-Verbindung fehl (im Versuch erzwungen; im
   Betrieb entsteht derselbe Zustand, wenn der Circuit während des laufenden Aufrufs endet).
4. Loslassen ⇒ keine Umsortierung, **keine Meldung**.
5. Verbindung wieder in Ordnung, ganz normal eine andere Kachel ziehen:

```
zweites Ziehen NACH dem ins Leere gelaufenen Aufruf: vorher 1,2,3 nachher 1,2,3 KEINE Wirkung (Ziehen blockiert)
```

Ursache im Code: `sendReorder` setzt `reorderInFlight = true` und setzt es nur in `then`/`catch` zurück.
Bleibt die Zusage schwebend, bleibt das Flag gesetzt, und `onPointerDown` steigt bei **jeder** weiteren
Geste sofort aus (`if (!listElement || gesture || reorderInFlight) return;`). Nur ein Neuladen der Seite —
oder ein Bereichswechsel, der `detach()` auslöst — hilft. Das ist genau die Wirkungsklasse, die diese
Korrektur beseitigen soll („Ziehen bewirkt nichts, nur die Schaltflächen wirken"), nur mit einer
Vorbedingung.

*Empfehlung:* `reorderInFlight` mit einer Zeitgrenze versehen (ein `setTimeout`, der es nach einigen
Sekunden zurücksetzt und dann `showHint(...)` zeigt), damit ein nie beantworteter Aufruf die Bedienung nicht
dauerhaft lahmlegt; zusätzlich das Flag in `attach()` zurücksetzen, nicht nur in `detach()`.

### A9 — Fingerbedienung auf Handy-Größe: **behoben**

Chromium-Handy-Kontext 390x844, `IsMobile`, `HasTouch`, DPR 3, echte Touch-Ereignisse (`recheck-touch.log`):

```
T1 Antippen waehlt aus: OK
T2 Wischen scrollt: scrollY=235 OK
T3 Quelle y=530 h=316, Viewport 844, scrollY 129
T3 Halten+Ziehen: dragging nach 700ms=True, Scrollstrecke 1532 px in ~2,0 s, markiert='5'
T3 vorher 1,2,3,4,5 nachher 2,3,4,5,1 / Position des gezogenen Titels 4
```

Die Kachel „5" lag beim Aufnehmen weit unterhalb des Sichtbereichs und wurde durch das automatische Scrollen
herangeholt; der Titel landete auf ihrer Position. In der ersten Runde waren es 51 px in 700 ms und es
geschah nichts. Das Bild `T3-touch-autoscroll.png` habe ich angesehen: die Zielkachel ist blau umrandet,
blau hinterlegt und trägt die Einfügelinie an der rechten Kante. Senkrechtes Wischen scrollt weiterhin (T2),
kein Scroll-Blocker, kein Auswahl-Popup.

Die Dokumentation sagt jetzt nur Belegtes zu: `playlists.md` erklärt ausdrücklich, dass auf schmalen
Bildschirmen die Nachbarkachel außerhalb des Sichtbereichs liegt, man den Finger an den Rand zieht und
wartet, und dass „An Anfang"/„An Ende" dort für größere Sprünge bequemer sind. Gleiches in den Release Notes.

### A3 und Neuladen nach einem Fehler: **behoben**

Echte Maus, echtes Edge (`recheck-fehler.log`):

- **E2** Eintrag während der gedrückten Maustaste serverseitig gelöscht, dann losgelassen:
  Statusmeldung „Fehler beim Umordnen: Playlist-Eintrag wurde nicht gefunden." **und** die Liste zeigt danach
  nur noch die verbliebenen **zwei** Kacheln — der gelöschte Eintrag bleibt nicht stehen (Bild
  `E2-serverfehler.png`). In der ersten Runde blieben es drei.
- **E3** Playlist während der Geste serverseitig auf Datumsmodus gestellt: „Fehler beim Umordnen: Playlist
  befindet sich nicht im manuellen Sortiermodus.", Reihenfolge unverändert.

Im Code meldet `MoveEntryToTargetPositionAsync` jetzt jeden Abbruchgrund mit `Logger.LogWarning`,
Statusmeldung und Neuladen. Stille `return`s gibt es nur noch für `draggedEntryId == targetEntryId`
(harmlos) und den `SortOrder is null`-Fall, der weiterhin eine Meldung zeigt, aber nicht neu lädt
(Invariantenverletzung, vernachlässigbar).

### A6 — Escape und der folgende Klick: **behoben**

R5 in allen drei Browsern: Reihenfolge unverändert **und** `Auswahl nach Loslassen=False`. Im Code setzt
`cancelGesture` die Klick-Unterdrückung jetzt auch beim Abbruch, und sie gilt bis zum nächsten Klick
(längstens 2 s) statt nur bis zum nächsten Ereigniszyklus.

### A7 — überlappende Drops: **behoben**

Doppelte Absicherung: `reorderInFlight` im Browser verhindert den Beginn einer neuen Geste, `isReordering` in
der Komponente weist einen dennoch eintreffenden zweiten Aufruf mit Protokolleintrag ab. R9 („zwei Gesten
sofort hintereinander") ergab in allen drei Browsern ein widerspruchsfreies Ergebnis. Kehrseite desselben
Flags ist N1.

### A8 — Ziel- und Auswahlfarbe: **behoben**

R7 in allen drei Browsern, gemessen am berechneten Stil derselben Kachel, die gleichzeitig ausgewählt und
Ziel ist: Auswahl `rgb(229, 9, 20)` (rot), Ziel `rgb(0, 168, 232)` (blau). In den Bildern
`*-R7-ziel-und-auswahl.png` und `T3-touch-autoscroll.png` deutlich unterscheidbar.

### A5 — Testlücken: **wesentlich verbessert**

Eigene Gegenprobe: zweiter Worktree auf `aed99a1`, darin nur `PlaylistEntriesList.razor`,
`playlistDragDrop.js` und `app.css` auf `f70ed54` zurückgesetzt, Tests unverändert:

```
Fehler PlaylistDragDropFeedbackE2ETests.E2E_EscapeDuringDrag_KeepsOrder_AndDoesNotSelectEntry
Fehler PlaylistDragDropFeedbackE2ETests.E2E_DropInGapBetweenTiles_MovesEntryToNearestTile
Fehler PlaylistDragDropFeedbackE2ETests.E2E_DropFarOutsideTheList_KeepsOrder_AndShowsHint
Fehler PlaylistDragDropFeedbackE2ETests.E2E_ServerRefusesReorder_ShowsError_AndReloadsList
Fehler PlaylistDragDropReorderE2ETests.E2E_DragWithTouch_OnPhoneScreen_AutoScrollsToATileBelowTheFold
Fehler!  : Fehler: 5, erfolgreich: 12, gesamt: 17, Dauer: 3 m 8 s
```

Die Angabe des Implementierers ist damit **bestätigt**: genau diese fünf Tests unterscheiden „nachgebessert"
von „nicht nachgebessert" — Lücke, weit daneben, Escape, Serverfehler mit Neuladen und Handy-Auto-Scroll.
Die drei vom Auftrag ausdrücklich verlangten Gegenproben (Lücke, Escape, Auto-Scroll) sind darunter. Der
sechste neue Test (`E2E_DoubleClickOnEntry_StartsPlayback`) ist in beiden Ständen grün; der
Implementierungsbericht bezeichnet ihn ausdrücklich als reinen Regressionsschutz — sachlich richtig
beschrieben.

Verglichen mit der ersten Runde (3 von 11 unterscheidend, und diese nur für den unbelegten
Roundtrip-Mechanismus) sichern die Tests die **Kundenwirkung** jetzt tatsächlich ab: der von mir als
wahrscheinlichste Ursache vermutete Fall (Loslassen neben der Kachel) hat zwei eigene Tests. Nicht
abgesichert bleibt N1.

### Regressionen: **keine gefunden**

Alles mit echter Eingabe (`recheck-maus.log`, `recheck-dblclick.log`, `recheck-touch.log`):

| Prüfpunkt | Ergebnis |
| --- | --- |
| R1 Ziehen 1 → 3 | erfüllt in Chromium, Edge und Firefox |
| R6 Klick wählt aus (Kopfbereich zeigt Titel) | erfüllt in allen drei Browsern |
| Doppelklick spielt ab | erfüllt — URL wird `…?entryId=1`, Videoelement vorhanden |
| R8 „An Anfang" / „An Ende" | erfüllt in allen drei Browsern |
| R10 Datumsmodus: kein Hinweistext, kein Ziehen | erfüllt in allen drei Browsern |
| T2 Wischen scrollt auf Touch | erfüllt |
| Nicht-Besitzer einer öffentlichen Playlist | meine Sonde scheiterte am Abmelden (Sondenfehler); abgedeckt durch `E2E_ForeignPublicPlaylist_DraggingDoesNotChangeOrder`, in meinen vollständigen Läufen grün |

**Firefox nachgeholt:** In der ersten Runde brach meine Sonde im Aufbau ab. Diesmal lief Firefox vollständig
durch und verhielt sich in R1, R2, R4, R4b sowie R5 bis R10 wie Chromium und Edge.

## Build und Tests der Nachprüfung (selbst ausgeführt)

- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler** (245 Warnungen, sämtlich vorbestehend).
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (vollständig, inkl. E2E):
  - **Lauf 1: 1250 Tests, 0 Fehler, 3 min 54 s.**
  - **Lauf 2: 1 Fehler von 1250, 3 min 51 s** — `PlaylistEntriesE2ETests.AddMovie_Duplicate_ShowsSuccessMessage`
    (Zeitüberschreitung, nichts mit dem Ziehen zu tun). **Einzeln wiederholt: grün in 14 s.**
    Das deckt sich mit dem vom Implementierer offen berichteten Muster (fünf von sechs Läufen je genau ein
    anderer, zeit-/wartebasierter Ausfall). Die beiden Zieh-Testklassen waren in beiden Läufen grün. Ein
    Zusammenhang mit dieser Änderung ist nicht erkennbar; die Flackerneigung der Suite unter Last sollte
    aber unabhängig davon angegangen werden.
- Gegenprobe (zweiter Worktree, Korrektur zurückgenommen): 5 von 17 rot, siehe oben.

## Verbleibende Mängel nach der Nachbesserung

- [ ] **N1 (mittel, neu):** Bleibt die Zusage des Serveraufrufs schwebend (Senden schlägt fehl, Circuit endet
  mitten im Aufruf), gibt es **keine Meldung**, und `reorderInFlight` blockiert danach **jedes weitere Ziehen
  im selben Tab wortlos** — bis die Seite neu geladen wird. Reproduktion und Empfehlung oben.
- [ ] **A4-Rest (klein):** Die Trefferzone reicht eine ganze Kachelhöhe über die Liste hinaus; ein Titel, den
  man nach oben aus der Liste zieht, wird umsortiert statt verworfen. Die Hilfe spricht von „knapp neben
  einer Kachel". Entweder die Toleranz oberhalb/unterhalb der Liste kleiner wählen oder die Hilfe präzisieren.
- [ ] **Zwei-Tab-Betrieb (klein, unverändert):** Ändert ein zweiter Tab die Reihenfolge, rechnet der erste bis
  zum nächsten Laden mit veralteten Sortierwerten. Der Implementierer nennt das offen; eine echte Absicherung
  bräuchte eine API-Änderung und war hier nicht Auftrag.
- [ ] **Flackernde Tests (klein, außerhalb dieser Änderung):** Die Suite hat unter Last eine reale
  Flackerneigung (bei mir 1 von 2 Läufen, beim Implementierer 5 von 6). Kein Zieh-Test war betroffen.

## Nicht geprüft

Safari/WebKit, echte Mobilgeräte, echte Stift-Eingabe. Die Fingerbedienung ist weiterhin ausschließlich mit
Touch-Emulation belegt — allerdings jetzt mit einem Ergebnis, das den Unterschied eindeutig zeigt.

## Hinweis zur Kundenursache

Unverändert gilt: **belegt ist sie nicht.** Neu ist, dass der Implementierer meinen Befund übernommen und als
gleichrangigen zweiten Kandidaten dokumentiert hat und dass beide Kandidaten jetzt konstruktiv ausgeschlossen
sind. Die Eingrenzungsliste für den Kunden aus der ersten Runde (Konsole, `window.playlistEntryReorder`,
WebSocket gegen Long-Polling, Edge-Version, Stelle des Loslassens) bleibt sinnvoll.

## Behebung nach der Nachprüfung

- **N1 (schwebende Zusage):** `sendReorder` hat jetzt eine Zeitgrenze (15 s, für Tests über
  `window.playlistEntryReorder.reorderTimeoutMilliseconds` überschreibbar). Läuft sie ab, wird `reorderInFlight`
  zurückgesetzt und ein Hinweis angezeigt; ein später eintreffendes Ergebnis wird über einen Zähler erkannt.
  Test `E2E_ServerCallNeverCompletes_ReportsItAfterTheTimeLimit_AndAcceptsANewDrag` (ohne Fix rot). Diese Behebung
  wurde nicht erneut von einem separaten Prüfer abgenommen.
- **A4-Rest:** Die Hilfe nennt jetzt die tatsächliche Toleranz („bis etwa eine Kachelhöhe neben einer Kachel“).
- **Bewusst offen:** Zwei-Tab-Betrieb mit veralteten Sortierwerten; flackernde zeitbasierte Tests außerhalb dieser Änderung.
