# Bestandsaufnahme: HLS-Segment-Auslieferung mit `FileShare.ReadWrite | FileShare.Delete` öffnen

Bestandsaufnahme der HLS-Auslieferung des automatisierten TV-Senders (`LiveStreamController`, `LiveStreamSegmentStore` und die ffmpeg-Segmentierung) bezogen auf die Anforderung in `requirement.md` (Issue #265, Nachbesserung zu Issue #111/PR #257): Der `.ts`-/Fallback-Zweig in `LiveStreamController.GetFile` soll statt `PhysicalFile` einen eigenen `FileStream` mit `FileShare.ReadWrite | FileShare.Delete` öffnen und als `FileStreamResult` liefern.

## Zusammenfassung

- **`LiveStreamController.GetFile`** (`VideoWebPlayer/Controllers/LiveStreamController.cs`, Z. 75–78) liefert `.ts`-Segmente und alle anderen auflösbaren Dateien aktuell über `PhysicalFile(path, contentType)` aus. `PhysicalFile` öffnet intern mit `FileShare.Read` — ein Löschen/Umbenennen durch `ffmpeg` (HLS-Fenster mit `delete_segments`) oder den Retention-Sweep schlägt unter Windows fehl, solange die Auslieferung läuft.
- **Das Zielmuster existiert bereits im selben Controller:** `RewritePlaylistAsync` (Z. 106–107) öffnet die Playlist mit `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)` — genau das Share-Flag, das die Anforderung für Segmente verlangt.
- **Pfadauflösung und Containment** laufen ausschließlich über `LiveStreamSegmentStore.TryResolveFileAsync` (Whitelist-Regex, Containment-Prüfung, `File.Exists`) — laut Anforderung unverändert.
- **Löschlogik:** `LiveStreamSegmentStore.SweepExpiredSegmentsAsync` (aufgerufen aus `LiveStreamCoordinator.SynchronizeAsync`, Z. 85) und `ClearAsync` (aufgerufen aus `LiveStreamWorker.ExecuteAsync`, Z. 62) löschen Segmente; `ffmpeg` selbst löscht abgelaufene Segmente über `-hls_flags delete_segments` (`FfmpegLiveStreamSegmenter.BuildArguments`, Z. 394). Alle drei Stellen bleiben laut Anforderung unangetastet.
- **Berechtigungen/Statuscodes** sind serverseitig umgesetzt und bleiben bestehen: `CheckLoggedIn()` → 401, Feature-Flag `GetTvSenderEnabledAsync` → 404, `IsTvSenderAccessGrantedAsync` → 403 (`Forbid`), nicht auflösbare Datei → 404, `IOException`/`UnauthorizedAccessException`/sonstige → 500.
- **Tests:** `LiveStreamControllerTests.GetFile_TsSegment_ReturnsVideoMp2t` (Z. 94–104) erwartet aktuell `PhysicalFileResult` und muss auf `FileStreamResult` umgestellt werden; ein Test „Löschen bei offenem Auslieferungs-Stream" existiert noch nicht. Es existieren außerdem HTTP-E2E-Tests (`LiveStreamE2ETests`) und Store-Tests (`LiveStreamSegmentStoreTests`).
- **Datenmodell/Interfaces/Enums/UI:** keine Änderungen erforderlich; beteiligte Datenklassen (`Setup`, `TvSenderUserAccess`) werden nur gelesen.

**Test-Ausgangszustand:** `dotnet build VideoPlayer.sln -c Debug` fehlerfrei (0 Fehler, 142 Warnungen). Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug`: **2248 Tests gesamt, 2247 bestanden, 0 fehlgeschlagen, 1 übersprungen** (explizit deaktivierter, dokumentierter Einzeltest außerhalb des Anforderungsbereichs). Details und Nachweise: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `Setup` (TV-Sender-/Live-Stream-Einstellungen), `TvSenderUserAccess` (nur lesend betroffen)
- [Logik](inventory/logic.md) — `LiveStreamController`, `LiveStreamSegmentStore`, `LiveStreamCoordinator`, `LiveStreamWorker`, `FfmpegLiveStreamSegmenter`, `ApiBaseController`
- [Enums](inventory/enums.md) — `LiveStreamSyncAction`
- [Interfaces](inventory/interfaces.md) — `ILiveStreamSegmenter`, `IAuthService`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Testklassen und Hilfsmethoden
