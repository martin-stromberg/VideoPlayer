# Plan-Gegenprüfung

Geprüft: `requirement.md`, `inventory.md` (+ Detaildokumente), `plan.md`, `…-tasks.md`
für Issue #267 — ffmpeg/ffprobe per Download-Button in den TV-Sender-Einstellungen nachinstallieren.

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|---|---|---|---|
| 1. „ffprobe fehlt"-Hinweis (Tab Laufzeitermittlung) + „ffmpeg fehlt / Live-Stream pausiert"-Hinweis (Einstellungen), je mit Download-Button direkt daneben | Locator-Injektionen + fehlt-Flags in `TVSenderAdmin.razor`; Warnzeile + Button im Tab „laufzeit" und in der Segmentierungs-`admin-sub-card` (plan.md §Programmabläufe, Umsetzungsreihenfolge Schritt 6; Tasks 10–12) | bUnit `FfmpegMissing_ShowsNoticeAndDownloadButton`, `FfprobeMissing_ShowsNoticeAndDownloadButton`, `BinariesPresent_NoNotice` (Fake-Locators); E2E `Admin_DownloadsFfmpegTools_WhenMissing` | Abgedeckt |
| 2. Download lädt plattformpassendes statisches Paket, zeigt Fortschritt (Prozent/Bytes) und Fehler an, legt beide Binaries nach `tools/ffmpeg/`, setzt unter Linux das Ausführungsbit | `"FfmpegTools"`-HttpClient mit `ResponseHeadersRead`, gestreamt in Temp-Datei, `BytesReceived`/`TotalBytes` im Snapshot; zip via `System.IO.Compression`, tar.xz via SharpCompress; `*.tmp`-Staging + `File.Move`; `File.SetUnixFileMode` (Schritte 1, 4–6; Tasks 5–8) | `Run_ReportsProgress`, `Run_ZipArchive_InstallsBothBinariesToToolsFfmpeg`, `Run_TarXzArchive_InstallsBothBinaries`, `Run_OnUnix_SetsExecutableBit`, `Run_HttpError_Fails`, `Run_CorruptArchive_Fails`, `Run_MissingBinaryInArchive_Fails`, `Run_UnsupportedPlatform_Fails` | Abgedeckt |
| 3. `Resolve()` beider Locators findet Binaries ohne Neustart; Worker laufen im nächsten Takt an | Locators cachen nichts (Bestand), Ablage identisch mit `ConventionCandidates()`; UI blendet Hinweise nach `Succeeded` aus (Designentscheidung Zielablage; Schritt 7) | `AfterInstall_LocatorResolveFindsBinaries` (interner Ctor, selbes `baseDirectory`); E2E prüft Verschwinden der Hinweise; Worker-Anlauf ist Bestandsverhalten (`LiveStreamCoordinatorTests.SynchronizeAsync_FfmpegMissing_ReportsUnavailable` deckt den Umkehrzustand) | Abgedeckt |
| 4. GPL-/Lizenzhinweis vor dem Download | `admin-dialog-overlay`/`admin-dialog`-Bestätigungsdialog vor `StartDownloadAsync` (Designentscheidung GPL-Hinweis; Task 13) | `DownloadButton_OpensGplDialog`, `GplDialog_Confirm_StartsDownload` (bUnit); Dialog im E2E-Fluss enthalten | Abgedeckt |
| 5. Alternativpfade (`MediaProbe:*Path`, `tools/ffmpeg/`, PATH) bleiben funktional unverändert; gesetzter Config-Pfad wird nicht überschrieben | Verbindliche Designentscheidung: Service schreibt ausschließlich nach `{baseDir}/tools/ffmpeg/`; Locators werden nicht geändert (AK-5-Vermerk in der Designtabelle) | Implizit: unveränderte bestehende Locator-Tests (`FfprobeLocatorTests`/`FfmpegLocatorTests`, u. a. `Resolve_ConfiguredPath_Wins`) pinnen die Suchreihenfolge; `Run_ZipArchive_…ToToolsFfmpeg` belegt das feste Zielverzeichnis — **kein dedizierter Test**, dass ein gesetzter Config-Pfad bei einem Download unangetastet bleibt | Abgedeckt (schwach — siehe Hinweise) |
| 6. Download-Aktion nur für Administratoren, serverseitig erzwungen; parallele Downloads verhindert | `StartDownloadAsync(userId)` prüft `user.IsAdmin` im Singleton via `IServiceScopeFactory` + `ApplicationDbContext` (`UnauthorizedAccessException`/`ForbiddenAccessException`, Muster `RequireAdminAsync`); `IsActive`-Parallel-Sperre; `isAdmin`-UI-Ausblendung nur zusätzlich (Designentscheidung Admin-Erzwingung; Schritt 2; Tasks 5, 14) | `StartDownloadAsync_NonAdmin_Throws`, `StartDownloadAsync_AnonymousUser_Throws`, `StartDownloadAsync_WhileRunning_ReturnsNotStarted`; bestehender E2E `NonAdmin_SeesNotAuthorized` (UI-Seite) | Abgedeckt |
| 7. Debug- + Release-Build fehlerfrei, volle Testsuite grün; Hilfe-Doku, README, Release Notes aktualisiert | Umsetzungsreihenfolge Schritte 9–10; Tasks 22–24; SharpCompress-Lizenz-/Versions-/CI-Prüfung in Schritt 1 | Definition-of-Done-Schritt (`dotnet build` Debug+Release, `dotnet test` grün); Doku-Tasks für `videolaufzeit.md`, `tv-sender.md`, `README.md`, `RELEASE_NOTES.md` | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Kein Test für die Archiv-Validierungsregel „Pfad-Traversal/Einträge mit Verzeichnispräfix werden verworfen, nur Allowlist-Dateinamen werden extrahiert" (plan.md §Validierungsregeln). Gerade bei einem Download aus dem Netz ist das die sicherheitsrelevanteste Regel des Entpackens; die Tests-Tabelle enthält nur `Run_CorruptArchive_Fails` und `Run_MissingBinaryInArchive_Fails` — ein Test mit manipuliertem Archiv (Eintrag `../evil`, verschachtelter Pfad, unerwarteter Dateiname) fehlt.
- [ ] Kein Test für die UI-Fehleranzeige: Das Feature „Fehler als Alert-Text" (Snapshot `Failed` → verständliche Meldung in der Komponente) hat weder einen bUnit- noch einen E2E-Negativtest; der einzige geplante E2E deckt ausschließlich den Happy Path. Ein bUnit-Test mit Stub-Service im `Failed`-Zustand (oder E2E gegen einen Loopback-Server, der 404/korruptes Archiv liefert) fehlt.
- [ ] Kein dedizierter Test zu AK 5 „gesetzter Config-Pfad wird nicht überschrieben": ein Test, der `MediaProbeOptions.FfprobePath`/`FfmpegPath` auf eine existierende Datei zeigen lässt, den Download laufen lässt und die Unverändertheit dieser Datei (und das Ignorieren des Config-Pfads als Installationsziel) belegt, fehlt. Implizit durch unveränderte Locator-Tests abgedeckt, aber nicht als eigener Nachweis geplant.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|---|---|---|
| Admin: fehlt-Hinweise in beiden Tabs → GPL-Dialog → Download mit Fortschritt → Erfolg → Hinweise verschwunden (AK 1, 2, 3, 4) | Neuer Playwright-Test `TVSenderAdminE2ETests.Admin_DownloadsFfmpegTools_WhenMissing` mit Loopback-`HttpListener` (zip mit Dummy-Binaries), `MediaProbe:DownloadSources:*`-Überschreibung und Locator/`baseDirectory`-DI-Override | Abgedeckt (mit Hinweis zur PATH-/Config-Neutralisierung, s. unten) |
| Nicht-Admin: kein Zugriff auf die Seite/Aktion (AK 6, UI-Seite) | Bestehender `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized`; serverseitige Dienst-Absicherung per Unit-Test | Abgedeckt |
| Download-Fehlerfall sichtbar in der UI (AK 2, „Fehler verständlich anzeigen") | Nicht geplant | Lücke |

## Fehlende oder unvollständige Planbestandteile

- [ ] E2E-Locator-Override unvollständig beschrieben: laut `inventory/tests.md` ist ffmpeg/ffprobe auf dem Testsystem **vorhanden**. Damit die fehlt-Hinweise im E2E überhaupt erscheinen, muss der DI-Override der Locators neben `baseDirectory` auch die `pathEnvironment`-Quelle (leerer/kontrollierter PATH) neutralisieren und sicherstellen, dass `MediaProbe:FfprobePath`/`FfmpegPath` im Test-Host nicht gesetzt sind — der interne Locator-Ctor erlaubt genau das, der Plan nennt aber nur „Temp-`baseDirectory` verbiegen".
- [ ] Kein Test geplant, der die Allowlist-/Pfad-Traversal-Verwerfung beim Entpacken belegt (s. fehlende Testanforderungen).
- [ ] Kein Negativtest für die UI-Fehleranzeige eines `Failed`-Downloads (s. fehlende Testanforderungen).

## Hinweise

- **Atomarität ist solide geplant, mit kleinem Restrisiko:** Download → System-Temp-Datei → Archiv-Validierung (Allowlist, erwartete Einträge, Traversal-Verwurf) **vor** Installation → `*.tmp`-Staging im Zielverzeichnis + `File.Move(overwrite: true)` je Binary; Temp-Datei wird in allen Pfaden aufgeräumt. Nicht explizit spezifiziert: ob beide Binaries erst komplett gestaged und dann beide gemovt werden — ein IO-Fehler zwischen den zwei Moves könnte einen halb installierten Zustand hinterlassen (nur eines der Binaries aktualisiert). Da ein „fehlt"-Hinweis dann korrekt weiter angezeigt würde, ist das Risiko gering, aber eine „beide stagen, dann beide moven"-Formulierung wäre präziser.
- **Serverseitige Admin-Erzwingung ist konkret:** `StartDownloadAsync(userId)` mit `IServiceScopeFactory` → `ApplicationDbContext` → `IsAdmin`-Prüfung im Singleton (Muster `MediaDurationProbeService.RequireAdminAsync`), plus `IsActive`-Sperre gegen Parallelstarts — keine reine UI-Ausblendung. Einzige Unschärfe: Exception-Typ im Plan alternierend `UnauthorizedAccessException`/`ForbiddenAccessException` genannt (beide sind im Bestandsmuster vorgesehen).
- **E2E-Strategie insgesamt realistisch und konsistent:** `MediaProbe:DownloadSources` wurde explizit auch als Test-Verdrahtungspunkt eingeführt (Designentscheidung „Quellen-Konfiguration"); Loopback-Server + DI-Overrides passen dazu. Detailrisiko `HttpListener` auf Windows (http.sys-URL-Reservierungen): falls der `localhost`-Prefix im CI/Entwicklerkontext Probleme macht, auf Kestrel/`WebApplication`-Mini-Host auf Loopback ausweichen — im Plan nicht als Fallback benannt.
- **Kleine Plan-/Tasks-Inkonsistenz:** Task 16 nennt „Schreibrechte" unter den Fehlerpfad-Tests; die Tests-Tabelle in `plan.md` enthält keinen expliziten Schreibrechte-Test (nur als Validierungsregel „Zielverzeichnis" und Risiko-Hinweis). Gleiches gilt für den tar.xz-Pfad auf Windows-Testsystemen („bedingt ausführen" ist erwähnt, aber die konkrete Ausführbedingung bleibt offen).
- **Button-Sperre während `IsActive` und Ausblenden nach `Succeeded`** sind geplant, aber ohne eigenen bUnit-Nachweis — im E2E-Happy-Path implizit mitgeprüft; wäre als expliziter Assert im E2E-Test zu verankern.
- Alle „Offenen Fragen" der Anforderung sind im Plan verbindlich entschieden (Quellen gyan.dev/johnvansickle, SharpCompress für tar.xz, x64-only, ein Paket installiert beide Binaries, GPL-Build mit Hinweisdialog, Platzierung in der Segmentierungs-Karte, `tools/ffmpeg/` übersteht Updates laut `docs/TECH_Auto_Update.md` ohne `ProtectedFiles`-Eintrag).
