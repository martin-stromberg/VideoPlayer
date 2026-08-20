# Testergebnisse

Status: Keine Fehler

## Ausgefuehrte Befehle

- `dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~MediaMetadataEditorServiceTests|FullyQualifiedName~MetadataEditorUiStateTests"`
  - Ergebnis: 26/26 bestanden.
- `dotnet build VideoPlayer.sln --no-restore`
  - Ergebnis: erfolgreich, 0 Fehler.
- `dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj --no-build`
  - Erster Lauf: 146/147 bestanden, ein nicht betroffener Parallelitaets-Test `EpisodeBackgroundImageServiceTests.Test_EnsureBackgroundImage_ThreadSafe_ParallelRequests` schlug fehl.
  - Wiederholungslauf: 147/147 bestanden.

## Fehlgeschlagene Tests

Keine im abschliessenden Lauf.
