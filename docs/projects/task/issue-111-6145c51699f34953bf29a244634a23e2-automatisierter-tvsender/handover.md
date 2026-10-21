# Handover – Automatisierter TV-Sender

## Bearbeiter

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Devin |  | aktiv | 2026-10-21 |
| 2 | Devin |  | fertig | 2026-10-21 |
| 3 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-3-sendeplan-erzeugen | aktiv | 2026-10-21 |

## Übergabestand je Schritt

### Schritt 1: Videolaufzeit erfassen — FERTIG (abgenommen)

- ffprobe-basierte Laufzeitermittlung (`Duration`/`DurationProbedAt` an MediaItem), Scan-Reset, Nachtrags-Backfill-Worker, Migration, Backup-Registrierung. Abnahme: `acceptance-schritt-1.md` — erfüllt.

(Bisher keine Übergaben — Projektstart.)


### Schritt 2: Sender und Programmregeln verwalten — FERTIG (abgenommen)

- TVChannel + ProgramRule (alle Regelarten/Materialarten), Admin-CRUD serverseitig, MediaSearchSelector-Auswahl, HolidayFederalState in Setup, Migration + Backup-Registrierung. Abnahme: `acceptance-schritt-2.md` — erfüllt.
- Hinweis an Schritt 3: verwaiste MaterialIds ohne DB-FK sind Designentscheidung, Behandlung dort vorgesehen.

### Schritt 3: Sendeplan aus Regeln erzeugen

- Umgesetzt: `ProgramSlot` (weiche Referenzen, `RuleType`-Snapshot), `ProgramRuleCursor` (Fortsetzung je Regel, FK+Kaskade), `Setup.ProgramScheduleHorizonHours`/`ProgramScheduleIntervalMinutes`, `GermanHolidayCalendar` (Computus + Landesfeiertage), `ProgramScheduleService` (Expand-only, Prioritätskonflikte, Füller-Lückenfüllung, Freigabe- und Laufzeitfilter, verwaiste Materialien), `ProgramScheduleWorker`, Admin-UI-Felder, Migration `AddProgramSchedule`, Backup-Registrierung inkl. Alt-Backup-Regressionstest.
- Entscheidungen: Ausstrahlungen im 24-h-Lookback konsumieren den Cursor (vergangene Sendung = „gelaufen"); der letzte Füller darf über das Horizont-Ende hinauslaufen; Freigabe = für jeden Nicht-Admin-Benutzer sichtbar (Quelle ODER Einzelfreischaltung); Extend-only — Reaktion auf Änderungen ist Schritt 4.
- Tests: 26 neue Tests (Generation, Konflikte, Füller, Freigabe, Feiertage, Settings, Worker, Alt-Backup) plus erweiterter E2E (`TVSenderAdminE2ETests`); alle grün.
