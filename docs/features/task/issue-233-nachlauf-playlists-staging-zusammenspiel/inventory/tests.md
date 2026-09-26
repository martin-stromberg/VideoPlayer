# Tests und Testausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-26, 14:30 UTC+2
- **Branch und Commit-ID:** `task/issue-233-nachlauf-playlists-staging-zusammenspiel`, Commit `b7db299` (HEAD)
  - Commit-Nachricht: "Playlists für Serien, Staffeln, Episoden, Filme und Filmsammlungen (#207) (#234)"
- **Uncommittete Änderungen:** 
  - `?? docs/features/task/customer-feedback.md` (nicht anfassen, siehe Anweisung)
  - `?? docs/features/task/issue-233-nachlauf-playlists-staging-zusammenspiel/` (neue Datei, gerade erstellt)
- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET SDK: 10.0.401
  - Test-Framework: xUnit
  - Browser-Test: Playwright (Chromium)
  - Compiler/Toolchain: `dotnet` CLI, `dotnet test` für Test-Ausführung
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Projekt-Testdatei: `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  - Ausführung: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build --logger "console;verbosity=minimal"`
  - Release-Build erfolgte vorher (Bestandsaufnahme prüft gegen Release-Binaries)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build --logger "console;verbosity=minimal"` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 1500 | 0 | 0 | [test-results/lauf-1-ausgabe.txt](test-results/lauf-1-ausgabe.txt) |

## Nachgewiesene bestehende Testfehler

**Keine Testfehler im Ausgangslauf nachgewiesen.**

Der Analysebericht erwähnt 3 flackernde Tests (F8), die ~25% Fehlerquote in Einzelläufen hatten:
- `PlaylistMediaSearchE2ETests.SelectSeason_CascadesOnlyThatSeasonsEpisodes`
- `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest`
- `BackupUploadE2ETests.BackupUpload_Interrupted_ResumesFromServerOffset`

**Im vorliegenden Ausgangslauf (2026-09-26) sind alle bestanden.** Flaky Tests sind inhärent nicht-deterministisch; sie fehlen möglicherweise beim nächsten Lauf wieder.

## Testlücken und Ausführungsprobleme

### Fehlende Test-Kombinationen

**A6 — Geräte-Kopplung + Playlists (F6):**
- Kein Test belegt, dass ein gekoppeltes Gerät (per QR-Bootstrap) Playlists sehen, abspielen und Fortschritt melden kann
- Kein Test für Geräte-Widerruf-Verhalten mit Playlists
- Kein Test für Playlist-Cover über `?access_token=`-Query-Parameter
- Kein Test für Weiterschauen-Meldung mit `playlistId` von einem Gerät

**Vorhandene Pairing-Test-Helfer (wiederverwendbar):**
- `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory`
- `VideoWebPlayer.Tests/Helpers/PairingTestDb`
- `VideoWebPlayer.Tests/Helpers/PairingCryptoHelper`
- Muster aus `PairingBootstrapEndpointsTests`

**A7 — Lokale Medienquellen + Playlists (F7):**
- Kein Test kombiniert lokale Verzeichnisse mit Playlists
- Fehlt: Wiedergabe aus Playlist mit lokaler Quelle
- Fehlt: Backfill-Marker nach lokalem Scan
- Fehlt: Cover-Collage aus lokalen Bildern
- Fehlt: Weiterschauen-Ersatz bei lokaler Quellenlöschung

**Vorhandene Helfer (wiederverwendbar):**
- `VideoWebPlayer.Tests/Services/LocalMediaSourcePipelineE2ETests` (echte Verzeichnisse)
- `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase` (Playlist-Fixture)

### Flackernde Tests (F8)

**Datei:** `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

**Methode `SelectSearchResultAsync` (Zeile 133–144):**
```csharp
protected async Task SelectSearchResultAsync(string searchTerm, string mediaType, long mediaId)
{
    await ShowAddModeAsync();
    await Page.FillAsync(".media-search-input", searchTerm);
    var resultLocator = Page.Locator($".media-search-result[data-media-type='{mediaType}'][data-media-id='{mediaId}']");
    await resultLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });  // ← PROBLEM
    await resultLocator.ClickAsync();
    await Page.WaitForSelectorAsync("#playlist-entries-status");
    await Page.WaitForTimeoutAsync(1000);  // ← PROBLEM: feste Zeit statt Zustand
}
```

**Probleme:**
1. **Timeout 5000 ms:** Unter Last zu kurz für die Suche + Blazor Server Roundtrip
2. **WaitForTimeoutAsync(1000):** Feste Wartezeit statt Warten auf Zustandsänderung
3. **Auswirkung:** Zwei Test-Klassen nutzen diese Methode:
   - `PlaylistMediaSearchE2ETests`
   - `PlaylistPlaybackE2ETests`

**Fehler bei Timeout:** `TimeoutException: Timeout 5000ms exceeded`, wartend auf `.media-search-result[…]` oder `#playlist-mode-add-button`

**Noch nicht behoben:** Die Hilfsfunktion wartet auf feste Zeiten und auf Element-Präsenz, nicht auf echte Zustandsänderung (z. B. API-Antwort erhalten, Formular hat Focus, usw.).

## Testklassen

### Pairing und Bootstrap

- `ApiTokenConfigurationTests` — X-API-Key-Gate-Tests
- `ApiTokenScopeTests` — Scope-Validierung
- `DevicePairingE2ETests` — Code-basiertes Pairing
- `PairingBootstrapE2ETests` — QR-Bootstrap-Flow
- `PairingBootstrapEndpointsTests` — Endpunkt-Tests
- `PairingServiceTests_CodeCreation` — Code-Erstellung
- `PairingServiceTests_Exchange` — Code-Einlösung
- `PairingExchangeContractTests_Flow` — Vertragstest
- `DeviceTokenServiceTests` — Token-Speicherung und Widerruf
- `RefreshTokenServiceTests` — Token-Rotation

### Playlists (ohne Geräte-Kombination)

- `PlaylistsE2ETestBase` — Fixture für Browser-Tests
- `PlaylistEntriesE2ETests` — Eintrag-Verwaltung
- `PlaylistMediaSearchE2ETests` — Titel-Suche und Hinzufügen (flaky)
- `PlaylistPlaybackE2ETests` — Wiedergabe (flaky)
- `PlaylistMediaSearchE2ETests` — weitere Szenarien

### Backup-Restore

- `VideoWebPlayerBackupDataTests` — Backup-Export/Restore
  - Einzelne Tests für fehlende Spalten (`ReadFromAsync_LegacyBackupWithoutX_RestoresWithDefault`)
  - Fehlt: Tests für fehlende **Tabellen** `PairedDevices`, `PairingCodes`, `RefreshTokens`

### API-Dokumentation

- `ApiDocumentationContractTests` — Vertragstest für dokumentierte Routen
  - Umfasst: Health, Login, Standard-Medien-Routen
  - Fehlt: Playlist-Routen, Session-Endpunkte

### Lokale Medienquellen (ohne Playlist-Kombination)

- `LocalMediaSourcePipelineE2ETests` — Scan und Klassifizierung
- `LocalMediaSourceStreamingE2ETests` — Streaming
- `MediaSourceLocalDirectoryE2ETests` — Verzeichnis-Verwaltung
- Weitere: `LocalMediaSourceReaderTests`, `LocalMediaSourceClassifierTests`

## Hilfsmethoden und Fixtures

### PlaylistsE2ETestBase
- `SelectSearchResultAsync(searchTerm, mediaType, mediaId)` — Titel suchen und hinzufügen (flaky, Zeile 133)
- `ShowAddModeAsync()` — In den "Titel hinzufügen"-Bereich wechseln
- `ShowEntriesAsync()` — In die Titelliste wechseln
- `SelectEntryAsync(mediaType, mediaId)` — Titel auswählen

### PairingWebApplicationFactory
- Stellt einen gehosteten Server für Pairing-Tests bereit (Fixture für E2E-Tests)
- Wiederverwendbar für A6-Tests

### PairingTestDb
- Vorbelegte Test-Datenbank mit Pairing-Daten
- Wiederverwendbar für A6-Tests

### Weitere
- `LocalMediaSourcePipelineE2ETests` — echte Verzeichnisse erstellen
- `VideoWebPlayerBackupData` — Backup-Klasse selbst testbar via `WebApplicationFactory`

## Zusammenfassung Testlücken

| Anforderung | Testlücke | Größe | Schweregrad |
|-------------|-----------|-------|------------|
| A1 | Fehlende Tabellen-Regressionstests (OptionalRestoreTables) | 3 Tests | Hoch |
| A2 | Vertragstest für 17 Playlist-Endpunkte + Session-Endpunkte | 1 Test (umfassend) | Hoch |
| A4 | Tests für 401→403 und 500→404 Korrekturen | ~6 Tests | Mittel |
| A5 | Keine Tests für neue Client-Methoden | 5+ Szenarien | Mittel |
| A6 | Keine Kombinationstests Gerät + Playlist | ~6 Szenarien | Mittel |
| A7 | Keine Kombinationstests Lokal + Playlist | ~4 Szenarien | Mittel |
| A8 | Keine Tests für Gerätedokumentation (manuelle Überprüfung) | - | Niedrig |
| A9 | Flackernde Tests (2 Tests mit festen Timeouts) | 2 Tests | Niedrig |
| A3 | Keine Tests für Release Notes (manuelle Überprüfung) | - | Niedrig |
