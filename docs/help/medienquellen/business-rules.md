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

## Altersfreigabe: Normalisierung auf kanonische Stufen

**Beschreibung:** Rohwerte aus NFO (`<mpaa>`/`<certification>`) oder TMDB werden auf bekannte Freigabestufen normalisiert, damit Anzeige und spätere Bereichsvergleiche („≤ FSK 12") einheitliche Werte vorliegen haben. Der Rohwert bleibt zusätzlich erhalten.

**Bedingungen:**
- Jede Auswertung — NFO-Parsing, Scan, Backfill und Admin-Editiermodus — läuft über denselben Normalizer.

**Verhalten:**
- „FSK 12", „FSK12", „FSK-12" → `FSK 12` (gültige Stufen: 0/6/12/16/18); ungültige FSK-Zahlen bleiben Rohwert.
- „Rated PG-13" → `PG-13`; Länderpräfixe („Germany:16", „DE:6", „US:PG-13") werden am Doppelpunkt abgetrennt. MPAA-Mapping: G→0, PG→8, PG-13→13, R→17, NC-17→18. Nackte Zahlen („16") → `FSK 16`, sofern gültig.
- Unbekannter Wert → `AgeRating` = getrimmter Rohwert, `AgeRatingMinAge` = `null` (wird angezeigt, ist aber nicht bereichsvergleichbar).
- Leere Eingabe → alle drei Felder `null`.

**Umsetzung:** `AgeRatingNormalizer.Normalize`/`ExtractFromXml` (static, zustandslos). `AgeRatingNormalizer.CanonicalOptions` ist die einzige Quelle der Auswahlwerte in `AgeRatingSelect`, damit UI und Normalisierung nicht driften.

## Altersfreigabe: Quellenpriorität und Überschreibschutz

**Beschreibung:** Mehrere Quellen können eine Freigabe liefern — die NFO hat Vorrang, die Online-Quelle ergänzt nur fehlende Werte, manuell gepflegte Einträge werden nie überschrieben.

**Verhalten:**
- NFO-`<mpaa>`/`<certification>` vorhanden → `ITmdbAgeRatingService` wird nicht aufgerufen.
- NFO ohne Freigabe + `Tmdb:Enabled` + `ApiKey` → `TryApplyOnlineAgeRatingAsync` setzt `AgeRatingRaw` = „{Land}:{Certification}" (Länderwahl `PreferredCountry` Default „DE" → „US" → erste Angabe) und normalisiert ihn.
- `IsManuallyEdited` ⇒ Scan und `BackfillMissingAgeRatingsAsync` überspringen den Eintrag vollständig — auch ein nachträglich in der NFO geänderter Wert wird nicht mehr übernommen.
- Admin-Editiermodus: `ValidateAgeRatingValue` akzeptiert nur kanonische Werte (`MinAge != null`) oder den bereits gespeicherten nicht-kanonischen Bestandswert (UI-Option „übernommen"); `ValidateNoAgeRating` lehnt gesetzte Werte für `moviecollection`/`tvshowseason`/`tvshowepisode` ab; Längenlimit 64 Zeichen (`MetadataEditModel.MaxAgeRatingLength`); leerer Wert löscht die Freigabe.

**Umsetzung:** `MediaSourceClassifier.TryApplyOnlineAgeRatingAsync` (nur wenn `AgeRatingRaw == null`), `MediaMetadataEditorService.{ApplyAgeRating, ValidateAgeRatingValue, ValidateAgeRatingLength, ValidateNoAgeRating}`, eintragsweiter `IsManuallyEdited`-Schutz.

## „Im Menü ausblenden" ist rein kosmetisch

**Beschreibung:** Das Merkmal `HiddenInMenu` einer Quelle unterdrückt ausschließlich deren Eintrag im Navigationsmenü — für Administratoren und Anwender gleichermaßen. Es ist kein Zugriffsschutz und verändert keine fachliche Verarbeitung.

**Bedingungen:**
- `MediaSource.HiddenInMenu == true`, gesetzt über die Checkbox `Im Menü ausblenden` in `MediaSourceAdminDetails.razor` (nur Administratoren).

**Verhalten:**
- `NavMenu.razor` filtert die Quelle clientseitig aus `mediaSources` heraus (`Where(s => !s.HiddenInMenu)` in `OnInitializedAsync`); die Überschrift „Quellen" entfällt, wenn alle freigegebenen Quellen versteckt sind.
- Unverändert bleiben: Freigabelogik (`MediaSourceUsers`, `IUnlockedMediaService`), Scanner und Klassifizierung, TV-Sender-Planungslogik (`ProgramScheduleService`, Füller-Regeln, `ProgramClipGroup`-Kandidaten inkl. der Materialauswahl „Clip-Quelle" in `TVSenderAdmin.razor`), die Admin-Quellenliste (`MediaSourceAdmin.razor`) und der Direktaufruf `/mediasource/{id}` (`SourcesController.GetSource` prüft nur die Freigabe).
- `SourcesController.GetSources` liefert versteckte Quellen weiterhin in der Liste aus — die Filterung ist bewusst clientseitig, damit das `DtoMediaSource.HiddenInMenu`-Flag für Listenkonsumenten (z. B. externe Clients) nutzbar bleibt.
- Eine Änderung des Merkmals wirkt im Menü bereits angemeldeter Clients erst beim nächsten Laden des `NavMenu` (gleiches Bestandsverhalten wie bei Quell-Umbenennungen).

**Umsetzung:** `NavMenu.OnInitializedAsync` (Filterung), `MediaSourceAdminDetails` (Checkbox `InputCheckbox` an `editSource.HiddenInMenu`, manuelle `editSource`-Kopie), `MediaSourceExtensions.Update` (Kopierpfad beim Speichern), Migration `AddMediaSourceHiddenInMenu`.
