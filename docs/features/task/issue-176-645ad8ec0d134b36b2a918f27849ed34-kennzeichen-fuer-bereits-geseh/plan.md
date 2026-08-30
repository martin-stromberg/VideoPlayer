# Umsetzungsplan

## 1. Benutzerbezogenes Gesehen-Modell und Datenbankregeln

- Eine eigene Entitaet `WatchedEntry` unter `VideoWebPlayer/Data/Entities/` anlegen. Sie enthaelt `Id`, die verpflichtende `UserId`, die optionalen Fremdschluessel `MovieId` und `TVShowEpisodeId` sowie `WatchedAt` als UTC-Zeitpunkt.
- In `WatchedEntryConfiguration` die fachliche Genau-ein-Titel-Invariante als Datenbank-Check-Constraint abbilden: Entweder ist `MovieId` gesetzt und `TVShowEpisodeId` ist `null`, oder `TVShowEpisodeId` ist gesetzt und `MovieId` ist `null`. Datensaetze mit beiden gesetzten oder beiden leeren Fremdschluesseln muessen bereits durch die Datenbank abgewiesen werden.
- Zwei eindeutige, gefilterte Indizes fuer `(UserId, MovieId)` beziehungsweise `(UserId, TVShowEpisodeId)` konfigurieren. Dadurch kann je Benutzer und konkretem Film beziehungsweise konkreter Episode nur ein Gesehen-Datensatz existieren. Zusaetzliche Abfrageindizes beginnen jeweils mit `UserId`.
- Foreign Keys zu `ApplicationUser`, `Movie` und `TVShowEpisode` mit der im Projekt verwendeten Loeschstrategie konfigurieren. `ApplicationDbContext` um `DbSet<WatchedEntry>` und `ApplyConfiguration` erweitern.
- `ApplicationDbContext.DeleteMediaSourceAsync` so erweitern, dass Gesehen-Datensaetze der zu loeschenden Filme und Episoden innerhalb derselben Transaktion vor den Medien entfernt werden. Fortschrittszaehlung und bestehende Loeschtests entsprechend anpassen; Eintraege anderer Quellen und Benutzer bleiben erhalten.
- Eine EF-Core-Migration samt Model-Snapshot erzeugen. Die Migration erstellt Tabelle, Check-Constraint, Foreign Keys und beide Unique-Indizes; vorhandene Medien erhalten keine rueckwirkende Gesehen-Markierung.
- Die objektbasierte Backup-/Restore-Logik in `VideoWebPlayerBackupData` aufnehmen: Die neue Tabelle wird durch die EF-Tabellenermittlung exportiert und fuer alte Backups als optionale Restore-Tabelle behandelt. Backup-/Restore-Tests pruefen sowohl den Roundtrip vorhandener `WatchedEntry`-Datensaetze als auch die Wiederherstellung eines Alt-Backups ohne diese Tabelle.

## 2. Gesehen-Service und benutzerbezogene Statusanreicherung

- Einen `WatchedStatusService` mit klaren Operationen einfuehren:
  - `MarkWatchedAsync(userId, movieId, episodeId, watchedAtUtc)` validiert genau einen Titelbezug und legt den Datensatz an beziehungsweise aktualisiert den vorhandenen `WatchedAt`-Wert ohne Duplikat.
  - Eine gebuendelte Leseoperation nimmt Mengen von Film- und Episoden-IDs entgegen und liefert ausschliesslich die Gesehen-Zeitpunkte des uebergebenen Benutzers, getrennt nach Titeltyp.
  - Leere ID-Mengen erzeugen keine Abfrage; konkurrierende beziehungsweise wiederholte Abschlussmeldungen verletzen die Unique-Indizes nicht und enden mit genau einem Datensatz.
- Den Service ueber Dependency Injection registrieren. Aufrufer beziehen `userId` immer aus dem authentifizierten Principal beziehungsweise dem bereits validierten serverseitigen Benutzer; eine vom Browser gelieferte Benutzer-ID wird nicht akzeptiert.
- `DtoMediaEntry`, `ContinueWatchingDto` und den fuer Quellenkarten verwendeten `MediaEntryDto` um einen nullable UTC-Wert `WatchedAt` erweitern. Die UI leitet `IsWatched` ausschliesslich aus `WatchedAt != null` ab; fuer Sammlungen, Serien und Staffeln bleibt der Wert `null`.
- Die DTO-Erzeugung fuer folgende Datenfluesse nach dem Laden der Titel jeweils mit einer gebuendelten Statusabfrage anreichern, nicht mit einer Abfrage pro Karte:
  - Film-Sammlungsdetails: alle enthaltenen `DtoMovie`-Objekte.
  - Serien-/Staffeldetails: alle enthaltenen `DtoTVShowEpisode`-Objekte.
  - Favoriten in `FavoritesService` fuer favorisierte Filme und Episoden.
  - neue Eintraege in `ItemsController.GetRecent` beziehungsweise `RecentEntryService` fuer Filme und Episoden.
  - `ContinueWatchingService.GetListAsync` fuer Movie- und Episode-Eintraege.
  - saisonale Genres fuer die dort tatsaechlich als Einzelkarte ausgegebenen Filme.
  - Quelleninhalt in `ItemsController.Get`: Ein Gesehen-Zeitpunkt wird nur gesetzt, wenn eine Karte einen konkreten Film oder eine konkrete Episode repraesentiert; reine Collection-/Show-Karten bleiben unmarkiert.
- Die Statusanreicherung fuer verschachtelte DTOs zentral in einer Helper-/Service-Methode halten, damit dieselbe Benutzerfilterung fuer Startseite, Sammlungs- und Seriendetails gilt. Bestehende Favoriten-, Freigabe- und Continue-Watching-Felder werden nicht veraendert.

## 3. Automatische Markierung an der vorhandenen Endschwelle

- `ContinueWatchingService.ProcessBufferedEntryAsync` im bestehenden Zweig `duration - position <= endThreshold` um den Aufruf von `MarkWatchedAsync` erweitern. Der Titelbezug stammt aus `movieId` beziehungsweise `episodeId`; `WatchedAt` wird mit einem injizierbaren UTC-Zeitgeber erzeugt, damit der Zeitpunkt deterministisch testbar ist.
- Ausschliesslich `ProgramSettingsService.GetContinueWatchingEndThresholdAsync` und damit `Setup.ContinueWatchingEndThresholdSeconds` verwenden. Im Browser und in `continueWatching.js` entsteht keine zweite Schwellenlogik.
- Markierung, Entfernen des aktuellen Continue-Watching-Eintrags und gegebenenfalls Anlegen des Folgemediums in einer konsistenten SaveChanges-/Transaktionsfolge ausfuehren. Nach erfolgreicher Verarbeitung innerhalb der Schwelle existiert die Gesehen-Markierung auch dann, wenn kein vorheriger Continue-Watching-Eintrag vorhanden war.
- Wiederholte `timeupdate`-, `pause`- und `ended`-Meldungen innerhalb der Schwelle sind idempotent: Es bleibt genau ein Datensatz je Benutzer/Titel bestehen, sein UTC-Zeitpunkt entspricht der zuletzt erfolgreich verarbeiteten Markierung, und Folgemedium sowie SignalR-Aktualisierung werden nicht dupliziert.
- Ausserhalb der Schwelle bleibt der Titel ungesehen und der bisherige Fortschrittsfluss unveraendert. Die vorhandene Logik fuer naechsten Film, naechste Episode, Staffelwechel und Serienende bleibt erhalten.

## 4. Einheitliche Kennzeichnung in allen Titelauflistungen

- `MediaBox.razor` um den optionalen Parameter `WatchedAt` erweitern. Bei gesetztem Wert wird `Images/gesehen64x64.png` als dekoratives Auge mit einer stabilen Testkennung, zum Beispiel `data-testid="watched-indicator"`, gerendert; ohne Wert entsteht kein entsprechendes DOM-Element.
- In `VideoWebPlayer/wwwroot/app.css` ein gemeinsames Overlay-Styling definieren: absolut rechts oben, oberhalb des Posters, mit fest begrenzter responsiver Groesse, ausreichendem Kontrast und `pointer-events: none`. Das Auge darf weder Schnellstart, Kontextmenue, Download-Aktion noch Titeltext verdecken. Fuer abweichende Kartenformen wird dieselbe visuelle Klasse verwendet.
- `MediaBaseEntryList.razor` erhaelt die gebuendelt geladenen Filmstatuswerte und reicht den passenden Wert an `MediaBox` weiter. Collection-, TVShow- und Season-Eintraege erhalten keinen Status.
- Die vier Startseitenlisten binden den Status an die gemeinsame Karte:
  - `FavoritesList`: Film und Episode; andere favorisierte Typen unveraendert ohne Auge.
  - `RecentEntriesList`: Film und Episode; Collection, TVShow und Season ohne Auge.
  - `ContinueWatchingList`: Film und Episode.
  - `SeasonalGenreList` ueber `MediaBaseEntryList`: einzelne Filme; Collections und TVShows ohne Auge. Episoden werden von dieser Liste derzeit nicht angeboten.
- Die vom Quelleninhalt aus erreichbaren konkreten Titellisten ebenfalls abdecken:
  - `MovieCollectionDetails.razor`: jede Movie-Karte erhaelt den Status des konkreten Films.
  - `TVShowDetails.razor`: jede `episode-box` erhaelt fuer die konkrete Episode dasselbe Auge rechts oben.
  - `MediaSourceDetails.razor`: den gemeinsamen Indikator fuer konkrete Movie-/Episode-Karten anbinden; derzeit gelieferte Collection-/TVShow-Karten bleiben mangels eigenem Film-/Episodenstatus unmarkiert.
- Eine Repository-Suche nach allen Verwendungen von `MediaBox`, `.media-box`, `.episode-box`, `DtoMovie`-Listen und `DtoTVShowEpisode`-Listen als Implementierungs-Check wiederholen. Jede weitere fachliche Film-/Episodenliste muss den gemeinsamen `WatchedAt`-Wert und dieselbe Overlay-Klasse verwenden; Verwaltungs-, Suchauswahl- oder Detailheader ohne Titelauflistung sind nicht betroffen.
- Nach Navigation oder Reload werden Statuswerte erneut serverseitig geladen. Es wird kein rein lokaler Komponentenstatus als Persistenznachweis verwendet.

## 5. Testdaten und wiederverwendbare Testhilfen

- Eine gemeinsame `WatchedStatusE2E`-Fixture auf Basis der vorhandenen WebApplicationFactory-/Playwright-Hilfen anlegen beziehungsweise erweitern. Sie startet die Anwendung mit relationaler Testdatenbank und stellt folgende Daten bereit:
  - Benutzer A und Benutzer B mit Zugriff auf dieselbe Quelle.
  - Eine Film-Sammlung mit je einem gesehenen und ungesehenen Film sowie eine Serie/Staffel mit je einer gesehenen und ungesehenen Episode.
  - `WatchedEntry`-Datensaetze nur fuer Benutzer A und nur fuer die als gesehen gekennzeichneten Titel.
  - Favoriten-, Recent- und Continue-Watching-Eintraege fuer die vier Titel sowie ein saisonales Genre fuer die Filme.
  - Einen nicht standardmaessigen Endschwellenwert, zum Beispiel 17 Sekunden, und abspielbare Testmedien fuer Film und Episode.
- Hilfsmethoden fuer `LoginAsync(user)`, Navigation zu Quelle/Sammlung/Serie, `ReloadAndWaitForListAsync`, Kartenlokalisierung ueber stabile `data-*`-Kennungen und `PlayUntilThresholdAsync(mediaType, mediaId, remainingSeconds)` bereitstellen. Die Wiedergabehilfe benutzt den echten Player-/Progress-Endpunkt im authentifizierten Browserkontext und wartet auf die serverseitige Pufferverarbeitung.
- Eine gemeinsame Assertion prueft je Karte Vorhandensein oder Fehlen des Auges. Eine Layout-Assertion vergleicht die Bounding Boxes von Karte und Auge und weist nach, dass das Auge im rechten oberen Kartenbereich liegt, innerhalb der Karte bleibt und sich nicht mit bedienbaren Schnellstart-, Kontextmenue- oder Download-Flaechen ueberschneidet.

## 6. Persistenz-, Service- und Integrationstests

- Relationale Tests fuer `WatchedEntry` ergaenzen; ein reiner EF-InMemory-Test reicht fuer Constraints und Unique-Indizes nicht aus:
  - Film- und Episodenstatus speichern und mit unveraendertem UTC-`WatchedAt` erneut lesen.
  - Benutzer A und B koennen fuer denselben Titel getrennte Datensaetze besitzen; die gebuendelte Abfrage fuer A liefert nie B-Daten.
  - Beide Fremdschluessel gesetzt und beide Fremdschluessel `null` werden durch den Check-Constraint abgewiesen.
  - Ein zweiter Datensatz fuer dieselbe `(UserId, MovieId)`- oder `(UserId, TVShowEpisodeId)`-Kombination wird abgewiesen; wiederholtes `MarkWatchedAsync` verwendet dagegen denselben Datensatz.
- `WatchedStatusServiceTests` pruefen gebuendelte gemischte Film-/Episode-Abfragen, leere Mengen, unbekannte IDs, Benutzerisolation, UTC-Zeitpunkt und wiederholte beziehungsweise parallele Markierungen.
- Tests fuer `ContinueWatchingService.ProcessBufferedEntryAsync` mit konfigurierbaren 17 Sekunden anlegen:
  - Film und Episode bei 18 Sekunden Rest bleiben ungesehen und behalten den normalen Fortschritt.
  - Film und Episode bei genau 17 Sekunden sowie darunter werden mit Zeitpunkt markiert.
  - Wiederholte Abschlussmeldungen erzeugen nur einen Gesehen-Datensatz.
  - Ein Gesehen-Datensatz entsteht auch ohne vorhandenen Continue-Watching-Eintrag.
  - naechster Film, naechste Episode, Staffelwechel, Serienende und SignalR-Nachrichten behalten ihr bestehendes Verhalten.
- `ApplicationDbContextTests.DeleteMediaSourceAsync_RemovesSourceAndAllRelatedEntities` um Gesehen-Eintraege fuer Film und Episode erweitern und `DeleteMediaSourceAsync_DoesNotAffectOtherSources` um den Gegenbeweis fuer andere Quellen/Benutzer ergaenzen.
- `VideoWebPlayerBackupDataTests` um Roundtrip und Alt-Backup-Kompatibilitaet der neuen optionalen Tabelle erweitern.
- DTO-/Controller-/Komponententests pruefen, dass alle gebuendelten Datenfluesse nur `WatchedAt` des authentifizierten Benutzers setzen und Collection, TVShow und Season niemals versehentlich als gesehen kennzeichnen.

## 7. Konkrete E2E-Abdeckungsmatrix

| Titelauflistung / Benutzerfluss | Medientypen | Setup und Aktion | Sichtbarer Nachweis |
|---|---|---|---|
| Quelle -> Film-Sammlung (`MovieCollectionDetails`) | Film | Als Benutzer A Quelle oeffnen, Sammlung waehlen; ein Film hat `WatchedAt`, ein Film nicht. Danach Seite neu laden. | Auge nur am gesehenen Film, rechts oben und auch nach Reload; Benutzer B sieht an beiden Filmen kein Auge. |
| Quelle -> Serie/Staffel (`TVShowDetails`) | Episode | Als Benutzer A Quelle oeffnen, Serie und Staffel waehlen; eine Episode hat `WatchedAt`, eine nicht. Danach Staffelansicht neu laden. | Auge nur an der gesehenen Episode, rechts oben und persistent; Benutzer B sieht kein Auge. |
| `FavoritesList` | Film und Episode | Je Typ einen gesehenen und ungesehenen Favoriten fuer Benutzer A anlegen, Startseite oeffnen und Liste neu laden. | Auge an gesehenem Film und gesehener Episode; kein Auge an beiden ungesehenen Titeln oder anderen Favoritentypen. |
| `RecentEntriesList` | Film und Episode | Je Typ gesehenen und ungesehenen Recent-Eintrag anlegen, Startseite oeffnen und erneut laden. | Derselbe positive und negative Nachweis innerhalb der Liste `Neu im Programm`. |
| `ContinueWatchingList` | Film und Episode | Je Typ gesehenen und ungesehenen Continue-Watching-Eintrag direkt als Testdaten anlegen und Startseite oeffnen. | Auge nur an den beiden gesehenen Karten; Kontextmenue und Kartennavigation bleiben bedienbar. |
| `SeasonalGenreList` / `MediaBaseEntryList` | Film | Gesehenen und ungesehenen Einzel-Film demselben aktiven Genre zuordnen; zusaetzlich Collection/TVShow aufnehmen. | Auge nur am gesehenen Einzel-Film; ungesehener Film, Collection und TVShow ohne Auge. |
| Automatischer Filmabschluss | Film | Film anfangs ohne `WatchedEntry`; Schwelle auf 17 Sekunden setzen; bis 18 Sekunden Rest spielen und fehlendes Auge pruefen, danach bis 17 Sekunden Rest spielen; zur Sammlung navigieren und reloaden. | Vor Schwelle kein Datenbankeintrag/kein Auge; an der Schwelle genau ein UTC-Zeitpunkt und sichtbares Auge rechts oben. |
| Automatischer Episodenabschluss | Episode | Episode anfangs ungesehen; denselben 18-/17-Sekunden-Ablauf ueber den Episode-Player ausfuehren; Staffelansicht anschliessend oeffnen oder aktualisieren. | Vor Schwelle kein Auge; danach genau ein persistierter Zeitpunkt und Auge an genau dieser Episode. |
| Responsive und Bedienbarkeit | Film- und Episodenkarten aller obigen Formen | Die positiven UI-Szenarien bei 1440x900 und 390x844 ausfuehren; Hover/Fokus, Klick, Long-Press-Kontextmenue und Episoden-Download betaetigen. | Overlay bleibt innerhalb der rechten oberen Ecke, Text und Aktionen bleiben sichtbar und ausloesbar, kein horizontaler Overflow. |

- Die vier Startseitenzeilen werden als getrennte Tests oder klar benannte parameterisierte Faelle umgesetzt, sodass ein Ausfall einer einzelnen Liste direkt erkennbar ist.
- Film- und Episodenabschluss sind zwei eigenstaendige E2E-Tests. Beide pruefen Ausgangszustand, nicht standardmaessige Konfiguration, Wiedergabeaktion, serverseitig persistierten Zeitpunkt, anschliessende Navigation/Reload und sichtbares Auge.
- Der Benutzerwechsel A -> B erfolgt mindestens fuer die Quellen-/Detailfluesse und einen Startseitenfall im Browser, nicht nur auf Serviceebene.

## 8. Bestehende Regressionstests und Gesamtabnahme

- Die laut Inventar betroffenen vorhandenen Tests explizit ausfuehren und bei erforderlichen Konstruktor-/Fixture-Aenderungen anpassen:
  - `ContinueWatchingE2ETests`: `HappyPath_EpisodeCompleted_NextEpisodeAppearsInContinueWatchingList`, Episode-Gap, Staffelwechel und Serienende.
  - `ContinueWatchingContextMenuActionTests`: Hide, Episode-Skip, Last-Episode-Skip und Movie-Skip.
  - `ContinueWatchingServiceGetNextEpisodeTests`: die gesamte Klasse fuer Sortierung, Luecken, Staffelwechel, Serienende und Schleifenregression.
  - `ContinueWatchingServiceSignalRTests`: NewEntry, UpdateExisting, Episode, MultipleUpdates und BufferFlow.
  - `ApplicationDbContextTests`: alle `DeleteMediaSourceAsync_*`-Faelle.
  - `MediaBoxContextMenuInteractionE2ETests`: Long-Press, Abbruch, Tastatur, Klick ausserhalb und Action-Auswahl.
  - `MediaBoxContextMenuPositionE2ETests`: erste und letzte Karte in allen vorhandenen Viewports.
- Zusaetzlich die neue `WatchedStatusE2ETests`-Klasse vollstaendig auf Desktop und Mobile ausfuehren. Reine Unit-/Integrationstests ersetzen keinen fehlenden E2E-Nachweis fuer Quellen-, Startseiten- oder Wiedergabefluesse.
- Abschliessend `dotnet test` fuer die gesamte Solution ausfuehren und fehlgeschlagene, uebersprungene oder mangels Browser/Runtime nicht ausgefuehrte E2E-Szenarien explizit dokumentieren.

## 9. Umsetzungsreihenfolge

1. `WatchedEntry`, Konfiguration, DbContext, Migration, Loesch- und Backup-/Restore-Anbindung implementieren.
2. `WatchedStatusService`, UTC-Zeitgeber, DI-Registrierung und Persistenz-/Service-Tests umsetzen.
3. DTOs und gebuendelte Statusanreicherung fuer Details, Quelle und alle vier Startseitenlisten ergaenzen.
4. Automatische Markierung in `ContinueWatchingService` integrieren und Endschwellen-/Regressionstests ausbauen.
5. Gemeinsames Auge-Overlay in `MediaBox`, Film-Sammlungs- und Episodenlisten anbinden und responsive Styles ergaenzen.
6. E2E-Fixture, Abdeckungsmatrix und Film-/Episoden-Wiedergabefluesse implementieren und ausfuehren.
7. Repository-weiten Listen-Audit, vollstaendigen Testlauf sowie Plan- und Code-Review durchfuehren; danach Dokumentation und Release Notes aktualisieren.

## Offene Punkte

Keine.
