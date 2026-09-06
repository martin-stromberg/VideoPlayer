# Bestandsaufnahme: Fehlerhaft implementierte Freischaltungsprüfung in Playlist-Einträgen

Diese Analyse dokumentiert den bestehenden Code bezüglich der fehlerhaften Freischaltungsprüfung in `PlaylistService.cs`, die Referenzimplementierung in `ItemsController.cs`, die beteiligten Datenmodelle, das aktuelle Test-Coverage und die fehlerhafte Dokumentation.

## Zusammenfassung

**Kernproblem:** Die Methode `IsEntryAccessible` in `PlaylistService` (Zeilen 660–671) ist semantisch falsch implementiert:
- Sie ignoriert vollständig den regulären Quellenzugriff (`MediaSourceUsers`)
- Sie gibt für alle Medientypen außer `MovieCollection` und `TVShow` hardcodiert `false` zurück
- Die korrekte Regel `hasSourceAccess OR isUnlocked` wird nicht angewendet
- Dies betrifft alle drei Verwendungsstellen: `GetPlaylistEntriesAsync`, `GetPlaylistEntriesPagedAsync`, `AddMediaToPlaylistAsync` über `BuildEntryDtosAsync`

**Referenzimplementierung vorhanden:** `ItemsController.cs` zeigt die korrekte Logik in `EnsureAccessAsync` (Zeilen 358–370) und `IsUnlockedAsync` (Zeilen 372–387) mit hierarchischer Auflösung:
- Film → Filmsammlung
- Episode → Staffel → Serie
- Staffel → Serie

**Test-Coverage unvollständig:**
- Nur TVShow und MovieCollection sind getestet
- Keine Tests für Movie, TVShowEpisode, TVShowSeason
- Keine Tests für regulären Quellenzugriff (`MediaSourceUsers`)
- Drei Zugriffs-Szenarien fehlen: nur Quelle, nur Freischaltung, weder noch

**Dokumentation fehlerhaft:** `docs/help/playlists-api.md` (Zeilen 301–307) dokumentiert das fehlerhafte Verhalten als beabsichtigt und muss korrigiert werden.

**Abhängigkeiten vorhanden:** `IUnlockedMediaService` ist bereits in `PlaylistService` integriert, bietet aber nur Bulk-Load für `MovieCollection`- und `TVShow`-IDs.

---

## Details

- [Logik und Methoden](inventory/logic.md)
- [Datenmodelle](inventory/models.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
- [Dokumentation](inventory/documentation.md)
