← [Zurück zur Übersicht](index.md)

# Medienquellen — Fehlerbehebung

## Lokale Quelle wird nicht gescannt (Collections fehlen)

**Symptom:** Eine Quelle vom Typ `Lokales Verzeichnis` wurde angelegt, aber es erscheinen keine Inhalte. Im Log steht „Collection '…' konnte nicht gelesen werden und wird übersprungen."

**Ursache:** Das Verzeichnis ist für den Serverprozess nicht erreichbar — z. B. nicht gemountetes Laufwerk, nicht auflösbare UNC-Freigabe oder fehlende Leserechte. Der `catch`-Filter in `MediaSourceScanner.ScanMediaCollectionInternalAsync` fängt `DirectoryNotFoundException`, `UnauthorizedAccessException` und `IOException` ab.

**Lösung:**
1. Prüfen, ob das Verzeichnis aus Sicht des Servers existiert (`Directory.Exists` auf dem Server, nicht auf dem Client).
2. Dem Dienstkonto des VideoWebPlayer-Prozesses Lesezugriff auf das Verzeichnis bzw. die Freigabe einräumen.
3. Der Scan wird automatisch über `ScanDueAt` neu terminiert; alternativ `Scan zurücksetzen` oder `Komplettscan aller Quellen` in der Übersicht.

> **Hinweis:** Das Speichern verhindert diesen Fall nur teilweise — `TryValidateLocalDirectory` prüft Existenz und Zugriff zum Anlege-Zeitpunkt, spätere Laufzeitfehler (Freigabe offline) entstehen trotzdem.

## Einzelne Dateien/Unterordner fehlen in einer lokalen Quelle

**Symptom:** Ein Verzeichnis wurde größtenteils eingelesen, einzelne Einträge fehlen. Debug-/Warning-Logeinträge des `LocalMediaSourceReader` verweisen auf übersprungene Einträge.

**Ursache:** Drei Filter wirken beim Einlesen:
- Namen mit führendem Punkt (`MediaEntryFilter.IsIgnoredEntry`) — betrifft z. B. `.actors`-Ordner und versteckte Dateien.
- `FileAttributes.ReparsePoint` — Junctions/Symlinks werden generell übersprungen.
- Fehler beim Attributzugriff (`File.GetAttributes`) — Eintrag wird mit Warning geloggt und übersprungen.

**Lösung:**
1. Verknüpfungen im Quellverzeichnis durch echte Verzeichnisse ersetzen oder das Zielverzeichnis als eigene Quelle anlegen.
2. Versteckte/`.`-Einträge umbenennen, falls sie als Medien erkannt werden sollen.
3. Log auf `Warning`-Einträge des `LocalMediaSourceReader` prüfen.

## NFO-/Bilddateien aus relativen Unterpfaden werden nicht gefunden

**Symptom:** `FileExistsAsync`/`ReadFileAsync` liefern `false`/`null`, obwohl die Datei im Quellverzeichnis liegt.

**Ursache:** `LocalMediaSourceReader.ResolveFilePath` lehnt den Zugriff ab, wenn der aufgelöste Pfad außerhalb des Quell-Roots liegt, ein Pfadsegment ein `ReparsePoint` ist oder das Ziel ein Verzeichnis statt einer Datei ist. Der Abgleich erfolgt case-insensitiv über Verzeichnis-Enumeration — auf Linux bleibt die Semantik identisch zum SFTP-Reader.

**Lösung:**
1. Prüfen, ob NFO-`<thumb>`-Pfade o. ä. aus dem Quell-Root herausführen (`..\..`) oder absolute Pfade enthalten — solche Verweise werden bewusst nicht gelesen.
2. Junctions/Symlinks in den betroffenen Pfadsegmenten entfernen.

## Streaming/Download aus lokaler Quelle liefert 404

**Symptom:** `GET …/stream` bzw. `…/download` auf ein `MediaItem` einer lokalen Quelle antwortet mit `NotFound`.

**Ursache:** `LocalMediaSourceReader.OpenFileStream` liefert `null`, wenn `ResolveFilePath` den Pfad ablehnt oder die Datei nicht existiert. `ItemsController` bildet `null` auf `NotFound` ab. Auch relevant: `Path.GetFileName(mediaItem.Path)` — unter Windows werden `/` und `\` als Separator erkannt; bei einem Linux-Deployment mit `\`-Pfaden würde der Dateiname falsch extrahiert (das Projekt ist Windows-fokussiert).

**Lösung:**
1. Prüfen, ob die Datei physisch unter `collection.Path` existiert und für den Serverprozess lesbar ist.
2. Prüfen, ob der gespeicherte `MediaItem.Path`/`MediaCollection.Path` noch zum aktuellen Quell-Root passt (z. B. nach Umbenennen des Verzeichnisses Quelle erneut scannen lassen).

## Bearbeiten-Dialog: Quelltyp lässt sich nicht ändern

**Symptom:** Die `Quelltyp`-Auswahl ist im Bearbeitungsdialog deaktiviert.

**Ursache:** Gewolltes Verhalten — ein Typwechsel würde die `Path`-Semantik aller gescannten Einträge ändern (siehe `business-rules.md`).

**Lösung:** Quelle löschen und mit dem gewünschten Typ neu anlegen.

## Alt-Backup schlägt an der Schema-Validierung fehl

**Symptom:** Restore eines Backups aus einer Version vor dem Feature schlägt fehl.

**Ursache:** Normalerweise nicht mehr relevant — `MediaSources.SourceType` ist in `OptionalRestoreColumns`/`OptionalRestoreIntDefaults` eingetragen (Default `0` = `Sftp`). Ein Fehler deutet auf eine ältere Anwendungsversion ohne diesen Eintrag hin.

**Lösung:**
1. Anwendung auf die aktuelle Version aktualisieren, dann Restore erneut ausführen.
2. Quellen aus dem Alt-Backup werden mit `SourceType = Sftp` wiederhergestellt — dem bisherigen Verhalten entsprechend.
