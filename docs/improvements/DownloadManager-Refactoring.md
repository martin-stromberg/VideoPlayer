# Refactoring-Vorschlag: DownloadManager

**Datum:** 14.03.2026  
**Autor:** CodeReviewAgent

## Problemstellung

Die Klasse `DownloadManager` ist aktuell sehr umfangreich und übernimmt zahlreiche Verantwortlichkeiten:
- Datenbankzugriffe (SQLite)
- Dateisystemoperationen (Downloads, Bilder)
- Netzwerkzugriffe (Metadaten, Bilder)
- Event-Publishing
- Periodische Hintergrundaufgaben

Diese starke Kopplung erschwert:
- Wartbarkeit und Übersichtlichkeit
- Testbarkeit (Unit-Tests)
- Erweiterbarkeit (z.B. neue Download-Typen, alternative Speicherstrategien)

## Ziel

- **Single Responsibility Principle**: Jede Klasse/Komponente soll genau eine klar umrissene Aufgabe haben.
- **Testbarkeit**: Abhängigkeiten sollen per Dependency Injection eingebunden werden.
- **Wartbarkeit**: Komplexität wird reduziert, Verantwortlichkeiten werden klar getrennt.

## Lösungsvorschlag

### 1. Aufteilung in Services

- **IDownloadManager**: Koordiniert die Download-Logik, delegiert an spezialisierte Services.
- **IDownloadRepository**: Kapselt alle Datenbankzugriffe (CRUD für Downloads).
- **IFileService**: Verantwortlich für Dateioperationen (Speichern/Löschen von Videos und Bildern).
- **IMetadataService**: Holt Metadaten und Bilder vom Server.
- **IEventPublisher**: Schnittstelle für Event-Publishing (z.B. DownloadCompleted, DownloadDeleted).

### 2. Dependency Injection

- Alle Services werden per DI injiziert.
- Statische Zugriffe auf App.ServiceProvider, Preferences, etc. werden vermieden.

### 3. Verbesserte Fehlerbehandlung

- Verwendung eines Logging-Frameworks (ILogger).
- Fehler werden differenziert behandelt und ggf. an den Aufrufer weitergegeben.

### 4. Periodische Tasks

- Periodische Aufgaben (z.B. Cleanup) werden über einen dedizierten BackgroundService realisiert, der sauber gestoppt werden kann (CancellationToken).

## Migrationsvorgehen

1. **Schrittweise Extraktion**: Zunächst werden Methoden in neue Services ausgelagert, die alte Klasse delegiert.
2. **Einführung von Interfaces**: Für alle neuen Services werden Interfaces definiert.
3. **Anpassung der Aufrufer**: Konsumenten des DownloadManagers werden auf die neue Struktur umgestellt.
4. **Tests**: Unit-Tests für die extrahierten Services.
5. **Abschließende Bereinigung**: Entfernen nicht mehr benötigter statischer Zugriffe und Hilfsmethoden.

## Vorteile

- Bessere Übersicht und Wartbarkeit
- Einfachere Erweiterung (z.B. neue Download-Quellen)
- Verbesserte Testbarkeit
- Klare Verantwortlichkeiten

---

**Hinweis:** Dieser Vorschlag ist als Grundlage für die weitere Diskussion und Planung gedacht. Die konkrete Umsetzung kann in mehreren Iterationen erfolgen.
