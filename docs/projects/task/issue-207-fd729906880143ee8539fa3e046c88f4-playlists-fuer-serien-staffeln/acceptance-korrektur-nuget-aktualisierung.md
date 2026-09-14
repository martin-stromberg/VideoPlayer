# Abnahmeprüfung – Korrektur NuGet-Aktualisierung

## Ergebnis

**Status:** Erfüllt

*(Ursprünglich „Abweichungen gefunden" – die unten dokumentierte kritische Abweichung wurde durch
Commit `81adb6b` behoben und in der „Nachprüfung nach Korrektur" ganz unten erneut unabhängig
verifiziert.)*

Geprüft wurde der Stand `4164d34` auf Branch
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-nuget-aktualisierung`
gegen den Basis-Branch `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln`,
unabhängig vom (unverifizierten) Bericht des implementierenden Agenten — durch Lesen des tatsächlichen
Diffs, eigene Build-/Restore-/Testläufe und Prüfung der referenzierten Bibliotheksdokumentation.

Die meisten Einzelpunkte des Berichts sind zutreffend und wurden bestätigt. Es wurde jedoch eine
schwerwiegende, vom Bericht nicht erwähnte Regression gefunden: **`SixLabors.ImageSharp 4.1.2` bricht
den Release-Build** und würde damit alle drei CI-Workflows zum Scheitern bringen.

### 1. Versionsangaben in den `.csproj`-Dateien

Diff gegen Basisbranch geprüft (`git diff …basis…HEAD -- '*.csproj'`) — alle Angaben aus dem Bericht
stimmen exakt:

- `Microsoft.Extensions.Logging.Abstractions`, `.Http`, `.Configuration.Abstractions`,
  `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`/`.Sqlite`,
  `Microsoft.AspNetCore.Authentication.JwtBearer`, `.Diagnostics.EntityFrameworkCore`,
  `.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`/`.Tools`: durchgängig
  `10.0.10 → 10.0.12`.
- `Microsoft.NET.Test.Sdk`: `18.8.1 → 18.10.0` (in `VideoWebPlayer.Tests.csproj` **und**
  `tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj`).
- `Microsoft.Playwright`: `1.49.0 → 1.62.0`.
- `bunit`: `1.35.3 → 2.11.3`.
- `SixLabors.ImageSharp`: `3.1.11 → 4.1.2`.
- `xunit.v3` (`3.2.2`) und `xunit.runner.visualstudio` (`3.1.5`) unverändert in beiden betroffenen
  Testprojekten — kein Rest einer Migration.
- `msTools.Updater` unverändert bei `0.10.4-rc.1`; `msTools.Backup` (`VideoWebPlayer.csproj`)
  unverändert bei `1.1.0-RC.4` — beide nicht Teil des Diffs.
- `tools/SecretScan/SecretScan.csproj` und `tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj`
  unverändert.

`git diff …basis…HEAD --stat` zeigt ausschließlich die 9 erwarteten Dateien (3 `.csproj`, 4
bunit-Testdateien, `EpisodeBackgroundImageGenerator.cs`, `MarkdownLinkCheck.Tests.csproj`) — keine
unbeteiligten Dateien angefasst.

### 2. NU1902 / AngleSharp

Bestätigt: `dotnet restore VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --force` liefert keine
NU1902-Warnung mehr. `dotnet list … package --vulnerable --include-transitive` meldet „liegen gemäß
den aktuellen Quellen keine anfälligen Pakete vor". `dotnet list … package --include-transitive`
zeigt AngleSharp jetzt bei **1.8.1** (vorher an der bekannt verwundbaren 1.1.2 hängend über bunit
1.35.3). Kein `NoWarn`-Eintrag in irgendeiner `.csproj`, der die Warnung stattdessen nur unterdrücken
würde — die Behebung ist echt, nicht kosmetisch.

### 3. bunit-Migration (4 Testdateien)

Inhaltlich geprüft, nicht nur „kompiliert". Gegen die offizielle bUnit-1-zu-2-Migrationsdokumentation
(`https://bunit.dev/docs/migrations/1to2.html`) abgeglichen:

- `TestContext → BunitContext`: reine Umbenennung, Verhalten identisch.
- `RenderComponent<T>(...) → Render<T>(...)`: mehrere `RenderXXX`-Methoden wurden zu einer
  vereinheitlicht, Funktionalität identisch.
- `AddTestAuthorization() → AddAuthorization()`: reine Umbenennung derselben Fake-Authentifizierung
  (registriert dieselben Services plus `CascadingAuthenticationState`); das nachfolgende
  `.SetAuthorized("test-user")` ist unverändert. Kein abgeschwächtes Testverhalten feststellbar.
- `DisposeComponents() → await DisposeComponentsAsync()`: laut Migrationsleitfaden bewusst asynchron
  gemacht, u. a. damit `IAsyncDisposable`-Komponenten korrekt entsorgt werden (eher eine Korrektur als
  ein Risiko). Einziger Aufrufort im gesamten Testprojekt ist
  `MediaSearchSelectorTests.cs:141` (`Dispose_CancelsPendingDebouncedSearch_WithoutThrowing`) — dort
  korrekt auf `await Record.ExceptionAsync(() => ctx.DisposeComponentsAsync())` umgestellt, nicht
  fire-and-forget. Kein weiterer `DisposeComponents`/`DisposeComponentsAsync`-Aufruf im Projekt, der
  übersehen worden wäre.

Alle vier Dateien enthalten ausschließlich diese vier mechanischen Ersetzungsmuster, keine
zusätzlichen, unbegründeten Änderungen an Testlogik oder Assertions.

### 4. ImageSharp-Änderung (`EpisodeBackgroundImageGenerator.cs`)

`Color.FromRgb(r, g, b) → Color.FromPixel(new Rgba32(r, g, b))`. Geprüft, ob `Color.FromRgb`
tatsächlich entfernt wurde: In der XML-Dokumentation des installierten Pakets
(`sixlabors.imagesharp/4.1.2/lib/net8.0/SixLabors.ImageSharp.xml`) existiert kein
`Color.FromRgb`-Member mehr, nur noch `Color.FromPixel<TPixel>` — die Ersetzung war also nicht
optional, sondern notwendig. Der dreistellige `Rgba32(byte, byte, byte)`-Konstruktor existiert
weiterhin (neben dem vierstelligen mit explizitem Alpha), konsistent mit dem seit jeher dokumentierten
Verhalten „Alpha implizit 255 (opak)".

Pixelidentität empirisch abgesichert statt nur angenommen: `EpisodeBackgroundImageGeneratorTests.cs`
enthält `Test_GetDominantColor_ReturnsCorrectColor`, der `dominant.ToPixel<Rgba32>()` (Ergebnis der
neuen `FromPixel`/`Rgba32`-Kette) vollständig — inklusive Alphakanal — gegen
`Color.Blue.ToPixel<Rgba32>()` vergleicht. Test lief im eigenen Testlauf grün
(`Test_GetDominantColor_ReturnsCorrectColor [7 ms]`), belegt also tatsächlich, nicht nur behauptet,
dass die Ersetzung pixelidentisch ist.

### 5. xunit-Entscheidung (Nicht-Migration auf v4)

Bestätigt: `xunit.v3` bleibt `3.2.2`, `xunit.runner.visualstudio` bleibt `3.1.5` in beiden betroffenen
Projekten, keine halb migrierten Reste (keine `Microsoft.Testing.Platform`-Pakete, keine
MTP-spezifischen `RunSettings`/Projekteigenschaften im Diff).

Die genannten drei Workflow-Dateien selbst eingesehen: `pr-staging-ci.yml` (Zeile 140/143/146),
`staging-ci.yml` (Zeile 121/124/127) und `release.yml` (Zeile 95) rufen `dotnet test` mit
`--collect:"XPlat Code Coverage"` bzw. `--logger "trx;LogFileName=…"` auf — beides VSTest-spezifische
Flags, die unter Microsoft.Testing.Platform (dem transitiven Bestandteil von xunit v4) nicht mehr in
dieser Form funktionieren. Die Begründung ist nachvollziehbar und durch den tatsächlichen
Workflow-Inhalt gedeckt, nicht nur behauptet.

### 6. Volle Testsuite

Eigener Lauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "console;verbosity=normal"`
(Debug-Konfiguration, kein `--filter`) lief **598 von 598 bestanden**, 0 Fehler, in 2,67 Minuten. Im
Log sind E2E-Tests mit Playwright klar erkennbar mitgelaufen (u. a. `PlaylistDetailE2ETests`,
`PlaylistPlaybackE2ETests`, `MediaBoxContextMenuInteractionE2ETests`, `PlaylistReorderE2ETests`,
`PlaylistsE2ETests` — Laufzeiten im Sekundenbereich pro Test, typisch für echte Browser-Interaktion).
Die `Category`-Filterung (`Category!=E2E` / `Category=Integration` / `Category=E2E`) existiert nur als
CI-Aufteilung in mehrere separate `dotnet test`-Aufrufe; ein ungefilterter Lauf wie hier deckt alle
Kategorien in einem Durchgang ab. Die im Bericht genannte Zahl 598/598 ist damit korrekt — **jedoch
nur für Debug-Konfiguration**, siehe Abweichung unten.

### 7. Unbeteiligte Dateien / msTools

`msTools.Backup` und `msTools.Updater` sind unverändert (siehe Punkt 1). Der Diff gegen den
Basisbranch enthält ausschließlich die 9 dem Auftrag zuzuordnenden Dateien; keine weiteren Änderungen
gefunden.

## Abweichungen

- **[BEHOBEN – siehe „Nachprüfung nach Korrektur" unten] Kritisch — `SixLabors.ImageSharp 4.1.2` bricht den Release-Build.** Ab Version 4.x führt
  ImageSharp bei jedem Nicht-Debug-Build eine Lizenzprüfung durch
  (`sixlabors.imagesharp/4.1.2/build/SixLabors.ImageSharp.targets`, Target
  `SixLabors_ValidateLicense`, `BeforeTargets="CoreCompile"`). Der Task
  `SixLabors.Licensing.ValidateLicenseTask` hat `ContinueOnError="$(Configuration.StartsWith('Debug'))"`
  — die Prüfung ist also nur in Debug-Konfiguration ein bloßer Hinweis, in **jeder anderen
  Konfiguration (insbesondere Release) ein harter Build-Fehler**, sofern kein `SixLaborsLicenseKey`
  bzw. keine `sixlabors.lic`-Datei vorhanden ist (hier nicht der Fall — Six Labors verlangt ab v4 eine
  kommerzielle Lizenz jenseits bestimmter Nutzungsbedingungen, siehe
  `https://sixlabors.com/pricing/`).

  Eigener, reproduzierter Befund:
  ```
  dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903
  …
  error : No Six Labors license found. Set $(SixLaborsLicenseKey), set $(SixLaborsLicenseFile), or add a 'sixlabors.lic' file to the project/workspace.
  error : Please obtain a license from https://sixlabors.com/pricing/
  Fehler beim Buildvorgang. 1 Fehler.
  ```
  Dieser exakte Befehl (Konfiguration, Projekt, Flags) ist wortgleich der Build-Schritt aus
  `pr-staging-ci.yml:132`, `staging-ci.yml:115` und `release.yml:81`. Alle drei CI-Workflows würden
  mit diesem Branch **fehlschlagen**, inklusive `release.yml` (Release-Pipeline). Der Bericht
  „Build ohne Fehler" trifft nur auf die (in CI nicht verwendete) Debug-Konfiguration zu — dort ist
  die Meldung wegen `ContinueOnError` nur eine Warnung, siehe eigener Befund unter „Hinweise". Diese
  Diskrepanz wurde im Bericht nicht erwähnt und dürfte übersehen worden sein, weil offenbar nicht in
  Release-Konfiguration bzw. nicht via `dotnet build`/`dotnet test -c Release` geprüft wurde.

  Damit ist genau die von der IT geforderte Bedingung „sofern diese keine Inkompatibilitäten
  mitbringen" für dieses eine Paket verletzt: Der Versionssprung auf 4.1.2 bringt eine funktionale
  Inkompatibilität mit dem bestehenden Release-/CI-Aufbau mit sich. Empfehlung: entweder auf der
  letzten Split-License-freien 3.x-Version bleiben (kein `4.x`) oder, falls 4.x gewünscht ist, vorab
  klären, ob eine kommerzielle Six-Labors-Lizenz vorliegt/beschafft wird und `SixLaborsLicenseKey`
  entsprechend in der Build-Umgebung hinterlegen.

## Hinweise

- Im eigenen Debug-Build (`dotnet build VideoPlayer.sln`, Standardkonfiguration) erscheint die
  Lizenzprüfung nur als **Warnung** (`No Six Labors license found …`), der Build selbst läuft
  fehlerfrei durch — das deckt sich mit dem Bericht, solange man Debug-Konfiguration verwendet, macht
  die oben beschriebene Release-Abweichung aber nicht sichtbar, wenn man nicht gezielt danach sucht.
- Alle übrigen sechs Prüfpunkte des Berichts (Patch-Updates, bunit-Migration inkl. NU1902-Fix,
  ImageSharp-Codeänderung selbst, xunit-Nichtmigration samt Begründung, Testergebnis 598/598,
  msTools/unbeteiligte Dateien) sind zutreffend und wurden unabhängig bestätigt.

## Nachprüfung nach Korrektur

Erneut unabhängig geprüft (eigener Agent, nicht der Implementierer, nicht der Orchestrator selbst)
nach Commit `81adb6b` („fix: Nachbesserung NuGet-Aktualisierung - SixLabors.ImageSharp auf 3.1.11
zurueckgesetzt"), Branchspitze zum Prüfzeitpunkt unverändert
`task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-nuget-aktualisierung`.

**Diff von `81adb6b` gegen `git show` gelesen:** Der Commit ändert ausschließlich zwei Zeilen in
`VideoWebPlayer/VideoWebPlayer.csproj` (`SixLabors.ImageSharp` `4.1.2 → 3.1.11`) und die dazugehörige
Codestelle in `EpisodeBackgroundImageGenerator.cs` (`Color.FromRgb(...)` statt
`Color.FromPixel(new Rgba32(...))`) – exakt die in der Commit-Beschreibung angekündigte,
minimalinvasive Rücksetzung. Keine weiteren Dateien betroffen; die übrigen Aktualisierungen dieser
Korrekturrunde (bunit 2.11.3, Patch-Updates, Playwright) sind im Diff nicht enthalten und damit
unangetastet geblieben. Im aktuellen Stand von `VideoWebPlayer.csproj` (Zeile 30) und
`EpisodeBackgroundImageGenerator.cs` (Zeilen 125–128) verifiziert – entspricht exakt dem Diff.

**Ursprünglich gemeldeter Fehlerbefehl selbst reproduziert – schlägt jetzt nicht mehr fehl:**
```
dotnet restore VideoPlayer.sln
dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903
…
149 Warnung(en)
0 Fehler
```
Keine Spur der zuvor aufgetretenen Meldung „No Six Labors license found" mehr.

**Vollständiger Solution-Build in beiden Konfigurationen grün:**
- `dotnet build VideoPlayer.sln -c Release` → „Der Buildvorgang wurde erfolgreich ausgeführt.", 0
  Fehler, 0 Warnungen.
- `dotnet build VideoPlayer.sln -c Debug` → ebenfalls 0 Fehler, 0 Warnungen.

**Volle Testsuite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug`
→ `Bestanden! : Fehler: 0, erfolgreich: 598, übersprungen: 0, gesamt: 598, Dauer: 2 m 41 s`. Deckt sich
exakt mit der in der vorherigen Runde und in der Commit-Beschreibung genannten Zahl.

**Übrige Aktualisierungen dieser Korrekturrunde nicht versehentlich zurückgesetzt:**
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`: `bunit` weiterhin `2.11.3`, `xunit.v3`
  weiterhin `3.2.2`, `xunit.runner.visualstudio` weiterhin `3.1.5` – unverändert gegenüber der
  vorherigen Abnahmerunde.
- `dotnet list VideoPlayer.sln package --vulnerable --include-transitive` meldet für alle sechs
  Projekte der Solution weiterhin „liegen gemäß den aktuellen Quellen keine anfälligen Pakete vor" –
  die NU1902/AngleSharp-Behebung aus Punkt 2 der ursprünglichen Prüfung ist also durch die Korrektur
  nicht wieder aufgehoben worden.

**`Color.FromRgb` in ImageSharp 3.1.11 – Existenz und Pixelidentität geprüft:** Der Release-Build
(siehe oben) kompiliert den Aufruf `Color.FromRgb(byte, byte, byte)` in
`EpisodeBackgroundImageGenerator.cs` Zeile 125 fehlerfrei, die Methode existiert in 3.1.11 also
tatsächlich (das ist zudem der ursprüngliche, vor der ersten Korrekturrunde bereits vorhandene Code,
keine Neuerung). `Test_GetDominantColor_ReturnsCorrectColor`
(`VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGeneratorTests.cs`,
Zeilen 31–43) vergleicht das Ergebnis von `GetDominantColor` (nutzt `Color.FromRgb`) über
`dominant.ToPixel<Rgba32>()` explizit gegen `Color.Blue.ToPixel<Rgba32>()` und lief im vollständigen
Testlauf grün – die Pixelgleichheit zur vorherigen `Color.FromPixel(new Rgba32(...))`-Variante ist
damit weiterhin durch einen echten Test abgesichert (beide Varianten setzen Alpha implizit auf 255,
`Color.FromRgb` ist in ImageSharp seit jeher ein reiner Komfort-Wrapper um denselben opaken
`Rgba32`-Konstruktor).

**Fazit:** Die einzige in der vorherigen Runde gefundene, kritische Abweichung (Release-Build-Bruch
durch die ImageSharp-Lizenzprüfung) ist durch Commit `81adb6b` behoben und eigenständig
nachverifiziert. Keine neuen Abweichungen gefunden, keine der zuvor bestätigten Korrekturen
versehentlich zurückgenommen. Status wird auf „Erfüllt" gesetzt.
