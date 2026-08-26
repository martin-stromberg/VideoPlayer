# Video Playback

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Web-Entwickler  
> **Letzte Aktualisierung**: 2026-08-25

VideoWebPlayer liefert Medien über die Web-API aus und stellt die Wiedergabe in der Weboberfläche bereit.

## Web-Endpunkte

- `GET /api/items/{type}/{id}/stream` streamt Film- und Episodendateien mit Range-Unterstützung.
- `GET /api/items/{type}/{id}/download` liefert dieselbe Datei als Download.
- `GET /api/pictures/{id}` liefert Poster, Banner und Fanart.
- `GET /api/episodes/{episodeId}/background-image` liefert generierte Episodenhintergründe.

## Unterstützte Stream-Typen

Der Stream-Endpunkt akzeptiert `movie` und `tvshowepisode`. Content-Types werden aus der Dateiendung abgeleitet; unbekannte Dateitypen werden als `application/octet-stream` ausgeliefert.

## Weitere Dokumentation

- [API-Vertrag](./API.md)
- [Episode Selection](./TECH_Episode_Selection.md)
