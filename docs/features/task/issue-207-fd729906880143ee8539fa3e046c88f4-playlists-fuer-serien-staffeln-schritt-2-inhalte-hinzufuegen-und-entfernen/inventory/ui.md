# Bestandsaufnahme: UI-Komponenten

## `PlaylistDetail.razor` Komponente
Datei: `VideoWebPlayer\Components\Playlists\PlaylistDetail.razor`

### Verwendete Injektionen
- `VideoWebPlayer.Client.VideoWebPlayerClient Client` — HTTP-Client für API-Aufrufe
- `AuthenticationStateProvider AuthStateProvider` — Token-Management
- `NavigationManager NavigationManager` — Navigation

### Relevante Properties (für diese Anforderung)
| Property | Typ | Zweck |
|----------|-----|-------|
| `newEntryMediaType` | `string` | Aktuell ausgewählter Medientyp im Dropdown (Default: `MediaTypeValues.Movie`) |
| `newEntryMediaId` | `long` | Eingabe-Feld für die Media-ID |
| `entriesStatusMessage` | `string?` | Status/Fehler-Meldung oberhalb der Einträge-Liste |
| `playlistEntries` | `List<DtoPlaylistEntry>` | Die aktuell geladenen Einträge der Playlist |

### HTML: Medientyp-Dropdown (Zeile 68–74)
```razor
<select class="form-control playlist-add-mediatype-select" @bind="newEntryMediaType">
    <option value="@MediaTypeValues.Movie">Film</option>
    <option value="@MediaTypeValues.TVShow">Serie</option>
    <option value="@MediaTypeValues.TVShowSeason">Staffel</option>
    <option value="@MediaTypeValues.TVShowEpisode">Episode</option>
    <option value="@MediaTypeValues.MovieCollection">Filmsammlung</option>
</select>
```
- Sendet immer normalisierte Werte (aus `MediaTypeValues`)

### HTML: Meldungsanzeige (Zeile 61–64)
```razor
@if (!string.IsNullOrWhiteSpace(entriesStatusMessage))
{
    <div class="alert alert-warning" id="playlist-entries-status">@entriesStatusMessage</div>
}
```
- ❌ Zeigt Meldung mit `alert-warning` Klasse (gelbe/Warn-Farbe)
- Problem: Sowohl Fehler als auch Info-Meldungen werden gleich angezeigt

### Methode: `AddEntryAsync` (Zeile 188–212) — PROBLEMATISCH

**Aktueller Code:**
```csharp
private async Task AddEntryAsync()
{
    entriesStatusMessage = null;
    try
    {
        await Client.AddMediaToPlaylistAsync(Id, new DtoAddMediaToPlaylistRequest
        {
            MediaType = newEntryMediaType,
            MediaId = newEntryMediaId
        });
        await LoadEntriesAsync();
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
    {
        entriesStatusMessage = "Medieninhalt bereits in dieser Playlist vorhanden.";
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        entriesStatusMessage = "Medieninhalt wurde nicht gefunden.";
    }
    catch (Exception ex)
    {
        entriesStatusMessage = $"Fehler beim Hinzufuegen: {ex.Message}";
    }
}
```

**Probleme (nach Anforderung):**
1. ❌ **HTTP 409 wird nicht mehr geworfen** — Der Catch-Block wird nicht mehr erreicht
2. ❌ **Keine Verarbeitung der neuen Response-Struktur** — `DtoPlaylistAddResult` mit Message
3. ❌ **Fehlende Rückmeldung über Duplikate** — Aktuell nur bei Fehler, sonst stille Übersprung

**Nach Anforderung sollte:**
1. ✅ HTTP 200 OK für alle Fälle (auch Duplikate)
2. ✅ Die `DtoPlaylistAddResult.Message` an Benutzer angezeigt werden (z.B. "3 Titel hinzugefügt, 2 bereits vorhanden")
3. ✅ Unterschied zwischen Fehler und Info-Meldung in UI (Farbe/Icon)
4. ✅ Optional: Detaillierte Liste der hinzugefügten vs. übersprungenen Titel anzeigen

### Methode: `RemoveEntryAsync` (Zeile 214–226)
- Wirft sich selbst auf und lädt Einträge neu nach erfolgreicher Löschung
- ✅ Unverändert für diese Anforderung

### Methode: `LoadEntriesAsync` (Zeile 176–186)
```csharp
private async Task LoadEntriesAsync()
{
    try
    {
        playlistEntries = (await Client.RequestPlaylistEntriesAsync(Id)).ToList();
    }
    catch (Exception ex)
    {
        entriesStatusMessage = $"Fehler beim Laden der Eintraege: {ex.Message}";
    }
}
```
- Wird nach erfolgreichem `AddEntryAsync` aufgerufen
- ✅ Nach Anforderung: Wird weiterhin aufgerufen, zeigt aktuelle Einträge

### Abhängigkeiten

**VideoWebPlayerClient (Zeile 193):**
- Aufgerufen: `Client.AddMediaToPlaylistAsync(Id, DtoAddMediaToPlaylistRequest)`
- **Nach Anforderung:** Gibt `DtoPlaylistAddResult` zurück (statt `DtoPlaylistEntry`)
- Muss in `AddEntryAsync` verarbeitet werden

**MediaTypeValues (Zeile 69):**
- Konstanten für die Dropdown-Optionen
- ✅ Nicht betroffen von Normalisierungs-Anforderung (UI sendet bereits normalisierte Werte)

## Weitere Razor-Komponenten (nicht direkt betroffen)

- **PlaylistForm.razor** — Erstellt/Bearbeitet Playlist-Metadaten (Name, Description, SortMode)
  - Nicht betroffen von dieser Anforderung
  
- **PlaylistDeleteConfirmationDialog.razor** — Bestätigungsdialog beim Löschen
  - Nicht betroffen
  
- **PlaylistOverview.razor** — Liste aller Playlists eines Benutzers
  - Nicht betroffen
