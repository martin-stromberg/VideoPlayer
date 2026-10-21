# Handover – Automatisierter TV-Sender

## Bearbeiter

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Devin |  | aktiv | 2026-10-21 |

## Übergabestand je Schritt

### Schritt 1: Videolaufzeit erfassen — FERTIG (abgenommen)

- ffprobe-basierte Laufzeitermittlung (`Duration`/`DurationProbedAt` an MediaItem), Scan-Reset, Nachtrags-Backfill-Worker, Migration, Backup-Registrierung. Abnahme: `acceptance-schritt-1.md` — erfüllt.

(Bisher keine Übergaben — Projektstart.)


### Schritt 2: Sender und Programmregeln verwalten — FERTIG (abgenommen)

- TVChannel + ProgramRule (alle Regelarten/Materialarten), Admin-CRUD serverseitig, MediaSearchSelector-Auswahl, HolidayFederalState in Setup, Migration + Backup-Registrierung. Abnahme: `acceptance-schritt-2.md` — erfüllt.
- Hinweis an Schritt 3: verwaiste MaterialIds ohne DB-FK sind Designentscheidung, Behandlung dort vorgesehen.
