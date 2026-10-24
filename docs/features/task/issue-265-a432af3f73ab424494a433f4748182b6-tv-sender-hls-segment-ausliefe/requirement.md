# Übersetzte Anforderung: HLS-Segment-Auslieferung mit `FileShare.ReadWrite | FileShare.Delete` öffnen

Quelle: Issue #265 — Nachbesserung aus Issue #111 (PR #257), betreffend `LiveStreamController.GetFile`.

## Fachliche Zusammenfassung

Die `.ts`-Segment-Auslieferung des TV-Sender-Live-Streams in `LiveStreamController.GetFile` erfolgt derzeit über `PhysicalFile(path, contentType)` (`VideoWebPlayer/Controllers/LiveStreamController.cs`, Z. 75–78). `PhysicalFile` öffnet die Datei intern über `File.OpenRead`, also mit `FileShare.Read`: Andere Prozesse dürfen mitlesen, aber nicht löschen oder umbenennen. Da `ffmpeg` das Segment-Verzeichnis als Rolling Window pflegt und abgelaufene Segmente löscht, schlägt unter Windows der Delete fehl, solange ein Client das Segment noch lädt — im ungünstigen Fall bricht der Muxer ab. Die Auslieferung soll analog zur bereits korrekt gelösten Playlist-Behandlung in `RewritePlaylistAsync` (Z. 106–107) die Datei selbst mit `FileShare.ReadWrite | FileShare.Delete` öffnen und als `FileStreamResult` zurückgeben.

## Akzeptanzkriterien

1. **Löschbares Segment während der Auslieferung:** `ffmpeg` bzw. `LiveStreamSegmentStore.SweepExpiredSegmentsAsync`/`ClearAsync` können ein `.ts`-Segment löschen, während ein Client es noch lädt — unter Windows ohne `IOException` durch Sharing-Verletzung, ohne verzögerte Warnung oder Muxer-Abbruch im `ffmpeg`-Log.
2. **Unverändertes Auslieferungsverhalten:** Content-Type (`video/mp2t` für `.ts`, `application/octet-stream` sonst), Statuscodes (401/403/404) und der vollständige Dateiinhalt bleiben wie bisher; die Auslieferung läuft zu Ende, auch wenn die Datei mittendrin gelöscht wurde.
3. **Nachweis per Test:** Ein Test belegt, dass ein Segment während eines noch offenen Auslieferungs-Streams gelöscht werden kann und die Auslieferung den vollständigen Inhalt liefert (unter Windows würde der Test ohne `FileShare.Delete` fehlschlagen; unter Linux muss die Share-Flag-Belegung anderweitig sichergestellt werden, z. B. über den Typ des Ergebnisses bzw. des Streams).

## Betroffene Klassen und Komponenten

- **Bestehend (Änderung):** `LiveStreamController.GetFile` (`VideoWebPlayer/Controllers/LiveStreamController.cs`, Z. 75–78) — `.ts`-/Fallback-Zweig: `PhysicalFile` durch eigenen `FileStream` + `FileStreamResult` ersetzen.
- **Bestehend (Muster, unverändert):** `LiveStreamController.RewritePlaylistAsync` (Z. 100–111) — Vorbild für den `FileStream` mit `FileShare.ReadWrite | FileShare.Delete`.
- **Bestehend (unverändert):** `LiveStreamSegmentStore` (`TryResolveFileAsync`, `SweepExpiredSegmentsAsync`, `ClearAsync`) — Pfadauflösung und Löschlogik bleiben unangetastet; `TryResolveFileAsync` bleibt die einzige Stelle, die den Pfad liefert (Containment-/Whitelist-Prüfung).
- **Tests:** `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs` — `GetFile_TsSegment_ReturnsVideoMp2t` erwartet aktuell `PhysicalFileResult` und muss auf `FileStreamResult` umgestellt werden; neuer Test für „Delete während offenem Stream".
- Keine Änderungen an Datenmodell, Interfaces, Enums oder UI-Komponenten.

## Implementierungsansatz

- In `GetFile` im `.ts`-/Fallback-Zweig (nach erfolgreicher `TryResolveFileAsync`-Auflösung) einen `FileStream` mit `FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete` öffnen (möglichst mit asynchronem Flag/`FileOptions.Asynchronous` und üblicher Puffergröße, analog gängiger Streaming-Praxis) und per `FileStreamResult` (bzw. `File(stream, contentType)`) zurückgeben — der ASP.NET-Core-Executor schließt den Stream nach der Auslieferung.
- Ergebnistyp-Parität beachten: `PhysicalFile` wurde ohne Range-Verarbeitung aufgerufen; `FileStreamResult` bietet sie über `EnableRangeProcessing` optional an — Standard (keine explizite Aktivierung) entspricht dem bisherigen Verhalten, ein explizites Aktivieren ist nicht Teil der Anforderung (Annahme, ggf. als sinnvolle Erweiterung zu prüfen, da Player Range-Requests nutzen können).
- Fehlerfälle bleiben in den bestehenden `catch`-Blöcken abgedeckt: Ein `IOException`/`UnauthorizedAccessException` beim Öffnen des Streams läuft in den vorhandenen 500-Handler; das vorherige 404-Verhalten für nicht auflösbare Dateien bleibt durch `TryResolveFileAsync` unverändert.
- Hinweis für den Test: Da das Share-Verhalten plattformabhängig sichtbar ist (Linux erlaubt `unlink` trotz Handle ohne `FileShare.Delete`), sollte der Test neben dem Löschversuch zusätzlich den Ergebnistyp (`FileStreamResult` statt `PhysicalFileResult`) absichern, damit die Korrektur nicht unter Linux unbemerkt zurückgebaut werden kann.

## Konfiguration

Keine — das Verhalten ist eine Fehlerkorrektur ohne Konfigurationsbedarf.

## Offene Fragen

- Soll die Gelegenheit genutzt werden, Range-Verarbeitung für `.ts`-Segmente zu aktivieren (`EnableRangeProcessing`)? Die Anforderung verlangt nur die Share-Flag-Korrektur; Range-Support wäre eine inhaltlich naheliegende, aber separate Erweiterung (Annahme: nicht Teil dieses Schritts, sofern nicht anders entschieden).
