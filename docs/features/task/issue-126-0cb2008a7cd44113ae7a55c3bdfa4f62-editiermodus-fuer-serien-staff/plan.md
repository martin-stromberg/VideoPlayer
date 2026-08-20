# Umsetzungsplan: Editiermodus für Medienmetadaten

## Ziel und Leitplanken

Der Editiermodus wird in den beiden bestehenden interaktiven Detailseiten umgesetzt:

- `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor`
- `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor`

Die Bearbeitung bleibt auf Titel, Datum, Genres und Plot beschränkt. Der aktuell angezeigte Kontext ist immer die alleinige Quelle für Laden und Speichern. Außerhalb des Editiermodus bleiben Auswahl, Favoriten, Abspielen und Download unverändert.
Für Serien, Filme und Filmsammlungen wird `ReleaseDate` bearbeitet; für Staffeln und Episoden wird `PremieredAt` bearbeitet. Es gibt kein gemeinsames, fachlich unbestimmtes Datumsfeld.
Staffeln erhalten keine neuen Genre- oder Plotfelder. Für Staffeln werden nur die im bestehenden Modell vorhandenen editierbaren Metadaten angeboten.

Für die Planung gelten folgende technische Entscheidungen:

- Es wird ein gemeinsamer, typisierter Metadaten-Updatevertrag mit einem expliziten Objekttyp verwendet. Die API darf keine frei übertragbare Entity-Bindung akzeptieren.
- `IsManuallyEdited` (oder ein gleichwertig benanntes persistentes Feld) wird auf allen fünf editierbaren Typen gespeichert. Der Status wird beim erfolgreichen Speichern gesetzt und ist unabhängig von `Changed`, `ClassifiedAt` und `ScanDueAt`.
- Die Auswahlliste verwendet genau die Genres, die `GenreAdmin.razor` ausgibt, und bindet sie an das bestehende Speicherformat (`Genres` oder `GenreNames`). Zusätzlich eingegebene Freitextgenres werden als neue Genres angelegt und anschließend ebenfalls normalisiert und dedupliziert gespeichert.
- Die Eingabefelder verwenden Blazor-Validierung. Das Datum wird als lokales `input type="date"` geführt und invariant als Datum ohne Uhrzeit übertragen.
- Der Überschreibschutz wird serverseitig erzwungen. UI-Sperren allein gelten nicht als Schutz.

## 1. Datenmodell und Migration

1. Gemeinsames Schutzfeld in `MediaBaseEntry` ergänzen; für `MovieCollection` gilt dadurch derselbe Schutz wie für Film, Serie, Staffel und Episode. Vorhandene Datensätze erhalten per Migration den Wert `false`.
2. Den Updatevertrag und die Domänenfelder für `ReleaseDate` und `PremieredAt` ausdrücklich trennen; keine stillschweigende Ableitung, die bei einem späteren Scan wieder verloren geht.
3. Für Staffeln keine neuen Genre-/Plotfelder modellieren. Die API validiert, dass für diesen Objekttyp nur tatsächlich vorhandene Felder geändert werden.
4. `ApplicationDbContext`-Konfiguration und Migration ergänzen. Prüfen, dass EF-InMemory und SQLite-Testdatenbanken das neue Feld korrekt abbilden.

## 2. API, DTOs und Autorisierung

1. In `VideoWebPlayer.Client/Models/DtoMovie.cs` die editierbaren Felder und den manuellen Schutzstatus in den Detail-DTOs abbilden. Für Genres eine stabile Liste statt UI-abhängiger Zeichenketten bereitstellen, sofern der vorhandene Vertrag das zulässt.
2. Einen Request-DTO für `ObjectType`, `Id`, `Name`, Datum, Plot und Genre-Liste einführen. IDs und Typ werden serverseitig gegen den tatsächlichen Datensatz geprüft.
3. Einen autorisierten Endpoint im Bereich `ItemsController` oder einem fachlich passenden neuen Controller ergänzen. Der Endpoint lädt den konkreten Datensatz, validiert erlaubte Felder, normalisiert Genres, setzt `IsManuallyEdited = true` und speichert atomar.
4. Den Client um eine Save-Methode mit klarer Fehlerbehandlung erweitern. Erfolgreiches Speichern liefert den aktualisierten Kontext zurück oder löst ein erneutes Laden aus; bei Fehlern bleibt der Editiermodus mit den Eingaben erhalten.
5. Die Bearbeitungsberechtigung an die bestehende Authentifizierungsinfrastruktur anbinden und serverseitig ausschließlich Administratoren das Speichern erlauben.
6. Keine sichtbare Funktion zum Aufheben des manuellen Schutzes vorsehen. Der Schutz darf nicht implizit beim Scan zurückgesetzt werden.

## 3. Scan- und Klassifizierungsschutz

1. Eine zentrale Hilfsmethode oder ein Service entscheidet, ob ein Feld aus NFO/Dateisystem übernommen werden darf: manuell geschützte Metadaten werden nicht überschrieben, technische Scanfelder dürfen weiterhin aktualisiert werden.
2. Diese Prüfung in `MediaSourceClassifier` an allen Pfaden verwenden:
   - `ClassifyAllAsync`
   - `ClassifyMediaItemsAsync`
   - `ClassifyMediaCollectionsAsync`
   - `ClassifyCollectionTreeAsync`
   - die konkrete Serien-, Staffel-, Episoden-, Film- und Sammlungsaktualisierung
3. `ReloadGenres` so ändern, dass geschützte Genrewerte und deren Linktabellen erhalten bleiben. Nicht geschützte Datensätze dürfen weiterhin vollständig aus der Informationsdatei neu aufgebaut werden.
4. Die Collection-Ableitung so anpassen, dass ein manuell geschützter Sammlungsname und die geschützten Sammlungsmetadaten nicht aus Filmen zurückgerechnet werden. Technische Mitglieder-/Pfadänderungen bleiben möglich.
5. Sicherstellen, dass derselbe Schutz für den initialen Scan, inkrementelle Scans und Scans nach geänderter NFO gilt. Die Prüfung muss vor jeder Zuweisung der geschützten Metadaten erfolgen.
6. Transaktionsgrenzen und konkurrierende Speichervorgänge prüfen: Ein Scan darf einen gerade gespeicherten manuellen Wert nicht durch einen bereits geladenen alten Entity-Zustand zurückschreiben.

## 4. Gemeinsames UI-Zustandsmodell

1. Für beide Razor-Seiten ein kleines, testbares Editierzustandsmodell einführen: `isEditing`, `editContext`, Snapshot der zuletzt geladenen Werte, aktuelle Eingaben und `isDirty`.
2. Beim Aktivieren werden Eingaben aus dem aktuell sichtbaren Objekt vorbelegt. Anzeigeelemente für Titel/Jahr/Genres, Plot und Abspielen werden im Editiermodus nicht gerendert.
3. Im Kopfbereich wird der Stift durch Speichern ersetzt; zusätzlich erscheint Abbrechen. Buttons erhalten weiterhin die vorhandenen Icons/Bedienmuster.
4. Die Genre-Auswahl wird horizontal und mehrfach auswählbar umgesetzt. Auswahländerungen aktualisieren nur den Editierzustand und setzen `isDirty`.
5. Vor jedem Kontextwechsel eine zentrale `EnsureCanLeaveEditContextAsync`-Prüfung aufrufen. Das betrifft insbesondere `SelectSeason`, `SelectEpisode`, `ShowSeason`, `SelectMovie`, `ShowCollection` sowie Kopfbereich-Wechsel zurück zu Serie bzw. Filmsammlung.
6. Bei Dirty-State einen Bestätigungsdialog mit Abbrechen und Verwerfen anzeigen. Bei Abbrechen bleibt Kontext und Eingabe unverändert; bei Verwerfen wird der Snapshot wiederhergestellt und erst danach gewechselt.
7. Beim Speichern wird nur der aktuell aktive Kontext übertragen. Nach Erfolg wird der Snapshot aktualisiert, `isDirty` zurückgesetzt und die Anzeige neu geladen; bei Fehlern erfolgt keine stille Verwerfung.
8. Die Kontextwechselregeln explizit abbilden: Serie -> Staffel/Episode, Staffel/Episode -> Serie, Sammlung -> Film und Film -> Sammlung. Beim Wechsel einer Staffel bzw. eines Films wird automatisch der zugehörige Editierkontext aktiviert.

## 5. Tests

### Domäne, API und Persistenz

- Migration/Testdaten: bestehende Datensätze starten ungeschützt; Speichern setzt den Schutz dauerhaft.
- Update je Objekttyp: Serie, Staffel, Episode, Film, Filmsammlung; korrekte ID-/Typ-Zuordnung und keine Änderung fremder Datensätze.
- Validierung: unbekannter Typ/ID, ungültiges Datum, zu lange Texte und nicht erlaubte Genrewerte.
- Autorisierung: nicht angemeldete bzw. nicht berechtigte Schreibzugriffe werden abgewiesen.
- Genres: Mehrfachauswahl, Reihenfolge-/Duplikatnormalisierung und Linktabellen.

### Scanregressionen

- Geschützte Serie, Staffel, Episode, Film und Filmsammlung behalten Titel, Datum, Plot und Genres nach initialem Scan.
- Dieselben Fälle nach geänderter NFO und nach inkrementellem Scan.
- Ungeschützte Datensätze werden weiterhin aktualisiert.
- `ReloadGenres` überschreibt keinen geschützten Genrebestand.
- Collection-Ableitung überschreibt keinen geschützten Sammlungsnamen bzw. keine geschützten Sammlungsmetadaten.
- Technische Scanfelder und neue Dateisystemmitglieder werden trotz Schutz weiter verarbeitet.
- Konflikttest für Save/Scan-Reihenfolge, soweit die vorhandene Testinfrastruktur dies reproduzierbar erlaubt.

### UI und Zustandslogik

- Aktivieren, Vorbelegung, Rendern der Eingabefelder und Umschalten Stift/Speichern.
- Dirty-State bei jeder Eingabe sowie Speichern und Abbrechen.
- Jeder Serien-/Staffel-/Episoden- und Sammlungs-/Film-Kontextwechsel fragt bei Dirty-State nach.
- Dialog: Verwerfen setzt Kontextwechsel fort; Abbrechen verwirft nichts.
- Kein Dialog bei sauberem Zustand.
- Fehlgeschlagenes Speichern behält Eingaben und Editierkontext.
- Bestehende Auswahl-, Favoriten- und Abspielfunktionen außerhalb des Editiermodus bleiben funktionsfähig.

Die UI-Tests sollen bevorzugt das Zustandsmodell und renderbare Hilfsmethoden direkt testen. Ergänzend werden Playwright-Tests verwendet, falls die bestehende Testkonfiguration die InteractiveServer-Komponenten zuverlässig hosten kann.

## 6. Verifikation und Lieferung

1. `dotnet build` für Solution und betroffene Projekte ausführen.
2. Betroffene xUnit-Tests zunächst fokussiert, anschließend die vollständige Testsuite ausführen.
3. API- und Scan-Tests mit SQLite sowie EF-InMemory ausführen, damit Migration und Relationen beide geprüft werden.
4. Plan- und Code-Review auf vollständige Typabdeckung, serverseitigen Schutz und alle Kontextwechsel durchführen.
5. Dokumentation um Bedienung, Datumssemantik, Genreauswahl und Verhalten des manuellen Schutzes ergänzen.

## Geklärte Entscheidungen

1. Für Serien, Filme und Filmsammlungen wird `ReleaseDate`, für Staffeln und Episoden `PremieredAt` bearbeitet; Eingabeformat ist `yyyy-MM-dd`.
2. Staffeln erhalten keine neuen Genre- oder Plotfelder.
3. Die Genre-Auswahlliste entspricht der Ausgabe von `GenreAdmin.razor`; zusätzliche Freitextgenres werden als neue Genres gespeichert.
4. Der Verwerfen-Dialog bietet „Änderungen verwerfen“ und „Weiter bearbeiten“.
5. Nur Administratoren dürfen Metadaten speichern.
6. Eine sichtbare Funktion zum Aufheben des manuellen Überschreibschutzes ist nicht vorgesehen.
