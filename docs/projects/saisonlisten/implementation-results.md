# Implementierungsnachweis Saisonlisten (vor unabhängiger Abnahme)

## Stand

Serverbasis: `eae60a6003ac0619958331351d21c5d983cb175f`.
Isolierter Branch: `task/38598ab90d844bb0bbc42a195643b6e3-saisonlisten-schritt-1-api-web`.
Das Ursprungsrepository wurde einschließlich `.git` nicht verändert.

Implementiert sind API-Vertrag, Quellenfilterung, Sammlungsgroupierung, Gesehenstatus,
UTC-Ablauf, Web-HTTP-Anbindung und aktive Entfernung abgelaufener Sektionen.

Für den geforderten Filmkartenfluss musste die vorhandene, bisher nicht implementierte
Webroute `/movie/{id}` ergänzt werden. Sie verwendet die vorhandene Filmdetailkomponente
und unterstützt auch Filme ohne Sammlung. Diese notwendige Begleitkorrektur wird in
einem separaten Zwischencommit ausgewiesen; keine anderen Funktionen wurden erweitert.

## Prüfungen des Implementierers

- `dotnet build VideoPlayer.sln -c Debug --no-restore`: erfolgreich, 0 Fehler.
- `dotnet build VideoPlayer.sln -c Release --no-restore`: erfolgreich, 0 Fehler (bestehende Warnungen).
- `SeasonalHomeServiceTests`: 8 bestanden, 0 Fehler, 0 übersprungen.
- `SeasonalGenresE2ETests`: 4 bestanden, 0 Fehler, 0 übersprungen, echter Headless-Chromium gegen Kestrel/SQLite.
- Vollständige Server-Testsuite: 2358 bestanden, 0 Fehler, 2 bestehende Skips, gesamt 2360, Dauer 5m23s.

Beim ersten E2E-Vorlauf fehlte der zur installierten Playwright-Version gehörige Browser.
Er wurde anschließend in `.playwright-browsers/` im isolierten Checkout installiert.
Die ersten tatsächlichen Browserläufe deckten statisches SSR (Ablauftimer war nicht
interaktiv), fehlenden Test-API-Key und die nicht existierende Filmroute auf; nach
Korrektur liefen sämtliche vier Browserflüsse erfolgreich. Kein Test abgeschwächt.

Für Folgeläufe `PLAYWRIGHT_BROWSERS_PATH` auf den absoluten Ordner
`.work/seasonal-server/.playwright-browsers` im Mobile-Workspace setzen.
Der neue E2E-Test behandelt fehlende Browser als Fehler (kein stiller Erfolg).

## Unabhängige Abnahme

Noch offen. Der Implementierer erstellt keinen eigenen Abnahmebericht.
Der Prüfer benötigt eigenen Checkout, Builds/volle Suite, Gegenbeispiele und
Mutation für die Quellenfilterung bzw. Ablaufprüfung nach `AGENTS.md`.
Kein finaler Feature-Commit und kein Push durch den Implementierer.

Die beiden unverändert vorhandenen Skips betreffen Unix-Dateirechte unter Windows und
einen bereits auf der Serverbasis explizit deaktivierten ContinueWatching-Test für Filme
ohne Sammlung (bestehender serverseitiger HTTP-500-Befund, nicht dieser Auftrag).
Keiner der zwölf neuen saisonalen Tests wurde übersprungen.

