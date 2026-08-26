# Testergebnisse

## Ergebnis

Alle ausgeführten Prüfungen waren erfolgreich.

## Befehle

```powershell
dotnet run --no-restore --project tools/SecretScan/SecretScan.csproj -- --root .
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --configuration Release /p:NoWarn=NU1903
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AboutPageE2ETests" --logger "console;verbosity=minimal"
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"
```

## Resultate

- Secret scan passed.
- Markdown link check passed: 59 files, 72 local links.
- Build erfolgreich: 0 Fehler, 320 Warnungen.
- About-E2E-Test erfolgreich: 1 Test.
- Voller Testlauf erfolgreich: 189 Tests.
