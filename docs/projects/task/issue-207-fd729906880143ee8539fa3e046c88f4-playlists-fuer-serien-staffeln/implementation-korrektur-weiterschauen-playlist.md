# Korrektur Kundenrückmeldung: Verhalten der Weiterschauen-Liste bei Playlists

Kundenmeldung (`docs/features/task/customer-feedback.md`, Abschnitt „Verhalten der Weiterschauen-Liste"):
„Es scheint, dass die Playlist bei der Weiterschauen-Liste nur bedingt berücksichtigt wird." Drei Szenarien.

Alle drei Szenarien wurden zuerst als fehlschlagende Tests nachgestellt, dann behoben. Grundlage der Tests
ist echtes SQLite (`PlaylistServiceTestBase`) mit einem echten `PlaylistService` und einem echten
`ContinueWatchingService`, die gegenseitig verdrahtet sind — die Unique-Indizes auf
`ContinueWatchingEntries` greifen dabei tatsächlich, anders als beim EF-InMemory-Provider.

## 1. Ursache je Szenario

### Szenario 1: Am Ende der ersten Serie wird die zweite Serie der Playlist nicht eingefügt

`ContinueWatchingService.ProcessBufferedEntryAsync` ermittelte den Nachfolger im Endsequenz-Zweig
ausschließlich über `GetNextMovieAsync`/`GetNextEpisodeAsync`, also aus der Serien- bzw.
Sammlungsreihenfolge — auch dann, wenn `playlistId` gesetzt war. Nach der letzten Episode einer Serie gibt
`GetNextEpisodeAsync` `null` zurück; der alte Eintrag wurde entfernt und kein neuer angelegt. Die Playlist
wurde an dieser Stelle nie gefragt.

### Szenario 2: Zweiter Eintrag für dieselbe Playlist

`UpsertAsync` entfernte beim Anlegen eines neuen Eintrags nur andere Einträge **derselben Serie bzw.
derselben Filmsammlung** mit derselben `PlaylistId` (`RemoveExistingTVShowEntry`,
`RemoveExtsingMovieCollectionEntry`). Eine Episode einer *anderen* Serie derselben Playlist fiel nicht
darunter, also entstand ein zweiter Eintrag für dieselbe Playlist. Eine Regel „ein Eintrag je Playlist"
existierte nicht.

### Szenario 3: Der aus der Playlist entfernte Titel wird als Nachfolger angeboten

Dieselbe Ursache wie Szenario 1: Der Nachfolger kam aus der Serienreihenfolge. Die ausgeschlossene Episode
ist in der Serie weiterhin die nächste; dass sie kein `PlaylistEntry` mehr ist (BR-19), spielte keine Rolle.
`SkipAsync` („Überspringen") hatte denselben Fehler.

## 2. Änderungen

Alle Änderungen liegen in `VideoWebPlayer/Services/ContinueWatchingService.cs`; Schema, Datenmodell,
Migrationen, Backup-Tabellen und die Client-Seite bleiben unverändert.

### Neue Regel: Nachfolger aus der Playlist

- `ResolveNextMediaAsync(userId, playlistId, movieId, episodeId, ct)` entscheidet zentral: mit
  `playlistId` → `ResolvePlaylistSuccessorAsync`, ohne → unverändert
  `GetNextMovieAsync`/`GetNextEpisodeAsync`.
- `ResolvePlaylistSuccessorAsync` bildet den aktuellen Titel über (`PlaylistId`, `MediaType`, `MediaId`) auf
  seinen `PlaylistEntry` ab (je Playlist eindeutig, Unique-Index `IX_PlaylistEntries_PlaylistId_MediaType_MediaId`,
  deshalb auch bei einem Titel eindeutig, der zugleich Kaskaden-Kind eines Sammel-Eintrags ist) und ruft
  `IPlaylistService.GetNextPlaylistEntryAsync` auf — dieselbe Navigation wie beim Weiterschalten während der
  Wiedergabe (`FindAdjacentPlayableEntryAsync`: Sortiermodus der Playlist, überspringt nicht abspielbare
  Sammel-Einträge und für **diesen** Anwender nicht zugängliche Titel).
- Fällt der aktuelle Titel nicht (mehr) in die Playlist, oder ist die Playlist nicht mehr lesbar
  (`KeyNotFoundException`/`PlaylistAccessDeniedException`/`InvalidOperationException`), wird „kein
  Nachfolger" zurückgegeben statt einer Ausnahme; das wird auf Debug-Ebene protokolliert.
- `ProcessBufferedEntryAsync` (Endsequenz) und `SkipAsync` nutzen jetzt beide `ResolveNextMediaAsync`.
  Nebeneffekt: Der Endsequenz-Zweig ermittelte den Nachfolger bisher **zweimal** (einmal zum Einfügen,
  einmal für die `hasNext`-Prüfung der SignalR-Benachrichtigung); das ist jetzt ein Aufruf.

### Neue Regel: ein Weiterschauen-Eintrag je Anwender und Playlist

- `RemoveOtherEntriesOfPlaylistAsync(userId, playlistId, keepMovieId, keepEpisodeId, ct)` entfernt alle
  Einträge desselben Anwenders mit derselben `PlaylistId`, die nicht auf den zu schreibenden Titel zeigen.
  Einträge mit `PlaylistId = NULL` und Einträge anderer Playlists bleiben unberührt, ebenso die Einträge
  anderer Anwender (die Regel gilt je Anwender — Besitzer und Betrachter einer öffentlichen Playlist haben
  jeweils ihren eigenen einen Eintrag).
- `RemoveSupersededEntriesAsync` bündelt die Fallunterscheidung: mit `PlaylistId` die neue Regel, ohne
  `PlaylistId` unverändert die bisherige engere Regel (eine Episode je Serie, ein Film je Sammlung).
- `UpsertAsync` ruft die Playlist-Bereinigung **immer** auf, wenn `playlistId` gesetzt ist — auch dann, wenn
  der Eintrag selbst nur aktualisiert wird. Damit verschwinden Altbestände mit mehreren Einträgen je
  Playlist beim nächsten Schreibvorgang von selbst. Für den Fall ohne Playlist bleibt es bei der bisherigen
  Einschränkung „nur beim Anlegen".
- `SkipAsync` verwendet `RemoveSupersededEntriesAsync` und behält wie bisher die `ListOrder` des ersetzten
  Eintrags.

### Bewusst nicht geändert

- **Kein Bereinigungslauf (Migration/Startup) für Altbestände.** Begründung: Es ändert sich kein Schema; die
  Bereinigung ist ohne Nutzeraktion nicht dringend; ein einmaliger Massenlöschlauf würde Fortschritte
  entfernen, die der Anwender gerade sieht, ohne dass er den Zusammenhang erkennen könnte. Stattdessen lazy
  beim nächsten Schreibvorgang der betroffenen Playlist — abgesichert durch
  `ExistingDuplicates_AreCleanedUpOnNextWrite`. Damit bleibt auch die Backup-Kompatibilitätsregel
  (`VideoWebPlayerBackupData.OptionalRestoreTables/Columns`) unangetastet.
- **`ResolvePlaylistEntryRemovalAsync` (Sicherheitslogik beim Entfernen eines Titels, Schritt 7)** ist mit
  der neuen Regel bereits konsistent: Der Ersatz wird in dieselbe Zeile geschrieben (`ListOrder` bleibt
  erhalten, der Eintrag springt nicht in der Liste), und der Kollisionszweig entfernt eine von zwei Zeilen,
  statt eine dritte anzulegen. Eine Kollision kann nur aus Altbestand entstehen. Das ist jetzt im XML-Doc
  der Methode festgehalten; Code-Änderung war keine nötig, die bestehenden Tests dazu bleiben grün.
- **Gesehen-Markierung** bleibt playlist-übergreifend: Beim Erreichen der Endsequenz werden weiterhin alle
  Varianten des Videos entfernt (`MultipleEntries_MarkWatched_DeletesAllVariants` unverändert grün).
- **Client (`VideoPlayer.razor`/`PlaylistPlaybackContext`)** brauchte keine Änderung: Beim automatischen
  Weiterschalten ändert sich `StreamUrl`, `continueWatching.attach` wird mit der unveränderten
  `currentPlaylistId` und der neuen `MediaId` neu gebunden, der Fortschritt des neuen Titels wird also
  weiterhin mit `playlistId` gemeldet. Der E2E-Test
  `E2E_PlaylistWithTwoShows_EndOfFirstShow_ContinuesWithSecondShow` belegt die Kette bis in die
  Weiterschauen-Liste inklusive Fortsetzen-Link.
- **`ContinueWatchingList.razor`** brauchte keine Änderung: `EnrichPlaylistInfoAsync` löst `PlaylistEntryId`
  für den Nachfolger auf, weil dieser selbst ein Eintrag derselben Playlist ist (Test
  `EndSequence_SuccessorEntry_ResolvesPlaylistEntryIdForResumeLink` und der E2E-Test oben).

## 3. Tests

Neu (alle gegen echtes SQLite, neue Basisklasse
`VideoWebPlayer.Tests/Helpers/ContinueWatchingPlaylistTestBase.cs`):

| Datei | Inhalt |
|-------|--------|
| `Services/ContinueWatchingPlaylistSuccessorTests.cs` | Szenario 1 und 3, Playlist-Ende ohne Ersatz, manuelle Sortierung statt Serienreihenfolge, Sortierung nach Erscheinungsdatum, gesperrter Titel übersprungen, Sammel-Eintrag übersprungen und Film folgt auf Episode, ohne Playlist weiterhin Serienreihenfolge, Fortsetzen-Link des Nachfolgers, aktueller Titel nicht mehr in der Playlist |
| `Services/ContinueWatchingPlaylistSingleEntryTests.cs` | Szenario 2 (direkt und über `ReportProgressAsync` + Puffer-Flush wie im Worker), Film ersetzt Episode derselben Playlist, mehrere Playlists unabhängig, Eintrag ohne Playlist unberührt, gleicher Bericht zweimal ohne Duplikat, Altbestand-Duplikate werden bereinigt, öffentliche Playlist je Anwender |
| `Services/ContinueWatchingPlaylistSkipTests.cs` | `SkipAsync` mit Playlist: nächster Playlist-Titel, ausgeschlossener Titel übersprungen, Playlist-Ende → `RemovedWithoutNext`, ohne Playlist weiterhin Serienreihenfolge |
| `ContinueWatchingE2ETests.E2E_PlaylistWithTwoShows_EndOfFirstShow_ContinuesWithSecondShow` | Kundenszenario 1 über HTTP, Puffer/Worker und Weiterschauen-Liste inklusive `playlistEntryId` |

### Gegenproben

- **22 neue Service-Tests vor dem Fix:** 16 rot, 6 grün. Rot waren genau die Tests, die das neue Verhalten
  fordern; grün waren die Kontrolltests (ohne Playlist, mehrere Playlists, Eintrag ohne Playlist, gleicher
  Bericht zweimal, aktueller Titel nicht mehr in der Playlist, `SkipAsync` ohne Playlist) — sie belegen, dass
  das unveränderte Verhalten wirklich unverändert bleibt.
  Fehlerbilder vor dem Fix u. a.: Szenario 1 „Assert.Single() Failure: The collection was empty",
  Szenario 2 „Assert.Single() Failure: The collection contained 2 items", Szenario 3 „Expected: 3, Actual: 2".
- **Neuer E2E-Test vor dem Fix:** `ContinueWatchingService.cs` per `git stash` auf den alten Stand gebracht,
  neu gebaut, Test gelaufen → `E2E_PlaylistWithTwoShows_EndOfFirstShow_ContinuesWithSecondShow` rot
  („Assert.NotNull() Failure: Value is null" — der Nachfolger-Eintrag entsteht nicht).

### Geänderter bestehender Test (mit Begründung)

`ContinueWatchingE2ETests.E2E_SkipWithPlaylistId_NextWithSamePlaylistId` band einen Weiterschauen-Eintrag an
eine **leere** Playlist und erwartete, dass „Überspringen" trotzdem die nächste Episode der *Serie* liefert.
Der Test schrieb damit genau die korrigierte Semantik fest (Playlist-Bezug, aber Serienreihenfolge) und wäre
nach dem Fix fehlgeschlagen. Die Anordnung ist zudem in der Anwendung nicht erreichbar: `PlaylistPlaybackContext`
meldet eine `playlistId` nur für Titel, die zu dieser Playlist gehören. Geändert wurde deshalb nur die
Anordnung — die Playlist enthält jetzt beide Episoden, und der Anwender hat Zugriff auf die Medienquelle;
die Erwartung (Episode 2 ersetzt Episode 1, Playlist-Bezug bleibt) ist unverändert. Ehrliche Einordnung:
Dieser Test schlägt ohne den Fix **nicht** fehl (Playlist- und Serienreihenfolge fallen hier zusammen), er
ist also kein Regressionsnachweis, sondern nur wieder realistisch. Den Nachweis führen die genannten neuen
Tests.

Sonst wurde kein bestehender Test geändert, keiner deaktiviert oder abgeschwächt. Eine Hilfsmethode wurde
erweitert: `PlaylistServiceTestBase.BuildContinueWatchingService` nimmt optional einen
`ContinueWatchingBuffer` entgegen, damit ein Test den vollständigen Weg
`ReportProgressAsync` → Puffer → `ProcessBufferedEntryAsync` fahren kann (Standardverhalten unverändert).

### Läufe

- `dotnet build VideoPlayer.sln -c Debug` → 0 Fehler (245 Warnungen, unverändert zum Ausgangsstand).
- `dotnet build VideoPlayer.sln -c Release` → 0 Fehler (245 Warnungen, unverändert zum Ausgangsstand).
- `dotnet test VideoWebPlayer.Tests` (vollständig, inkl. Playwright-E2E), zwei Läufe:
  - Lauf 1 (vor der letzten, reinen XML-Doc-Ergänzung an `ResolvePlaylistEntryRemovalAsync`):
    **1274 erfolgreich, 0 Fehler**, 4 min 34 s.
  - Lauf 2 (Stand der Commits): **1273 erfolgreich, 1 Fehler**, 3 min 57 s. Fehlgeschlagen ist
    `Components/MediaSearchSelectorTests.Search_NoResults_ShowsEmptyMessage` — ein bUnit-Test mit
    `WaitForAssertion`, der die Playlist- und Weiterschauen-Logik nicht berührt und in Lauf 1 grün war.
    Einzeln dreimal wiederholt: dreimal grün (je 6/6 der Klasse). Bewertung: Flackern unter Last, nicht
    durch diese Änderung verursacht. Der Test wurde nicht angefasst, nicht abgeschwächt und nicht
    deaktiviert.
- `python .githooks/razor-usage-check.py --all --strict` → OK
- `python .githooks/enum-coverage-check.py --all --strict` → OK
- `python .githooks/no-notimplemented-check.py --all --strict` → OK

## 4. Dokumentation

- `docs/help/playlists.md` — Abschnitt „Weiterschauen mit Playlist-Bezug": ein Eintrag je Playlist, Nachfolger
  ist der nächste Playlist-Titel, Verhalten am Playlist-Ende.
- `docs/help/playlists-business-rules.md` — neue Regeln **BR-35** (genau ein Eintrag je Anwender und Playlist,
  samt Begründung und Altbestands-Entscheidung) und **BR-36** (Nachfolger ist der nächste Playlist-Titel),
  dazu zwei Zeilen in der Zusammenfassungstabelle.
- `docs/help/playlists-ablauf-technisch.md` — neuer „Ablauf 9" mit Schritt-für-Schritt-Kette, Randfällen und
  beteiligten Klassen.
- `docs/help/weiterschauen/business-rules.md` — die beiden bisherigen Regeln („nur eine Episode je Serie",
  „Serienwechsel ist nicht möglich") ausdrücklich auf Einträge **ohne** Playlist-Bezug eingeschränkt; zwei
  neue Regeln für den Playlist-Fall ergänzt.
- `docs/help/weiterschauen/beschreibung.md` — Anwendersicht: ein Eintrag je Playlist, Nachfolger aus der
  Playlist, präzisierte Beschreibung von „Überspringen".
- `docs/API.md` — `POST /api/continue-watching/skip`: Nachfolger hängt von `playlistId` ab.
- `docs/RELEASE_NOTES.md` — je ein Eintrag in „What's New" und „Neuerungen" (Umlaute korrekt, CRLF erhalten,
  nur ergänzt).

## 5. Nachbesserung nach der Abnahme (`acceptance-korrektur-weiterschauen-playlist.md`)

Der Prüfer hat die drei Kundenszenarien im laufenden Programm bestätigt und vier Abweichungen gemeldet.
Alle vier sind aufgearbeitet; die Belege des Prüfers liegen im Scratchpad unter `abnahme/`.

### A2 (Fehler, behoben): Der entfernte Titel riss den Playlist-Eintrag wieder an sich

*Ursache:* `ResolvePlaylistSuccessorAsync` behandelte den Fall „Titel gehört nicht mehr zur Playlist" nur
bei der **Nachfolgerermittlung**. Der Schreibweg `UpsertAsync` legte den Eintrag trotzdem mit `PlaylistId`
an und löschte über die Ein-Eintrag-Regel den beim Entfernen gesetzten Ersatz (Beleg
`mangel2-2-nach-weitermeldung.txt`: `Episode=1 Playlist=1 Pos=00:11:00`). BR-36 und Ablauf 9 behaupteten
das Gegenteil — die Dokumentation war an dieser Stelle falsch.

*Behebung:* Neue Methode `ContinueWatchingService.NormalizePlaylistBindingAsync`. Eine
Fortschrittsmeldung mit `playlistId` für einen Titel, der kein `PlaylistEntry` dieser Playlist (mehr) ist,
verliert ihren Playlist-Bezug (`PlaylistId = null`) und fällt ab da unter die Regeln ohne Playlist (eigener
Eintrag, Nachfolger ist die nächste Episode der Serie). Geprüft wird in `ReportProgressAsync` (damit schon
der Pufferschlüssel stimmt) **und** in `ProcessBufferedEntryAsync` (der Titel kann die Playlist zwischen
Puffern und Verarbeiten verlassen; außerdem wird die Methode direkt vom Worker und von Tests aufgerufen).
Die Prüfung ist eine Punktabfrage auf den Unique-Index (`PlaylistId`, `MediaType`, `MediaId`).

*Kaskaden-Kinder und Betrachter:* Ein Kaskaden-Kindeintrag ist eine eigene `PlaylistEntry`-Zeile und behält
seinen Bezug (Test `ProgressForCascadeChildEntry_KeepsPlaylistBinding`). Die Abfrage hängt nicht am
Anwender, gilt also für Besitzer und Betrachter gleich; dass der Eintrag des Besitzers unberührt bleibt,
belegt `ViewerOfPublicPlaylist_ProgressForForeignTitle_DoesNotTouchOwnersPlaylistEntry`.

`ResolvePlaylistSuccessorAsync` behält seine eigene Prüfung als Absicherung für `SkipAsync`, das auf einem
bestehenden Eintrag arbeitet und nicht auf einer Meldung.

### A1 (bewusst so belassen, dokumentiert und abgesichert)

Spielt der Anwender einen bereits beendeten Titel derselben Playlist wieder an (zurückspulen und
pausieren, Player schließen), wird dieser Titel wieder der eine Eintrag der Playlist und der Nachfolger
entfällt. Kein Codeeingriff: Der Anwender ist aktiv wieder bei diesem Titel, es gilt „der letzte Schreiber
gewinnt", und ohne Playlist verhält sich die Liste genauso. Festgeschrieben als Geschäftsregel (BR-35,
Abschnitt „Erneutes Anspielen") und durch zwei Kontrolltests
(`ReplayingFinishedTitleOfSamePlaylist_BecomesThePlaylistEntryAgain`,
`ReplayingFinishedTitleWithoutPlaylist_BehavesTheSameWay`). Diese beiden Tests sind **keine**
Fehlerbehebung und deshalb auch ohne Codeeingriff grün — sie halten das gewollte Verhalten fest.

*Ehrliche Prüfung des Nachzügler-Verdachts (kann eine gepufferte Meldung dasselbe auslösen, ohne dass der
Anwender zurückgespult hat?):* Nein, nach Lesen von `continueWatching.js` und `ContinueWatchingBuffer`:

- `continueWatching.js` sendet bei `timeupdate` nur, wenn `pos - lastSent >= 3`, und bei `pause`, `ended`
  und `detach` erzwungen mit der **dann aktuellen** Position. Eine kleinere Position als zuletzt gesendet
  entsteht nur, wenn `videoEl.currentTime` zurückspringt — also durch ein echtes Zurückspulen.
- Beim Weiterschalten auf den nächsten Playlist-Titel lädt `VideoPlayer.razor` das Element zuerst neu
  (`currentTime` = 0); der erzwungene Abschlussversand meldet dann Position 0 und wird serverseitig von
  der 5-Sekunden-Grenze in `ReportProgressAsync` verworfen.
- `ContinueWatchingBuffer` hält je (Anwender, Film, Episode, Playlist) genau einen Schnappschuss und
  überschreibt ihn mit dem neueren; der Worker ist Einzelleser und findet denselben Schlüssel beim zweiten
  Lesen leer. Eine ältere Meldung kann eine neuere also nicht überholen. Belegt durch den Test
  `BufferedOutdatedReport_IsSupersededByLaterEndSequenceReport`.

Nicht ausschließen kann ich eine Umordnung **zweier HTTP-Anfragen auf dem Netzweg** (die ältere trifft
später ein als die neuere). Das ist kein Pufferproblem und habe ich nicht nachgestellt; es bleibt als
Restrisiko genannt.

### A3 (Aufwand, behoben)

Neue Methode `ShouldResolveSuccessorAsync`: Im Endsequenz-Zweig wird der Nachfolger nur ermittelt, wenn
tatsächlich ein Eintrag des beendeten Titels entfernt wurde **oder** die Playlist für diesen Anwender noch
gar keinen Eintrag hat (jemand springt direkt in die Endsequenz — die Lücke, die der Prüfer an der
einfachen Bedingung zu Recht benannt hat). Zeigt der eine Eintrag der Playlist schon auf einen anderen
Titel, ist nichts zu tun. Ohne Playlist bleibt es bei der bisherigen Punktabfrage, dort wurde nichts
geändert.

Beleg statt Messung: Die Testbasis zählt über einen Test-Double die Aufrufe von
`IPlaylistService.GetNextPlaylistEntryAsync`. `RepeatedEndSequenceReports_ResolveSuccessorOnlyOnce` schickt
zehn Meldungen durch die Endzone: **1 Aufruf** statt bisher 10 (Gegenprobe ohne die Abkürzung:
`Expected: 1 / Actual: 10`).

*Bekannte Grenze, ehrlich benannt:* Beim **letzten** Titel einer Playlist bleibt danach kein Eintrag übrig,
deshalb greift die Bedingung „Playlist hat noch keinen Eintrag" und jede weitere Meldung derselben Endzone
ermittelt erneut — ohne Wirkung auf das Ergebnis. Der Test
`EndSequenceOfLastPlaylistTitle_RemovesEntryEvenWithShortCircuit` hält das mit einer Zähler-Zusicherung
fest. Eine weitergehende Abkürzung hätte den Gesehen-Status als Kriterium gebraucht und damit das
Wiederanschauen eines Playlist-Finales verschlechtert; das war mir den Gewinn nicht wert.

### A4 (Anzeige des Altbestands, behoben)

`GetListAsync` gibt über `CollapseToOneEntryPerPlaylist` je Playlist nur den zuletzt aktualisierten Eintrag
aus (`UpdatedAt`, dann `Id`), in der bisherigen Listenreihenfolge. Einträge ohne Playlist-Bezug werden nie
zusammengefasst. **Rein lesend** — gelöscht wird nichts, denn ein Lesevorgang darf keinen Fortschritt
verwerfen (dieselbe Linie wie BR-30); die überzähligen Zeilen verschwinden weiterhin beim nächsten
Schreibvorgang. Der Test prüft beides: eine Kachel in der Liste, zwei Zeilen in der Datenbank.

### Neue Tests und Gegenproben der Nachbesserung

| Datei | Inhalt | Gegenprobe (Fix ausgebaut) |
|-------|--------|----------------------------|
| `Services/ContinueWatchingPlaylistBindingTests.cs` (5) | A2: entfernter Titel direkt und über den Puffer, Endsequenz des entfernten Titels, Kaskaden-Kind, Betrachter einer öffentlichen Playlist | **4 rot** (`ProgressForCascadeChildEntry_KeepsPlaylistBinding` ist Kontrolltest und bleibt grün) |
| `Services/ContinueWatchingPlaylistEndSequenceTests.cs` (6) | A1-Kontrolltests (mit und ohne Playlist), Puffer kann nicht überholen, A3-Zähler, Direktsprung in die Endzone, Playlist-Ende | **2 rot** (A3-Zähler: `Actual: 10` bzw. `Actual: 2`); die drei A1-/Puffer-Kontrolltests bleiben bewusst grün |
| `Services/ContinueWatchingPlaylistListViewTests.cs` (3) | A4: Altbestand zusammengefasst, verschiedene Playlists und Einträge ohne Playlist unberührt | **1 rot** (`Assert.Single() Failure: The collection contained 2 items`) |

Vorgehen der Gegenproben: `ContinueWatchingService.cs` gesichert, je Punkt genau die neue Stelle ausgebaut
(die beiden `NormalizePlaylistBindingAsync`-Aufrufe / der `ShouldResolveSuccessorAsync`-Wachposten / der
`CollapseToOneEntryPerPlaylist`-Aufruf), neu gebaut, Tests gelaufen, danach aus der Sicherung
wiederhergestellt.

### Geänderte bestehende Tests der Nachbesserung (mit Begründung)

Die A2-Regel hat 13 bestehende Tests rot gemacht. Alle 13 hatten dieselbe, in der Anwendung nicht
erreichbare Anordnung: Sie meldeten Fortschritt mit einer `playlistId` für ein Video, das gar nicht in
dieser Playlist stand. `PlaylistPlaybackContext` meldet eine `playlistId` aber nur für Titel dieser
Playlist. Geändert wurde deshalb **nur die Anordnung** — das Video wird der Playlist als `PlaylistEntry`
hinzugefügt —, keine einzige Zusicherung wurde abgeschwächt oder entfernt:

- `Services/ContinueWatchingServicePlaylistTests.cs` (3 Tests)
- `Services/ContinueWatchingServiceMultipleEntriesTests.cs` (3 Tests)
- `Services/ContinueWatchingServiceRemovalTests.cs` (2 Tests)
- `ContinueWatchingE2ETests.cs` (4 Tests: `E2E_Playlist_CreateAndReportProgress_EntryCreated`,
  `E2E_MultipleEntriesPlaylist_AllThreeVariantsVisible`, `E2E_MarkWatchedPlaylist_RemovesAllVariants`,
  `E2E_HideWithPlaylistId_OnlyRemovesMatching`)

Zusätzlich `E2E_PlaylistDeleted_EntryBecomesFree`: Der Test war nach der A2-Regel zwar grün, aber nur noch
trivial — sein Eintrag hätte gar keinen Playlist-Bezug mehr gehabt, den das Löschen der Playlist hätte
aufheben können. Auch dort ist das Video jetzt Teil der Playlist, damit der Test wieder prüft, was sein
Name sagt. Ehrliche Einordnung: Diese 14 Tests schlagen mit der korrigierten Anordnung auch **ohne** den
A2-Fix nicht fehl; den Nachweis führen die neuen Tests oben.

Erweitert wurden außerdem die Testhilfen: `PlaylistServiceTestBase.BuildContinueWatchingService` nimmt
optional einen Rückruf entgegen, mit dem `ContinueWatchingPlaylistTestBase` die Aufrufe von
`GetNextPlaylistEntryAsync` zählt (A3-Beleg).

### Dokumentation der Nachbesserung

- `docs/help/playlists-business-rules.md` — BR-36 „Sonderfall" sachlich korrigiert (Meldung ohne
  Playlist-Bezug statt „kein Nachfolger"), Abschnitt zum Aufwand ergänzt; BR-35 um „Erneutes Anspielen"
  und „Anzeige des Altbestands" erweitert.
- `docs/help/playlists-ablauf-technisch.md` — Ablauf 9: Normalisierung als eigener Schritt, Randfälle
  richtiggestellt (entfernter Titel, erneutes Anspielen, Altbestand beim Lesen), Aufwandshinweis.
- `docs/help/weiterschauen/business-rules.md`, `docs/help/weiterschauen/beschreibung.md`,
  `docs/help/playlists.md` — dieselben Aussagen in Anwendersprache.
- `docs/RELEASE_NOTES.md` — je ein ergänzender Eintrag in „What's New" und „Neuerungen".

### Läufe der Nachbesserung

- `dotnet build VideoPlayer.sln -c Debug` → 0 Fehler; `-c Release` → 0 Fehler (245 Warnungen, unverändert).
- `dotnet test VideoWebPlayer.Tests` (vollständig, inkl. Playwright-E2E), zwei Läufe:
  - Lauf 1 (vor dem Commit): **1288 erfolgreich, 0 Fehler**, 4 min 14 s.
  - Lauf 2 (Stand der Commits): **1287 erfolgreich, 1 Fehler**, 4 min 1 s. Fehlgeschlagen ist
    `Components/MediaSearchSelectorTests.EventCallback_OnMediaSelected_InvokedWithCorrectParameters` —
    wieder dieselbe bUnit-Klasse mit `WaitForAssertion`, die schon im ersten Durchgang dieser Aufgabe
    geflackert hat (dort mit einer anderen Methode derselben Klasse). Sie berührt die Playlist- und
    Weiterschauen-Logik nicht und wurde von dieser Änderung nicht angefasst. Einzeln dreimal
    wiederholt: dreimal grün (je 6/6 der Klasse). Bewertung: Flackern unter Last. Der Test wurde nicht
    angefasst, nicht abgeschwächt und nicht deaktiviert.
- `razor-usage-check.py`, `enum-coverage-check.py`, `no-notimplemented-check.py`, jeweils `--all --strict`
  → OK.

---

## 6. Restrisiken

- **Erneutes Anspielen eines beendeten Titels** verdrängt den Nachfolger (A1). Bewusst so; als
  Geschäftsregel dokumentiert und durch Kontrolltests festgehalten.
- **Umordnung zweier HTTP-Fortschrittsmeldungen auf dem Netzweg.** Trifft die ältere später ein als die
  neuere, gewinnt sie (letzter Schreiber). Über den Puffer ist das ausgeschlossen (Test), über das Netz
  nicht; nachgestellt habe ich es nicht. Eine Unterdrückung bräuchte den Meldezeitpunkt in
  `ProcessBufferedEntryAsync` (`ContinueWatchingBuffer.ProgressEntry.UpdatedAt` wird heute nicht
  durchgereicht).
- **Aufwand der Nachfolgerermittlung.** Je Titel nur noch ein Playlist-Ladevorgang statt rund zehn (Test).
  Ausnahme: der letzte Titel einer Playlist, siehe A3. Absolute Laufzeiten sind nicht gemessen.
- **Reentranz.** `GetNextPlaylistEntryAsync` kann über `LoadValidPlaylistEntriesAsync` die stille
  Waisen-Bereinigung (BR-7) und damit `ResolvePlaylistEntryRemovalAsync` auslösen — auf demselben
  `DbContext`. Im Endsequenz-Zweig sind die eigenen Löschungen vorher gespeichert, im Skip-Zweig wird der
  Nachfolger vor dem `Remove` ermittelt, so dass keine halbfertige Änderung mitgespeichert wird. Der Fall
  „Waise genau während des Nachfolger-Aufrufs" ist nicht eigens getestet; die bestehenden Waisen-Tests
  (`PlaylistServiceTests_RemoveMediaWithContinueWatching`) bleiben grün.
- **Kundenabnahme steht aus.** `customer-feedback.md` wurde nur gelesen, nicht abgehakt — das Abhaken liegt
  beim Kunden.
