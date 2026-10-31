# Technische Anforderungsübersetzung: Automatisierter TV-Sender (Live-Stream aus der Mediathek)

**GitHub Issue:** #111 „Automatisierter TV-Sender"  
**Aufgaben-ID:** 6145c516-99f3-4953-bf29-a244634a23e2  
**Branch:** task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender  
**Erstellt:** 2026-10-20

---

## Fachliche Zusammenfassung

Das System wird um einen automatisierten „TV-Sender" erweitert: Ein Scheduler erzeugt aus administrierbaren Regeln (Serien zu festen Sendezeiten, Filme zur Primetime, Genre-/Formatrotation, Wiederholungen, Feiertags-Specials) einen fortlaufenden Programmplan aus Sende-Slots (Startzeit, Endzeit, Video). Eine Playlist-Engine bestimmt zur aktuellen Uhrzeit das laufende Video samt Offset, Restdauer und Nachfolger. Eine Streaming-Pipeline liefert den Plan als durchgehenden Live-Stream (HLS oder DASH) aus; Planänderungen erhalten gültige Slots und setzen verdrängte Fortsetzungen zum nächstmöglichen Zeitpunkt wieder ein. Da die Mediathek privat ist, werden Playlist- und Segment-Endpunkte authentifiziert ausgeliefert.

---

## Betroffene Klassen und Komponenten

### Neue Datenmodellklassen (Entities)

- **`TvChannel`** bzw. Sender-Entity (neu, falls mehrere Sender konfigurierbar sein sollen — Annahme, siehe Offene Fragen)
  - `Id` (long, PK), `Name` (string, RQ), `CreatedAt`, `UpdatedAt`
  - Navigation: `ScheduleRules`, `ScheduleSlots`

- **`ScheduleRule`** (neu) — eine Programmregel, vom Administrator zusammengestellt
  - `Id` (long, PK), `TvChannelId` (FK), `Name`/`Description`
  - `RuleType` (Enum, RQ): Serien-Sendezeit, Film-Primetime, Platzhalter/Füller, Genre-/Formatrotation, Wiederholung, Feiertags-Special
  - `Recurrence` (Enum oder Cron-ähnlicher Ausdruck): täglich, wöchentlich (Wochentag), einmalig, Feiertag
  - `StartTime`/`TimeOfDay` (TimeOnly), `Duration` oder `SlotCount` — angestrebte Sendezeit und Länge
  - Inhaltsauswahl-Referenzen (polymorph, analog `PlaylistEntry`): `TVShowId`, `MovieId`, `MovieCollectionId`, `GenreId`, `PlaylistId` — je nach `RuleType`
  - `Priority`/`Order` (int) — Vorrang bei Konflikten im Plan
  - `IsActive` (bool)
  - Annahme: Genau eine Inhaltsreferenz pro Regel ist gesetzt; die Kombination „Serien, Filme in Kombination mit Genres" wird über `GenreId` + Inhaltstyp abgebildet.

- **`ScheduleSlot`** (neu) — ein konkreter, vom Scheduler erzeugter Programmeintrag
  - `Id` (long, PK), `TvChannelId` (FK), `ScheduleRuleId` (long?, FK, optional bei manuell eingefügten Slots)
  - `StartAt`/`EndAt` (DateTime, RQ) — geplante Sendezeit
  - Videozuordnung polymorph: `MovieId` (long?, FK) oder `TVShowEpisodeId` (long?, FK)
  - `Status` (Enum): `Scheduled`, `Airing`, `Aired`, `Replaced`/`Invalidated`
  - `SequenceIndex` / Fortsetzungs-Kontext (z. B. Episodennummer), damit verdrängte Folgen später wieder eingeplant werden können
  - `CreatedAt`, `UpdatedAt`

- **`MediaDuration`** oder neue Eigenschaft `Duration`/`Runtime` (TimeSpan?) an `Movie` und `TVShowEpisode` bzw. `MediaItem` (Annahme, zwingend erforderlich)
  - Der Scheduler braucht die Laufzeit jedes Videos, um `EndAt` und den Offset zu berechnen. Im Datenmodell existiert heute **kein** Dauer-Feld (geprüft: `Movie`, `TVShowEpisode`, `MediaItem`, `MediaBaseEntry`). Das Feld muss neu eingeführt und beim Medien-Scan bzw. nachträglich befüllt werden.

- Optional: **`ScheduleProgress`** (neu) — Merker, welche Episode einer Serie als Nächstes an der Reihe ist, damit wiederkehrende Serien-Sendezeiten fortlaufend weiterschreiten (Fortsetzungslogik).

### Enums

- **`ScheduleRuleType`**: `SeriesFixedTime`, `MoviePrimeTime`, `Filler`, `GenreRotation`, `Repeat`, `HolidaySpecial`
- **`ScheduleRecurrence`**: `Daily`, `Weekly`, `Once`, `Holiday`
- **`ScheduleSlotStatus`**: `Scheduled`, `Airing`, `Aired`, `Replaced`
- **`LiveStreamFormat`**: `Hls`, `Dash` (Konfiguration, falls beide Varianten unterstützt werden)

### Logikklassen / Services

- **`IScheduleService` / `ScheduleService`** (Scheduler)
  - `GenerateScheduleAsync(channelId, from, to)` — erzeugt Slots aus den aktiven Regeln
  - `RebuildScheduleAsync(channelId)` — bei Regeländerung: gültige Slots bleiben, ungültige werden ersetzt
  - `RescheduleContinuationAsync(slotId)` — verdrängte Folge zur nächst passenden Gelegenheit wieder einplanen
  - `FillGapsAsync(channelId, from, to)` — Lücken mit Platzhalterclips füllen
  - Konfliktauflösung nach `Priority`

- **`IPlaylistEngine` / `PlaylistEngine`** (zentrale Live-Logik, Namenskonflikt mit bestehendem `IPlaylistService` beachten — ggf. `ILiveProgramService` o. ä.)
  - `GetCurrentProgramAsync(channelId, utcNow)` → aktueller Slot, Offset im Video, Restdauer
  - `GetNextProgramAsync(channelId, utcNow)` → nächster Slot
  - Rein zeitbasiert, zustandslos gegenüber Zuschauern

- **`ILiveSegmenter` / Segmentierungs-Prozess**
  - Erzeugt fortlaufend HLS-Segmente aus dem aktuell laufenden Video und schaltet am Slot-Übergang auf das nächste Video um
  - Annahme: erfordert einen externen Encoder (z. B. FFmpeg); im Projekt existiert heute **keine** FFmpeg-Anbindung und kein HLS/DASH — die Streaming-Pipeline ist komplett neu (siehe Offene Fragen zur Architektur-Alternative)

- **`IChannelAuthService`** oder Wiederverwendung bestehender Mechanismen
  - Schutz von Playlist- und Segment-Endpunkten: Session-Cookie (`IAuthService`/`CurrentUser` in `ApiBaseController`), signierte URLs oder Tokens (`DeviceTokenService`, `RefreshTokenService`, `PairingCode` existieren bereits)

- **`ILiveStatusService`** (DTO-Erzeugung für API)
  - Liefert `LiveStatusDto`: aktuelles Video, Offset, verbleibende Dauer, nächstes Video

- **`IPlaybackDurationService`** (neu, falls Dauer nicht aus NFO vorhanden)
  - Ermittelt/pflegt die Videolaufzeit (z. B. via ffprobe beim Scan)

### UI-Komponenten / Controller

- **`LiveTvController`** (neu, `api/livetv` o. ä., erbt `ApiBaseController`)
  - `GET /api/livetv/status` — aktuelles Video, Offset, Restdauer, nächstes Video
  - `GET /api/livetv/playlist.m3u8` — dynamisch erzeugte HLS-Playlist (authentifiziert)
  - `GET /api/livetv/segments/{name}` — Segmentauslieferung (authentifiziert)
  - `GET /api/livetv/program` — optional: Programmübersicht der nächsten Stunden

- **`ScheduleRulesController`** oder Admin-Endpoints (neu)
  - CRUD für `ScheduleRule`, nur für `IsAdmin`-Benutzer (Muster: `AdminSourcesController`, `SetPlaylistPublicAsync` in `PlaylistsController`)
  - Auswahl-Interaktion (eigener Fachpunkt): Der Administrator muss Serien, Filme, Filmsammlungen, Genres und ggf. Playlists aus dem bestehenden Datenbestand **auswählen und einer Regel zuordnen** können — dazu sind Such-/Auswahldialoge nötig, keine Eingabe interner IDs

- **Blazor-Komponenten** (unter `Components/`)
  - `LiveTvPlayerComponent` — `<video>`-Tag, ggf. mit hls.js-Fallback für Browser ohne native HLS-Unterstützung
  - `ScheduleRuleListComponent` / `ScheduleRuleEditComponent` — Regelverwaltung im Admin-Bereich
  - `ProgramGuideComponent` — optional: Anzeige des Programmplans

- **Hintergrund-Worker** (Muster: `PlaylistBackfillWorker`, `ContinueWatchingWorker`)
  - `ScheduleGeneratorWorker` — rollierende Planerstellung (z. B. täglich 24–48 h voraus), Signal nach Regeländerung (Muster: `PlaylistBackfillSignal`)
  - `LiveSegmenterWorker` — betreibt den Segmentierungs-Prozess (Architektur abhängig von der offenen Frage)

### Erweiterungen bestehender Komponenten

- **`ApplicationDbContext`** — DbSets und Konfigurationen (`Data/Configurations/`) für die neuen Entities; EF-Migration
- **`VideoWebPlayerBackupData`** — neue Tabellen/Spalten in `OptionalRestoreTables`/`OptionalRestoreColumns` samt Regressionstest für Alt-Backups (Pflicht gemäß Definition of Done)
- **`MediaSourceScanner`** / `LocalMediaSourceReader` — ggf. Ermittlung der Videolaufzeit beim Scan
- **Zugriffsprüfung** — `MediaSourceUser`/`UnlockedMediaEntry` auf geplante Inhalte anwenden (serverseitig, siehe Offene Fragen)

### Tests

- **Unit-Tests:** `ScheduleServiceTests` (Regelgenerierung, Wiederholungen, Konflikte, Lückenfüllung, Fortsetzungs-Wiedereinplanung nach Regeländerung), `PlaylistEngineTests` (Offset-/Restdauer-/Nachfolger-Berechnung zu beliebigen Zeitpunkten), Authentifizierungs-Tests für Playlist/Segmente
- **Integrationstests (echtes SQLite, nicht EF InMemory):** Unique-Indizes/FK/Löschverhalten der neuen Tabellen; Alt-Backup ohne neue Tabellen wiederherstellen
- **UI/Component-Tests:** Live-Player lädt Playlist; Regel-Editor; Programmführer
- Streaming-Pipeline: Tests der Segment-Playlist-Generierung ohne echten Encoder möglichst isoliert

---

## Implementierungsansatz

### 1. Datenmodell und Migration

- Neue Entities (`ScheduleRule`, `ScheduleSlot`, ggf. `TvChannel`, `ScheduleProgress`) und Dauer-Eigenschaft an Video-Entitäten; Konfigurationen unter `Data/Configurations/` wie bei `PlaylistConfiguration`/`PlaylistEntryConfiguration`
- Löschverhalten definieren: Löschen eines `Movie`/`TVShowEpisode` muss referenzierende `ScheduleSlot`s entweder invalidieren (`Status = Replaced`) oder kaskadieren — Entscheidung in Offene Fragen
- Registrierung in `VideoWebPlayerBackupData` + EF-Migration

### 2. Scheduler

- Regeln werden rollierend in einen Zeitplan expandiert: für jede aktive `ScheduleRule` werden Vorkommen im Planungshorizont berechnet und mit konkreten Videos befüllt (Serien: nächste ungesendete Episode via `ScheduleProgress`; Filme: nach `GenreId`/Sammlung; Lücken: `Filler`-Regeln)
- Bei Regeländerung: `RebuildScheduleAsync` vergleicht bestehende Slots mit dem neuen Plan — noch gültige Slots bleiben erhalten, ungültige werden als `Replaced` markiert/ersetzt; ersetzte Fortsetzungen (z. B. ausgelassene Folge) werden beim nächsten passenden Slot desselben Regelkontexts wieder eingeplant
- Auslöser: Änderungen an Regeln (Controller) und `MediaUpdateHub`/Scan-Events (neue Episoden), analog zum `PlaylistBackfillCoordinator`-Muster

### 3. Playlist-Engine

- Reine Zeitfunktion über `ScheduleSlot`: `Offset = utcNow − slot.StartAt`, `Remaining = slot.EndAt − utcNow`; Nachschlagen des nächsten Slots per sortierter Abfrage
- Wird sowohl vom Status-Endpunkt als auch von der dynamischen Playlist-Generierung genutzt — Single Source of Truth für den Live-Charakter

### 4. Streaming-Pipeline

- Zwei denkbare Architekturen (Entscheidung offen):
  - **Echte Segmentierung:** Ein Prozess (z. B. FFmpeg) segmentiert das aktuell laufende Video kontinuierlich zu HLS und wird am Slot-Übergang umgeschaltet — echter gemeinsamer Live-Stream, aber neue externe Abhängigkeit und hoher Ressourcenbedarf
  - **Virtueller Live-Stream:** Die HLS-Playlist wird serverseitig aus dem Zeitplan und den Originaldateien berechnet (`EXT-X-DISCONTINUITY` am Slot-Wechsel); Segmente werden on-the-fly aus den Quelldateien geschnitten oder der vorhandene Range-Stream (`/api/items/{type}/{id}/stream`) wird per Client-Logik simuliert — deutlich weniger Infrastruktur, aber eingeschränktes „echtes" Live-Verhalten
- Segmentauslieferung als Controller-Endpoints mit Caching-Headern

### 5. Authentifizierung

- Playlist- und Segment-Endpoints erfordern angemeldeten Benutzer (`CurrentUser`-Muster aus `ApiBaseController`) bzw. gekoppeltes Gerät (`DeviceTokenService`/`PairedDevice`/`RefreshTokenService`)
- Da `<video>`-Elemente und hls.js keine Authorization-Header mitsenden, ist für Segment-URLs entweder Session-Cookie oder signierte URL/Token im Query vorzusehen (Konfiguration/Entscheidung offen)
- Serverseitige Durchsetzung der Zugriffsrechte auf die enthaltenen Inhalte ist Pflicht (keine UI-Ausblendung als „Absicherung")

### 6. Frontend

- `LiveTvPlayerComponent` mit `<video>` + `src = /api/livetv/playlist.m3u8`; hls.js als Fallback (nuGet/npm-Abhängigkeit `hls.js` prüfen: Lizenz, Release-Build, CI)
- `ProgramGuideComponent` für „was läuft gerade / als Nächstes"
- Regel-Administration in den bestehenden Admin-Bereich einordnen

---

## Konfiguration

1. **Anwendungsebene** (`appsettings*.json`, Muster `PlaylistSettings`/`DiscoveryOptions` + `ProgramSettingsService`):
   - Planungshorizont des Schedulers (z. B. 48 h) und Generierungsintervall
   - Primetime-Fenster, Feiertagskalender-Quelle
   - Stream-Format (`Hls`/`Dash`), Segmentlänge (z. B. 4–6 s), Segment-Speicherort/Retention
   - Authentifizierungsmodus für Segmente (Cookie vs. signierte URL, Token-Lebensdauer)

2. **Administrator-spezifisch (pro Sender/Regel):**
   - Regelwerk: Sendezeiten, Wiederholungen, Inhaltsauswahl (Serie/Film/Genre/Sammlung/Playlist), Priorität, Aktiv-Flag
   - Auswahl der Platzhalterclips (Quelle/Genre/eigene Sammlung)

3. **Pro Slot/Datensatz:**
   - Manuell erzeugte Slots ohne Regel (falls vorgesehen), Fortsetzungsstand `ScheduleProgress`

---

## Offene Fragen und Annahmen

### Annahmen (zu klären kennzeichnen):

1. **Ein Sender:** Annahme: Es gibt zunächst genau einen automatisierten Sender; `TvChannel` wird trotzdem als Entity modelliert, um spätere Mehrkanalfähigkeit nicht zu verbauen.
2. **HLS als Primärformat:** Annahme: HLS wird umgesetzt (im Issue „HLS oder DASH"); DASH bleibt optional/nachgelagert.
3. **Platzhalterclips:** Annahme: Platzhalter sind reguläre Videos aus der Mediathek, die über eine eigene Regel/Auswahl (z. B. dedizierte `MediaSource`, Genre oder Playlist) gekennzeichnet werden — kein neuer Inhaltstyp.
4. **Serien-Fortsetzung:** Annahme: „Fortsetzungen" meint die nächste Episode derselben Serie in Sendedreihenfolge (Staffel/Episode), nicht die Weiterschauen-Position einzelner Benutzer.
5. **Gemeinsamer Stream:** Annahme: Alle Zuschauer sehen denselben, zeitgleichen Programmpunkt (kein per-Benutzer-Stream, kein Timeshift/Pause) — „echtes TV-Gefühl".

### Zu klärende Punkte:

1. **Architektur der Streaming-Pipeline:** Echte Live-Segmentierung (FFmpeg-Prozess, Transcodierung, CPU-Last, externe Abhängigkeit + Lizenz-/CI-Prüfung) vs. virtueller Live-Stream (serverseitig berechnete Playlist über vorhandene Dateien). Das ist die weitreichendste technische Entscheidung und beeinflusst Build, Deployment und Hardware-Anforderungen — vor Implementierung mit dem Auftraggeber festzulegen.
2. **Videolaufzeit:** Es existiert kein Dauer-Feld im Datenmodell. Soll die Laufzeit per ffprobe beim Scan ermittelt, aus NFO-Dateien gelesen oder geschätzt werden? Neuer Scan-Schritt nötig.
3. **Zugriffsrechte im Sender:** Was passiert, wenn ein geplantes Video für einen Zuschauer nicht freigeschaltet ist (`UnlockedMediaEntry`/`MediaSourceUser`)? Wird der Sender nur aus für alle freigegebenen Inhalten geplant, oder wird pro Zuschauer gefiltert/ersetzt? Bei einem gemeinsamen Stream ist nur Ersteres konsistent.
4. **Löschverhalten geplanter Inhalte:** Wird ein Video gelöscht, das bereits in `ScheduleSlot`s eingeplant ist — Slot invalidieren und durch Ersatz füllen, oder Kaskade?
5. **Feiertage:** Soll ein deutscher Feiertagskalender fest hinterlegt sein, oder konfigurierbar (Bundesland)?
6. **Auth für `<video>`/hls.js:** Session-Cookie (gleiche Site) vs. signierte Segment-URLs — mit Blick auf gekoppelte Geräte (`PairedDevice`) zu entscheiden.
7. **Browser-Unterstützung:** Ist hls.js als neue Client-Abhängigkeit akzeptabel (Safari kann HLS nativ, Chrome/Firefox nicht)?
8. **Mehrere parallele Sender?** Soll die Regelverwaltung mehrere Sender/Kanäle unterstützen, oder reicht ein globaler Sender?
9. **Programmplan-Sichtbarkeit:** Sehen normale Benutzer den Programmplan (EPG) oder nur „Jetzt/Gleich"?
10. **Verhalten bei Server-Neustart:** Der Stream muss sich am Zeitplan neu synchronisieren (kein Fortsetzen eines Prozesszustands) — bestätigen, dass rein zeitbasierte Wiederaufnahme ausreicht.
</content>
