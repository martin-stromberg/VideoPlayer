# Tasks: Lokales Verzeichnis als Medienquelle

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Voraussetzung | `NavMenu.razor`: `OnInitializedAsync` auf authentifizierte Benutzer beschränken bzw. `UnauthorizedAccessException` abfangen (Behebung des preexisting E2E-Blockers aus Commit `9d8868d`) | Offen | — |
| 2 | Datenmodell | Enum `MediaSourceType` anlegen (`VideoWebPlayer/Data/MediaSourceType.cs`, Werte `Sftp = 0`, `LocalDirectory = 1`) | Offen | — |
| 3 | Datenmodell | `MediaSource` um Eigenschaft `SourceType` (`MediaSourceType`, Default `Sftp`) erweitern (Datei ist Windows-1252-kodiert) | Offen | — |
| 4 | Datenmodell | `MediaSourceExtensions.Update` um das Kopieren von `SourceType` erweitern (Windows-1252-Datei) | Offen | — |
| 5 | Datenmodell | EF-Core-Migration `AddMediaSourceSourceType` erzeugen (Spalte `MediaSources.SourceType`, `int`, non-nullable, Default `0`; Snapshot aktualisieren) | Offen | — |
| 6 | Logik | Interface `IMediaSourceReader` anlegen (`VideoWebPlayer/Services/IMediaSourceReader.cs`) mit `ReadRootDirectory`, `ReadDirectoryEntries`, `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`, `OpenFileStream` | Offen | — |
| 7 | Logik | `SftpMediaSourceReader` implementiert `IMediaSourceReader`; `GetSftpFileStream` zu `OpenFileStream` umbenennen (Rückgabe `Stream?`); `ReadSubtree` bleibt außerhalb des Interface | Offen | — |
| 8 | Logik | `LocalMediaSourceReader` implementieren (`VideoWebPlayer/Services/LocalMediaSourceReader.cs`): Enumeration per `Directory.EnumerateFileSystemEntries`, `.`-Einträge und `ReparsePoint`s ignorieren, `CreatedAt` aus `GetLastWriteTimeUtc` | Offen | — |
| 9 | Logik | `LocalMediaSourceReader`: Traversal-Schutz — Pfade per `Path.GetFullPath` normalisieren und auf Quell-Root (`MediaSource.Path`) begrenzen; Dateinamensabgleich `OrdinalIgnoreCase`; `OpenFileStream` liefert `FileStream` | Offen | — |
| 10 | Logik | `MediaSourceReaderDispatcher` anlegen (`IMediaSourceReader`-Implementierung, wählt pro Aufruf anhand `MediaSource.SourceType`; Default `Sftp`) | Offen | — |
| 11 | Logik | `ServiceCollectionExtensions`: `LocalMediaSourceReader` (scoped) und `IMediaSourceReader` → `MediaSourceReaderDispatcher` (scoped) registrieren; `SftpMediaSourceReader`-Registrierung behalten | Offen | — |
| 12 | Logik | `MediaSourceScanner` auf `IMediaSourceReader` umstellen (Konstruktor + Feld) | Offen | — |
| 13 | Logik | `MediaSourceScanner.ScanMediaCollectionInternalAsync`: `catch`-Filter um `DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException` erweitern | Offen | — |
| 14 | Logik | `MediaSourceClassifier` auf `IMediaSourceReader` umstellen (Konstruktor + Feld, ~16 Aufrufstellen unverändert) | Offen | — |
| 15 | Logik | `ItemsController` auf `IMediaSourceReader` umstellen und `StreamMediaItem` auf `OpenFileStream` aufrufen | Offen | — |
| 16 | Backup | `VideoWebPlayerBackupData`: `MediaSources.SourceType` in `OptionalRestoreColumns` und `OptionalRestoreIntDefaults` (Default `0`) eintragen | Offen | — |
| 17 | UI | `MediaSourceAdminDetails.razor`: `InputSelect` „Quelltyp" (id `source-type`) ergänzen, nur bei Neuanlage aktiv (`disabled` im Edit-Modus) — Muster: `InputSelect` in `Register.razor` | Offen | — |
| 18 | UI | `MediaSourceAdminDetails.razor`: `Host`/`Port`/`Username`/`Password` nur bei `SourceType == Sftp` rendern; `Path`-Label typabhängig; `SourceType` beim Laden in `editSource` kopieren | Offen | — |
| 19 | Validierung | `MediaSourceAdminDetails.razor` `Save()`: bei `LocalDirectory` Pfad validieren (nicht leer, rooted, `Directory.Exists`) → `errorMessage` + Abbruch; SFTP-Felder neutralisieren (`Host = string.Empty`, `Port = 0`, `Username`/`Password = null`) | Offen | — |
| 20 | UI | `MediaSourceAdmin.razor`: Spalte „Typ" („SFTP"/„Lokal") ergänzen; bei lokalen Quellen `—` in `Host`/`Port`/`Benutzername` | Offen | — |
| 21 | Tests | `MediaSourceScannerTests`: DI-Registrierung `SftpMediaSourceReader` → `IMediaSourceReader` umstellen | Offen | — |
| 22 | Tests | `MediaSourceScanServiceTests` (5 Stellen): DI-Registrierung `SftpMediaSourceReader` → `IMediaSourceReader` umstellen | Offen | — |
| 23 | Tests | `MediaSourceClassifierBackgroundImageTests`: `BuildServiceProvider`-Registrierung auf `IMediaSourceReader` umstellen | Offen | — |
| 24 | Tests | `MediaSourceClassifierActorBackfillTests`: Fake-Registrierungen (`BackfillSftpMediaSourceReader`, `MissingPathSftpMediaSourceReader`) auf `IMediaSourceReader` umstellen | Offen | — |
| 25 | Tests | `LocalMediaSourceReaderTests` (neu): Root-Verzeichnis, Eintrags-Enumeration inkl. `.`-Ignorierung, `OrdinalIgnoreCase`-Abgleich, `ReadFileAsync`/`ReadFileStreamAsync`/`OpenFileStream` mit Temp-Verzeichnissen | Offen | — |
| 26 | Tests | `LocalMediaSourceReaderTests`: Traversal-/ReparsePoint-Fälle (`..\..`, absoluter Pfad außerhalb Root, Junction/Symlink) liefern `null`/`false` bzw. werden übersprungen | Offen | — |
| 27 | Tests | `MediaSourceReaderDispatcherTests` (neu): Routing nach `SourceType` inkl. `null`-Navigation → SFTP-Default | Offen | — |
| 28 | Tests | `MediaSourceScannerLocalTests` (neu o. Erweiterung): lokale Quelle scannen legt Root-Collection + Einträge an; fehlendes Verzeichnis → überspringen + `ScanDueAt` neu terminieren | Offen | — |
| 29 | Tests | `LocalMediaSourceClassifierTests` (neu): `tvshow.nfo`-Struktur in Temp-Verzeichnis wird zu `TVShow`/`TVShowEpisodes` klassifiziert | Offen | — |
| 30 | Tests | `ItemsControllerLocalSourceTests` (neu): `StreamMediaItem` auf lokaler Quelle liefert `FileStreamResult` mit `FileStream` und korrektem Content-Type | Offen | — |
| 31 | Tests | `ApplicationDbContextTests` erweitern: `UpdateMediaSourceAsync` kopiert `SourceType` | Offen | — |
| 32 | Tests | `VideoWebPlayerBackupDataTests` erweitern: Alt-Backup ohne `SourceType`-Spalte restoriert erfolgreich mit Default `Sftp` | Offen | — |
| 33 | E2E-Tests | `MediaSourceLocalDirectoryE2ETests` (neu, Playwright): Admin legt lokale Quelle an; Typ-Kennzeichnung in Übersicht; SFTP-Felder bei lokalem Typ ausgeblendet | Offen | — |
| 34 | E2E-Tests | `MediaSourceLocalDirectoryE2ETests`: nicht existierender lokaler Pfad → Fehlermeldung, kein Speichern; Typ-Select im Edit-Modus deaktiviert | Offen | — |
| 35 | E2E-Tests | `LocalMediaSourcePipelineE2ETests` (neu, `WebApplicationFactory` + HTTP): lokale Quelle (Temp-Verz. mit `tvshow.nfo`) wird gescannt + klassifiziert, Inhalte über API abrufbar | Offen | — |
| 36 | E2E-Tests | `LocalMediaSourceStreamingE2ETests` (neu, `WebApplicationFactory` + `access_token`): `stream`/`download` liefern 200 + Dateibytes; ohne `MediaSourceUsers`-Freigabe 401 | Offen | — |
