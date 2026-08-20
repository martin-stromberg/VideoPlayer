# Code-Review: Editiermodus fuer Medienmetadaten

Status: Befunde vorhanden

## Befunde

1. **Hoch - Manuell umbenannte Serien werden beim naechsten Scan nicht stabil wiedergefunden.**  
   Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs:419`  
   `CreateOrUpdateTVShow` sucht bestehende Serien nur ueber `MediaSourceId` und den aktuellen XML-Titel (`s.Name == showName`). Wird eine Serie manuell umbenannt und damit `IsManuallyEdited = true`, findet der Scanner sie bei unveraendertem NFO-Titel nicht mehr und legt eine zweite `TVShow` fuer dieselbe `CollectionId` an. Der Schutz verhindert dadurch zwar Ueberschreiben am alten Datensatz, erzeugt aber Duplikate und verschiebt neue Episoden/Staffeln auf den neuen Datensatz. Filme wurden bereits ueber `MovieMediaItems.MediaItemId` stabilisiert (`MediaSourceClassifier.cs:715`), fuer Serien fehlt ein entsprechender stabiler Lookup ueber `CollectionId`.

2. **Hoch - Neue TV-Show-Episoden koennen an eine falsche Staffel gehaengt werden, wenn eine Staffel manuell umbenannt wurde.**  
   Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs:561`  
   Episoden werden per `TVShowSeasonId` und Episodennummer gesucht. Wenn die betroffene Staffel nach manueller Umbenennung im Scan nicht mehr anhand des Namens gefunden wird, entsteht eine neue Staffel; danach findet der Episoden-Lookup die bereits vorhandene Episode in der alten Staffel nicht mehr und legt sie erneut an. Das ist dieselbe Datenverdopplung auf Staffel-/Episodenebene und kann Wiedergabestatus, Favoriten und manuelle Metadaten auseinanderlaufen lassen. Es fehlt ein Test fuer manuell umbenannte Staffeln/Episoden mit erneutem Scan.

3. **Mittel - Der Schreibendpunkt prueft nur `IsAdmin=True`, aber keine Medienquellen-Zuordnung oder objektspezifische Zugriffserlaubnis.**  
   Datei: `VideoWebPlayer/Controllers/ItemsController.cs:71`  
   `UpdateMetadata` delegiert nach der Admin-Claim-Pruefung direkt an `MediaMetadataEditorService.UpdateAsync`. Der Service laedt Datensaetze nur per ID (`MediaMetadataEditorService.cs:77`, `:94`, `:106`, `:123`, `:138`). Falls Administratoren nicht als globale Superuser fuer alle Quellen gelten, kann ein Admin mit geratenen IDs Metadaten in fremden Medienquellen aendern. Bestehende Lese-/Stream-Pfade verwenden teilweise `MediaSourceUsers`-Checks, der neue Schreibpfad nicht. Es fehlt ein Controller-/Integrationstest fuer "Admin ohne Quellenzugriff darf nicht speichern".

4. **Mittel - Encoding-Korruption in neu beruehrten Dateien ist sichtbar.**  
   Dateien: `VideoWebPlayer/Controllers/ItemsController.cs:206`, `:317`, `:354`, `:388`; `VideoWebPlayer/Data/MediaBaseEntry.cs:6`  
   Mehrere deutsche Strings enthalten Ersatzzeichen wie `f�r`, `Eintr�ge`, `Ung�ltiger Medientyp`. Im Diff ist das als `fï¿½r`/`Ungï¿½ltig` sichtbar. Das betrifft Logs, BadRequest-Antworten und Kommentare und sollte vor dem Commit auf korrektes UTF-8 oder bewusst ASCII-normalisierte Schreibweise korrigiert werden. Es gibt weitere aeltere Encoding-Probleme im Bestand; fuer diese Anforderung sind mindestens die neu veraenderten Stellen zu bereinigen.

5. **Mittel - TV-Show-Datumssemantik ist inkonsistent und nicht getestet.**  
   Dateien: `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor:485`, `VideoWebPlayer/Services/MediaMetadataEditorService.cs:111`  
   Die UI nutzt fuer Serien `ReleaseDate`, fuer Staffeln/Episoden `PremieredAt`. Der Service speichert bei Serien nur `ReleaseDate` und ignoriert `PremieredAt`. Die Anforderung nennt `ReleaseDate` und `PremieredAt`; ohne typgebundene Validierung/Tests kann ein Client fuer `tvshow` gleichzeitig beide Werte senden, wobei `PremieredAt` still verworfen wird. Fuer Staffeln/Episoden wird umgekehrt ein irrtuemlich gesetztes `ReleaseDate` still ignoriert. Das sollte serverseitig eindeutig validiert und getestet werden.

6. **Niedrig - Der neue Metadaten-Service ist nur teilweise getestet.**  
   Datei: `VideoWebPlayer.Tests/Services/MediaMetadataEditorServiceTests.cs`  
   Tests decken Movie, Genre-Optionen, Staffel-Ablehnung von Plot/Genres und Episode ab. Es fehlen Tests fuer `TVShow`-Genre-Speicherung, `MovieCollection`, Admin-/Nicht-Admin-Verhalten des Controllers, falsche Objekt-IDs, fremde Medienquellen, leere/zu lange Titel, Plot-Laenge sowie Scan-Neulauf nach manueller Umbenennung. Gerade die beiden Duplikat-Befunde oben waeren mit Regressionstests gut absicherbar.

## Gezielte Encoding-Pruefung

Der Hinweis zu `ItemsController.cs` ist bestaetigt. In der aktuellen Datei stehen u.a. `f�r`, `Eintr�ge`, `Ung�ltiger Medientyp` und `Ung�ltige ID`. Im Git-Diff erscheinen diese als typische doppelt fehlinterpretierte Sequenzen (`fï¿½r`, `Ungï¿½ltig`). Die neu hinzugefuegten Strings in `UpdateMetadata` selbst sind ASCII-normalisiert (`duerfen`, `Ungueltige`) und nicht betroffen; die Datei enthaelt aber durch die Aenderung an beruehrten Zeilen weiterhin korrupt dargestellte deutsche Strings.

## Fehlende Tests

- Controller-/API-Tests fuer Admin-Claim, Nicht-Admin, fehlende Anmeldung und Medienquellen-Zugriff beim Speichern.
- Scan-Regression fuer manuell umbenannte `TVShow`, `TVShowSeason` und `TVShowEpisode`.
- Typabhaengige Datumsvalidierung fuer `ReleaseDate`/`PremieredAt`.
- UI- oder bUnit-Tests fuer Dirty-Dialog, Speichern/Abbrechen und Genre-Freitexteingabe.

## Review-Umfang

Geprueft wurden die aktuellen Workspace-Diffs und die direkt betroffenen Dateien: Controller, DTOs, Client, Razor-Views, `MediaMetadataEditorService`, `MediaSourceClassifier`, Datenmodell, Migrationen und neue Tests. Tests wurden in diesem Review-Schritt nicht ausgefuehrt.
