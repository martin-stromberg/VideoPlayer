# Logik und Services

## Geräte-Kopplung und Session-Verwaltung

### `PairingService`
**Datei:** `VideoWebPlayer/Services/PairingService.cs`

Verantwortlich für Code-basierte Pairing (nicht QR). Erstellt und validiert Pairing-Codes.

### `PairingBootstrapService`
**Datei:** `VideoWebPlayer/Services/PairingBootstrapService.cs`

Verantwortlich für QR-Bootstrap: erzeugt Bootstrap-Tickets und validiert QR-Payloads (ECDH + AES-256-GCM).

### `DeviceTokenService`
**Datei:** `VideoWebPlayer/Services/DeviceTokenService.cs`

Speichert Geräte-Tokens (als SHA-256-Hash), Widerruf und Abfrage.

### `RefreshTokenService`
**Datei:** `VideoWebPlayer/Services/RefreshTokenService.cs`

Verwaltet Refresh-Tokens mit Rotation und Reuse-Detection.

**Befund:** Alle vier Services existieren und sind funktionsfähig (empirisch belegt im Analysebericht Abschnitt 2.1).

## PlaylistsController: Fehlerbehandlung

**Datei:** `VideoWebPlayer/Controllers/PlaylistsController.cs` Zeile 52–116

**Methode `ExecuteAsync`:** 
```csharp
private async Task<IActionResult> ExecuteAsync(
    Func<Task<IActionResult>> action,
    string logContext,
    Func<InvalidOperationException, IActionResult>? mapInvalidOperation = null)
```

Diese Zentral-Exception-Handler bildet ab:
- `KeyNotFoundException` → `NotFound` (HTTP 404)
- `UnauthorizedAccessException` → `Unauthorized` (HTTP 401)
- `PlaylistAccessDeniedException` → `Forbid` (HTTP 403) ✓ Korrekt
- `ManualSortOrderConfirmationRequiredException`, `ContinueWatchingConfirmationRequiredException`, `UploadedCoverReplacementConfirmationRequiredException`, `PlaylistNotInManualSortModeException`, `PlaylistNameAlreadyExistsException` → verschiedene Conflicts (HTTP 409)

**Befund:** PlaylistsController hat korrekte Exception-zu-Statuscode-Mapping. ItemsController (siehe A4) hat das nicht.

## ItemsController: Fehlerbehandlung (A4-Befund)

**Datei:** `VideoWebPlayer/Controllers/ItemsController.cs`

**Befund F3 (Analysebericht):** 
- `EnsureAccessAsync` wirft `UnauthorizedAccessException` für fehlende Berechtigung → wird auf `401` gemappt
- `RecordNotFoundException` wird nirgends auf `404` gemappt → fällt in generischen `catch (Exception)` → `500`
- Dokumentation (`docs/API.md` Zeile 29–30) fordert aber `403` (Forbidden) bzw. `404` (Not Found)

**Abweichung:** Der Fehler liegt nicht im Code-Design, sondern in der inkonsistenten Exception-Behandlung zwischen PlaylistsController (richtig) und ItemsController (falsch).

## Playlist-Backfill-Markierung

**Datei:** `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs` Zeile 41–80

Hook in `SaveChanges`/`SaveChangesAsync`, der automatisch neue Serie/Staffel/Film-Einträge markiert. Quelltyp-unabhängig (funktioniert für SFTP und lokale Verzeichnisse gleich).

**Befund:** Code-seite OK für A7 (lokale Verzeichnisse).

## Weitere Services mit Playlist-Bezug

- **`PlaylistEntryAccessResolver`:** Arbeitet über `MediaSourceId` und `UnlockedMediaEntries`, kennt Quelltyp nicht
- **`PlaylistCoverImageGenerator`:** Liest aus `Pictures`/`Movies`/`TVShows`, nie vom Dateisystem → quelltyp-unabhängig
- **`IMediaSourceReader` (Dispatcher):** Kapselt `LocalMediaSourceReader` und `SftpMediaSourceReader` → gleiches Streaming für beide Quelltypen

**Befund:** Alle Services sind quelltyp-unabhängig implementiert (A7-Code OK).
