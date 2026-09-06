# Umsetzungsplan: Nachbesserung Entwicklungsschritt 3 – Playlist-Inhalte mit Titelbild, Freischaltung und korrekter Button-Logik

## Übersicht

Die Anforderung behebt drei unvollständige Aspekte der Playlist-Detailansicht:

1. **Titelbild pro Eintrag hinzufügen** – Neues Feld `PosterPictureId` in `DtoPlaylistEntry`, befüllt über existierende `MediaBaseEntry`-Bilder, angezeigt in Tabellenzeile mit Fallback auf Placeholder
2. **Echte Freischaltungsprüfung umsetzen** – `PlaylistService` erhält `IUnlockedMediaService` injiziert, `ToDto()` wird zu asynchroner Methode `ToDtoAsync()` und befüllt `IsAccessible` durch echte Prüfung gegen aktuellem Benutzer
3. **"Nicht startbar"-Logik verschieben** – "Entfernen"-Button wird von `IsAccessible`-Bindung befreit, damit Besitzer auch nicht freigeschaltete Einträge entfernen können; visuelle Ausgrauung bleibt erhalten

Der Plan umfasst Datenmodell-, Service-, UI- und Test-Änderungen. Die Implementierung folgt bestehenden Mustern der Codebasis (MediaTypeHandlers für skalierte Datenladung, Fallback-Bildladung wie in `MediaBaseEntryList.razor`, Async-Services wie `UnlockedMediaService`).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Async-Refaktorierung von `ToDto()` | Neue private Methode `ToDtoAsync()` parallel zu synchronem `ToDto()`, neue öffentliche Methode `GetPlaylistEntriesPagedAsync()` nutzt die async Variante | Aufruferseite ist bereits async (`GetPlaylistEntriesPagedAsync()`); Async/Await ist Standard für Datenbankzugriffe im Projekt; vermeidet `.Result`-Anti-Pattern; konsistent mit anderen Service-Methoden |
| Benutzer-Ermittlung in PlaylistService | Direktes Injizieren von `IAuthService`, Nutzung der `CurrentUser`-Property | Folgt bestehendem Pattern in `UnlockedMediaService`; nicht nötig, Parameter zu übergeben; zentralisierte Auth-Logik |
| Bildladung pro Eintrag | Fallback-Kette: `PosterPictureId` → `BannerPictureId` → `FanartPictureId` → Placeholder | Exakt wie in `MediaBaseEntryList.razor` implementiert; konsistent mit Projekt-Konvention; verhindert fehlende oder leere Bilder |
| Bild-URL-Format | `/api/pictures/{id}?access_token={token}` | Bestehendes Pattern aus `MediaBaseEntryList.razor` und `MediaBox.razor` |
| Asynchrone Architektur | `IUnlockedMediaService.IsUnlockedAsync()` wird für jeden Eintrag aufgerufen, keine Batch-Optimierung in dieser Phase | Einfacher, wartbarer Ansatz; Controller/Razor-Komponente ist bereits async; Batch-Optimierung kann in späterem Schritt erfolgen |
| Service-Abhängigkeiten | PlaylistService erhält zusätzlich `IUnlockedMediaService` und `IAuthService` injiziert | `IAuthService` ist notwendig für `CurrentUser`; `IUnlockedMediaService` ist notwendig für Freischaltungsprüfung; beide sind bereits im DI-Container registriert |

## Programmabläufe

### Abruf paginierter Playlist-Einträge mit Freischaltung und Bildern

1. `PlaylistDetail.razor` ruft `Client.RequestPlaylistEntriesPagedAsync()` auf
2. Client sendet HTTP-Request zu `PlaylistController.GetPlaylistEntriesPagedAsync()`
3. `PlaylistService.GetPlaylistEntriesPagedAsync()` wird aufgerufen
4. Für jeden `PlaylistEntry` wird `ToDtoAsync()` aufgerufen:
   - Titel aus `LoadTitlesForMediaRefsAsync()` wird ermittelt
   - `MediaBaseEntry` für Medientyp wird geladen und `PosterPictureId` extrahiert (oder Fallback auf `BannerPictureId`, `FanartPictureId`)
   - `IUnlockedMediaService.IsUnlockedAsync()` wird mit dem Media-DTO aufgerufen, um `IsAccessible` zu bestimmen
5. `DtoPlaylistEntry` wird mit gefüllten Werten (`PosterPictureId`, `IsAccessible`) zurückgegeben
6. `PlaylistDetail.razor` zeigt Eintrag mit Bild und reduzierter Opazität an (falls `!IsAccessible`)
7. "Entfernen"-Button ist nicht deaktiviert; nur visuell wird nicht freigeschalteter Status durch `.opacity-50` angezeigt

Beteiligte Klassen/Komponenten: `PlaylistService`, `IUnlockedMediaService`, `IAuthService`, `MediaTypeHandlers`, `PlaylistDetail.razor`, `MediaBox.razor`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| — | — | Keine neuen Klassen erforderlich |

## Änderungen an bestehenden Klassen

### `DtoPlaylistEntry` (DTO)
Datei: `VideoWebPlayer.Client\Models\DtoPlaylistEntry.cs`

- **Neue Eigenschaften:**
  - `PosterPictureId` (`long?`) — Optionale ID des Posterbildes, wird in UI für Bildanzeige genutzt; null, wenn keine Bild-ID verfügbar
- **Geänderte Eigenschaften:**
  - `IsAccessible` — Kommentar wird aktualisiert/entfernt, da dies nun echten Freischaltungsstatus widerspiegelt (nicht mehr Platzhalter)

### `PlaylistService` (Service)
Datei: `VideoWebPlayer\Services\PlaylistService.cs`

- **Neue Abhängigkeiten (Konstruktor):**
  - `IUnlockedMediaService _unlockedMediaService` — Für Freischaltungsprüfung pro Eintrag
  - `IAuthService _authService` — Für Zugriff auf aktuellen Benutzer (wird von `IUnlockedMediaService` verwendet, aber auch lokal für Kontext)
- **Neue Methode:**
  - `private async Task<DtoPlaylistEntry> ToDtoAsync(PlaylistEntry entry, string mediaTitle, string? parentMediaTitle, CancellationToken cancellationToken)` — Asynchrone Variante von `ToDto()`, lädt `PosterPictureId` und prüft Freischaltung via `IUnlockedMediaService.IsUnlockedAsync()`
- **Geänderte Methode:**
  - `GetPlaylistEntriesPagedAsync()` — Nutzt neue `ToDtoAsync()` statt `ToDto()`; Aufrufer müssen bereits async behandeln (sind bereits async)
- **Alte Methode (wird belassen für Kompatibilität mit nicht-paginierten Abfragen):**
  - `ToDto(PlaylistEntry, ...)` — Bleibt bestehen, wird aber evtl. durch `GetPlaylistEntriesAsync()` in späteren Schritten auch zu async umgebaut

### `PlaylistDetail.razor` (UI-Komponente)
Datei: `VideoWebPlayer\Components\Playlists\PlaylistDetail.razor`

- **Tabellenstruktur:**
  - Neue Spalte für Titelbild hinzufügen (vor oder nach "Typ"); sollte mit `<th>Bild</th>` in Thead und `<td>` in Tbody definiert sein
  - Bildtag in Tabellenzeile: Entweder direktes `<img>`-Tag mit Fallback-Logik oder `<MediaBox>`-Komponente (erste Variante einfacher für inline Darstellung in Tabelle)
  - URL-Logik: `PosterPictureId` → `/api/pictures/{id}?access_token={token}`; Fallback auf `/images/placeholder.png`
- **Button-Änderung:**
  - "Entfernen"-Button (Zeile 109): Entfernen von `disabled="@(!entry.IsAccessible)"` Binding
  - Button bleibt anklickbar für alle Einträge (auch nicht freigeschaltete)
- **Visuelle Darstellung:**
  - `.opacity-50` CSS-Klasse bleibt auf `<tr>` erhalten (Zeile 103 Bedingung: `if (!entry.IsAccessible)`)

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| — | — | Keine Datenbankmigrationen erforderlich (keine Änderungen am Schema; `PosterPictureId` wird aus bestehenden Media-Entitäten geladen, nicht in `PlaylistEntry` gespeichert) |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| — | — | Keine zusätzlichen Validierungen erforderlich (Existenz und Berechtigung von `PlaylistEntry` und Media-Entität werden bereits durch bestehende Logik geprüft) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| — | — | — | Keine Konfigurationsänderungen erforderlich |

## Seiteneffekte und Risiken

- **Async-Refaktorierung von `ToDto()`:** `GetPlaylistEntriesAsync()` (nicht-paginierte Variante, Zeile 506) nutzt aktuell `ToDto()` synchron. Diese Methode muss in späteren Schritten ebenfalls zu async umgebaut werden, um echte Freischaltung auch dort zu unterstützen. Für diese Anforderung ist dies nicht nötig (nur paginierte Variante ist Ziel), aber der Kommentar sollte hinzugefügt werden.
- **Performance:** Für jede Seite werden `count(pageSize)` × 2 oder mehr Datenbankzugriffe durchgeführt (Titel laden, Freischaltung prüfen, ggf. Bilder-IDs laden). Dies sollte bei typischen Seitengößen (z.B. 10–20 Einträge) acceptable sein; Batch-Optimierung ist eine Kandidat für Folgephasen.
- **IUnlockedMediaService-Limitation:** `IUnlockedMediaService.IsUnlockedAsync()` funktioniert nur für `DtoMovieCollection` und `DtoTVShow`. Für andere Medientypen in Playlists (Filme, Staffeln, Episoden) wird `false` zurückgegeben. Dies ist ein bekanntes Limitation der aktuellen Implementierung; Benutzer mit Playlists aus anderen Medientypen sehen diese als nicht freigeschaltet. Kann in separater Phase adressiert werden.
- **Berechtigungsprüfung:** Die Freischaltungsprüfung erfolgt auf Basis des `IAuthService.CurrentUser`; falls Auth-Kontext leer ist (z.B. bei HTTP-Test ohne gültige Session), werden alle Einträge als nicht freigeschaltet angezeigt. Dies ist beabsichtigtes Verhalten.

Keine bekannten Seiteneffekte auf andere Features außer den oben dokumentierten.

## Umsetzungsreihenfolge

1. **Datenmodell: `PosterPictureId` zu `DtoPlaylistEntry` hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: 
     - Feld `long? PosterPictureId { get; set; }` in `DtoPlaylistEntry.cs` hinzufügen
     - Optional: Kommentar aktualisieren oder entfernen, da Feld nun verwendet wird

2. **Service: Neue Dependencies zu `PlaylistService.ctor` hinzufügen**
   - Voraussetzungen: `IUnlockedMediaService` und `IAuthService` sind bereits in DI-Container registriert (Verifizierung in `Program.cs`)
   - Beschreibung:
     - `IUnlockedMediaService _unlockedMediaService` als Parameter zu Konstruktor hinzufügen
     - `IAuthService _authService` als Parameter zu Konstruktor hinzufügen
     - Beide als private Felder speichern
     - Sicherstellen, dass Registrierung in DI-Container korrekt ist (bereits vorhanden)

3. **Service: Neue private Methode `ToDtoAsync()` erstellen**
   - Voraussetzungen: Schritt 2 (Dependencies) abgeschlossen
   - Beschreibung:
     - Neue private Methode `private async Task<DtoPlaylistEntry> ToDtoAsync(PlaylistEntry entry, string mediaTitle, string? parentMediaTitle, CancellationToken cancellationToken)` erstellen
     - Logik: Kopie von `ToDto()`, mit folgenden Änderungen:
       - `await` vor `_unlockedMediaService.IsUnlockedAsync()` aufrufen, um echten Freischaltungsstatus zu ermitteln (statt `true` hart zu setzen)
       - DtoMediaEntry für den Medientyp von `entry` erstellen (basierend auf `entry.MediaType` und `entry.MediaId`), um an `IsUnlockedAsync()` zu übergeben
       - `PosterPictureId` aus der entsprechenden `MediaBaseEntry` laden (über `MediaTypeHandlers` oder neue Hilfsmethode `LoadPosterPictureIdAsync()`)
       - `AddedAt` in DTO abbilden
       - Fallback implementieren: Wenn `PosterPictureId` null ist, auf `BannerPictureId` prüfen, dann auf `FanartPictureId`
     - Hilfsmethode `private async Task<long?> LoadPosterPictureIdAsync(string mediaType, long mediaId, CancellationToken cancellationToken)` erstellen oder MediaTypeHandlers erweitern

4. **Service: `GetPlaylistEntriesPagedAsync()` anpassen, um `ToDtoAsync()` zu nutzen**
   - Voraussetzungen: Schritt 3 (ToDtoAsync-Methode) abgeschlossen
   - Beschreibung:
     - In `GetPlaylistEntriesPagedAsync()` die Konvertierung von `PlaylistEntry` zu `DtoPlaylistEntry` ändern
     - Statt `ToDto()` aufzurufen, `await ToDtoAsync()` aufrufen (in Schleife)
     - Sicherstellen, dass `CancellationToken` weitergegeben wird
     - Parallele Abfragen erwägen (z.B. `Task.WhenAll()` für alle Einträge der Seite auf einmal), wenn Performance ein Problem ist

5. **UI: Bildspalte zu `PlaylistDetail.razor` hinzufügen**
   - Voraussetzungen: Schritt 1 (DtoPlaylistEntry.PosterPictureId) abgeschlossen
   - Beschreibung:
     - Neue `<th>` in Thead für Bildspalte hinzufügen (Position: vor oder nach "Typ", empfohlen: am Anfang)
     - Neue `<td>` in Tbody mit `<img>` oder `<MediaBox>` für Bildanzeige hinzufügen
     - Bildquelle: 
       - `if (entry.PosterPictureId.HasValue) { url = `/api/pictures/{entry.PosterPictureId}?access_token={AuthorizationToken}` }`
       - `else if (...BannerPictureId...) { url = ... }`
       - `else if (...FanartPictureId...) { url = ... }`
       - `else { url = `/images/placeholder.png` }`
     - `onerror` Fallback hinzufügen, um auf Placeholder zu wechseln bei Fehler (wie in `MediaBox.razor`)

6. **UI: "Entfernen"-Button-Binding anpassen**
   - Voraussetzungen: Keine (unabhängig)
   - Beschreibung:
     - In `PlaylistDetail.razor` Zeile 109 das Binding `disabled="@(!entry.IsAccessible)"` entfernen
     - Button bleibt für alle Einträge anklickbar
     - CSS-Klasse `.opacity-50` bleibt auf `<tr>` erhalten (Bedingung bleibt bestehen)

7. **E2E-Test: Baseline-Test anpassen**
   - Voraussetzungen: Schritte 2–4 (Service-Logik) abgeschlossen
   - Beschreibung:
     - Existierenden Test `PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()` überprüfen und ggf. anpassen
     - Falls Freischaltung jetzt korrekt implementiert ist, können alle Einträge freigeschaltet sein (Test bleibt gültig)
     - Sicherstellen, dass kein Eintrag `.opacity-50` hat (Assertion bleibt gleich)

8. **E2E-Test: Neuer Test für nicht freigeschaltete Einträge**
   - Voraussetzungen: Schritte 2–4 (Service-Logik) abgeschlossen
   - Beschreibung:
     - Neuer Test: `PlaylistDetail_ShowsReducedOpacity_WhenEntryNotAccessible()`
     - Setup: Playlist mit mehreren Einträgen erstellen, mindestens einen Eintrag NICHT freischalten
     - Assertion: 
       - Nicht freigeschaltete Einträge zeigen `.opacity-50` CSS-Klasse
       - Freigeschaltete Einträge zeigen `.opacity-50` NICHT
       - "Entfernen"-Button ist sichtbar und nicht deaktiviert für nicht freigeschaltete Einträge
     - Test-Hilfsmethode notwendig: `RemoveUnlockedMediaEntryAsync(long mediaId, string userId)` oder ähnlich, um Freischaltung in Test zu manipulieren

9. **E2E-Test: Test für Bildspalte**
   - Voraussetzungen: Schritte 1 und 5 (Datenmodell und UI) abgeschlossen
   - Beschreibung:
     - Neuer Test: `PlaylistDetail_DisplaysImageForEachEntry()`
     - Assertion: 
       - Jede Tabellenzeile hat ein Bildelement (z.B. `<img>` mit `src` gesetzt)
       - Bilder laden erfolgreich (kein 404-Fehler in Browser-Console)
       - Placeholder-Bild wird angezeigt, wenn `PosterPictureId` null ist

10. **Dokumentation: Kommentare und Code-Review**
    - Voraussetzungen: Alle vorherigen Schritte abgeschlossen
    - Beschreibung:
      - Code-Kommentare hinzufügen für komplexe Logik (z.B. Fallback-Bildladung, async-Refaktorierung)
      - Sicherstellen, dass `ToDtoAsync()` Method als `private` bleibt, da nur von `GetPlaylistEntriesPagedAsync()` genutzt
      - Optional: `GetPlaylistEntriesAsync()` (nicht-paginierte Variante) mit TODO-Kommentar markieren für zukünftige Async-Refaktorierung

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `PlaylistDetail_ShowsReducedOpacity_WhenEntryNotAccessible()` | `PlaylistDetailE2ETests` | Nicht freigeschaltete Einträge werden mit `.opacity-50` angezeigt; freigeschaltete ohne diese Klasse |
| `PlaylistDetail_RemoveButton_EnabledForAllEntries()` | `PlaylistDetailE2ETests` | "Entfernen"-Button ist sichtbar und nicht deaktiviert, auch für nicht freigeschaltete Einträge |
| `PlaylistDetail_DisplaysImageForEachEntry()` | `PlaylistDetailE2ETests` | Jeder Eintrag zeigt Titelbild an; Fallback auf Placeholder bei fehlender `PosterPictureId` |
| `RemoveUnlockedMediaEntryAsync()` | `PlaylistsE2ETestBase` | Hilfsmethode: Entfernt Freischaltung für ein bestimmtes Medium für einen Benutzer (für Test-Setup) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()` | Baseline-Test überprüfen; sollte weiterhin gelten, da alle Einträge im Test freigeschaltet sind |
| `PlaylistDetailE2ETests` (allgemein) | Falls Tests auf `disabled`-Attribut des "Entfernen"-Buttons prüfen, diese Assertions entfernen/anpassen |

Falls keine bestehenden Tests betroffen sind durch Assertions auf `disabled`-Attribut: „Keine." — wird nach Code-Analyse überprüft

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Freischaltete Einträge sind normal sichtbar, nicht freigeschaltete mit Opazität | `PlaylistDetailE2ETests` | AC 2.2: Nicht freigeschaltete Einträge mit reduzierter Opazität angezeigt | Visuelle Darstellung ist UI-spezifisch; nur E2E kann Rendering und CSS-Klasse-Anwendung überprüfen |
| Pflicht | "Entfernen"-Button ist nicht deaktiviert für nicht freigeschaltete Einträge | `PlaylistDetailE2ETests` | AC 3.2: Playlist-Besitzer kann nicht freigeschaltete Einträge entfernen | Benutzeraktion und Button-Verhalten können nur über UI getestet werden |
| Pflicht | Titelbild wird für jeden Eintrag angezeigt | `PlaylistDetailE2ETests` | AC 1.2–1.4: `PosterPictureId` in DTO, Bildspalte in UI, Placeholder bei fehlend | Bildladung und Rendering sind UI/Browser-abhängig; nur E2E kann erfolgreiche Bildanzeige und Fallback-Verhalten überprüfen |
| Optional | Freischaltung wird korrekt bestimmt (Unit-Test der Service-Logik) | `PlaylistServiceTests` (Unit) | AC 2.1: `IsAccessible` wird korrekt mit Service-Ergebnis befüllt | Unit-Test mit Mock von `IUnlockedMediaService` kann Service-Integration prüfen; E2E deckt auch die Gesamtkette ab, aber Unit ist für schnelle Feedback wichtig |

Bestehende E2E-Tests, die betroffen sind: Überprüfung nach Step 6 erforderlich. Falls `disabled`-Attribut in Selektoren/Assertions verwendet wird, müssen diese angepasst werden.

## Offene Punkte

Keine.

---

## Notizen zur Entscheidungsfindung

Die folgenden Punkte aus der Anforderung wurden pragmatisch entschieden:

1. **IAuthService-Integration:** `PlaylistService` erhält `IAuthService` per Dependency Injection (wie in `UnlockedMediaService`). Der Zugriff auf den aktuellen Benutzer erfolgt über die `CurrentUser`-Property. Dies folgt dem bestehenden Muster und erfordert keine Parameter-Übergabe an `ToDto()`.

2. **Asynchrone Architektur:** `ToDto()` wird zu einer neuen privaten Methode `ToDtoAsync()` umgebaut. Die öffentliche Methode `GetPlaylistEntriesPagedAsync()` nutzt `ToDtoAsync()` und ist bereits async. Dies folgt dem Standard-Pattern für Datenbankzugriffe und vermeidet `.Result`-Blockierung.

3. **Bildladestrategie:** Das Fallback-Muster wird exakt wie in `MediaBaseEntryList.razor` implementiert: `PosterPictureId` → `BannerPictureId` → `FanartPictureId` → Placeholder. Dies ist eine bewährte Konvention der Codebasis.
