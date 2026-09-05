# Bestandsaufnahme: Datenmodell

## `PlaylistEntry`
Datei: `VideoWebPlayer\Data\PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige ID des Eintrags (Primary Key) |
| `PlaylistId` | `long` | Fremdschlüssel zur Playlist |
| `Playlist` | `Playlist` | Navigation Property zur Playlist |
| `MediaType` | `string` (Required) | Medientyp als String (eines der Werte aus `MediaTypeValues`: "Movie", "TVShow", "TVShowSeason", "TVShowEpisode", "MovieCollection"). **Problem: Wird nicht normalisiert gespeichert** |
| `MediaId` | `long` | ID des referenzierten Medieninhalts |
| `ParentMediaType` | `string?` | Medientyp der übergeordneten Sammlung (z.B. "TVShow" wenn Episode durch Serie hinzugefügt), oder null für Top-Level-Einträge |
| `ParentMediaId` | `long?` | ID der übergeordneten Sammlung, oder null für Top-Level-Einträge |
| `AddedAt` | `DateTime` | Zeitstempel (UTC) wann der Eintrag hinzugefügt wurde. Default: `DateTime.UtcNow` |

### Bekannte Probleme / Beobachtungen

1. **Keine Normalisierung des MediaType-Feldes:**
   - PlaylistService speichert in Zeile 182 den rohen `mediaType` String: `MediaType = mediaType`
   - Sollte sein: `MediaType = parsedMediaType.ToString()` (kanonischer Enum-Wert)
   - Beispiel: "Movie" und "movie" hätten beide unterschiedliche DB-Einträge

2. **Unique Constraint implizit erforderlich:**
   - Duplikat-Check in PlaylistService nutzt HashSet mit `(MediaType, MediaId)` Tuple
   - Es sollte ein DB-Unique-Index auf (PlaylistId, MediaType, MediaId) vorhanden sein (oder zumindest validiert werden)
   - Die Validierung erfolgt case-sensitiv auf den (möglicherweise nicht-normalisierten) String-Wert

## `Playlist`
Datei: `VideoWebPlayer\Data\Playlist.cs` (indirekt referenziert)

Die PlaylistEntry-Klasse verweist auf eine Playlist als übergeordnete Entität. Diese ist nicht direkt analysiert, aber sie wird in PlaylistService mehrfach validiert (UserId-Check).
