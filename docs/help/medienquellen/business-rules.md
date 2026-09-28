← [Zurück zur Übersicht](index.md)

# Medienquellen — Business Rules

## Quelltyp ist nach dem Anlegen nicht änderbar

**Beschreibung:** Der `SourceType` einer Quelle kann nur beim Anlegen gewählt werden. Im Bearbeitungsdialog ist die Auswahl deaktiviert.

**Bedingungen:**
- `MediaSourceAdminDetails.razor`, `InputSelect` mit `disabled="@(!IsNew)"`.

**Verhalten:**
- Neue Quelle (`IsNew`): Auswahl `SFTP-Server`/`Lokales Verzeichnis` aktiv.
- Bestehende Quelle: Auswahl gesperrt; Felder rendern passend zum gespeicherten Typ.

**Umsetzung:** `MediaSourceAdminDetails` (Razor-Attribut `disabled`). Ein Typwechsel würde die `Path`-Semantik aller bereits gescannten `MediaCollections`/`MediaItems` ändern; der Scanner löscht verwaiste Einträge nicht — die Sperrung verhindert inkonsistente Bestände. Serverseitig kopiert `MediaSourceExtensions.Update` den `SourceType` konsistent mit.

## Validierung lokaler Verzeichnispfade

**Beschreibung:** Beim Speichern einer Quelle vom Typ `Lokales Verzeichnis` wird der Pfad serverseitig geprüft, bevor persistiert wird.

**Bedingungen:**
- `SourceType == LocalDirectory`.

**Verhalten:**
- Pfad leer oder nicht absolut (`Path.IsPathRooted` false): Meldung „Ungültiger Pfad: Bitte einen absoluten Verzeichnispfad angeben."
- `Path.GetFullPath` wirft (`ArgumentException`/`NotSupportedException`/`PathTooLongException`): Meldung „Ungültiger Pfad: Der angegebene Verzeichnispfad ist ungültig."
- `Directory.Exists` false: Meldung „Das Verzeichnis existiert nicht auf dem Server."
- Testzugriff (`Directory.EnumerateFileSystemEntries`) wirft `IOException`/`UnauthorizedAccessException`: Meldung „Auf das Verzeichnis kann nicht zugegriffen werden: Der Serverprozess hat keine Leseberechtigung."
- Bei Fehler: `errorMessage` wird gesetzt, `Save()` bricht ab — es wird nichts gespeichert.
- Bei Erfolg: SFTP-Felder werden neutralisiert (`Host = string.Empty`, `Port = 0`, `Username`/`Password = null`), da `Host` in der Datenbank non-nullable ist.

**Umsetzung:** `MediaSourceAdminDetails.TryValidateLocalDirectory` + `Save`. UNC-Pfade sind zulässig, da `Path.IsPathRooted`/`Directory.Exists` sie nativ unterstützen.

## Pfad-Traversal-Schutz bei lokalen Quellen

**Beschreibung:** Alle Dateizugriffe des `LocalMediaSourceReader` sind auf das konfigurierte Quellverzeichnis beschränkt — auch für relative Pfadangaben aus NFO-Dateien (z. B. `<thumb>.actors/Name.jpg</thumb>`).

**Bedingungen:**
- Jeder Aufruf von `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`, `OpenFileStream` auf eine lokale Quelle.

**Verhalten:**
- Der Pfad wird per `Path.GetFullPath(Path.Combine(collection.Path, fileName))` aufgelöst und muss innerhalb des normalisierten Quell-Roots (`MediaSource.Path`) liegen.
- Pfade außerhalb (z. B. über `..\..` oder absolute Angaben) liefern `false`/`null` — die Datei gilt als nicht vorhanden, es wird keine Exception geworfen.
- `ReparsePoint`-Einträge (Junctions/Symlinks) werden abgelehnt, damit die Root-Prüfung nicht über Verzeichnis-Links umgangen werden kann.
- Verzeichnisse als Endziel einer Dateiangabe liefern `null`.

**Umsetzung:** `LocalMediaSourceReader.ResolveFilePath`. Der Präzedenzfall ist `VideoWebPlayerBackupData.IsSafeRelativePath`. Da die Root-Begrenzung bereits im Reader liegt, wurde keine zusätzliche Whitelist erlaubter Basisverzeichnisse umgesetzt.

## Einheitliche Reader-Auswahl pro Aufruf

**Beschreibung:** Konsumenten arbeiten ausschließlich gegen `IMediaSourceReader`; die Auswahl des konkreten Readers erfolgt zentral.

**Bedingungen:**
- `MediaSource.SourceType == LocalDirectory` → `LocalMediaSourceReader`.
- `SourceType == Sftp` → `SftpMediaSourceReader`.
- `MediaSource`-Navigation `null` (nicht eager geladen) → `InvalidOperationException` — die `MediaSource` muss per `Include` mitgeladen werden.

**Umsetzung:** `MediaSourceReaderDispatcher.GetReader` — der Dispatcher implementiert selbst `IMediaSourceReader`, sodass die ~18 Aufrufstellen in Scanner, Classifier und `ItemsController` unverändert `MediaSource`/`MediaCollection` übergeben können.

## Ignorierte Verzeichniseinträge

**Beschreibung:** Einträge mit führendem Punkt (Navigations-Einträge, versteckte Dateien, `.actors`-Ordner) werden beim Einlesen beider Quelltypen übersprungen.

**Umsetzung:** `MediaEntryFilter.IsIgnoredEntry` — gemeinsame Regel, aus `SftpMediaSourceReader` extrahiert und von beiden Readern genutzt.
