# Plan-Review (Iteration 2)

## Ergebnis

**Status:** Vollständig umgesetzt

Der implementierbare Umfang des Plans ist vollständig abgedeckt. Iteration 1 (`review.1.md`)
hatte bereits alle Planelemente als umgesetzt verifiziert; diese Runde prüfte zusätzlich die
beiden Delta-Commits `f9a4d5c` und `187f0ac` (Behebung der `review-code.1.md`-Befunde) auf
Plan-Konformität und Wirksamkeit. Verbleibend ist ausschließlich die per Definition erst
nach Push/PR-Erstellung mögliche Post-Push-Verifikation (eigener Abschnitt unten).

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `VideoWebPlayer.Tests/Infrastructure/Testing/TestCategories.cs` (statische Klasse) — angelegt, Konstanten `E2E`, `OsInterface`, `Flaky`
- [x] `Infrastructure/Testing/OsInterfaceAttribute.cs` (`ITraitAttribute`, Method|Class → `Category=OsInterface`) — angelegt
- [x] `Infrastructure/Testing/FlakyAttribute.cs` (`ITraitAttribute`, Method|Class → `Category=Flaky`, Doku-Kommentar „nur mit OsInterface, nur bei dokumentiertem Flake") — angelegt
- [x] `Infrastructure/Testing/OsInterfaceFactAttribute.cs` (`FactAttribute` + `ITraitAttribute`, CallerFilePath/LineNumber-Konstruktor) — angelegt
- [x] `Infrastructure/Testing/OsInterfaceTheoryAttribute.cs` (`TheoryAttribute` + `ITraitAttribute`, dto.) — angelegt; Delta `f9a4d5c` dokumentiert im XML-Kommentar, dass das Attribut bewusst ungenutzt und als planvorgeschriebene symmetrische Ergänzung für künftige Theory-Tests reserviert ist (plan-konforme Antwort auf den „Speculative Generality"-Befund — der Plan schreibt die Datei explizit vor)
- [x] `Infrastructure/Testing/FlakyConventionTests.cs` — angelegt: `FlakyMarkedTests_AlwaysCarryOsInterface` und `E2eMarkedTests_AlwaysCarryOsInterface` per Reflection über die Assembly, xUnit-`Assert` (kein FluentAssertions), Klasse selbst untraited → reguläre Lane
- [x] `Helpers/TempDirectoryCleanup.cs` (interne statische Klasse) — `DeleteBestEffort(root, processesToKill, normalizeAttributes)` + `DeleteFileBestEffort(path)`; `Kill(entireProcessTree: true)` + `WaitForExit(10 s)` mit tolerierten Fehlern; Attribut-Normalisierung; `Directory.Delete` mit max. 5 Versuchen und `100*attempt`-ms-Backoff, nur `IOException`/`UnauthorizedAccessException` retriggern; Restfehler → `Console.Error`, wirft nie
- [x] `Helpers/TempDirectoryCleanupTests.cs` — alle geplanten Fälle: MissingDirectory, LockedDirectory (kein Wurf), ReadOnly+Normalize löscht, KillsRegisteredProcessTree plattformübergreifend (`cmd ping`/`sleep` per `OperatingSystem.IsWindows()`), `DeleteFileBestEffort` (missing/locked/existing); Kill-Test trägt `[OsInterfaceFact]`
- [x] `scripts/lib/Trx.ps1` — `Get-TrxDocument`, `New-TrxNamespaceManager`, `Get-TrxTestResults`, `Get-TrxRunStartTime` (1:1-Portierung)
- [x] `scripts/lib/TestDiscovery.ps1` — `Get-FlakyTestNames` via `dotnet test --no-build --list-tests --filter "Category=Flaky"`; Delta `187f0ac` ergänzt `ConvertTo-MethodName` (vorher identisch in beiden Skripten dupliziert — Deduplizierung aus dem Code-Review, thematisch passend: normalisiert TRX-Namen auf die Methoden-FQNs aus `Get-FlakyTestNames`)
- [x] `scripts/Retry-FlakyTests.ps1` — kompletter geplanter Ablauf: Primär-TRX parsen, Theory-Zeilen auf Methoden-FQN normalisiert (jetzt via `ConvertTo-MethodName` aus der Lib), Flaky-Schnittmenge, fehlendes TRX → „keine Flaky-Fehlschläge" Exit 0, genau ein Re-Run `--filter "(Category=Flaky)&(FullyQualifiedName~…)"` → `<primär>-flaky-retry.trx`, recovered/persistent/indeterminate mit `::notice::`/`::warning::` (jetzt via `Write-Annotation` aus der Lib), Exit 1 bei persistenten Fehlern
- [x] `scripts/Analyze-FlakyTestResults.ps1` — TRX-Einsammlung, Primär/Retry-Trennung über `-flaky-retry`-Suffix, Sortierung über `<Times start>` (LastWriteTimeUtc-Fallback), Flake-Ereignis-Definition (fail→pass Retry ODER fail→pass Folge-Primär), `NotExecuted` als fehlende Beobachtung, Schwellwerte `MaxRuns=20`, `QuarantineMinFlakeEvents=2`, `QuarantineMinFailureRate=0.2`, `QuarantineMinExecutions=5`, `WatchMinFailureRate=0.1`, `ReEntryMinConsecutivePasses=10`; `::warning::` Quarantäne-Kandidaten, `::notice::` Re-Entry-Kandidaten (Flaky-Menge via `Get-FlakyTestNames`, `recovered`-Retry setzt Streak zurück), Exit immer 0; Delta `187f0ac` nutzt `Write-Annotation` statt inlinierter Varianten und korrigiert die Kommentare auf die realen Skip-Mechanismen (`[Fact(Skip = …)]`, `Assert.SkipWhen`, SkipBrowser-/E2EBinaryLocator-return statt der nicht existierenden `SOFTWARESCHMIEDE_SKIP_*`-Flags)
- [x] `scripts/lib/Annotations.ps1` — **neu im Delta `187f0ac`, über die Plan-Dateiliste hinaus**: kapselt die `::kind::`-Annotation-Syntax samt `GITHUB_ACTIONS`-Prüfung als gemeinsamen Helfer (Deduplizierungs-Befund des Code-Reviews); lokale Farbausgabe per `-ForegroundColor` erhalten
- [x] `docs/help/stabilitaet/os-interface-tests.md` — angelegt mit allen geforderten Inhalten: Klassifizierungskriterium (Assertion hängt am OS-Verhalten), Tagging-Schreibweisen, lokale Befehle, Retry-Konvention, Flake-Statistik, Quarantäne-Eintritt als manuelle Pflicht (Issue + `[Flaky]` + Maintainer-Zuweisung + Zwei-Zyklen-Frist), Re-Entry-Regel (Fix-Referenz + 10 aufeinanderfolgende grüne Primär-Läufe → Trait entfernen, Issue schließen), `gh run rerun --job <id>` nur für den OS-Job, entfernter Integration-Schritt dokumentiert, bekannte Skip-Pfad-Einschränkung der Statistik; Verlinkung aus `docs/help/index.md`

### CI-Workflows

- [x] `.github/workflows/staging-ci.yml` — `build-and-test`: Playwright-Install entfernt, `Run unit tests` → `Run regular tests` mit `--filter "Category!=E2E&Category!=OsInterface"`, Schritte `Run integration tests` + `Run E2E tests` entfernt (mit Kommentar-Verweis), Coverage-Gate 70 % unverändert; neuer Job `os-interface-tests` (`needs: detect-backmerge`, `if: is_backmerge != 'true'`, `continue-on-error: true`, `ubuntu-latest`, `timeout-minutes: 30`, Playwright-Install, `Category=OsInterface`-Lauf, Retry `if: failure()` pwsh, Analyse `if: always()` pwsh, Artefakt `test-results-os-interface-staging` mit Glob `test-results-os-interface*.trx`, 14 Tage); `version`-`needs:` unverändert (kein Bezug zum OS-Job)
- [x] `.github/workflows/pr-staging-ci.yml` — identischer Umbau, Artefakt `test-results-os-interface-pr`; `no-pending-backmerge` unverändert
- [x] `.github/workflows/release.yml` — Gate-Schritt `Release gate - deterministic test suite` mit `--filter "Category!=OsInterface"` + Kommentar-Verweis auf die staging-OS-Lane; `Install Playwright browsers` entfernt; TRX-Name `final-test-results.trx` unverändert

### Trait-Vergabe

- [x] Alle `Category=E2E`-Dateien tragen `[OsInterface]` auf Klassenebene — verifiziert: `grep -rln 'Trait("Category","E2E")'` → 61 Dateien (53 Bestand + 8 nachgetaggte), davon fehlt `OsInterface` in **keiner**
- [x] Die 8 ungetaggten Playwright-Dateien (`LiveTvE2ETests`, `LiveTvClipBlocksE2ETests`, `TVSenderAdminE2ETests`, `TVSenderAdminE2ETests_AgeWindow`, `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminDayBarE2ETests`, `TVSenderAdminValidityE2ETests`) tragen `[Trait("Category","E2E")] + [OsInterface]`
- [x] Untraited Kategorie C/B: `UdpDiscoveryListenerTests`, `Services/LocalMediaSourceReaderTests`, `MediaSourceAdminDetailsValidationTests`, `Services/FfmpegToolDownloadServiceTests_{Install,Extraction,Failures,Start}` — alle `[OsInterface]`
- [x] Untraited Kategorie D: `InternalMediaStreamControllerTests`, `InternalVideoWebPlayerClientTests_Impersonation`, `VideoWebPlayerClientTests_*` (9 Dateien), `PairingBootstrapEndpointsTests`, `PairingExchangeContractTests_{Flow,JsonContract,Validation}`, `ApiDocumentationContractTests_Runtime` — alle `[OsInterface]`
- [x] `SegmenterProcessSupportTests` — `[OsInterface]` auf Methodenebene an `StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull` (einzige `StartRealProcess_UnstartableBinary_*`-Methode); Klasse bleibt regulär
- [x] `[Flaky]` auf `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` (Methodenebene) — `--list-tests --filter "Category=Flaky"` liefert exakt diesen einen Test

### Cleanup-Härtung

- [x] Alle 15 ungeschützten `Directory.Delete`-Stellen aus dem Plan auf `TempDirectoryCleanup.DeleteBestEffort` umgestellt (`Controllers/LiveStreamControllerTests:64`, `Extensions/LocalConfigurationTests:129`, `LiveStreamCoordinatorTests:127`, `LiveStreamWorkerTests:115`, `Services/AutoUpdateProtectedFilesTests:168`, `Services/FfmpegLocatorTests:19`, `Services/LiveStreamMediaResolverTests:64`, `Services/LiveStreamSegmentStoreTests:48`, `Services/LiveStreamStandbyProviderTests:36`, `Services/LiveStreamStandbySegmenterTests:130`, `Services/MsToolsUpdaterIntegrationTests:114`, `Services/StaticAssetVersionerTests:18`, `Services/UpdateBackupCoordinatorTests:28`, `Services/UpdateBackupEventBinderTests:28`, `Services/VideoWebPlayerUpdateBackupServiceTests:117`)
- [x] `BackupUploadSessionServiceTestSupport.cs:33` — `File.Delete` → `DeleteFileBestEffort`; `TempDirectory`-Wrapper (Z. 60) delegiert an `DeleteBestEffort`
- [x] Geschützte Deletes vereinheitlicht — `PlaylistsE2ETestBase` (Z. 49, 120), `LocalMediaPlaylistE2ETestBase` (Z. 89–90), `MediaBoxContextMenuE2ETestBase` (Z. 40, 97), `DeviceClientTestBase`, `PairingExchangeContractTestBase`, `MediaProbeHttpIntegrationTests` (Z. 41, 79, 83), `FfmpegLiveStreamSegmenterTests_FirstOutput` (`_tempDirs`), `FfmpegLiveStreamSegmenterE2ETests` (6 Stellen), `Services/Backups/VideoWebPlayerBackupFacadeTests` (Z. 138, 143); `normalizeAttributes: true` bei `MediaSourceLocalDirectoryE2ETests:218` und `MediaSourceAdminDetailsValidationTests:21` (DenyReadAccess-Nutzer); repo-weit keine rohen `Directory.Delete`/`File.Delete`-Cleanup-Aufrufe mehr (verbleibende `File.Delete` in `LiveStreamControllerTests:156` ist bewusster Testgegenstand der Lock-Semantik)
- [x] Prozess-Kill-Lücke: `LiveStreamE2ETests` — Generator-`Process` im `finally` über `DeleteBestEffort(processesToKill: …)` + `Dispose`; `MediaProbeHttpIntegrationTests` — `_processesToKill`-Liste, Registrierung beim Start, Kill im `DisposeAsync` über den Helfer + `Dispose`. **Delta `f9a4d5c`:** `using` am ffmpeg-Generator-Prozess entfernt — das Objekt wurde vorher am Methodenende entsorgt, sodass das registrierte Kill-Netz nie greifen konnte (`ObjectDisposedException`, still geschluckt). Jetzt bleibt das `Process`-Objekt bis `DisposeAsync` gültig; `TempDirectoryCleanup` prüft `!process.HasExited` vor `Kill` (bereits beendete Prozesse toleriert) und die `DisposeAsync`-Schleife (Z. 84–87) entsorgt allein — das planmäßige Sicherheitsnetz ist jetzt tatsächlich wirksam.
- [x] `WaitForExit(10_000)`-Rückgabewerte ausgewertet: `RunIcacls` (`TestHelpers.cs:302–307`) und `TryCreateJunction` (`LocalMediaSourceReaderTests.cs:331–338`) — Timeout → `Kill(entireProcessTree: true)` + Befund in der Diagnose/Assert-Meldung
- [x] `Components/GenreAdminTests.cs:26,95` — fester Pfad durch `genre-admin-wwwroot-{Guid}` ersetzt + `DeleteBestEffort` im Dispose (eigener Commit `5e3d52c`)

### Deadline-Angleichung

- [x] `LiveStreamE2ETests.cs:279` Standby-Poll 60 s → 150 s (300×500 ms, Host > `TranscodeTimeout` 120 s + Headroom, kommentiert)
- [x] `FfmpegLiveStreamSegmenterTests_FirstOutput.cs:111` `WaitUntilAsync` 2 s → 10 s (1000×10 ms, kommentiert)
- [x] `MediaSourceScanServiceTests` — alle `WaitFor*`-Deadlines auf `TimeSpan.FromSeconds(30)` bzw. `waitTimeSec = 30` (Z. 83–85, 165–166, 252–255, 295, 355–380, 655)
- [x] `MediaSourceScanCoordinatorScopeTests.cs:91` Wake-Poll-Deadline 10 s → 30 s (kommentiert); `Task.Delay(100/400)`-Gnadenfristen unverändert (Z. 83, 94, 125)
- [x] `MediaProbeHttpIntegrationTests.cs:164` — unbegrenztes `WaitForExitAsync` → `WaitAsync(60 s)` + Kill + `Assert.Fail` mit stderr-Diagnose
- [x] `FfmpegLiveStreamSegmenterE2ETests.cs:467` (`GenerateVideoAsync`) und `LiveStreamE2ETests.cs:242` — explizites 120-s-Budget `WaitAsync(TimeSpan.FromSeconds(120))` + Kill + stderr-Diagnose
- [x] Unverändert wie geplant: `SegmenterProcessSupportTests`-Drain-Budgets, `MediaSourceScanCoordinatorScanTests` (direktes `await` mit Test-CT), Playwright-Timeouts

### Verifikation

Aus Iteration 1 übernommen (`review.1.md`, damals ausgeführt):

- [x] Lane-Partition disjunkt und vollständig (`--list-tests` gegen Release-Build): `Category=OsInterface` = 424, `Category!=E2E&Category!=OsInterface` = 2368, Summe 2792 = ungefilterte Gesamtzahl; `Category=E2E&Category!=OsInterface` = 0 (E2E ⊆ OsInterface)
- [x] Coverage der neuen regulären Lane gemessen: regulärer Lane-Lauf (2549 Tests, 0 Fehler) + `coverage.cobertura.xml` → **Line-Coverage 94,44 % ≥ 70 %** — Gate-Datenbasis ungefährdet, Rückfallregel für D-Klassen nicht nötig
- [x] Reguläre Lane vollständig grün: 2549/2549 bestanden, 0 Fehler
- [x] Skript-Trockenläufe: `Analyze-FlakyTestResults.ps1` gegen die Inventory-TRX-Dateien → Report + „1 Flaky-markierte(r) Test", Exit 0; `Retry-FlakyTests.ps1` gegen fehlendes TRX → „keine Flaky-Fehlschläge", Exit 0
- [x] `dotnet build VideoPlayer.sln` Debug **und** Release — jeweils 0 Fehler
- [x] `dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error` — grün
- [x] Scope eingehalten: Diff berührt ausschließlich `VideoWebPlayer.Tests/`, `.github/workflows/`, `scripts/`, `docs/`, `README.md` — kein Anwendungscode, kein Datenmodell, keine EF-Migration nötig

In dieser Iteration (gegen die Delta-Commits) erneut ausgeführt:

- [x] `dotnet build VideoWebPlayer.Tests.csproj -c Debug` — 0 Fehler (Delta `f9a4d5c` kompiliert)
- [x] PowerShell-Parser-Check aller 5 Skripte (`Retry-FlakyTests`, `Analyze-FlakyTestResults`, `lib/Trx`, `lib/TestDiscovery`, `lib/Annotations`) — keine Syntaxfehler
- [x] `Retry-FlakyTests.ps1` Trockenlauf gegen fehlendes TRX nach dem Lib-Refactoring — „Keine Flaky-Fehlschläge … fehlt oder ist unauswertbar", Exit 0 (Funktion unverändert)
- [x] Dot-Sourcing-Verkabelung geprüft: beide Skripte sourcen `lib/Trx.ps1`, `lib/TestDiscovery.ps1`, `lib/Annotations.ps1`; `ConvertTo-MethodName`/`Write-Annotation` werden aufgerufen und sind genau einmal in den Libs definiert; keine `SOFTWARESCHMIEDE_SKIP`-/`$inGitHubActions`-Reste im Skript-Code
- [x] Diff-Umfang der Delta-Commits: ausschließlich `VideoWebPlayer.Tests/` und `scripts/` — Scope eingehalten

## Post-Push-Verifikation

Ausstehend — per Definition erst nach Push/PR-Erstellung erfüllbar, daher nicht Teil des
implementierbaren Umfangs (Status oben spiegelt den Implementierungsstand):

- [ ] `CI-Verifikation im PR-Lauf` (Task 39) — PR-Lauf beobachten: reguläre Lane strikt grün, `os-interface tests` best effort mit Artefakten `test-results-{pr|staging}`/`test-results-os-interface-{pr|staging}`; bei OS-Fehlschlag Sichtbarkeit (Annotation/TRX) prüfen; Läufe bei Bedarf wiederholen.
- [ ] `Audit-Ergebnisliste in die PR-Beschreibung` (Teil von Task 37) — Liste der getaggten Dateien beim Anlegen des PRs einfügen.

## Hinweise

- Die beiden Delta-Commits beheben alle vier Befunde aus `review-code.1.md`: Kill-Netz wirksam (`f9a4d5c`), `OsInterfaceTheoryAttribute` als planvorgeschriebene Reserve dokumentiert (`f9a4d5c`), `ConvertTo-MethodName`/`Write-Annotation` dedupliziert (`187f0ac`), Kommentare auf reale Skip-Mechanismen korrigiert (`187f0ac`).
- `scripts/lib/Annotations.ps1` ist eine additive Datei außerhalb der Plan-Dateiliste — begründete Deduplizierung aus dem Code-Review, keine Plan-Lücke (alle geplanten Skript-Funktionen bleiben erhalten).
- Die `Flaky`-Markierung ist der einzige retry-berechtigte Test (`Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`); der dokumentierte Timing-Flake wurde wie geplant markiert statt gefixt (Scope-Ausschluss 5).
- Der entfernte `Run integration tests`-Schritt matchte 0 Tests — die Entfernung ist in beiden Workflow-Kommentaren und der Hilfe-Doku begründet.
- `OsInterfaceFact`/`OsInterfaceTheory` sind planvorgeschriebene Schreibweise-Helfer; `OsInterfaceFact` wird produktiv genutzt (`TempDirectoryCleanupTests.DeleteBestEffort_KillsRegisteredProcessTree`), `OsInterfaceTheory` ist dokumentiert reserviert.
- Die Inventar-TRX-Dateien unter `inventory/test-results/` sind Baseline-Artefakte der Bestandsaufnahme (Commit `0aa5842`), keine Post-Umbau-Läufe.
