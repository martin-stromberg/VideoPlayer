# Scan- und Klassifizierungsbestandsaufnahme

## Scanpfad

- [`MediaSourceScanner.cs`](../../../../../../VideoWebPlayer/Services/MediaSourceScanner.cs) scannt Quellen und Collections, aktualisiert Scanzeitpunkte und markiert geänderte MediaItems.
- [`MediaSourceScanService.cs`](../../../../../../VideoWebPlayer/Services/MediaSourceScanService.cs) orchestriert Root-Scan, inkrementelle Scans und anschließende Klassifizierung.
- Die Scanlogik schreibt Dateisystemzustände in `MediaCollection`/`MediaItem`; sie kennt aktuell kein manuelles Metadaten-Override.

## Klassifizierung

- [`MediaSourceClassifier.cs`](../../../../../../VideoWebPlayer/Services/MediaSourceClassifier.cs) liest `tvshow.nfo` und episoden-/filmbezogene NFO-Dateien über den SFTP-Reader.
- Beim Erstellen oder Aktualisieren werden unter anderem `Name`, Datumsfelder, `Plot` und `GenreNames` aus XML gesetzt. Bestehende Serien, Episoden und Filme werden dabei direkt aktualisiert und gespeichert.
- `ReloadGenres` baut Genreentitäten und Linktabellen aus `GenreNames` neu auf. Diese Routine ist ein zusätzlicher Überschreibpfad, der manuelle Genreänderungen berücksichtigen muss.
- MovieCollections werden aus klassifizierten Filmen gebildet beziehungsweise aktualisiert; Name und Datumswerte werden aus den Filmen abgeleitet.

## Schutzlücke

- Es gibt im Modell und in der Klassifizierung keine erkennbare Prüfung `IsManuallyEdited`/`MetadataLocked` oder vergleichbarer Eigenschaften.
- Ein späterer initialer Scan, ein geänderter NFO-Inhalt, `ClassifyMediaItemsAsync`, `ClassifyMediaCollectionsAsync` und `ReloadGenres` können daher manuelle Werte überschreiben.
- Die vorhandenen Scan-Flags `Changed`, `Classifyable`, `ClassifiedAt` und `ScanDueAt` beschreiben Verarbeitung, nicht Benutzerbesitz der Metadaten.
