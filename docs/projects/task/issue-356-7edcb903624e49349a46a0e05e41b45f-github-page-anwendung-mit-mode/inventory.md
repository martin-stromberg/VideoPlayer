# Bestandsaufnahme: GitHub-Page mit Demoinstallation (Issue #356)

Analysiert wurde der Repository-Stand bezogen auf die Anforderung „öffentliche
Präsentationsseite + Demoinstallation" (siehe [requirement.md](requirement.md)).
Die Vor-Bestandsaufnahme zum GitHub-Page-Teil liegt unter
`docs/features/task/issue-356-…/inventory.md` und wird hier nicht wiederholt —
diese Erhebung ergänzt sie um die Anwendungsseite: Demodaten-Mechanismus,
lokale Medienquelle, Scan-/Klassifizierungs-Pipeline, FFmpeg-/Bilderzeugung und
die zugehörige Testinfrastruktur.

## Zusammenfassung

- **Lokale Medienquelle ist vollständig implementiert:**
  `MediaSourceType.LocalDirectory`, `LocalMediaSourceReader`,
  `MediaSourceReaderDispatcher`, Admin-UI mit Quelltyp-Auswahl und
  Pfad-Validierung, Streaming und ffprobe-Laufzeitermittlung laufen über
  denselben `IMediaSourceReader`-Vertrag wie SFTP. Für die Demoinstallation
  fehlt nur, den Typ korrekt zu setzen.
- **Der Demodaten-Mechanismus existiert, ist aber ein Stub:**
  `IDemoDataSetService`/`FileSystemDemoDataSetService` lesen JSON-Definitionen
  aus `DemoDataSets/` und legen `MediaSource` + `MediaSourceUser` an — ohne
  `SourceType` (Default `Sftp`), ohne Medienverzeichnis, ohne Inhalte. Das
  Definitionsfeld `Icon` wird beim Anlegen ignoriert.
- **`DemoDataSets/` ist leer und wird mit nichts geliefert:** der Ordner
  existiert im Repo nicht, `StandardSources.json` (für das csproj-Target
  `CopyStandardSourcesDemoData` gedacht) existiert nicht — `GetAvailableAsync()`
  liefert eine leere Liste und `Register.razor` blendet die Auswahl aus
  (Zeilen 47–59, 80–85). Die csproj-Regel kopiert nur `DemoDataSets\*.json`,
  keine Unterverzeichnisse — Medien-Assets würden beim Publish fehlen.
- **Dateiablage + Scan ist der eingeschlagene Weg und funktioniert
  end-to-end:** `LocalMediaSourcePipelineE2ETests` beweist, dass ein
  `LocalDirectory` mit `tvshow.nfo`/`<episodedetails>`-NFOs plus beliebigen
  Datei-Inhalten zu `TVShow`/`TVShowSeason`/`TVShowEpisode`-Entitäten
  klassifiziert wird. Filme brauchen `<movie>`-NFOs; Bilder entstehen aus
  `<name>-poster.jpg`/`folder.jpg`-Konventionen und werden in `Picture.Data`
  gespeichert. Direkte DB-Persistenz wäre die Alternative, muss aber das
  komplette Verknüpfungsmodell (`MovieMediaItem`, `TVShowEpisodeMediaItem`,
  `Picture.MediaItemId`, `MediaSourceUser`) von Hand abbilden.
- **Der Scan läuft nicht sofort:** `MediaSourceCreatedEvent` hat keinen
  Subscriber; gescannt wird im Hintergrund-Loop (`MediaSourceScanService`,
  Intervall aus `ScanProcessIntervalMinutes`, Default 60 min) oder manuell
  (`MediaSourceAdmin`, `MediaSourceExplorer`). Für „sofort sichtbar nach
  Registrierung" müsste `ApplyAsync` den `MediaSourceScanCoordinator`
  anstoßen oder ein Event verdrahtet werden.
- **FFmpeg ist nicht gebundelt, aber voll integriert:** `FfmpegLocator`/
  `FfprobeLocator` suchen `MediaProbe:FfmpegPath`/`FfprobePath` →
  `tools/ffmpeg/` → `PATH`; `FfmpegToolDownloadService` lädt die statischen
  Binaries (~145 MB) auf Admin-Knopfdruck nach `tools/ffmpeg/`. Damit ist
  dynamische Dummy-Video-Erzeugung möglich, aber von der Umgebung abhängig.
- **Bilderzeugung ohne FFmpeg vorhanden:** SkiaSharp ist Projekt-Dependency
  (`EpisodeBackgroundImageGenerator`, `HomeBackgroundImageGenerator`,
  Playlist-Cover-Checker); Test-Helfer `TestImages` erzeugt synthetische PNGs.
  Anzeigebilder für Demodaten lassen sich also zur Laufzeit generieren — ohne
  neue Dependency.
- **Wiedergabe braucht kein echtes Video — der Player schon:**
  `/api/items/{type}/{id}/stream` liefert die Datei unverändert
  (`OpenFileStream`); Tests streamen 6-Byte-Dummies. Für Screenshot-taugliche
  Player-Ansichten reicht das nicht — dafür muss die Dummy-Datei ein echtes,
  im Browser abspielbares Containerformat sein (mp4/h264).
- **Test-Ausgangszustand:** volle Suite vom selben Commit (Baseline der
  Vorarbeit): 2969 bestanden, 2 nachgewiesene Flakes (beide im Wiederholungslauf
  grün), 2 begründete Übersprünge. Fokuslauf heute über alle
  `*MediaSource*`/`*LocalMediaSource*`-Tests: **120/120 bestanden**.
  Es gibt **keinerlei Tests für den Demodaten-Mechanismus** und keinen Test,
  der die Demodaten-Auswahl im Registrierungsformular bedient.
  Nachweis: [tests.md](inventory/tests.md).

## Details

- [Demodaten-Mechanismus und Erstregistrierung](inventory/demo-data.md)
- [Lokale Medienquelle (LocalDirectory)](inventory/media-sources.md)
- [Scan- und Klassifizierungs-Pipeline, NFO-/Bild-Konventionen](inventory/scan-pipeline.md)
- [Datenmodell (Medien-Entitäten)](inventory/media-model.md)
- [FFmpeg-Verfügbarkeit und Bilderzeugung](inventory/ffmpeg-images.md)
- [Tests](inventory/tests.md)
