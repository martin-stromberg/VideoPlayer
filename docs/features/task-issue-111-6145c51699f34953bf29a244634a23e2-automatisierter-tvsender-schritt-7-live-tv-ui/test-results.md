# Testergebnisse

## Läufe

| Lauf | Befehl | Ergebnis | Nachweis |
|------|--------|----------|----------|
| Build Debug | `dotnet build VideoPlayer.sln -c Debug` | 0 Fehler | Konsole |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | 0 Fehler | Konsole |
| Volle Suite (final) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | **1965/1966 bestanden, 0 Fehler, 1 Skip** (~4,8 Min) | `final-dotnet-test.log` (`inventory/test-results/final-dotnet-test.log`) |
| Volle Suite (Baseline, vor Implementierung) | gleiche Kommandozeile | 1952/1954, 1 Flake (`TVSenderAdminE2ETests`, Einzelretry grün), 1 Skip | `baseline-dotnet-test.log` (`inventory/test-results/baseline-dotnet-test.log`) |

## Neue Tests (12, alle grün)

- `LiveTvTests` (bUnit, 7): Jetzt/Gleich/Vorschau + `liveTv.attach` mit `access_token`; Sendelücke (Hinweis, kein Video, kein Attach); LoginForm unauthentifiziert ohne API-Aufruf; generischer Ladefehler; 403→Zugriffshinweis; `NotifyStreamUnsupported`-/`NotifyStreamError`-Hinweise.
- `NavMenuLiveTvTests` (1): genau ein „Live-TV"-NavLink auf `/live-tv`.
- `VideoWebPlayerClientTests_ProgramGuide` (2): gepaarter Client liest `RequestProgramGuideNowAsync`/`RequestProgramGuideUpcomingAsync` gegen gehostete App.
- `LiveTvE2ETests` (Playwright, 2): Navigationspfad → Player/Programmanzeige + `live.m3u8?access_token`-Request; Sendelücke mit freundlichem Hinweis + Vorschau + kein Video.

## Bekannte Einschränkungen/Nachweise

- 1 dauerhaft übersprungener Alt-Test (`DevicePlaylistE2ETests_Playback...ContinueWatchingListStillLoads`) — Bestand.
- Wiederanlauf bei Slot-Beginn ohne Reload: **nicht E2E-nachweisbar**, weil `ProgramScheduleService.ReconcileAsync` manuell eingefügte, regellose `ProgramSlot`s beim nächsten Lauf als `Displaced` markiert (im Lauf verifiziert: DB-Dump `st=Displaced`; die Poll-Ticks liefen korrekt alle 15 s). Mechanismus abgesichert über bUnit-Zustandsübergänge + Attach-Nachweis; Hinweis im Test dokumentiert.
- Chromium hat kein natives HLS → der E2E läuft über den hls.js/MSE-Pfad; der native `canPlayType`-Pfad ist eine einfache Verzweigung in `liveTv.js`.

## Flake-Notiz

`TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` fiel einmalig im Baseline-Lauf (Timing, `FillAsync` vor Circuit-Handover verloren), Einzelwiederholung grün — kein Regressionszusammenhang.
