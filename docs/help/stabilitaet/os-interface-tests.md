# OS-Schnittstellen-Tests (Lane-Trennung, Flaky-Retry, Flake-Statistik)

Reguläre Tests sind deterministische Unit-, Service- und Komponententests ohne echte
Betriebssystem-Schnittstellen. Sie dürfen keine echten Browser-Sessions,
ffmpeg/ffprobe-Kindprozesse, gebundene Ports, UDP-Sockets, Junctions/Symlinks,
OS-Rechte-Änderungen (icacls/chmod) oder SQLite-Dateibanken benötigen.

OS-Schnittstellen-Tests prüfen genau diese Umgebungskontakte. Sie bleiben erhalten,
laufen aber in einer eigenen, nicht blockierenden CI-Lane und werden separat
ausgewertet.

## Klassifizierungskriterium

Maßgeblich ist, ob die **Assertion** eines Tests am OS-Verhalten des Beteiligten
hängt — nicht, ob irgendwo ein `Process.Start` vorkommt:

- Als OS-Schnittstelle gelten: Playwright/Chromium-E2E (inkl. aller
  `Category=E2E`-Tests), echte Prozessstarts (ffmpeg/ffprobe, icacls), gebundene
  Kestrel-/Loopback-Ports und UDP-Sockets, NTFS-Reparse-Punkte/Junctions und
  Symlinks, OS-Dateirechte (icacls/`SetUnixFileMode`/chmod), SQLite-Dateibanken
  unter `Path.GetTempPath()` (Lock-/Löschpfade mit OS-Semantik).
- Als Fixture-Plumbing gelten (bleiben regulär): reine Temp-Verzeichnis-Nutzung
  ohne OS-Assertion, In-Memory-Streams, Fakes (`FakePumpProcess`), bUnit ohne
  echten Browser. Das frühere Flake-Risiko dieser Tests waren ungeschützte
  `Directory.Delete`-Würfe im Dispose — die sind über `TempDirectoryCleanup`
  gehärtet (siehe unten).

## Kategorien und Tagging

`OsInterface` ist eine zweite Achse neben `E2E` — kein Ersatz. Gültige
Kombinationen:

- `[Trait("Category", "E2E")]` + `[OsInterface]` (Browser-E2E)
- `[OsInterface]` allein (Nicht-E2E mit OS-Kontakt)
- `[Flaky]` **nur in Kombination mit `OsInterface`** — nie in der regulären Lane.

Das Testprojekt `VideoWebPlayer.Tests` stellt zentrale Attribute bereit
(`Infrastructure/Testing/`); die Kategorie-Namen stehen als Konstanten in
`TestCategories` (`E2E`, `OsInterface`, `Flaky`):

```csharp
// Klassenebene (erbt auf alle Methoden)
[Trait("Category", "E2E")]
[OsInterface]
public sealed class MeineE2ETests : E2ETestBase { ... }

// Methodenebene bei gemischten Klassen (z. B. nur die echte Prozessstart-Methode)
[Fact]
[OsInterface]
public void StartRealProcess_...() { ... }

// Flaky-Markierung zusätzlich
[Fact]
[Flaky]
public async Task MeinFlakyTestAsync() { ... }

// Alternativ die komplettierten Fact/Theory-Attribute:
[OsInterfaceFact]
[OsInterfaceTheory]
```

Zwei Meta-Tests in `FlakyConventionTests` (untraited → laufen in der regulären,
blockierenden Lane) sichern die Konvention ab:

- `Flaky` ⇒ `OsInterface`
- `E2E` ⇒ `OsInterface`

Die `Integration`-Kategorie existiert nicht; der ehemalige CI-Schritt
`Run integration tests` (`Category=Integration`) matchte 0 Tests und wurde
entfernt. Wer künftig eine Integration-Lane will, muss sie bewusst neu einführen —
ein stehengelassener Schritt würde Tests sonst doppelt ausführen (reguläre Lane +
Integration-Schritt).

## Lokale Befehle

```powershell
# Nur reguläre Tests (blockierende Lane)
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category!=E2E&Category!=OsInterface"

# Nur OS-Schnittstellen-Tests (best-effort-Lane)
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=OsInterface"

# Nur Flaky-markierte Tests
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=Flaky"

# Übersichten ohne Ausführung
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --list-tests --filter "Category=OsInterface"
```

## Automatischer Retry (Flaky)

Der automatische Einmal-Retry greift ausschließlich für `Flaky`-markierte Tests
und existiert nur in der best-effort-Lane — die blockierende Lane bleibt
retry-frei.

**In der CI** (Job `os-interface tests` in `pr-staging-ci.yml` und
`staging-ci.yml`): Schlägt ein Schritt fehl, läuft `Retry flaky tests once`
(`if: failure()`) `scripts/Retry-FlakyTests.ps1`:

1. Primär-TRX parsen, fehlgeschlagene Testnamen einsammeln (Theory-Datenzeilen
   werden auf den Methoden-FQN normalisiert).
2. Flaky-Menge via `dotnet test --no-build --list-tests --filter "Category=Flaky"`
   ermitteln.
3. Schnittmenge leer → „keine Flaky-Fehlschläge", Exit 0 (kein Retry; bei
   Build-Fehlschlag/fehlendem TRX ebenso).
4. Sonst genau ein gefilterter Re-Run:
   `--filter "(Category=Flaky)&(FullyQualifiedName~...)"` → Retry-TRX
   `test-results-os-interface-flaky-retry.trx`.
5. Auswertung: recovered wird als `::notice::`, persistent als `::warning::`
   annotiert; persistente Fehlschläge lassen den Schritt mit Exit 1 fehlschlagen
   (der Job bleibt durch `continue-on-error` nicht-blockierend).

Beide TRX-Dateien wandern über das Artefakt-Glob
`test-results-os-interface*.trx` ins Upload-Artefakt — der Erstfehlschlag bleibt
sichtbar.

**Lokal**:

```powershell
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=OsInterface" --logger "trx;LogFileName=test-results-os-interface.trx"
./scripts/Retry-FlakyTests.ps1 -TestProject VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -PrimaryTrx VideoWebPlayer.Tests/TestResults/test-results-os-interface.trx -Configuration Release
```

## Flake-Statistik und Quarantäne

`scripts/Analyze-FlakyTestResults.ps1` wertet gesammelte TRX-Verzeichnisse aus:
Lauf-Gruppierung über die Namenskonvention `<name>.trx` (Primär) /
`<name>-flaky-retry.trx` (Retry desselben Laufs). Als Flake-Ereignis zählt
Fehlschlag-im-Primär + grün-im-Retry desselben Laufs oder Fehlschlag→grün in
aufeinanderfolgenden Primär-Läufen. Primär-Outcomes außerhalb Passed/Failed
(z. B. `NotExecuted` durch übersprungene Tests) gelten als fehlende Beobachtung:
Sie zählen weder als Ausführung noch als grüner Folgelauf und unterbrechen die
Fehlschlag→grün-Sequenz nicht.

```powershell
# Lokale Läufe
./scripts/Analyze-FlakyTestResults.ps1 -Path VideoWebPlayer.Tests/TestResults

# CI-Artefakte
gh run download <run-id> -n test-results-os-interface-pr -D artifacts/os-pr
./scripts/Analyze-FlakyTestResults.ps1 -Path artifacts/os-pr
```

Klassifikation pro Test (Fenster: letzte `-MaxRuns 20` Primär-Läufe):

- `Quarantaene-Kandidat`: ≥ 2 Flake-Ereignisse ODER Fehlrate ≥ 20 % bei ≥ 5
  Ausführungen → `::warning::` mit Verweis auf diese Doku.
- `beobachten`: ≥ 1 Flake-Ereignis ODER Fehlrate ≥ 10 %.
- `stabil`: sonst.

Die Schwellwerte sind Startwerte und kalibrierbar (`-MaxRuns`,
`-QuarantineMinFlakeEvents`, `-QuarantineMinFailureRate`,
`-QuarantineMinExecutions`, `-WatchMinFailureRate`,
`-ReEntryMinConsecutivePasses`). Für die Re-Entry-Auswertung ermittelt der
Analyzer die `Flaky`-markierten Tests per `--list-tests` über das Testprojekt
(`-TestProject`, Standard `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
relativ zum Repository-Stamm; `-Configuration`, Standard `Release`) — ist das
Projekt nicht erreichbar (z. B. Auswertung heruntergeladener Artefakte außerhalb
des Klon-Verzeichnisses), wird die Re-Entry-Auswertung übersprungen, der Rest
bleibt unverändert. Der Analyzer legt **keine
Issues automatisch an** — er meldet Kandidaten mit Empfehlung und endet immer mit
Exit 0. In der CI läuft er als `if: always()`-Schritt `Analyze flaky test
results` über dem TestResults-Verzeichnis des Laufs.

### Quarantäne-Eintritt (manuelle Pflicht)

Schlägt die Statistik an oder zeigt ein wiederholtes Fehlerbild:

1. **Quarantäne-Issue anlegen** (manuelle Pflicht, dokumentiert Flake-Grund,
   erste Beobachtung, Verweis auf den Analyzer-Report).
2. **`[Flaky]` auf den Test setzen** — er läuft weiter in der OS-Lane, ist aber
   retry-berechtigt.
3. Das Fehlerbild unter „Bekannte Fehlerbilder" in dieser Datei dokumentieren.

Verantwortlichkeit und Frist: Quarantäne-Issues werden einem Maintainer
zugewiesen; Ziel-Frist ist der nächste Release-Zyklus. Verbleibt ein Test ohne
Fix über zwei Release-Zyklen in Quarantäne, wird im Issue entschieden: Fix
priorisieren oder Test einschränken/entfernen. `[Flaky]` ist keine stille
Dauer-Entschuldigung.

### Re-Entry aus der Quarantäne

`[Flaky]` wird entfernt, wenn beide Bedingungen erfüllt sind:

1. Der dokumentierte Flake-Grund ist behoben (Fix-Commit im Quarantäne-Issue
   referenziert — oder das Issue begründet, dass die Markierung von Anfang an zu
   streng war), **und**
2. der Test ist seither **10 aufeinanderfolgende Primär-Durchläufe der OS-Lane**,
   in denen er enthalten war, ohne Fehlschlag gelaufen. Ein `recovered`-Retry
   setzt die Zählung zurück; `NotExecuted`/nicht-enthaltene Läufe unterbrechen
   sie nicht.

`Analyze-FlakyTestResults.ps1` unterstützt die Auswertung: ein `Flaky`-markierter
Test, der das Stabilitätskriterium erfüllt, erscheint als Re-Entry-Kandidat
(`::notice::`, Schwellwert `-ReEntryMinConsecutivePasses 10`).

Aktion beim Re-Entry: `[Flaky]` per normalem PR entfernen, Quarantäne-Issue mit
Statistik-Verweis schließen. `[OsInterface]` bleibt bestehen — die
OS-Kontakt-Eigenschaft ändert sich nicht, der Test ist nur nicht mehr
retry-berechtigt: ein Fehlschlag ist wieder ein reales Fehlsignal und speist die
Statistik erneut (erneute Quarantäne bleibt möglich).

## CI-Verhalten

Die Workflows `.github/workflows/pr-staging-ci.yml` und
`.github/workflows/staging-ci.yml` trennen die Suite in zwei Jobs:

- Job `build & test`: blockierend; `Run regular tests` läuft mit
  `--filter "Category!=E2E&Category!=OsInterface"` und liefert die Coverage für
  das 70-%-Gate. Keine Playwright-Installation.
- Job `os-interface tests`: best-effort (`continue-on-error: true`,
  `timeout-minutes: 30`, Ubuntu, Release-Build) mit Playwright-Installation,
  `dotnet test --filter "Category=OsInterface"`, anschließend
  `Retry flaky tests once` (`if: failure()`) und `Analyze flaky test results`
  (`if: always()`). Der Job steht in keiner `needs:`-Kette der blockierenden
  Jobs (`version`/`prerelease` hängen nicht an ihm).
- TRX-Artefakte: `test-results-{pr|staging}` (regulär) und
  `test-results-os-interface-{pr|staging}` (OS-Lane, inkl. `*-flaky-retry.trx`),
  je 14 Tage Retention.
- `release.yml` (Release-Gate): `--filter "Category!=OsInterface"` — die OS-Lane
  lief bereits auf `staging` für denselben Baum; die Playwright-Installation ist
  dort ebenfalls entfernt.

Reguläre Fehler gelten als Regressionsfehler. OS-Schnittstellen-Fehler werden
sichtbar gemacht (Job-Ergebnis, Annotationen, TRX-Artefakt), blockieren aber
weder Merge noch Release-Kette.

### Job-Rerun-Konvention

Schlägt der `os-interface tests`-Job ohne auswertbares TRX fehl (Runner-Abbruch,
Testhost-Crash — der Retry-Schritt findet dann keine Fehlschläge und endet
neutral), ist genau ein `gh run rerun --job <job-id>` nur für diesen Job
erlaubt — niemals `gh run rerun --failed` (das würde blockierende Jobs mit
wiederholen). Die numerische Job-ID kommt aus `gh run view <run-id> --json jobs`.
Bleibt der Fehler bestehen oder meldet der Analyzer Schwellwert-Überschreitung,
gilt die Quarantäne-Regel oben. Fehler in der regulären Lane werden niemals
wiederholt — sie gelten immer als Regression.

## Cleanup-Konvention (`TempDirectoryCleanup`)

Fixture-Dispose darf einen Assertion-Fehler nie überdecken. Für Temp-Verzeichnisse
und -Dateien gilt deshalb:

```csharp
// Verzeichnis (optional: registrierte Kindprozesse vorher beenden, Rechte
// normalisieren für DenyReadAccess-/chmod-Fälle)
TempDirectoryCleanup.DeleteBestEffort(dir, processesToKill: new[] { proc }, normalizeAttributes: true);

// Einzelne Temp-Datei (z. B. SQLite-Dateibank)
TempDirectoryCleanup.DeleteFileBestEffort(_dbPath);
```

Der Helfer beendet optional Prozessbäume (`Kill(entireProcessTree: true)` +
10 s Wartezeit), normalisiert Dateiattribute und versucht das Löschen bis zu
5× mit Backoff (`100 * attempt` ms, nur bei `IOException`/
`UnauthorizedAccessException`). Endgültige Fehlschläge werden nach
`Console.Error` gemeldet — geworfen wird nie. Reale Kindprozesse werden beim
Cleanup registriert (`processesToKill`), und `WaitForExit`-Timeouts beenden den
Prozess, statt ihn verwaist weiterlaufen zu lassen.

## Bekannte Fehlerbilder

- `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`
  (`[Flaky]`): Timeout beim Warten auf `#live-tv-video` — der
  Standby/Off-Air-Übergang trifft das Poll-Fenster unter Last zeitweise nicht.
- Browser-E2E-Timeouts unter CI-Last (Playwright-Locator > 30 s).
- Langsam startende Kindprozesse (ffmpeg unter CI-Last) können Host-Pollfenster
  überziehen — Host-Deadlines sind bewusst größer als die Kind-Budgets, damit ein
  echter Kind-Timeout zuerst im Kind-Exit-Code sichtbar wird.
- SQLite-Dateibanken unter `Path.GetTempPath()` können durch Locks kurzzeitig
  nicht löschbar sein (deshalb `TempDirectoryCleanup.DeleteFileBestEffort`).
- Testhost- oder Build-Artefakt-Rennen sind Infrastrukturfehler und werden
  separat von Assertion-Fehlern bewertet.

## Bekannte Einschränkung der Statistik

Übersprungene/nicht enthaltene Tests (`NotExecuted`, z. B. bei Discovery- oder
Vorbedingungs-Skips) sind für den Analyzer eine fehlende Beobachtung: Sie zählen
nicht als Ausführung und unterbrechen weder Flake- noch Re-Entry-Sequenzen. Ein
dauerhaft geskipper `Flaky`-Test kann dadurch nie das Re-Entry-Kriterium
erreichen — das ist gewollt (kein Re-Entry ohne echte Beobachtung), muss aber bei
der Auswertung langer Skip-Phasen im Blick bleiben.

## Audit-Heuristik

Wiederholbarer Suchbefehl für die Klassifikation ungetaggter Tests (Heuristik,
Ergebnis fachlich bewerten — Kriterium „Assertion hängt am OS-Verhalten"):

```bash
grep -rnE 'IPlaywright|Process\.Start|ffprobe|ffmpeg|UdpClient|GetTempPath.*\.db|SetUnixFileMode|icacls|CreateSymbolicLink|TestServer|WebApplicationFactory' VideoWebPlayer.Tests --include='*.cs'
```

Treffer ohne `OsInterface`-Markierung prüfen: Helper/Fixtures ohne eigene
Testmethoden, Fakes und reine Temp-Datei-Nutzung ohne OS-Assertion sind
Fehl-Positive und bleiben regulär.
