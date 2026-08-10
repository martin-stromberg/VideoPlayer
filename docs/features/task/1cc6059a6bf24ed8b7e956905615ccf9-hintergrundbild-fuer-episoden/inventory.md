# Bestandsaufnahme: Dynamische Hintergrundbild-Generierung für Episoden

Diese Bestandsaufnahme dokumentiert den aktuellen Stand des Projekts bezogen auf die Implementierung von dynamisch generierten Hintergrundbildern für Episode-Detailansichten. Analysiert wurde der Quellcode in `VideoWebPlayer`, `VideoWebPlayer.Client` und Test-Projekten.

---

## Zusammenfassung

### Was bereits vorhanden ist

**Datenmodell:**
- ✅ `TVShowEpisode`, `TVShowSeason`, `TVShow` mit umfangreicher Struktur
- ✅ `Picture` Klasse für Bilder-Verwaltung (Id, MediaItemId, Type, Data, ContentType, Width, Height)
- ✅ `MediaBaseEntry` als Basis für alle Medien-Einträge (mit PosterPictureId, BannerPictureId, FanartPictureId)

**Services & Logik:**
- ✅ `MediaSourceClassifier` — Klassifizierung von MediaItems/Collections, Zuordnung von Bildern
- ✅ `MediaSourceScanner` — Scanning von Medienquellen für neue/aktualisierte Elemente
- ✅ `EventManager` — Publish/Subscribe Event-System für Benachrichtigungen
- ✅ `VideoWebPlayerBackupDataProvider` — Backup/Restore-Funktionalität

**UI-Komponenten:**
- ✅ `TVShowDetails.razor` — Episode-Detailansicht mit Header-Background (aktuell Banner/Fanart)
- ✅ `VideoWebPlayerClient` mit DTOs für Client-Kommunikation
- ✅ API-Endpoints für Bildbereitstellung (`/api/pictures/{id}`)

**Tests:**
- ✅ Test-Projekte vorhanden (VideoWebPlayer.Tests, VideoWebPlayer.Maui.Tests)
- ✅ Bestehende Tests für Backups, Background-Processing

### Was noch fehlt

**Datenmodell-Erweiterungen:**
- ❌ `TVShowEpisode.GeneratedBackgroundImageId` — Referenz zu generiertem Bild
- ❌ `TVShowEpisode.BackgroundImageRequiresUpdate` — Flag für Neugenerierung
- ❌ `TVShowEpisode.BackgroundImageGeneratedAt` — Zeitstempel der Generierung
- ❌ `Picture.IsGeneratedBackground` — Kennzeichnung generierter Bilder
- ❌ `Picture.EpisodeIdReference` — Back-Reference für Queries

**Services (komplett neu):**
- ❌ `EpisodeBackgroundImageGenerator` — Bildverarbeitung (Skalierung, Farbextraktion, Canvas, Overlay)
- ❌ `EpisodeBackgroundImageService` — Business-Logic (Lazy-Load, Persistierung, Caching, Thread-Safety)
- ❌ `EpisodeBackgroundImageMapper` — DTO-Mapper für Blazor (optional)

**UI-Integration:**
- ❌ Rendering des generierten Background-Images im Header von TVShowDetails.razor
- ❌ Schleier-Effekt (Tint-Overlay) CSS/HTML
- ❌ Asynchrones Laden des Background-Images
- ❌ API-Endpoint: `GET /api/episodes/{episodeId}/background-image`

**Integration mit bestehenden Prozessen:**
- ❌ Scanner: Flagging bei neuem Fanart (BackgroundImageRequiresUpdate setzen)
- ❌ Klassifizierer: Integration in Episode-Verarbeitung
- ❌ Backup: Ausschluss generierter Bilder oder Regenerierungs-Strategie

**Tests:**
- ❌ `EpisodeBackgroundImageGeneratorTests` — Unit-Tests für Bildverarbeitung
- ❌ `EpisodeBackgroundImageServiceTests` — Tests für Caching, Thread-Safety, Persistierung

**Konfiguration:**
- ❌ `appsettings.json` Sektion für EpisodeBackgroundImage-Optionen
- ❌ Service-Registrierung in `Program.cs`

---

## Details

Detaillierte Informationen zum aktuellen Stand finden Sie in den folgenden Dokumenten:

- [Datenmodellklassen](inventory/models.md) — Aktuell vorhandene und geplante Properties
- [Logik-Klassen und Services](inventory/logic.md) — Existierende Services und geplante neue Services
- [UI-Komponenten](inventory/ui_components.md) — Razor-Komponenten und API-Endpoints
- [Tests und Test-Hilfsmittel](inventory/tests.md) — Bestehende und geplante Test-Klassen

---

## Architektur-Übersicht

### Bestehender Data-Flow

```
Scanner (ScanCollectionTreeAsync)
    ↓
[MediaItems geändert / neu erkannt]
    ↓
Klassifizierer (ProcessEpisodesForTVShowAsync)
    ↓
[Bilder zugeordnet via AssignPicturesToTVShowEpisodeAsync]
    ↓
TVShowEpisode mit PosterPictureId, BannerPictureId, FanartPictureId
```

### Geplanter Data-Flow (mit Background-Generierung)

```
Scanner (ScanMediaCollectionAsync)
    ↓
[Fanart erkannt oder geändert]
    ↓
TVShowEpisode.BackgroundImageRequiresUpdate = true (Flag setzen)
    ↓
Klassifizierer / Video-Player-Aufruf
    ↓
EpisodeBackgroundImageService.EnsureBackgroundImageAsync()
    ↓
[Lazy-Loading + Generation wenn nötig]
    ↓
EpisodeBackgroundImageGenerator.GenerateBackgroundImageAsync()
    ↓
[Bildverarbeitung: Resize → Farbe → Canvas → Overlay]
    ↓
Picture (mit IsGeneratedBackground = true) speichern
    ↓
TVShowEpisode.GeneratedBackgroundImageId + GeneratedAt setzen
    ↓
In Memory-Cache speichern
    ↓
UI rendert background-image CSS
```

---

## Abhängigkeiten und Integration-Punkte

### Zu integrierende bestehende Services

| Service | Integrations-Punkt | Aktion |
|---------|-------------------|--------|
| `MediaSourceScanner` | Nach Erkennung neuem Fanart | Flag `BackgroundImageRequiresUpdate = true` setzen |
| `MediaSourceClassifier` | Episode-Verarbeitung | Optional: Triggern von Bildgenerierung |
| `EventManager` | Status-Updates | Publizieren von Events bei Generierung |
| `VideoWebPlayerBackupDataProvider` | Export/Restore | Generierte Bilder ausschließen oder Neugenerierungs-Strategie |
| `ApplicationDbContext` | Entity Framework Migration | Schema-Migration für neue Properties |

### Zu integrierende UI-Komponenten

| Komponente | Integration | Aktion |
|-----------|-------------|--------|
| `TVShowDetails.razor` | Header-Background | Rendering von GeneratedBackgroundImageId |
| `VideoWebPlayerClient` (DTOs) | DTO-Struktur | Optional: Erweiterung für Background-Image-Info |

---

## Technische Anforderungen (aus Anforderungsdokument)

**Bildverarbeitung:**
- System.Drawing oder SixLabors.ImageSharp für Cross-Platform-Support
- Proportionale Skalierung mit Seitenverhältnis-Erhalt
- Dominante-Farbe-Berechnung (Histogramm oder k-means)
- Canvas-Erstellung mit Zielmaßen (z.B. 1920×1080)
- Zentrierte Platzierung des Fanarts
- Tint-Overlay mit konfigurierbarer Opazität

**Persistierung:**
- Picture.Data als Binär-Speicherung
- Format: PNG (Qualität) oder JPEG (Größe) – **offen**

**Caching:**
- In-Memory Cache mit konfigurierbarer Dauer
- Thread-Safety durch Locks/AsyncLock

**Fehlerbehandlung:**
- Fallback auf Placeholder bei Fehler
- Logging via ILogger

---

## Offene Fragen aus Anforderung

(Gemäß Anforderungsdokument, Punkt "Offene Fragen")

1. Canvas-Zielmaßstäbe? (z.B. 1920×1080)
2. Bildformat und Komprimierung? (PNG vs JPEG, Kompressionsstufe)
3. Dominante-Farbe-Berechnung? (Algorithmus, Region)
4. Tint-Overlay-Intensität? (Feste Opazität oder kontrast-adaptiv)
5. Persistierung des Fanarts? (Binary in Picture.Data vs Dateisystem-Referenz)
6. Cache-Strategie? (Nur Image vs auch Intermediate Results)
7. Bestehende Placeholder-Logik? (Fallback-Bild definieren)
8. MAUI-Frontend? (Gilt Feature auch für Mobile-App)
9. Performance unter Last? (In-Memory Cache ausreichend?)
10. Rückwärts-Kompatibilität? (Episoden ohne Fanart → Fallback)

---

## Nächste Schritte (Implementierungs-Planung)

1. **Database Migration:** Schema-Migration für neue Properties
2. **Service-Implementierung:** EpisodeBackgroundImageGenerator + EpisodeBackgroundImageService
3. **Integration:** Scanner und Klassifizierer anpassen
4. **UI:** TVShowDetails.razor erweitern, API-Endpoint hinzufügen
5. **Testing:** Unit-Tests + Integration-Tests
6. **Backup:** Strategie für generierte Bilder definieren
7. **Konfiguration:** appsettings.json + Service-Registrierung
