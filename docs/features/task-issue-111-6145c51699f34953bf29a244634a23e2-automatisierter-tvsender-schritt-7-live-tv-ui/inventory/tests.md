# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, ab ca. 09:20 MESZ (+02:00)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-7-live-tv-ui`, HEAD `849d39b` (step: Schritt 7 begonnen)
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte dieses Feature-Verzeichnisses (untracked); kein Produktivcode geändert
- Testumgebung: Windows, `dotnet --version` 10.0.402, Playwright 1.62.0 (Chromium headless über `PlaylistsE2ETestBase`)
- Ermittelte Testsuiten: `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit v3, bUnit, Moq, Mvc.Testing, Playwright, EF SQLite) — einziger Testbefehl laut `AGENTS.md` DoD: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
- Vorab-Build: `dotnet build VideoPlayer.sln -c Debug` → 0 Fehler, 141 Warnungen (Bestand) — [Log](test-results/baseline-build.log)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"` | Repo-Wurzel | 1 | 1952 | 1 | 1 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |
| Einzelwiederholung | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "FullyQualifiedName~TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule"` | Repo-Wurzel | 0 | 1 | 0 | 0 | Konsole, s. u. |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` | `Assert.Equal(10, setup.LiveStreamSegmentLengthSeconds)` → `Expected: 10 / Actual: 6` (Standardwert statt eingegebenem Wert) — E2E-Timing-Flake: `FillAsync("#tvsender-segment-length-seconds", "10")` ging vor dem Circuit-Handover verloren; **Einzelwiederholung bestanden** (10,2 s) | Baseline-Lauf Z. 1221 ff.; Retry-Konsole |

Effektiver Ausgangszustand: **1953/1954 grün, 1 Alt-Skip, 0 verbleibende Fehler** (der Baseline-Fehlschlag ist in der Einzelwiederholung nicht reproduzierbar).

### Testlücken und Ausführungsprobleme

- 1 dauerhaft übersprungener Test (Bestand): `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — bekannter Alt-Skip.
- Playwright-E2E-Tests laufen headless-Chromium; bei fehlender Browser-Installation setzt `PlaylistsE2ETestBase` `SkipBrowser` und Tests kehren ohne Prüfung zurück (kein Fehlschlag).

## Testklassen (relevante Bestandstests)

### `TVSenderAdminE2ETests` (`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`)
- `Admin_CreateEditDeactivateDeleteRule` — konsolidierter Playwright-E2E für `/admin/tv-sender`: Sender-Einstellungen (inkl. Live-Stream-Felder), Regel-CRUD via `MediaSearchSelector`, Sendeplan-Karte mit „Verdrängt"-Chip; seedet `TVChannel`/`ProgramSlot` direkt per `Services.CreateScope()` + `ApplicationDbContext`.
- `NonAdmin_SeesNotAuthorized` — Nicht-Admin sieht „Nicht autorisiert".

### `ProgramGuideE2ETests` (`VideoWebPlayer.Tests/ProgramGuideE2ETests.cs`)
- HTTP-E2E (`DeviceClientTestBase`, `[Trait("Category","E2E")]`): 401 anonym, `now` liefert Now/Next mit Position/Remaining, `access_token`-Query, `upcoming` begrenzt (laufender + folgender, `Displaced` nie), 400 bei `hours=99`. Hilfsmethode `SeedChannelWithProgramAsync` (TVChannel + MediaSource/Collection/Movie/MediaItem + laufender/folgender/verdrängter `ProgramSlot`).

### `LiveStreamE2ETests` (`VideoWebPlayer.Tests/LiveStreamE2ETests.cs`)
- HTTP-E2E für `api/live-stream`: 401 anonym, m3u8-Token-Rewrite (`seg_*.ts?access_token=`), `video/mp2t`, 404 invalid/missing/traversal; `SeedSegmentDirectoryAsync` schreibt m3u8+ts in Temp-Verzeichnis und setzt `Setup.LiveStreamSegmentDirectory`.

### `NavMenuPlaylistsTests` (`VideoWebPlayer.Tests/Components/NavMenuPlaylistsTests.cs`)
- bUnit: `ctx.AddAuthorization().SetAuthorized(...)`, `InMemoryDatabase`-`ApplicationDbContext`, `NoOpVideoWebPlayerClient`, `Mock<ILoginIpBlockService>` — Muster für NavMenu-Tests (Link-Anzahl/-href/-Text).

### `VideoPlayerStreamErrorTests` (`VideoWebPlayer.Tests/Components/VideoPlayerStreamErrorTests.cs`)
- bUnit: Fehler-Overlay des Players bei `onerror`, Rücksetzen bei neuem Titel/`onloadeddata`; JS-Interop im Loose-Mode (kein echter Browser).

### `PlaylistDetailTests` u. a. (`VideoWebPlayer.Tests/Components/`)
- bUnit-Muster: `JSInterop.Mode = Loose`, `NoOpVideoWebPlayerClient` (überschreibt `HttpGetAsync<T>` — neue DTO-Typen ggf. ergänzen), `AddAuthorization().SetAuthorized`.

## Hilfsmethoden

### `PlaylistsE2ETestBase` (`VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`)
- `LoginAsync(email)` — echter UI-Login + Blazor-Circuit-Wartezeit.
- `DriveUntilStateAsync(interaction, waitForState)` — treibt einen Schritt wiederholt, bis der erwartete DOM-Zustand steht (Circuit-Handover-Flakiness).
- `SeedMovieAsync`, `SeedTvShowWithSeasonsAsync`, `SeedMovieCollectionAsync`, `SeedTvShowWithSingleSeasonAsync` — DB-Seeding über `RunScopedAsync` mit geteilter `MediaSource`.
- `MakeUserAdminAsync`, `GrantMediaSourceAccessForUserAsync` — Berechtigungsseeding.
- `Services` — gehostete App-DI für direktes DB-Seeding (TVChannel/ProgramSlot-Muster aus `TVSenderAdminE2ETests`).

### `DeviceClientTestBase` (`VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`)
- `CreateHttpClient()`, `CreateUserAndPairDeviceAsync(email)`, `CreateScope()` — gekoppeltes Gerät gegen gehostete App (für HTTP-E2E der API-Endpunkte).

### `NoOpVideoWebPlayerClient` (`VideoWebPlayer.Tests/Helpers/NoOpVideoWebPlayerClient.cs`)
- `VideoWebPlayerClient`-Stand-in für bUnit; `HttpGetAsync<T>` liefert leere Liste für `List<DtoGenreOption>` bzw. `List<MediaEntryDto>` — für neue Program-Guide-DTO-Typen erweiterbar.
