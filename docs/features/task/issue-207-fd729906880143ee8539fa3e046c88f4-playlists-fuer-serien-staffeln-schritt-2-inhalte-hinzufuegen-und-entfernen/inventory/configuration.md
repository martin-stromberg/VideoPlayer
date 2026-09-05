# Konfiguration und Einstellungen

## `PlaylistSettings`
Datei: `VideoWebPlayer/Configuration/PlaylistSettings.cs`

| Eigenschaft | Typ | Standardwert | Beschreibung |
|-------------|-----|--------------|-------------|
| `MaxPlaylistsPerUser` | `int?` | `null` (unbegrenzt) | Maximale Anzahl Playlists pro Benutzer |

**Verwendung:** Diese Klasse wird über `IOptions<PlaylistSettings>` in den `PlaylistService` injiziert.

**Konfigurationsquelle:** `appsettings.json` oder andere Konfigurationsquellen

**Beispiel `appsettings.json`:**
```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null
  }
}
```

---

## Zu erweitern für Schritt 2

### `PlaylistSettings` (Erweiterung)
**Neue Eigenschaft erforderlich:**
- `MaxPlaylistItemCount` (int?, Standard: null = unbegrenzt)
  - Maximale Anzahl von Einträgen pro Playlist
  - Optional für Schritt 2 (kann auch als Future-Work markiert werden)
  - Konfigurationsschlüssel: `Playlists:MaxPlaylistItemCount`

---

## Fehlerbehandlung

### `PlaylistAccessDeniedException`
Datei: `VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs`

**Zweck:** Wird geworfen, wenn ein Benutzer versucht, auf eine Playlist eines anderen Benutzers zuzugreifen oder diese zu modifizieren

**Implementierung:**
- Erbt von `Exception`
- Mehrere Konstruktoren für verschiedene Initialisierungsmöglichkeiten

**Verwendung in:**
- `PlaylistService.GetPlaylistAsync()` — Bei Eigentumspr
üfung
- `PlaylistService.UpdatePlaylistAsync()` — Bei Eigentumspr
üfung
- `PlaylistService.DeletePlaylistAsync()` — Bei Eigentumspr
üfung
- `PlaylistsController` — Wird zu HTTP 403 gemappt

---

## Events

### `PlaylistCreatedEvent`
Datei: `VideoWebPlayer/Events/PlaylistCreatedEvent.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Playlist` | `Playlist` | Die neu erstellte Playlist |

**Publiziert von:** `PlaylistService.CreatePlaylistAsync()`

---

### `PlaylistUpdatedEvent`
Datei: `VideoWebPlayer/Events/PlaylistUpdatedEvent.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Playlist` | `Playlist` | Die aktualisierte Playlist |

**Publiziert von:** `PlaylistService.UpdatePlaylistAsync()`

---

### `PlaylistDeletedEvent`
Datei: `VideoWebPlayer/Events/PlaylistDeletedEvent.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `PlaylistId` | `long` | ID der gelöschten Playlist |
| `UserId` | `string` | Benutzer-ID des Besitzers |

**Publiziert von:** `PlaylistService.DeletePlaylistAsync()`

---

## Zu erweitern für Schritt 2

### Neue Events (optional)
**Eventuell erforderlich:**
- `PlaylistEntryAddedEvent` — Wenn ein Eintrag hinzugefügt wird
- `PlaylistEntryRemovedEvent` — Wenn ein Eintrag entfernt wird

**Entscheidung:** Abhängig von anforderungen für Event-Handling in anderen Komponenten

