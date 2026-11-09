# Unabhängige Abnahme – Schritt 2 Serverintegration

## Ergebnis

**Status:** Erfüllt

Geprüfter Integrationsstand: `194f0e64eceb0fb403f9ee1eb03ed1977b2b9baa`, Basis `6a89228edf77a756563abbc7c211b8abe634533f` (`staging` beim Integrationsstart). Zugehöriger PR: https://github.com/martin-stromberg/VideoPlayer/pull/287.

Prüfer: Codex `accept_server_integration`, unabhängig vom Integrationsagenten. Eigener Klon `.work/seasonal-server-integration-review`, Branch `review/saisonlisten-schritt-2`. Erst Übergaben, dann Git-Historie und tatsächlicher Diff gelesen. Integrationscheckout, Originalservercheckout und Remote-Refs unverändert. Keine Produktkorrekturen oder Commits durch den Prüfer.

## Abweichungen

Keine.

## Hinweise

- Der Integrationsdiff umfasst 23 Dateien: saisonale Produkt-/Test-/Hilfedateien sowie die nachvollziehbare Integrationskoordination. Die Historie enthält genau drei eigene Commits oberhalb der aktuellen Stagingbasis, keine Zusammenführung der alten fremden Feature-Basishistorie.
- Die tatsächlichen Hinzufügungs-/Entfernungszeilen aller elf C#/Razor-Dateien sind mit dem zuvor unabhängig abgenommenen saisonalen Diff (`eae60a6` bis Produktcommit `a36d2db5272b122dff0b7f54e3b9d8ff29309438`) identisch. `MovieCollectionDetails.razor` und `ServiceCollectionExtensions.cs` enthalten zusätzliche aktuelle Stagingänderungen; ihre gesamten Blobs sind deshalb erwartungsgemäß verschieden, die saisonalen Änderungsdeltas aber identisch. Diese Stagingänderungen bleiben erhalten.
- README und beide Sprachabschnitte der Release Notes wurden ausschließlich additiv ergänzt (README +1/-0, Release Notes +4/-0). Keine Migrationen, Pakete, Projektdateien oder fremden Funktionen geändert.
- Tatsächlichen authentifizierten Controller samt bestehendem Bearer-Filter geprüft. Die Benutzeridentität wird serverseitig aufgelöst. Filme, Sammlungen, Sammlungsmitglieder und Serien werden auf erlaubte Quellen beschränkt; verschachtelter Sammlungskontext wird nur für erlaubte Quellen geliefert. Aktive Sektionen sind nicht leer und erhalten die exklusive UTC-Ablaufgrenze. Der Webbereich verwendet den echten HTTP-Client statt EF-Abfragen und entfernt abgelaufene Sektionen. Die ergänzte Filmroute verwendet die vorhandene Detailansicht und erhält neuere Stagingfunktionalität.
- Unabhängiger Testlauf im eigenen Klon: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~SeasonalHomeServiceTests|FullyQualifiedName~SeasonalGenresE2ETests" --disable-build-servers`: **12 bestanden, 0 Fehler, 0 Skips**, Exitcode 0, Testdauer 14 Sekunden. Darunter vier echte Playwright-/Kestrel-/SQLite-Flüsse (Authentifizierung/HTTP-API, Quellenfilter/Watched, Mediennavigation, Ablauf auf geöffneter Seite/Reload).
- Lokales maschinenlesbares Ergebnis im Mobile-Workspace: `.work/server-integration-review-results/acceptance-seasonal-integration.trx`. Der Reviewklon ist nach dem Lauf hinsichtlich Produktcode unverändert. `git diff --check` des Integrationsdiffs ist erfolgreich.
- Die unabhängigen Quellenfilter-/Ablauf-Mutationsnachweise aus `../saisonlisten/acceptance-schritt-1.md` bleiben für die exakt identischen saisonalen Änderungen gültig. Die unveränderten Tests wurden auf der aktuellen Integrationsbasis erneut selbst ausgeführt; keine Testabschwächung, kein Skip und keine spekulative Korrektur.
- Debug-/Release-Build, exakter CI-Formatcheck und vollständige Serversuite (2434 bestanden, 0 Fehler, 2 bestehende Skips) sind für exakt diesen Integrationsstand im Implementiererbericht `test-results.md` dokumentiert. Sie wurden nicht unnötig erneut vollständig ausgeführt; sie ersetzen nicht den oben unabhängig ausgeführten gezielten Lauf.
- Keine `RaiseUiActionRequested`-Aktionen im betroffenen Backend-/Razor-Diff vorhanden.
- Diese lokale fachliche Abnahme ersetzt keine grüne Remote-CI und keine Prüfung eines später veränderten PR-HEAD. Ready-/Mergeentscheidung, Main-Promotion und Deployment erfolgen durch Root bzw. den vorgesehenen Releaseweg. Keine Remoteänderung oder Laufzeitbereitstellung durch den Prüfer.
- Der mobile WinRT-Shutdownbefund und der fehlende saubere mobile 72/72-Gesamtlauf sind gesonderte offene Nacharbeit; die Serverabnahme behauptet keinen mobilen Gesamtabschluss.
