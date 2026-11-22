# Code-Review

## Ergebnis

**Status:** Keine Befunde

Iteration 2: Alle vier Befunde aus `review-code.1.md` sind am realen Code verifiziert
behoben; die Delta-Commits `f9a4d5c` und `187f0ac` (`git diff HEAD~2..HEAD`) enthalten
keine neuen Befunde.

Nachprüfung der einzelnen Befunde:

1. **`MediaProbeHttpIntegrationTests.cs` — Kill-Sicherheitsnetz wirksam:** `using` ist
   entfernt (Z. 154–159). Das `Process`-Objekt wird in `_processesToKill` registriert
   und bleibt bis `DisposeAsync` gültig: `DeleteBestEffort(_rootDir, processesToKill:
   _processesToKill)` (Z. 83) kann einen Abbruchpfad überlebenden ffmpeg-Generator nun
   tatsächlich killen (`TempDirectoryCleanup.cs` Z. 23–44: `HasExited`/`Kill` auf dem
   nicht-disposed Objekt, Fehler tolerant). Entsorgt wird genau einmal in der
   Dispose-Schleife (Z. 84–87) — kein Doppel-Dispose, kein Leak. Der erklärende
   Kommentar (Z. 154–157) beschreibt die Konstruktion korrekt.

2. **Gemeinsame Helfer ausgelagert:** `ConvertTo-MethodName` existiert nur noch einmal
   in `scripts/lib/TestDiscovery.ps1` (Z. 34–41); `Write-Annotation` liegt in der neuen
   `scripts/lib/Annotations.ps1` (Env-Prüfung `GITHUB_ACTIONS` jetzt in der Funktion,
   optionales `-ForegroundColor` nur für den lokalen Pfad). Dot-Sourcing ist in beiden
   Skripten korrekt (`Retry-FlakyTests.ps1` Z. 51–53, `Analyze-FlakyTestResults.ps1`
   Z. 102–104). Kein Rest-`$inGitHubActions`, keine Duplikat-Definitionen, keine
   vergessenen Aufrufstellen (alle `Write-Annotation`-Aufrufe auf benannte Parameter
   umgestellt; `scripts/` enthält außer den beiden Skripten nur `lib/` und
   `resolve-release-version.mjs`). PowerShell-Parser-Check aller fünf Dateien: fehlerfrei.
   Funktions-Smoke-Test verifiziert: lokaler Modus gibt `[kind] msg` aus (mit/ohne
   Farbe), GitHub-Actions-Modus `::kind::msg`, `ConvertTo-MethodName` normalisiert
   Theory-Datenzeilen korrekt und lässt Facts unverändert.

3. **Kommentare an Repo-Stand angepasst:** Die genannten Mechanismen existieren alle:
   `[Fact(Skip = "…")]` (`DevicePlaylistE2ETests_Playback.cs:78`), `Assert.SkipWhen`
   (`Services/FfmpegToolDownloadServiceTests_Install.cs:81`), `SkipBrowser`-return-Pfade
   (u. a. `PlaylistsE2ETestBase.cs`, `TVSenderAdminE2ETests.cs`) und
   `E2EBinaryLocator`-return-Pfade (`FfmpegLiveStreamSegmenterE2ETests.cs`,
   `LiveStreamE2ETests.cs`, `MediaProbeHttpIntegrationTests.cs`). Die neue Passage
   (Z. 17–24) benennt zusätzlich korrekt die bekannte Einschränkung, dass
   return-basierte Skips als `Passed` zählen. Die Inline-Kommentare an Z. 164–170 und
   Z. 187–190 sind ebenfalls aktualisiert; `SOFTWARESCHMIEDE_SKIP_*` kommt im Skript
   nicht mehr vor.

4. **`OsInterfaceTheoryAttribute` bewusst behalten:** `<remarks>` (Z. 7–10) dokumentiert
   die Designentscheidung „reserviert laut Plan" — `plan.md` Z. 114/115 und Z. 231
   schreiben das Attribut tatsächlich vor, die Hilfe-Doku
   (`docs/help/stabilitaet/os-interface-tests.md` Z. 57–58) nennt es. Konsistent
   umgesetzt.

Delta-Review der beiden Fix-Commits: keine Regressionen — `Retry-FlakyTests.ps1`
verhält sich funktional identisch (Annotation-Ausgabe nur über den neuen Helfer,
Exit-Codes unverändert), `Analyze-FlakyTestResults.ps1` behält die
Report-/Klassifikationslogik bei, und der C#-Diff beschränkt sich auf die
`using`-Entfernung plus Kommentar.

## Befunde

Keine.

## Geprüfte Dateien

Delta dieser Iteration (vollständig gelesen):
- `VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs`
- `VideoWebPlayer.Tests/Infrastructure/Testing/OsInterfaceTheoryAttribute.cs`
- `scripts/Retry-FlakyTests.ps1`
- `scripts/Analyze-FlakyTestResults.ps1`
- `scripts/lib/Annotations.ps1` (neu)
- `scripts/lib/TestDiscovery.ps1`

Gegenprüfung/Referenzen:
- `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanup.cs` (`DeleteBestEffort`,
  `processesToKill`-Kill-Pfad)
- `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` (`[Fact(Skip=…)]`-Beleg)
- `VideoWebPlayer.Tests/Services/FfmpegToolDownloadServiceTests_Install.cs`
  (`Assert.SkipWhen`-Beleg)
- `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`,
  `VideoWebPlayer.Tests/Helpers/E2EBinaryLocator.cs` (Skip-/return-Pfade)
- `docs/features/task/issue-368-…/plan.md`,
  `docs/help/stabilitaet/os-interface-tests.md` (Reservierungs-Vorgabe für
  `OsInterfaceTheoryAttribute`)
- Vollscan `scripts/` auf Rest-Verweise (`$inGitHubActions`, Annotation-Duplikate)
