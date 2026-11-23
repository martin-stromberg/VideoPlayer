# Stabilität

Konventionen und Werkzeuge rund um die Stabilität der `VideoWebPlayer.Tests`-Suite:
Lane-Trennung umgebungssensitiver Tests, Flaky-Retry, Flake-Statistik mit
Quarantäne-/Re-Entry-Regel und Fixture-Cleanup.

## Inhalt

- [OS-Schnittstellen-Tests](os-interface-tests.md) — Klassifizierungskriterium,
  `OsInterface`/`Flaky`-Tagging, lokale Befehle, Einmal-Retry, Flake-Statistik,
  Quarantäne-/Re-Entry-Regel, `TempDirectoryCleanup`-Konvention und CI-Verhalten
