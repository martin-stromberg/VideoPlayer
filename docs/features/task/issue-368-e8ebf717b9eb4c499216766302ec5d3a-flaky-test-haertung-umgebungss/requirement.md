# Übersetzte Anforderung: Flaky-Test-Härtung — OsInterface-Lane, Deadlines/Cleanup und Retry-/Quarantäne-Konvention

### Fachliche Zusammenfassung

Die CI-Testausführung des Repositories (`.github/workflows/staging-ci.yml`, `.github/workflows/pr-staging-ci.yml`) wird um eine Lane-Trennung für umgebungssensitive Tests erweitert: Tests mit echter Betriebssystem-Schnittstellenberührung (Playwright/Browser, echte Kindprozesse wie ffmpeg/ffprobe, echte Temp-Verzeichnisse/SQLite-Dateibanken, Loopback-Ports, enge Zeitbudgets) erhalten einen `OsInterface`-Trait und laufen in einem eigenen, best-effort-CI-Job (`continue-on-error`), während die reguläre Lane deterministisch und blockierend bleibt. Ergänzend werden Cleanup-Pfade in Test-Fixtures gehärtet (Prozess-Kill, Temp-Verzeichnis-Löschung mit Retry/Backoff), Poll-Deadlines an die Kind-/Job-Budgets angeglichen, eine `Flaky`-Retry-Konvention (genau ein automatisierter Re-Run nur markierter Fehlschläge) samt Meta-Test-Absicherung und eine TRX-basierte Flake-Statistik mit Quarantäne-Empfehlung eingeführt. Blaupause ist die Referenzumsetzung `martin-stromberg/Softwareschmiede` PR #432 — deren Annahmen sind zu prüfen, nicht ungeprüft zu kopieren.

### Fachlicher Scope

- **Verarbeitungseinheit:** Ein CI-Testlauf der `VideoWebPlayer.Tests`-Suite, zerlegt in Lanes. Die beobachtete Einheit innerhalb des Laufs ist der einzelne Test (Methode bzw. Theory-Datenzeile) mit oder ohne Betriebssystem-Schnittstellenberührung; die Trait-Zuordnung entscheidet, welcher Lane er angehört. Nicht zum Scope gehören produktive Anwendungspfade — verarbeitet wird ausschließlich die Test- und CI-Ebene.
- **Auslösende Ereignisse:** CI-Systemereignisse — Push auf `staging` (`staging-ci.yml`) und Pull Request gegen `staging` (`pr-staging-ci.yml`). Zusätzlich zu prüfen: der Release-Gate-Lauf in `release.yml`, der die volle Suite ungefiltert ausführt (verifizierter Befund, siehe Offene Fragen). Lokal auslösbar sind die neuen Skripte (`scripts/*.ps1`) über `pwsh`.
- **Erwartete Wirkung:** Sichtbar im CI-Verhalten für den Betreiber-/Reviewer-Kontext:
  - Ein einzelner umgebungssensitiver Fehlschlag macht den regulären `build & test`-Job nicht mehr rot; die reguläre Lane bleibt strikt grün gefordert und speist weiterhin das 70-%-Coverage-Gate.
  - Ein separater OS-Interface-Job (best effort, `continue-on-error: true`, eigenes `timeout-minutes`, eigene TRX-Artefakte) führt die `OsInterface`-Tests aus; Fehlschläge dort sind als Job-Ergebnis, Annotation und Artefakt sichtbar, blockieren aber weder Merge noch Release-Kette.
  - Genau ein automatisierter Re-Run ausschließlich der `Flaky`-markierten Fehlschläge der OS-Lane (Referenz: `scripts/Retry-FlakyTests.ps1`); persistente Fehlschläge bleiben ehrlich sichtbar.
  - Ein Report-Schritt (Referenz: `scripts/Analyze-FlakyTestResults.ps1`) wertet die TRX-Artefakte aus und meldet Flake-Statistik sowie Quarantäne-Kandidaten (Annotations/Report).
  - Das Ergebnis des Audits (welche Tests als `OsInterface` gelten) ist als Liste im PR dokumentiert.
- **Umgang mit fehlenden Objekten:**
  - `OsInterface`-/`Flaky`-Trait-Infrastruktur: im Repo nicht vorhanden — zu erzeugen (Blaupause: Konstanten-Klasse `TestCategories`, `ITraitAttribute`-Implementierungen `OsInterfaceAttribute`/`FlakyAttribute`; xunit.v3 3.2.2 ist bereits referenziert und unterstützt `ITraitAttribute`).
  - `TempDirectoryCleanup`-Helfer (Retry/Backoff für `Directory.Delete`): nicht vorhanden — zu erzeugen, ungeschützte Löschstellen darauf umzustellen.
  - PowerShell-Skripte `Retry-FlakyTests.ps1`/`Analyze-FlakyTestResults.ps1` (ggf. mit `scripts/lib/`): zu erzeugen; `scripts/` existiert bereits, enthält aber nur `resolve-release-version.mjs` (Node).
  - Hilfe-Dokumentation `docs/help/stabilitaet/os-interface-tests.md`: Verzeichnis `stabilitaet` existiert nicht — neu anzulegen; ergänzend `docs/STAGING_WORKFLOW.md` (dort ist das Lane-/Job-Modell beschrieben) aktualisieren.
  - `Integration`-Trait-Menge: verifizierter Befund — im gesamten Testprojekt existiert **kein** `Trait("Category", "Integration")` (0 Treffer; nur 79× `Category=E2E`). Der CI-Schritt `--filter "Category=Integration"` matcht daher aktuell 0 Tests (No-op-Lane); die im Issue behauptete Doppelausführung findet faktisch nicht statt, droht aber, sobald `Integration`-Traits vergeben werden. Umgang (Traits einführen, Lane entfernen oder Filter korrigieren) ist zu klären — siehe Offene Fragen.
  - Quarantäne-Issue-Objekt: bei Überschreitung des (noch festzulegenden) Schwellwerts zu erzeugen; ob automatisch im Workflow oder als dokumentierte manuelle Pflicht, ist offen (Blaupause: reine Empfehlung im Report).
- **Scope-Ausschlüsse:**
  - Kein Blanket-Retry der Gesamtsuite und kein zusätzlicher Framework-Retry (z. B. vstest-Retry) — Retry existiert ausschließlich als einmaliger, auf `Flaky`-markierte Fehlschläge der OS-Lane beschränkter Re-Run.
  - Kein separates Testprojekt für OS-Tests — der Trait-Ansatz der Blaupause wird übernommen (Alternative im Analyse-Kommentar erwogen und verworfen).
  - Keine Änderungen an Anwendungscode, Datenmodell, EF-Migrationen oder UI (Ausnahme: Diagnose-Ausgaben/Timeouts in produktivem Code nur, soweit das Audit sie als Flake-Ursache erfordert — dann als eigene Commits).
  - Keine Senkung oder Aufweichung des 70-%-Coverage-Gates — nur die Datenbasis (welche Lane die Coverage liefert) ist zu prüfen.
  - Windows-spezifische Heuristiken der Vorlage (ConPTY/`PseudoConsole`, FlaUI/UI-Automation, Clipboard, NTFS-Junctions) sind nicht Schwerpunkt — CI läuft auf `ubuntu-latest`, Blazor-Server-App.
  - Die Behebung einzelner bekannter Flakes ist nicht das Ziel; Ziel ist die Konvention plus Infrastruktur (Grundsatz der Blaupause: „selten, lokalisiert, selbstdiagnostizierend").
  - Kein dauerhaft tolerierter Blindspot: ohne Quarantäne-/Re-Entry-Regel gilt die Lane-Trennung nicht als abgeschlossen (Zielkonflikt aus dem Analyse-Kommentar).

### Betroffene Klassen und Komponenten

Keine Datenmodellklassen, Services, Interfaces, Enums oder UI-Komponenten der Anwendung betroffen. Betroffene bzw. neu zu erstellende Artefakte:

- **CI-Workflows:**
  - `.github/workflows/staging-ci.yml` und `.github/workflows/pr-staging-ci.yml`: Job `build-and-test` umbauen — reguläre Lane filtert `OsInterface` aus, neuer paralleler OS-Job mit `continue-on-error: true`, eigenem `timeout-minutes`, Retry-Schritt (`if: failure()`), Analyse-Schritt (`if: always()`), angepassten TRX-Dateinamen und Artefakt-Uploads; `needs:`-Ketten so gestalten, dass `version`/`prerelease` (staging) nicht vom OS-Job abhängen (Blaupause-Muster).
  - `.github/workflows/release.yml`: Release-Gate läuft die volle Suite ungefiltert (`final-test-results.trx`) — Geltungsbereich der Trennung dort klären.
- **Testinfrastruktur in `VideoWebPlayer.Tests` (neu, Blaupause-Namen):**
  - `Infrastructure/Testing/TestCategories.cs` (Konstanten `OsInterface`, `Flaky`)
  - `Infrastructure/Testing/OsInterfaceAttribute.cs`, `Infrastructure/Testing/FlakyAttribute.cs` (`ITraitAttribute` aus `Xunit.v3`, liefern `Category`-Traits)
  - `Infrastructure/Testing/FlakyConventionTests.cs` (Meta-Test: `Flaky` nur in Kombination mit `OsInterface`; zu erwägen: Lane-Matrix-Abdeckung, d. h. jeder Test gehört genau einer Lane)
  - `Helpers/TempDirectoryCleanup.cs` + zugehörige Tests (`Directory.Delete` mit Retry/Backoff, Restfehler nur protokollieren)
- **Umzustellende Test-Support-/Fixture-Klassen (Cleanup):**
  - `Helpers/BackupUploadSessionServiceTestSupport.cs` — ungeschütztes `Directory.Delete(Path, recursive: true)` in `Dispose` (ca. Zeile 59–64)
  - `Helpers/LocalMediaPlaylistE2ETestBase.cs` — bereits best-effort (Zeile ~90), auf `TempDirectoryCleanup` vereinheitlichen
  - `Helpers/PairingWebApplicationFactory.cs` (`CreateTempDbPath`, Zeile 24) — Temp-DB-Pfade/Löschung
  - weitere `IAsyncLifetime`-/`DisposeAsync`-Basisklassen unter `Helpers/` (z. B. `PlaylistsE2ETestBase`, `ContinueWatchingPlaylistTestBase`, `MediaBoxContextMenuE2ETestBase`, `TVSenderAdminE2ETestBase`) sowie Fixture-Stellen mit `Process.Start` — Kindprozesse mit `Kill(entireProcessTree: true)` beenden
- **Audit-Kandidaten für `OsInterface`-Markierung:** alle 79 `Category=E2E`-Klassen (Playwright), `Services/SegmenterProcessSupportTests.cs`, `Services/FfmpegLiveStreamSegmenterTests*`/`FfmpegLiveStreamSegmenterE2ETests.cs`, `LocalMediaSourceReaderTests`, `MediaProbeHttpIntegrationTests.cs` (trägt bereits `E2E`), `LiveStreamE2ETests.cs`, `LiveStreamCoordinatorTests.cs`, `LiveStreamWorkerTests.cs`, `Services/FfmpegLocatorTests.cs`, `Services/FfprobeLocatorTests.cs`, `InternalMediaStreamControllerTests.cs`, `Services/AutoUpdateProtectedFilesTests.cs`, `Extensions/LocalConfigurationTests.cs`, Backup-Tests — finale Menge ergibt das Audit.
- **Skripte (neu):** `scripts/Retry-FlakyTests.ps1`, `scripts/Analyze-FlakyTestResults.ps1`, ggf. `scripts/lib/Trx.ps1`, `scripts/lib/TestDiscovery.ps1` (Blaupause-Struktur; an dieses Repo anpassen: ein Testprojekt statt zwei, TRX-Namen `test-results.trx`, `integration-test-results.trx`, `e2e-test-results.trx`).
- **Dokumentation:** `docs/help/stabilitaet/os-interface-tests.md` (neu), `docs/STAGING_WORKFLOW.md` (Lane-Modell, `gh run rerun --job <id>`-Regel nur für den OS-Job), `README.md`, `docs/RELEASE_NOTES.md` gemäß Definition of Done.

### Implementierungsansatz

1. **Audit:** Test-Dateien mit angepasster Heuristik durchgehen (Playwright/`IPage`, `Process.Start`/ffmpeg/ffprobe, echte Temp-Verzeichnisse und SQLite-Dateibanken, Loopback-Ports/Netzwerk, enge Zeitbudgets/Polling-Loops); Ergebnis als Liste im PR dokumentieren.
2. **Trait-Infrastruktur** analog Blaupause anlegen und Audit-Ergebnis markieren; Meta-Test `FlakyConventionTests` absichern.
3. **Workflow-Umbau in beiden CI-Workflows:** OS-Job parallel zu `build-and-test`; Filter je nach Trait-Entscheidung (`Category!=OsInterface` bzw. `Category!=E2E&Category!=OsInterface`); OS-Job benötigt eigene Playwright-Installation (`pwsh .../playwright.ps1 install --with-deps chromium` — Laufzeit-/Caching-Kosten bedenken); Retry-Schritt wertet das OS-Primär-TRX aus und wiederholt nur `Category=Flaky`-Fehlschläge einmal; Analyse-Schritt läuft immer und bleibt Exit-0.
4. **Deadline-Angleichung:** Host-Poll-Deadlines > Kind-/Phasen-Budget prüfen in `SegmenterProcessSupportTests` (enthält bereits `TimeSpan`-Budgets, z. B. 5 s), ffmpeg-/Segmenter-Tests und Playwright-Timeouts gegen das 30-min-Job-Budget; Diagnose-Ausgabe (Phase/State/Error) beibehalten/ergänzen.
5. **Cleanup-Härtung:** `Kill(entireProcessTree: true)` in Fixture-`Dispose`/`DisposeAsync`/`finally`, `TempDirectoryCleanup` für alle Temp-Pfade; ein Cleanup-Fehler darf den eigentlichen Assert nie als `AggregateException` überdecken.
6. **Verifikation:** mehrere komplette CI-Durchläufe selbstständig wiederholen (Referenz: drei); Erfolgsdefinition: reguläre Lane strikt grün, OS-Lane-Fehlschläge toleriert, aber im Report sichtbar.

### Konfiguration

- Keine Anwendungs-/Laufzeitkonfiguration erforderlich — das Verhalten liegt versionskontrolliert in den Workflow-Dateien (Filterausdrücke, `timeout-minutes`, `continue-on-error`, Artefakt-Namen) und in C#-Konstanten (`TestCategories`).
- Skript-Schwellwerte als Parameter mit Defaults (Blaupause-Vorschlag, kalibrierbar): Auswertefenster `-MaxRuns` 20, Quarantäne ab ≥ 2 Flake-Ereignissen oder ≥ 20 % Fehlrate bei Mindestausführungszahl — finaler Schwellwert ist offener Punkt.
- Dokumentierte manuelle Regel: `gh run rerun --job <id>` nur für den OS-Job, nie für die blockierende Lane (Ablage in `docs/help/stabilitaet/os-interface-tests.md` bzw. `docs/STAGING_WORKFLOW.md`).

### Offene Fragen

- **Trait-Modell:** Ersetzt `OsInterface` die bestehende `E2E`-Kategorie oder wird es als zweite Achse geführt (z. B. `Integration ∧ OsInterface`)? Entscheidung bestimmt Filterausdrücke, Meta-Test und ob Klassen wie `MediaProbeHttpIntegrationTests` umsortiert werden.
- **`Integration`-Lane (verifizierte Korrektur zum Issue):** Es existiert kein `Category=Integration`-Trait — die Lane ist heute ein No-op und die behauptete Doppelausführung findet nicht statt. Soll beim Filter-Umbau (a) `Integration` als echte Trait-Kategorie eingeführt, (b) der Schritt entfernt oder (c) der Filter korrigiert werden? Damit entfällt die Doppelausführungs-Frage oder wird neu gestellt.
- **Geltungsbereich `release.yml`:** Dort läuft die volle Suite ungefiltert als Release-Gate (blockierend). Bleibt das so, oder wird auch dort getrennt/best effort gelaufen?
- **Quarantäne-Regel:** Konkreter Schwellwert (Blaupause-Defaults: ≥ 2 Flake-Events oder ≥ 20 % Fehlrate im Fenster von 20 Läufen), Verantwortlichkeit und Frist für Quarantäne-Issues, Kriterium für den Re-Entry in die blockierende Lane — und ob das Issue automatisch angelegt wird oder der Report nur empfiehlt.
- **Coverage-Gate:** Die 70-%-Pflicht hängt am `Category!=E2E`-Lauf — beim Verschieben von Tests in die OS-Lane prüfen, dass die Abdeckung nicht unter den Schwellwert rutscht (Datenbasis des Gates ändert sich).
- **Geltungsbereich der Workflows:** OS-Job in `pr-staging-ci.yml` und `staging-ci.yml` gleichermaßen? Annahme: ja (beide Workflows sind nahezu identisch) — zu bestätigen.
- **TRX-Artefakt-Kopplung:** Die Flake-Statistik braucht stabil benannte TRX-Dateien (aktuell `test-results.trx`, `integration-test-results.trx`, `e2e-test-results.trx`); Namenskonvention für die OS-Lane (Blaupause: `test-results-os-interface*.trx`) festlegen.
