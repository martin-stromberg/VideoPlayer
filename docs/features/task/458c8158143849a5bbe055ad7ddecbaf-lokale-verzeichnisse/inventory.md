# Bestandsaufnahme: Lokales Verzeichnis als Medienquelle

Analysiert wurde der bestehende Medienquellen-Stack (Datenmodell, Reader, Scanner, Klassifizierung, Streaming, Admin-UI, Tests) bezogen auf die Anforderung, neben SFTP auch lokale Verzeichnisse als Quelle zu erlauben.

## Zusammenfassung

- `MediaSource` (`VideoWebPlayer/Data/MediaSource.cs`) enthält ausschließlich SFTP-Verbindungsfelder (`Host`, `Port`, `Username`, `Password`); es gibt **kein** Quelltyp-Feld, **kein** `MediaSourceType`-Enum und keine zugehörige Migration.
- `SftpMediaSourceReader` (`VideoWebPlayer/Services/SftpMediaSourceReader.cs`) ist der einzige Dateizugriff und wird überall konkret injiziert — es existiert **kein** `IMediaSourceReader`/Factory; die Streaming-Methode `GetSftpFileStream` ist nicht virtuell und liefert den SFTP-spezifischen `SftpStreamWrapper`.
- Alle drei Konsumenten sind SFTP-gekoppelt: `MediaSourceScanner` (`ReadRootDirectory` Zeile 68, `ReadDirectoryEntries` Zeile 211, catch nur `SftpPathNotFoundException`/`SftpPermissionDeniedException` Zeile 213), `MediaSourceClassifier` (~15 Reader-Aufrufe für NFO/Bilder/Schauspieler), `ItemsController` (`GetSftpFileStream` Zeile 442 für Stream+Download).
- `MediaSourceExtensions.Update` kopiert nur die vorhandenen Felder; Admin-UI (`MediaSourceAdminDetails.razor`) zeigt immer alle SFTP-Felder ohne Typauswahl und ohne Pfadvalidierung; `MediaSourceAdmin.razor` listet Host/Port/Pfad ohne Typ-Spalte; `MediaSourceExplorer.razor` arbeitet nur auf DB-Daten.
- `DtoMediaSource` enthält keinen Quelltyp; das DTO-Mapping (`ApiBaseController.Create<T>`) würde ein gleichnamiges Feld automatisch kopieren.
- `MediaSourceCreated/Updated/DeletedEvent` werden publiziert, haben aber **keine** produktiven Subscriber.
- Pfad-Traversal-Präzedenz existiert in `VideoWebPlayerBackupData.IsSafeRelativePath` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` Zeile 970); relevant, weil der Classifier relative `<thumb>`-Pfade aus NFOs direkt an den Reader gibt (Zeile 1500).
- README/`docs/help/einrichtung.md` bewerben lokale Quellen bereits, obwohl sie nicht implementiert sind.
- Test-Helpers (`Fake/Throwing/Series/BackfillSftpMediaSourceReader` + eine Ableitung in `MediaSourceClassifierActorBackfillTests`) erben von `SftpMediaSourceReader`; Tests für lokale Quellen existieren nicht. Streaming via `GetSftpFileStream` ist in keinem Test abgedeckt.

**Test-Ausgangszustand** (Details: [inventory/tests.md](inventory/tests.md)): Unit-Suite (`Category!=E2E`) 256/256 bestanden, Integration-Filter liefert 0 Tests (Kategorie existiert nicht), E2E-Suite 8 bestanden / **37 fehlgeschlagen** / 0 übersprungen — alle Fehlschläge mit demselben Bild (HTTP `Found` statt `OK` bzw. Playwright-Timeout auf `#email`), plausiblerweise verursacht durch den während dieser Bestandsaufnahme entstandenen Commit `9d8868d` (NavMenu wirft `UnauthorizedAccessException` für anonyme SSR-Requests).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [UI](inventory/ui.md)
- [Tests](inventory/tests.md)
