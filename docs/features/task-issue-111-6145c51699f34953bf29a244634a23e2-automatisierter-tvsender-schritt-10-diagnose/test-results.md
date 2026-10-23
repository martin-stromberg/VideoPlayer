# Testergebnisse — Schritt 10: Planungsdiagnose + manueller Generierungsanstoß (N5)

## Builds

| Build | Ergebnis |
|-------|----------|
| `dotnet build VideoPlayer.sln -c Debug` | 0 Fehler (142 Warnungen — allesamt pre-existing, z. B. BL0008 in Account-Seiten) |
| `dotnet build VideoPlayer.sln -c Release` | 0 Fehler |

## Testläufe

| Lauf | Befehl | Ergebnis |
|------|--------|----------|
| Neue Tests | `dotnet test … --filter "FullyQualifiedName~ProgramScheduleServiceTests_Diagnosis"` | 10/10 grün |
| Betroffene Suiten | `--filter "FullyQualifiedName~ProgramSchedule|ProgramRule|ProgramGuide|TVSenderAdmin"` | 122/123 grün — 1 Fehlschlag: `Admin_CreateEditDeactivateDeleteRule` (Flake, s. u.) |
| TVSenderAdminE2ETests isoliert | `--filter "FullyQualifiedName~TVSenderAdminE2ETests"` | 3/3 grün |
| **Volle Suite** | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug` | **2007 grün, 1 fehlgeschlagen, 1 übersprungen** (gesamt 2009, 4 m 55 s) |
| Wiederholung des Fehlschlags | `--filter "…Admin_CreateEditDeactivateDeleteRule"` | 1/1 grün |

## Fehlgeschlagene Tests

| Test | Fehlerbild | Einordnung |
|------|-----------|------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | `Assert.Equal(10, setup.LiveStreamSegmentLengthSeconds)` → actual 6 (Zeile 49) | **Flake im Alt-Testteil**: Die Fill-/Save-Sequenz der Sender-Einstellungen rast unter Volllauf-Parallelität dem Bind-Update hinterher; der betroffene Code (Zeile 36–52, Schritt-7-Feature) wurde von diesem Schritt nicht angefasst. In zwei isolierten Läufen grün; im gefilterten Baseline-Lauf vor der Umsetzung ebenfalls grün. Die neuen Assertions desselben Tests (Diagnose-Zelle „ohne Mediendatei", `#tvsender-generate-now` + Status „aktualisiert") liefen in allen Durchläufen, in denen der Test die Settings-Phase überstand, fehlerfrei. |

## Übersprungene Tests

| Test | Grund |
|------|-------|
| `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | Bereits vor diesem Schritt bekannter Skip (im Volllauf mit SKIP markiert). |

## Abdeckung der Akzeptanzkriterien

- Pro-Regel-Diagnose (planbar / verwaist / ohne Mediendatei / Probe läuft / Probe gescheitert /
  nicht freigegeben): `ProgramScheduleServiceTests_Diagnosis` (10 Integrationstests, echtes SQLite).
- Admin-Erzwingung serverseitig: `Diagnosis_NonAdmin_Throws` + E2E `NonAdmin_SeesNotAuthorized`.
- UI: Diagnose-Zelle + „Jetzt generieren" inkl. Rückmeldung: E2E `Admin_CreateEditDeactivateDeleteRule`.
- Keine Migration nötig (on-demand berechnet); `OptionalRestoreColumns` unverändert korrekt.
