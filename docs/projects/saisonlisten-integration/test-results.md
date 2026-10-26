# Prüfungen: Saisonlisten auf aktuellem staging

Geprüft am: 2026-10-26. Integrationsbasis: `6a89228edf77a756563abbc7c211b8abe634533f`.

## Ergebnis

Implementiererprüfung erfolgreich; unabhängige Abnahme und PR-CI bleiben separate Schritte.

- `dotnet build VideoPlayer.sln --configuration Debug`: erfolgreich, 0 Fehler; 141 vorhandene Warnungen.
- `dotnet build VideoPlayer.sln --configuration Release --no-restore`: erfolgreich, 0 Fehler; 141 vorhandene Warnungen.
- `dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error`: erfolgreich, Exitcode 0, keine Änderungen nötig.
- Saisonale Service-/SQLite-/Zeitfenster- und echte Web-E2E-Tests: **12 bestanden, 0 Fehler, 0 Skips** (16 s).
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --no-restore`: **2434 bestanden, 0 Fehler, 2 bestehende Skips, 2436 gesamt**, Exitcode 0, etwa 5 Minuten.
- `git diff --check`: erfolgreich.

Die beiden bestehenden Skips sind `Run_OnUnix_SetsExecutableBit` (auf Windows) und der bereits deaktivierte `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`. Keine Tests wurden für diese Integration übersprungen, deaktiviert oder abgeschwächt.

Die Produktdateien wurden aus dem zuvor unabhängig abgenommenen saisonalen Lieferdiff übernommen. Aktuelles staging bleibt vollständig erhalten; nur README-/Release-Notes-Kontext musste manuell eingepasst werden. Keine Paket-, Schema- oder fremden Featureänderungen.

Lokale ausführliche Logs/TRX im Mobile-Workspace: `.work/server-integration-results/` (`build-debug.log`, `build-release.log`, `format-check.log`, `seasonal-integration.trx`, `server-integration-full.trx`). Die ursprüngliche saisonale Mutationstest-Abnahme steht in `../saisonlisten/acceptance-schritt-1.md`; die unabhängige Prüfung dieser aktuellen Integration ist noch ausstehend.
