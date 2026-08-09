# Testergebnisse

Ausgeführt am: 2026-08-09

## Build

Kommando:

```text
dotnet build VideoWebPlayer/VideoWebPlayer.csproj --no-restore
```

Ergebnis: Erfolgreich (Exit-Code 0), 0 Fehler, 1 Warnung.

## Tests

Kommando:

```text
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore
```

Ergebnis: Erfolgreich (Exit-Code 0), 67 bestanden, 0 fehlgeschlagen, 0 übersprungen.

## Warnungen

- `NU1903`: Das Paket `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 weist eine bekannte hoch eingestufte Sicherheitsanfälligkeit auf: https://github.com/advisories/GHSA-2m69-gcr7-jv3q
- Die Warnung wurde beim Build und beim Testlauf ausgegeben. Weitere Compiler- oder Analyzer-Warnungen wurden in diesen Läufen nicht ausgegeben.

## Fehlgeschlagene Tests

Keine.

## Gesamtbewertung

Keine Fehler
