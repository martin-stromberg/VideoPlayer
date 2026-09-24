# Abnahme: Korrektur Kundenrückmeldung "Verhalten der Weiterschauen-Liste" (Playlists)

Geprüfter Stand: `61f22ca` (Branch
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-weiterschauen-playlist`),
Vergleichsstand vor der Änderung: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln`.
Prüfer: Claude (Softwareschmiede Bot) — nicht der Implementierer. Der Selbstbericht
(`implementation-korrektur-weiterschauen-playlist.md`) wurde ausschließlich als Behauptung behandelt; alles
Folgende stammt aus eigenen Läufen. `docs/features/task/customer-feedback.md` wurde nur gelesen.

Alle Belege liegen außerhalb des Repositories unter
`C:\Users\Martin\AppData\Local\Temp\claude\D--Repositories-softwareschmiede-fd729906-8801-43ee-8539-fa3e046c88f4\e838bc0e-97f1-46db-9b8f-7f49b0cca26b\scratchpad\abnahme\`:

| Datei | Inhalt |
| --- | --- |
| `s1-1-mitte.txt`, `s1-2-nach-endsequenz.txt`, `s1-link.txt` | Datenbankstand `ContinueWatchingEntries` vor/nach der Endsequenz, Szenario 1, samt Fortsetzen-Link |
| `s1-weiterschauen.png`, `s1-nach-klick.png` | Weiterschauen-Liste auf der Startseite und Ergebnis des Klicks auf die Kachel |
| `s2-1-erste-serie.txt`, `s2-2-zweite-serie.txt`, `s2-weiterschauen.png` | Szenario 2 |
| `s3-1-erste-episode.txt`, `s3-2-nach-endsequenz.txt`, `s3-weiterschauen.png` | Szenario 3 |
| `skipui-1-vorher.png`, `skipui-2-nachher.png`, `skipui-ergebnis.txt` | „Überspringen" über das Kontextmenü der Weiterschauen-Kachel |
| `mangel1-*.txt`, `mangel1-weiterschauen.png` | Mangel A1 (Nachzügler nach der Endsequenz) im laufenden Programm |
| `mangel2-*.txt`, `mangel2-weiterschauen.png` | Mangel A2 (Titel während der Wiedergabe aus der Playlist entfernt) |
| `rand-A-*.txt`, `rand-B-*.txt`, `rand-K-*.txt`, `rand-L-*.txt`, `rand-O-*.txt` | Randfälle: Nachzügler, Pufferreihenfolge, Altbestand, Entfernen, Sortierung |
| `risiko-*.txt`, `risiko-weiterschauen.png` | erster (fehlgeschlagener) Nachstellversuch des Restrisikos, siehe Abschnitt 3 |
| `CWS-HEAD.cs` | Sicherungskopie für die Gegenprobe (Service kurzzeitig auf den Basisstand zurückgesetzt) |

Die eigenen Prüf-Testklassen (`ZzAbnahmeWeiterschauenE2ETests.cs`, `ZzAbnahmeWeiterschauenRandfaelleTests.cs`)
wurden nach den Läufen wieder gelöscht und **nicht** ins Repository übernommen; der Arbeitsbaum ist bis auf
diesen Bericht unverändert (`git status`: nur `docs/features/task/` als fremde untracked Datei).

## Ergebnis

**Status:** Abweichungen gefunden

Die drei vom Kunden gemeldeten Szenarien habe ich selbst im laufenden Programm nachgestellt — frisch
gestartete Anwendung, eigene SQLite-Datei, freier Port, echtes Chromium über Playwright, echte Playlist,
echter Player mit der echten `continueWatching.js`-Anbindung, echte HTTP-Fortschrittsmeldung, echter
`ContinueWatchingBuffer`/`ContinueWatchingWorker`. **Alle drei Szenarien sind erfüllt.** Zusätzlich sind
zwölf Randfälle geprüft und in Ordnung, die Gegenprobe des Implementierers ist exakt reproduzierbar
(16 von 22 neuen Service-Tests rot ohne den Fix, der neue E2E-Test ebenfalls rot), und die Zugriffs-/
Privatsphäre-Regeln bei öffentlichen Playlists halten.

Freigabe empfehle ich trotzdem noch nicht. Es bleiben vier Abweichungen. Die beiden wichtigsten sind
**keine theoretischen Restrisiken, sondern mit einfachen Anwendergesten auslösbar** und habe ich im
laufenden Programm mit Bild und Datenbankstand belegt:

* **A1** — Nach der Endsequenz genügt „zurückspulen und pausieren" (oder Player schließen), damit der
  bereits als *gesehen* markierte alte Titel wieder in der Weiterschauen-Liste steht **und der gerade
  angelegte Nachfolger gelöscht wird**. Das ist mehr als das im Selbstbericht genannte „kurzzeitig".
* **A2** — Wird der laufende Titel aus der Playlist entfernt (Sicherheitsabfrage aus Schritt 7, Ersatz
  wird korrekt gesetzt), macht die nächste Fortschrittsmeldung des weiterlaufenden Players den Ersatz
  binnen ~3 Sekunden wieder kaputt: Der entfernte Titel steht wieder in der Liste, mit Playlist-Bezug,
  obwohl er nicht mehr zur Playlist gehört. Die Geschäftsregel BR-36 behauptet ausdrücklich das
  Gegenteil.

---

## 1. Die drei Kundenszenarien im laufenden Programm

Aufbau je Szenario: zwei bzw. eine Serie in einer manuell sortierten Playlist, Medienquelle für den
Anwender freigeschaltet, Anmeldung im Browser, Start der Wiedergabe über die Playlist-Detailseite
(`/playlists/{id}?entryId=…` — genau der Weg, den auch die Weiterschauen-Kachel benutzt). Der Fortschritt
wird über das echte `timeupdate`-Ereignis des `<video>`-Elements gemeldet; da im Testaufbau keine
streambare Medienquelle existiert, werden nur `duration`/`currentTime` des Elements gesetzt (das ist die
im Auftrag erlaubte Video-Stubbung — der komplette Weg Player-Skript → `POST /api/continue-watching/progress`
→ Puffer → Worker → `ContinueWatchingService` läuft echt).

### Szenario 1 — Ende der ersten Serie, Fortsetzung wird eingefügt

**Status: Erfüllt.**

Playlist `Szenario1` = Original E1, Original E2, Fortsetzung E1, Fortsetzung E2. Wiedergabe von
Original E2, Meldung bei 10:00 → ein Eintrag (`s1-1-mitte.txt`: `Episode=2 Playlist=1 Pos=00:10:00`).
Sprung in die Endsequenz (44:50 von 45:00) → `s1-2-nach-endsequenz.txt`:
`Episode=3 Playlist=1 Pos=00:00:00` — genau **ein** Eintrag, und zwar die erste Episode der zweiten Serie.
`s1-weiterschauen.png` zeigt auf der Startseite genau eine Kachel „SzenarioEinsFortsetzung … S1 1" mit dem
Untertitel „In Playlist: Szenario1". Der Link der Kachel ist `/playlists/1?entryId=3` (`s1-link.txt`), also
der Playlist-Eintrag der Fortsetzung. Der Klick darauf landet in der Playlist und startet den richtigen
Titel: `s1-nach-klick.png` zeigt das Wiedergabe-Abzeichen `[Szenario1: 3/4]`, also den dritten von vier
Einträgen.

### Szenario 2 — zweiter Titel derselben Playlist ersetzt den Eintrag

**Status: Erfüllt.**

Wiedergabe von Original E1 aus der Playlist, Meldung bei 10:00 → ein Eintrag
(`s2-1-erste-serie.txt`: `Episode=1`). Player geschlossen, Episode der zweiten Serie aus derselben
Playlist gestartet, Meldung bei 10:00 → `s2-2-zweite-serie.txt`: genau **ein** Eintrag,
`Episode=3 Playlist=1`. Es entsteht kein zweiter Eintrag; der Eintrag der ersten Serie ist ersetzt.
`s2-weiterschauen.png` zeigt eine einzige Kachel „SzenarioZweiFortsetzung … S1 1 / In Playlist: Szenario2".

### Szenario 3 — aus der Playlist ausgeschlossene Episode wird übersprungen

**Status: Erfüllt.**

Serie mit E1, E2, E3 in der Playlist; E2 über den echten `IPlaylistService.RemoveMediaFromPlaylistAsync`
(inkl. Ausschlussvermerk) entfernt. Wiedergabe von E1, Meldung bei 10:00 → Eintrag E1
(`s3-1-erste-episode.txt`). Endsequenz → `s3-2-nach-endsequenz.txt`: `Episode=3`, also **E3**; die
ausgeschlossene E2 wird übersprungen. `s3-weiterschauen.png` zeigt „SzenarioDreiSerie … S1 3".

### Zusätzlich: „Überspringen" in der Weiterschauen-Liste (Oberfläche)

**Status: Erfüllt.** Kontextmenü der Kachel geöffnet (Shift+F10 auf der fokussierten Kachel — die
Langdruck-Geste war im Prüflauf nicht zuverlässig auslösbar, das Menü selbst ist dasselbe), „Überspringen"
gewählt: Rückmeldung „Eintrag wurde übersprungen.", die Kachel zeigt danach den nächsten **Playlist**-Titel
(`skipui-2-nachher.png`, `skipui-ergebnis.txt`), `ListOrder` bleibt erhalten (eigener Dienst-Test).

---

## 2. Gegenprobe (ohne den Fix)

`VideoWebPlayer/Services/ContinueWatchingService.cs` kurzzeitig auf den Basisstand zurückgesetzt, alles neu
gebaut, danach wiederhergestellt (`git status` sauber):

* `dotnet test --filter FullyQualifiedName~ContinueWatchingPlaylist` → **Fehler: 16, erfolgreich: 6, gesamt: 22.**
  Das deckt sich exakt mit der Angabe des Implementierers (16 rot, 6 Kontrolltests grün).
* `ContinueWatchingE2ETests` → **1 rot**, nämlich
  `E2E_PlaylistWithTwoShows_EndOfFirstShow_ContinuesWithSecondShow`; die übrigen 11 grün.
* Meine **eigenen** Prüftests → 11 von 22 rot, darunter **alle drei Kundenszenarien im Browser**
  (`Szenario1…`, `Szenario2…`, `Szenario3…`) sowie gesperrter Titel, Kaskaden-Kind, gemischte Playlist,
  Pufferreihenfolge, Skip, Altbestand-Bereinigung und das Oberflächen-Überspringen.

Der geänderte bestehende Test `E2E_SkipWithPlaylistId_NextWithSamePlaylistId` ist **ohne** den Fix grün —
die Einordnung des Implementierers („kein Regressionsnachweis, nur wieder realistisch") ist damit bestätigt
und ehrlich. Die geänderte Anordnung (Playlist enthält jetzt beide Episoden, Quellzugriff erteilt) ist
sachlich richtig: `PlaylistPlaybackContext` meldet eine `playlistId` nur für Titel dieser Playlist.

---

## 3. Das Restrisiko „verspätete Fortschrittsmeldung" — real geprüft

### Wie Fortschritt tatsächlich zum Server kommt

`VideoWebPlayer/wwwroot/js/continueWatching.js` hängt sich an `timeupdate`, `pause` und `ended` des
`<video>`-Elements und schickt `POST /api/continue-watching/progress` mit
`mediaType/mediaId/positionSeconds/durationSeconds/playlistId`. Drosselung: bei `timeupdate` nur, wenn
`pos - lastSent >= 3`; bei `pause` und beim Abhängen (`detach`, u. a. beim Schließen des Players und beim
Wechsel auf den nächsten Playlist-Titel) **erzwungen**, also ohne Drosselung. Serverseitig filtert
`ReportProgressAsync` Positionen < 5 s heraus, prüft den Playlist-Zugriff und legt einen Eintrag im
`ContinueWatchingBuffer` ab — je (Benutzer, Film, Episode, Playlist) genau einer, neuere Meldungen
überschreiben ältere. Der `ContinueWatchingWorker` ist ein einzelner Leser (`SingleReader = true`) und
verarbeitet die Schlüssel streng in Eingangsreihenfolge.

**Puffer geprüft (Beleg `rand-B-puffer.txt`):** Zwei gepufferte Einträge desselben Anwenders für
verschiedene Videos derselben Playlist werden nacheinander verarbeitet; am Ende bleibt genau **ein**
Eintrag, und zwar der des zuletzt gemeldeten Titels. Die Reihenfolge bleibt erhalten; zwei Einträge je
Playlist entstehen dabei nicht. Das gemeldete Kundenproblem tritt hier also nicht wieder auf.

**Wechsel-Abläufe geprüft:** Beim Weiterschalten innerhalb des Players (Auto-Advance oder „Nächster")
lädt `VideoPlayer.razor` zuerst das Element neu (`videoPlayer.reload`, `currentTime` = 0) und bindet erst
danach `continueWatching.attach` neu; der dabei ausgelöste erzwungene Abschluss-Versand des alten Titels
meldet Position 0 und wird serverseitig durch die 5-Sekunden-Grenze verworfen. Beim Schließen des Players
und anschließendem Start eines anderen Titels liegt die Meldung des alten Titels zeitlich **vor** der des
neuen. Eine reine Netz-Umordnung zweier Meldungen halte ich deshalb für unwahrscheinlich; der Selbstbericht
liegt insoweit richtig.

**Aber:** Die Drosselung des Skripts hat mich bei meinem ersten Nachstellversuch getäuscht (`risiko-*.txt`:
scheinbar „kein Problem" — tatsächlich wurde die nachgereichte Meldung im Browser gar nicht abgeschickt,
weil `pos - lastSent` negativ war). Über die **erzwungenen** Sendewege (`pause`, `detach`) ist der Fall
sehr wohl erreichbar, siehe A1.

---

## 4. Abweichungen

- [ ] **A1 — Nachzügler nach der Endsequenz macht den Nachfolger kaputt und holt einen bereits gesehenen
  Titel zurück.** (Belege: `mangel1-1-nach-endsequenz.txt`, `mangel1-2-nach-zuruecksp.txt`,
  `mangel1-ergebnis.txt`, `mangel1-weiterschauen.png`; zusätzlich `rand-A-nachzuegler.txt` auf Dienstebene.)

  *Reproduktion im laufenden Programm:* Playlist mit zwei Serien, letzte Episode der ersten Serie aus der
  Playlist abspielen, in die Endsequenz laufen lassen → Eintrag ist korrekt die erste Episode der zweiten
  Serie (`Episode=3`). Dann **zurückspulen und pausieren** (das `pause`-Ereignis sendet erzwungen).
  Ergebnis: `Episode=2 Playlist=1 Pos=00:10:00` — der alte Titel ist zurück, und der Eintrag der zweiten
  Serie ist **gelöscht** (`RemoveOtherEntriesOfPlaylistAsync` entfernt ihn, weil pro Playlist nur ein
  Eintrag bleiben darf). Der zurückgekehrte Titel ist außerdem bereits als *gesehen* markiert; das
  Gesehen-Abzeichen ist im Bildschirmfoto oben rechts auf der Kachel zu sehen. Das Schließen des Players
  nach einem Rückspulen wirkt genauso (`detach` sendet ebenfalls erzwungen).

  *Bewertung:* Tritt praktisch auf, sobald jemand nach dem Abspann zurückspult (Nachspann-Szene ansehen,
  Stelle noch einmal hören) und dann pausiert oder schließt — keine Ausnahmesituation. Der Selbstbericht
  beschreibt das als „zeigt dann kurzzeitig wieder den alten Titel"; tatsächlich ist der Nachfolger
  dauerhaft weg, bis erneut etwas gemeldet wird. Vor dem Fix war das Verhalten nicht besser, aber anders:
  Damals wäre ein zweiter Eintrag entstanden statt den Nachfolger zu löschen.

  *Empfehlung:* Im Endsequenz-/Nachfolger-Zweig den Zeitpunkt der Meldung mitführen
  (`ContinueWatchingBuffer.ProgressEntry.UpdatedAt` erreicht `ProcessBufferedEntryAsync` heute nicht) und
  eine Meldung verwerfen, die älter ist als der aktuelle Eintrag der Playlist; alternativ (einfacher und
  fachlich gut begründbar) eine Fortschrittsmeldung für einen Titel ignorieren, der für diesen Anwender
  bereits als *gesehen* markiert ist und dessen Playlist inzwischen einen anderen Eintrag hat.

- [ ] **A2 — Wird der laufende Titel aus der Playlist entfernt, hebt die nächste Fortschrittsmeldung den
  Ersatz wieder auf; BR-36 behauptet das Gegenteil.** (Belege: `mangel2-1-nach-entfernen.txt`,
  `mangel2-2-nach-weitermeldung.txt`, `mangel2-ergebnis.txt`, `mangel2-weiterschauen.png`;
  Dienstebene: `rand-L-entfernt.txt`.)

  *Reproduktion:* Playlist mit E1, E2, E3; E1 aus der Playlist abspielen, Fortschritt melden → Eintrag E1.
  Dann E1 über die Sicherheitsabfrage aus Schritt 7 aus der Playlist entfernen → Eintrag wird korrekt auf
  E2 gesetzt (`ResolvePlaylistEntryRemovalAsync`, BR-17 funktioniert). Der Player läuft weiter und meldet
  ~3 Sekunden später erneut Fortschritt für E1 mit `playlistId`. Ergebnis: `Episode=1 Playlist=1
  Pos=00:11:00` — der entfernte Titel steht wieder in der Weiterschauen-Liste, der Ersatz E2 ist gelöscht.

  *Warum das mehr ist als A1:* Der Eintrag zeigt danach auf ein Video, das gar nicht mehr zur Playlist
  gehört. `ContinueWatchingList` kann für ihn keine `PlaylistEntryId` auflösen (`EnrichPlaylistInfoAsync`
  findet keinen passenden `PlaylistEntry`) und verlinkt deshalb auf `/playlists/{id}` **ohne** `entryId`.
  `PlaylistDetail` startet dann gar keine Wiedergabe (`if (EntryId.HasValue) …`): Der Anwender klickt auf
  eine Kachel mit einem konkreten Titel und landet auf der Playlist-Seite, ohne dass dieser Titel läuft.
  Das ist genau die Verwirrung, die BR-17 vermeiden sollte.

  *Dokumentationsfehler an zwei Stellen:* `docs/help/playlists-business-rules.md`, BR-36, Abschnitt
  „Sonderfall": „eine verspätete Fortschrittsmeldung des entfernten Titels darf ihn nicht überschreiben",
  und `docs/help/playlists-ablauf-technisch.md`, Ablauf 9, Randfälle: „Der Ersatz wurde beim Entfernen
  bereits gesetzt … und bleibt unangetastet." Der Code setzt das nur für die **Nachfolgerermittlung** um
  (`ResolvePlaylistSuccessorAsync` liefert dann „kein Nachfolger"); der Schreibweg `UpsertAsync` legt den
  Eintrag trotzdem an und löscht den Ersatz. Beide Aussagen sind in dieser Form falsch.

  *Empfehlung:* In `UpsertAsync` (bzw. davor) eine Fortschrittsmeldung mit `playlistId` verwerfen, wenn der
  gemeldete Titel kein `PlaylistEntry` dieser Playlist (mehr) ist — die Zuordnung wird in
  `ResolvePlaylistSuccessorAsync` ohnehin schon abgefragt. Dann ist BR-36 auch tatsächlich erfüllt.

- [ ] **A3 — Nachfolger wird bei jeder Meldung innerhalb der Endsequenz neu berechnet.** Der
  Endsequenz-Zweig läuft für **jede** Fortschrittsmeldung in den letzten 30 Sekunden, also bei der
  3-Sekunden-Drosselung des Skripts rund zehnmal je Titel. Jedes Mal lädt
  `GetNextPlaylistEntryAsync` → `FindAdjacentPlayableEntryAsync` sämtliche `PlaylistEntries`, prüft deren
  Existenz, sortiert sie und löst die Zugänglichkeit aller Einträge auf (`_accessResolver`), zusätzlich zu
  `MarkWatchedAsync` und dem Upsert. Vorher war das eine Punktabfrage auf die nächste Episode. Bei großen
  Playlists ist das spürbar; gemessen wurde nichts, im Selbstbericht steht es nur als Nebensatz
  („Nicht gemessen"). Fachlich ist der wiederholte Lauf idempotent, also kein Fehler — nur teuer.
  *Empfehlung:* Vor der Nachfolgerermittlung prüfen, ob für diese Playlist bereits der erwartete
  Nachfolger-Eintrag steht, und dann früh zurückkehren. Ein simples „nur wenn `existingEntries.Count > 0`"
  greift zu kurz: Ein Anwender kann direkt in die Endsequenz springen, ohne je einen Eintrag gehabt zu
  haben — dann muss der Nachfolger trotzdem entstehen.

- [ ] **A4 — Altbestand mit mehreren Einträgen je Playlist wird beim reinen Anzeigen nicht behandelt.**
  (Beleg: `rand-K-altbestand.txt`.) Zwei per Datenbank angelegte Einträge derselben Playlist bleiben beim
  Abrufen der Liste unverändert bestehen — `GetListAsync` liest und mappt jede Zeile ohne Zusammenfassung,
  der Anwender sieht also zwei Kacheln für dieselbe Playlist, bis er zufällig wieder etwas aus dieser
  Playlist abspielt. Erst der nächste Schreibvorgang bereinigt (das funktioniert, siehe Beleg: 2 → 1).
  Die bewusste Entscheidung gegen eine Migration ist nachvollziehbar und in BR-35 sauber begründet; die
  Doku sagt aber nicht, dass der Zwischenzustand dem Anwender angezeigt wird. *Empfehlung:* Entweder in
  `GetListAsync` je (Anwender, Playlist) nur die jüngste Zeile ausgeben, oder den Zwischenzustand in BR-35
  ausdrücklich benennen. Kein Blocker.

---

## 5. Geprüfte Randfälle ohne Befund

Alle folgenden Punkte habe ich selbst gegen die laufende Anwendung mit echtem SQLite und den echten
Diensten geprüft (nicht über die Tests des Implementierers):

| Randfall | Ergebnis |
| --- | --- |
| Playlist-Ende ohne Nachfolger | Eintrag verschwindet ersatzlos, auch wenn die Serie weiterginge |
| Manuelle Sortierung | Nachfolger folgt der Playlist-Reihenfolge, nicht der Serienreihenfolge |
| Sortierung nach Erscheinungsdatum | Nachfolger folgt der Datums-Sortierung (`rand-O-sortierung.txt`) |
| Playlist mit Filmen und Episoden gemischt | Auf eine Episode folgt korrekt der Film der Playlist |
| Kaskaden-Kindeinträge (Serie als Ganzes, danach eine Episode entfernt) | Entfernte Episode wird übersprungen, Sammel-Eintrag (TVShow) wird nicht als Nachfolger angeboten |
| Gesperrter Titel (nicht freigegebene Medienquelle) | wird übersprungen |
| Öffentliche Playlist eines anderen Anwenders | Betrachter bekommt seinen eigenen Eintrag; der Eintrag des Besitzers bleibt unverändert (Position und Titel), keine Fremd-Einträge werden gelöscht |
| Zwei Playlists mit demselben Video | beeinflussen sich nicht, je Playlist ein eigener Eintrag |
| Eintrag ohne Playlist | bleibt beim Schreiben eines Playlist-Eintrags unberührt |
| `SkipAsync` mit Playlist (Dienst und Oberfläche) | nächster Playlist-Titel, `ListOrder` bleibt erhalten |
| Altbestand, nächster Schreibvorgang | wird bereinigt (2 → 1), siehe aber A4 |
| Titel-Entfernen mit Weiterschauen-Sicherheitsabfrage (Schritt 7) | Ersatz wird korrekt gesetzt, siehe aber A2 |
| Playlist gelöscht | bestehender Test `E2E_PlaylistDeleted_EntryBecomesFree` grün |
| Regression ohne Playlist | nächste Episode der Serie und nächster Film der Sammlung unverändert |
| Gesehen-Markierung | bleibt playlist-übergreifend: alle Varianten des Videos verschwinden, `WatchedEntries` wird gesetzt |

---

## 6. Code-Review des Diffs

Der Diff berührt an Produktivcode ausschließlich `VideoWebPlayer/Services/ContinueWatchingService.cs`
(+194/−33 Zeilen); kein Schema, keine Migration, keine Client-Änderung. Das passt zur Aufgabe.

**Gut gelöst:**

* Die Nachfolgerermittlung ist an genau einer Stelle gebündelt (`ResolveNextMediaAsync`) und wird von
  Endsequenz **und** `SkipAsync` benutzt — beide Wege, einen Titel zu verlassen, verhalten sich damit
  gleich. Das ist der Kern der Kundenmeldung und sauber umgesetzt.
* Wiederverwendung von `IPlaylistService.GetNextPlaylistEntryAsync` statt einer zweiten, eigenen
  Sortier-/Zugänglichkeitslogik. Dadurch gelten Sortiermodus, Ausschlüsse (BR-19), Sperren und das
  Überspringen nicht abspielbarer Sammel-Einträge automatisch mit.
* Die Abbildung Titel → `PlaylistEntry` über (`PlaylistId`, `MediaType`, `MediaId`) ist wegen des
  Unique-Index eindeutig — auch für einen Titel, der zugleich Kaskaden-Kind eines Sammel-Eintrags ist.
  Selbst nachgestellt (Randfall „Kaskaden-Kindeinträge").
* Die Ausnahmebehandlung (`KeyNotFoundException`/`PlaylistAccessDeniedException`/`InvalidOperationException`
  → „kein Nachfolger", Debug-Protokoll) verhindert, dass eine zwischenzeitlich gelöschte oder wieder
  privat gestellte Playlist den Worker in eine Fehlerschleife schickt.
* **Sicherheit/Privatsphäre halten.** `RemoveOtherEntriesOfPlaylistAsync` filtert auf `UserId`, es werden
  nie fremde Einträge gelöscht. Die Zugänglichkeit wird immer für den meldenden Anwender aufgelöst, und in
  `FindAdjacentPlayableEntryAsync` läuft die stille Waisen-Bereinigung nur für den Besitzer
  (`removeOrphans: isOwner`) — ein Betrachter einer öffentlichen Playlist verändert also nichts an fremden
  Daten. Durch eigenen Test belegt.
* Unique-Index-Kollisionen beim Ersetzen konnte ich nicht provozieren: Die entfernten Zeilen betreffen
  immer andere Medien-IDs als die geschriebene, und der „behalten"-Fall wird in `UpsertAsync` als
  Aktualisierung derselben Zeile behandelt. Die Tests laufen gegen echtes SQLite, die Indizes greifen dort
  wirklich.
* `ListOrder` bleibt im `SkipAsync`-Pfad und in `ResolvePlaylistEntryRemovalAsync` erhalten.

**Anmerkungen (keine eigenen Abweichungen, aber erwähnenswert):**

* **Reentranz** ist im Endsequenz-Zweig entschärft (eigene Löschungen vorher gespeichert) und im
  Skip-Zweig durch die Reihenfolge (Nachfolger ermitteln, dann `Remove`). Ein Restfall bleibt: Löst der
  Nachfolger-Aufruf in `SkipAsync` eine Waisen-Bereinigung aus, die über
  `ResolvePlaylistEntryRemovalAsync` **genau die Zeile** entfernt, die `SkipAsync` danach selbst entfernen
  will, arbeitet `SkipAsync` auf einer bereits gelöschten Entität weiter. Das setzt voraus, dass ein
  Medium exakt in diesem Moment aus der Bibliothek verschwindet; ich habe es nicht reproduziert und halte
  es für sehr unwahrscheinlich. Der Selbstbericht benennt den Fall ebenfalls offen.
* **Wechsel innerhalb einer Playlist setzt `ListOrder` neu.** Beim normalen Wechsel auf einen anderen Titel
  derselben Playlist entsteht eine neue Zeile mit frischem `ListOrder`; der Playlist-Eintrag rutscht damit
  an den Anfang der Weiterschauen-Liste. Für die gemeldeten Szenarien richtig (frische Aktivität), in der
  Doku aber nicht erwähnt.
* Der Kommentarblock „Wenn wir wirklich etwas entfernt haben …" sitzt jetzt am `else if`-Zweig und ist dort
  etwas irreführend formuliert; rein kosmetisch.

**Tests des Implementierers:** Die 22 neuen Dienst-Tests laufen gegen echtes SQLite, nicht gegen
EF-InMemory — das ist für die Unique-Indizes wichtig und richtig entschieden. Sie decken die drei Szenarien
und die genannten Randfälle ab, und sie scheitern ohne den Fix (siehe Abschnitt 2). Kein bestehender Test
wurde abgeschwächt oder deaktiviert; die eine Änderung an
`E2E_SkipWithPlaylistId_NextWithSamePlaylistId` ist begründet und die Begründung ist tragfähig und ehrlich
als „kein Regressionsnachweis" gekennzeichnet. **Nicht abgedeckt** sind die Abläufe aus A1 und A2 — der
Selbstbericht sagt für A1 selbst „Kein Test deckt diesen Ablauf ab".

**Dokumentation:** BR-35 und BR-36 sind ausführlich, gut begründet und bis auf den unter A2 genannten
Satz sachlich richtig; die Einschränkung der beiden alten Weiterschauen-Regeln auf Einträge *ohne*
Playlist-Bezug ist korrekt nachgezogen, `docs/API.md` beschreibt die neue Skip-Semantik zutreffend. Die
Entscheidung gegen eine Migration ist offen dargelegt (siehe aber A4).

---

## 7. Eigene Läufe

* `dotnet build VideoPlayer.sln -c Release` → **0 Fehler** (245 Warnungen, alles vorbestehende
  xUnit-/Analyzer-Warnungen, unverändert zum Ausgangsstand).
* `dotnet test VideoWebPlayer.Tests` (vollständig, inkl. Playwright-E2E), Lauf 1:
  **1272 erfolgreich, 2 Fehler**, 4 min 6 s. Fehlgeschlagen:
  `PlaylistMediaSearchE2ETests.AddMedia_SearchCaseInsensitive_FindsMedia` und
  `PlaylistPlaybackE2ETests.PlaylistPreviousEntryE2ETest`, beide mit
  `TimeoutException` beim Warten auf `.media-search-input`/`#playlist-mode-add-button`.
* Beide Tests einzeln dreimal wiederholt: **1× rot (direkt nach einem Neubau), 2× grün.** Beide gehören
  zur Playlist-Suchoberfläche, wurden von dieser Änderung nicht angefasst und berühren die
  Weiterschauen-Logik nicht. Bewertung: bekanntes Flackern zeitbasierter Playwright-Tests unter Last bzw.
  beim ersten Lauf nach einem Neubau, nicht durch diese Änderung verursacht. Ich habe die Tests nicht
  angefasst.
* `dotnet test VideoWebPlayer.Tests` Lauf 2 (nach Entfernen meiner Prüfklassen, Arbeitsbaum wie `61f22ca`):
  **1274 erfolgreich, 0 Fehler**, 4 min 57 s. Damit ist die Suite auf diesem Stand grün; die beiden
  Ausfälle aus Lauf 1 sind Flackern.
* Eigene Prüfklassen (22 Tests, davon 5 im echten Browser): mit dem Fix **22 grün**, ohne den Fix
  **11 rot**. Danach gelöscht.

---

## 8. Hinweise

* Die Kundenrückmeldung bleibt unabgehakt — das Abhaken liegt beim Kunden.
* Für A1 und A2 empfehle ich eine Nachbesserung im selben Zug, weil beide dieselbe Ursache haben
  (eine Fortschrittsmeldung darf den „einen Eintrag je Playlist" nicht rückwirkend auf einen bereits
  verlassenen oder nicht mehr zur Playlist gehörenden Titel setzen) und weil A2 einer dokumentierten
  Geschäftsregel widerspricht. A3 und A4 können auch später kommen.
