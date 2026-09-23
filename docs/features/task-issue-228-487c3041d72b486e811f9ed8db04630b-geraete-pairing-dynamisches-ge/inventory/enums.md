# Enums — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

## `ApiTokenScope`

Datei: `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs` (Zeilen 91–102, **globaler Namespace**)

| Wert | Bedeutung |
|------|-----------|
| `AnyClient` | Akzeptiert `Jwt:ApiToken`, `Jwt:ApiToken:Web` und `Jwt:ApiToken:Maui` (Default des Attributs) |
| `MauiOnly` | Akzeptiert nur `Jwt:ApiToken:Maui` — laut Anforderung künftig zusätzlich DB-Geräte-Tokens |

Verwendet ausschließlich in `ApiTokenCheckAttribute` und `AuthController` (`MauiOnly` nur auf `Login`, `AnyClient` implizit auf `Impersonate`).

## Weitere Enums (nicht direkt betroffen)

- `MediaSourceType` (`VideoWebPlayer/Data/MediaSourceType.cs`) — Quelltyp (`Sftp = 0`, `LocalDirectory = 1`), für die Anforderung nicht relevant.

Keine weiteren Enums im Umfeld der Anforderung vorhanden.
