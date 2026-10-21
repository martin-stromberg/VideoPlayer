# Enums

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Sftp` | SFTP-Quelle (Default in `MediaSource.SourceType` und im Backup-Int-Default) |
| `LocalDirectory` | Lokales Verzeichnis |

Steuert die Reader-Auswahl im `MediaSourceReaderDispatcher` (`!= LocalDirectory` → SFTP-Reader). Für die Laufzeitermittlung relevant: lokale Dateien können ffprobe direkt mit Pfad aufrufen, entfernte nur per Stream (`pipe:0`).

Keine weiteren betroffenen Enums — für die Probe-Markierung wird ein `DateTime?`-Zeitstempel (`DurationProbedAt`) statt eines Status-Enums gewählt (Konvention: `ClassifiedAt`, `ActorsClassifiedAt`, `LastScannedAt`).
