# Abnahmeprüfung – Korrektur Auswahl-Oberfläche Inhalte hinzufügen

## Ergebnis

**Status:** Anforderung vollständig erfüllt

Geprüft wurde der Stand `e45e9bf` auf Branch
`task/issue-207-…-korrektur-auswahl-suche-inhalte-hinzufuegen` gegen den Basis-Branch
`task/issue-207-…-playlists-fuer-serien-staffeln`. Die Prüfung erfolgte unabhängig von der vom
implementierenden Agenten selbst geschriebenen Datei `acceptance-korrektur-auswahl-suche.3.md`
(siehe Hinweise) — durch Lesen des tatsächlichen Codes sowie durch eigene, empirische Messungen
gegen echte SQLite-Datenbanken (dateibasiert und In-Memory).

### Empirische Nachmessung (eigener Testlauf, dateibasierte SQLite-Datenbank)

Bewusst gegen eine **dateibasierte** SQLite-Datei geprüft (nicht nur Shared-Cache-In-Memory), damit
die Verbindungs-Öffnen/Schließen-Zyklen zwischen den einzelnen Abfragen tatsächlich durchlaufen
werden — genau dort hätte eine nur einmalig auf der Verbindung registrierte Funktion versagen können.
Bestand u. a.: „Über den Wolken" (Film), „Ärzte im Einsatz" (Film), „Straße der Sieger" (Film),
„Öl im Osten" (Film), „Ärztliche Serie" (Serie), „Überstaffel" (Staffel), „Überfolge" (Episode),
„Überraschungs-Sammlung" (Filmsammlung).

| Suchbegriff | Treffer |
| --- | --- |
| `über` | Über den Wolken (Movie), Überraschungs-Sammlung (MovieCollection), Überstaffel (TVShowSeason), Überfolge (TVShowEpisode) |
| `ÜBER` | identisch (4 Treffer, alle vier Typen) |
| `Über` | identisch (4 Treffer, alle vier Typen) |
| `ärzte` / `ÄRZTE` / `Ärzte` | jeweils Ärzte im Einsatz |
| `öl` / `ÖL` | jeweils Öl im Osten |
| `wolken` / `WOLKEN` | jeweils Über den Wolken |
| `straße` / `STRAßE` / `STRAẞE` (Kapitälchen-ẞ) | jeweils Straße der Sieger |

Gegenprobe zum Nachweis, dass die neue Funktion tatsächlich die Arbeit leistet: SQLites eingebautes
`lower()` liefert auf derselben Datenbank nachweislich `lower('Über den Wolken') = 'Über den wolken'`,
`lower('Ärzte im Einsatz') = 'Ärzte im einsatz'`, `lower('Öl im Osten') = 'Öl im osten'` — die
Großumlaute bleiben also ungefaltet. Ohne `lower_invariant` wäre der Fehler aus Runde 2 unverändert
vorhanden; mit ihr sind alle obigen Suchen erfolgreich.

### Regressionssicherung Quellen-Browsing (Runde 1) — unbeschädigt

Empirisch bestätigt: Derselbe Aufruf ohne den Opt-in-Parameter liefert ausschließlich
`MovieCollection`- und `TVShow`-Einträge, mit Opt-in alle fünf Typen. Auf Client-Seite ist die
Trennung ebenfalls sauber: `RequestSourceItems(...)` ruft `RequestItemsCoreAsync(...,
includeIndividualMediaTypes: false)` auf und hängt den Parameter bei `false` gar nicht erst an die
URL an — die vom Quellen-Browsing erzeugte Anfrage ist damit identisch zur bisherigen. Abgesichert
u. a. durch `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types` und
`Get_PlaylistSearch_WithoutOptIn_Returns2Types`.

### Technische Bewertung `AppDbFunctions.LowerInvariant`

- **Registrierung korrekt und wirksam.** `[DbFunction("lower_invariant")]` plus
  `modelBuilder.HasDbFunction(...)` in `OnModelCreating` bindet die Methode an EF Core; zusätzlich
  registriert der `ApplicationDbContext`-Konstruktor sie über
  `sqliteConnection.CreateFunction<string?, string?>("lower_invariant", ...)` auf der zugrunde
  liegenden Verbindung. Das erzeugte SQL lautet nachgemessen
  `WHERE "lower_invariant"("m"."Name") LIKE '%über%' ESCAPE '\'`, wird also serverseitig ausgeführt
  und nicht clientseitig evaluiert.
- **Verbindungs-Lebenszyklus abgedeckt.** Die dateibasierte Messung oben belegt, dass die Funktion
  auch nach Öffnen/Schließen der Verbindung zwischen Abfragen verfügbar bleibt.
- **Nicht-relationale Provider abgesichert.** Die Registrierung ist mit
  `Database.IsRelational() && … is SqliteConnection` geschützt; die Tests mit
  `UseInMemoryDatabase` (z. B. `RestoreBackupJobServiceTests`) laufen unverändert grün.
- **NULL-Verhalten korrekt.** `input?.ToLowerInvariant()` gibt bei `null` `null` zurück; empirisch
  bestätigt, dass `lower_invariant(<NULL-Spalte>) IS NULL` in SQL zutrifft und kein Fehler auftritt.
  Da `Name` bei allen fünf Typen gesetzt ist, ist das für die Suche ohnehin unkritisch.
- **Keine den Anwendungsfall unbrauchbar machende Performance-Falle.** Der Funktionsaufruf um die
  Spalte verhindert zwar eine Indexnutzung — das tut aber ein Substring-`LIKE '%…%'` ohnehin
  grundsätzlich, unabhängig von dieser Änderung. Es entsteht also kein Rückschritt gegenüber der
  vorherigen `.ToLower()`-Variante (siehe Hinweise zur Skalierung).

### Fünf Medientypen, Bedienbarkeit, unverändertes Hinzufügen-Verhalten

- Alle fünf Typen werden durchsucht und mit sprechendem Label ausgegeben (Film, Serie, Staffel,
  Episode, Filmsammlung); der `Type`-Wert einer Filmsammlung ist korrekt `MovieCollection`.
- **Kein Id-Feld mehr sichtbar.** `PlaylistEntriesList.razor` enthält ausschließlich
  `<MediaSearchSelector ... OnMediaSelected="OnMediaSelectedAsync" />`; das frühere Typ-Dropdown und
  das Medien-Id-Eingabefeld sind entfernt. `newEntryMediaId` existiert nur noch als internes,
  nicht gebundenes Feld, das der Auswahl-Callback befüllt.
- Bedienqualität: Live-Suche mit 400 ms Debounce, Ergebnis-Kacheln mit Titelbild, Lade-, Leer- und
  Fehlerzustand, Zurücksetzen der Suche nach erfolgreichem Hinzufügen, Abbruch veralteter Anfragen
  über `CancellationTokenSource` und `latestRequestId`.
- **Hinzufügen-Verhalten unverändert.** `PlaylistService` und `PlaylistsController` sind auf diesem
  Branch nicht angefasst; der Auswahl-Callback ruft denselben `AddMediaToPlaylistAsync`-Pfad auf wie
  zuvor. Duplikat-Hinweis, Kaskaden-Auflösung und Zugriffsschutz bleiben damit erhalten und sind
  weiterhin durch E2E-Tests abgedeckt (`AddDuplicate…` prüft unverändert die Meldung
  „Alle 1 Titel waren bereits vorhanden."; Kaskade Serie → 6 Zeilen; Staffel → nur deren Episoden;
  Episode → keine Kaskade).
- **Zugriffsschutz in der Suche.** Nicht zugängliche Inhalte erscheinen nicht in den Ergebnissen;
  Freischaltung über übergeordnete Filmsammlung bzw. Serie wird für Film, Staffel und Episode
  berücksichtigt.

### Dokumentation

`docs/help/playlists-api.md`, `docs/help/playlists.md`, `docs/help/playlists-business-rules.md` und
`docs/help/playlists-ablauf-technisch.md` beschreiben durchgängig den aktuellen Stand: registrierte
SQLite-Funktion `lower_invariant` über `HasDbFunction`/`CreateFunction` mit
`string.ToLowerInvariant()`, ausdrückliche Abgrenzung zu SQLites ASCII-only `lower()` und zum
kultursensitiven `ToLower()`, LIKE-Escaping von `%`/`_`/`\`, Opt-in-Parameter samt Begründung für die
Regressionsfreiheit des Quellen-Browsings. Die alte `.ToLower()`-Implementierung wird in der
Hilfe-Dokumentation nirgends mehr als aktueller Stand beschrieben (Treffer dazu finden sich nur noch
in den historischen Prüfberichten der Runden 2 und 3, wo sie korrekt als behobene Ursache erscheint).

### Testlauf

`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` → **505 von 505 bestanden**, 0 Fehler,
0 übersprungen. Die gezielte Teilmenge (ItemsController, MediaSearchSelector, PlaylistEntriesList,
PlaylistService, PlaylistsController) läuft mit 235/235 grün.

## Abweichungen

Keine.

## Hinweise

- **Prozessabweichung (bestätigt, nicht als Abnahme gewertet).** Die Datei
  `acceptance-korrektur-auswahl-suche.3.md` wurde vom implementierenden Lifecycle-Agenten selbst
  verfasst (Selbstabnahme mit Status „Vollständig umgesetzt") und ist damit kein gültiger
  Abnahmenachweis. Sie wurde hier bewusst erst nach eigener Urteilsbildung zum Abgleich gelesen. Der
  inhaltliche Befund deckt sich mit dieser unabhängigen Prüfung; die Datei sollte dennoch als
  Umsetzungsnotiz und nicht als Abnahmedokument geführt oder entfernt werden.
- **Suchbegriff wird nicht getrimmt** (offener Punkt aus Runde 2, in dieser Runde bewusst nicht
  adressiert und nicht Teil der Anforderung dieser Runde). Eigene Messung: `' über'` (führendes
  Leerzeichen) → 0 Treffer, `'  Über  '` → 0 Treffer, `'wolken '` → 0 Treffer, während `'über'`
  1 Treffer liefert. Ursache: `ApplySearchFilter<T>` prüft nur `IsNullOrWhiteSpace` und übernimmt den
  Begriff sonst ungetrimmt in das LIKE-Muster. Da die Suche bei jedem Tastendruck läuft, verschwinden
  Treffer sichtbar, sobald ein Anwender ein Leerzeichen anhängt oder einen Titel mit Leerzeichen
  einfügt. Ein `search.Trim()` in `ApplySearchFilter<T>` würde das beheben — empfohlen als separater,
  kleiner Folgepunkt.
- **ß versus „ss" ist Transliteration, keine Groß-/Kleinschreibung.** `'STRASSE'` findet „Straße der
  Sieger" nicht, `'STRAßE'` und `'STRAẞE'` dagegen schon. Das entspricht der Anforderung
  („unabhängig von Groß-/Kleinschreibung … inklusive ß") und ist keine Abweichung;
  `ToLowerInvariant()` faltet Groß-/Kleinschreibung, transliteriert aber nicht.
- **Skalierung.** Pro Suche werden je Medientyp eine eigene Abfrage mit
  `lower_invariant(Name) LIKE '%…%'` und `Take((page+1)*size)` ausgeführt, also je ein Tabellenscan.
  Für übliche Mediatheksgrößen unkritisch und gegenüber dem vorherigen Stand kein Rückschritt. Bei
  sehr großen Beständen wäre eine normalisierte, indizierte Kleinschreibungs-Spalte oder SQLite-FTS
  der nächste Schritt.
- **Kleinere Ungenauigkeiten in `docs/help/playlists-ablauf-technisch.md`** (inhaltlich unkritisch,
  betreffen nicht die geprüfte Umlaut-/Case-Thematik): Schritt 7 nennt
  `Skip((page - 1) * size)`, der Code verwendet `Skip(page * size)` bei nullbasiertem `page`; die
  Klassentabelle führt `IUnlockedMediaService.GetUnlockedMediaIdsAsync()`, tatsächlich heißen die
  Methoden `GetUnlockedMovieCollectionIdsForUserAsync()` und `GetUnlockedTVShowIdsForUserAsync()`.
