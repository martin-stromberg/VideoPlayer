# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine verbleibenden Befunde im Endstand. Während der Prüfung selbst gefundener und vor diesem
Bericht behobener Punkt (mit Beleg):

- **`ProgramGuideController.GetUpcoming`** — *Fehlerbehandlung:* `TimeSpan.FromHours(hours)` hätte
  bei extremen Werten (z. B. `hours = 2147483647`) eine `OverflowException` geworfen → HTTP 500
  statt 400. Behoben: Bereichsprüfung `1..MaxPreviewHours` im Controller vor der Umwandlung
  (Service-Validierung bleibt für Nicht-HTTP-Aufrufer). Beleg-Test:
  `ProgramGuideControllerTests.GetUpcoming_HoursOutOfRange_Returns400(hours: 2147483647)`.

## Geprüfte Dateien

- `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`
- `VideoWebPlayer.Client/Models/DtoProgramGuideNow.cs`
- `VideoWebPlayer/Services/IProgramGuideService.cs`
- `VideoWebPlayer/Services/ProgramGuideService.cs`
- `VideoWebPlayer/Controllers/ProgramGuideController.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (nur Registrierungszeile)
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` (nur `CreateGuideService`/`CreateSlotAsync`)
- `VideoWebPlayer.Tests/ProgramGuideServiceTests.cs`
- `VideoWebPlayer.Tests/Controllers/ProgramGuideControllerTests.cs`
- `VideoWebPlayer.Tests/ProgramGuideE2ETests.cs`

## Geprüfte Aspekte (auszugsweise)

- **Namensabgrenzung:** `ProgramGuide`/`api/program-guide` durchgehend — kein „Playlist"-Naming;
  `GetNowAsync`/`GetUpcomingAsync` folgen der `IProgramScheduleService`-Signaturkonvention
  (`utcNow` explizit, `CancellationToken` hinten).
- **Zustandslosigkeit:** Der Service hält nur `_db`; kein Cache, kein Prozesszustand — Antwort ist
  reine Funktion von (`utcNow`, Persistenz).
- **Status-Filter:** Alle drei Abfragen filtern `ProgramSlotStatus.Scheduled` — `Displaced` kann
  nicht als laufend/geplant durchrutschen.
- **Grenzfälle:** `now == End` → Slot nicht laufend und nicht „next" (`Start >= current.End`);
  Lücke → `Now = null`, `Next` gefüllt; kein Sender → leere Auskunft ohne Schreibseiteneffekt
  (direkter `TVChannels`-Leser statt `GetOrCreateChannelAsync` — dokumentiert).
- **Fehlerbehandlung:** `ExecuteAsync` mappt `UnauthorizedAccessException` → 401,
  `ArgumentOutOfRangeException`/`ArgumentException` → 400 (Reihenfolge korrekt: Spezialfall vor
  Basisklasse), Rest → 500; deutsche Log-Kontexte wie `PlaylistsController`.
- **Queries:** `AsNoTracking`, Batch-Titelauflösung in zwei Lookups statt N+1; leere Id-Listen
  werden übersprungen.
- **Tests:** AAA-Struktur, ein fachlicher Fall je Methode, echtes SQLite (`ProgramScheduleTestDb`);
  E2E prüft den echten HTTP-Pfad inkl. `BearerTokenCheckAttribute` und `access_token`-Query.
- **DTOs:** PascalCase + XML-Docs, `MediaTypeValues`-Konstanten statt Literale — Projektstil.
