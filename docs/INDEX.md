# Dokumentations-Index

> Zentrale Übersicht über alle verfügbaren Dokumentationen der VideoWebPlayer-Solution

## 📚 Allgemeine Dokumentation (GUIDE_*)

Diese Dokumentationen richten sich an Endbenutzer, Administratoren und neue Entwickler.

| Dokument | Beschreibung | Status |
|----------|--------------|--------|
| [Installation & Setup](./GUIDE_Installation.md) | Vollständige Installations- und Konfigurationsanleitung | ⏳ Geplant |
| [Benutzerhandbuch](./GUIDE_User_Manual.md) | Anleitung zur Nutzung der Anwendung | ⏳ Geplant |
| [Feature-Übersicht](./GUIDE_Features.md) | Detaillierte Beschreibung aller Features | ⏳ Geplant |

## 🔧 Technische Dokumentation (TECH_*)

Diese Dokumentationen richten sich an Entwickler und beschreiben die technische Implementierung.

### Architektur & System

| Dokument | Beschreibung | Status |
|----------|--------------|--------|
| [Architektur-Übersicht](./TECH_Architecture.md) | System-Architektur und Design-Entscheidungen | ⏳ Geplant |
| [Database Schema](./TECH_Database_Schema.md) | Datenbankstruktur und Entitäten | ⏳ Geplant |

### Backend (ASP.NET Core / Blazor)

| Dokument | Beschreibung | Status |
|----------|--------------|--------|
| [SignalR-Implementation](./TECH_SignalR_Implementation.md) | Echtzeit-Update-System | ✅ Vollständig |
| [Automatisierte Programmupdates](./TECH_Auto_Update.md) | Selbst-Update via msTools.Updater inkl. Sicherung vor der Installation | ✅ Vollständig |
| [Media-Kategorisierung](./TECH_Media_Classification.md) | Automatische Video-Klassifizierung | ⏳ Geplant |
| [REST API Reference](./TECH_API_Reference.md) | API-Endpoints und Verwendung | ⏳ Geplant |
| [SignalR Hub Events](./TECH_SignalR_Events.md) | SignalR Event-Reference | ⏳ Geplant |

### Frontend (.NET MAUI)

| Dokument | Beschreibung | Status |
|----------|--------------|--------|
| [Event-System](./TECH_Event_System.md) | MAUI Notification Event Infrastructure | ✅ Vollständig |
| [Download-Management](./TECH_Download_Management.md) | Offline-Download-System | ⏳ Geplant |
| [Episode Selection](./TECH_Episode_Selection.md) | Smart Episode Selection & Play Button | ✅ Vollständig |
| [MediaElement Error Handling](./TECH_MediaElement_Error_Handling.md) | Video-Player Fehlerbehandlung | ✅ Vollständig |
| [Notification Ticker](./TECH_Notification_Ticker.md) | Footer-Lauftext-Komponente | ✅ Vollständig |

## 🎯 Komponenten-Dokumentation

Dedizierte Dokumentationen für einzelne Komponenten befinden sich direkt bei den Komponenten:

- `VideoWebPlayer.Maui/Services/Events/README.md` - Event-System Details
- `VideoWebPlayer.Maui/Components/NotificationTicker.README.md` - Ticker-Komponente

## 📖 Legende

| Symbol | Bedeutung |
|--------|-----------|
| ✅ | Vollständig dokumentiert |
| ⏳ | In Arbeit / Geplant |
| 🔄 | Benötigt Aktualisierung |

## Dokumentations-Namenskonvention

### Präfixe

- **GUIDE_** - Allgemeine Dokumentation für Benutzer und Administratoren
- **TECH_** - Technische Dokumentation für Entwickler
- **API_** - API-Referenz-Dokumentation

### Format

Alle Dokumentationen folgen diesem Format:

```markdown
# Titel - Dokumenttyp

> **Dokumenttyp**: [Technisch/Allgemein]  
> **Zielgruppe**: [Entwickler/Benutzer/Administratoren]  
> **Version**: [Version]  
> **Letzte Aktualisierung**: [Datum]

[Inhalt...]

## Related Documentation

- [Link zu verwandter Dokumentation]

---

**Siehe auch:**
- [Externe Links]
```

## Beitragen zur Dokumentation

### Neue Dokumentation erstellen

1. Datei im `Docs/`-Verzeichnis anlegen
2. Passenden Präfix verwenden (GUIDE_ oder TECH_)
3. Template-Format befolgen
4. Diesen Index aktualisieren

### Bestehende Dokumentation aktualisieren

1. Versionsnummer erhöhen
2. "Letzte Aktualisierung" aktualisieren
3. Änderungen in Git committen

## Feedback & Fragen

Für Fragen zur Dokumentation:
- **Issues**: [GitHub Issues](https://github.com/Muesli84/VideoPlayer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Muesli84/VideoPlayer/discussions)

---

**Hinweis**: Dieser Index wird regelmäßig aktualisiert. Stand: 2024
