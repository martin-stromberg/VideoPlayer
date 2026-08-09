# Testergebnisse

Datum: 2026-08-09

## Zusammenfassung

Status: Keine Fehler

## Ausgefuehrte Tests

```text
dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj
```

Ergebnis:

```text
Bestanden!   : Fehler:     0, erfolgreich:    56, uebersprungen:     0, gesamt:    56, Dauer: 8 s - VideoWebPlayer.Tests.dll (net10.0)
```

## Fehlgeschlagene Tests

Keine.

## Hinweise

- Bekannte Warnung `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 mit hoher Sicherheitsanfaelligkeit: https://github.com/advisories/GHSA-2m69-gcr7-jv3q.
