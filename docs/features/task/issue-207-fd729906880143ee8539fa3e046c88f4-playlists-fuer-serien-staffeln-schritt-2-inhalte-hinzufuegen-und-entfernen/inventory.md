# Bestandsaufnahme: Playlists – Inhalte hinzufügen und entfernen (Schritt 2)

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezogen auf die Anforderung "Playlists – Inhalte hinzufügen und entfernen (Schritt 2)" aus `requirement.md`. Dokumentiert wird nur, was bereits im Projekt existiert — keine Planung oder Implementierungsvorschläge.

---

## Zusammenfassung

**Bestehende Implementierungen:**
- ✅ Playlist-Datenmodell (`Playlist`) mit Grundeigenschaften (Name, Beschreibung, SortMode, etc.)
- ✅ PlaylistService mit CRUD-Operationen (Erstellen, Lesen, Aktualisieren, Löschen)
- ✅ PlaylistsController mit entsprechenden API-Endpoints
- ✅ DTOs für Playlist-Verwaltung (`DtoPlaylist`, `DtoCreatePlaylistRequest`, `DtoUpdatePlaylistRequest`)
- ✅ PlaylistSortMode-Enum (ByReleaseDate, Manual)
- ✅ Exception-Handling (`PlaylistAccessDeniedException`)
- ✅ Event-System (PlaylistCreatedEvent, PlaylistUpdatedEvent, PlaylistDeletedEvent)
- ✅ Event-Manager mit Publish/Subscribe-Mechanismus
- ✅ UI-Komponenten (PlaylistsList, PlaylistDetail, PlaylistForm, PlaylistDeleteConfirmationDialog)
- ✅ Umfangreiche Unit- und E2E-Tests für CRUD-Operationen
- ✅ EF Core Migrationen und Konfigurationen
- ✅ Authentifizierungs- und Berechtigungsprüfungen

**Fehlende Implementierungen für Schritt 2:**
- ❌ PlaylistEntry-Datenmodell (neu zu erstellen)
- ❌ DtoPlaylistEntry und Anfrage-DTOs (DtoAddMediaToPlaylistRequest, DtoRemoveMediaFromPlaylistRequest)
- ❌ Methoden für Inhalts-Verwaltung (AddMediaToPlaylistAsync, RemoveMediaFromPlaylistAsync, GetPlaylistEntriesAsync)
- ❌ Cascade-Logik für Medientypen (TVShow → Staffeln/Episoden, etc.)
- ❌ API-Endpoints für Einträge-Verwaltung
- ❌ Tests für neue PlaylistEntry-Funktionalität
- ❌ UI-Komponenten zur Anzeige und Verwaltung von Playlist-Einträgen

---

## Details

- [Datenmodell](inventory/models.md) — Bestehende und zu erstellende Datenmodellklassen
- [Logik](inventory/logic.md) — Service-Klassen und deren Methoden
- [DTOs](inventory/dtos.md) — Client-Modelle und Transfer-Objekte
- [Enums](inventory/enums.md) — Aufzähltypen und deren Werte
- [Controller](inventory/controllers.md) — API-Endpoints und HTTP-Methoden
- [Tests](inventory/tests.md) — Unit- und Integrationstests sowie Test-Hilfsmethoden
- [UI-Komponenten](inventory/ui.md) — Razor-Komponenten und UI-Struktur
- [Konfiguration](inventory/configuration.md) — Einstellungen, Exception-Handling und Events

