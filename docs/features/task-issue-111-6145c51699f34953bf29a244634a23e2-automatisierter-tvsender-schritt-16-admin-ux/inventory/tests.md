# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-22, UTC+2 (lokale Ausführung)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-16-admin-ux` @ `68dc6e7f0a224983e8046956c1c22407c6c6ff6e`
- Uncommittete Änderungen im getesteten Stand: keine Quellcode-Änderungen; nur neue,
  unverfolgte Planungsartefakte unter `docs/features/...-schritt-16-admin-ux/`
  (beeinflussen den Testlauf nicht)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.402, `net10.0`, Windows,
  Playwright-Chromium vorhanden (E2E-Tests laufen real, kein `SkipBrowser`)
- Ermittelte Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests`
  (xUnit v3 + bUnit + Playwright + `Microsoft.AspNetCore.Mvc.Testing`), Befehl aus
  `AGENTS.md` §7 (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet build VideoPlayer.sln` (Debug, vorher) dann `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 2092 | 0 | 1 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf ist vollständig grün (2092 bestanden, 0 fehlgeschlagen).

### Testlücken und Ausführungsprobleme

- 1 übersprungener Test (bereits vorher bekannt):
  `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  (Skip im Test selbst, kein Ausführungsfehler).

## Testklassen

### `TVSenderAdminE2ETests` (`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`, Playwright, `PlaylistsE2ETestBase`)

- `Admin_CreateEditDeactivateDeleteRule` — konsolidierter Lifecycle: Probe-Karte
  (Zähler + `tvsender-probe-row`-Liste), Sender-Einstellungen speichern inkl.
  `Setups`-Rundreise, Regel anlegen (über `MediaSearchSelector`), umbenennen,
  Diagnose-Zelle, „Jetzt generieren", (de)aktivieren, löschen; danach Sendeplan-
  Zeilen inkl. `.tvsender-slot-displaced`. **Betroffen**: wartet auf
  `#tvsender-name` direkt nach `GotoAsync` und bedient `#tvsender-*`-Felder ohne
  Tab-Wechsel — muss an die Tab-Struktur angepasst werden.
- `Admin_GenreRule_MultiSelectAndSourceFilter` — Genre-Mehrfachauswahl +
  Quellen-Filter im Regel-Editor inkl. Join-Tabellen-Nachweis.
  **Betroffen**: öffnet den Editor über `#tvsender-new-rule` (bleibt im Regeln-Tab
  sichtbar — voraussichtlich unverändert lauffähig).
- `NonAdmin_SeesNotAuthorized` — Admin-Gate der Seite.

### `MediaDurationProbeServiceTests_Progress` (`VideoWebPlayer.Tests/Services/`, echtes SQLite über `ProgramScheduleTestDb`)

- `GetProgressAsync_CountsStatesSeparately` — Zähler probed/pending/failed inkl.
  Extension-Filter (`.nfo` zählt nicht).
- `GetProgressAsync_RecentItems_OrderedNewestFirstAndLimited` — Sortierung + Limit.
- `GetProgressAsync_ReportsLastActivityAndHourlyRate` — `LastActivityAt`, `ProbedLastHour`.
- `GetProgressAsync_EmptyLibrary_ReturnsZeros` — Leerbibliothek.
- `GetProgressAsync_NonAdmin_Throws` — `ForbiddenAccessException`/`UnauthorizedAccessException`.

### `GenreAdminTests` (`VideoWebPlayer.Tests/Components/`, bUnit)

- `SaveGenre_AppliesConfigToCaseVariants` — Muster für bUnit-Tests mit echter
  SQLite-In-Memory-DB, `ctx.AddAuthorization().SetAuthorized().SetClaims()` und
  direkter Service-Registrierung (`ApplicationDbContext`, konkrete Services).

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/`, internal)

- `CreateAsync(name, ct)` — shared In-Memory-SQLite, `EnsureCreated`.
- `AddUserAsync(email, isAdmin)` — `ApplicationUser` mit GUID-Id.
- `EnsureSourceAsync(id)` / `GrantSourceAccessAsync` — Quelle „Testquelle" + Zugriff.
- `CreateMediaItemAsync(sourceId, duration)` — Collection + Item (`DurationProbedAt`
  wird auf `DateTime.UtcNow` gesetzt; für „nie probiert" muss der Test es auf
  `null` zurücksetzen, siehe `SeedItemAsync` in `MediaDurationProbeServiceTests_Progress`).
- `CreateRuleAsync` / `CreateSlotAsync` / `CreateShowAsync` / `CreateMovieAsync` /
  `CreateGenreAsync` / `LinkMovieGenreAsync` / `LinkShowGenreAsync` — Seeds für
  Regeln, Sendeplätze, Shows, Filme, Genres.

### `PlaylistsE2ETestBase` (`VideoWebPlayer.Tests/Helpers/`)

- `LoginAsync(email)`, `MakeUserAdminAsync`, `GrantMediaSourceAccessForUserAsync`,
  `SeedTvShowWithSeasonsAsync`, `DriveUntilStateAsync(interaction, waitForState)` —
  Flake-sichere Interaktions-/Warteschleife; `SkipBrowser`-Fallback.

### Stubs in `MediaDurationProbeServiceTests_Progress`

- `NullProbe` (`IMediaDurationProbe`), `PresentLocator` (`IFfprobeLocator`),
  `StubMediaSourceReader` (`IMediaSourceReader`) — für die bUnit-Registrierung von
  `MediaDurationProbeService` wiederverwendbar (müssen ggf. sichtbar gemacht oder
  im neuen Test dupliziert werden).
