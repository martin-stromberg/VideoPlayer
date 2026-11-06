# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `DtoProgramGuideEntry` (DTO) — angelegt (`VideoWebPlayer/Client/Models` → `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`): `SlotId`, `Title`, `MediaType`, `MediaId`, `MediaItemId`, `IsFiller`, `StartUtc`, `EndUtc`, `DurationSeconds`, `PositionSeconds?`, `RemainingSeconds?`
- [x] `DtoProgramGuideNow` (DTO) — angelegt (`VideoWebPlayer.Client/Models/DtoProgramGuideNow.cs`): `ServerTimeUtc`, `Now`, `Next`
- [x] `IProgramGuideService` (Interface) — angelegt (`VideoWebPlayer/Services/IProgramGuideService.cs`): `GetNowAsync`, `GetUpcomingAsync` — beide mit explizitem `utcNow`
- [x] `ProgramGuideService` (Service) — angelegt (`VideoWebPlayer/Services/ProgramGuideService.cs`): `MaxPreviewHours = 12`, `GetNowAsync`, `GetUpcomingAsync`, `GetChannelIdAsync` (lesend, ohne Sender-Anlage), `LoadTitlesAsync` (Batch-Lookup nach `TVSenderAdmin`-Muster), `ToEntry`, `TitleLookup`
- [x] `ProgramGuideController` (Controller) — angelegt (`VideoWebPlayer/Controllers/ProgramGuideController.cs`): `[ApiController]`, `[Route("api/program-guide")]`, `[BearerTokenCheck]`, `GET now`, `GET upcoming?hours=` (Default 6), `ExecuteAsync`-Muster (401/400/500)
- [x] DI-Registrierung `AddScoped<IProgramGuideService, ProgramGuideService>()` — vorhanden in `ServiceCollectionExtensions` direkt hinter `IProgramScheduleService`
- [x] `ProgramScheduleTestDb.CreateSlotAsync` — vorhanden (direkter `ProgramSlot`-Seed; ergänzend `CreateGuideService()` zum Instanziieren)
- [x] `ProgramGuideServiceTests` — angelegt: laufende Sendung mit Position/Restdauer/`Next`, Displaced nicht als laufend, Lücke (`Now=null`, `Next` gefüllt), Grenze `now == End`, Episoden-Titelformat, Füller-Markierung, gelöschter Film → „Unbekannt", kein Sender → leer + keine Seiteneffekt-Anlage, Vorschau-Fenster (halbes offenes Intervall, Sortierung, Positionsfelder nur laufend), Displaced-Ausschluss, `preview`-Validierung, kein Sender → leer (12 Tests)
- [x] `ProgramGuideControllerTests` — angelegt: anonym → 401 (beide Endpunkte), `hours` 0/−3/13/48 → 400, weggelassen → 200 + Default-Fenster, `now` → DTO
- [x] `ProgramGuideE2ETests` (`[Trait("Category","E2E")]`) — angelegt: 401 anonym (beide Endpunkte), 200 mit Pairing-Token (Header, laufende Sendung + Position + `Next`), 200 über `access_token`-Query, begrenzte Vorschau (Displaced fehlt), `hours=99` → 400
- [x] `dotnet build VideoPlayer.sln` Debug **und** Release: 0 Fehler (Release-Warnungen ausschließlich vorbestehende BL0008 in Account-Razor-Seiten)
- [x] Gezielter Lauf `FullyQualifiedName~ProgramGuide`: 26/26 bestanden

## Offene Aufgaben

Keine.

## Hinweise

- Alle Planelemente vorhanden und getestet. Der volle Testsuite-Lauf (Tasks-Nr. 12) steht als
  Lifecycle-Schritt 10 noch aus.
- Zusätzlich zum Plan: `CreateGuideService()` in `ProgramScheduleTestDb` (Instanziierungshilfe —
  notwendig, da `ProgramGuideService` einen eigenen Konstruktor hat).
