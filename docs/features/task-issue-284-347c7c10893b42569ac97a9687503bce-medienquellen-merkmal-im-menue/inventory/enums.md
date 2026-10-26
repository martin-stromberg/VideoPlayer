# Enums — Bestandsaufnahme

Für das Merkmal selbst ist laut Anforderung kein neuer Enum nötig. Relevant sind die bestehenden Enums, die den fachlichen Kontext („Clip-Quelle") und den Quelltyp beschreiben.

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Sftp = 0` | Entfernter SFTP-Server (Default für Bestandsdaten) |
| `LocalDirectory = 1` | Lokales Verzeichnis auf dem Server (inkl. UNC-Pfade) |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (namensdedupliziert über Quellen) |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | Trailer kommender Filme (globales Füllermaterial, ohne `MaterialId`) |
| `MediaSourceClips = 6` | Unklassifizierte Video-`MediaItem`s einer Quelle („Clip-Quelle", `MaterialId` = `MediaSources.Id`) — der Materialtyp, der reine Platzhalter-/Maskottchen-Quellen kennzeichnet und damit der fachliche Auslöser für `HiddenInMenu` |
