# Anforderung – Nachbesserung Entwicklungsschritt 3

Branch: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-3-automatische-sortierung-anzeige`

## Ziel

Drei unvollständige Aspekte der ursprünglichen Anforderung für Entwicklungsschritt 3 ("Playlist-Inhalte anzeigen und automatisch nach Erscheinungsdatum sortieren") sind nachzubessern:

1. Titelbild (Poster) zu jedem Eintrag in der Playlist-Detailansicht hinzufügen
2. Freischaltungsprüfung durch Integration mit bestehendem `IUnlockedMediaService` tatsächlich umsetzen
3. "Nicht startbar"-Logik von Entfernen-Button auf geplante Abspielen-Aktion verschieben

## Umfang

### 1. Titelbild (Poster) in der Playlist-Detailansicht

- Erweiterung `DtoPlaylistEntry` um Feld `PosterPictureId` (Typ `long?`, optional)
- Erweiterung `PlaylistService.ToDto()` um Befüllung von `PosterPictureId` pro Eintrag
- Änderung `PlaylistDetail.razor`: Ergaenzung der Tabellenzeile um eine Bildspalte, die `PosterPictureId` über `MediaBox.razor` oder ähnliche Konvention anzeigt
- Wiederverwendung bestehender Bild-Transport und -Anzeige-Konvention (vgl. `MediaBaseEntryList.razor`, dort: `GetImageUrl(entry)` unter Verwendung von `/api/pictures/{id}?access_token={token}`)
- Fallback auf Placeholder-Bild bei fehlender `PosterPictureId`

### 2. Freischaltungsprüfung (IsAccessible) mit IUnlockedMediaService

- `PlaylistService` erhält Dependency Injection von `IUnlockedMediaService`
- `PlaylistService` erhält Zugriff auf aktuellen Benutzer (über `IAuthService` oder äquivalentes Konzept)
- `PlaylistService.ToDto()` muss für jeden Eintrag `IUnlockedMediaService.IsUnlockedAsync()` aufrufen und das Ergebnis in `DtoPlaylistEntry.IsAccessible` speichern
- Dies ist ein asynchroner Aufruf, daher benötigt die Methode eine neue asynchrone Variante oder Refaktorierung
- Der E2E-Test für die Playlist-Anzeige wird angepasst, um zu prüfen, dass nicht freigeschaltete Einträge mit reduzierter Opazität angezeigt werden (visuell überprüfbar)

### 3. "Nicht startbar"-Logik verschieben

- Entfernen der `disabled="@(!entry.IsAccessible)"` Bindung vom "Entfernen"-Button in `PlaylistDetail.razor` (Zeile 109)
- Erstellung oder Ergaenzung einer Abspielen-/Start-Aktion, die für nicht freigeschaltete Einträge deaktiviert ist (falls dies in einem späteren Schritt implementiert wird)
- Beibehaltung der CSS-Klasse `.opacity-50` oder ähnliche Darstellung auf der Tabellenzeile, um visuell anzuzeigen, dass der Eintrag nicht freigeschaltet ist

## Nicht-Ziele

- Vollständige Wiedergabefunktionalität aus der Playlist heraus (ist Gegenstand eines späteren Entwicklungsschritts)
- Änderung des Datenbankschemas jenseits der bestehenden `PlaylistEntry`-Struktur
- Änderung an der Authentifizierung oder dem Freischaltungsmechanismus selbst

## Akzeptanzkriterien

### 1. Titelbild

- [ ] `DtoPlaylistEntry` enthält neues Feld `PosterPictureId` vom Typ `long?`
- [ ] `PlaylistService.ToDto()` befüllt `PosterPictureId` aus der jeweiligen Medien-Entity (Film, Serie, Staffel, Episode, Filmsammlung)
- [ ] Jeder Eintrag in `PlaylistDetail.razor` zeigt sein Titelbild an (über `MediaBox` oder direkt via `img`-Tag mit API-URL)
- [ ] Fehlendes Bild wird durch Placeholder-Bild ersetzt
- [ ] Visuelle Integration mit bestehender Tabellendarstellung kohärent

### 2. Freischaltungsprüfung

- [ ] `PlaylistService` ist mit `IUnlockedMediaService` injiziert
- [ ] `PlaylistService` hat Zugriff auf aktuellen Benutzer (Klarstellung: über bestehenden Mechanismus wie `IAuthService`)
- [ ] `DtoPlaylistEntry.IsAccessible` wird korrekt mit Freischaltungsstatus des aktuellen Anwenders befüllt (nicht hart auf `true` gesetzt)
- [ ] Nicht freigeschaltete Einträge werden in `PlaylistDetail.razor` mit reduzierter Opazität angezeigt (`.opacity-50` o.Ä.)
- [ ] E2E-Test prüft: Eintrag ohne Freischaltung wird mit reduzierter Opazität angezeigt
- [ ] Kommentar in `DtoPlaylistEntry` (Zeile 16) wird aktualisiert oder entfernt, um nicht mehr als Platzhalter zu fungieren

### 3. "Nicht startbar"-Logik

- [ ] "Entfernen"-Button in `PlaylistDetail.razor` ist nicht mehr an `IsAccessible` gebunden (Zeile 109)
- [ ] Playlist-Besitzer kann nicht freigeschaltete Einträge aus seiner Playlist entfernen
- [ ] Visuelle Darstellung (Opazität, Farbe) der Zeile bleibt erhalten
- [ ] (Zukünftig) falls Abspielen-Aktion hinzugefügt: Diese wird für `!IsAccessible` deaktiviert

## Betroffene Klassen und Komponenten

### Datenmodellklassen

- `DtoPlaylistEntry` (Datei: `VideoWebPlayer.Client\Models\DtoPlaylistEntry.cs`)
  - Neues Feld: `PosterPictureId` (Typ `long?`)

### Logikklassen / Services

- `PlaylistService` (Datei: `VideoWebPlayer\Services\PlaylistService.cs`)
  - Neue Abhängigkeit: `IUnlockedMediaService` (Injection im Konstruktor)
  - Zugriff auf aktuellen Benutzer (zu prüfen: beste Stelle für IAuthService-Integration)
  - Methode `ToDto(PlaylistEntry, string mediaTitle, string? parentMediaTitle)` (Zeile 535): Refaktorierung für Async-Befüllung von `PosterPictureId` und `IsAccessible`
  - Ggf. neue Methode: `ToDtoAsync()` mit `IUnlockedMediaService.IsUnlockedAsync()` für jeden Eintrag

### UI-Komponenten

- `PlaylistDetail.razor` (Datei: `VideoWebPlayer\Components\Playlists\PlaylistDetail.razor`)
  - Tabellenstruktur: Ergaenzung einer Bildspalte in Thead/Tbody (vor oder nach "Typ")
  - Tabellenzeile (ab Zeile 103): Ergaenzung um Bildspalte mit MediaBox oder img-Tag
  - Button "Entfernen" (Zeile 109): Entfernen von `disabled="@(!entry.IsAccessible)"`

### Tests

- E2E-Test für Playlist-Anzeige (zu lokalisieren)
  - Anpassung: Prüfung, dass nicht freigeschaltete Einträge mit reduzierter Opazität angezeigt werden
  - Klarstellung: Test darf nicht bestätigen, dass *kein* Eintrag ausgegraut ist

## Implementierungsansatz

### Phase 1: Datentransport (PosterPictureId)

1. `DtoPlaylistEntry.PosterPictureId` hinzufügen
2. `PlaylistService.ToDto()` für jeden Eintrag `PosterPictureId` aus der zugehörigen Media-Entity (Film, Serie, Staffel, Episode, Filmsammlung) laden
   - Nutzung bestehender Architektur: `MediaTypeHandlers` (ähnlich wie bei `LoadTitlesAsync`) erweitern oder separate Methode `LoadPosterPictureIdsAsync()` ergänzen
   - Scaling: Nur für die aktuell anzuzeigende Seite (`GetPlaylistEntriesPagedAsync`), nicht für die gesamte Playlist

### Phase 2: Freischaltungsprüfung (IsAccessible)

1. `PlaylistService` erhält `IUnlockedMediaService` per Konstruktor-Injection
2. Aktuellen Benutzer auflösen (Best Practice: über `IAuthService.CurrentUser` oder äquivalentes)
3. `PlaylistService.ToDto()` (oder neue `ToDtoAsync()`) ruft für jeden Eintrag `IUnlockedMediaService.IsUnlockedAsync()` auf
4. Ergebnis in `DtoPlaylistEntry.IsAccessible` schreiben
5. Kommentar in `DtoPlaylistEntry` überprüfen und ggf. aktualisieren

### Phase 3: UI-Anpassung (PlaylistDetail.razor)

1. Tabellenzeile ergaenzen um Bildspalte (mit Poster oder Placeholder)
2. ImageUrl berechnen (Konvention: `/api/pictures/{PosterPictureId}?access_token={token}`)
3. `disabled`-Bindung vom "Entfernen"-Button entfernen
4. Bestätigung: `.opacity-50` bleibt auf Zeile wirksam für nicht freigeschaltete Einträge

### Phase 4: Test-Anpassung

1. E2E-Test für Playlist-Anzeige anpassen oder neu erstellen
2. Prüfen: Mindestens ein nicht freigeschalteter Eintrag wird mit visueller Reduktion angezeigt
3. Prüfen: "Entfernen"-Button ist nicht deaktiviert für nicht freigeschaltete Einträge

## Konfiguration

- Keine neue Konfiguration erforderlich
- Freischaltung folgt bestehendem Mechanismus (`IUnlockedMediaService`)

## Offene Fragen

1. **IAuthService-Integration**: Wie wird der aktuelle Benutzer in `PlaylistService.ToDto()` am besten ermittelt?
   - Option A: HttpContext-Zugriff über Dependency Injection
   - Option B: Benutzer-ID als Parameter an `PlaylistService.ToDto()` oder `GetPlaylistEntriesPagedAsync()` übergeben
   - Option C: `IUnlockedMediaService` ist bereits mit aktuellem Benutzer vertraut (prüfen: Implementierung in `UnlockedMediaService`)

2. **Asynchrone Änderung bei ToDto**: Wird `ToDto()` synchron (Status aus Parameter) oder asynchron (IUnlockedMediaService.IsUnlockedAsync abfragen)? Dies beeinflusst Signatur und Aufrufer.

3. **Bildladung bei Pagination**: Wird `PosterPictureId` für alle Einträge oder nur für die aktuelle Seite geladne? (Empfehlung: Nur für Seite, wie `LoadTitlesForMediaRefsAsync` bei Titeln)

4. **E2E-Test-Strategie**: Soll ein bestehendes Test-Fixture angepasst oder ein neuer Test erstellt werden? Wie wird "Freischaltung entfernen" im Test simuliert (Datenbank-Setup)?
