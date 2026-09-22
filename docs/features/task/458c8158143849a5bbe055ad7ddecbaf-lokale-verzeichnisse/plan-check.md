# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `MediaSource` erhält Quelltyp-Diskriminator (`MediaSourceType`-Enum `Sftp`/`LocalDirectory`, Default `Sftp` für Bestandsdaten) inkl. EF-Core-Migration | Schritte 2–3; neue Klasse `MediaSourceType`; Migration `AddMediaSourceSourceType` (`int`, Default `0`); Designentscheidung „Enum-Persistenz" | Persistenz indirekt über E2E-Anlegefluss und `Update_CopiesSourceType`; Backup-Test prüft Default `0` | Abgedeckt |
| `MediaSourceExtensions.Update` kopiert `SourceType` | Schritt 3; Abschnitt „Änderungen an bestehenden Klassen" | `Update_CopiesSourceType` (`ApplicationDbContextTests`) | Abgedeckt |
| Abstraktion `IMediaSourceReader` + `SftpMediaSourceReader` implementiert es; `GetSftpFileStream` → `OpenFileStream` (`Stream?`) | Schritt 4; Designentscheidungen „Quellenzugriff-Abstraktion", „Streaming-Methode" | Kompilieren der bestehenden Fakes/Suites (Abschnitt „Betroffene bestehende Tests") | Abgedeckt |
| Neuer `LocalMediaSourceReader` per `System.IO` (Enumeration, `.`-Ignorierung, `CreatedAt` aus `GetLastWriteTimeUtc`, `OrdinalIgnoreCase`-Abgleich, volle lokale Pfade) | Schritt 5; Designentscheidung „Dateinamen-Abgleich (lokal)"; Programmablauf „Scan einer Quelle" | `LocalMediaSourceReaderTests` (7 Tests: Root, Enumeration, ReparsePoint, Case-Insensitivität, Inhalt/null, Traversal, Stream) | Abgedeckt |
| Reader-Auswahl anhand `MediaSource.SourceType` + DI-Registrierung | Schritt 6 (`MediaSourceReaderDispatcher` implementiert selbst `IMediaSourceReader`); `ServiceCollectionExtensions`-Registrierung | `Dispatcher_RoutesBySourceType` (inkl. `null`-Navigation → SFTP) | Abgedeckt |
| `MediaSourceScanner` typabhängig (Root- und Collection-Scan) + lokale Fehlerbehandlung (`DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException`) analog SFTP-Fehlern | Schritt 7; Programmablauf „Scan einer Quelle" inkl. erweitertem `catch`-Filter (Zeile 213) | `MediaSourceScannerLocalTests` (3 Tests inkl. Negativfall `ScanNextMediaCollection_SkipsCollection_WhenLocalDirectoryMissing`) | Abgedeckt |
| `MediaSourceClassifier` typabhängig (alle ~16 Reader-Aufrufstellen für `tvshow.nfo`, Episoden-/Film-NFOs, Poster/Banner/Fanart/Thumbs, Schauspieler-NFOs/Bilder) | Schritt 7; Programmablauf „Klassifizierung einer lokalen Quelle" | `ClassifyAllAsync_ClassifiesTvShow_FromLocalSource`; zusätzlich E2E `LocalMediaSourcePipelineE2ETests` | Abgedeckt |
| Streaming und Download aus lokaler Quelle (`ItemsController.StreamMediaItem`/`Download`, `FileStream`, `enableRangeProcessing: true`) | Schritt 7; Programmablauf „Streaming und Download" | `StreamMediaItem_LocalSource_ReturnsFileStreamResult`; E2E `LocalMediaSourceStreamingE2ETests` (Stream + Download) | Abgedeckt |
| Admin-UI: explizite Typauswahl („SFTP-Server"/„Lokales Verzeichnis"), keine implizite Erkennung; typabhängige Felder (bei lokal nur `Name`, `Icon`, `Path`, Benutzerfreigaben) | Schritt 9; Programmablauf „Lokale Quelle über die Admin-UI anlegen"; Designentscheidung „Quelltyp im Edit-Dialog" | E2E `MediaSourceLocalDirectoryE2ETests` (Anlegen inkl. Feldsichtbarkeit) | Abgedeckt |
| Serverseitige Pfadvalidierung beim Speichern (nicht leer, absolut, `Directory.Exists`) | Schritt 9; Abschnitt „Validierungsregeln" | E2E `MediaSourceLocalDirectoryE2ETests` (ungültiger Pfad → Fehlermeldung, nichts persistiert) | Abgedeckt |
| Admin-Übersicht: Typ-Kennzeichnung bzw. typabhängige Spalten | Schritt 10 (`MediaSourceAdmin.razor`: „Typ"-Spalte, `—` für SFTP-Felder) | Im Create-E2E enthalten („Quelle erscheint in der Übersicht mit Typ-Kennzeichnung") | Abgedeckt |
| Sicherheit: Pfad-Traversal-Schutz für lokale Zugriffe (relative NFO-`<thumb>`-Pfade, `..\..`, Symlinks/Junctions), Präzedenz `IsSafeRelativePath` | Designentscheidung „Pfadauflösung & Traversal-Schutz (lokal)"; Schritt 5; Validierungsregeln | `*_RejectsTraversalOutsideRoot` (mehrere Fälle) + `ReadDirectoryEntries_SkipsReparsePoints` | Abgedeckt |
| Backup/Restore: neue Spalte automatisch serialisiert; Alt-Backups ohne `SourceType` bleiben restorierbar (offene Frage 6) | Schritt 8; Programmablauf „Restore eines Alt-Backups" (`OptionalRestoreColumns` + `OptionalRestoreIntDefaults`) | `Restore_OldBackupWithoutSourceType_Succeeds` (`VideoWebPlayerBackupDataTests`) | Abgedeckt |
| Typwechsel bestehender Quellen (offene Frage 2): Entscheidung getroffen — nicht änderbar, da `Path`-Semantik wechselt und Scanner verwaiste Einträge nicht löscht | Designentscheidung „Quelltyp im Edit-Dialog" (Select `disabled` bei `!IsNew`); Schritt 9 | E2E `MediaSourceLocalDirectoryE2ETests` (Edit-Szenario, Typ-Select deaktiviert) | Abgedeckt |
| Zugriffsschutz/Benutzerfreigaben gelten auch für lokale Quellen | Unveränderte `EnsureAccessAsync`-/`MediaSourceUsers`-Logik; Freigaben im Formular beider Typen | E2E `LocalMediaSourceStreamingE2ETests` (401 ohne `MediaSourceUsers`-Freigabe); bestehende `ItemsControllerAccessTests` bleiben | Abgedeckt |
| Bestehende SFTP-Funktionalität unverändert (Default `Sftp`, keine Änderung an SFTP-Pfadlogik) | Designentscheidungen; `SftpMediaSourceReader` fachlich unverändert; `FileSystemDemoDataSetService`/`MediaSourceExplorer` unverändert (Seiteneffekte) | Bestehende Unit-/E2E-Suites als Regression (Abschnitt „Betroffene bestehende Tests") | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin legt Quelle vom Typ „Lokales Verzeichnis" an: Typauswahl, `Name`+`Path`, Speichern → Übersicht mit Typ-Kennzeichnung; SFTP-Felder bei lokalem Typ nicht sichtbar | `MediaSourceLocalDirectoryE2ETests` (Playwright, Pflicht; Muster wie `MediaSourceDeleteE2ETests`) | Abgedeckt |
| Ungültiger lokaler Pfad → sichtbare Fehlermeldung, Quelle wird nicht angelegt | `MediaSourceLocalDirectoryE2ETests` (Pflicht) | Abgedeckt |
| Bearbeiten lokaler Quelle: Typ-Select deaktiviert, lokale Felder sichtbar, Speichern möglich | `MediaSourceLocalDirectoryE2ETests` (Soll) | Abgedeckt |
| Lokale Quelle durchläuft Scan + Klassifizierung im echten Host; Inhalte über API abrufbar | `LocalMediaSourcePipelineE2ETests` (HTTP-Ebene via `WebApplicationFactory`, Pflicht) | Abgedeckt |
| Streaming (`GET .../stream`) und Download (`GET .../download`) aus lokalem Verzeichnis liefern 200, `video/*`-Content-Type und Dateibytes | `LocalMediaSourceStreamingE2ETests` (HTTP-Ebene, Pflicht) | Abgedeckt |
| Zugriff ohne `MediaSourceUsers`-Freigabe → HTTP 401 am echten Endpunkt | `LocalMediaSourceStreamingE2ETests` (Pflicht) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- Der Plan enthält mit Umsetzungsschritt 1 die Behebung des preexisting `NavMenu`-Defekts (Commit `9d8868d`, `UnauthorizedAccessException` für anonyme SSR-Requests). Das liegt fachlich außerhalb der Anforderung, ist aber im Plan nachvollziehbar als Voraussetzung für browserbasierte E2E-Tests begründet (37 dokumentierte Baseline-Fehlschläge) und als Risiko benannt.
- Offene Fragen 2–5 aus `requirement.md` wurden als Designentscheidungen festgelegt und begründet: Typwechsel bestehender Quellen gesperrt, UNC-Pfade zulässig, keine Pfad-Whitelist (`MediaSources:LocalRootPaths`), kein `SourceType` im `DtoMediaSource`. Offene Frage 1 (FTP vs. SFTP) ist als Annahme konsistent umgesetzt.
- HTTP-basierte E2E-Tests (Pipeline, Streaming) sind bewusst browserunabhängig geplant und damit vom `NavMenu`-Defekt unberührt — sie folgen dem bestehenden Muster `EpisodesBackgroundImageAccessTokenE2ETests`.
- Die Windows-1252-Kodierung von `MediaSource.cs`, `MediaSourceExtensions.cs` und `MediaSourceAdminDetails.razor` ist im Plan als Voraussetzung vermerkt (Enum in eigener UTF-8-Datei).
