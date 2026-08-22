# Bestandsaufnahme: Fehlerhafte Ermittlung der nächsten Episode

Diese Bestandsaufnahme analysiert die Codebase der Anforderung "Fehlerhafte Ermittlung der nächsten Episode (GetNextEpisodeAsync)" (Aufgaben-ID: 3aac1d2e-8d91-42ab-afe0-430e3682c757). Sie dokumentiert die bestehenden Komponenten, Datenmodelle, Service-Logik und Testabdeckung.

---

## Zusammenfassung

### Vorhanden

- **Datenmodelle:**
  - `TVShowEpisode` mit Eigenschaften `Number`, `TVShowSeasonId`, `ReleaseDate`, `PremieredAt`, `Plot` u.a.
  - `TVShowSeason` mit Eigenschaften `TVShowId`, `Name`, `Episodes` u.a.
  - `MediaBaseEntry` als Basisklasse mit `ReleaseDate`, `PremieredAt`, `Name`, `Id` u.a.

- **Service:**
  - `ContinueWatchingService` mit öffentlichen Methoden `GetListAsync()`, `ReportProgressAsync()`, `ProcessBufferedEntryAsync()`
  - Private Methode `GetNextEpisodeAsync()` (Zeilen 346–379) — **dies ist die problematische Methode**
  - Private Methode `GetNextMovieAsync()` als funktionierendes Referenz-Beispiel

- **Tests:**
  - `ContinueWatchingServiceSignalRTests` mit 5 Test-Methoden für SignalR-Integration
  - Aber **KEINE Unit-Tests für `GetNextEpisodeAsync()`**

### Identifizierte Probleme

1. **NULL-Handling:** Die WHERE-Klausel in `GetNextEpisodeAsync()` vergleicht `ReleaseDate >= current.ReleaseDate`, was bei NULL-Werten zu unerwarteten SQL-Vergleichen führt

2. **Sortierung:** ReleaseDate ist Primärsortierung, Episodennummer (Number) nur Tiebreaker — sollte umgekehrt sein (Episodennummer ist zuverlässiger)

3. **Fehlende Test-Abdeckung:** Es gibt keine Unit-Tests für verschiedene Szenarien:
   - Episoden mit NULL-ReleaseDate
   - Episoden-Lücken
   - Staffel-Wechsel
   - Edge Cases

---

## Details

- [Datenmodelle](inventory/models.md)
- [Service-Logik](inventory/logic.md)
- [Testabdeckung](inventory/tests.md)

---

## Kritische Befunde

### `GetNextEpisodeAsync()` — Zeilen 346–379

**Aktuelle Implementierung:**
```csharp
private async Task<TVShowEpisode?> GetNextEpisodeAsync(long currentEpisodeId, CancellationToken ct)
{
    var current = await _db.TVShowEpisodes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == currentEpisodeId, ct);
    if (current == null) return null;

    var season = await _db.TVShowSeasons.FirstOrDefaultAsync(s => s.Id == current.TVShowSeasonId, ct);
    if (season is null) return null;

    // ❌ PROBLEMATISCH: ReleaseDate-Vergleich mit NULL
    var next = await _db.TVShowEpisodes.AsNoTracking()
        .Where(e => e.TVShowSeasonId == current.TVShowSeasonId 
                    && e.Id != current.Id 
                    && e.ReleaseDate >= current.ReleaseDate)  // NULL-Vergleich!
        .OrderBy(e => e.ReleaseDate)      // NULL-Werte unsortiert
        .ThenBy(e => e.Number)            // Nur Tiebreaker
        .Select(e => e.Id)
        .FirstOrDefaultAsync(ct);

    if (next == 0)
    {
        // Staffel-Wechsel: ordnung nach Name
        var nextSeason = (await _db.TVShowSeasons.Where(s => s.TVShowId == season.TVShowId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct))
            .SkipWhile(s => s.Id != season.Id)
            .SkipWhile(s => s.Id == season.Id)
            .FirstOrDefault();
        if (nextSeason is null) return null;
        next = await _db.TVShowEpisodes.AsNoTracking()
            .Where(e => e.TVShowSeasonId == nextSeason.Id)
            .OrderBy(e => e.ReleaseDate)   // Wieder ReleaseDate-Sortierung
            .ThenBy(e => e.Number)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(ct);
    }

    return next == 0 ? null : await _db.TVShowEpisodes.FindAsync(new object[] { next }, ct);
}
```

**Fehlerquellen:**
1. `e.ReleaseDate >= current.ReleaseDate` — Bei NULL wird dies zu unerwartetem SQL-Verhalten
2. `OrderBy(e => e.ReleaseDate)` — NULL-Werte werden nicht konsistent sortiert
3. Episodennummer sollte Primärsortierung sein, nicht Sekundär

---

## Referenzierte Dateien

| Datei | Zweck | Status |
|-------|-------|--------|
| `VideoWebPlayer/Services/ContinueWatchingService.cs` | Hauptservice mit problematischer `GetNextEpisodeAsync()`-Methode | ✓ Analysiert |
| `VideoWebPlayer/Data/TVShowEpisode.cs` | Datenmodell für Episoden | ✓ Analysiert |
| `VideoWebPlayer/Data/TVShowSeason.cs` | Datenmodell für Staffeln | ✓ Analysiert |
| `VideoWebPlayer/Data/MediaBaseEntry.cs` | Basisklasse mit ReleaseDate, PremieredAt | ✓ Analysiert |
| `VideoWebPlayer.Tests/Services/ContinueWatchingServiceSignalRTests.cs` | Bestehende Tests (nur SignalR) | ✓ Analysiert |
