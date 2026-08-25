# Download Management

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: MAUI-Team, Backend-Entwickler  
> **Letzte Aktualisierung**: 2026-08-25

Das Offline-Download-Management gehört zur ausgelagerten .NET-MAUI-App. Die Implementierung liegt im separaten MAUI-Repository unter `VideoWebPlayer.Maui/Services/DownloadManager.cs`; in dieser Arbeitskopie entspricht das `Sub-Repository/VideoWebPlayer.Maui/Services/DownloadManager.cs`. Die App nutzt den Web-API-Vertrag aus [API.md](./API.md).

## Web-relevante Verträge

- Mediendetails: `GET /api/items/{type}/{id}`
- Streams: `GET /api/items/{type}/{id}/stream`
- Downloads: `GET /api/items/{type}/{id}/download`
- Bilder: `GET /api/pictures/{id}`
- Weiterschauen-Fortschritt: `POST /api/continue-watching/progress`

## Repository-Grenze

Das Web-Repository stellt Daten, Streams und Bilder bereit. Persistenz der Offline-Dateien, lokale SQLite-Ablage, Queueing und mobile UI-Zustände werden im MAUI-Repository gepflegt.

Siehe auch:

- [API-Vertrag](./API.md)
- [SignalR-Implementation](./TECH_SignalR_Implementation.md)
- [Event-System](./TECH_Event_System.md)
