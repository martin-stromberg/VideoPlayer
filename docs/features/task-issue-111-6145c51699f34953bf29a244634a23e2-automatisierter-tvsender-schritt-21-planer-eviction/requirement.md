# Anforderung — N16: Planer verdrängt Kandidaten en masse statt stabilen Plan

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N16 (Priorität: kritisch, BUG)

## Live-Befund (2026-10-22)

- Regel „Vorbendserien" (GenreRotation, täglich 16:00, 4 h): 35 Slots, **alle Displaced**,
  überlappend geclustert (8 Items starten ~14:43–14:45 morgen); heute 16 Uhr komplett leer.
- Gesamt: **4520 Displaced vs. 38 Scheduled** — Regel „Morgenprogramm" allein 4374 verdrängte.
- Material ist planbar (Items verknüpft, kein „kein planbares Material" im Log).

## Umzusetzen

1. Reproduktion als Test: realistische Szene (Genre-/Serien-Regel + Material) → `EnsureCoverageAsync`
   → zweiter Lauf / `ReconcileAsync` darf bestehende Slots NICHT verdrängen (idempotent).
   Mehrere Regeln mit überlappenden Fenstern + Priorität: nur echte höherpriore Konflikte verdrängen.
   Mechanismus der Overlap-Cluster finden.
2. Fix: Eviction nur bei echten Konflikten (Priorität/Fenster), keine Oszillation; Platzierung
   kumulativ innerhalb der Occurrence; Re-Run idempotent.
3. `coverageStart`/Horizont: warum bleibt heute 16:00 leer — Occurrence-Fenster prüfen.
4. Keine Migration/Massenbereinigung ohne Not — Fokus auf den Mechanismus.
5. Tests: Overlap-Cluster-Reproduktion, Idempotenz, Prioritäts-Konflikt korrekt, Displaced-Anzahl
   sinkt drastisch.
