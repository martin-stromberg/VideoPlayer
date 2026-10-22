# Handover – Automatisierter TV-Sender

## Bearbeiter

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Devin |  | aktiv | 2026-10-21 |
| 2 | Devin |  | fertig | 2026-10-21 |
| 3 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-3-sendeplan-erzeugen | pausiert | 2026-10-21 |
| 4 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-4-sendeplan-nachfuehren | abgeschlossen | 2026-10-21 |
| 5 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-5-programmauskunft | abgeschlossen | 2026-10-21 |
| 6 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-6-live-stream | pausiert | 2026-10-21 |
| 7 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-7-live-tv-ui | abgeschlossen | 2026-10-21 |
| 8 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-8-probe-robust | abgeschlossen | 2026-10-21 |
| 9 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-9-genre-auswahl | aktiv | 2026-10-22 |
| 11 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-11-probe-fortschritt | aktiv | 2026-10-21 |
| 12 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-12-sftp-probe | aktiv | 2026-10-22 |
| 13 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-13-genre-vererbung | aktiv | 2026-10-22 |
| 16 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-16-admin-ux | abgeschlossen | 2026-10-22 |
| 18 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-18-maskottchen | fertig | 2026-10-24 |
| 19 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-19-stream-input | aktiv | 2026-10-22 |

## Übergabestand je Schritt

### Schritt 1: Videolaufzeit erfassen — FERTIG (abgenommen)

- ffprobe-basierte Laufzeitermittlung (`Duration`/`DurationProbedAt` an MediaItem), Scan-Reset, Nachtrags-Backfill-Worker, Migration, Backup-Registrierung. Abnahme: `acceptance-schritt-1.md` — erfüllt.

(Bisher keine Übergaben — Projektstart.)


### Schritt 2: Sender und Programmregeln verwalten — FERTIG (abgenommen)

- TVChannel + ProgramRule (alle Regelarten/Materialarten), Admin-CRUD serverseitig, MediaSearchSelector-Auswahl, HolidayFederalState in Setup, Migration + Backup-Registrierung. Abnahme: `acceptance-schritt-2.md` — erfüllt.
- Hinweis an Schritt 3: verwaiste MaterialIds ohne DB-FK sind Designentscheidung, Behandlung dort vorgesehen.

### Schritt 3: Sendeplan aus Regeln erzeugen

- Umgesetzt: `ProgramSlot` (weiche Referenzen, `RuleType`-Snapshot), `ProgramRuleCursor` (Fortsetzung je Regel, FK+Kaskade), `Setup.ProgramScheduleHorizonHours`/`ProgramScheduleIntervalMinutes`, `GermanHolidayCalendar` (Computus + Landesfeiertage), `ProgramScheduleService` (Expand-only, Prioritätskonflikte, Füller-Lückenfüllung, Freigabe- und Laufzeitfilter, verwaiste Materialien), `ProgramScheduleWorker`, Admin-UI-Felder, Migration `AddProgramSchedule`, Backup-Registrierung inkl. Alt-Backup-Regressionstest.
- Entscheidungen: Ausstrahlungen im 24-h-Lookback konsumieren den Cursor (vergangene Sendung = „gelaufen"); der letzte Füller darf über das Horizont-Ende hinauslaufen; Freigabe = für jeden Nicht-Admin-Benutzer sichtbar (Quelle ODER Einzelfreischaltung); Extend-only — Reaktion auf Änderungen ist Schritt 4.
- Tests: 26 neue Tests (Generation, Konflikte, Füller, Freigabe, Feiertage, Settings, Worker, Alt-Backup) plus erweiterter E2E (`TVSenderAdminE2ETests`); alle grün.


### Schritt 3: Sendeplan aus Regeln erzeugen — FERTIG (abgenommen)

- ProgramSlot + ProgramRuleCursor (Serien-Fortsetzung, Wrap-around), ProgramScheduleService (extend-only, Prioritäts-Konflikte atomar, Füller lückenlos, Filter: Duration + Vollfreigabe, verwaiste Materialien übersprungen), GermanHolidayCalendar (Computus + DE-*), Worker mit Gate-Lease, Settings Horizon/Intervall in TVSenderAdmin. Abnahme: `acceptance-schritt-3.md` — erfüllt.
- Schritt 4 baut auf: Plan-Nachführung/Verdrängen.


### Schritt 4: Sendeplan bei Änderungen nachführen — FERTIG

- ProgramSlot.Status (Scheduled/Displaced, Migration AddProgramSlotStatus, optionale Backup-Spalte + Alt-Backup-Regressionstest); ProgramScheduleService.ReconcileAsync — Validierungssweep per Ausstrahlungs-Simulation (kein Dirty-Flag nötig, absturzsicher), Suffix-Verdrängung je Regel, Cursor-Rücksetzung (Wiedereinplanung ohne Episodensprung), Prioritäts-Eviction, Neu-Materialisierung + Füller-Lücken; GetScheduleAsync für die Admin-Ansicht.
- IProgramScheduleSignal/ProgramScheduleSignal (koaleszierend, Scan-Suppression); Worker weckt per Signal oder Intervall und läuft hinter IBackgroundProcessingGate; Trigger: ProgramRuleService-CRUD, DbContext-Marker-Hook (neue Episoden/Filme), DeleteMediaSourceAsync, Scan-Ende.
- TVSenderAdmin: neue Karte „Sendeplan" mit Status-Chip „Verdrängt" (E2E-nachgewiesen); Hilfe tv-sender.md + RELEASE_NOTES aktualisiert.
- Entscheidungen: Suffix-Verdrängung statt Einzel-Slot für Sequenz-Konsistenz (kann inhaltlich identische Ersatzzeilen erzeugen); Restlücken < kleinster Füller bleiben wie bei der Erzeugung; Bulk-ExecuteDelete-Pfade außer DeleteMediaSourceAsync werden nur periodisch erkannt (zustandsloser Sweep als Netz).
- Tests: 14 neue (10 Reconcile + 3 Signal + 1 Alt-Backup) plus erweiterter E2E; volle Suite 1865 grün / 1 übersprungen / 0 fehlgeschlagen (Baseline-Flake PlaylistSkipLockedEntriesE2ETest im Retry grün, dokumentiert).


### Schritt 4: Sendeplan bei Änderungen nachführen — FERTIG (abgenommen)

- ReconcileAsync (Validierungssweep, Suffix-Verdrängung, Cursor-Rücksetzung ohne Episodensprung, Prioritäts-Eviction), ProgramSlotStatus (Scheduled/Displaced), IProgramScheduleSignal + Trigger (ProgramRuleService-CRUD, DbContext-Marker für neue Medien/Löschung, Scan-Suppression), Sendeplan-Karte in TVSenderAdmin. Abnahme: `acceptance-schritt-4.md` — erfüllt nach Fix von CI-Warnungen (CS1573/CS1574/xUnit1051).


### Schritt 5: Laufendes Programm zeitbasiert auskunftsfähig machen — FERTIG (Implementierung; Abnahme ausstehend)

- `IProgramGuideService`/`ProgramGuideService` (eigener Begriff „Programmauskunft", kein Playlist-Naming): rein zeitbasiert, zustandslos — `GetNowAsync` (laufende Sendung + `PositionSeconds`/`RemainingSeconds` + nächste Sendung) und `GetUpcomingAsync` (begrenzte Vorschau, harte Obergrenze `MaxPreviewHours = 12`). Nur `Scheduled`-Slots; `Displaced` ist Historie. Kanal wird lesend geholt (kein Seiteneffekt-Anlegen). Titelauflösung per Batch-Lookup nach `TVSenderAdmin`-Muster, Fallback „Unbekannt".
- `ProgramGuideController` (`api/program-guide`): `GET now`, `GET upcoming?hours=` (Default 6, 1..12 sonst 400 — Bereichsprüfung vor `TimeSpan.FromHours` wegen Overflow). `[BearerTokenCheck]` (Header oder `access_token`-Query) + `CheckLoggedIn`, `ExecuteAsync`-Fehlermapping (401/400/500) nach `PlaylistsController`-Muster.
- DTOs `DtoProgramGuideNow`, `DtoProgramGuideEntry` in `VideoWebPlayer.Client/Models`; DI scoped neben `IProgramScheduleService`. Keine Migration, keine Backup-Änderung (reiner Service/API-Schritt).
- Tests: 27 neue (12 Service gegen echtes SQLite via `ProgramScheduleTestDb` + `CreateSlotAsync`, 9 Controller, 6 HTTP-E2E via `DeviceClientTestBase` inkl. 401-anonym und `access_token`-Query). Volle Suite: 1892/1893 grün, 0 Fehler, 1 bekannter Alt-Skip.
- Doku: `docs/help/tv-sender.md` (Abschnitt Programmauskunft), `docs/API.md` (beide Endpunkte), `docs/RELEASE_NOTES.md` (What's New + Neuerungen).
- Entscheidungen: `ProgramGuide`-Namensraum (EPG-Begriff); `Next` = erster `Scheduled`-Slot mit `Start >= current.End` (bzw. `>= now` in der Lücke); Vorschau-Fenster halb-offen `[now, now+preview)`; Grenze als Konstante statt Setup-Feld.
- Hinweis an Schritt 6: `DtoProgramGuideEntry.MediaItemId` ist die Videodatei-Referenz für die Stream-Auslieferung; `ProgramGuideService.MaxPreviewHours` begrenzt auch die Vorschau des UI (Schritt 7).


### Schritt 5: Programmauskunft — FERTIG (abgenommen)

- IProgramGuideService/ProgramGuideService (GetNowAsync: Position/Remaining/Duration + Next; GetUpcomingAsync: halbes Fenster, MaxPreviewHours=12, nur Scheduled), ProgramGuideController (api/program-guide/now + upcoming, BearerTokenCheck + CheckLoggedIn, 400 bei ungueltigem hours), DTOs. Abnahme: `acceptance-schritt-5.md` — erfüllt.
- Schritt 6 nutzt dieselbe Auskunft als Basis für den Stream.

### Schritt 6: Live-Stream ausliefern — FERTIG (Implementierung; Abnahme ausstehend)

- Echte ffmpeg-Segmentierung (kein virtueller Stream): `FfmpegLiveStreamSegmenter` (Singleton, `ISegmenterProcess`-Testseam, `BuildArguments`: `-re -ss -t -f hls`, H.264/AAC-Transcode mit segmentausgerichteten Keyframes, `hls_list_size 12`, `delete_segments+independent_segments`, eindeutige Segmentnamen `seg_%Y%m%d%H%M%S_{slotId}_%05d.ts`, `-nostdin` bei Datei- bzw. stdin-Pump bei Pipe-Eingang, `Kill(entireProcessTree)`).
- Rein zeitbasierte Synchronisation ohne Prozesszustand: `LiveStreamCoordinator` fragt je Tick `IProgramGuideService.GetNowAsync` ab; laufender Slot → Start/Neustart bei Slot-Wechsel oder Drift > 30 s, Sendelücke/aufloesbares Medium → Stopp, `WakeAtUtc` = Slot-Ende bzw. Nächster-Start. `LiveStreamWorker` (Initialdelay, einmaliges `ClearAsync` des Segment-Verzeichnisses, Gate-Lease je Tick, Poll-Cap 30 s, Weckung über `IProgramScheduleSignal`, gedrosseltes Fehlen-Log).
- Aufloesung: `LiveStreamMediaResolver` — `LocalDirectory`-Quellen liefern den physischen Pfad (`LocalMediaSourceReader.ResolveLocalFilePath`, neue öffentliche Methode auf bestehender Containment-Logik), entfernte Quellen eine `OpenFileStream`-Streamfabrik (bekannte Einschraenkung: MP4 ohne moov-am-Anfang kann ueber Pipe nicht demuxen).
- Konfiguration: drei neue `Setup`-Spalten (Segmentlänge 6 s, Segment-Verzeichnis Default `Data/LiveStream` unter ContentRoot, Retention 30 min) + `ProgramSettingsService`-Getter/`UpdateLiveStreamSettingsAsync` + drei Felder in `TVSenderAdmin`; Migration `AddSetupLiveStreamSettings`, Backup-Optionaleinträge + Alt-Backup-Regressionstest. ffmpeg-Bereitstellung analog ffprobe: `MediaProbe:FfmpegPath` → `tools/ffmpeg/` + App-Dir → `PATH` (`FfmpegLocator`); GPL-Lizenzaspekt und Bereitstellung in `docs/help/tv-sender.md` dokumentiert.
- Auslieferung: `LiveStreamController` (`api/live-stream/{fileName}`, `[BearerTokenCheck]` + `CheckLoggedIn`): `.m3u8` wird beim Abruf gelesen und jede Segment-URI um den übergebenen `access_token` ergänzt (relative URIs verlieren sonst die Query), `no-store`; `seg_*.ts` als `video/mp2t`; strikte Dateinamen-Whitelist + Containment, 404 bei ungültig/fehlend/liegender Sendelücke. Serverseitige Zugriffsprüfung — kein UI-Versteck.
- Tests: 61 neue (Locator, Segmenter-Fake-Prozess, Coordinator gegen echtes SQLite, Resolver, Store, Controller, Worker, Settings, Alt-Backup, HTTP-E2E 401/200/Token-Rewrite/404, Playwright-Erweiterung). Volle Suite: 1953/1954 grün, 0 Fehler, 1 bekannter Alt-Skip.
- Doku: `docs/API.md` (neuer Endpunkt), `docs/help/tv-sender.md` (Abschnitt „Live-Stream (HLS)" + ffmpeg-Bereitstellung/Lizenz), `docs/RELEASE_NOTES.md` (What's New).
- Entscheidungen: Roh-Pfad im Setup speichern — ContentRoot-Auflösung liegt konsistent im `LiveStreamSegmentStore`; kein persistenter Stream-Zustand; Drift-Schwelle 30 s als Konstante; `-t` deckelt die Ausgabe auf die Slot-Restdauer als Sicherheitspuffer gegen Worker-Lag.
- Hinweis an Schritt 7: Player konsumiert `GET /api/live-stream/live.m3u8?access_token={token}`; die Player-Oberfläche (Videoelement/HLS-Client) ist erst Schritt 7.


### Schritt 6: Live-Stream ausliefern — FERTIG (abgenommen)

- FfmpegLiveStreamSegmenter (Singleton, -re/-ss/-t, HLS-Fenster, stderr-Drain), LiveStreamCoordinator + LiveStreamWorker (zeitbasierte Resynchronisierung, Slot-Wechsel/Drift/Luecke), LiveStreamController (api/live-stream, BearerTokenCheck, m3u8-Token-Rewrite, Traversal-Schutz), LiveStreamSegmentStore (Speicherort + Retention), Setup-Spalten + Migration + Backup. Abnahme: `acceptance-schritt-6.md` — erfüllt nach Fixes (stderr-Drain, xUnit1051, CS1570).
- Schritt 7: Player konsumiert api/live-stream/live.m3u8?access_token={token}.


### Schritt 7: Live-TV in der Benutzeroberfläche — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- Neue Seite `Components/Pages/LiveTv.razor` (`/live-tv`, InteractiveServer): Videoplayer nur bei laufender Sendung, „Jetzt läuft" (Titel, Uhrzeiten, Restdauer, Fortschrittsbalken), „Gleich", begrenzte Vorschau der nächsten 6 h (`ProgramGuideService.MaxPreviewHours`-Grenze serverseitig ohnehin gedeckelt). Vollständiger Sendeplan bleibt administrativ.
- Client-API: neues `IProgramGuideApiClient` (partial `VideoWebPlayerClient.ProgramGuide.cs`, nutzt `HttpGetAsync`, teilt Reauth/Impersonation) — gleiches Muster wie `IPlaylistApiClient`; DI-Registrierung scoped auf dieselbe Client-Instanz.
- HLS-Wiedergabe: `wwwroot/js/liveTv.js` — natives HLS via `canPlayType` zuerst, sonst hls.js (Lazy-Load des fingerabdruckten `js/vendor/hls.min.js`, erst bei Bedarf); `WeakMap`-Zustand je Videoelement, `detach` räumt Listener/Timer/hls-Instanz auf, JS-Re-Attach alle 5 s bei fatalen Fehlern, DotNet-Callbacks `NotifyStreamPlaying`/`NotifyStreamError`/`NotifyStreamUnsupported`. hls.js 1.7.3 vendored (Apache-2.0, `hls.js.LICENSE.txt` liegt daneben); kein Pipeline-Eingriff, Release-Build unberührt.
- Authentifizierung: `access_token`-Query auf `/api/live-stream/live.m3u8` aus `Client.AuthorizationToken` nach `EnsureAuthorizationTokenSilentlyAsync`; Server bleibt verbindliche Prüfung (`[BearerTokenCheck]`).
- Verständliche Zustände: `LoginForm` bei unauthentifiziert, `LiveTvErrorMessages` (Sendelücke, Stream-Hinweis mit Auto-Retry, Browser-Hinweis, Ladefehler, 403-Zugriffstext) — kein technischer Fehlertext im UI.
- Aktualität: `PeriodicTimer` 15 s (Datenabruf im Hintergrund, `InvokeAsync(StateHasChanged)` nur fürs Rendern; Einzelfehler tötet die Schleife nicht), Re-Attach bei Slot-Wechsel in `OnAfterRenderAsync`.
- Navigation: Eintrag „Live-TV" (`bi-tv`) im `<Authorized>`-Block der `NavMenu`.
- Tests: 12 neue — bUnit `LiveTvTests` (7), `NavMenuLiveTvTests` (1), `VideoWebPlayerClientTests_ProgramGuide` (2), Playwright `LiveTvE2ETests` (2: Nav→Player+`access_token`-Request; Sendelücke mit Hinweis). Volle Suite: **1965/1966 grün, 0 Fehler, 1 bekannter Alt-Skip**; Debug+Release-Builds 0 Fehler.
- Bekannte Einschränkung beim E2E-Nachweis: „Wiederanlauf bei Slot-Beginn ohne Reload" ist nicht browserseitig testbar, weil `ReconcileAsync` manuell eingefügte, regellose Slots als `Displaced` verwirft (im Lauf verifiziert: DB-Dump; Poll-Ticks liefen korrekt alle 15 s). Abgedeckt über bUnit-Zustandsübergänge; im Test kommentiert.
- Doku: `docs/help/tv-sender.md` (Abschnitt „Live-TV-Ansicht"), `README.md`, `docs/RELEASE_NOTES.md`. Kein `changes.log` im Repo vorhanden (Datei existiert nicht) — kein Eintrag möglich.
- Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-7-live-tv-ui/` (requirement, inventory, plan, plan-check, review, review-usability, review-code, test-results).
- Hinweis an Abnahme: Baseline-Flake `TVSenderAdminE2ETests` (FillAsync-Timing) — Einzelwiederholung grün, kein Regressionszusammenhang.


### Schritt 7: Live-TV in der Benutzeroberfläche — FERTIG (abgenommen)

- LiveTv.razor (/live-tv, nativ+hls.js-Fallback mit Retry, Jetzt/Gleich + 6h-Vorschau, verständliche Fehlerzustände, sauberes Dispose), hls.js 1.7.3 vendored (Apache-2.0), IProgramGuideApiClient, Nav-Eintrag. Abnahme: `acceptance-schritt-7.md` — erfüllt nach Fixes (nativer Retry, bi-tv-Icon, Analyzer-Warnungen, Format).


### Schritt 8: Laufzeit-Ermittlung robust für Remote-Quellen + Retry (N4) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- Konfiguration: `MediaProbe:StreamTimeoutSeconds` (Default 600 s, nur `pipe:0`-Probes; `TimeoutSeconds` 120 s gilt weiter für lokale Pfade), `RetryDelaySeconds` (300 s), `MaxAttempts` (10), `ParallelProbes` (4) — alle in `MediaProbeOptions` mit `Effective*`-Fallback und in `appsettings.json` dokumentiert.
- Retry statt Dauer-Brandmarkung: neue Spalte `MediaItem.DurationProbeAttempts` (Migration `AddMediaItemDurationProbeAttempts`, `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` Default 0 → Alt-Backups restaurieren und stellen failed-Items wieder in die Queue). `DurationProbedAt` ist jetzt „Zeitpunkt des letzten Versuchs"; Selektion: `Duration == null && (DurationProbedAt == null || (Attempts < max && DurationProbedAt <= jetzt - RetryDelay))`. Bestands-Failed-Items (Attempts = 0) werden automatisch wieder aufgegriffen; Rescan (`MediaSourceScanner`) setzt den Zähler mit zurück.
- Fehlerklassifikation: `IMediaDurationProbe` liefert `MediaDurationProbeResult(Duration, Transient)` statt `TimeSpan?`. `FfprobeMediaDurationProbe`: Timeout/Start-Fehler/Exit != 0/Ausnahme → transient; Exit 0 ohne parsebare Dauer → permanent (setzt Attempts = max → kein Retry). stderr wird für Warn-Logs erfasst (200 Zeichen gekürzt).
- Parallelität: `SemaphoreSlim(EffectiveParallelProbes)` über `Task.WhenAll` — nur ffprobe/Stream läuft parallel, DbContext-Anwendung + `SaveChangesAsync` bleiben seriell je Item; `SftpMediaSourceReader.OpenFileStream` baut pro Aufruf eigenen `SftpClient`.
- Planbarkeit: `MediaDurationProbeService` injiziert optional `IProgramScheduleSignal` und feuert `NotifyScheduleChanged()` sobald ≥1 `Duration` geschrieben wurde → Items sind sofort wieder planbar.
- Tests: 38 Probe-Tests grün (Retry-Gate, Legacy-Aufgriff, MaxAttempts, permanent vs. transient, Stream-Timeout, Parallelität, Signal, Alt-Backup-Regression). Volle Suite synchron: **1975 grün / 0 Fehler / 1 bekannter Alt-Skip**; Debug+Release-Build 0 Fehler; `dotnet format --verify-no-changes --severity error` sauber.
- Doku: `docs/help/videolaufzeit.md` (Retry-Semantik + 4 neue Schlüssel), `docs/RELEASE_NOTES.md`. Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-8-probe-robust/` (im Abschlusscommit entfernt, Planung in Historie).
- Hinweis an Abnahme: Bewusste Designentscheidung — `ExitCode != 0` zählt als transient (Stream-Abriss nicht von korrupt unterscheidbar); korrupte Dateien kosten maximal `MaxAttempts` Versuche. Permanente Fehler werden über `Attempts >= MaxAttempts` codiert — Erhöhung von `MaxAttempts` reaktiviert sie (harmlos).
- Hinweis an Schritt 9/10: `DurationProbedAt`-Index existiert weiter; N5-Diagnose kann `DurationProbeAttempts`/`DurationProbedAt` für „keine Laufzeit"-Gründe nutzen.


### Schritt 8: Probe-Robustheit (N4) — FERTIG (abgenommen)

- StreamTimeoutSeconds=600 für pipe:0 (lokal bleibt 120s), MediaDurationProbeResult transient/permanent, Retry 300s/max 10 via DurationProbeAttempts, Altbestand-Faileds werden wieder aufgegriffen, ParallelProbes=4, ProgramScheduleSignal bei fertiger Duration. Abnahme: `acceptance-schritt-8.md` — erfüllt.

### Schritt 9: Genre-Material — Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3) — Implementierung abgeschlossen, Abnahme steht aus

- Datenmodell: `ProgramRuleGenre` + `ProgramRuleMediaSource` (Composite-Key-FKs, Cascade); Migration `AddProgramRuleGenreAndSourceLinks` mit Backfill `MaterialId`→Join; beide Tabellen in `OptionalRestoreTables`; `MaterialId` bleibt Kompatibilitätsspalte (erstes Genre / Altbestand-Fallback in Expansion, UI und Backup-Restore ohne Join-Rows).
- Logik: `GenreCanonicalResolver` (sichtbar = nicht `IsHidden` + saisonales Fenster analog `GenreService`; Alternativnamen rekursiv + zyklussicher quellbezogen aufgelöst); Sender-Optionsquelle `IProgramRuleService.GetGenreMaterialOptionsAsync` — `api/items/genres` bleibt bewusst unverändert (Metadaten-/Playlist-Editoren brauchen rohe Namen).
- Expansion (`ExpandGenreAsync`): Stored-Genres → kanonische Namen → alle In-Scope-Genre-Rows (Quell-Filter) mit Überschneidung → Union von Filmen/Serien inkl. Alt-Zuordnungen auf versteckte Genre-Zeilen → Quell-Filter zusätzlich auf Inhalts-`MediaSourceId` → Dedupe pro Inhalt → Access-/Dauer-Filter + Cursor-Semantik unverändert.
- Validierung serverseitig: Genre-Material verlangt ≥1 GenreId; Genre-/Quell-Listen bei anderem Material → Fehler; unbekannte Ids → `KeyNotFoundException`; Admin-Pflicht überall.
- UI (`TVSenderAdmin`): Genre-Checkbox-Liste `rule-material-genre` + Quell-Checkboxen `rule-material-sources` (nur bei Genre-Material), Optionsliste folgt Quell-Filter, bereits gewählte Genres außerhalb des Filters bleiben sichtbar, Material-Chip `Genre: A, B`, Tabelle zeigt Quellen.
- Tests: 24 neue Tests (10 Schedule/Genre, 10 Service/Genre-Material, 2 Backup-Regression, 1 konsolidierter E2E), alle gegen echtes SQLite bzw. Playwright. Volle Suite synchron: **1998 grün / 0 Fehler / 1 bekannter Alt-Skip (1999 gesamt)**; Debug- und Release-Build 0 Fehler.
- Doku: `docs/help/tv-sender.md` (Programmmaterial-Abschnitt), `docs/RELEASE_NOTES.md`. Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-9-genre-auswahl/`.
- Hinweis an Abnahme: Bewusste Entscheidungen — leere Genre-Auswahl wird serverseitig abgelehnt; Quell-Filter ist pro Regel (nicht pro Genre); Options-Dedupe nach kanonischem Namen über Quellen hinweg mit kleinster sichtbarer kanonischer Id als Repräsentant.
- Hinweis an Schritt 10 (N5): Genre-Expansion liefert jetzt deduplizierte Union über `ProgramRuleGenre`-Joins — Laufzeit-Diagnose siehe `ExpandGenreAsync` in `ProgramScheduleService`.


### Schritt 9: Genre-Auswahl (N1+N2+N3) — FERTIG (abgenommen)

- Join-Tabellen ProgramRuleGenre/ProgramRuleMediaSource (Mehrfachauswahl + Quell-Filter leer=alle), GenreCanonicalResolver (IsHidden/Alternativnamen/StartDate-EndDate, zyklussicher), eigene Options-API GetGenreMaterialOptionsAsync (api/items/genres unveraendert), ExpandGenreAsync auf kanonischer Union + Quell-Filter, Alt-Zuordnungen eingeschlossen, Migration mit MaterialId-Backfill. Abnahme: `acceptance-schritt-9.md` — erfüllt.
- Hinweis an Folgeschritte: Saison-Tests sind laufzeitdatums-abhaengig (Nov/Dez-Fenster beachten); Zyklen ad-hoc geprueft.


### Schritt 10: Planungsdiagnose + Generierungsanstoß (N5) — FERTIG (abgenommen)

- GetRuleDiagnosesAsync (admin-only, on-demand ueber ExpandMaterialAsync, Gruende: MaterialMissing/NoMediaItem/ProbePending/ProbeExhausted/nicht freigegeben), Planbar-Spalte in TVSenderAdmin, #tvsender-generate-now Button -> ScheduleSignal. Abnahme: `acceptance-schritt-10.md` — erfüllt.
- E2E-Flake Admin_CreateEditDeactivateDeleteRule (Settings-Race aus Schritt 7) dokumentiert, Einzelretry gruen.


### Schritt 11: Fortschrittsanzeige Laufzeitermittlung (N6) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- Neue Admin-only-Abfrage `MediaDurationProbeService.GetProgressAsync(userId, recentCount, ct)` (serverseitiges `RequireAdminAsync` wie in ProgramRule/ScheduleService): Zähler Total/Ermittelt/Ausstehend/Dauerhaft gescheitert über die `VideoExtensionPredicate()`-Teilmenge, `LastActivityAt` = MAX(`DurationProbedAt`), `ProbedLastHour` (1-h-Rate via `TimeProvider`), Rolling-Liste `MediaDurationProbeRecentItem` (Name, Quelle, Zeitpunkt, Dauer, Versuche) — neue Records `MediaDurationProbeProgress`/`MediaDurationProbeRecentItem` in `Services/MediaDurationProbeProgress.cs`. Keine Migration nötig.
- `TVSenderAdmin.razor`: neue Karte „Laufzeitermittlung" am Seitenende (`#tvsender-probe-card`) — Statuszeile (`ProbeStatusLabel`: ermittelt/abgeschlossen/läuft/hängt), Chips ermittelt/ausstehend/fehlgeschlagen, Bootstrap-Fortschrittsbalken, letzte Aktivität + Stundenrate, Rolling-Tabelle `#tvsender-probe-recent`, Empty-State; `PeriodicTimer`-Auto-Refresh 15 s (Muster `LiveTv.razor`), Abbruch in `Dispose` via `refreshCts.Cancel()`.
- Tests: 5 neue `MediaDurationProbeServiceTests_Progress` (echtes In-Memory-SQLite, Zähler/Reihenfolge+Limit/Aktivität+Rate/Leerstand/Admin-Erzwingung); E2E konsolidiert in `Admin_CreateEditDeactivateDeleteRule` erweitert (deterministische Seeds: 1 ermittelt + 1 dauerhaft gescheitert, beide nicht ausstehend, damit der mitlaufende Probe-Worker den Zustand nicht verändert).
- Volle Suite synchron (`-c Release`): **2012 grün + 1 bekannter E2E-Flake (`PlaylistSkipLockedEntriesE2ETest`, Einzelwiederholung grün) + 1 bekannter Alt-Skip** — 2014 gesamt. `dotnet format --verify-no-changes --severity error` sauber. Doku: `docs/help/tv-sender.md` (Abschnitt „Fortschritt der Laufzeitermittlung"), `docs/help/videolaufzeit.md` (Abschnitt „Fortschritt beobachten"), `docs/RELEASE_NOTES.md` (EN+DE).
- **Offener Umgebungspunkt für die Abnahme:** `dotnet build VideoPlayer.sln` (Debug) schlug mit MSB3027 fehl, weil eine laufende Benutzer-Instanz `VideoWebPlayer.exe` (PID 55720, aus VS gestartet) `bin/Debug/net10.0/` sperrt — der Prozess wurde nicht beendet. Release-Build 0 Fehler; Debug-Kompilierung vollständig über Scratch-`-p:OutDir`-Build verifiziert (0 Fehler, Warnungsstand 142 = Bestand). Sobald die Instanz geschlossen ist, läuft der normale Debug-Build wieder.
- Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-11-probe-fortschritt/` (im Abschlusscommit entfernt, Planung in Historie).


### Schritt 11: Fortschrittsanzeige Laufzeitermittlung (N6) — FERTIG (abgenommen)

- MediaDurationProbeProgress + GetProgressAsync (admin-only), Fortschrittskarte in TVSenderAdmin mit Zaehlern/Balken/Rolling-Liste/Status (laeuft/fertig/haengt), 15s-Auto-Refresh. Abnahme: `acceptance-schritt-11.md` — erfuellt nach Fix (Tick auf Dispatcher via InvokeAsync statt Threadpool-DbContext).

### Schritt 12: Remote-Probes seekbar via sftp:// (N8) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- `IMediaDurationProbe.ProbeUriAsync` neu: `FfprobeMediaDurationProbe` übergibt für SFTP-Items mit Credentials eine `sftp://user:pass@host:port/pfad`-URL direkt an ffprobe (libssh, seekfähig — nur Header/moov statt ganzer Datei via `pipe:0`); `SftpProbeUriBuilder` (internal static) baut die URL mit `Uri.EscapeDataString` je Segment + IPv6-Klammerung + Port-Konditional, liefert `null` bei fehlenden Credentials/relativem Pfad → Pipe-Fallback.
- Pfadauflösung: `MediaItem.Path` (voller `entry.FullName`) wenn absolut, sonst `collection.Path`+`item.Path`/`item.Name` (`SftpProbeUriBuilder.CombinePath`, spiegelt `OpenFileStream`); nicht absolut → Fallback.
- Credential-Schutz: Userinfo-Maskierung `scheme://***@` für Log-Anzeigename UND stderr (ffprobe echot die URL in Fehlermeldungen). Entscheidung dokumentiert (`docs/help/videolaufzeit.md`): Passwort in der Prozesszeile für denselben Benutzer sichtbar — keine libssh-Alternative (nur `timeout`/`truncate`/`private_key`), akzeptabel im Single-Admin-Server-Kontext, Passwort liegt bereits in der App-DB.
- Klassifikation: `moov atom not found`/`Invalid data found when processing input` bei Exit≠0 + seekfähiger Eingabe → `PermanentFailure` (statt 10 Retries); `pipe:0` bleibt bewusst transient (Stream-Abbruch erzeugt dieselbe stderr). `Protocol not found` → neues `MediaDurationProbeResult.UriUnsupported` + `_uriProtocolUnsupported`-Instanzflag → Service fällt im selben Versuch auf `pipe:0` zurück.
- Timeout der URI-Probe: `EffectiveTimeoutSeconds` (120 s), nicht Stream-Timeout.
- Unverändert (bewusst): Live-Stream-Seite (`LiveStreamMediaResolver`/`FfmpegLiveStreamSegmenter`) — gleiche Pipe-Einschränkung bleibt dort bestehen.
- Tests: +33 (12 `SftpProbeUriBuilderTests`-Fälle, +8 Probe-Tests inkl. Maskierungs-Log-Test, +4 Service-Tests, Fakes erweitert). Volle Suite synchron (`-c Release`): **2046 grün / 0 Fehler / 1 bekannter Alt-Skip** (2047 gesamt; Baseline-Flake `PlaylistSkipLockedEntriesE2ETest` lief durch). Debug+Release-Build 0 Fehler, 142 Warnungen = Bestand; `dotnet format --verify-no-changes --severity error` sauber.
- Doku: `docs/help/videolaufzeit.md` (sftp://-Probe + Sichtbarkeitshinweis + Fallback + Klassifikation, `StreamTimeoutSeconds`-Semantik), `docs/RELEASE_NOTES.md` (EN+DE).
- Hinweis an Abnahme: kein echter SFTP-Integrationstest möglich (kein Server in der Testumgebung) — Nachweis über `_executeAsync`-Seam + Argument-/Klassifikations-Tests.


### Schritt 12: sftp://-Probes (N8) — FERTIG (abgenommen)

- SftpProbeUriBuilder (sftp://user:pass@host:port/pfad, EscapeDataString, IPv6), ProbeUriAsync seekable (120s lokal-Timeout reicht), Credentials in allen Log-Stellen maskiert, Pipe-Fallback ohne verbrannten Attempt + prozessweites UriUnsupported-Flag, moov/Invalid-data bei seekbarer Eingabe permanent. Abnahme: `acceptance-schritt-12.md` — erfüllt.


### Schritt 13: Genre-Konfiguration global vererben (N7) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- `GenreGlobalConfig` (interne statische Hilfsklasse, `Services/`): `HasCustomConfig` (IsHidden/Saisonfenster/effektive Alternativnamen via `GenreCanonicalResolver`), `LoadConfiguredTemplatesAsync` (älteste konfigurierte gleichnamige Zeile anderer Quellen, ci), `ApplyConfig` (EF-konform für neue + getrackte Zeilen).
- `MediaSourceClassifier.GetOrCreateGenresAsync` umgebaut (`GenreResolutionState` + `ResolveGenresAsync`): Quell-Genres einmalig statt pro Name geladen; neue Zeilen erben Vorlagen-Konfiguration; Default-Zeilen werden beim Zugriff geheilt (konfigurierte nie überschrieben); geerbte Alternativnamen lenken rekursiv auf kanonisches Genre derselben Quelle um; `visiting`-Zyklenschutz + Eigenname ci-ausgeschlossen; `GenreCanonicalResolver.EffectiveAlternateNames` auf internal gehoben (Wiederverwendung).
- `MediaMetadataEditorService.NormalizeGenresAsync`: gleiche Vererbung + Heilung; `Include(AlternateNames)` nötig für `HasCustomConfig`.
- `GenreAdmin.SaveGenre`: ci+Trim-Filter auf alle Zeilen (deckungsgleich mit der Gruppierung).
- `DataUpgradeManager` `CurrentVersion` 9, `Upgrade_9` → `ReloadGenres` (Bestandskorrektur beim Start; heilt Default-Zeilen + baut Verknüpfungen um). Keine Migration.
- Tests: +9 (`MediaSourceClassifierGenreInheritanceTests` 7 Fälle, `MediaMetadataEditorServiceGenreInheritanceTests`, `GenreAdminTests` bUnit) — echtes SQLite. Gegenprobe: Vererbung sabotiert → 3 Tests rot, revertiert.
- Volle Suite synchron (Debug): **2055 grün + 1 Skip / 0 Fehler** (2056 gesamt). Debug+Release-Build 0 Fehler. Doku: `docs/help/einrichtung.md`, `docs/RELEASE_NOTES.md`.
- Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-13-genre-vererbung/` (im Abschlusscommit entfernt, Planung in Historie); Tasks-Datei `docs/features/...-schritt-13-genre-vererbung-tasks.md` bleibt.


### Schritt 13: Genre-Vererbung (N7) — FERTIG (abgenommen)

- GenreGlobalConfig (Vorlage = aelteste konfigurierte ci-gleichnamige Zeile anderer Quellen), Vererbung bei Neuanlage in Scan + Metadaten-Edit, Heilung nur Default-Zeilen, Same-Source-Umlenkung + Zyklenschutz, SaveGenre-Casefix, DataUpgrade v9 ReloadGenres-Heilung fuer Altbestand. Abnahme: `acceptance-schritt-13.md` — erfüllt.
- Hinweis: C: fast voll (~0,6 GB) verursachte SQLITE_FULL in E2E-Tests.


### Schritt 14: HTTP-Range-Probes (N9) — FERTIG (abgenommen)

- Interner Endpunkt GET /api/internal/media-stream/{token} (256-Bit-In-Memory-Token, TTL 600s, pro-Datei gescoped), Loopback via IServerAddressesFeature + HttpProbeBaseUrl-Override, Fallback HTTP→sftp://→pipe:0, HTTP-Transportfehler=UriUnsupported. Abnahme: `acceptance-schritt-14.md` — erfüllt nach Fixes (Token-Maskierung in Logs/stderr, _uriProtocolUnsupported pro Scheme, BaseUrl-Negativcache weg).
- Offen: HTTP-Probe-Ansatz auch fuer Live-Streaming pruefen (Schritt-6-pipe:0-Grenze, Folgeschritt moeglich).


### Schritt 15: Saisonale Genres (N10) — FERTIG (abgenommen)

- BuildOptions includeOutOfSeason: Genres ausserhalb StartDate/EndDate bleiben waehlbar und werden mit Fenster-Label "Name (dd.MM.-dd.MM.)" angezeigt; !IsHidden explizit getrennt; Expansion unveraendert (Fenster wirkt nur auf Ausstrahlung). Abnahme: `acceptance-schritt-15.md` — erfüllt.


### Schritt 16: Adminseite restrukturieren + Fehlschlag-/Ausstehend-Liste (N13+N14) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- `TVSenderAdmin.razor` nach abgenommenem Mockup (`docs/projects/.../preview/admin-ux-vorschau.html`) in vier Tabs gegliedert: Regeln / Sendeplan / Einstellungen / Laufzeitermittlung. Panes bleiben gerendert und werden nur per CSS gezeigt — kein Reload, Formularzustand und 15s-Fortschrittsloop laufen weiter. Regel-Editor ist jetzt eine einklappbare Card unter der Regel-Tabelle (kein Overlay mehr); Lösch-Dialog bleibt Overlay. Header zeigt Sendername + Generierungsintervall als Chips; Einstellungen in drei Unterkarten (Sender/Planung/Segmentierung).
- `MediaDurationProbeProgress` um `PendingItems`/`FailedItems` (neuer Record `MediaDurationProbeDetailItem`: Name, SourceName, LastAttemptAt, Attempts) erweitert; `GetProgressAsync` mit `detailItemCount`-Parameter (Default 25), Filter identisch zu den Zaehlern, sortiert nach `DurationProbedAt` absteigend, ueber gemeinsamen Helfer `DetailItemsAsync`. „▾ Details" an den Zaehlern oeffnet `<details class="probe-detail">`-Tabellen (Name/Quelle/Versuche/Letzter Versuch) mit „… und N weitere"-Kappungshinweis; `open` an Razor-Bools gebunden, kein JS.
- Tests: +3 Service-Tests (Pending/Failed/Limit, echtes SQLite), +3 bUnit-Tests `Components/TVSenderAdminTests` (Tabs, Detail-Listen, Editor-Card), `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` an Tab-Struktur angepasst + Tab-/Detail-Assertions, `SwitchTabAsync`-Helfer.
- Volle Suite synchron (Debug): **2098 gruen + 1 Skip / 0 Fehler** (2099 gesamt; Skip = bekannter Baseline-Skip `PairedDevice_ReportsProgressForMovieWithoutCollection...`). Debug+Release-Build 0 Fehler. Doku: `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`.
- Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-16-admin-ux/` (im Abschlusscommit entfernt, Planung in Historie); Tasks-Datei `docs/features/...-schritt-16-admin-ux-tasks.md` bleibt.
- Bewusst nicht umgesetzt: optionales „Probe zuruecksetzen" (N13 optional, nicht im Mockup). Kein Befund in review.md/review-usability.md/review-code.md; Abnahme durch anderen Agenten steht noch aus.


### Schritt 17: Trailer (N11) — FERTIG (abgenommen)

- MovieMediaItem.IsTrailer (Migration+Backup), Konventionserkennung (-trailer-Suffix + trailers/-Ordner), Trailer nie als reguläres Material, neue Füller-Materialart Trailer anstehender Filme mit Vorlauf TrailerLeadDays (7), Fallback Füller. Abnahme: `acceptance-schritt-17.md` — erfüllt nach Fixes (Sidecar-Video-Filter, Trailer nur Füller, CS1572, bUnit-Test).


### Schritt 18: Maskottchen-Interstitials (N12) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- **Designentscheidung (kein Interstitial-Marker nötig):** Filme entstehen ausschließlich aus `<movie>`-NFOs (`ProcessCollectionAsMovieAsync`) — Videodateien ohne NFO bleiben rohe `MediaItem`s, unsichtbar in der Mediathek, aber geprobt (Duration-Pfad greift automatisch). Statt eines `IsTrailer`-analogen Markers wurde die neue Füller-Materialart **„Clip-Quelle"** (`ProgramRuleMaterialType.MediaSourceClips=6`, `MaterialId`→`MediaSources.Id`) gebaut: `ExpandMediaSourceClipsAsync` expandiert alle Video-`MediaItem`s der Quell-Collections **ohne** Movie-/Episoden-Verknüpfung, mit Duration und `AccessContext`-Freigabe — eingeordnete Inhalte derselben Quelle bleiben außen vor. Playlist-Material reichte nicht (nur Movie/Episode-Einträge), ein Movie-Flag hätte alle Bibliotheksabfragen getroffen — die Quell-Expansion ist die minimale, saubere Lösung und deckt N12s „eigene Quelle ohne Serien/Film-Einordnung" direkt ab.
- Umsetzung: `ProgramCursorContentType.MediaItem=4` (Ring-Fortsetzung), nur `Filler` zulässig (Repeat/HolidaySpecial explizit ausgeschlossen), Existenzprüfung in `ValidateRuleInputAsync`, Admin-Dropdown `#rule-material-clipsource` (nur bei Filler), `MediaItem.Name`-Titel-Fallback in `ProgramGuideService` + `TVSenderAdmin.SlotTitle` für Slots ohne Inhaltsreferenz. Keine Migration, keine Backup-Änderung (reine Enum-Erweiterung).
- **Werkzeug-Evaluierung Clip-Produktion:** Es gibt keinen Zugang zu KI-Videogeneratoren; freie Online-Tools (Pika/Runway o. ä.) brauchen Registrierung, verlangen Upload fremder Assets und liefern i. d. R. Wasserzeichen oder geringe Auflösung. Eine lokale Video-Diffusions-Pipeline (ComfyUI/Stable-Video-Diffusion) ist ohne GPU und Multi-GB-Basismodelle nicht verfügbar. Entscheidung: **prozedurale Generierung** — Python+Pillow rendert Frames (RGBA-rawvideo via stdin) an ffmpeg (libx264/AAC), Sinus-Jingles per `wave`-Modul. Sofort verfügbar, reproduzierbar, versionierbar, Motion-Graphics-Niveau ausreichend für Lückenfüller-Kennzeichen; lokale Diffusion bleibt dokumentierte Zukunftsoption.
- Produziert (Maskottchen „Funki, das Glühbirnchen"): 6 Drehbücher `docs/projects/…/maskottchen/drehbuecher/01-06` + 6 MP4s in `…/maskottchen/clips/` (1280×720, 25 fps, h264+AAC): `funki-begruessung.mp4` (12 s), `funki-programmhinweis.mp4` (15 s), `funki-technische-pause.mp4` (20 s), `funki-senderkennzeichen.mp4` (10 s), `funki-werbepause.mp4` (12 s), `funki-gute-nacht.mp4` (18 s). Generator: `…/maskottchen/generator/generate_clips.py` (`python generate_clips.py all`).
- Tests: +14 (8 Schedule-Expansion, 4 Validierung, 1 Guide-Titel, 1 bUnit) + Erweiterung `Admin_CreateEditDeactivateDeleteRule` (Füller-Regel mit Clip-Quelle im selben App-Lauf). Volle Suite synchron (Debug): **2124 grün + 1 fehlgeschlagen + 1 Alt-Skip** — der Fehlschlag war die eigene E2E-Erweiterung (seed-abhängiger Quellname), gefixt, Einzel-Retry grün. Baseline-Flake `PlaylistSkipLockedEntriesE2ETest` lief in diesem Lauf durch. Debug+Release-Build 0 Fehler, `dotnet format --verify-no-changes --severity error` sauber.
- Doku: `docs/help/tv-sender.md` (Abschnitt „Maskottchen-/Kennzeichen-Clips (Interstitials)" — Quelle anlegen, keine .nfo, für alle teilen, Füller-Regel „Clip-Quelle"), `docs/RELEASE_NOTES.md` (EN+DE), `README.md`, `docs/projects/…/maskottchen/README.md`.
- Hinweis an Abnahme: bewusste Nicht-Umsetzung eines Markers — begründet oben; Clips mit `.nfo` würden versehentlich zu Filmen (in Hilfe dokumentiert).
- Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-18-maskottchen/` (im Abschlusscommit entfernt, Planung in Historie); Tasks-Datei `docs/features/...-schritt-18-maskottchen-tasks.md` bleibt.


### Schritt 18: Maskottchen (N12) — FERTIG (abgenommen)

- Neue Füller-Materialart Clip-Quelle (MediaSourceClips, rotiert uneingeordnete Videos einer Quelle), 6 ffmpeg-produzierte Clips + 6 Drehbuecher + Generator unter docs/projects/…/maskottchen/, Titel-Fallback. Werkzeug: kein KI-Zugang → prozedurale Erzeugung; Evaluierung dokumentiert. Abnahme: `acceptance-schritt-18.md` — erfüllt.


### Schritt 19: Segmenter stderr-Logging + seekfaehiger HTTP-Input (N15) — FERTIG (Implementierung; Abnahme durch anderen Agenten ausstehend)

- **Diagnosability (Prio 1):** `DrainStandardErrorAsync` haelt jetzt einen 8-KB-stderr-Tail (`StreamReader` statt Rohbytes). Endet stderr, klassifiziert `ReportProcessExit`: `_process != process` (Kill via `StopCoreAsync`) oder Laufende innerhalb Toleranz `max(10 s, 2×SegmentSeconds)` → Debug; vorzeitiger Selbstexit → `LogWarning` mit Exit-Code + maskiertem Tail (`MaskStreamTokenInText` + `MaskCredentialsInText`) + `LastUnexpectedExit` (`LiveStreamExitInfo`: SlotId, ExitCode, HttpTransportFailure). Neuer `LogDebug` der maskierten ffmpeg-Argumente in `StartAsync`.
- **HTTP-Input:** `LiveStreamInput.HttpUrl`/`ForHttp` (dritte Eingabeart; `IsPipe` jetzt `OpenStreamAsync is not null`), `BuildArguments` quotet die URL wie Pfade, `-nostdin` + kein Stdin-Redirect — ffmpeg seekt via HTTP-Range, `-ss` wirkt, moov-am-Ende entfaellt. `LiveStreamMediaResolver` (neue Deps: `IInternalMediaStreamTokenService` + `IOptions<LiveStreamOptions>`) liefert fuer Remote-Quellen primaer `ForHttp`, sonst bisheriges `ForStream`; lokale Quellen unveraendert `ForFile`.
- **Token-Sharing/TTL:** Neues `IInternalMediaStreamTokenService.TryCreateStreamUri(mediaItemId, lifetime)` (gemeinsamer privater `TryCreateUri` mit `TryCreateProbeUri`; Probe-Pfad unveraendert an `HttpProbeEnabled` gekoppelt, Stream-Pfad nicht). TTL = `RemainingSeconds` + 15 min (Min. 60 s, Default bei unbekannter Restlaufzeit 45 min). Neue Option `LiveStream:UseHttpInput` (Default `true`, `LiveStreamOptions`, Section `LiveStream`, in `appsettings.json` dokumentiert).
- **Fallback:** `FfprobeMediaDurationProbe`-HTTP-Transportmarker in `FfmpegErrorMarkers` (internal static) extrahiert und geteilt. Nur `HttpTransportFailure` desselben Slots laesst den Coordinator `useHttpInput:false` an den Resolver geben (Pipe-Pfad, mit Info-Log); andere Fehler bleiben auf dem besseren HTTP-Pfad — kein Kaskadieren. `LastUnexpectedExit` wird in `StartAsync` zurueckgesetzt → natuerliches Alternieren HTTP/Pipe.
- **Restart-Analyse (N15 Punkt 3):** Die +30 s-Spruenge sind positionsbasiert korrekt — Restart am naechsten Tick (Poll-Cap 30 s) an `now.PositionSeconds` (Live-Position, kein Doppel-Booking; `_gate` serialisiert, stets ein Prozess). Je Crash-Zyklus gehen ~30–45 s Inhalt verloren. Dokumentiert im Coordinator-Kommentar + `docs/help/tv-sender.md`.
- Tests: +21 (8 Segmenter-Fake inkl. `ControlledPipeStream` mit echtem EOF-Verhalten, 6 Resolver, 4 Coordinator, 4 Token-Dienst — davon 1 Ersetzung). Volle Suite synchron (Debug): **2145 gruen / 0 Fehler / 1 Alt-Skip** (2146 gesamt). Debug+Release-Build 0 Fehler, `dotnet format --verify-no-changes --severity error` sauber.
- Doku: `docs/help/tv-sender.md` (HTTP-Input, `LiveStream:UseHttpInput`, stderr-Diagnose, moov-Hinweis aktualisiert), `docs/RELEASE_NOTES.md` (EN+DE).
- Hinweis an Abnahme: `ResolveAsync` praegt je Tick ein Token auch im „Unchanged"-Fall (gebunden durch Sweep, vernachlaessigbar — bewusst belassen). Echte Fehlerursache des Live-Befunds ist per stderr-Log jetzt sichtbar; vermutete Ursache (SFTP-Pump) entfaellt mit HTTP-Input.
- Lifecycle-Artefakte: `docs/features/task-issue-111-...-schritt-19-stream-input/` (im Abschlusscommit entfernt, Planung in Historie); Tasks-Datei `docs/features/...-schritt-19-stream-input-tasks.md` bleibt.


### Schritt 19: Segmenter-Input (N15) — FERTIG (abgenommen)

- ffmpeg-stderr-Tail (8KB) bei unerwartetem Exit als Warnung (Laufzeit<RemainingSeconds, auch Exit0), HTTP-Range-Input via TryCreateStreamUri (TTL=Slotrest+15min), Fallback auf pipe:0 nur bei HTTP-Transportfehler, +30s-Spruenge als positionsbasierter Resync dokumentiert. Abnahme: `acceptance-schritt-19.md` — erfüllt.
- Nachtrag 2026-10-22: Mit stderr-Logging wurde die echte Ursache gefunden — `-strftime 1` + `%05d` im Segmentnamen kollidiert (%0 ist kein strftime-Konversion, EINVAL beim ersten Segment). Fix: Timestamp in C# formatiert, strftime entfernt (Commit fix: HLS-Segmentdateiname).
