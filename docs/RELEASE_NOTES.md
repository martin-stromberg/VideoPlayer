# Release Notes

> As of the CI standardization migration, `.github/workflows/release.yml` (previously
> `main-release.yml`) no longer reads this file as the GitHub release body. Releases now use
> semantic-release's auto-generated notes (derived from conventional commit messages) instead
> of this hand-curated changelog. This file is kept for historical reference; whether a
> manually maintained changelog should be reintroduced is an open follow-up question.
>
> Frühere Fassung: Diese Datei wurde von `main-release.yml` als Body des GitHub-Releases
> verwendet. Seit der CI-Vereinheitlichung generiert semantic-release die Release-Notes
> automatisch aus den Commit-Nachrichten; diese Datei wird nicht mehr automatisch verwendet.

## Important Notes Before Update

- Backup upload API changed: the multipart endpoint `POST admin/backups/api/upload` was replaced by a chunked `application/octet-stream` protocol (`POST admin/backups/api/upload/chunk`, `GET`/`DELETE admin/backups/api/upload/{id}`) — custom scripts or integrations using the old endpoint must be updated.
- New configuration `Kestrel:Limits:MaxRequestBodySize` in `appsettings.Production.json` is set to `0` (unlimited) and applied via an explicit `ConfigureKestrel` binding, because Kestrel does not load `Limits` from configuration on its own.
- IIS deployments: the hosting model is now `OutOfProcess` (`AspNetCoreHostingModel` in the project file); the IIS `requestFiltering` limit `maxAllowedContentLength` (~30 MB default) must be raised so large uploads are not blocked.
- The configurable upload limit `Backups:MaxUploadSizeBytes` (default 5 GiB) is now enforced server-side — files above the limit are rejected; raise it in the admin UI if larger backups must be accepted.

## What's New

- Media sources now support local directories: in addition to `SFTP server`, `Local directory` — a directory on the server, including UNC paths — can be selected as the source type.
- The source type is chosen when creating a source and cannot be changed afterwards; the form only shows the fields relevant to the selected type.
- Local directory paths are validated when saving — the path must be absolute, the directory must exist and be readable; otherwise a descriptive error message is shown and the source is not saved.
- The sources overview shows the type of each source (`SFTP`/`Local`) in a new `Type` column.
- Scanning, classification (NFO metadata, covers, actor images) and streaming/downloads work for local sources just like for SFTP sources; hidden entries (names starting with `.`) are skipped.
- Backups from older versions remain restorable: a missing `SourceType` column in `MediaSources` is filled with the default `SFTP`.
- Fixed: the navigation menu only requests the media source list for authenticated users with a valid authorization token.
- New help pages for media sources under `docs/help/medienquellen/`.
- The backup upload limit in the settings can now be entered as a value with a unit (MB/GB) instead of raw bytes.
- Backup upload now transfers files in chunks as `application/octet-stream` and supports backups larger than 6 GB.
- Interrupted uploads can be resumed at the already transferred position — even after a page reload or a lost connection; unfinished uploads are shown with their file name and can be discarded.
- Upload progress indicator with transferred/total size and percentage, automatic retry on network interruptions, and clear error messages on failure.
- Incomplete uploads that have been inactive for more than 24 hours are cleaned up automatically.
- An existing `.bak` file with the same name is now replaced on import.

## Wichtige Hinweise vor dem Update

- Backup-Upload-API geändert: Der bisherige Multipart-Endpunkt `POST admin/backups/api/upload` wurde durch ein chunkbasiertes `application/octet-stream`-Protokoll ersetzt (`POST admin/backups/api/upload/chunk`, `GET`/`DELETE admin/backups/api/upload/{id}`) — eigene Skripte oder Integrationen, die den alten Endpunkt nutzen, müssen angepasst werden.
- Neue Konfiguration `Kestrel:Limits:MaxRequestBodySize` in `appsettings.Production.json` ist auf `0` (unbegrenzt) gesetzt und wird über eine explizite `ConfigureKestrel`-Bindung angewendet, da Kestrel die `Limits`-Sektion nicht selbst aus der Konfiguration lädt.
- IIS-Bereitstellungen: Das Hosting-Modell ist jetzt `OutOfProcess` (`AspNetCoreHostingModel` in der Projektdatei); das IIS-`requestFiltering`-Limit `maxAllowedContentLength` (~30 MB Standard) muss angehoben werden, damit große Uploads nicht blockiert werden.
- Das konfigurierbare Upload-Limit `Backups:MaxUploadSizeBytes` (Standard 5 GiB) wird jetzt serverseitig durchgesetzt — Dateien oberhalb des Limits werden abgelehnt; bei Bedarf in der Admin-Oberfläche erhöhen.

## Neuerungen

- Medienquellen unterstützen jetzt lokale Verzeichnisse: Neben `SFTP-Server` kann `Lokales Verzeichnis` — ein Verzeichnis auf dem Server, auch UNC-Pfad — als Quelltyp gewählt werden.
- Der Quelltyp wird beim Anlegen einer Quelle gewählt und ist nachträglich nicht mehr änderbar; das Formular zeigt nur die zum Typ passenden Felder.
- Lokale Verzeichnispfade werden beim Speichern geprüft — der Pfad muss absolut sein, das Verzeichnis muss existieren und lesbar sein; andernfalls erscheint eine verständliche Fehlermeldung und die Quelle wird nicht gespeichert.
- Die Quellenübersicht zeigt den Typ jeder Quelle (`SFTP`/`Lokal`) in einer neuen Spalte `Typ`.
- Einlesen, Klassifizierung (NFO-Metadaten, Cover- und Schauspielerbilder) und Streaming/Downloads funktionieren für lokale Quellen genauso wie für SFTP-Quellen; versteckte Einträge (Namen mit führendem `.`) werden übersprungen.
- Backups aus älteren Versionen können weiterhin wiederhergestellt werden: Eine fehlende `SourceType`-Spalte in `MediaSources` wird mit dem Standard `SFTP` ergänzt.
- Behoben: Das Navigationsmenü ruft die Medienquellenliste nur noch für angemeldete Benutzer mit gültigem Autorisierungstoken ab.
- Neue Hilfeseiten zu Medienquellen unter `docs/help/medienquellen/`.
- Das Backup-Upload-Limit lässt sich in den Einstellungen jetzt als Wert mit Einheit (MB/GB) statt in Bytes eintragen.
- Der Backup-Upload überträgt Dateien jetzt in Abschnitten (Chunks) als `application/octet-stream` und unterstützt Backups über 6 GB.
- Unterbrochene Uploads können an der bereits übertragenen Position fortgesetzt werden — auch nach einem Seitenreload oder einer unterbrochenen Verbindung; nicht abgeschlossene Uploads werden mit Dateinamen angezeigt und können verworfen werden.
- Fortschrittsanzeige mit übertragener/Gesamtgröße und Prozentangabe, automatische Wiederholung bei Netzwerkunterbrechungen und verständliche Fehlermeldungen bei endgültigem Fehlschlag.
- Nicht abgeschlossene Uploads, auf die länger als 24 Stunden nicht zugegriffen wurde, werden automatisch aufgeräumt.
- Eine bereits vorhandene gleichnamige `.bak`-Datei wird beim Import jetzt ersetzt.
