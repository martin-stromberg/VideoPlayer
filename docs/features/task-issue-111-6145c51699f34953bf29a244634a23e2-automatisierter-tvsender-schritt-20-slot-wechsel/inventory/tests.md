# Detaildokument: Tests

## Test-Ausgangszustand (Baseline, vor Änderungen)

- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --nologo`
- Ergebnis: **2147 Tests — 2146 bestanden, 0 fehlgeschlagen, 1 übersprungen** (4:12 min)
- Build Debug (`dotnet build VideoPlayer.sln`): 0 Fehler, 144 Warnungen (Baseline)
- Nachweis: `test-results/baseline-summary.txt`

## Bestehende Tests im betroffenen Bereich

### `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`
Fake-`ISegmenterProcess`; assertet u. a. die Argumentzeile (`-re`, `-ss`, `-hls_time`,
`-hls_list_size 12`, `delete_segments`, Segmentmuster `seg_\d{14}_42_%05d\.ts`, `-t`,
`live.m3u8"`), Pipe/HTTP/File-Input, Start/Stop-Zustände, Unexpected-Exit-Diagnose inkl.
Token-Maskierung. → Betroffen: Argument-Test muss die neuen Flags erwarten.

### `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
`[Trait("Category", "E2E")]`: ein echter ffmpeg-Lauf auf ein per lavfi erzeugtes
6-s-Video (`GenerateVideoAsync`, `FindBinary("ffmpeg")` über PATH; Skip wenn kein ffmpeg).
Assertet Playlist + `seg_*.ts`. → Hier gehört der geforderte Zwei-Job-Regressionstest hin.

### `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs`
In-memory `LiveStreamSegmentStore` + Temp-Verzeichnis: Token-Rewrite, Content-Type,
404-Pfade. → Keine Änderung nötig (Rewrite verändert `#`-Zeilen nicht); ein neuer Assert
„DISCONTINUITY/PDT-Zeilen bleiben erhalten" wäre optional, ist aber durch die
Zeilenlogik implizit abgedeckt.

### `VideoWebPlayer.Tests/LiveStreamE2ETests.cs`
HTTP-E2E über `DeviceClientTestBase`: 401 anonym, Rewrite + Segment-Auslieferung mit
Device-Token. → Unberührt.

### `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` / `LiveStreamWorkerTests.cs`
Fake-`ILiveStreamSegmenter` — unberührt, da die Schnittstelle `LiveStreamJob` unverändert
bleibt.

## Testlücken

- Kein Test deckt den Übergang zweier ffmpeg-Generationen ab (genau der N17-Fehler).
- Kein Test assertet `-hls_flags`-Inhalt vollständig (nur `delete_segments` enthalten).
- Kein JS-/Browser-Test der hls.js-Fehlerbehandlung (existiert projektweit nicht —
  der E2E-Nachweis erfolgt über die Playlist-Struktur, nicht über einen Browser-Player).
