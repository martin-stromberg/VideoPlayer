# Übersetzte Anforderung: Issue #267 — ffmpeg/ffprobe per Download-Button in den TV-Sender-Einstellungen nachinstallieren

Quelle: GitHub-Issue #267 (Nachbesserung aus Issue #111 / PR #257), `issue.md` im Repository-Root.

## Fachliche Zusammenfassung

Der TV-Sender benötigt die externen Binaries `ffprobe` (`MediaDurationProbeService` /
`FfprobeMediaDurationProbe` für die Laufzeitermittlung) und `ffmpeg`
(`FfmpegLiveStreamSegmenter` für die HLS-Segmentierung des Live-Streams). Beide werden über die
Locator `IFfprobeLocator.Resolve()` bzw. `IFfmpegLocator.Resolve()` gesucht — in der Reihenfolge
konfigurierter Pfad (`MediaProbe:FfprobePath` / `MediaProbe:FfmpegPath`), Konventionsverzeichnis
`tools/ffmpeg/` unterhalb `AppContext.BaseDirectory`, Anwendungsverzeichnis, `PATH`. Fehlen die
Binaries (z. B. Produktivsysteme ohne Paketverwaltung wie Synology), wartet die
Laufzeitermittlung und der `LiveStreamCoordinator` meldet `LiveStreamSyncAction.FfmpegUnavailable`
— sichtbar ist das heute nur im Server-Log bzw. indirekt über den Stau-Hinweis
(`ProbeStatusLabel`: „ffprobe verfügbar?") im Tab „Laufzeitermittlung".

Die Anforderung erweitert die TV-Sender-Verwaltung (`/admin/tv-sender`,
`TVSenderAdmin.razor`) um eine serverseitig erzwungene Admin-Aktion „ffmpeg/ffprobe
herunterladen": Ein Download-Dienst lädt das zur Laufzeitplattform passende statische
ffmpeg-Paket (mit `ffmpeg` + `ffprobe`), entpackt die Binaries nach `tools/ffmpeg/` neben der
Anwendung — dem Konventionsverzeichnis beider Locators, das laut Anforderung das Update-Overlay
übersteht — und setzt unter Linux das Ausführungsbit. Die „Binary fehlt"-Hinweise (Probe-Warnung
im Tab „Laufzeitermittlung" und Live-Stream-Pausier-Hinweis) erhalten den Download-Button direkt
daneben; der Download zeigt Fortschritt und Fehler an und blendet einen GPL-/Lizenzhinweis ein.
Die bestehende Suchreihenfolge (Config-Feld, `tools/ffmpeg/`, `PATH`) bleibt unverändert.

## Betroffene Klassen und Komponenten

- **Neuer Download-/Installationsdienst** (Arbeitsname, Annahme: `FfmpegToolDownloadService`
  mit Interface `IFfmpegToolDownloadService`, z. B. unter `VideoWebPlayer/Services/`): lädt das
  Plattform-Paket per `HttpClient`, entpackt `ffmpeg`/`ffprobe` nach
  `{AppContext.BaseDirectory}/tools/ffmpeg/` und setzt auf Unix das Executable-Bit
  (`File.SetUnixFileMode`). Job-/Snapshot-Muster analog `ManualBackupJobService` +
  `ManualBackupJobSnapshot`/`ManualBackupJobStatus` (Services/Backups): ein Singleton hält den
  Fortschrittszustand (`Idle`/`Running`/`Succeeded`/`Failed`, Bytes/Prozent, Fehlermeldung), den
  die UI pollt. Admin-Prüfung serverseitig nach dem Muster `MediaDurationProbeService.RequireAdminAsync`.
- **`FfprobeLocator` / `FfmpegLocator`** (`Services/`): bleiben unverändert — `tools/ffmpeg/`
  ist bereits Konventionskandidat; `Resolve()` wird für die „fehlt"-Erkennung in der UI
  wiederverwendet.
- **`TVSenderAdmin.razor`** (`Components/Pages/Admin/`): neue Hinweis-/Aktionsflächen —
  (a) Tab „Laufzeitermittlung": wenn `IFfprobeLocator.Resolve()` `null` liefert, Warnung mit
  Download-Button direkt daneben (heute nur indirekter Text „ffprobe verfügbar?"),
  (b) Tab „Einstellungen" (bzw. dort, wo der Live-Stream-Pausier-Zustand angezeigt wird):
  entsprechend für `IFfmpegLocator`. Fortschrittsanzeige während des Downloads, Fehleranzeige,
  GPL-Hinweis vor/beim Download-Start.
- **`MediaDurationProbeProgress`** (optional): könnte ein Verfügbarkeits-Flag aufnehmen; einfacher
  ist die direkte Locator-Abfrage in der Komponente (beide Locators sind Singletons mit billigem
  `File.Exists`-Check).
- **`MediaProbeOptions`** / neuer Konfigurationsabschnitt: optionale Überschreibung der
  Download-Quelle(n) — siehe Abschnitt „Konfiguration".
- **Tests**: neue `FfmpegToolDownloadServiceTests` (Fake-`HttpMessageHandler`, Entpackung,
  Zielpfad, Executable-Bit, Fehlerpfade) und Erweiterung von `TVSenderAdminTests`
  (Hinweis + Button sichtbar nur bei fehlendem Binary; Admin-only) ggf. `TVSenderAdminE2ETests`.
  Locator-Regression: nach „Installation" findet `Resolve()` die Binaries in `tools/ffmpeg/`.
- **Doku**: `docs/help/videolaufzeit.md` (Abschnitt „ffprobe einrichten"), `docs/help/tv-sender.md`
  (Abschnitt „ffmpeg-Bereitstellung und Lizenz"), ggf. `README.md`/`docs/RELEASE_NOTES.md`.

## Implementierungsansatz

- **Plattformwahl zur Laufzeit**: `OperatingSystem.IsWindows()` /
  `RuntimeInformation.OSArchitecture` bzw. RID bestimmen das Paket — die Anwendung wird als
  self-contained `win-x64`- und `linux-x64`-Release gebaut (`release.config.js`,
  `release-win-x64.zip`/`release-linux-x64.zip`); Synology-Zielsysteme sind primär Linux/x64.
- **Download mit Fortschritt**: `HttpClient` mit `HttpCompletionOption.ResponseHeadersRead`,
  gestreamtes Schreiben in eine temporäre Datei und Zwischenstände im Job-Snapshot; die
  Komponente pollt den Snapshot (Muster `PollBackupStatusAsync` in `Backups.razor`, bzw. der
  bestehende 15-s-`PeriodicTimer` in `TVSenderAdmin`).
- **Entpacken**: Windows-Builds liegen als `.zip` vor (`System.IO.Compression` vorhanden);
  verbreitete Linux-Static-Builds (johnvansickle, BtbN) als `.tar.xz` — xz-Dekompression ist im
  BCL **nicht** enthalten (nur `System.Formats.Tar` für das Tar-Archiv), d. h. entweder eine
  zip-basierte Quelle wählen oder eine zusätzliche NuGet-Abhängigkeit für xz/7z (Lizenz- und
  Release-Build-Prüfung laut Projektregeln nötig). Detailentscheidung in der Planung.
- **Ablage**: `{AppContext.BaseDirectory}/tools/ffmpeg/{ffmpeg,ffprobe}[.exe]` — identisch mit
  den `ConventionCandidates()` beider Locators; danach greift `Resolve()` ohne Neustart bei
  jedem neuen Aufruf (Locators cachen nichts). Annahme laut Kunde: das Update-Overlay des
  Updaters (`msTools.Updater`) fügt nur hinzu und lässt `tools/ffmpeg/` bei Updates bestehen.
- **UI-Einbindung**: beide „fehlt"-Hinweise rendern denselben Download-Button; ein laufender
  Download sperrt weitere Starts (ein Job-Prozess); nach `Succeeded` verschwinden die Hinweise
  und die wartenden Worker (`MediaDurationProbeWorker`, `LiveStreamWorker`) laufen im nächsten
  Takt automatisch an — kein Neustart nötig.
- **GPL-Hinweis**: vor Auslösung des Downloads einblendbarer Hinweis (Dialog oder Inline-Hinweis),
  dass die üblichen statischen Builds mit `--enable-gpl` gebaut sind und die Lizenzbedingungen
  der jeweiligen FFmpeg-Distribution gelten (Text konsistent zu `docs/help/tv-sender.md` §
  „ffmpeg-Bereitstellung und Lizenz").
- **Berechtigung**: die Aktion ist Admin-only und muss serverseitig erzwungen werden — in der
  Blazor-Server-Komponente über die bestehende `isAdmin`-Prüfung plus einer Prüfung im Dienst
  selbst (Muster `RequireAdminAsync(userId)`).

## Konfiguration

Vorschlag: neuer Schlüssel unter dem bestehenden Abschnitt `MediaProbe` in `appsettings.json`,
z. B. `MediaProbe:DownloadSources` (Mapping Plattform → Paket-URL, optional überschreibbar für
abgeschottete Umgebungen) mit eingebauten Defaults je Plattform. Die konkrete Bezugsquelle
(gyan.dev, johnvansickle, ffbinaries u. ä.) ist noch festzulegen — siehe Offene Fragen. Kein
neuer `Setup`-Speicher nötig: der Installationszustand ergibt sich aus dem Dateisystem
(`File.Exists` über die Locators), der Download-Status ist flüchtig im Job-Singleton.

## Akzeptanzkriterien

1. Auf einem System ohne ffmpeg/ffprobe zeigt `/admin/tv-sender` einen „ffprobe fehlt"-Hinweis
   im Tab „Laufzeitermittlung" und einen „ffmpeg fehlt / Live-Stream pausiert"-Hinweis im
   Einstellungsbereich — jeweils mit Download-Button direkt daneben.
2. Der Download lädt das zur Plattform passende statische Paket, zeigt Fortschritt (Prozent/Bytes)
   und Fehler verständlich an, legt `ffmpeg` und `ffprobe` unter `tools/ffmpeg/` ab und setzt
   unter Linux das Ausführungsbit.
3. Nach erfolgreichem Download finden `FfprobeLocator.Resolve()` und `FfmpegLocator.Resolve()`
   die Binaries ohne Neustart; Laufzeitermittlung und Live-Stream-Segmentierung laufen im
   nächsten Worker-Takt an.
4. Vor dem Download wird ein GPL-/Lizenzhinweis eingeblendet.
5. Die Alternativpfade `MediaProbe:FfprobePath`/`MediaProbe:FfmpegPath`, `tools/ffmpeg/` und
   `PATH` bleiben funktional unverändert; ein gesetzter Config-Pfad wird nicht überschrieben.
6. Die Download-Aktion ist nur für Administratoren erreichbar und serverseitig abgesichert;
   parallele Downloads werden verhindert.
7. Debug- und Release-Build fehlerfrei, volle Testsuite grün; Hilfe-Doku (`videolaufzeit.md`,
   `tv-sender.md`), `README.md` und Release Notes aktualisiert.

## Offene Fragen

- **Bezugsquelle je Plattform**: Welcher Anbieter ist freigegeben (gyan.dev für Windows; für
  Linux üblicherweise johnvansickle `tar.xz` oder zip-basierte Quellen wie ffbinaries)?
  Versionen pinnen oder „latest"? Integritätsprüfung (Checksumme/Signatur) gewünscht?
- **Paketformat Linux**: Die verbreiteten statischen Linux-Builds sind `.tar.xz`; xz-Extraktion
  fehlt im BCL — zip-Quelle bevorzugen oder neue NuGet-Abhängigkeit (z. B. SharpCompress;
  Lizenz-/CI-Prüfung erforderlich) in Kauf nehmen?
- **Architekturabdeckung**: Nur `x64` (entsprechend den Release-RIDs `win-x64`/`linux-x64`) oder
  auch `arm64` (ARM-Synology, Raspberry Pi)? macOS überhaupt relevant (Developer-Only)?
- **Umfang eines Downloads**: Ein Paket mit beiden Binaries pro Klick (übliche Builds enthalten
  beides) oder getrennte Downloads für `ffmpeg` und `ffprobe` — auch bei unterschiedlichem
  Fehl-Zustand (nur eins fehlt)?
- **Lizenzwahl des Builds**: GPL-Build (mit libx264 etc.) oder bewusst LGPL-/GPL-freier Build —
  welche Build-Variante wird vorgehalten, und genügt der GPL-Hinweis vor dem Download?
- **Platzierung des Live-Stream-Hinweises**: Der Pausier-Zustand existiert heute nur im Log
  (`LiveStreamWorker`, gedrosselte Warnung) — soll ein neuer UI-Hinweis in den Tab
  „Einstellungen" (Segmentierungs-Karte) oder zusätzlich in die Admin-Ansicht des Sendeplans?
- **Update-Bestand**: Ist die Annahme verifiziert, dass das Update-Overlay `tools/ffmpeg/` bei
  Updates bestehen lässt (Verhalten von `msTools.Updater` 0.11.0-rc.4)?
