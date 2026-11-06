# Umsetzungsplan: Programmauskunft (Schritt 5)

## Übersicht

Neue, eigenständige Komponente „Programmauskunft" (`ProgramGuide*`): ein zustandsloser,
rein zeitbasierter Lesedienst über dem Sendeplan (`ProgramSlot`, nur `Scheduled`), dazu eine
abgesicherte HTTP-Schnittstelle für angemeldete Benutzer (`api/program-guide`) mit aktuellem
Stand („Jetzt/Gleich": laufende Sendung, Position, Restdauer, nächste Sendung) und begrenzter
Vorschau der kommenden Stunden. Keine Schema-/Konfigurationsänderungen, keine UI-Änderungen —
die Live-TV-Ansicht ist Schritt 7.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Benennung | `ProgramGuide` (Service `IProgramGuideService`/`ProgramGuideService`, Controller `ProgramGuideController`, DTOs `DtoProgramGuide*`), fachlich „Programmauskunft" | Anforderung verlangt eigenen Begriff, klar von Benutzer-Playlists abgegrenzt; `ProgramGuide` ist der EPG-Standardbegriff |
| Service-Ergebnistypen | Client-DTOs direkt (`DtoProgramGuideNow`, `DtoProgramGuideEntry`) | Projekt-Konvention: `IPlaylistService` liefert ebenfalls `DtoPlaylist*` — kein zusätzlicher Mapping-Layer |
| Zeitpunkt-Parameter | `utcNow` explizit an den Service übergeben (wie bei `IProgramScheduleService`); Controller übergibt `DateTimeOffset.UtcNow` | Zustandslosigkeit ist Kern der Anforderung; expliziter Parameter macht die Auskunft rein deterministisch und ohne Zeit-Mock testbar |
| Kanal-Zugriff | `ProgramGuideService` liest den Sender direkt (`_db.TVChannels` erster Datensatz); **kein** `GetOrCreateChannelAsync` | Die Auskunft ist eine reine Leseoperation — sie darf bei leerem Bestand keinen Sender anlegen (Seiteneffekt auf GET). Ergebnis ohne Sender: leere Auskunft |
| Vorschau-Begrenzung | Harte Obergrenze `ProgramGuideService.MaxPreviewHours = 12` (Konstante); `hours` außerhalb `1..12` → `ArgumentOutOfRangeException` → 400; Default `hours=6` | „Begrenzte Übersicht" ist fachliche Schranke, die der volle administrativer Sendeplan abgrenzt; kein Setup-Feld verlangt |
| Eintragsmodell | Ein einziges `DtoProgramGuideEntry` für laufende wie kommende Sendungen; `PositionSeconds`/`RemainingSeconds` nullable und nur beim laufenden Eintrag gesetzt | Ein Modell für „Jetzt/Gleich" und Vorschau; Position existiert nur für den laufenden Slot |
| Titelauflösung | Batch-Lookup wie `TVSenderAdmin.razor` (Movie-Ids → `Movies.Name`, Episoden-Ids → `TVShowEpisodes` inkl. `TVShowSeason.TVShow`, Format `{Show} — {Season}, E{Number}: {EpisodeName}`); Fallback „Unbekannt" bei leerlaufender weicher Referenz | Bewährtes Anzeigeformat der Admin-Ansicht; konsistente Titel in Admin- und Benutzerkontext |
| Fehlerbehandlung Controller | `ExecuteAsync`-Muster aus `PlaylistsController` (Unauthorized→401, Argument→400, sonst→500 mit deutschem Log-Kontext) | Bestehende Konvention, kein neues Fehlergerüst |

## Programmabläufe

### Auskunft „Jetzt/Gleich" (`GET api/program-guide/now`)

1. `BearerTokenCheck` validiert das JWT (Header oder `access_token`-Query); anonym → 401.
2. Controller: `CheckLogedIn()`; `utcNow = DateTimeOffset.UtcNow`; Aufruf `IProgramGuideService.GetNowAsync(utcNow, RequestAborted)`.
3. Service: ersten `TVChannel`-Datensatz lesen (kein Sender → `Now`/`Next` = `null`, `ServerTimeUtc` gesetzt).
4. Laufender Slot: `Scheduled` und `Start <= utcNow && End > utcNow` (erster nach `Start`). Nächster Slot: `Scheduled` und `Start >= utcNow` (erster nach `Start`).
5. Titel per Batch-Lookup auflösen; `Now`-Eintrag zusätzlich mit `PositionSeconds = utcNow - Start`, `RemainingSeconds = End - utcNow`.
6. `DtoProgramGuideNow { ServerTimeUtc, Now, Next }` zurückgeben → `200 OK`.

Beteiligte Klassen: `ProgramGuideController`, `ProgramGuideService`, `ApplicationDbContext`, `Movie`, `TVShowEpisode`.

### Begrenzte Programmübersicht (`GET api/program-guide/upcoming?hours=`)

1. Auth wie oben; `CheckLogedIn()`; `hours` (Default 6) → `TimeSpan.FromHours`.
2. Service validiert `preview > 0 && preview <= MaxPreviewHours` sonst `ArgumentOutOfRangeException` → 400.
3. Slots: `Scheduled` und `End > utcNow && Start < utcNow + preview`, sortiert nach `Start` — enthält den laufenden Slot (falls vorhanden) als ersten Eintrag plus alle beginnenden Sendungen im Fenster.
4. Pro Eintrag Titelauflösung; beim laufenden Eintrag (`Start <= utcNow`) zusätzlich `PositionSeconds`/`RemainingSeconds`.
5. `DtoProgramGuideEntry[]` zurückgeben → `200 OK`.

Beteiligte Klassen: `ProgramGuideController`, `ProgramGuideService`, `ApplicationDbContext`.

### Titelauflösung (interner Ablauf beider Endpunkte)

1. `MovieId`s und `TVShowEpisodeId`s der betroffenen Slots sammeln.
2. `Movies` → `Dictionary<long, string>` (Id → Name); `TVShowEpisodes` inkl. `TVShowSeason.TVShow` → `Dictionary<long, string>` (Format `{Show} — {Season}, E{Number}: {Name}`).
3. `MediaType`/`MediaId` je Slot setzen: `MovieId` → `MediaTypeValues.Movie` + Movie-Id; `TVShowEpisodeId` → `MediaTypeValues.TVShowEpisode` + Episoden-Id; kein Treffer/leere Referenz → Titel „Unbekannt", `MediaType`/`MediaId` leer.

Beteiligte Klassen: `ProgramGuideService`, `ApplicationDbContext`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IProgramGuideService` | Interface (`VideoWebPlayer/Services/`) | Vertrag der Programmauskunft: `GetNowAsync`, `GetUpcomingAsync` |
| `ProgramGuideService` | Service (`VideoWebPlayer/Services/`) | Zeitbasierte, zustandslose Auskunft über `ProgramSlot` (nur `Scheduled`) inkl. Titelauflösung; Konstante `MaxPreviewHours` |
| `ProgramGuideController` | Controller (`VideoWebPlayer/Controllers/`) | `api/program-guide`: `GET now`, `GET upcoming`; `ApiBaseController` + `[BearerTokenCheck]` |
| `DtoProgramGuideEntry` | DTO (`VideoWebPlayer.Client/Models/`) | Ein Sendeplatz: `SlotId`, `Title`, `MediaType`, `MediaId`, `MediaItemId`, `IsFiller`, `StartUtc`, `EndUtc`, `DurationSeconds`, `PositionSeconds?`, `RemainingSeconds?` |
| `DtoProgramGuideNow` | DTO (`VideoWebPlayer.Client/Models/`) | „Jetzt/Gleich"-Antwort: `ServerTimeUtc`, `Now` (Entry?), `Next` (Entry?) |
| `ProgramGuideServiceTests` | Testklasse (`VideoWebPlayer.Tests/`) | Service-Tests gegen echtes SQLite-InMemory (`ProgramScheduleTestDb`) |
| `ProgramGuideControllerTests` | Testklasse (`VideoWebPlayer.Tests/Controllers/`) | Auth-/Validierungs-/Mapping-Tests (`FakeAuthService`) |
| `ProgramGuideE2ETests` | Testklasse (`VideoWebPlayer.Tests/`, `[Trait("Category","E2E")]`) | HTTP-E2E: 401 anonym, 200 mit Pairing-Token, `access_token`-Query, Vorschau-Inhalt |

## Änderungen an bestehenden Klassen

### `ServiceCollectionExtensions` (DI-Registrierung)

- **Neue Registrierung:** `services.AddScoped<IProgramGuideService, ProgramGuideService>();` — direkt neben `IProgramScheduleService`.

### `ProgramScheduleTestDb` (Test-Helfer)

- **Neue Hilfsmethode:** `CreateSlotAsync(channelId, start, end, mediaItemId, movieId, episodeId, ruleType, status)` — legt einen `ProgramSlot` direkt an (für Auskunftstests ohne Planerzeugung).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `hours`-Parameter `GET upcoming` | `1 <= hours <= ProgramGuideService.MaxPreviewHours` (12); Default 6 bei Weglassen | `ArgumentOutOfRangeException` → 400 Bad Request |
| Slot-Status | Nur `ProgramSlotStatus.Scheduled` geht in die Auskunft ein | `Displaced`-Slots werden ausgefiltert (kein Fehler) |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`GetNowAsync` liest `TVChannels` direkt statt `GetOrCreateChannelAsync`:** absichtlich, damit der reine Lese-Endpunkt keine Sender-Zeile anlegt; Verhalten bei fehlendem Sender ist definiert (leere Auskunft).
- **Benennung:** Neue öffentliche Namen `program-guide`/`ProgramGuide` dürfen nicht mit Playlist-Begriffen kollidieren — beim Review explizit prüfen.
- **Weiche Referenzen:** Gelöschte Filme/Episoden lassen Titel „Unbekannt" zurück — gewolltes Verhalten (Planhistorie bleibt auskunftsfähig), getestet.
- **Keine E2E-UI:** Schritt ist reine API — Playwright-E2E nicht nötig; HTTP-E2E über `WebApplicationFactory` ist der primäre Funktionsnachweis der gesicherten Schnittstelle.

## Umsetzungsreihenfolge

1. **DTOs anlegen**
   - Voraussetzungen: `MediaTypeValues` existiert in `VideoWebPlayer.Client/Models`.
   - Beschreibung: `DtoProgramGuideEntry`, `DtoProgramGuideNow` in `VideoWebPlayer.Client/Models/` mit XML-Docs (PascalCase, Projektstil).

2. **`IProgramGuideService` + `ProgramGuideService` anlegen**
   - Voraussetzungen: Schritt 1; `ProgramSlot`, `ProgramSlotStatus`, `TVChannel`, `Movie`, `TVShowEpisode` vorhanden.
   - Beschreibung: Interface + Implementierung (Constructor: `ApplicationDbContext`, `ILogger<ProgramGuideService>`; `MaxPreviewHours`-Konstante; private Hilfen `LoadTitlesAsync`, `ToEntry`).

3. **`ProgramGuideController` anlegen**
   - Voraussetzungen: Schritte 1–2; `ApiBaseController`, `BearerTokenCheckAttribute` vorhanden.
   - Beschreibung: `[ApiController] [Route("api/program-guide")] [BearerTokenCheck]`, `ExecuteAsync`-Muster (reduzierte Variante: Unauthorized→401, ArgumentOutOfRange/Argument→400, sonst→500), Endpunkte `now`/`upcoming`.

4. **DI-Registrierung**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `AddScoped<IProgramGuideService, ProgramGuideService>()` in `ServiceCollectionExtensions`.

5. **Test-Helfer erweitern + Service-Tests**
   - Voraussetzungen: Schritte 1–2; `ProgramScheduleTestDb`.
   - Beschreibung: `CreateSlotAsync` in `ProgramScheduleTestDb`; `ProgramGuideServiceTests` (alle Szenarien unten).

6. **Controller-Tests**
   - Voraussetzungen: Schritte 3, 5; `FakeAuthService`.
   - Beschreibung: `ProgramGuideControllerTests` (anonym→401, hours-Validierung→400, Ok→DTO-Inhalt).

7. **HTTP-E2E-Tests**
   - Voraussetzungen: Schritte 3–4; `DeviceClientTestBase`.
   - Beschreibung: `ProgramGuideE2ETests` (anonym→401, Header-Token→200, `access_token`-Query→200 mit Inhalt, Vorschau begrenzt/gefüllt).

8. **Build + volle Testsuite**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: `dotnet build VideoPlayer.sln` (Debug+Release), `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateSlotAsync` | `ProgramScheduleTestDb` (Helfer) | `ProgramSlot`-Seed direkt in die DB |
| `GetNowAsync_RunningSlot_ReturnsEntryWithPositionAndRemaining` | `ProgramGuideServiceTests` | Laufender Slot: Titel, `PositionSeconds`, `RemainingSeconds`, `Next` = Folgeslot |
| `GetNowAsync_DisplacedSlotCoveringNow_IsNotReported` | `ProgramGuideServiceTests` | `Displaced`-Slot über `now` ⇒ `Now == null` |
| `GetNowAsync_ScheduleGap_ReturnsNullNowButNamesNext` | `ProgramGuideServiceTests` | Lücke: `Now == null`, `Next` gefüllt |
| `GetNowAsync_ExactlyAtSlotEnd_NextSlotIsCurrent` | `ProgramGuideServiceTests` | Grenze `now == End`: alter Slot nicht laufend, Folgeslot läuft |
| `GetNowAsync_EpisodeSlot_ReturnsFormattedEpisodeTitle` | `ProgramGuideServiceTests` | Episoden-Titelformat `{Show} — {Season}, E{n}: {Name}` |
| `GetNowAsync_DeletedMovie_FallsBackToUnknownTitle` | `ProgramGuideServiceTests` | Leerlaufende `MovieId` → „Unbekannt" |
| `GetNowAsync_NoChannel_ReturnsEmptyNowAndNext` | `ProgramGuideServiceTests` | Ohne `TVChannel` leere Auskunft, `ServerTimeUtc` gesetzt |
| `GetUpcomingAsync_ReturnsRunningAndFollowingWithinWindow` | `ProgramGuideServiceTests` | Laufender Eintrag zuerst, Folgeslots sortiert, Positionsfelder nur beim laufenden |
| `GetUpcomingAsync_ExcludesSlotsBeyondWindow_AndDisplaced` | `ProgramGuideServiceTests` | `Start >= now+preview` und `Displaced` fehlen |
| `GetUpcomingAsync_InvalidPreview_Throws` | `ProgramGuideServiceTests` | `preview <= 0` / `> 12h` → `ArgumentOutOfRangeException` |
| `GetNow_Anonymous_Returns401` / `GetUpcoming_Anonymous_Returns401` | `ProgramGuideControllerTests` | `CurrentUser = null` → Unauthorized |
| `GetUpcoming_HoursOutOfRange_Returns400` | `ProgramGuideControllerTests` | `hours=0`/`13` → BadRequest; `hours` weggelassen → 200 |
| `GetNow_LoggedIn_ReturnsDto` | `ProgramGuideControllerTests` | OkObjectResult mit `DtoProgramGuideNow` |
| `Anonymous_RequestNow_Returns401` | `ProgramGuideE2ETests` | HTTP-E2E ohne Token → 401 |
| `PairedDevice_RequestNow_ReturnsCurrentAndNext` | `ProgramGuideE2ETests` | Pairing-Token (Header): 200, laufende Sendung mit Position, `Next` |
| `PairedDevice_RequestNowWithAccessTokenQuery_Returns200` | `ProgramGuideE2ETests` | `?access_token=`-Pfad → 200 |
| `PairedDevice_RequestUpcoming_ReturnsBoundedEntries` | `ProgramGuideE2ETests` | 200, Einträge = laufende + folgende im Fenster, `hours=99` → 400 |

### Betroffene bestehende Tests

Keine — alle Änderungen sind additiv; `ProgramScheduleTestDb` bekommt nur eine neue Hilfsmethode.

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung hat keinen UI-Benutzerfluss (Live-TV-Ansicht ist Schritt 7); der Benutzerfluss ist
der abgesicherte API-Aufruf — dafür sind HTTP-E2E-Tests über `WebApplicationFactory` der primäre
Nachweis (401 anonym, 200 mit Pairing-Token, `access_token`-Query, begrenzte Vorschau).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Anonymer Aufruf wird abgelehnt | `ProgramGuideE2ETests.Anonymous_RequestNow_Returns401` | Abgesicherte Schnittstelle | Nur der echte HTTP-Pfad durchläuft `BearerTokenCheckAttribute` |
| Pflicht | Angemeldeter Benutzer (gekoppeltes Gerät) erhält Jetzt/Gleich | `ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext` | Laufende Sendung + Position + Nächste | End-to-End über DI, Routing, Auth, Service |
| Pflicht | `access_token`-Query statt Header | `ProgramGuideE2ETests.PairedDevice_RequestNowWithAccessTokenQuery_Returns200` | Tokenbasiertes Abrufmuster | Attribut-Verhalten nur über echten Request prüfbar |
| Pflicht | Begrenzte Vorschau | `ProgramGuideE2ETests.PairedDevice_RequestUpcoming_ReturnsBoundedEntries` (+400 bei `hours=99`) | Begrenzte Übersicht, voller Plan administrativ | Serverseitige Begrenzung end-to-end |

Keine bestehenden E2E-Tests betroffen.

## Offene Punkte

Keine.
