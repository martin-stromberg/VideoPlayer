# Testergebnisse

Ausgefuehrt am: 2026-08-10

## Build

Kommando:

```text
dotnet build
```

Ergebnis: Erfolgreich (Exit-Code 0), 0 Fehler. Es bleiben bestehende Warnungen.

## Tests

Kommando:

```text
dotnet test --no-build
```

Ergebnis: Erfolgreich (Exit-Code 0), 85 bestanden, 0 fehlgeschlagen, 0 uebersprungen.

## Warnungen

- `NU1903`: Die Pakete `SQLitePCLRaw.lib.e_sqlite3` und `SQLitePCLRaw.lib.e_sqlite3.android` 2.1.11 weisen bekannte hoch eingestufte Sicherheitsanfaelligkeiten auf.
- Weitere bestehende Compiler- und Analyzer-Warnungen verhindern den erfolgreichen Lauf nicht.

## Fehlgeschlagene Tests

Keine.

## Gesamtbewertung

Keine Fehler im aktuellen Lauf.
