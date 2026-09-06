# Kundenanforderung: Fehlerhaft implementierte Freischaltungsprüfung in Playlist-Einträgen

## Fachliche Zusammenfassung

Die Zugriffsvalidierung für Playlist-Einträge in `PlaylistService.cs` ist semantisch falsch implementiert. Sie ignoriert den regulären Quellenzugriff (`MediaSourceUsers`) und gibt für die Medientypen `Movie`, `TVShowSeason` und `TVShowEpisode` immer `false` zurück, auch für vollständig berechtigte Benutzer. Die korrekte Regel der Anwendung folgt dem Prinzip `hasSourceAccess || isUnlocked`: Ein Titel ist zugänglich, wenn der Benutzer entweder regulären Zugriff auf die zugrunde liegende Mediaquelle hat ODER der Titel für ihn individuell freigeschaltet wurde. Der Fix muss diese Logik für alle fünf unterstützten Medientypen korrekt implementieren, dabei die bestehende Referenzimplementierung aus `ItemsController.cs` spiegeln und die Bulk-Loading-Effizienz über alle Verwendungsstellen hinweg bewahren.

## Betroffene Klassen und Komponenten

### Logikklassen / Services
- `VideoWebPlayer/Services/PlaylistService.cs`
  - Methode: `IsEntryAccessible` bzw. Bulk-Loading-Variante (Name zu prüfen)
  - Methoden, die diese nutzen:
    - `GetPlaylistEntriesAsync`
    - `GetPlaylistEntriesPagedAsync`
    - `AddMediaToPlaylistAsync` / `BuildAddResultAsync`
  - Gemeinsame Kernlogik (bereits vorhanden):
    - `BuildEntryDtosAsync`

### Referenz-Implementierung (zu spiegeln)
- `VideoWebPlayer/Controllers/ItemsController.cs`
  - Methoden: `IsUnlockedAsync`, `EnsureAccessAsync`
  - Verwendete Auflösungslogik für die Hierarchie:
    - Film → Filmsammlung
    - Episode → Staffel → Serie
    - Staffel → Serie

### Datenmodell (zu unterstützen)
- `Movie` (Film)
- `TVShowEpisode` (Episode)
- `TVShowSeason` (Staffel)
- `TVShow` (Serie)
- `MovieCollection` (Filmsammlung)

### Abhängigkeiten
- `IUnlockedMediaService` (bereits im Einsatz)
- `MediaSourceUsers` (regulärer Quellenzugriff, derzeit ignoriert)

### Tests
- Bestandstests in `VideoWebPlayer.Tests/Services/PlaylistServiceTests.cs` oder äquivalent
- Zu ergänzen/zu korrigieren mit Szenarien für:
  - Zugriff über Mediaquelle, keine individuelle Freischaltung → **zugänglich**
  - Keine Mediaquelle-Zugriff, aber individuell freigeschaltet → **zugänglich**
  - Weder Quelle noch Freischaltung → **nicht zugänglich**
  - Tests für mehrere Medientypen (aktuell nur ein Typ dokumentiert)

### Dokumentation
- `docs/help/playlists-api.md` (derzeit fehlerhaftes Verhalten als beabsichtigt dokumentiert)
- `README.md` (ggf. Anpassung erforderlich)

## Implementierungsansatz

### Zugriffsregel (Kernlogik)
```
isAccessible = hasSourceAccess OR isUnlocked
```

### Hierarchische Auflösung (Cascading Resolution)
Die bereits in `PlaylistService` implementierte Hierarchie-Auflösungslogik wird für den Zugriff genutzt:
- **Film**: Zugriff prüfen über zugrunde liegende Filmsammlung
- **Episode**: Zugriff prüfen über zugrunde liegende Staffel → Serie
- **Staffel**: Zugriff prüfen über zugrunde liegende Serie
- **Serie**: Direkter Zugriff auf Media-Source
- **Filmsammlung**: Direkter Zugriff auf Media-Source

### Bulk-Loading-Effizienz
- Alle drei Verwendungsstellen (`GetPlaylistEntriesAsync`, `GetPlaylistEntriesPagedAsync`, `AddMediaToPlaylistAsync`) nutzen bereits die gemeinsame Kernlogik `BuildEntryDtosAsync`
- Die Zugriffsvalidierung muss in diese zentrale Methode integriert werden
- Es darf keine N+1-Abfragen verursachen (alle erforderlichen Zugriffsinformationen müssen in einer oder wenigen Bulk-Queries geladen werden)

### Implementierungsstrategie
1. Referenz-Implementierung in `ItemsController.cs` analysieren (Methoden `IsUnlockedAsync`, `EnsureAccessAsync`)
2. Entsprechende Logik in `PlaylistService` als separater, testbarer Service-Method implementieren oder integrieren
3. Bulk-Load aller erforderlichen Zugriffsinformationen (Media-Source-Zuordnungen, Unlock-Status) vor der DTO-Generierung
4. Für jeden Eintrag die Regel `hasSourceAccess || isUnlocked` evaluieren
5. Bestehende Tests erweitern und neue Testfälle für alle Medientypen und Zugriffsszenarios hinzufügen

## Konfiguration

Nicht erforderlich. Die Zugriffsregel folgt dem kanonischen Prinzip der Anwendung und wird zentral in den bestehenden Dependency-Injection-Konfigurationen über `IUnlockedMediaService` und `MediaSourceUsers` gesteuert.

## Offene Fragen / Klärungsbedarfe

1. **Aktuelle Implementierung prüfen**: Wie sieht die aktuelle fehlerhafte Logik in `IsEntryAccessible` bzw. der Bulk-Loading-Methode konkret aus? (Name der Methode, genaue Logik)

2. **Test-Coverage**: Welche Szenarien werden in den bestehenden Tests bereits abgedeckt? Welche fehlen?

3. **Performance-Anforderungen**: Gibt es spezifische Anforderungen an die maximale Anzahl von Playlist-Einträgen, die in einer Abfrage geladen werden können?

4. **Fehlerbehandlung**: Wie sollen Fehler bei der Auflösung der Mediahierarchie behandelt werden (z. B. wenn eine referenzierte Serie nicht existiert)?

5. **Cascade-Verhalten**: Soll ein Eintrag als zugänglich markiert werden, wenn die Quelle zugänglich ist, aber das Kind-Objekt (z. B. die Episode) selbst nicht existiert oder gelöscht wurde?

## Lifecycle-Workflow-Schritte

1. **Plan**: Architektur und Implementierungsstrategie unter Berücksichtigung der bestehenden `ItemsController`-Implementierung
2. **Inventory**: Code-Analyse der fehlerhafte Logik und Referenzimplementierung
3. **Implement**: Korrektur in `PlaylistService`, Bulk-Loading-Optimierung
4. **Write Tests**: Testfälle für alle Szenarien und Medientypen
5. **Update Documentation**: `docs/help/playlists-api.md` und README korrigieren
6. **Verify**: Manuelles Testen mit Playlists verschiedener Medientypen
7. **Commit**: Finaler Commit auf dem Feature-Branch mit aussagekräftiger Commit-Message
