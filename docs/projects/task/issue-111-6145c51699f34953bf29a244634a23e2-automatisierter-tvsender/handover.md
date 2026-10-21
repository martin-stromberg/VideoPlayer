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
| 7 | Devin | task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-7-live-tv-ui | aktiv | 2026-10-21 |

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
