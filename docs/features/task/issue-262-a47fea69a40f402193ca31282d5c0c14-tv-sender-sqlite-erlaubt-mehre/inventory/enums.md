# Bestandsaufnahme: Enums

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

Art des Inhalts, auf den `ProgramRuleCursor.LastContentId` verweist. Persistiert als `INTEGER`.

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Ein Spielfilm (`Movie`) |
| `TVShowEpisode = 1` | Eine Serien-Episode (`TVShowEpisode`) |
| `PlaylistEntry = 2` | Ein `PlaylistEntry` eines Playlist-Materials |
| `Trailer = 3` | Ein Trailer-`MediaItem` (`MovieMediaItem.IsTrailer`) |
| `MediaItem = 4` | Ein rohes `MediaItem` eines Clip-Quellen-Materials (`ProgramRuleMaterialType.MediaSourceClips`); `LastContentId` = MediaItem-Id |

Für die Anforderung nicht änderbar/relevant: Der Enum betrifft nur den Inhalt des Cursors,
nicht den `StrandKey`. Keine anderen Enums sind betroffen.
