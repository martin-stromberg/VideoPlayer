# Unabhängige Backend-Abnahme – Schritt 1 API und Web

## Ergebnis

**Status:** Erfüllt

Geprüfter Implementierungsstand: `1bb12a42ffeadab62702117fcdc9eb9955e7fd48`, Basis `eae60a6003ac0619958331351d21c5d983cb175f`.
Prüfer: Codex review_code, unabhängig vom Implementierungsagenten. Eigener Klon `.work/seasonal-server-review`, Branch `review/saisonlisten`. Der Implementierungscheckout und das ursprüngliche Serverrepository wurden nicht verändert.

## Abweichungen

Keine.

## Hinweise

- Tatsächlichen Diff von API, Service, Zeitfenster, DTO/Client, Webkomponente und ergänzter Filmroute geprüft. Serverseitige Quellenprüfung umfasst Filme, Sammlungen, Sammlungsmitglieder und Serien; verschachtelter Sammlungskontext wird nur bei freigegebener Quelle geliefert.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter FullyQualifiedName~SeasonalHomeServiceTests --disable-build-servers`: unabhängig 8/8 bestanden.
- Gegenbeispiel Quellenberechtigung: in der eigenen Prüfkopie den Film-Quellenfilter vor `db.MovieGenres.Any` entfernt. Der unveränderte Test `Service_FiltersSourcesEmptyGenresAndMapsGroupedSortedWatchedMedia` schlug mit zusätzlichem unerlaubtem Film `Secret` fehl (Exitcode 1). Produktdatei danach mit Git wiederhergestellt.
- Gegenbeispiel Saisonende: ausschließlich in der Prüfkopie `end.AddDays(1)` durch `end.AddDays(2)` ersetzt. Die unveränderten Saison-Tests schlugen fünfmal wegen des falschen Ablaufdatums fehl (Exitcode 1). Produktdatei danach mit Git wiederhergestellt.
- Anschließender unveränderter Lauf: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore --filter "FullyQualifiedName~SeasonalHomeServiceTests|FullyQualifiedName~SeasonalGenresE2ETests" --disable-build-servers`: 12/12 bestanden, keine Skips, Dauer 28 s. Darunter alle vier echten Playwright-/Kestrel-/SQLite-Flüsse: Anmeldung/Filter/Watched, Film-/Sammlungs-/Seriennavigation, authentifizierte HTTP-API samt 401 und leerer Antwort, Ablauf bei geöffneter Seite und Reload.
- Browserpfad: `PLAYWRIGHT_BROWSERS_PATH` zeigt auf die installierten Browser in `.work/seasonal-server/.playwright-browsers`.
- Nach Mutationen ist `git diff` der Prüfkopie leer. Keine Produktkorrektur durch den Prüfer, keine Tests abgeschwächt und keine Produktcommits erzeugt.
- Die vorhandene fehlende Filmroute ist im tatsächlichen Diff und im getrennten Begleitcommit `6b819d9` geprüft; sie verwendet die bestehende Detailansicht und lädt sowohl einzelne Filme ohne Sammlung als auch Filme mit Sammlung.
- Keine `RaiseUiActionRequested`-Aufrufe im betroffenen Backend/UI oder Mobile-Diff vorhanden; somit keine fehlenden Aktionshandler aus diesem Mechanismus.
- Der eigenständige Lifecycle-Testschritt führt anschließend die breiten Abschlussprüfungen aus. Der vorherige Implementierungsbericht dokumentiert Debug/Release und volle Suite; diese Selbstangaben ersetzen die hier selbst ausgeführten 12 Tests nicht.
