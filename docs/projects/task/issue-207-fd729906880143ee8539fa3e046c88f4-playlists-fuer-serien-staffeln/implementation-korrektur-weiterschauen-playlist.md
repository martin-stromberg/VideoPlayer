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

- `dotnet build VideoPlayer.sln -c Release` → 0 Fehler (245 Warnungen, unverändert zum Ausgangsstand).
- `dotnet test VideoWebPlayer.Tests` (vollständig, inkl. Playwright-E2E) → **1274 erfolgreich, 0 Fehler**,
  4 min 34 s. Kein Test musste wiederholt werden, es gab keine flackernden Ausfälle.
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

## 5. Restrisiken

- **Verspätete Fortschrittsmeldung nach dem Wechsel.** Trifft nach dem Wechsel auf den nächsten Titel noch
  eine Meldung des alten Titels derselben Playlist ein, entsteht kein zweiter Eintrag (das war die
  Kundenmeldung), aber die Playlist zeigt dann kurzzeitig wieder den alten Titel — der letzte Schreiber
  gewinnt. Das entspricht der Semantik, die `UpdatedAt`/`ListOrder` ohnehin haben, und ist im technischen
  Ablauf dokumentiert. Eine Unterdrückung müsste den Zeitpunkt der Meldung mitführen
  (`ContinueWatchingBuffer.ProgressEntry.UpdatedAt` erreicht `ProcessBufferedEntryAsync` heute nicht) und
  war für die gemeldeten Szenarien nicht nötig. Kein Test deckt diesen Ablauf ab.
- **Mehrbelastung beim Ermitteln des Nachfolgers.** `GetNextPlaylistEntryAsync` lädt und sortiert die Einträge
  der Playlist und löst deren Zugänglichkeit auf. Das geschieht nur beim Erreichen der Endsequenz und beim
  Überspringen, also selten; bei sehr großen Playlists ist es trotzdem teurer als die bisherige
  Punktabfrage. Nicht gemessen.
- **Reentranz.** `GetNextPlaylistEntryAsync` kann über `LoadValidPlaylistEntriesAsync` die stille
  Waisen-Bereinigung (BR-7) und damit `ResolvePlaylistEntryRemovalAsync` auslösen — auf demselben
  `DbContext`. Im Endsequenz-Zweig sind die eigenen Löschungen vorher gespeichert, im Skip-Zweig wird der
  Nachfolger vor dem `Remove` ermittelt, so dass keine halbfertige Änderung mitgespeichert wird. Der Fall
  „Waise genau während des Nachfolger-Aufrufs" ist nicht eigens getestet; die bestehenden Waisen-Tests
  (`PlaylistServiceTests_RemoveMediaWithContinueWatching`) bleiben grün.
- **Kundenabnahme steht aus.** `customer-feedback.md` wurde nur gelesen, nicht abgehakt — das Abhaken liegt
  beim Kunden.
