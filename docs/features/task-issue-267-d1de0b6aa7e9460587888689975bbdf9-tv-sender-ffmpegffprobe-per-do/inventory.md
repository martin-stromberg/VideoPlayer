# Bestandsaufnahme: Issue #267 — ffmpeg/ffprobe per Download-Button in den TV-Sender-Einstellungen nachinstallieren

Die Anforderung ergänzt `/admin/tv-sender` um einen Admin-geschützten Download, der statische ffmpeg/ffprobe-Binaries nach `tools/ffmpeg/` neben der Anwendung legt. Die Bestandsaufnahme zeigt: Die Ablage- und „fehlt"-Erkennung ist bereits vollständig vorhanden (Locators + Konventionsverzeichnis + `FfmpegUnavailable`-Zustand); was komplett fehlt, ist jegliche Download-/Installations-Infrastruktur, die UI-Hinweise mit Button und die Fortschritts-/GPL-Anzeige.

## Zusammenfassung

- **Vorhanden — Locators & Konvention:** `FfprobeLocator`/`FfmpegLocator` (`VideoWebPlayer/Services/`) suchen in exakt der Reihenfolge Config-Pfad → `tools/ffmpeg/`/`AppDir` → `PATH`, cachen nichts und greifen nach einer Nachinstallation ohne Neustart. Beide sind Singletons mit testbarem internen Ctor.
- **Vorhanden — „fehlt"-Erkennung:** `MediaDurationProbeService.ProbePendingAsync` bricht bei fehlendem ffprobe mit einmaliger Warnung ab; `LiveStreamCoordinator` liefert `LiveStreamSyncAction.FfmpegUnavailable`, das `LiveStreamWorker` 5-minütig gedrosselt ins Log schreibt. In der UI sichtbar ist der Zustand heute nur indirekt: `ProbeStatusLabel` im Tab „Laufzeitermittlung" („ffprobe verfügbar?", `TVSenderAdmin.razor:861`); ein Live-Stream-Pausier-Hinweis existiert **nur im Log**, nicht in der UI.
- **Fehlt komplett:** kein Download-/Installationsdienst, kein `IFfmpegToolDownloadService`, kein Job-Snapshot für Downloads, keine Download-Fortschrittsanzeige (auch `msTools.Updater`-Downloads zeigen nur den Status-Chip „Download", keine Bytes/Prozent), kein GPL-Hinweis, kein `MediaProbe:*`-Schlüssel für Download-Quellen, keine Locator-Injektion in `TVSenderAdmin.razor`.
- **Muster vorhanden:** Job-/Snapshot-Singleton `ManualBackupJobService` + `ManualBackupJobSnapshot`/`ManualBackupJobStatus` (Enum-Werte per `EnumContractTests` gepinnt); UI-Polling via `PeriodicTimer` in `Backups.razor` (`PollBackupStatusAsync`, 3 s) und `TVSenderAdmin.razor` (`ProbeProgressLoopAsync`, 15 s); Admin-Erzwingung doppelt: `RequireAdminAsync` im Service (wirft `ForbiddenAccessException`) + Controller-Pattern `[Authorize(Policy = "AdminOnly")]` (`BackupsController`, Route `admin/backups/api/create` — so wird der Backup-Job gestartet); Bestätigungsdialog-Muster `admin-dialog-overlay`/`admin-dialog` für den GPL-Hinweis; Fortschrittsbalken-CSS `.progress`/`.progress-bar` bereits im Einsatz.
- **HTTP/Entpacken:** Einziger HttpClient ist der benannte `"Internal"`-Client (Self-Calls mit API-Key/User-Agent) — für externe Downloads ungeeignet als solcher, `IHttpClientFactory` steht aber bereit. `System.IO.Compression` wird nur von den Backup-Klassen (msTools.Backup-Format) genutzt; tar/xz-Entpackung existiert nicht im Code, keine passende NuGet-Abhängigkeit.
- **Update-Bestand:** `AutoUpdate:ProtectedFiles` in `appsettings.json` schützt `web.config` + `appsettings*.json` (Merge). `docs/TECH_Auto_Update.md` dokumentiert, dass deployment-seitig angelegte Dateien außerhalb des Pakets Updates überstehen — `tools/ffmpeg/` fällt darunter, steht aber nicht explizit auf der Schutzliste.
- **Tests:** Debug-Build fehlerfrei (142 Warnungen). Volle Suite: **2248 Tests — 2245 bestanden, 2 fehlgeschlagen, 1 übersprungen**; beide Fehlschläge (`VideoWebPlayerClientTests_ProgramGuide.PairedDevice_RequestProgramGuideUpcoming_ReturnsBoundedEntries`, `ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext`, je 404 auf program-guide) sind im isolierten Wiederholungslauf bestanden → preexisting Flakes unter Parallel-Last. Details: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell & Optionsklassen](inventory/models.md)
- [Logikklassen & Komponenten](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests & Test-Ausgangszustand](inventory/tests.md)
