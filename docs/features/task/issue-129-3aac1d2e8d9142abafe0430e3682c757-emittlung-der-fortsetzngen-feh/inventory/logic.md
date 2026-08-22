# Service-Logik

## `ContinueWatchingService`
Datei: `VideoWebPlayer/Services/ContinueWatchingService.cs`

Verwaltung von "Continue Watching"-Einträgen und Puffer-Logik für Wiedergabefortschritt.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `GetListAsync(ClaimsPrincipal user, CancellationToken ct)` | `public` | Ruft die Continue-Watching-Liste für einen Benutzer ab (bis zu 50 Einträge, sortiert nach Änderungsdatum) |
| `ReportProgressAsync(ApplicationUser user, long? movieId, long? episodeId, TimeSpan position, TimeSpan duration, CancellationToken ct)` | `public` | Puffert den Wiedergabefortschritt für spätere Verarbeitung |
| `ProcessBufferedEntryAsync(string userId, long? movieId, long? episodeId, TimeSpan position, TimeSpan duration, CancellationToken ct)` | `public` | Verarbeitet einen gepufferten Eintrag und aktualisiert den Speicher; ruft `GetNextEpisodeAsync()` auf, wenn eine Episode zu Ende ist |
| `GetNextMovieAsync(long currentMovieId, CancellationToken ct)` | `private` | Ermittelt den nächsten Film in der Film-Sammlung (Sortierung: ReleaseDate, PremieredAt, Name) |
| **`GetNextEpisodeAsync(long currentEpisodeId, CancellationToken ct)`** | `private` | **PROBLEMATISCHE METHODE** — Ermittelt die nächste Episode nach der aktuellen; Zeilen 346–379; nutzt fehlerhafte ReleaseDate-Vergleiche und NULL-Handling |
| `UpsertAsync(string userId, long? nextMovieId, long? nextEpisodeId, TimeSpan position, TimeSpan? duration, CancellationToken ct)` | `private` | Fügt einen neuen Eintrag ein oder aktualisiert einen bestehenden (mit Change-Detection) |
| `RemoveExistingTVShowEntry(string userId, long? nextEpisodeId, CancellationToken ct)` | `private` | Entfernt alle anderen Episode-Einträge derselben Serie für einen Benutzer |
| `RemoveExtsingMovieCollectionEntry(string userId, long? nextMovieId, CancellationToken ct)` | `private` | Entfernt alle anderen Film-Einträge derselben Sammlung für einen Benutzer |
| `GetUserIdAsync(ClaimsPrincipal principal, CancellationToken ct)` | `private` | Ruft die Benutzer-ID aus den Ansprüchen ab |
| `Create<T>(object ms)` | `protected` | Erstellt einen DTO durch Kopieren von übereinstimmenden Eigenschaften |

### Abhängigkeiten (Injiziert)
- `ApplicationDbContext _db` — Datenbankkontext
- `UserManager<ApplicationUser> _userManager` — Benutzerverwaltung
- `ILogger<ContinueWatchingService> _logger` — Logger
- `ContinueWatchingBuffer _buffer` — In-Memory-Puffer für Einträge
- `MediaUpdateNotificationService _notificationService` — SignalR-Benachrichtigungen

### Konfigurierbare Konstanten
- `MinStart = TimeSpan.FromSeconds(5)` — Minimale Startposition
- `EndThreshold = TimeSpan.FromSeconds(30)` — Schwellenwert zum Markieren als "abgeschlossen"

### Problem-Details: `GetNextEpisodeAsync()`

**Zeilen 346–379:**

1. **Fehlerhafte WHERE-Klausel (Zeile 355):**
   ```csharp
   .Where(e => e.TVShowSeasonId == current.TVShowSeasonId 
              && e.Id != current.Id 
              && e.ReleaseDate >= current.ReleaseDate)
   ```
   - Wenn `current.ReleaseDate` ist NULL, liefert der Vergleich unerwartete Ergebnisse in SQL
   - Episoden ohne ReleaseDate werden nicht zuverlässig behandelt

2. **Sortierung mit NULL-Werten (Zeilen 356–357):**
   ```csharp
   .OrderBy(e => e.ReleaseDate)
   .ThenBy(e => e.Number)
   ```
   - NULL-Werte in ReleaseDate werden standardmäßig am Anfang oder Ende sortiert (DB-abhängig)
   - Episodennummer (Number) ist nicht Primärsortierung, sondern nur Tiebreaker

3. **Konsequenz:**
   - Kann zu Endlosschleifen führen (Episoden A ↔ B wiederholt)
   - Episoden-Lücken werden möglicherweise nicht richtig behandelt
   - Staffelwechsel kann fehlerhaft ausgelöst werden

## `MediaUpdateNotificationService`
Datei: (Referenz aus Tests) `VideoWebPlayer/Services/MediaUpdateNotificationService.cs`

Sendet SignalR-Benachrichtigungen über Updates.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `NotifyContinueWatchingUpdatedAsync(string userId, CancellationToken ct)` | `public` | Sendet SignalR-Event "ContinueWatchingUpdated" an einen Benutzer |
