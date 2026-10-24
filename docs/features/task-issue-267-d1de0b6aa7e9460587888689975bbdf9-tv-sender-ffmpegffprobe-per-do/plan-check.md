# Plan-Gegenprüfung

Geprüft: `requirement.md`, `inventory.md` (+ Detaildokumente), `plan.md` (zweite Fassung),
`…-tasks.md` (Tasks 1–29) für Issue #267 — ffmpeg/ffprobe per Download-Button in den
TV-Sender-Einstellungen nachinstallieren. Zweite Prüfrunde; Referenz: `plan-check.1.md`
(4 Lücken: Traversal-Test, UI-Negativtest, AK-5-Test, E2E-Locator-Override).

## Ergebnis

**Status:** Plan vollständig

Alle vier Lücken aus `plan-check.1.md` sind geschlossen, ohne dass neue Lücken entstanden sind:

- Traversal-/Allowlist-Nachweis: Tasks 25 + Tests-Tabelle — `Run_ZipWithMaliciousEntries_…` /
  `Run_TarXzWithMaliciousEntries_…` mit `../evil`, `../ffmpeg.exe`, absoluten Pfaden,
  `subdir/evil.exe` neben legitimen `bin/`-Präfixen; Asserts auf flache Ablage, nichts
  außerhalb, keine Staging-Reste.
- UI-Negativtest: Task 27 — bUnit `DownloadFailed_ShowsErrorAlert_NoticeRemains` +
  `DownloadActive_DisablesButtons`; zusätzlich E2E `Admin_DownloadFails_ShowsErrorAlert`
  (Task 29), der die Fehleranzeige end-to-end über echten HTTP-Fehler belegt.
- AK-5-Test: Task 26 — `Run_ConfiguredPaths_RemainUntouched` (Marker-Dateien am Config-Pfad
  byte-identisch, `Resolve()` liefert weiterhin Config-Pfad, Schreibzugriffe nur unter
  `{baseDir}/tools/ffmpeg/`).
- E2E-Locator-Override: jetzt vollständig spezifiziert — **echte** Locators über internal
  Ctor (Temp-`baseDirectory`, `pathEnvironment = () => null`, frische `MediaProbeOptions`
  ohne `*Path`) → alle drei Kandidatenquellen neutralisiert; Download-Service auf dasselbe
  Temp-`baseDirectory` gebogen, sodass `Resolve()` nach `Succeeded` real greift (AK 3
  e2e-seitig). Verifiziert: internal Ctor existiert (`FfprobeLocator.cs:31`,
  `FfmpegLocator.cs:32`), `InternalsVisibleTo` im csproj, `TVSenderAdminE2ETests` erbt von
  `PlaylistsE2ETestBase` (`UseKestrel` + `UseUrls("http://127.0.0.1:0")` vorhanden).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|---|---|---|---|
| 1. „ffprobe fehlt"-Hinweis (Tab Laufzeitermittlung) + „ffmpeg fehlt / Live-Stream pausiert"-Hinweis (Einstellungen/Segmentierungs-Karte), je mit Download-Button direkt daneben | Locator-Injektionen + fehlt-Flags in `TVSenderAdmin.razor`; Warnzeile + Button im Tab „laufzeit" und in der Segmentierungs-`admin-sub-card` (§Programmabläufe, Schritt 6; Tasks 10–12) | bUnit `FfmpegMissing_ShowsNoticeAndDownloadButton`, `FfprobeMissing_ShowsNoticeAndDownloadButton`, `BinariesPresent_NoNotice`; E2E `Admin_DownloadsFfmpegTools_WhenMissing` (beide Tabs) | Abgedeckt |
| 2. Plattformpassendes Paket, Fortschritt (Prozent/Bytes), verständliche Fehler, Ablage `tools/ffmpeg/`, Executable-Bit unter Linux | `"FfmpegTools"`-HttpClient mit `ResponseHeadersRead`, gestreamt in Temp-Datei, `BytesReceived`/`TotalBytes`; zip via `System.IO.Compression`, tar.xz via SharpCompress; `*.tmp`-Staging („beide stagen, dann beide moven"); `File.SetUnixFileMode` (Schritte 1, 4–6; Tasks 5–8) | `Run_ZipArchive_InstallsBothBinariesToToolsFfmpeg`, `Run_TarXzArchive_InstallsBothBinaries`, `Run_OnUnix_SetsExecutableBit`, `Run_ReportsProgress`, `Run_HttpError_Fails`, `Run_CorruptArchive_Fails`, `Run_MissingBinaryInArchive_Fails`, `Run_UnsupportedPlatform_Fails`, `Run_TargetDirectoryNotWritable_Fails`; E2E Happy Path + `Admin_DownloadFails_ShowsErrorAlert` | Abgedeckt |
| 3. `Resolve()` findet Binaries ohne Neustart; Worker laufen im nächsten Takt an | Locators cachen nichts (Bestand); Ablage identisch mit `ConventionCandidates()`; UI fragt Locator-Flags nach `Succeeded` neu ab (Schritt 7) | `AfterInstall_LocatorResolveFindsBinaries`; E2E mit **echten** Locators belegt Wiederfinden; Worker-Anlauf ist Bestandsverhalten (`LiveStreamCoordinatorTests.SynchronizeAsync_FfmpegMissing_ReportsUnavailable` deckt den Umkehrzustand) | Abgedeckt |
| 4. GPL-/Lizenzhinweis vor dem Download | `admin-dialog-overlay`/`admin-dialog`-Bestätigungsdialog vor `StartDownloadAsync`; Text konsistent zu `docs/help/tv-sender.md` (Task 13) | `DownloadButton_OpensGplDialog`, `GplDialog_Confirm_StartsDownload` (inkl. User-ID-Assert); Dialog im E2E-Fluss | Abgedeckt |
| 5. Alternativpfade (`MediaProbe:*Path`, `tools/ffmpeg/`, PATH) unverändert; Config-Pfad wird nicht überschrieben | Verbindlich: Service schreibt ausschließlich nach `{baseDir}/tools/ffmpeg/`; Locators unverändert (Designtabelle „Zielablage") | `Run_ConfiguredPaths_RemainUntouched` (Task 26) + unveränderte `FfprobeLocatorTests`/`FfmpegLocatorTests` pinnen die Suchreihenfolge | Abgedeckt |
| 6. Admin-only, serverseitig erzwungen; parallele Downloads verhindert | `StartDownloadAsync(userId)` prüft `IsAdmin` im Singleton via `IServiceScopeFactory`+`ApplicationDbContext` (`UnauthorizedAccessException`/`ForbiddenAccessException`, Muster `RequireAdminAsync`); `IsActive`-Sperre; `isAdmin`-UI-Ausblendung nur zusätzlich (Schritt 2; Tasks 5, 14) | `StartDownloadAsync_NonAdmin_Throws`, `_AnonymousUser_Throws`, `StartDownloadAsync_WhileRunning_ReturnsNotStarted`, `DownloadActive_DisablesButtons`; bestehender E2E `NonAdmin_SeesNotAuthorized` | Abgedeckt |
| 7. Debug- + Release-Build fehlerfrei, volle Testsuite grün; Hilfe-Doku, README, Release Notes aktualisiert | Schritte 9–10; Tasks 22–24; SharpCompress-Lizenz-/Versions-/CI-Prüfung in Schritt 1/Task 1 | Definition-of-Done (`dotnet build` Debug+Release, `dotnet test` grün); Doku-Tasks für `videolaufzeit.md`, `tv-sender.md`, `README.md`, `RELEASE_NOTES.md` | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|---|---|---|
| Admin: fehlt-Hinweise in beiden Tabs → GPL-Dialog → Download mit Fortschritt → Buttons während `IsActive` deaktiviert → Erfolg → Hinweise aus dem DOM verschwunden (AK 1, 2, 3, 4) | Neuer `TVSenderAdminE2ETests.Admin_DownloadsFfmpegTools_WhenMissing` mit Kestrel-Loopback-Mini-Host (zip-Payload), `MediaProbe:DownloadSources:*`-Umleitung, echten Locators mit neutralisierten Quellen (Tasks 20, 21; explizite Asserts auf disabled-Buttons und DOM-Detach) | Abgedeckt |
| Download-Fehlerfall sichtbar in der UI (AK 2, „Fehler verständlich anzeigen") | Neuer `TVSenderAdminE2ETests.Admin_DownloadFails_ShowsErrorAlert` (Loopback liefert 404/kaputtes Archiv → Alert sichtbar, Hinweis bleibt) — Task 29 | Abgedeckt |
| Nicht-Admin: kein Zugriff auf Seite/Aktion (AK 6, UI-Seite) | Bestehender `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized`; serverseitige Dienst-Absicherung per Unit-Test | Abgedeckt |

## Hinweise

- **Atomarität jetzt präzise formuliert:** „beide Binaries komplett als `*.tmp` stagen, dann
  beide `File.Move(overwrite: true)`" steht explizit in Designtabelle, Programmablauf
  Schritt 6 und §Seiteneffekte samt begründetem Restrisiko — der Hinweis aus Prüfrunde 1 ist
  umgesetzt.
- **Loopback-Server-Technik korrigiert:** `HttpListener` wurde durch einen minimalen
  `WebApplication`/Kestrel-Mini-Host auf `http://127.0.0.1:0` ersetzt — exakt das im Projekt
  etablierte Muster (`PlaylistsE2ETestBase`, `WebApplicationFactory` + `UseKestrel()` +
  `UseUrls`), vermeidet http.sys-URL-Reservierungen unter Windows.
- **tar.xz-Ausführbedingung geklärt:** der tar.xz-Test läuft plattformunabhängig über den
  injizierbaren Plattform-Override (`linux-x64`), da SharpCompress rein managed ist; nur
  `SetUnixFileMode` bleibt OS-gebunden (`Run_OnUnix_SetsExecutableBit` skippbar auf Windows).
- **Kleinere, nicht lückenhafte Restpunkte (bewusst vermerkt, keine Pflicht):**
  - `TotalBytes == null` (fehlendes Content-Length → Byte- statt Prozentanzeige) ist im Plan
    beschrieben, hat aber keinen dedizierten Test; implizit über E2E/UI-Logik abgedeckt.
  - Ungültige/fehlende `DownloadSources`-URL für die aktuelle Plattform fällt unter den
    generischen `Failed`-Pfad (`Run_UnsupportedPlatform_Fails` deckt das benachbarte RID-
    Szenario); kein eigener Test benannt.
  - Temp-Aufräumen im **Erfolgs**pfad ist nur implizit belegt (Fehlerpfad via
    `Run_CorruptArchive_Fails`, Staging-Reste via Traversal-Tests).
- **Implementierungsrisiko, kein Vollständigkeitsmangel:** Task 20 setzt voraus, dass der
  `ConfigureServices`-Hook in `PlaylistsE2ETestBase` die Singleton-Registrierungen der
  Locators und des Download-Dienstes **ersetzen** kann (Registrierung muss nach den
  App-Registrierungen laufen bzw. `Replace`/`RemoveAll` nutzen) — beim Umsetzen auf die
  Registrierungsreihenfolge achten.
- **`ForbiddenAccessException` ist `internal`** (`Controllers/Exceptions/`) — im Service der
  eigenen Assembly nutzbar, in Tests dank `InternalsVisibleTo` erreichbar; passt zum
  `RequireAdminAsync`-Muster (`MediaDurationProbeService.cs:225-231`).
- Plan- und Tasks-Datei sind konsistent (Tasks 1–29 decken die Umsetzungsreihenfolge
  Schritte 1–10 vollständig ab); alle „Offenen Fragen" der Anforderung sind verbindlich
  entschieden.
