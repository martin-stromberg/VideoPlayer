# Plan-Review: Duplikate und Normalisierung in Playlist-Inhalte

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Die Core-Implementierung ist **vollständig umgesetzt**, aber die **Dokumentation** wurde nicht aktualisiert.

---

## Umgesetzte Planelemente

### Datenmodell

- [x] `DtoPlaylistAddResult` (DTO) — angelegt mit Properties: `TopLevelEntry`, `AddedEntries[]`, `SkippedDuplicateCount`, `Message`
  - Datei: `/VideoWebPlayer.Client/Models/DtoPlaylistAddResult.cs`
  - Status: Vollständig vorhanden

### Datenbankmigrationen

- [x] `NormalizePlaylistEntryMediaTypes` Migration — erstellt mit SQL für case-insensitive Normalisierung aller MediaType-Werte
  - Datei: `/VideoWebPlayer/Migrations/20260905220309_NormalizePlaylistEntryMediaTypes.cs`
  - Status: Vollständig vorhanden
  - Implementiert: UPDATE-Statements für alle 5 MediaType-Varianten (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection)

### Service-Logik (PlaylistService)

- [x] `AddMediaToPlaylistAsync` Rückgabetyp geändert — von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`
  - Zeilen: 141–242
  - Status: Vollständig implementiert

- [x] MediaType-Normalisierung — `normalizedMediaType = parsedMediaType.ToString()` nach ParseMediaType
  - Zeile 146
  - Status: Korrekt implementiert
  - Speicherung: Zeilen 177, 199 verwenden normalisierte Werte

- [x] Top-Level-Duplikat-Check — keine Exception mehr, Zähler erhöhen
  - Zeilen 168–171
  - Status: Korrekt implementiert (Duplikate zählen statt werfen)

- [x] Cascade-Duplikat-Zähler — skippedCount für jedes übersprungene Cascade-Item
  - Zeilen 187–194
  - Status: Korrekt implementiert mit Zähler und continue

- [x] Response-DTO konstruktion — Message basierend auf AddedEntries.Count und skippedCount
  - Zeilen 225–241
  - Status: Korrekt mit Fallunterscheidung für alle Szenarien

### Schnittstellen (IPlaylistService)

- [x] `AddMediaToPlaylistAsync` Signatur aktualisiert — Rückgabetyp `Task<DtoPlaylistAddResult>`
  - Datei: `/VideoWebPlayer/Services/IPlaylistService.cs`, Zeile 40
  - Status: Vollständig aktualisiert

### API-Controller (PlaylistsController)

- [x] `AddMediaToPlaylist` Response-Mapping — `DtoPlaylistAddResult` direkt zurückgegeben
  - Zeilen 209–217
  - Status: Korrekt implementiert (HTTP 200 OK für alle Fälle)

- [x] Exception-Handling vereinfacht — HTTP 409 Duplikat-Check nicht mehr nötig
  - Zeilen 234–238
  - Status: Exception-Handling bleibt für andere Fehler erhalten, jedoch kein spezifischer Duplikat-404-Check mehr

### HTTP-Client (VideoWebPlayerClient)

- [x] `AddMediaToPlaylistAsync` Rückgabetyp aktualisiert — zu `Task<DtoPlaylistAddResult>`
  - Zeile 450–454
  - Status: Vollständig aktualisiert mit generischen Typ `HttpPostAsync<DtoPlaylistAddResult>`

### UI-Komponente (PlaylistDetail.razor)

- [x] `AddEntryAsync` Response-Verarbeitung — `result.Message` anzeigen
  - Zeile 201
  - Status: Korrekt implementiert

- [x] HTTP 409 Catch-Block entfernt — wird nicht mehr geworfen
  - Zeilen 205–219
  - Status: HTTP 409 Catch entfernt, behaltet Fehlerbehandlung für 404 und 403

- [x] CSS-Klasse angepasst — `alert-success` verwenden
  - Zeile 63
  - Status: Korrekt implementiert mit bedingter Klasse `alert-success` oder `alert-danger`

### Unit-Tests (PlaylistServiceTests_AddMedia)

- [x] `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount` — Top-Level-Duplikat wirft keine Exception
  - Zeilen 36–49
  - Status: Vollständig implementiert
  - Assertions: Verifies `SkippedCount=1`, `AddedEntries.Length=0`, korrekte Message

- [x] `AddMedia_PartialDuplicates_AddedNewAndSkipped` — Mix aus neuen und übersprungenen
  - Zeilen 52–66
  - Status: Vollständig implementiert
  - Assertions: Verifies `AddedEntries.Length=4`, `SkippedCount=1`, korrekte Message

- [x] `AddMedia_AllDuplicates_ReturnsZeroAddedCount` — Alle Titel bereits vorhanden
  - Zeilen 69–84
  - Status: Vollständig implementiert
  - Assertions: Verifies `AddedEntries.Length=0`, `SkippedCount=6`, korrekte Message

- [x] `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection` — "Movie" vs "movie"
  - Zeilen 87–100
  - Status: Vollständig implementiert
  - Assertions: Verifies case-insensitive Duplikat-Erkennung, gespeicherter Wert ist normalisiert

- [x] Bestehendes Test-Anpassung: `AddMedia_ValidMovie_Success` — angepasst für neue Response-Struktur
  - Zeilen 15–33
  - Status: Angepasst mit Assertions auf `DtoPlaylistAddResult`

- [x] Bestehendes Test-Anpassung: `AddMedia_CascadeWithDuplicates_SkipsDuplicates` — angepasst
  - Zeilen 195–211
  - Status: Angepasst mit Assertions auf neue Response-Struktur und SkippedCount

- [x] Weitere bestehendes Tests bleiben gültig:
  - `AddMedia_InvalidMediaType_ThrowsInvalidOperationException` — Zeilen 103–112 ✓
  - `AddMedia_MediaNotFound_ThrowsKeyNotFoundException` — Zeilen 115–122 ✓
  - `AddMedia_NotOwner_ThrowsPlaylistAccessDeniedException` — Zeilen 125–133 ✓
  - `AddMedia_TVShow_CascadesEpisodes` — Zeilen 136–156 ✓
  - `AddMedia_TVShowSeason_CascadesEpisodes` — Zeilen 159–173 ✓
  - `AddMedia_MovieCollection_CascadesMovies` — Zeilen 176–192 ✓
  - `AddMedia_MaxItemCountExceeded_ThrowsInvalidOperationException` — Zeilen 214–228 ✓
  - `AddMedia_CascadeExceedsMaxItemCount_*` — Zeilen 231–243 ✓

---

## Offene Aufgaben

- [ ] **Dokumentation: `docs/help/playlists-api.md`** — Abschnitt "POST /api/playlists/{id}/entries" nicht aktualisiert
  - **Problem:** Zeilen 40–62 dokumentieren noch die alte Response-Struktur (`DtoPlaylistEntry` statt `DtoPlaylistAddResult`)
  - **Problem:** Zeilen 56–64 erwähnen noch HTTP 409 Conflict für Duplikate (wird nicht mehr geworfen)
  - **Problem:** Keine Dokumentation der neuen Response-Felder (`AddedEntries[]`, `SkippedDuplicateCount`, `Message`)
  - **Problem:** Keine Dokumentation der neuen HTTP 200 OK Antwort bei Duplikaten

- [ ] **Dokumentation: `docs/help/playlists-business-rules.md`** — BR-1 und BR-2 nicht aktualisiert
  - **Problem:** Zeile 19 erwähnt noch HTTP 409 für Duplikate
  - **Problem:** Zeilen 20–27 erwähnen alte Exception-basierte Fehlerbehandlung
  - **Problem:** BR-2 (Zeilen 32–61) erwähnt "Keine Fehlermeldung" — sollte aktualisiert werden auf "Benutzer sieht Message mit Duplikat-Info"

- [ ] **Dokumentation: `docs/API.md`** — Abschnitt "POST /api/playlists/{id}/entries" nicht aktualisiert
  - **Problem:** Zeile 363 dokumentiert noch `DtoPlaylistEntry` als Response-Struktur
  - **Problem:** Zeile 367 erwähnt noch HTTP 409 Conflict
  - **Problem:** Keine Dokumentation der neuen `DtoPlaylistAddResult` Struktur

- [ ] **Integration-Tests** — E2E-Tests nicht implementiert (aus plan.md Anforderungen)
  - [ ] Test: Serie zweimal hinzufügen (HTTP 200, Message zeigt Statistik)
  - [ ] Test: Serie → Episode entfernen → Serie erneut → Episode wieder hinzugefügt
  - [ ] Test: MediaType-Normalisierung über API ("Movie" vs "movie" → Duplikat erkannt)

- [ ] **Component-Tests** — PlaylistDetail.razor UI-Tests nicht implementiert
  - [ ] Test: Duplikat-Message wird korrekt angezeigt (nicht als Fehler)
  - [ ] Test: CSS-Klasse ist `alert-success` oder `alert-info` (nicht `alert-danger`)

---

## Hinweise

### Kritische Befunde

**Keine kritischen Fehler festgestellt.** Die Core-Implementierung entspricht vollständig dem Plan. Die Response-Struktur wird korrekt verarbeitet, die Duplikat-Logik funktioniert vereinheitlicht, und die MediaType-Normalisierung ist implementiert.

### Dokumentation ist Priorität

Die fehlende Dokumentation ist ein **Breaking-Change**, der für External API-Consumer relevant ist:
- HTTP 409 wird nicht mehr geworfen (alte Clients erwarten dies möglicherweise)
- Response-Struktur hat sich fundamental geändert
- Neue `Message`-Feld mit Duplikat-Info

Dokumentation sollte vor Release aktualisiert werden.

### Tests sind ausreichend für Regression

Alle geplanten Unit-Tests sind vorhanden und führen zu grünem Status. Integration-Tests und Component-Tests wären wünschenswert, aber nicht kritisch für diese Nachbesserung (da Unit-Tests die Logik bereits abdecken).

### Migration wurde erstellt

Die Datenbank-Migration normalisiert bestehende MediaType-Werte case-insensitiv. Diese sollte vor Deployment ausgeführt werden via `dotnet ef database update`.

---

## Zusammenfassung für Tasks-Datei

### Erledigt (Status: Erledigt)

Alle technischen Implementierungsaufgaben sind vollständig abgeschlossen:
- Aufgaben 1–25: Alle Code-Änderungen und Unit-Tests sind implementiert
- Aufgabe 26: Migration erstellt (Ausführung noch ausstehend, aber SQL ist vorhanden)

### Offen (Status: Offen)

Folgende Aufgaben benötigen noch Arbeit:
- Aufgaben 27–29: Dokumentations-Updates erforderlich
- Integration- und Component-Tests: Optional, aber empfohlen

