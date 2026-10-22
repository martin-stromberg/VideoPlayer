# Testergebnisse – Schritt 22

- `dotnet build VideoPlayer.sln` Debug: 0 Fehler; Release: 0 Fehler (keine neuen Warnungen).
- `dotnet test VideoWebPlayer.Tests` vollständig: **2157 bestanden, 1 übersprungen (Alt-Skip)**.
  Erstlauf: 2 Playwright-E2E-Timeouts (`BackupUpload_ExceedingLimit_ShowsError`,
  `PlaylistSkipCollectionEntriesE2ETest`) — fachlich unrelated; Einzelwiederholung beider grün.
- Neue Tests (7): `ProgramScheduleServiceTests_Boundary` — Overshoot-Nachbarschaft,
  Invasor-Trim an teilvergangener Occurrence (Repro „heute 16:00, jetzt 16:30"), Stabilität
  über 3 Läufe, Oversized-Head-Konsum, Cleanup/Reset/Admin-Auth.
