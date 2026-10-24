# Umsetzungsplan: HLS-Segment-Auslieferung mit `FileShare.ReadWrite | FileShare.Delete` öffnen

## Übersicht

Kleiner Bugfix im `.ts`-/Fallback-Zweig von `LiveStreamController.GetFile` (`VideoWebPlayer/Controllers/LiveStreamController.cs`, Z. 75–78): `PhysicalFile(path, contentType)` öffnet intern mit `FileShare.Read` und verhindert so unter Windows das Löschen abgelaufener HLS-Segmente durch `ffmpeg` bzw. den Retention-Sweep, solange ein Client das Segment noch lädt. Die Auslieferung wird auf einen selbst geöffneten `FileStream` mit `FileShare.ReadWrite | FileShare.Delete` umgestellt, der als `FileStreamResult` zurückgegeben wird — exakt das Muster, das `RewritePlaylistAsync` im selben Controller (Z. 106–107) bereits vormacht. Betroffen sind nur der Controller und die zugehörigen Controller-Tests; kein Datenmodell, kein Interface, keine UI.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Auslieferungsmechanismus `.ts`-/Fallback-Zweig | Eigener `FileStream` + Rückgabe via `File(stream, contentType)` (`FileStreamResult`) | `PhysicalFileResult` öffnet die Datei zwingend über `File.OpenRead` (`FileShare.Read`) und bietet keinen Share-Parameter — die geforderten Flags `FileShare.ReadWrite \| FileShare.Delete` sind nur über einen selbst geöffneten Stream erreichbar. Vorbild `RewritePlaylistAsync` im selben Controller (Transaction-Script-Ansatz der Action bleibt unverändert). |
| Range-Verarbeitung (`EnableRangeProcessing`) | Nicht aktivieren | `PhysicalFile` wurde bisher ohne Range-Verarbeitung aufgerufen; der Default von `FileStreamResult` (keine explizite Aktivierung) entspricht also exakt dem bisherigen Verhalten. Die Anforderung begrenzt den Schritt ausdrücklich auf die Share-Flag-Korrektur — Range-Support ist eine separate, nicht angeforderte Erweiterung. |
| Stream-Öffnungsoptionen | `FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite \| FileShare.Delete`, Standardpuffer, `FileOptions.Asynchronous` | Der `FileStreamResult`-Executor kopiert asynchron; ein asynchrones Handle ist die empfohlene Praxis für Server-Streaming und ändert am Laufzeitverhalten nichts Fachliches. |
| `Last-Modified`-Header | Bewusst entfallen lassen — einfache `File(stream, contentType)`-Überladung ohne `lastModified`/`entityTag` | `PhysicalFileResult` sendet `Last-Modified` immer (Executor belegt ihn aus `FileInfo`); `FileStreamResult` ohne Argumente sendet weder `Last-Modified` noch `ETag`. Für HLS-Segmente fachlich unkritisch: Die Segmentnamen sind eindeutig und zeitgestempelt (`seg_{yyyyMMddHHmmss}_{SlotId}_%05d.ts`, siehe `FfmpegLiveStreamSegmenter.BuildArguments`), der Inhalt ändert sich nie, ein Conditional GET (304) ist für Segmente nicht vorgesehen. Die Alternative `File(stream, contentType, lastModified, entityTag)` erforderte ein zusätzliches Metadaten-Read (`File.GetLastWriteTime*`) mit weiterem TOCTOU-Fenster — ohne erkennbaren fachlichen Nutzen. |
| Ort des Öffnens | Innerhalb des bestehenden `try`-Blocks, direkt nach erfolgreicher `TryResolveFileAsync`-Auflösung | Öffnungsfehler (`FileNotFoundException`/`IOException`/`UnauthorizedAccessException`, z. B. aus dem TOCTOU-Fenster zwischen `File.Exists` und Öffnen) laufen dann in die bestehenden `catch`-Blöcke und liefern geloggte 500-Antworten, statt als Executor-Ausnahme an die Middleware zu entkommen. |

## Programmabläufe

### Segment-/Fallback-Auslieferung in `GetFile` (geänderter Zweig)

1. `CheckLoggedIn()` — ohne angemeldeten Benutzer 401 (bestehend).
2. `_settings.GetTvSenderEnabledAsync` — deaktiviertes Feature-Flag → 404 (bestehend).
3. `_settings.IsTvSenderAccessGrantedAsync` — ohne Freigabe → `Forbid` (403) (bestehend).
4. `_segmentStore.TryResolveFileAsync(fileName, ct)` — Whitelist-/Containment-Prüfung und `File.Exists`; `null` → 404 (bestehend).
5. `.m3u8`-Zweig: `CacheControl: no-store`, `RewritePlaylistAsync`, `ContentResult` — **unverändert**.
6. `.ts`-/Fallback-Zweig (geändert): `contentType` bestimmen (`video/mp2t` für `*.ts` OrdinalIgnoreCase, sonst `application/octet-stream` — unverändert); `FileStream` mit `FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete` und `FileOptions.Asynchronous` öffnen; `File(stream, contentType)` (`FileStreamResult`) zurückgeben. Der ASP.NET-Core-Executor streamt den Inhalt und verwirft den Stream nach der Auslieferung — auch dann, wenn die Datei mittendrin gelöscht wurde.
7. Öffnungsfehler (z. B. zwischen `TryResolveFileAsync` und Öffnen gelöscht) → vorhandene `catch`-Blöcke → geloggtes 500.

Beteiligte Klassen/Komponenten: `LiveStreamController`, `LiveStreamSegmentStore`, `FileStreamResult`/`FileStreamResultExecutor` (ASP.NET Core)

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `LiveStreamController` (Controller, `VideoWebPlayer/Controllers/LiveStreamController.cs`)

- **Geänderte Methoden:** `GetFile` — der `.ts`-/Fallback-Zweig (Z. 75–78) ersetzt `return PhysicalFile(path, contentType)` durch: eigenen `FileStream` mit `FileShare.ReadWrite | FileShare.Delete` (+ `FileOptions.Asynchronous`) öffnen und `File(stream, contentType)` zurückgeben. Dazu ein kurzer Kommentar im Stil des Playlist-Kommentars, der die Begründung festhält (ffmpeg/`SweepExpiredSegmentsAsync` löschen Segmente aus dem Rolling Window, während die Auslieferung läuft).
- **Unverändert:** `RewritePlaylistAsync` (Vorbild), alle Statuscode-/Berechtigungsprüfungen, Content-Type-Logik, Fehlerfänge.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — die bestehende Whitelist-/Containment-Prüfung in `LiveStreamSegmentStore.TryResolveFileAsync` bleibt die einzige Pfadquelle und wird nicht angefasst.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Fehlerpfad beim Öffnen:** Ein Öffnungsfehler (TOCTOU-Fenster zwischen `File.Exists` in `TryResolveFileAsync` und dem eigentlichen Öffnen) tritt künftig bereits in der Action auf und läuft durch die bestehenden `catch`-Blöcke → 500 mit Controller-Logging. Bisher öffnete der `PhysicalFileResult`-Executor die Datei erst nach Verlassen der Action; ein Fehler dort entkam dem Controller-`try`. Der HTTP-Statuscode (500) bleibt identisch, die Protokollierung wird konsistenter — beabsichtigte Nebenwirkung.
- **Fallback-Zweig (nicht-`.ts`-Dateien, `application/octet-stream`):** Erhält ebenfalls `FileShare.ReadWrite | FileShare.Delete`. Beabsichtigt und ungefährlich — die Dateien liegen im selben Segmentverzeichnis unter derselben Löschlogik.
- **Antwort-Header:** `FileStreamResult` leitet `Content-Length` aus dem seekbaren Stream ab; `EnableRangeProcessing` bleibt aus. Content-Type, Statuscodes und `Accept-Ranges`-Verhalten bleiben damit gleich. **Bewusste Änderung:** `PhysicalFileResult` sendet stets `Last-Modified` (der Executor belegt ihn aus `FileInfo`); `FileStreamResult` sendet weder `Last-Modified` noch `ETag`. Der Header entfällt also ab sofort — als Designentscheidung hingenommen (siehe oben), weil HLS-Segmente eindeutige, zeitgestempelte Namen tragen und kein Conditional-GET-Bedarf besteht.
- **Auslieferungspfad (Performance):** `PhysicalFileResult` nutzt `response.SendFileAsync` (TransmitFile-/sendfile-Pfad, Kernel-Copy); `FileStreamResult` kopiert den Inhalt durch den Managed-Stream. Für die kleinen `.ts`-Segmente praktisch irrelevant, der Vollständigkeit halber vermerkt.
- **Stream-Lebensdauer in Tests:** Der Executor verwirft den Stream erst beim Ausführen des Result. Controller-Unit-Tests, die das Result nur inspizieren, müssen `FileStreamResult.FileStream` selbst schließen (`await using`/Dispose), sonst bleibt das Dateihandle offen und `DisposeAsync` der Testklasse kann das Temp-Verzeichnis unter Windows nicht löschen.
- **Kein Effekt** auf `.m3u8`-Zweig, `LiveStreamSegmentStore`, `LiveStreamCoordinator`, `LiveStreamWorker`, `FfmpegLiveStreamSegmenter`.

## Umsetzungsreihenfolge

1. **`GetFile` `.ts`-/Fallback-Zweig umstellen**
   - Voraussetzungen: Keine
   - Beschreibung: In `VideoWebPlayer/Controllers/LiveStreamController.cs` Z. 75–78 `PhysicalFile` durch eigenen `FileStream` (`FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete`, `FileOptions.Asynchronous`) + `File(stream, contentType)` ersetzen; kurzen Begründungskommentar analog Z. 103–105 ergänzen.

2. **Bestehenden Test `GetFile_TsSegment_ReturnsVideoMp2t` umstellen**
   - Voraussetzungen: Schritt 1
   - Beschreibung: In `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs` (Z. 94–104) `Assert.IsType<PhysicalFileResult>` durch `Assert.IsType<FileStreamResult>` ersetzen, Content-Type-Prüfung beibehalten, Stream am Ende des Tests schließen.

3. **Neuen Regressionstest „Delete während offenem Auslieferungs-Stream" anlegen**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Neuen Test in `LiveStreamControllerTests` (siehe Abschnitt „Tests"), der den Stream des `FileStreamResult` offen hält, während (b) eine zweite Handle-Öffnung mit `FileAccess.ReadWrite`/`FileShare.Read` versucht wird (ReadWrite-Share-Nachweis, Windows-Diskriminator) und (c) die Segmentdatei gelöscht wird (Delete-Share-Nachweis, Windows-Diskriminator), und danach den vollständigen Inhalt verifiziert; die Plattform-Einordnung (unter Linux trägt allein die Typ-Assertion) wird im Test kommentiert.

4. **Build und volle Testsuite**
   - Voraussetzungen: Schritte 1–3
   - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei; `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig grün (Baseline: 2248 Tests, 2247 bestanden, 1 dokumentiert übersprungen — laut `inventory/tests.md`).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen` (Arbeitstitel) | `LiveStreamControllerTests` (`VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs`) | Legt `seg_001.ts` mit bekanntem Inhalt im Temp-Segmentverzeichnis an, ruft `GetFile("seg_001.ts")` auf und hält den zurückgegebenen Stream offen. Prüft: (a) Ergebnistyp ist `FileStreamResult` und `FileStream` ist ein `System.IO.FileStream` — plattformunabhängige Absicherung gegen einen Rückbau auf `PhysicalFile` (Linux würde `unlink` auch ohne `FileShare.Delete` zulassen); (b) **Windows-Diskriminator ReadWrite-Share:** ein zweites Handle auf dieselbe Datei mit `FileAccess.ReadWrite`, `FileShare.Read` (alternativ `FileShare.ReadWrite`) lässt sich öffnen, solange der Auslieferungs-Stream offen ist — unter Windows scheitert dieser Zweitgriff mit einem `FileShare.Read`-Auslieferungs-Handle (alter Codepfad) und gelingt nur bei `FileShare.ReadWrite`-Share (empirisch verifiziert); (c) **Windows-Diskriminator Delete-Share:** `File.Delete` auf die Segmentdatei wirft keine `IOException`, solange der Auslieferungs-Stream offen ist — unter Windows schlägt dieser Schritt ohne `FileShare.Delete` fehl (Fehlverhalten-Reproduktion, empirisch verifiziert); (d) der Auslieferungs-Stream liefert nach dem Löschen noch den vollständigen Inhalt (Byte-Vergleich). Stream am Ende schließen, damit `DisposeAsync` das Temp-Verzeichnis löschen kann. **Plattform-Einordnung (ehrlich im Test dokumentieren):** .NET emuliert `FileShare` unter Unix nur binär (`FileShare.None` → `flock` `LOCK_EX`, jede andere Belegung → `LOCK_SH`), und `FileShare.Delete` ist unter Linux gar nicht beobachtbar (`File.Delete` ist ein blankes `unlink`, das keine Locks konsultiert) — die Schritte (b) und (c) sind reine Windows-Diskriminatoren und belegen unter Linux nichts. Dort trägt den Share-Flag-Nachweis ausschließlich die Typ-Assertion (a); das genügt der Anforderung („z. B. über den Typ des Ergebnisses bzw. des Streams"). Wichtig: Ein Zweit-Handle mit `FileShare.None` ist **kein** geeigneter positiver Diskriminator — es scheitert auf beiden Plattformen auch bei korrekter Implementierung (Windows: symmetrischer Share-Check → Sharing Violation; Linux: `LOCK_EX` gegen das gehaltene `LOCK_SH` → `EWOULDBLOCK`) und würde den Test bei richtigem Code rot halten. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `LiveStreamControllerTests.GetFile_TsSegment_ReturnsVideoMp2t` (Z. 94–104) | Erwartet `PhysicalFileResult`; nach der Umstellung liefert `GetFile` ein `FileStreamResult`. Typ-Assertion und Stream-Disposal anpassen, Content-Type-Prüfung (`video/mp2t`) beibehalten. |

### E2E-Tests (primärer Funktionsnachweis)

**Keine neuen E2E-Tests erforderlich.** Begründung:

- Die Anforderung berührt keinen Benutzerfluss: Es ändert sich kein Endpunkt, kein Statuscode, kein Header, kein Inhalt — nur der `FileShare`-Modus des serverseitigen Datei-Handles während einer ohnehin bestehenden Auslieferung. Von außen ist der Happy Path exakt derselbe.
- Die geänderte Eigenschaft (Share-Flags des Auslieferungs-Handles) ist über HTTP prinzipiell nicht beobachtbar. Ein HTTP-E2E-Test könnte lediglich „Segment-Datei löschen, während der Response-Body läuft" nachstellen; das belegt dann aber nur erneut das Windows-Dateisystemverhalten, das der Controller-Test deterministischer und timing-frei über das zurückgegebene `FileStreamResult`-Handle nachweist. Über HTTP hingegen hängt der Nachweis vom Executor-Timing ab (die Datei müsste zwischen Header-Empfang und Body-Ende gelöscht werden — bei einem 13-Byte-Testsegment keine verlässliche Synchronisation) und wäre auf Linux wirkungslos, weil `unlink` dort ohnehin auch ohne `FileShare.Delete` gelingt.
- Das Muster wurde geprüft: Es existieren HTTP-E2E-Tests für genau diesen Dateiabruf (`LiveStreamE2ETests`, `VideoWebPlayer.Tests/LiveStreamE2ETests.cs`, Category `E2E`, `DeviceClientTestBase` mit echtem Segmentverzeichnis über `Setup.LiveStreamSegmentDirectory`). Insbesondere `PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` deckt den unveränderten Auslieferungs-Happy-Path (200, `video/mp2t`, vollständiger Inhalt) bereits end-to-end ab und bleibt ohne Anpassung grün; er sichert damit das Akzeptanzkriterium „unverändertes Auslieferungsverhalten" auf HTTP-Ebene mit ab. Eine Erweiterung um das Delete-Szenario bringt keinen zusätzlichen Beleg, den der geplante Controller-Test nicht stabiler liefert.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine bestehenden E2E-Tests betroffen. |

## Offene Punkte

Keine. — Die in `requirement.md` offen gehaltene Frage nach `EnableRangeProcessing` ist durch die dortige explizite Abgrenzung („nicht Teil dieses Schritts") entschieden: Range-Verarbeitung wird nicht aktiviert (siehe Designentscheidungen).
