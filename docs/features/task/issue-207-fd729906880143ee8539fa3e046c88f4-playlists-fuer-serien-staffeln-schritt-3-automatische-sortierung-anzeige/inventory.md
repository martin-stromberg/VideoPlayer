# Bestandsaufnahme: Nachbesserung Entwicklungsschritt 3 – Playlists für Serien-Staffeln

## Überblick

Diese Bestandsaufnahme dokumentiert den bestehenden Projektcode zur Anforderung "Nachbesserung Entwicklungsschritt 3" für das Playlist-Feature. Der Fokus liegt auf drei unvollständigen Aspekten:

1. **Titelbild (Poster)** zu Playlist-Einträgen hinzufügen
2. **Freischaltungsprüfung** mit `IUnlockedMediaService` umsetzen
3. **"Nicht startbar"-Logik** vom Entfernen-Button auf geplante Abspielen-Aktion verschieben

Analysiert wurden Datenmodelle, Services, UI-Komponenten, Interfaces und Tests.

---

## Zusammenfassung der Befunde

### Was existiert bereits:

✅ **Freischaltungs-Infrastruktur:**
- `IUnlockedMediaService` Interface und Implementierung `UnlockedMediaService`
- `UnlockedMediaEntry` Datenbankentität für Benutzer-Freischaltungen
- `IAuthService` zur Ermittlung des aktuellen Benutzers
- E2E-Test-Infrastructure mit Playwright

✅ **Bild-Ladungskonvention:**
- `MediaBaseEntry` hat `PosterPictureId`, `BannerPictureId`, `FanartPictureId`
- `MediaBox.razor` Komponente für Bildanzeige mit Fallback auf Placeholder
- `MediaBaseEntryList.razor` zeigt URL-Konvention: `/api/pictures/{id}?access_token={token}`
- CSS-Klasse `.media-poster` für Bilder

✅ **Playlist-Detail-UI:**
- `PlaylistDetail.razor` mit Tabellenstruktur für Einträge
- Virtualisierung mit `Virtualize<T>` für paginierte Einträge
- CSS-Klasse `.opacity-50 text-muted` bereits auf Zeile angewendet für `!entry.IsAccessible`
- E2E-Test `PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()` als Baseline

✅ **Pagination und Datenladung:**
- `PlaylistService.GetPlaylistEntriesPagedAsync()` für paginierte Einträge
- `LoadTitlesForMediaRefsAsync()` Pattern für skalierte Datenladung (nur aktuelle Seite)
- `MediaTypeHandlers` Dictionary mit Datenladungs-Logik pro Medientyp

---

### Was fehlt oder ist unvollständig:

❌ **Titelbild-Transport:**
- `DtoPlaylistEntry` hat kein `PosterPictureId` Feld
- `PlaylistService.ToDto()` befüllt keine Bild-IDs

❌ **Freischaltungsprüfung:**
- `PlaylistService` hat `IUnlockedMediaService` nicht injiziert
- `PlaylistService.ToDto()` setzt `IsAccessible` hart auf `true` (Zeile 546)
- Keine echte Freischaltungs-Prüfung pro Eintrag
- `ToDto()` ist synchron, `IUnlockedMediaService.IsUnlockedAsync()` ist asynchron → Refaktorierung nötig

❌ **UI-Bildspalte:**
- `PlaylistDetail.razor` hat keine Bildspalte in der Tabelle
- Keine Nutzung von `MediaBox.razor` oder `img`-Tag mit Bild-URL

❌ **Nicht startbar-Logik:**
- "Entfernen"-Button hat `disabled="@(!entry.IsAccessible)"` (Zeile 109)
- Dies ist zu entfernen (Phase 3 der Anforderung)

---

## Detaillierte Analyse

### [Datenmodelle](inventory/models.md)
Dokumentiert die bestehenden Felder in `DtoPlaylistEntry`, `PlaylistEntry`, `MediaBaseEntry` und `UnlockedMediaEntry`.

**Wichtigste Erkenntnisse:**
- `DtoPlaylistEntry` hat bereits `IsAccessible` (aktuell `true`), fehlt aber `PosterPictureId`
- `MediaBaseEntry` ist die Basis für alle Medientypen mit `PosterPictureId`, `BannerPictureId`, `FanartPictureId`
- `PlaylistEntry` (DB-Entity) hat keine Bild-IDs, diese müssen aus referenzierten Medien geladen werden

### [Logik und Services](inventory/logic.md)
Dokumentiert `PlaylistService`, `IUnlockedMediaService`, `UnlockedMediaService` und `IAuthService`.

**Wichtigste Erkenntnisse:**
- `PlaylistService` injiziert derzeit nur `ApplicationDbContext`, `EventManager`, `IOptions<PlaylistSettings>`
- `IUnlockedMediaService` benötigt `IAuthService` um aktuellen Benutzer zu ermitteln
- `UnlockedMediaService.IsUnlockedAsync()` nutzt `_authService.CurrentUser` (synchroner Zugriff via `.Result`)
- `MediaTypeHandlers` Pattern kann Vorlage sein für Bild-ID-Ladung

### [Interfaces und Contracts](inventory/interfaces.md)
Dokumentiert die Methoden und Parameter von `IPlaylistService`, `IUnlockedMediaService`, `IAuthService`.

**Wichtigste Erkenntnisse:**
- `IPlaylistService.GetPlaylistEntriesPagedAsync()` ist der Einstiegspunkt für paginierte Einträge
- `IUnlockedMediaService.IsUnlockedAsync()` erfordert `DtoMediaEntry` (nicht `DtoPlaylistEntry`)
- `IAuthService.CurrentUser` ist eine Property (nicht async) - könnte Herausforderung für async `ToDto()` sein

### [UI-Komponenten](inventory/ui.md)
Dokumentiert `PlaylistDetail.razor`, `MediaBox.razor`, `MediaBaseEntryList.razor`.

**Wichtigste Erkenntnisse:**
- `PlaylistDetail.razor` nutzt Virtualisierung mit `ItemsProviderAsync()`
- `MediaBox.razor` akzeptiert `ImageUrl` Parameter, hat Fallback auf `/images/placeholder.png`
- `MediaBaseEntryList.razor` zeigt die Konvention: `PosterPictureId` → `BannerPictureId` → `FanartPictureId` → Placeholder
- Bild-URL-Format: `/api/pictures/{id}?access_token={token}`

### [Tests](inventory/tests.md)
Dokumentiert bestehende E2E-Tests und Test-Hilfsmethoden.

**Wichtigste Erkenntnisse:**
- `PlaylistDetailE2ETests` hat Baseline-Test `PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()` (alle Einträge freigeschaltet)
- Test-Fixtures `SeedMovieAsync()`, `SeedTvShowWithSeasonsAsync()` etc. sind verfügbar
- Neue Tests benötigen Test-Hilfsmethode zum Setzen/Entfernen von Freischaltungen

---

## Architektur-Hinweise

### Datenladungs-Pattern (MediaTypeHandlers)
Das Service nutzt ein `Dictionary<MediaType, MediaTypeHandler>` mit Lambda-Funktionen pro Medientyp:
```
MediaTypeHandlers[MediaType.Movie] = new MediaTypeHandler {
    LoadTitlesAsync = (db, ids, ct) => ...,
    LoadReleaseDateAsync = (db, ids, ct) => ...,
    ...
}
```

Dieses Pattern kann erweitert werden um `LoadPosterPictureIdsAsync`, um `PosterPictureId` effizient nur für die aktuelle Seite zu laden (nicht die gesamte Playlist).

### Async-Herausforderung
`PlaylistService.ToDto()` ist synchron, aber `IUnlockedMediaService.IsUnlockedAsync()` ist asynchron.
- **Option A:** `ToDto()` synchron mit Cache/Parameter halten, `IsAccessible` in separater Schleife async laden
- **Option B:** `ToDto()` in `ToDtoAsync()` umbennen, Alle Aufrufer anpassen
- **Option C:** `IUnlockedMediaService` mit Batch-Abfrage erweitern für alle Einträge einer Seite auf einmal

### Freischaltungs-Logik für Playlist-Einträge
`UnlockedMediaService.IsUnlockedAsync()` funktioniert nur für `DtoMovieCollection` und `DtoTVShow`. 
- Filme, Serien-Staffeln und Episoden geben aktuell `false` zurück
- Für Playlists mit diesen Medientypen müssen Freischaltungen anders gehandhabt werden (oder Service erweitern)

---

## Abhängigkeits-Übersicht

```
PlaylistDetail.razor
  ├─ Client.RequestPlaylistEntriesPagedAsync()
  │   └─ PlaylistService.GetPlaylistEntriesPagedAsync()
  │       ├─ ApplicationDbContext
  │       ├─ LoadValidPlaylistEntriesAsync()
  │       ├─ LoadTitlesForMediaRefsAsync() [SKALIERT auf Seite]
  │       └─ (FEHLT) IUnlockedMediaService für IsAccessible
  │           └─ IAuthService für CurrentUser
  │
  └─ DtoPlaylistEntry (DTO vom Server)
      ├─ IsAccessible (aktuell immer true)
      └─ (FEHLT) PosterPictureId
```

---

## Offene Fragen aus Anforderung

1. **IAuthService-Integration in PlaylistService:**
   - Wird `IAuthService` direkt injiziert oder über `IUnlockedMediaService` genutzt?
   - Beide haben Zugriff auf aktuellen Benutzer

2. **Async-Refaktorierung von ToDto():**
   - Wird neue `ToDtoAsync()` Methode erstellt?
   - Oder wird `IsUnlockedAsync()` batched aufgerufen?

3. **Freischaltungs-Scope:**
   - Nur für `TVShow` und `MovieCollection` (wie in `UnlockedMediaService`)?
   - Oder auch für Filme, Staffeln, Episoden?

4. **Bildladung:**
   - Nur für aktuelle Seite (wie Titel)?
   - Oder Vorladung der nächsten Seite?

---

## Nachfolgende Schritte

Diese Bestandsaufnahme dokumentiert den aktuellen Zustand. Für die Implementierung sollten die Detaildokumente konsultiert werden:

1. Start mit [Datenmodelle](inventory/models.md) - Feld `PosterPictureId` hinzufügen
2. Dann [Logik](inventory/logic.md) - Services injizieren und `ToDto()` anpassen
3. Dann [UI](inventory/ui.md) - Bildspalte hinzufügen, Button-Binding entfernen
4. Abschließend [Tests](inventory/tests.md) - E2E-Tests anpassen und erweitern
