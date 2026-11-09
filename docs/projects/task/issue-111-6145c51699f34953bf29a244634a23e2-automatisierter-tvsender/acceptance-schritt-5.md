# Abnahme Schritt 5: Laufendes Programm zeitbasiert auskunftsfähig machen

Prüfer: Devin (unabhängiger Lauf, nicht der Implementierer)
Branch: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-5-programmauskunft` (HEAD `98819df`)
Geprüft gegen `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`.

## Ergebnis

**Status:** Erfüllt

## Abweichungen

Keine.

## Hinweise

- **Playwright-Flake (ehrlich berichtet):** Im Vollauf lief `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest`
  einmal in einen 30-s-Timeout (`#playlist-detail-selected-entry`). Der Test ist fachlich nicht Teil
  dieses Diffs (kein Playlist-/Playback-Code geändert); Einzelwiederholung lief grün. Derselbe,
  bereits in den Abnahmen zu Schritt 3 und 4 dokumentierte Baseline-Flake.
- **`Next`-Semantik:** `next` ist der erste `Scheduled`-Slot mit `Start >= current.End` (bzw.
  `>= now` in einer Sendelücke) — für den lückenlosen Plan identisch mit „erster Slot nach dem
  aktuellen". Bei einem hypothetischen überlappenden `Scheduled`-Bestand würde ein während des
  laufenden Slots beginnender Eintrag übersprungen; die Planinvariante (lückenlos, überlappungsfrei)
  schließt das aus — vertretbar.
- **Kanalwahl:** `GetChannelIdAsync` liest den ersten `TVChannel`-Datensatz statt
  `GetOrCreateChannelAsync` — bewusste, im Plan begründete Entscheidung (kein Seiteneffekt-Anlegen
  auf GET; Test `GetNowAsync_NoChannel_...` belegt, dass kein Sender angelegt wird). Korrekt, da
  genau ein Sender ausgeliefert wird.
- **CI-Gate „Static analysis":** Der Testprojekt-/App-Rebuild im Prüflauf zeigt keine neuen
  Warnungen in den Diff-Dateien (kein CS1573/CS1574/xUnit1051; die neuen Tests nutzen durchgehend
  `TestContext.Current.CancellationToken`). Die 141 Build-Warnungen sind sämtlich vorbestehend
  (v. a. BL0008 in Account-Pages).
- **Kein Schema-/Backup-Bedarf:** Reiner Service-/API-Schritt ohne neue Tabellen oder Spalten —
  die DoD-Punkte zu Migration/Backup entfallen zu Recht.

## Durchgeführte Prüfungen

- **Diff:** 29 Dateien (+1861/−2). Kerndateien vollständig gelesen: `ProgramGuideService`,
  `IProgramGuideService`, `ProgramGuideController`, `DtoProgramGuideEntry`, `DtoProgramGuideNow`,
  `BearerTokenCheckAttribute`, `ApiBaseController.CheckLoggedIn`, `ProgramSlot`/`ProgramSlotStatus`,
  `ServiceCollectionExtensions` (DI: `AddScoped<IProgramGuideService, ProgramGuideService>`),
  alle neuen Testdateien (`ProgramGuideServiceTests`, `ProgramGuideControllerTests`,
  `ProgramGuideE2ETests`, Fixture-Erweiterung `ProgramScheduleTestDb.CreateSlotAsync`/
  `CreateGuideService`) sowie `docs/API.md`, `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`.
- **Build:** `dotnet build VideoPlayer.sln` Debug **und** Release — jeweils 0 Fehler.
- **Volle Testsuite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` komplett,
  synchron abgewartet: **1891 erfolgreich, 1 übersprungen** (Alt-Skip
  `PairedDevice_ReportsProgressForMovieWithoutCollection_...`), **1 Fehler** = der oben genannte
  Playwright-Flake; Einzelwiederholung grün → faktisch **1892 grün + 1 Alt-Skip** wie erwartet.
- **Gegenprobe (Mutation):** `Status == ProgramSlotStatus.Scheduled`-Filter aus der
  `current`-Abfrage in `ProgramGuideService.GetNowAsync` entfernt →
  `GetNowAsync_DisplacedSlotCoveringNow_IsNotReported` schlug wie erwartet fehl
  (`Assert.Null(): DtoProgramGuideEntry` wurde geliefert). Anschließend per `git checkout --`
  revertiert; Working Tree wieder sauber. Der Displaced-Ausschluss ist also testabgesichert.

## Fachliche Kontrollen (aus dem Code verifiziert)

- **Rein zeitbasiert, zustandslos:** Beide Service-Methoden erhalten `utcNow` als expliziten
  Parameter (Controller übergibt `DateTimeOffset.UtcNow`); kein Prozesszustand, kein
  Zuschauerbezug, reine DB-Lesepfade (`AsNoTracking`) — nach Neustart sofort korrekt.
- **Zeitlogik korrekt:** Laufender Slot = `Scheduled && Start <= now && End > now`;
  `PositionSeconds = now - Start`, `RemainingSeconds = End - now` nur für den laufenden Eintrag
  (nullable, bei `Next`/Vorschau-Einträgen `null`). Grenzfall `now == End` konsistent: der endende
  Slot wird nicht mehr geliefert, der exakt beginnende schon (`PositionSeconds = 0`, Test
  `GetNowAsync_ExactlyAtSlotEnd_NextSlotIsCurrent`); der abgelaufene Slot taucht auch nicht als
  `Next` wieder auf.
- **Verdrängte Slots ausgeschlossen:** Alle Abfragen filtern auf
  `ProgramSlotStatus.Scheduled`; `Displaced` ist Historie (Service-Tests für `now` und
  `upcoming`, E2E-Test mit geseedetem Displaced-Slot). Per Mutation verifiziert.
- **Vorschau gedeckelt:** `MaxPreviewHours = 12` — Controller lehnt `hours` außerhalb `1..12` mit
  400 ab (vor `TimeSpan.FromHours`, kein Overflow), Service validiert das `TimeSpan` zusätzlich
  (`ArgumentOutOfRangeException` → 400). Fenster halb-offen `[now, now+preview)` (Test: Slot an
  der Fensterkante ausgeschlossen); enthält laufende Sendung + beginnende Sendungen, sortiert.
  Der volle Sendeplan bleibt administrativ (nur `TVSenderAdmin`, kein öffentlicher Endpunkt).
- **Auth serverseitig:** `[BearerTokenCheck]` auf Klassenebene (JWT via `Authorization`-Header
  **oder** `access_token`-Query — Gerätepfad) plus `CheckLoggedIn()` je Endpunkt; anonym → 401
  (E2E-nachgewiesen für beide Endpunkte), `hours=99` → 400 (E2E), Pairing-Token → 200 sowohl per
  Header als auch per Query (E2E).
- **Begrifflichkeit:** Eigener Namensraum `api/program-guide`, `ProgramGuide*`, fachlich
  „Programmauskunft" — kein Playlist-Naming, klare Abgrenzung in Code und Doku.
- **Titelauflösung:** Batch-Lookup nach `TVSenderAdmin`-Muster (Filmname bzw.
  „{Show} — {Season}, E{Number}: {Name}"), Fallback „Unbekannt" bei leerlaufender weicher
  Referenz (Test mit gelöschtem Film); `IsFiller` über den `RuleType`-Snapshot — robust gegen
  gelöschte Regeln.
