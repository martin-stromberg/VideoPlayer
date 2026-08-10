# Bestandsaufnahme: Tests und Test-Hilfsmittel

## Bestehende Test-Klassen für Episode-Verarbeitung

### Relevante Test-Dateien im Projekt

**VideoWebPlayer.Tests/**
- `BackgroundProcessingGateTests.cs` — Tests für Background-Processing-Gate (möglicherweise relevant für Klassifizierungs-Prozesse)
- `BackupsControllerAuthorizationTests.cs` — Tests für Backup-Autorisierung
- `BackupSettingsServiceTests.cs` — Tests für Backup-Einstellungen

**Keine direkt relevanten Test-Klassen gefunden für:**
- Episode-Generierung
- Bildverarbeitung
- Media-Klassifizierung (spezifisch)

---

## Noch nicht vorhanden (gemäß Anforderung)

Die folgenden Test-Klassen müssen neu implementiert werden:

### `EpisodeBackgroundImageGeneratorTests`
Testet die technische Bildverarbeitung.

**Geplante Test-Methoden:**
- `Test_ResizeImage_KeepAspectRatio()` — Image-Skalierung mit Seitenverhältnis-Erhalt
- `Test_GetDominantColor_ReturnsCorrectColor()` — Dominante Farbe-Ermittlung
- `Test_CreateCanvasWithScaledImage_PlacesImageCentered()` — Canvas-Erstellung und Platzierung
- `Test_ApplyTintOverlay_OpacityApplied()` — Schleier-Effekt-Anwendung
- `Test_GenerateBackgroundImage_WithValidFanart_ReturnsImage()` — End-to-End-Test mit validem Fanart
- `Test_GenerateBackgroundImage_WithMissingFanart_ReturnsNull()` — Error-Handling bei fehlendem Fanart

**Test-Daten:**
- Test-Bilder (z.B. einfache Testmuster)
- Verschiedene Auflösungen und Seitenverhältnisse
- Grauwerte für Farbberechnung

---

### `EpisodeBackgroundImageServiceTests`
Testet Business-Logic, Persistierung, Caching, Thread-Safety.

**Geplante Test-Methoden:**
- `Test_EnsureBackgroundImage_LazyLoads_OnFirstCall()` — Lazy-Load & Persist Pattern
- `Test_EnsureBackgroundImage_UsesCached_OnSubsequentCall()` — Cache-Verhalten
- `Test_EnsureBackgroundImage_ThreadSafe_ParallelRequests()` — Thread-Safety bei parallelen Requests
- `Test_MarkBackgroundImageForUpdate_SetsFlag_OnNewFanart()` — Update Flag bei neuem Fanart
- `Test_EnsureBackgroundImage_Regenerates_WhenFlagSet()` — Neugenerierung nach Flag-Setzung
- `Test_EnsureBackgroundImage_ReturnsFallback_OnGenerationError()` — Fallback-Logik

**Test-Fixtures/Mocks:**
- Mock `ApplicationDbContext` für Datenbankzugriffe
- Mock `EpisodeBackgroundImageGenerator` für Bildgenerierung
- Mock `IMemoryCache` für Cache-Verhalten
- Test-Daten für TVShowEpisode mit/ohne Fanart

---

## Test-Struktur-Empfehlungen

**Test-Projekt:** `VideoWebPlayer.Tests/` (bereits vorhanden)

**Naming-Konvention:** `{ClassName}Tests.cs`

**Framework:** Wahrscheinlich MSTest oder xUnit (basierend auf bestehenden Tests)

**Hilfsmethoden/Fixtures:**
- `CreateTestEpisode()` — Erzeugt TVShowEpisode mit Standard-Testdaten
- `CreateTestPicture()` — Erzeugt Picture mit Test-Bilddaten
- `GenerateTestImage(width, height)` — Erstellt einfaches Test-Bild

---

## Integration mit Bestehenden Tests

**BackgroundProcessingGateTests** könnte relevant sein für:
- Testen, dass Bildgenerierung nicht den Classification-Gate blockiert
- Monitoring von Background-Task-Performance

**BackupSettingsServiceTests** könnte erweitert werden für:
- Testen, dass generierte Bilder korrekt aus Backups ausgeschlossen werden
- Wiederherstellung mit Neugenerierung-Flag
