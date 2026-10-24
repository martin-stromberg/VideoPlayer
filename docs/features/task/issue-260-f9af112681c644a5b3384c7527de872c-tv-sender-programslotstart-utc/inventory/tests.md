# Tests — Bestandsaufnahme und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-24, Lauf ca. 07:50–07:56 (UTC+2, lokale Rechnerzeit), Retry 08:04
- Branch und Commit-ID: `task/issue-260-f9af112681c644a5b3384c7527de872c-tv-sender-programslotstart-utc`, `ef67e425b3da3b465940a2ebcaa97b53261f0dfd`
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte unter `docs/features/task/issue-260-.../` (kein Produktivcode, keine Tests geändert)
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.402, `net10.0`, Playwright-Chromium (E2E)
- Ermittelte Testsuiten und Quellen der Testbefehle: einzige Testsuite `VideoWebPlayer.Tests` (xunit.v3, bUnit, Playwright-E2E, reales In-Memory-SQLite) — Befehl aus AGENTS.md §7

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=baseline-issue260.trx"` | Repo-Root | 1 | 2222 | 1 | 1 | [baseline-issue260.trx](test-results/baseline-issue260.trx) |
| Retry Einzeltest | `dotnet test ... --filter "FullyQualifiedName=VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest" --no-build` | Repo-Root | 0 | 1 | 0 | 0 | Konsolenausgabe (Bestanden, 21 s) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs:178` | `System.TimeoutException : Timeout 30000ms exceeded.` — `Locator("#playlist-detail-selected-entry")` nicht sichtbar (Playwright-E2E unter Last) | Baseline-TRX; Einzelwiederholung ohne Neubau: **bestanden** (21 s) → bekannter E2E-Flacker laut AGENTS.md §7 |

### Testlücken und Ausführungsprobleme

- `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `NotExecuted` (übersprungen, im Baseline-TRX verzeichnet); Grund im Test-Attribut, nicht durch diesen Lauf verursacht.
- Kein Test zum `DateTimeKind` von `ProgramSlot.Start`/`End` vorhanden — die bestehenden Tests arbeiten das Problem mit `DateTime.SpecifyKind(..., Utc)`-Umhüllungen in den Asserts weg (z. B. `ProgramScheduleServiceTests_Generation`).

## Testklassen

### `ProgramGuideServiceTests` (`VideoWebPlayer.Tests/ProgramGuideServiceTests.cs`)
- `GetNowAsync_*`, `GetUpcomingAsync_*` — Guide-Abfragen gegen seeded `ProgramSlot`-Zeilen (reales SQLite via `ProgramScheduleTestDb`).

### `ProgramScheduleServiceTests_*` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_*.cs`)
- `EnsureCoverage_*`, `Reconcile_*` u. a. — Sendeplan-Generierung/-Nachführung; Asserts kapseln `slot.Start` regelmäßig in `DateTime.SpecifyKind(..., DateTimeKind.Utc)` + `TimeZoneInfo.ConvertTimeFromUtc` (Workaround für den `Unspecified`-Lesewert).

### `TVSenderAdminTests` (`VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`)
- `Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges` — Tagesgruppierung der Admin-Ansicht; erwartet lokale Tagesdaten (`slotStart.ToLocalTime()`).
- `Sendeplan_RendersProgramCards_*`, `RendersFourTabs_*`, Probe-Tabellen-Tests.

### `TVSenderAdminE2ETests` (`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`)
- Admin-E2E-Flüsse der TV-Sender-Seite (Regeln, Sendeplan, Probe-Reset).

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — Shared-In-Memory-SQLite + `EnsureCreated`, seedet `Setups.TvSenderEnabled = true`.
- `CreateSlotAsync(channelId, startUtc, endUtc, mediaItemId, ...)` — legt `ProgramSlot` direkt an.
- `SlotsAsync(ct)` — `AsNoTracking`-Lesen aller Slots (materialisiert aus SQLite → hier wirkt der geplante Konverter).
- `GetChannelAsync`, `CreateMediaItemAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateRuleAsync`, `SaveDiagAsync` — Seed-Helfer.
