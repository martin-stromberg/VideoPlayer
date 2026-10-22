# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-22, ~17:20–17:35 UTC
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-24-plan-panels`, Stand des Branch-Starts (nur Arbeitsverzeichnis-Artefakte `docs/features/` hinzugekommen)
- Uncommittete Änderungen im getesteten Stand: keine Quellcode-Änderungen; nur dieses Feature-Verzeichnis
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.402, Windows, Playwright/Chromium verfügbar (E2E laufen)
- Ermittelte Testsuite: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (AGENTS.md Definition of Done)
- **Hinweis Build-Pfad:** `bin/Debug` ist durch eine laufende `VideoWebPlayer`-Instanz und Visual Studio gesperrt (MSB3027 beim Kopieren). Build und Tests laufen deshalb mit `--artifacts-path artifacts/schritt24-bin` — die laufende Instanz wird nicht beendet.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet build VideoPlayer.sln -c Debug --artifacts-path artifacts/schritt24-bin` | Repo-Root | 0 (144 Warnungen, alle pre-existing) | – | – | – | Konsole |
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --artifacts-path artifacts/schritt24-bin` | Repo-Root | 1 | 2164 | 2 | 1 | `test-results/baseline.log` |
| Wiederholung (2 Fehlschläge) | `dotnet test … --filter "FullyQualifiedName~FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity|FullyQualifiedName~PlaylistPlaybackE2ETests.PlaylistSkipCollectionEntriesE2ETest"` | Repo-Root | 0 | 2 | 0 | 0 | `test-results/baseline-rerun.log` |

### Nachgewiesene bestehende Testfehler

Keine dauerhaften Fehler nachgewiesen. Beide Fehlschläge des Gesamtlaufs sind im isolierten
Wiederholungslauf bestanden — Load-/Timing-Flakes (Playwright-Timeout bzw.
Live-Stream-Race-Condition), wie in `AGENTS.md` Abschnitt 7 beschrieben:

| Test-ID | Suite | Fehlerbild im Gesamtlauf | Wiederholung |
|---------|-------|--------------------------|--------------|
| `FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity` | `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs` | `Assert.Null() Failure` (früher `LiveStreamExitInfo`) | bestanden |
| `PlaylistPlaybackE2ETests.PlaylistSkipCollectionEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs` | Playwright-Timeout `30 s` auf `#playlist-detail-selected-entry` | bestanden |

Der eine Übersprungene ist der bekannte Alt-Skip
(`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`).

### Testlücken und Ausführungsprobleme

- Keine nicht ausführbaren Suiten. Playwright-E2E laufen in dieser Umgebung (Chromium vorhanden).
- Build-Sperre `bin/Debug` durch laufende Benutzer-App → dokumentierter Workaround `--artifacts-path`.

## Testklassen (für diese Anforderung relevant)

### `ProgramGuideServiceTests`
Datei: `VideoWebPlayer.Tests/ProgramGuideServiceTests.cs` — In-Memory-SQLite via `ProgramScheduleTestDb`, Slots direkt geseedet.
- `GetNowAsync_RunningSlot_ReturnsEntryWithPositionAndRemaining` — laufender Slot
- `GetNowAsync_EpisodeSlot_ReturnsFormattedEpisodeTitle` — Episoden-Titelformat
- `GetNowAsync_BareMediaItemSlot_FallsBackToItemName` — Clip-Füller ohne Inhaltsbezug
- `GetNowAsync_DeletedMovie_FallsBackToUnknownTitle` — „Unbekannt"-Fallback
- `GetUpcomingAsync_ReturnsRunningAndFollowingWithinWindow` u. a.

### `ProgramGuideControllerTests`
Datei: `VideoWebPlayer.Tests/Controllers/ProgramGuideControllerTests.cs` — Auth (401), `hours`-Validierung (400), Ergebnis-Mapping.

### `VideoWebPlayerClientTests_ProgramGuide`
Datei: `VideoWebPlayer.Tests/ClientLibrary/VideoWebPlayerClientTests_ProgramGuide.cs` — gekoppeltes Gerät liest `now`/`upcoming` über HTTP+JSON.

### `ProgramGuideE2ETests`
Datei: `VideoWebPlayer.Tests/ProgramGuideE2ETests.cs` — HTTP-Ebene der Programmauskunft.

### `LiveTvTests` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/LiveTvTests.cs` — Jetzt/Gleich/Vorschau-Markup
(`.livetv-upcoming-row`, `#live-tv-now-title`), Sendelücke, Login-Formular, Fehlermeldungen,
`liveTv.attach`-Aufruf mit `access_token`. `BunitContext` + `Mock<IProgramGuideApiClient>`.

### `TVSenderAdminTests` (bUnit, echtes SQLite)
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs` — Tabs, Regel-Editor,
Probe-Detail-Tabellen; Fixture `ProgramScheduleTestDb`, Admin-Claims.

### `LiveTvE2ETests` / `TVSenderAdminE2ETests` (Playwright)
- `LiveTv_ViaNavMenu_ShowsPlayerAndProgram` — Player, `#live-tv-now-title`, `#live-tv-next`,
  `.livetv-upcoming-row`-Anzahl, `access_token` im HLS-Request
- `LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming` — Sendelücke
- `TVSenderAdminE2ETests` — Sendeplan-Teil prüft `.tvsender-slot-row`, `.tvsender-slot-displaced`,
  „Geplant"/„Verdrängt"

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — Shared-In-Memory-SQLite mit echten FKs
- `CreateSlotAsync`, `CreateMovieAsync`, `CreateShowAsync` (Show+Staffel „Staffel 1"+Episoden),
  `CreateMediaItemAsync`, `CreateGenreAsync`, `LinkMovieGenreAsync`, `LinkShowGenreAsync`,
  `CreateRuleAsync`, `GetChannelAsync`, `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`

### `MediaPictureUrlResolver` (Produktivcode)
- `Resolve(pictureId, token)` — Bild-URL mit `access_token` oder `/images/placeholder.png`

### `TestImages` (`VideoWebPlayer.Tests/Helpers/TestImages.cs`)
- Erzeugt Testbild-Bytes für `Picture`-Seeds.
