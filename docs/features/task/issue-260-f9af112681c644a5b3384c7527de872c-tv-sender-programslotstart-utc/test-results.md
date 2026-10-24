# Testergebnisse: Issue #260 — `ProgramSlot`-Zeitwerte als UTC lesen

## Befehle

- `dotnet build VideoPlayer.sln` (Debug) — **0 Fehler** (142 Warnungen, alle preexisting).
- `dotnet build VideoPlayer.sln -c Release` — **0 Fehler** (142 Warnungen, alle preexisting).
- `dotnet ef migrations has-pending-model-changes` (in `VideoWebPlayer/`) — **„No changes have been made to the model since the last migration"** → keine Migration erforderlich (Konverter ändert nur die Materialisierung, nicht das Schema).
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=after-issue260.trx"` — **2226 erfolgreich, 0 Fehler, 1 übersprungen (2227 gesamt)**, Dauer ~5 min. Nachweis: [after-issue260.trx](inventory/test-results/after-issue260.trx)

## Neue Tests

- `ProgramSlotTests_DateTimeKind.ReadSlot_AfterSaveAndReload_ExposesUtcKind` — aus SQLite gelesener `ProgramSlot` hat `Kind == Utc` auf `Start`/`End`/`CreatedAt`, Ticks identisch zum Seed.
- `ProgramSlotTests_DateTimeKind.ReadSlot_ToLocalTime_ConvertsInNonUtcZone` — `ToLocalTime()` auf dem gelesenen Wert liefert `Kind == Local` und stimmt mit `ConvertTimeFromUtc` überein; Festzone Tokio (UTC+9) verifiziert die erwartete lokale Uhrzeit.
- `ProgramSlotTests_DateTimeKind.ReadSlot_KindAwareApis_TreatValueAsUtc` — `new DateTimeOffset(read.Start).Offset == Zero`, `ToUniversalTime()` ist Round-Trip-stabil (die eigentliche `Unspecified`-Fragilität).

## Sabotageprobe

Konverter-Leserichtung auf `DateTimeKind.Unspecified` gesetzt und neu gebaut:

- `ReadSlot_AfterSaveAndReload_ExposesUtcKind` → **FAIL** (`Expected: Utc, Actual: Unspecified`)
- `ReadSlot_KindAwareApis_TreatValueAsUtc` → **FAIL** (`Expected: 00:00:00, Actual: 02:00:00` Offset)
- `ReadSlot_ToLocalTime_ConvertsInNonUtcZone` → grün — erwartbar, da `ToLocalTime()` auf .NET `Unspecified`-Werte als UTC deutet und korrekt konvertiert (Details in `review.md`).

Nach Rückbau: 3/3 grün.

## Bekannte Flüchtigkeiten / Auslassungen

- Baseline-Vorlauf: 1 Fehlschlag `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout 30 s, `SelectEntryAsync`) — Einzelretry grün, bekannter E2E-Flacker (AGENTS.md §7). Im Nach-Lauf grün.
- 1 Übersprungener Test: `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — preexisting Skip (bereits in der Baseline).

## Vergleich zur Baseline

| Lauf | Gesamt | Erfolgreich | Fehler | Übersprungen |
|------|--------|-------------|--------|--------------|
| Baseline | 2224 | 2222 | 1 (Flake, Retry grün) | 1 |
| Nach Änderung | 2227 | 2226 | 0 | 1 |

+3 neue Tests, kein bestehender Test gebrochen.
