# Bestandsaufnahme: Editiermodus für Medienmetadaten

## Zusammenfassung

Die Anforderung betrifft eine bestehende Blazor-Server-Oberfläche mit zwei kombinierten Detailseiten und einem EF-Core-Domänenmodell. Filme, Filmsammlungen, Serien, Staffeln und Episoden werden über `ItemsController` als verschachtelte DTOs geladen. Schreiben von Metadaten aus der Oberfläche ist derzeit nicht vorgesehen. Die Scan-/Klassifizierungslogik aktualisiert vorhandene Datensätze direkt aus NFO-Dateien, sodass ein persistenter manueller Überschreibschutz neu eingeführt und an allen Klassifizierungspfaden berücksichtigt werden muss.

## Betroffene Bereiche

| Bereich | Bestand | Relevanz |
| --- | --- | --- |
| UI | `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor`, `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor` | Editiermodus, Dirty-State, Kontextwechsel, Speichern/Abbrechen |
| DTO/Client/API | `VideoWebPlayer.Client/Models/DtoMovie.cs`, `VideoWebPlayer.Client/VideoWebPlayerClient.cs`, `VideoWebPlayer/Controllers/ItemsController.cs` | Editierbare Daten laden, Updateverträge und korrekte Objektzuordnung |
| Datenmodell | `VideoWebPlayer/Data/MediaBaseEntry.cs`, `Movie.cs`, `MovieCollection.cs`, `TVShow.cs`, `TVShowSeason.cs`, `TVShowEpisode.cs`, Genre-Linktabellen | Felder, Beziehungen und persistenter manueller Schutz |
| Scan/Klassifizierung | `VideoWebPlayer/Services/MediaSourceScanner.cs`, `MediaSourceScanService.cs`, `MediaSourceClassifier.cs` | Schutz bei initialem Scan, NFO-Änderung und Genre-Neuaufbau |
| Tests | `VideoWebPlayer.Tests` | API-, Persistenz-, Scan- und UI-Regressionsfälle |

## Detaildokumente

- [UI-Bestandsaufnahme](inventory/ui.md)
- [Datenmodell- und API-Bestandsaufnahme](inventory/data-and-api.md)
- [Scan- und Klassifizierungsbestandsaufnahme](inventory/scan-and-classification.md)
- [Tests, Authentifizierung und Betrieb](inventory/tests-and-security.md)

## Wichtige Befunde

1. Die Anforderung nennt einheitliche Felder für alle fünf Kontexte, das Modell hat diese aber nicht symmetrisch: `MovieCollection` ist `MediaEntry` und `TVShowSeason` besitzt keine eigenen Plot-/Genre-Felder.
2. Die vorhandene DTO-Ausgabe ist lesend und verschachtelt; es gibt keinen generischen Metadaten-Update-Endpunkt.
3. Die Klassifizierung überschreibt bestehende Metadaten direkt. Ein neues Schutzmerkmal muss nicht nur im Hauptklassifizierer, sondern auch in `ReloadGenres` und der Collection-Ableitung berücksichtigt werden.
4. Die vorhandene Testbasis eignet sich für Persistenz- und Scanregressionen, enthält aber noch keine Editier- oder UI-Interaktionstests.

## Offene Punkte für die Planung

- Datumssemantik und Eingabeformat je Objektkontext.
- Genrequelle, Mehrfachauswahl und Speicherung einschließlich Staffel-/Sammlungsmodell.
- Bestätigungsdialog und Berechtigungsregel für Metadatenänderungen.
- Entscheidung, ob der manuelle Schutz je Objekt aufgehoben werden kann und wie das fachlich ausgelöst wird.
