# Testergebnisse

Ausgeführt am: 2026-08-20

## Ergebnisse

| Prüfschritt | Ergebnis |
|---|---|
| `dotnet build VideoPlayer.sln` | Erfolgreich, 0 Fehler, 72 Warnungen |
| `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~MediaMetadataEditorServiceTests|FullyQualifiedName~ItemsControllerMetadataTests|FullyQualifiedName~MediaSourceClassifier" --no-build` | Erfolgreich, 24 von 24 Tests bestanden, 0 übersprungen |
| `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` | Erfolgreich, 132 von 132 Tests bestanden, 0 übersprungen |

Der Build meldet bestehende Paket-Sicherheitswarnungen für `SQLitePCLRaw.lib.e_sqlite3` und `SSH.NET` sowie bereits vorhandene Compiler-/Framework-Warnungen.

## Fehlgeschlagene Tests

Keine Fehler
