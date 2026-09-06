# Bestandsaufnahme: UI-Komponenten

## `PlaylistDetail.razor`
Datei: `VideoWebPlayer\Components\Playlists\PlaylistDetail.razor`

### Struktur und Funktionalität

**Seite:** `/playlists/{id:long}` (Zeile 1)

**Benutzer-Authentifizierung:**
- `AuthenticationStateProvider` für Auth-Prüfung (Zeile 6, 174)
- `IAuthService` nicht direkt injiziert (wird über Client genutzt)

**Datenfluss:**
1. `OnInitializedAsync()` (Zeile 171) → `LoadPlaylistAsync()` (Zeile 178)
   - Lädt Playlist-Metadaten und erste Seite von Einträgen
2. `LoadInitialPageAsync()` (Zeile 204) 
   - Ruft `Client.RequestPlaylistEntriesPagedAsync()` auf
3. Virtualisierung via `Virtualize<DtoPlaylistEntry>` (Zeile 101)
   - `ItemsProviderAsync()` lädt weitere Seiten beim Scrollen (Zeile 229)

### Tabelle: Playlist-Einträge (Zeile 90-120)

**Aktuelle Spalten:**
- `Typ` (MediaType)
- `Titel` (MediaTitle)
- `Sammlung` (ParentMediaTitle)
- `Hinzugefügt` (AddedAt)
- `Aktionen` (Entfernen-Button)

**Status nicht freigeschalteter Einträge:**
- Zeile 103: CSS-Klasse `.opacity-50 text-muted` auf `<tr>` wenn `!entry.IsAccessible`
- Zeile 109: "Entfernen"-Button hat `disabled="@(!entry.IsAccessible)"`
  - **HINWEIS:** Dies wird zu entfernen sein (Anforderung Phase 3)

### Fehlende Implementierungen (gemäß Anforderung):

1. **Fehlende Bildspalte:**
   - Keine Spalte für `PosterPictureId`
   - Sollte `MediaBox.razor` oder direktes `<img>`-Tag mit `/api/pictures/{PosterPictureId}?access_token={token}` sein
   - Fallback auf `/images/placeholder.png`

2. **Nicht vollständige Freischaltungsprüfung:**
   - `entry.IsAccessible` ist aktuell immer `true` (Server setzt hart auf true)
   - Opazität wird angewendet, aber es gibt keine echten nicht freigeschalteten Einträge

### Event-Handler:
- `OnInitializedAsync()` - Initialisierung
- `LoadPlaylistAsync()` - Laden der Playlist
- `AddEntryAsync()` - Hinzufügen von Einträgen (Zeile 266)
- `RemoveEntryAsync()` - Entfernen von Einträgen (Zeile 298)
- `OpenEditForm()` / `HandleSavedAsync()` - Bearbeiten-Dialog
- `RequestDelete()` / `ConfirmDeleteAsync()` - Lösch-Bestätigung

---

## `MediaBox.razor`
Datei: `VideoWebPlayer\Components\Shared\Media\MediaBox.razor`

### Zweck:
Wiederverwendbare Komponente zur Anzeige von Medieneinträgen mit Poster, Titel, Subtitle und Aktionsmenü.

### Parameter:
- `Title` (required) - Haupttitel
- `Subtitle` - Untertitel (z.B. Sammlung)
- `ImageUrl` (default: `/images/placeholder.png`) - URL zum Bild
- `LinkUrl` (default: `#`) - Navigations-URL
- `CardKey` - Eindeutige Kennung für das Card
- `WatchedAt` (nullable DateTime) - Markierung als angesehen
- `Actions` - Context-Menu-Aktionen
- `OnActionSelected` - Event Callback

### Bildhandling (Zeile 76-96):
```html
<img class="media-poster"
     src="@ImageUrl"
     alt="@Title"
     loading="lazy"
     onerror="this.onerror=null;this.src='/images/placeholder.png';" />
```
- CSS-Klasse: `.media-poster`
- Lazy Loading aktiviert
- Fallback auf `/images/placeholder.png` via `onerror`

### Verwendungsbeispiel in `MediaBaseEntryList.razor`:
```csharp
<MediaBox Title="@entry.Name"
          ImageUrl="@GetImageUrl(entry)"
          LinkUrl="@GetLinkUrl(entry)"
          WatchedAt="@GetWatchedAt(entry)" />
```

---

## `MediaBaseEntryList.razor`
Datei: `VideoWebPlayer\Components\Shared\Media\MediaBaseEntryList.razor`

### Zweck:
Zeigt eine Liste von `MediaBaseEntry`-Objekten in `MediaBox`-Komponenten.

### Bild-URL-Konvention (Zeile 17-26):
```csharp
private string GetImageUrl(MediaBaseEntry entry)
{
    if (entry.PosterPictureId.HasValue)
        return $"/api/pictures/{entry.PosterPictureId}?access_token={AuthorizationToken}";
    if (entry.BannerPictureId.HasValue)
        return $"/api/pictures/{entry.BannerPictureId}?access_token={AuthorizationToken}";
    if (entry.FanartPictureId.HasValue)
        return $"/api/pictures/{entry.FanartPictureId}?access_token={AuthorizationToken}";
    return "/images/placeholder.png";
}
```

**Fallback-Reihenfolge:**
1. PosterPictureId → `/api/pictures/{id}?access_token={token}`
2. BannerPictureId → `/api/pictures/{id}?access_token={token}`
3. FanartPictureId → `/api/pictures/{id}?access_token={token}`
4. Placeholder → `/images/placeholder.png`

**Parameter:**
- `Entries` (List<MediaBaseEntry>) - Liste der Medieneinträge
- `AuthorizationToken` - Access Token für API-Anfragen

### Wichtig für Anforderung:
Dieses Pattern ist das Vorbild für die Bildladung in `PlaylistDetail.razor`. Für Playlist-Einträge können ähnliche Methoden verwendet werden, um `PosterPictureId` aus den referenzierten Medienentitäten zu laden.

---

## CSS-Konventionen

### `.media-poster`
- CSS-Klasse für Poster-Bilder
- Definiert in Stylesheet (nicht in den gefundenen Komponenten)
- Wird von `MediaBox.razor` (Zeile 77) verwendet

### `.opacity-50` und `.text-muted`
- Auf Tabellenzeile angewendet (Zeile 103 in `PlaylistDetail.razor`)
- Zeigt visuell an, dass Eintrag nicht freigeschaltet ist
- **Sollte erhalten bleiben** (Anforderung Phase 3)

### `.playlist-entry-row`
- CSS-Klasse für Tabellenzeilen in Playlist-Detail
- Hat Daten-Attribute `data-media-type` und `data-media-id` (Zeile 103)

---

## API-Konventionen für Bildladung

**Bild-Endpunkt:**
```
GET /api/pictures/{id}?access_token={token}
```

**Verwendung in Komponenten:**
- URL wird im `src`-Attribut von `<img>`-Tags verwendet
- Access Token wird als Query-Parameter übergeben
- Fallback auf `/images/placeholder.png` bei Fehler

---

## Client-seitige Datenbeschaffung

**Client-Klasse:** `VideoWebPlayer.Client.VideoWebPlayerClient`

**Relevante Methoden (aus `PlaylistDetail.razor`):**
- `RequestPlaylistAsync(long playlistId)` → `Task<DtoPlaylist?>`
- `RequestPlaylistEntriesPagedAsync(long playlistId, int page, int pageSize, CancellationToken?)` → `Task<DtoPlaylistEntriesPagedResult>`
- `AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest)` → `Task<DtoPlaylistAddResult>`
- `RemoveMediaFromPlaylistAsync(long playlistId, string mediaType, long mediaId)` → `Task`

**Hinweis:** Der Client wird über `@inject` injiziert (Zeile 5) und nutzt Authorization Token von `AuthStateProvider`.
