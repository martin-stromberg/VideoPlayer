# Umsetzungsplan: Flaky-Test-Härtung — OsInterface-Lane, Deadlines/Cleanup und Retry-/Quarantäne-Konvention (Issue #368)

## Übersicht

Die CI-Testausführung wird um eine Lane-Trennung für umgebungssensitive Tests erweitert: Tests mit echter Betriebssystem-Schnittstellenberührung erhalten einen `OsInterface`-Trait und laufen in einem neuen, best-effort-CI-Job (`continue-on-error`), während die reguläre Lane strikt blockierend bleibt und weiterhin das 70-%-Coverage-Gate speist. Ergänzend werden Cleanup-Pfade in Test-Fixtures gehärtet (`TempDirectoryCleanup` mit Retry/Backoff, `Kill(entireProcessTree)`), enge Host-Poll-Deadlines an die Kind-Budgets angeglichen, ein einmaliger Flaky-Re-Run (`scripts/Retry-FlakyTests.ps1`) sowie eine TRX-basierte Flake-Statistik mit Quarantäne-Empfehlung (`scripts/Analyze-FlakyTestResults.ps1`) eingeführt. Referenz ist die Blaupause martin-stromberg/Softwareschmiede PR #432, angepasst auf ein Testprojekt, `ubuntu-latest`, Release-Builds und xUnit-`Assert` (kein FluentAssertions). Betroffen sind ausschließlich Test- und CI-Ebene — kein Anwendungscode, kein Datenmodell, keine UI.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein CI-Testlauf der `VideoWebPlayer.Tests`-Suite, zerlegt in zwei Lanes: regulär (`Category!=E2E&Category!=OsInterface`, blockierend, Coverage-Gate) und OS-Interface (`Category=OsInterface`, best effort). Beobachtete Einheit: der einzelne Test mit/ohne OS-Schnittstellenberührung; die Trait-Zuordnung entscheidet die Lane. |
| Auslösende Ereignisse | Push auf `staging` (`staging-ci.yml`), Pull Request gegen `staging` (`pr-staging-ci.yml`), Release-Lauf auf `main`/Tags (`release.yml`). Lokal auslösbar: `dotnet test --filter` je Lane, `pwsh scripts/*.ps1`. |
| Erwartete Wirkung | Ein umgebungssensitiver Fehlschlag macht `build & test` nicht mehr rot; der OS-Job zeigt Fehlschläge als Job-Ergebnis, Annotation und TRX-Artefakt, blockiert aber weder Merge noch `version`/`prerelease`-Kette; genau ein automatisierter Re-Run nur `Flaky`-markierter Fehlschläge; Flake-Statistik/Quarantäne-Empfehlung als Report. |
| Umgang mit fehlenden Objekten | `TestCategories`/`OsInterfaceAttribute`/`FlakyAttribute`/`FlakyConventionTests`: erzeugen (xunit.v3 3.2.2 liefert `ITraitAttribute`). `TempDirectoryCleanup`: erzeugen, ungeschützte Löschstellen umstellen. `scripts/*.ps1` + `scripts/lib/`: erzeugen (`scripts/` existiert). `docs/help/stabilitaet/`: neu anlegen. `Integration`-Trait-Menge: bleibt leer — der No-op-CI-Schritt wird entfernt (Entscheidung unten). Quarantäne-Issue: nicht automatisch — dokumentierte manuelle Pflicht bei `::warning::`-Empfehlung; Re-Entry aus der Quarantäne/`Flaky`-Markierung nach dokumentierter Regel (Fix-Referenz + Stabilitätsfenster, s. Designentscheidung „Quarantäne-/Re-Entry-Regel"). |

### Scope-Ausschlüsse

Alle Ausschlüsse wurden am 2026-11-22 per Issue-Kommentar (#368) dem Anwender zur Bestätigung vorgelegt; die Umsetzung erfolgt auf dieser Grundlage, das PR-Review dient als Bestätigungsinstanz.

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Blanket-Retry der Gesamtsuite und kein Framework-Retry (vstest o. ä.) — Retry nur als einmaliger Re-Run `Flaky`-markierter Fehlschläge der OS-Lane | Scope der Anforderung; Retry soll selten, lokalisiert und selbstdiagnostizierend bleiben | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |
| 2 | Kein separates Testprojekt für OS-Tests | Trait-Ansatz der Blaupause; Mehraufwand ohne Mehrwert bei einem Testprojekt | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |
| 3 | Keine Änderungen an Anwendungscode, Datenmodell, EF-Migrationen oder UI | Anforderung begrenzt den Scope auf Test-/CI-Ebene; Diagnose-/Timeout-Anpassungen in Produktivcode nur bei nachgewiesener Flake-Ursache als eigene Commits | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |
| 4 | Keine Senkung des 70-%-Coverage-Gates | Gate bleibt unverändert; nur die Datenbasis (reguläre Lane) wird verifiziert | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |
| 5 | Behebung einzelner bekannter Flakes (z. B. das Timing in `TvSenderOnAirE2ETests`) ist nicht Teil dieses Auftrags | Ziel ist Konvention plus Infrastruktur; der bekannte Flake wird als `Flaky` markiert statt gefixt | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |
| 6 | Windows-Heuristiken der Blaupause (ConPTY, FlaUI, Clipboard) werden nicht übernommen | CI läuft auf `ubuntu-latest`; relevante Heuristiken hier: Playwright/Chromium, ffmpeg/ffprobe-Kindprozesse, Kestrel-Loopback, SQLite-Dateibanken, UDP-Sockets | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |
| 7 | Kein automatisches Anlegen von Quarantäne-Issues im Workflow | Blaupause: reine Empfehlung im Report; manuelle Pflicht wird in der Hilfe-Doku festgeschrieben — vermeidet Issue-Spam ohne menschliche Einordnung | vorgelegt (Issue-Kommentar 2026-11-22, Bestätigung über PR-Review) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Trait-Modell | `OsInterface` als **eigene, zweite Trait-Achse** zusätzlich zu `E2E` (nicht als Ersatz). Alle `Category=E2E`-Tests erhalten zusätzlich `Category=OsInterface`; `Flaky` darf nur in Kombination mit `OsInterface` vorkommen. Lane-Zugehörigkeit wird ausschließlich über `OsInterface` gesteuert; `E2E` bleibt rein fachlicher Marker. | Vorgabe des Orchestrators, deckt sich mit der Blaupause (dort kommt `OsInterface` ebenfalls zusätzlich zu Bestandskategorien). Vorteil: `Category=E2E` bleibt als selektiver lokaler Filter nutzbar, und die Lane-Matrix ist über einen einzigen Achsenwert (`OsInterface` ja/nein) eindeutig partitionierbar — jeder Test gehört genau einer Lane. |
| Lane-Matrix-Absicherung | `FlakyConventionTests` prüft **zwei** Regeln per Reflection: (a) `Flaky ⇒ OsInterface` (Blaupausen-Regel), (b) `E2E ⇒ OsInterface` (Erweiterung). | Regel (a) verhindert, dass ein retry-berechtigter Test in die blockierende Lane rutscht. Regel (b) sichert die Partition: Ohne sie könnte ein neuer `E2E`-Test ohne `OsInterface` still in der blockierenden regulären Lane landen — exakt der Fehlermechanismus, den die 8 ungetaggten Playwright-Klassen heute zeigen. Der Meta-Test selbst trägt keinen Trait und läuft in der blockierenden Lane. |
| `Integration`-CI-Schritt | **Entfernen** in beiden Workflows (Schritt `Run integration tests` samt Erzeugung von `integration-test-results.trx`). | Verifizierter Befund: `Category=Integration` matcht 0 Tests — der Schritt ist heute ein No-op, es geht kein einziger Test verloren. Würde der Schritt stehen bleiben, droht genau die im Issue befürchtete Doppelausführung, sobald jemand `Integration`-Traits vergibt (ein solcher Test liefe dann in der regulären Lane **und** im Integration-Schritt). Die Entfernung wird im Commit, im PR und in `docs/help/stabilitaet/os-interface-tests.md` dokumentiert; wer künftig eine Integration-Lane will, muss sie bewusst neu einführen. |
| `E2E`-CI-Schritt im blockierenden Job | Ebenfalls **entfernen**; alle E2E-Tests laufen ausschließlich in der OS-Lane (`Category=E2E ⊆ Category=OsInterface`, durch Meta-Test abgesichert). | Ein `Category=E2E`-Schritt in `build & test` würde alle Browser-/Server-Tests weiterhin blockierend ausführen und die Lane-Trennung ad absurdum führen; zugleich Doppelausführung mit der OS-Lane. |
| Playwright-Installation | Der Schritt `playwright.ps1 install --with-deps chromium` wandert aus `build & test` in den `os-interface-tests`-Job. | Nach der Trait-Vergabe läuft in der regulären Lane kein Browser-Test mehr; die Installation (~1–3 min) verkürzt so die kritische blockierende Kette. Der OS-Job braucht den Schritt zwingend (Blaupause hatte keine Browser-Tests — hier Pflicht). |
| `release.yml` Release-Gate | Der bisher ungefilterte Vollsuite-Lauf bekommt `--filter "Category!=OsInterface"` mit Kommentar-Verweis, dass die OS-Lane bereits auf `staging` für denselben Baum gelaufen ist. | Das Gate ist laut eigenem Kommentar ein Race-Netz: PRs gegen `main` kommen nur von `staging`, wo die volle Suite inkl. OS-Lane bereits lief. Eine ungefilterte Suite würde Flakes release-blockierend machen — der wesentliche Grund für die Lane-Trennung. `Category!=OsInterface` genügt, weil `E2E ⊆ OsInterface` per Meta-Test erzwungen ist. |
| `Install Playwright browsers` in `release.yml` | **Entfernen** (Schritt Z. 83–85); am Gate-Schritt kurzer Kommentar, dass Browser-Tests ausschließlich in der OS-Lane laufen. | Mit dem Gate-Filter `Category!=OsInterface` läuft im Release-Gate kein Browser-Test mehr — der Schritt wäre tote Laufzeit (~1–3 min je Release). Ein Verstoß ist doppelt abgedeckt: `E2E` ohne `OsInterface` fängt `FlakyConventionTests` (untraited → läuft im Gate selbst mit), ein komplett ungetaggter Browser-Test scheitert bereits auf `staging` in der regulären Lane (Playwright-Install dort ebenfalls entfernt), bevor ein Release-Kandidat entsteht. Konsistent zur Entscheidung „Playwright-Installation" in den Staging-Workflows. |
| Kategorie-D-Einstufung (TestServer + SQLite-Dateibank, untraited) | Die 16 untraited Datei-DB-Klassen (`ClientLibrary/*`, `Pairing*`, `InternalMediaStreamControllerTests`, `ApiDocumentationContractTests_Runtime`) erhalten `OsInterface`. | Orchestrator-Vorgabe („Prozess-/Port-/Dateibank-Tests"), konsistent zu den E2E-getaggten Datei-DB-Klassen, die ohnehin in die OS-Lane wandern: gleiche Fixture-Technik (`PairingWebApplicationFactory`, Temp-`.db` unter `GetTempPath`, Lock-/Löschpfade) darf nicht je nach Trait-Zufall in zwei Lanes landen. **Vorab abgesicherte Rückfallregel:** Fällt die Coverage der neuen regulären Lane unter 70 %, bleiben genau diese untraited D-Klassen regulär — sie erfüllen das Blaupausen-Assertionskriterium (Assertion hängt am HTTP-/Service-Verhalten, OS-Kontakt ist nur Fixture-Plumbing, Cleanup ist dann bereits gehärtet) — und werden erneut gemessen. |
| Kategorie-E-Einstufung (reine Temp-Dateisystem-Tests) | **Regulär lassen**, aber Cleanup härten (`TempDirectoryCleanup`, siehe unten) und `GenreAdminTests`-Festpfad-Kollision beheben. | Assertionskriterium der Blaupause: Die Assertions hängen nicht am OS-Verhalten; das bisherige Flake-Risiko waren ausschließlich die ungeschützten `Directory.Delete`-Würfe im Dispose — die werden durch den Helfer eliminiert. Pauschales Taggen aller Temp-Nutzer würde die reguläre Lane unnötig ausdünnen und die Coverage-Datenbasis gefährden. |
| Kategorie-F-Einstufung (Zeitbudgets) | Keine Trait-Vergabe aufgrund von Zeitbudgets; stattdessen Deadline-Angleichung der vom Audit belegten Mismatches. | Ein knappes Budget ist ein Bug im Test, keine OS-Schnittstelle — das Angleichen macht den Test deterministisch. Ausnahme: Tests, die zusätzlich echte Prozesse/Ports nutzen, sind ohnehin über Kategorien B/C erfasst. |
| `TempDirectoryCleanup` | Blaupausen-Helfer 1:1 portieren, **plus** Geschwistermethode `DeleteFileBestEffort(path)` für einzelne Temp-Dateien. | Dieses Repo löscht in ~30 Fixtures einzelne Temp-`.db`-Dateien (`File.Delete(_dbPath)`) — ein Verzeichnis-Helfer allein deckt den dominierenden Fall nicht ab. |
| Retry-/Analyse-Skripte | Blaupausen-Skripte portieren (`scripts/lib/Trx.ps1`, `scripts/lib/TestDiscovery.ps1`, `Retry-FlakyTests.ps1`, `Analyze-FlakyTestResults.ps1`); Aufrufe mit `-Configuration Release`; ein Testprojekt statt zweier. | Struktur und Protokoll (`*-flaky-retry.trx`-Suffix, `::warning::`, Exit-0-Report) sind direkt übertragbar; Anpassungen nötig wegen Release-Builds (Blaupause: Debug) und einzelnem Testprojekt. |
| Quarantäne-Schwellwerte | Blaupausen-Defaults unverändert: Auswertefenster `-MaxRuns 20`, Quarantäne-Kandidat ab ≥ 2 Flake-Ereignissen ODER Fehlrate ≥ 20 % bei ≥ 5 Ausführungen, Beobachtungsstufe ab ≥ 1 Flake bzw. ≥ 10 % Fehlrate. Report-only (`::warning::`); Quarantäne-Issues werden manuell nach dokumentierter Konvention angelegt. | Orchestrator-Vorgabe; bewusst kalibrierbar als Skript-Parameter, keine harte Kopplung an den Workflow. Kein Auto-Issue — vermeidet Rauschen ohne Einordnung. |
| Quarantäne-/Re-Entry-Regel | **Eintritt:** Schlägt die Statistik an (Schwellwerte s. Zeile darüber), ist manuell ein Quarantäne-Issue anzulegen (dokumentierte Pflicht in der Hilfe-Doku) und der Test erhält `[Flaky]` — er läuft weiter in der OS-Lane, ist aber retry-berechtigt. Das Issue dokumentiert Flake-Grund, erste Beobachtung und Statistik-Verweis. **Re-Entry-Kriterium:** `[Flaky]` wird entfernt, wenn (a) der dokumentierte Flake-Grund behoben ist (Fix-Commit im Issue referenziert — oder das Issue begründet, dass die Markierung von Anfang an zu streng war) **und** (b) der Test seither **10 aufeinanderfolgende Primär-Durchläufe der OS-Lane**, in denen er enthalten war, ohne Fehlschlag gelaufen ist. Ein `recovered`-Retry-Ergebnis setzt die Zählung zurück; `NotExecuted`/nicht-enthaltene Läufe unterbrechen sie nicht (gleiche Konvention wie in der Statistik). `Analyze-FlakyTestResults.ps1` unterstützt die Auswertung: ein `Flaky`-markierter Test, der das Stabilitätskriterium erfüllt, erscheint im Report als **Re-Entry-Kandidat** (`::notice::`, symmetrisch zur `::warning::`-Quarantäne-Empfehlung). **Aktion:** `[Flaky]` per normalem PR entfernen, Quarantäne-Issue mit Statistik-Verweis schließen; `OsInterface` bleibt bestehen. **Wirkung:** Der Test ist nicht mehr retry-berechtigt — ein Fehlschlag ist wieder ein reales Fehlsignal (`::error::` wie jeder OS-Fehlschlag) und zählt als frische Beobachtung in der Statistik; erneute Quarantäne bleibt möglich. **Frist/Verantwortlichkeit:** Quarantäne-Issues werden einem Maintainer zugewiesen, Ziel-Frist ist der nächste Release-Zyklus; verbleibt ein Test ohne Fix über zwei Release-Zyklen in Quarantäne, wird im Issue entschieden: Fix priorisieren oder Test einschränken/entfernen. | Schließt die Anforderung „ohne Quarantäne-/Re-Entry-Regel gilt die Lane-Trennung nicht als abgeschlossen": `[Flaky]` darf keine stille Dauer-Entschuldigung werden. 10 Läufe = halbes Beobachtungsfenster (`MaxRuns 20`) — lang genug, um intermittierende Flakes zu entlarven, kurz genug für zeitnahen Re-Entry. `OsInterface` wird bewusst **nicht** entfernt: Re-Entry bedeutet Rückkehr zur ungetrübten Fehlschlags-Betrachtung (kein Retry mehr), nicht zur blockierenden Lane — die OS-Kontakt-Eigenschaft des Tests ändert sich nicht. |

## Programmabläufe

### CI-Lauf: reguläre Lane (blockierend, `build & test`)

1. `detect-backmerge` entscheidet wie bisher; `build-and-test` startet mit `needs: detect-backmerge`.
2. Schritte: Checkout → Setup .NET 10 → `dotnet restore` → `dotnet build … -c Release -p:NoWarn=NU1903` → **`dotnet test --filter "Category!=E2E&Category!=OsInterface"`** mit Coverage-Collector und TRX `test-results.trx` (Schritt umbenannt in „Run regular tests"; Playwright-Install entfällt hier).
3. ReportGenerator + 70-%-Schwelle unverändert; Uploads `coverage-report-*` und `test-results-*` (`if: always()`).
4. Die Schritte `Run integration tests` und `Run E2E tests` entfallen ersatzlos (Begründung: siehe Designentscheidungen).
5. `version`/`prerelease` (`staging-ci.yml`) benötigen weiterhin `build-and-test` — **`os-interface-tests` steht in keiner `needs:`-Kette.**

Beteiligte Artefakte: `.github/workflows/staging-ci.yml`, `.github/workflows/pr-staging-ci.yml`.

### CI-Lauf: OS-Interface-Lane (best effort, neuer Job `os-interface-tests`)

1. `os-interface-tests`: `needs: detect-backmerge`, `if: is_backmerge != 'true'`, `continue-on-error: true`, `runs-on: ubuntu-latest`, `timeout-minutes: 30`.
2. Schritte: Checkout → Setup .NET 10 → `dotnet restore` → Build Testprojekt Release → **Playwright-Install** (`pwsh …/playwright.ps1 install --with-deps chromium`) → `dotnet test --filter "Category=OsInterface"` → TRX `test-results-os-interface.trx`.
3. `Retry flaky tests once` (`if: failure()`, `shell: pwsh`): `scripts/Retry-FlakyTests.ps1 -TestProject VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -PrimaryTrx VideoWebPlayer.Tests/TestResults/test-results-os-interface.trx -Configuration Release` — Exit 1 bei persistenten Fehlschlägen (Job bleibt durch `continue-on-error` nicht-blockierend).
4. `Analyze flaky test results` (`if: always()`, `shell: pwsh`): `scripts/Analyze-FlakyTestResults.ps1 -Path VideoWebPlayer.Tests/TestResults` — immer Exit 0, Report + `::warning::` für Quarantäne-Kandidaten.
5. Upload `test-results-os-interface-{staging|pr}` (`if: always()`) mit Glob `VideoWebPlayer.Tests/TestResults/test-results-os-interface*.trx` (nimmt `*-flaky-retry.trx` mit), 14 Tage Retention.

Beteiligte Artefakte: beide Staging-Workflows, `scripts/Retry-FlakyTests.ps1`, `scripts/Analyze-FlakyTestResults.ps1`, `scripts/lib/*`.

### Flaky-Re-Run (Skriptablauf `Retry-FlakyTests.ps1`)

1. Primär-TRX parsen (`Get-TrxTestResults`): fehlgeschlagene Testnamen einsammeln; Theory-Datenzeilen auf Methoden-FQN normalisieren.
2. Flaky-Menge via `dotnet test --no-build --list-tests --filter "Category=Flaky"` ermitteln (`Get-FlakyTestNames` aus `scripts/lib/TestDiscovery.ps1`).
3. Schnittmenge leer → „keine Flaky-Fehlschläge", Exit 0. Fehlendes/unauswertbares TRX gilt ebenfalls als „keine" (wichtig für `if: failure()` nach Build-Fehlschlag).
4. Sonst genau ein Re-Run: `dotnet test --no-build -c <Configuration> --filter "(Category=Flaky)&(FullyQualifiedName~…)"` → TRX `<primärname>-flaky-retry.trx`.
5. Auswertung: `recovered` (fail → pass) vs. `persistent` (fail → fail); Annotationen (`::notice::`/`::warning::`/`::error::`); Exit 1 bei persistenten Fehlern.

Beteiligte Artefakte: `scripts/Retry-FlakyTests.ps1`, `scripts/lib/Trx.ps1`, `scripts/lib/TestDiscovery.ps1`.

### Flake-Statistik (Skriptablauf `Analyze-FlakyTestResults.ps1`)

1. TRX-Dateien unter `-Path` einsammeln; Primär/Retry über das `-flaky-retry`-Suffix trennen; Läufe nach `<Times start>` sortieren.
2. Flake-Ereignis = Fehlschlag im Primär-TRX + grün im Retry desselben Laufs ODER grün im nächsten Primär-Lauf, der den Test enthält. `NotExecuted` zählt als fehlende Beobachtung (unterbricht keine Sequenz).
3. Klassifikation im Fenster `MaxRuns`: Quarantäne-Kandidat / beobachten / stabil gemäß den Schwellwerten.
4. Report in die Konsole; Quarantäne-Kandidaten zusätzlich als `::warning::` mit Hinweis auf `docs/help/stabilitaet/os-interface-tests.md`. Symmetrisch dazu meldet der Report `Flaky`-markierte Tests (Menge via `Get-FlakyTestNames` aus `scripts/lib/TestDiscovery.ps1`), die das Re-Entry-Kriterium erfüllen (kein Flake-Ereignis in den letzten 10 aufeinanderfolgenden Primär-Läufen, in denen der Test enthalten war), als `::notice::`-Re-Entry-Kandidaten — Verweis auf dieselbe Doku-Konvention (Trait entfernen, Issue schließen). Exit immer 0.

### Temp-Verzeichnis-/Prozess-Cleanup (`TempDirectoryCleanup.DeleteBestEffort`)

1. Optional registrierte Kindprozesse beenden: `process.Kill(entireProcessTree: true)` + `WaitForExit(10 s)`; Fehler (disposed, weg, Win32) toleriert.
2. Optional `normalizeAttributes`: Dateiattribute im Baum auf `Normal` setzen (relevant für `DenyReadAccess`/`SetUnixFileMode`-Szenarien).
3. `Directory.Delete(root, recursive: true)` mit bis zu 5 Versuchen und `Thread.Sleep(100*attempt)`-Backoff; nur `IOException`/`UnauthorizedAccessException` retriggern.
4. Letzter Restfehler → `Console.Error`, nie werfen — ein Cleanup-Fehler überdeckt nie den eigentlichen Assert.
5. `DeleteFileBestEffort(path)`: gleiches Best-effort-Protokoll für Einzeldateien (Temp-DBs, Session-Dateien).

Beteiligte Klassen: `TempDirectoryCleanup`, alle Dispose-/`DisposeAsync`-Pfade der betroffenen Fixtures (Liste unter „Änderungen an bestehenden Klassen").

### Trait-Vergabe und Lane-Partition

1. Klassenebene: `[OsInterface]` neben dem bestehenden `[Trait("Category","E2E")]` auf alle E2E-Testklassen; die 8 ungetaggten Playwright-Klassen erhalten `[Trait("Category","E2E")] + [OsInterface]` (Bug-Nachzug).
2. Nicht-E2E OS-Kandidaten erhalten `[OsInterface]` auf Klassenebene (bzw. Methodenebene bei `SegmenterProcessSupportTests` — nur `StartRealProcess_UnstartableBinary_*` startet real, der Rest nutzt `FakePumpProcess` deterministisch).
3. `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` erhält zusätzlich `[Flaky]` (Methodenebene).
4. `FlakyConventionTests` erzwingt die Matrix; `--list-tests` pro Filter belegt lokal die disjunkte Partition.

## Neue Klassen

| Klasse / Datei | Typ | Zweck |
|----------------|-----|-------|
| `VideoWebPlayer.Tests/Infrastructure/Testing/TestCategories.cs` | statische Klasse | Konstanten `OsInterface`, `Flaky` (Portierung Blaupause; `E2E` bleibt `[Trait]`-Literal-Bestand, kein Breaking Change nötig — Konstante `E2E` kann ergänzend aufgenommen werden) |
| `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs` | Attribut (`ITraitAttribute`, Method\|Class) | liefert `Category=OsInterface` |
| `VideoWebPlayer.Tests/Infrastructure/Testing/FlakyAttribute.cs` | Attribut (`ITraitAttribute`, Method\|Class) | liefert `Category=Flaky`; Doku-Kommentar: nur mit `OsInterface`, nur bei dokumentiertem Flake |
| `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceFactAttribute.cs` | Attribut (`FactAttribute` + `ITraitAttribute`) | Schreibweise für neue OS-Tests (CallerFilePath/LineNumber-Konstruktor wie Blaupause) |
| `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceTheoryAttribute.cs` | Attribut (`TheoryAttribute` + `ITraitAttribute`) | dto. für Theories |
| `VideoWebPlayer.Tests/Infrastructure/Testing/FlakyConventionTests.cs` | Testklasse | Meta-Test: `Flaky ⇒ OsInterface` und `E2E ⇒ OsInterface`; xUnit-`Assert` statt FluentAssertions |
| `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanup.cs` | interne statische Klasse | `DeleteBestEffort(root, processesToKill, normalizeAttributes)` + `DeleteFileBestEffort(path)`; Portierung Blaupause, Namespace `VideoWebPlayer.Tests.Helpers` |
| `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanupTests.cs` | Testklasse | Helfer-Tests (Details unter „Tests") |
| `scripts/lib/Trx.ps1` | PowerShell-Bibliothek | `Get-TrxDocument`, `New-TrxNamespaceManager`, `Get-TrxTestResults`, `Get-TrxRunStartTime` (1:1-Portierung) |
| `scripts/lib/TestDiscovery.ps1` | PowerShell-Bibliothek | `Get-FlakyTestNames` via `dotnet test --no-build --list-tests --filter "Category=Flaky"` |
| `scripts/Retry-FlakyTests.ps1` | PowerShell-Skript | einmaliger gefilterter Re-Run `Flaky`-Fehlschläge (Ablauf siehe oben) |
| `scripts/Analyze-FlakyTestResults.ps1` | PowerShell-Skript | TRX-Flake-Statistik + Quarantäne-Empfehlung (`::warning::`) + Re-Entry-Kandidaten-Meldung (`::notice::`), Exit 0 |
| `docs/help/stabilitaet/os-interface-tests.md` | Dokumentation | Klassifizierungskriterium, Tagging-Schreibweisen, lokale Befehle, Retry-/Statistik-Konvention, **Quarantäne-/Re-Entry-Regel** (Eintritt: Schwellwert-Empfehlung + manuelles Quarantäne-Issue + `[Flaky]`; Re-Entry: Fix-Referenz + 10 aufeinanderfolgende grüne Primär-Läufe → Trait entfernen, Issue schließen; Frist/Verantwortlichkeit s. Designentscheidung), `gh run rerun --job <id>`-Regel nur für `os-interface tests`, entfernter Integration-Schritt |

## Änderungen an bestehenden Klassen

### CI-Workflows (kein C# — YAML)

#### `.github/workflows/staging-ci.yml`

- `build-and-test`: Schritt `Install Playwright browsers` entfernen; `Run unit tests` → `Run regular tests` mit Filter `Category!=E2E&Category!=OsInterface`; Schritte `Run integration tests` und `Run E2E tests` entfernen; Rest unverändert.
- Neuer Job `os-interface-tests` wie unter „Programmabläufe" beschrieben (Artefaktname `test-results-os-interface-staging`).
- `version`: `needs:` unverändert — kein Bezug zum OS-Job.

#### `.github/workflows/pr-staging-ci.yml`

- Identischer Umbau wie `staging-ci.yml` (Job ist strukturell identisch); Artefaktname `test-results-os-interface-pr`. `no-pending-backmerge`/`back-merge-skip` unverändert.

#### `.github/workflows/release.yml`

- Schritt `Release gate - full test suite`: `--filter "Category!=OsInterface"` ergänzen; Kommentar erweitern: Gate läuft die deterministische Teilmenge, OS-Lane lief bereits auf `staging` (`test-results-os-interface-*`-Artefakte). TRX-Name `final-test-results.trx` bleibt.
- Schritt `Install Playwright browsers` (Z. 83–85) entfernen — mit dem Gate-Filter läuft kein Browser-Test mehr in diesem Workflow (Begründung s. Designentscheidung).

### Trait-Vergabe — `OsInterface` (Klassenebene, zusätzlich zu bestehendem `E2E`)

**Alle 53 `Category=E2E`-Dateien** erhalten `[OsInterface]` (Klassenebene; methodengetaggte E2E-Vorkommen erben den Klassen-Trait): die 34 Playwright-Dateien aus Audit-Kategorie A plus `AboutPageE2ETests`, `ContinueWatchingE2ETests`, `DevicePairingE2ETests`, `DevicePlaylistE2ETests_*` (4), `EpisodesBackgroundImageAccessTokenE2ETests`, `FirstUserRedirectE2ETests`, `IdentityRedirectManagerE2ETests`, `LocalMediaPlaylistE2ETests_*` (4), `LocalMediaSourcePipelineE2ETests`, `LocalMediaSourceStreamingE2ETests`, `LiveStreamE2ETests`, `MediaProbeHttpIntegrationTests`, `ProgramGuideE2ETests`, `Services/FfmpegLiveStreamSegmenterE2ETests`, `Services/…` — vollständige Liste = die 53 Dateien aus `grep -rln 'Trait("Category","E2E")'` (Nachweis: `inventory/os-interface-audit.md`, Kategorien A/B/D).

**8 ungetaggte Playwright-Dateien** erhalten `[Trait("Category","E2E")] + [OsInterface]` (Bug-Nachzug des fehlenden E2E-Traits): `LiveTvE2ETests`, `LiveTvClipBlocksE2ETests`, `TVSenderAdminE2ETests`, `TVSenderAdminE2ETests_AgeWindow`, `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminDayBarE2ETests`, `TVSenderAdminValidityE2ETests`.

**Untraited OS-Kandidaten → `[OsInterface]` (Klassenebene):**

- Kategorie C: `UdpDiscoveryListenerTests` (echte UDP-Loopback-Sockets).
- Kategorie B (untraited): `Services/LocalMediaSourceReaderTests` (Junction/Symlink via `mklink /J`/`CreateSymbolicLink`), `MediaSourceAdminDetailsValidationTests` (nutzt `TestHelpers.DenyReadAccess` → `icacls`/`SetUnixFileMode`-Kindprozesspfad), `Services/FfmpegToolDownloadServiceTests_Install`, `Services/FfmpegToolDownloadServiceTests_Extraction`, `Services/FfmpegToolDownloadServiceTests_Failures`, `Services/FfmpegToolDownloadServiceTests_Start` (echte Archivextraktion/chmod/Temp-Marker-Dateien mit OS-Semantik; `Run_OnUnix_SetsExecutableBit` asserted chmod-Verhalten — OS-Assertion).
- Kategorie D (untraited): `InternalMediaStreamControllerTests`, `ClientLibrary/InternalVideoWebPlayerClientTests_Impersonation`, `ClientLibrary/VideoWebPlayerClientTests_{DeviceRunEndToEnd,DeviceTokenHeader,Pairing,PlaylistSessionRenewal,ProgramGuide,Reauthorization,RenewalCoverage,Session,StaleUnauthorized}`, `PairingBootstrapEndpointsTests`, `PairingExchangeContractTests_{Flow,JsonContract,Validation}`, `ApiDocumentationContractTests_Runtime`.
- Kategorie B Methodenebene: `Services/SegmenterProcessSupportTests` — nur die `StartRealProcess_UnstartableBinary_*`-Methoden erhalten `[OsInterface]` (Rest der Klasse = `FakePumpProcess`, deterministisch regulär).

**`Flaky` (Methodenebene):** `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` (`TvSenderOnAirE2ETests.cs` — dokumentierter Timing-Flake über zwei Baselines, siehe `inventory/tests.md`).

**Explizit regulär (kein Trait):** Kategorie-H-Dateien (Fakes, In-Memory-SQLite, EF-InMemory, bUnit), reine Temp-Dateisystem-Tests der Kategorie E (Cleanup gehärtet, Assertions deterministisch), `SegmenterProcessSupportTests`-Rest, `ProgramScheduleWorkerTests`/`LiveStreamWorkerTests` (`ControllableTimeProvider`).

### Cleanup-Härtung — `TempDirectoryCleanup`-Umstellung

**Ungeschützte `Directory.Delete` → `DeleteBestEffort` (15 Stellen):** `Controllers/LiveStreamControllerTests.cs:65` · `Extensions/LocalConfigurationTests.cs:128` · `LiveStreamCoordinatorTests.cs:128` · `LiveStreamWorkerTests.cs:116` · `Services/AutoUpdateProtectedFilesTests.cs:167` · `Services/FfmpegLocatorTests.cs:19` · `Services/LiveStreamMediaResolverTests.cs:65` · `Services/LiveStreamSegmentStoreTests.cs:49` · `Services/LiveStreamStandbyProviderTests.cs:37` · `Services/LiveStreamStandbySegmenterTests.cs:131` · `Services/MsToolsUpdaterIntegrationTests.cs:113` · `Services/StaticAssetVersionerTests.cs:17` · `Services/UpdateBackupCoordinatorTests.cs:27` · `Services/UpdateBackupEventBinderTests.cs:27` · `Services/VideoWebPlayerUpdateBackupServiceTests.cs:117`.

**Ungeschütztes `File.Delete` → `DeleteFileBestEffort`:** `Helpers/BackupUploadSessionServiceTestSupport.cs:34` (`DisposeSessionAsync`).

**Vereinheitlichung geschützter Deletes (~50 Stellen):** mechanische Umstellung der drei Catch-Varianten (blanker `catch`, gefilterter `IOException`/`UnauthorizedAccessException`/`DirectoryNotFoundException`-Catch, `Directory.Exists`-Vorprüfung) auf `DeleteBestEffort`/`DeleteFileBestEffort` — u. a. `PlaylistsE2ETestBase` (Z. 49, 120), `MediaBoxContextMenuE2ETestBase`, `LocalMediaPlaylistE2ETestBase` (Z. 89–90), `DeviceClientTestBase`, `PairingExchangeContractTestBase`, `MediaProbeHttpIntegrationTests` (Z. 76–77), `FfmpegLiveStreamSegmenterTests_FirstOutput` (`_tempDirs`), `FfmpegLiveStreamSegmenterE2ETests`, `Services/Backups/VideoWebPlayerBackupFacadeTests` (Z. 152 — bereits `try/catch` + `Directory.Exists`-Vorprüfung, vom Plan-Check als geschützt verifiziert; gehört in diese Gruppe, nicht zu den ungeschützten Stellen), `BackupUploadSessionServiceTestSupport.TempDirectory` (Z. 59–70 — Wrapper delegiert dann an den Helfer statt eigener Logik; keine zweite TempDirectory-Klasse einführen). `normalizeAttributes: true` überall dort, wo `DenyReadAccess`/`SetUnixFileMode` im Spiel ist (`MediaSourceLocalDirectoryE2ETests`, `MediaSourceAdminDetailsValidationTests`).

**Prozess-Kill-Lücke:** `LiveStreamE2ETests.cs:223` (eigener `Process.Start` für Standby-ffmpeg) und `MediaProbeHttpIntegrationTests.cs:143` (ffmpeg-Videogenerator) registrieren ihre `Process`-Instanzen und beenden sie im `finally`/`DisposeAsync` via `Kill(entireProcessTree: true)` (ggf. über den `processesToKill`-Parameter des Helfers, wenn ein Temp-Verzeichnis im selben Cleanup ansteht). Kurzlebige Prozesse mit `WaitForExit(10_000)` (`RunIcacls`, `TryCreateJunction`) bleiben, aber der Rückgabewert wird ausgewertet: Timeout → `Kill(entireProcessTree: true)` + Befund in die Assert-Meldung.

**`Components/GenreAdminTests.cs:61`:** fester Pfad `Path.GetTempPath()/genre-admin-wwwroot` ohne Cleanup → auf GUID-Unterverzeichnis pro Testlauf umstellen + `DeleteBestEffort` im Dispose (Kollisionsfix, eigener Commit).

### Deadline-Angleichung (Host-Poll > Kind-Budget)

| Stelle | Ist | Soll |
|--------|-----|------|
| `LiveStreamE2ETests.cs:260–267` Standby-Poll | 120×500 ms = **60 s** < `TranscodeTimeout` 120 s | ≥ 150 s (z. B. 300×500 ms) — Host-Budget oberhalb des Kind-Budgets plus Headroom |
| `Services/FfmpegLiveStreamSegmenterTests_FirstOutput.cs:105–111` `WaitUntilAsync` | 200×10 ms = **2 s** (~4 Polls à 500 ms) | 10 s (Headroom für CI-Last; Kind-Pollintervall 500 ms) |
| `MediaSourceScanServiceTests` `WaitFor*` | 2–10 s (`waitTimeSec = 10`, Z. 293) | 30 s Obergrenze für scan-getriebene Waits (echter Scan + Klassifikation unter Last) |
| `Services/MediaSourceScanCoordinatorScopeTests.cs:89–91` Wake-Poll | 10-s-Deadline + 50-ms-Poll auf das gebündelte `IProgramScheduleSignal`-Wake nach Scan-Ende | **30 s**, an dieselbe Begründung gekoppelt wie die `WaitFor*`-Angleichung (scan-getriebener Wait: Thread-Pool-Dispatch nach `runTask`-Ende ist unter CI-Last anfällig, obwohl das Signal in-memory ist) |
| `MediaSourceScanCoordinatorScopeTests` `Task.Delay(100/400)`-Gnadenfristen (Z. 83, 92, 123) | feste Sleeps vor Negativ-Assertions („kein Wake vor Scan-Ende", „genau ein gebündeltes Wake", „läuft noch während Restore-Lease") | unverändert — keine Deadlines, sondern Absicherungen gegen Fehl-Positive; Verlängern würde die Suite nur bremsen, ohne Flake-Schutz zu bringen |
| `Services/MediaSourceScanCoordinatorScanTests`, `WaitUntilEnteredAsync(ct)` | direktes `await` mit Test-CT, kein eigenes Budget | unverändert — die Scans laufen gegen In-Memory-SQLite + Temp-Dateien; ein Hängen wäre ein echter Deadlock im Produkt, den kein kleineres Test-Budget als Flake tarnen sollte |
| `MediaProbeHttpIntegrationTests.cs:144` | `WaitForExitAsync()` unbegrenzt | `WaitAsync(TimeSpan.FromSeconds(60))` + Kill im Fehlerfall |
| `Services/FfmpegLiveStreamSegmenterE2ETests.cs` `GenerateVideoAsync`, `LiveStreamE2ETests.cs:233` | `WaitForExitAsync(ct)` — Budget nur implizit über Test-CT | explizites Budget 60–120 s pro Generator-Aufruf; bei Timeout Kill + Prozess-Diagnose (Phase/State/Error in die Meldung, Muster der vorhandenen `WaitFor*`-Log-Dumps beibehalten) |
| `SegmenterProcessSupportTests` Drain-Budgets 50 ms/5 s | bewusst knapp (testet Timeout-Verhalten selbst) | unverändert — kein Mismatch |
| Playwright-Timeouts (30 s `SetDefaultTimeout`, 60 s `DriveUntilStateAsync`) | vs. 30-min-Job | unverändert — Größenordnungen passen (Audit-Befund) |

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Category`-Trait `Flaky` | nur zusammen mit `Category=OsInterface` auf derselben Methode/Klasse (vererbt) | `FlakyConventionTests` schlägt fehl → blockierende Lane rot |
| `Category`-Trait `E2E` | immer zusammen mit `Category=OsInterface` (Lane-Matrix) | `FlakyConventionTests` schlägt fehl → blockierende Lane rot |
| `Category=Integration` | wird nicht eingeführt; der CI-Schritt wird entfernt | — (kein Testverlust: matcht heute 0 Tests) |
| Skript-Schwellwerte (`MaxRuns`, `QuarantineMin*`, `WatchMinFailureRate`) | positive Werte; Defaults per Blaupause | Skript-Parameter-Validierung (PowerShell-`param`-Typen) |

## Konfigurationsänderungen

Keine Anwendungs-/Laufzeitkonfiguration. Das Verhalten liegt versionskontrolliert in den Workflow-Dateien und Skript-Parametern:

| Eintrag | Ort | Wert | Zweck |
|---------|-----|------|-------|
| Filter reguläre Lane | beide Staging-Workflows | `Category!=E2E&Category!=OsInterface` | deterministische blockierende Lane + Coverage-Datenbasis |
| Filter OS-Lane | beide Staging-Workflows | `Category=OsInterface` | best-effort-Ausführung aller umgebungssensitiven Tests |
| Filter Release-Gate | `release.yml` | `Category!=OsInterface` | deterministische Teilmenge als Race-Netz |
| `continue-on-error` | OS-Job | `true` | Fehlschläge sichtbar, nicht blockierend |
| `timeout-minutes` | OS-Job | `30` | analog `build-and-test`/Blaupause |
| TRX-Namen | TestResults | `test-results.trx` (regulär), `test-results-os-interface.trx` + `test-results-os-interface-flaky-retry.trx` (OS-Lane), `final-test-results.trx` (Release); `integration-test-results.trx`/`e2e-test-results.trx` entfallen | stabile Basis für Artefakt-Glob und Analyse-Suffix-Konvention |
| Artefaktnamen | Uploads | `test-results-os-interface-staging` / `test-results-os-interface-pr` | getrennte OS-Artefakte für `gh run download` + Analyse |
| Skript-Defaults | `Analyze-FlakyTestResults.ps1` | `MaxRuns=20`, `QuarantineMinFlakeEvents=2`, `QuarantineMinFailureRate=0.2`, `QuarantineMinExecutions=5`, `WatchMinFailureRate=0.1`, `ReEntryMinConsecutivePasses=10` | Blaupausen-Schwellwerte + Re-Entry-Fenster (= halbes Beobachtungsfenster), kalibrierbar |

## Seiteneffekte und Risiken

- **Coverage-Datenbasis:** Das 70-%-Gate speist sich künftig aus `Category!=E2E&Category!=OsInterface` statt `Category!=E2E`. Das Herausfiltern der untraited D-Klassen (ClientLibrary-, Pairing-, Controller-Tests) entzieht dem Gate messbare Abdeckung. Absicherung: lokale Coverage-Messung der neuen regulären Lane **vor** dem Workflow-Umbau (Schritt 8 der Reihenfolge); Rückfallregel siehe Designentscheidung „Kategorie-D-Einstufung".
- **Lokale Lauf-Gewohnheiten:** `dotnet test --filter "Category=E2E"` bleibt gültig (liefert weiterhin exakt die Browser-/Server-Tests); wer bisher ungefiltert lokal lief, bekommt unverändert alles. Die CI-TRX-Namen `e2e-test-results.trx`/`integration-test-results.trx` entfallen — keine externen Verbraucher bekannt (Artefakt-Downloads erfolgen manuell).
- **OS-Job-Laufzeit:** Die komplette E2E-Suite plus OS-Tests läuft in einem Job (~6–10 min lokal gemessen + Playwright-Install); mit `timeout-minutes: 30` ausreichend, aber der Job verlängert die Gesamtlaufzeit des Runs nicht, da er parallel zu `build & test` läuft.
- **Stille Skip-Pfade:** `SkipBrowser`-/`E2EBinaryLocator`-`return`-Muster melden nicht gelaufene Tests weiterhin als `Passed` (Befund, kein Auftrag). In der CI sind Browser/ffmpeg vorhanden; der Punkt wird in der Hilfe-Doku als bekannte Einschränkung der Flake-Statistik dokumentiert.
- **`MediaProbeHttpIntegrationTests`:** trägt bereits `E2E` und wandert damit korrekt in die OS-Lane — der Klassenname („Integration") darf nicht zur Vergabe eines `Integration`-Traits verführen.
- **Retry-Ehrlichkeit:** `if: failure()` ist bewusst weit gefasst — bei Build-Fehlschlag meldet das Skript „keine Flaky-Fehlschläge" + Exit 0, der ursprüngliche Fehlschritt bleibt das führende Signal.
- **Workflow-Syntax:** YAML-Änderungen an drei Workflows; `if: always()`/`if: failure()`-Kombinationen und `needs:`-Ketten sind per Review und erstem PR-Lauf zu verifizieren.
- **GenreAdminTests-Fix:** verändert ein Test-Setup (fester Temp-Pfad → GUID); geringe Kollisionsgefahr mit parallelen lokalen Läufen entfällt dadurch.

## Umsetzungsreihenfolge

1. **Trait-Infrastruktur anlegen**
   - Voraussetzungen: `xunit.v3` 3.2.2 referenziert (vorhanden, `VideoWebPlayer.Tests.csproj:18`).
   - Beschreibung: `Infrastructure/Testing/` mit `TestCategories`, `OsInterfaceAttribute`, `FlakyAttribute`, `OsInterfaceFactAttribute`, `OsInterfaceTheoryAttribute` anlegen (Portierung der Blaupausen-Dateien, Namespace `VideoWebPlayer.Tests.Infrastructure.Testing`).

2. **`TempDirectoryCleanup` + Tests**
   - Voraussetzungen: keine (Schritt 1 nicht nötig).
   - Beschreibung: `Helpers/TempDirectoryCleanup.cs` (`DeleteBestEffort` + `DeleteFileBestEffort`) und `Helpers/TempDirectoryCleanupTests.cs` anlegen.

3. **Cleanup-Härtung umsetzen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: 15 ungeschützte `Directory.Delete` + 1 `File.Delete` umstellen; ~50 geschützte Deletes vereinheitlichen (inkl. `VideoWebPlayerBackupFacadeTests` — bereits geschützt, nur vereinheitlichen); Prozess-Registrierung + `Kill(entireProcessTree)` in `LiveStreamE2ETests`/`MediaProbeHttpIntegrationTests`; `WaitForExit`-Rückgabewerte auswerten; `GenreAdminTests`-Pfadfix (eigener Commit).

4. **Deadline-Angleichung**
   - Voraussetzungen: keine.
   - Beschreibung: die 6 in der Tabelle genannten Budget-Anpassungen (inkl. `MediaSourceScanCoordinatorScopeTests` Wake-Poll) + explizite Timeouts auf den unbegrenzten `WaitForExitAsync`-Stellen.

5. **Trait-Vergabe**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `[OsInterface]` auf die 53 E2E-Dateien; `E2E`+`OsInterface` auf die 8 ungetaggten Playwright-Dateien; `[OsInterface]` auf die untraited Kandidaten (B/C/D, Methodenebene bei `SegmenterProcessSupportTests`); `[Flaky]` auf `Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`. Audit-Ergebnisliste für den PR erzeugen.

6. **`FlakyConventionTests`**
   - Voraussetzungen: Schritte 1 und 5 (Test ist sonst nicht aussagekräftig).
   - Beschreibung: Meta-Test mit beiden Regeln auf xUnit-`Assert` umschreiben.

7. **Skripte portieren**
   - Voraussetzungen: Schritt 5 (Flaky-Discovery braucht vergebene Traits).
   - Beschreibung: `scripts/lib/Trx.ps1`, `scripts/lib/TestDiscovery.ps1`, `scripts/Retry-FlakyTests.ps1`, `scripts/Analyze-FlakyTestResults.ps1` anlegen; lokale Trockenläufe (`Retry` gegen vorhandenes TRX, `Analyze` gegen `TestResults/`).

8. **Lane-Verifikation lokal**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: `dotnet test --list-tests --filter "Category=OsInterface"` / `--filter "Category!=E2E&Category!=OsInterface"` — Partition disjunkt und vollständig (Summe = Gesamtzahl ungefiltert); reguläre Lane mit `--collect:"XPlat Code Coverage"` + ReportGenerator lokal messen → ≥ 70 % sicherstellen, sonst Rückfallregel (untraited D-Klassen regulär) anwenden und erneut messen; volle Suite pro Lane-Filter laufen lassen.

9. **Workflow-Umbau**
   - Voraussetzungen: Schritt 8.
   - Beschreibung: `staging-ci.yml` + `pr-staging-ci.yml` (regulärer Schritt umbenannt/umgefiltert, Integration- und E2E-Schritt entfernt, Playwright-Install verschoben, neuer Job `os-interface-tests`), `release.yml` (Gate-Filter + Kommentar, `Install Playwright browsers` entfernen).

10. **Dokumentation**
    - Voraussetzungen: Schritt 9.
    - Beschreibung: `docs/help/stabilitaet/os-interface-tests.md` neu (Klassifizierungskriterium, Tagging-Schreibweisen, lokale Befehle, Retry-/Statistik-/Quarantäne-Konvention inkl. manueller Issue-Pflicht **und Re-Entry-Regel** (Fix-Referenz + 10 aufeinanderfolgende grüne Primär-Läufe → `[Flaky]` entfernen, Issue schließen; Frist: nächster Release-Zyklus, Verbleib über zwei Zyklen → Entscheidung Fix-Priorisierung oder Entfernen), `gh run rerun --job`-Regel nur für den OS-Job, entfernter Integration-Schritt, bekannte Skip-Pfad-Einschränkung); `docs/STAGING_WORKFLOW.md` (Lane-/Job-Modell in Stage 1/2, Quality Gates); `docs/RELEASE_NOTES.md`; `README.md` prüfen/aktualisieren (Testabschnitt); Audit-Ergebnisliste in die PR-Beschreibung.

11. **CI-Verifikation**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: PR-Lauf beobachten — reguläre Lane grün und blockierend, OS-Job läuft parallel mit Artefakten; bei OS-Fehlschlag Sichtbarkeit prüfen (Job-Ergebnis, Annotation, TRX). `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; `dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error` grün (Pre-Commit-Hook: SecretScan + MarkdownLinkCheck + Format beachten). CI-Läufe bei Bedarf wiederholen (Referenz: drei Durchläufe); einmalige Playwright-Einzelflakes im OS-Job einzeln wiederholen und im Bericht benennen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `FlakyMarkedTests_AlwaysCarryOsInterface` | `FlakyConventionTests` | Reflection über Assembly: jede Methode/Klasse mit `Category=Flaky` liefert auch `Category=OsInterface` (Blaupausen-Logik, xUnit-`Assert.Empty` statt FluentAssertions) |
| `E2eMarkedTests_AlwaysCarryOsInterface` | `FlakyConventionTests` | gleiche Mechanik für `Category=E2E` — sichert die Lane-Matrix gegen neue ungetaggte OS-nahe E2E-Tests |
| `DeleteBestEffort_MissingDirectory_DoesNotThrow` | `TempDirectoryCleanupTests` | nicht vorhandenes Verzeichnis → keine Exception |
| `DeleteBestEffort_LockedDirectory_DoesNotThrow` | `TempDirectoryCleanupTests` | gelockte/offene Datei im Baum → Retries, Restfehler nur `Console.Error`, kein Wurf |
| `DeleteBestEffort_ReadOnlyFile_WithNormalize_Deletes` | `TempDirectoryCleanupTests` | Read-only-Datei + `normalizeAttributes: true` → Verzeichnis wird gelöscht |
| `DeleteBestEffort_KillsRegisteredProcessTree` | `TempDirectoryCleanupTests` | langlebiger registrierter Kindprozess wird beendet — plattformübergreifend: `cmd.exe /c ping -n 300 127.0.0.1` (Windows) bzw. `sleep 300` (Unix) per `OperatingSystem.IsWindows()`-Abzweigung (Blaupausen-Test ist Windows-spezifisch — hier anpassen) |
| `DeleteFileBestEffort_*` (2–3 Tests) | `TempDirectoryCleanupTests` | Einzeldatei: fehlende Datei tolerant, gelockte Datei wirft nicht, normale Datei wird gelöscht |
| `Get-FlakyTestNames` / TRX-Parsing / Re-Entry-Erkennung | — | Skript-Logik wird über lokale Trockenläufe verifiziert (kein Testprojekt für PowerShell vorhanden; Verifikation = Aufruf gegen reale TRX-Artefakte inkl. eines konstruierten Re-Entry-Falls: `Flaky`-markierter Test ohne Flake-Ereignis im Fenster → `::notice::`, dokumentiert im Bericht) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle 53 `Category=E2E`-Dateien | zusätzliches `[OsInterface]` auf Klassenebene — rein deklarativ, kein Verhaltenswechsel |
| 8 ungetaggte Playwright-Dateien (`LiveTvE2ETests`, `LiveTvClipBlocksE2ETests`, `TVSenderAdminE2ETests`, `_AgeWindow`, `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminDayBarE2ETests`, `TVSenderAdminValidityE2ETests`) | fehlenden `E2E`-Trait nachziehen + `[OsInterface]` — wechseln aus der bisherigen Unit-Lane in die OS-Lane |
| Untraited OS-Kandidaten (Liste oben: `UdpDiscoveryListenerTests`, `LocalMediaSourceReaderTests`, `MediaSourceAdminDetailsValidationTests`, `FfmpegToolDownloadServiceTests_*`, D-Kategorie, `SegmenterProcessSupportTests`-Methoden) | `[OsInterface]` — Wechsel in die OS-Lane |
| `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` | `[Flaky]` — wird retry-berechtigt |
| 15 Dateien mit ungeschütztem `Directory.Delete` + `BackupUploadSessionServiceTestSupport` | Umstellung auf `TempDirectoryCleanup` — Dispose wirft nicht mehr; Testverhalten im Erfolgsfall unverändert |
| ~50 Stellen geschützte Deletes | mechanische Vereinheitlichung auf den Helfer |
| `LiveStreamE2ETests`, `MediaProbeHttpIntegrationTests`, `FfmpegLiveStreamSegmenterE2ETests` | Prozess-Registrierung/Kill + explizite Warte-Budgets |
| `FfmpegLiveStreamSegmenterTests_FirstOutput`, `MediaSourceScanServiceTests`, `MediaSourceScanCoordinatorScopeTests` | angehobene Poll-Deadlines |
| `Components/GenreAdminTests` | eindeutiger Temp-Pfad + Cleanup |

### E2E-Tests (primärer Funktionsnachweis)

**Keine neuen E2E-Tests erforderlich.** Die Anforderung berührt keinen Benutzerfluss: Es werden weder UI noch anwendererreichbare Abläufe geändert — der gesamte Scope liegt auf Test-Infrastruktur (Traits, Cleanup-Helfer), CI-Workflows und PowerShell-Skripten. Der „sichtbare Effekt" ist CI-Verhalten (Job-Ergebnisse, Annotationen, Artefakte) und wird über die Akzeptanzszenarien sowie die Lane-Verifikation (`--list-tests`, Meta-Test, PR-Lauf-Beobachtung) nachgewiesen — nicht über UI-E2E. Die bestehenden E2E-Tests erhalten lediglich deklarative Trait-Ergänzungen; ihr Testinhalt bleibt unverändert. Eine „reale End-to-End-Abnahme" mit Screenshots ist folglich nicht anwendbar und entfällt — das wird hiermit explizit festgehalten.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| sämtliche bestehenden E2E-Testklassen | `[OsInterface]`-Ergänzung (deklarativ); inhaltlich unverändert — Wechsel in die best-effort-Lane |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: PR-CI mit Lane-Trennung | PR gegen `staging` | Trait-Infrastruktur und Workflows umgebaut; keine `OsInterface`-Infrastruktur vorhanden gewesen — alles neu erzeugt | `pr-staging-ci.yml` startet; `build & test` führt nur `Category!=E2E&Category!=OsInterface` aus; parallel `os-interface tests` mit `Category=OsInterface` | `build & test` grün mit Coverage-Bericht ≥ 70 %; `os-interface tests` läuft komplett durch; Artefakte `test-results-pr` und `test-results-os-interface-pr` vorhanden | PR-Lauf-Beobachtung + Artefakt-Download; lokale `--list-tests`-Partition |
| 2 | OS-Fehlschlag blockiert nicht | Push/PR mit einem fehlschlagenden, nicht `Flaky`-markierten OS-Test | reguläre Lane grün | `os-interface tests` schlägt fehl (`continue-on-error`) | Gesamt-Run bleibt „bestanden"-fähig: `version`/`prerelease` bzw. Merge-Gate hängen nicht am OS-Job; Fehlschlag als Job-Ergebnis + TRX-Artefakt sichtbar | Workflow-`needs:`-Review + beobachteter Lauf |
| 3 | Flaky-Re-Run recovered | OS-Lane mit Fehlschlag eines `Flaky`-Tests (Kandidat: `Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`) | Primär-TRX enthält `Failed` | `if: failure()`-Schritt ruft `Retry-FlakyTests.ps1`; Filter `(Category=Flaky)&(FQN~…)`; nur der Fehlschlag läuft erneut | `*-flaky-retry.trx` entsteht; bei Erfolg `::notice::`-Annotation „recovered"; kein Gesamt-Re-Run | Retry-TRX im Artefakt; Skript-Konsolenausgabe |
| 4 | Persistenter Flaky-Fehlschlag bleibt ehrlich | `Flaky`-Test schlägt auch im Re-Run fehl | wie 3 | Retry-TRX zeigt erneuten `Failed` | `::error::`-Annotation, Schritt Exit 1 — Job-Ergebnis rot, aber `continue-on-error` → nicht blockierend | Annotation + Job-Status |
| 5 | Flake-Statistik/Quarantäne | `if: always()`-Analyse-Schritt nach OS-Lauf | TRX-Dateien vorhanden | `Analyze-FlakyTestResults.ps1` wertet aus; Überschreitung der Schwellwerte (≥2 Flakes bzw. ≥20 % Fehlrate bei ≥5 Ausführungen im 20-Lauf-Fenster) | Report im Job-Log; Quarantäne-Kandidaten als `::warning::` mit Hinweis auf die dokumentierte manuelle Issue-Pflicht; Exit immer 0 | Job-Log + Hilfe-Doku-Konvention |
| 6 | Kein Flake-Fall / Build-Fehlschlag | OS-Schritt scheitert vor TRX-Erzeugung (Build-Fehler) | kein Primär-TRX | Retry-Schritt läuft wegen `if: failure()` trotzdem | „keine Flaky-Fehlschläge" + Exit 0; der Build-Fehlschritt bleibt das führende Signal | Job-Log |
| 7 | Release-Gate | Push auf `main` / Tag | Release-Pfad `create` | `release.yml` führt `dotnet test --filter "Category!=OsInterface"` aus | deterministische Teilmenge läuft blockierend; OS-Test-Ergebnisse stammen aus den `staging`-Artefakten | Workflow-Review + Release-Lauf-Log |
| 8 | Stille Regelverletzung | Entwickler vergibt `[Flaky]` ohne `[OsInterface]` oder `E2E` ohne `OsInterface` | neuer Test | reguläre Lane führt `FlakyConventionTests` aus | Meta-Test schlägt fehl → blockierende Lane rot, Verstoß benannt | Meta-Test (lokal: absichtlich Verstoß einbauen → Test muss fehlschlagen) |
| 9 | Cleanup-Fehler überdeckt Assert nie | Temp-Datei gelockt/Read-only im Dispose | Test läuft | `DeleteBestEffort`-Retries laufen durch; Restfehler → `Console.Error` | Testergebnis = Assert-Ergebnis, kein `AggregateException`-Overlay | `TempDirectoryCleanupTests` + Suite-Lauf |
| 10 | Re-Entry: `Flaky`-Test kehrt aus der Quarantäne zurück | Quarantänisierter Test (`[Flaky]` + Quarantäne-Issue), Flake-Grund gefixt | `[Flaky]`-Trait gesetzt, Fix-Commit im Issue referenziert | OS-Lane läuft ≥ 10 aufeinanderfolgende Primär-Durchläufe mit dem Test ohne Fehlschlag; `Analyze-FlakyTestResults.ps1` meldet ihn als Re-Entry-Kandidat (`::notice::`) | `[Flaky]` wird per PR entfernt, Issue mit Statistik-Verweis geschlossen; der Test ist wieder nicht retry-berechtigt — ein Fehlschlag ist ein reales Signal und speist die Statistik erneut (erneute Quarantäne möglich); `OsInterface` bleibt | dokumentierte Regel in `docs/help/stabilitaet/os-interface-tests.md` + Statistik-Report-Beobachtung; Negativnachweis: ein `recovered`-Retry-Lauf setzt die Zählung zurück |

## Offene Punkte

Keine.
