# Bestandsaufnahme: Demoinstallation — mitgelieferter Demodatenbestand (Schritt 1)

Analysiert wurde der Repository-Stand bezogen auf die Anforderung
„mitgelieferter Demodatenbestand bei der Erstregistrierung"
([requirement.md](requirement.md)). Eine ausführliche, im Projekt bereits
erstellte Bestandsaufnahme mit denselben Fundstellen liegt unter
`docs/projects/task/issue-356-…/inventory/` — dieses Dokument fasst die
befundene Lage zusammen und verweist auf die Detaildokumente.

## Zusammenfassung

- **Demodaten-Mechanismus existiert als Stub:**
  `IDemoDataSetService`/`FileSystemDemoDataSetService`
  (`VideoWebPlayer/Services/DemoData/`) lesen `*.json` aus `DemoDataSets/`
  (Top-Level) und legen pro Eintrag `MediaSource` + `MediaSourceUser` an —
  **ohne `SourceType`** (bleibt Entity-Default `Sftp`), ohne `Icon`, ohne
  Dateien/Inhalte und ohne Scan-Trigger. DI: `AddScoped` in
  `ServiceCollectionExtensions.cs:344`.
- **`DemoDataSets/` fehlt vollständig:** der Ordner existiert im Repo nicht;
  `GetAvailableAsync()` liefert eine leere Liste → `Register.razor` (Zeilen
  47–59, 80–85) blendet die Auswahl aus. csproj-Regel (Zeilen 87–92) kopiert
  nur `DemoDataSets\*.json` flach; Unterverzeichnisse mit Medien-Assets würden
  beim Build/Publish fehlen.
- **Auswahldialog existiert unverändert:** `Register.razor` lädt
  `GetAvailableAsync()` nur für den ersten Benutzer, bietet Option „(Keine
  Demodaten anlegen)" und ruft `ApplyAsync(Input.DemoDataSetId, user)` nach
  erfolgreichem `UserManager.CreateAsync`; Fehler → Meldung im Formular,
  Benutzer bleibt angelegt (gewünschtes Fehlerverhalten).
- **Lokale Medienquelle ist komplett:** `MediaSourceType.LocalDirectory = 1`,
  `LocalMediaSourceReader`, `MediaSourceReaderDispatcher`, Admin-UI,
  Pfad-Validierung (absoluter Pfad), Streaming/ffprobe über denselben
  `IMediaSourceReader`-Vertrag — nicht neu zu bauen.
- **Dateiablage + Scan ist der bewiesene Weg:**
  `LocalMediaSourcePipelineE2ETests` beweist, dass ein `LocalDirectory` mit
  `tvshow.nfo` + `<episodedetails>`-NFOs zu
  `TVShow`/`TVShowSeason`/`TVShowEpisode` klassifiziert wird; Filme brauchen
  `<movie>`-NFOs; Bilder entstehen aus `poster`/`fanart`-Namenskonventionen
  und werden als `Picture.Data` in der DB gespeichert.
- **Kein Scan-Trigger beim Anlegen:** `MediaSourceCreatedEvent` hat keinen
  Subscriber; der Hintergrund-Loop (`MediaSourceScanService`, Intervall
  Default 60 min) oder manuelle Scans sind die einzigen Trigger. Für „sofort
  sichtbar" muss `ApplyAsync` den
  `MediaSourceScanCoordinator.RunFullScanAsync` anstoßen (scoped, per DI
  injizierbar, `ServiceCollectionExtensions.cs:240`).
- **Bilderzeugung zur Laufzeit möglich:** SkiaSharp ist Dependency
  (`EpisodeBackgroundImageGenerator` erzeugt produktiv JPEGs). Kein ffmpeg
  gebündelt — das Dummy-Video wird daher statisch im Repo beigelegt
  (Festlegung 1).
- **Datenbank liegt unter `Data/` relativ zum ContentRoot**
  (`ConnectionStrings:DefaultConnection` = `Data Source=Data/WebVideoPlayer.db`) —
  `Data/` ist die etablierte schreibbare Ablage.
- **Tests:** Es gibt keinerlei Tests für den Demodaten-Mechanismus und keinen
  Test für die Demodaten-Auswahl im Registrierungsformular. HTTP-E2E-Muster
  für die Registrierung existiert (`FirstUserRedirectE2ETests.RegisterFirstUser_SubmitsAndSetsAuthCookie`).
  Ausgangszustand siehe [tests.md](inventory/tests.md).

## Details

- [Demodaten-Mechanismus und Erstregistrierung](inventory/demo-data.md)
- [Lokale Medienquelle (LocalDirectory)](inventory/media-sources.md)
- [Scan- und Klassifizierungs-Pipeline, NFO-/Bild-Konventionen](inventory/scan-pipeline.md)
- [FFmpeg-Verfügbarkeit und Bilderzeugung](inventory/ffmpeg-images.md)
- [Tests](inventory/tests.md)
