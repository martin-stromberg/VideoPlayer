# Bestandsaufnahme: Programmauskunft (Schritt 5 — Laufendes Programm zeitbasiert auskunftsfähig machen)

Analysiert wurden der Sendeplan-Datenbestand (`ProgramSlot`), die Planungsdienste, das
Controller-/Auth-Muster und die Testinfrastruktur — bezogen auf die zeitbasierte Programmauskunft.

## Zusammenfassung

- `ProgramSlot` enthält bereits alles Nötige: `Start`/`End` (UTC), weiche Inhaltsreferenzen
  (`MediaItemId`, `MovieId`, `TVShowEpisodeId`), `RuleType`-Snapshot und `Status`
  (`Scheduled`/`Displaced`). Keine Schema-Änderung nötig.
- `IProgramScheduleService` deckt Erzeugung, Nachführung und die administrative Planansicht ab —
  eine **benutzerseitige, auf `Scheduled`-Slots beschränkte Auskunft fehlt vollständig** (kein
  Service, kein Controller, keine DTOs). `GetScheduleAsync` liefert bewusst auch `Displaced` und
  ist administrativ — nicht wiederverwendbar für die Auskunft.
- Titelauflösung existiert bisher nur inline in `TVSenderAdmin.razor` (Movie-/Episoden-Lookup) —
  Muster für die zu verlagernde Service-Logik.
- Auth- und Controller-Muster vollständig vorhanden (`BearerTokenCheckAttribute` inkl.
  `access_token`-Query, `ApiBaseController`/`CheckLoggedIn`, `ExecuteAsync`-Fehler-Mapping).
- DI-Registrierung in `ServiceCollectionExtensions` (scoped-Services rund um `IProgramScheduleService`).
- Test-Infrastruktur vorhanden: `ProgramScheduleTestDb` (echtes SQLite-InMemory),
  `FakeAuthService`, `DeviceClientTestBase`/`PairingWebApplicationFactory` (HTTP-E2E mit
  Pairing-Token und `access_token`-Query).

Test-Ausgangszustand: Baseline-Lauf 1866 Tests — 1865 bestanden, 1 übersprungen (bekannter
Alt-Skip), 0 fehlgeschlagen. Nachweis: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
