# Datenmodell (Bestandsaufnahme Issue #333)

Die Anforderung erwartet keine Änderung am Datenmodell. Die folgenden Eigenschaften sind für den Scan-Pfad relevant, weil die Sequenz sie liest oder schreibt.

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | Zugehörige Medienquelle (Zeile 14). |
| `LastScannedAt` | `DateTime?` | Zeitpunkt des letzten Scans (Zeile 22). |
| `ScanDueAt` | `DateTime?` | Fälligkeit für `ScanNextMediaCollection` (Zeile 26). Ist `null` oder in der Zukunft, wird nicht gescannt. |
| `Classifyable` | `bool` | Markiert, ob die Collection klassifiziert werden kann (Zeile 30). Wird von `ScanAllSourcesAsync` auf `false` gesetzt. |

## Geerbte Eigenschaften (Basisklassen)
Die Eigenschaften `ClassifiedAt`, `Changed` und `CreatedAt` sind in `VideoWebPlayer/Data/MediaBaseEntry.cs` (Zeilen 43, 47, 51) und `VideoWebPlayer/Data/MediaEntry.cs` (Zeilen 28, 32, 36) deklariert. Welche Klasse die Basis der anderen ist, wurde nicht untersucht.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `CreatedAt` | `DateTime` | Erstellzeitpunkt (Vergleich bei Root-Collections in `ScanAllSourcesAsync`). |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung. Ohne Wert gilt die Collection als unklassifiziert (`!mc.ClassifiedAt.HasValue`). |
| `Changed` | `bool` | Kennzeichnet geänderte Einträge, die neu klassifiziert werden müssen. |

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `LastScannedAt` | `DateTime?` | Wird in `ScanAllSourcesAsync` (Zeile 66) auf den Scanzeitpunkt gesetzt (Zeile 44). |

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GenresChanged` | `bool` | Auslöser für den Genre-Reload in `CheckReloadGenres` (Zeile 17). |

## Nicht betroffen
- Die Einträge `PlaylistBackfillMarker` werden vom DbContext geschrieben (siehe [logic.md](logic.md)), aber für die Anforderung nicht geändert.
