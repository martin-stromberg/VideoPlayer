# Umsetzungsplan: Fortschrittsanzeige Laufzeitermittlung (N6)

## Übersicht

Die TV-Sender-Adminseite (`TVSenderAdmin.razor`, `/admin/tv-sender`, Admin-only) erhält eine
Status-Karte „Laufzeitermittlung", die den Fortschritt des `MediaDurationProbeWorker` aus den
`MediaItem`-Probespalten aggregiert anzeigt: Zähler (gesamt/ermittelt/ausstehend/fehlgeschlagen),
letzter Aktivitätszeitpunkt, Rate der letzten Stunde, Statushinweis „läuft vs. hängt" und eine
Rolling-Liste der zuletzt geprüften Dateien. Datenquelle ist eine neue, admin-gegate
Abfragemethode `MediaDurationProbeService.GetProgressAsync` — keine neue Persistenz, keine
Migration.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ort der Abfrage | `MediaDurationProbeService.GetProgressAsync(userId, recentCount, ct)` | Der Service besitzt bereits `ApplicationDbContext`, `MediaProbeOptions` (Erschöpfungsschwelle), `TimeProvider` (testbares Zeitfenster) und das `VideoExtensionPredicate()` für exakt dieselbe Video-Teilmenge, die der Worker bearbeitet. Scoped registriert und ohne Interface direkt injizierbar (Muster wie `ProgramSettingsService`). |
| Ergebnistypen | `sealed record MediaDurationProbeProgress` + `sealed record MediaDurationProbeRecentItem` in eigener Datei `Services/MediaDurationProbeProgress.cs` | Enthält eine `IReadOnlyList` — als Klasse null-sicherer als `record struct` (`ProgramRuleDiagnosis` trägt nur Wertefelder). Analoger Ablageort wie `MediaDurationProbeResult.cs`. |
| Zähler-Aggregation | Drei `CountAsync`-Queries + `MaxAsync(DurationProbedAt)` + Top-N-Projektion | Gezählte/indizierte Spalten, keine Materialisierung; kein `GroupBy` mit bedingtem Schlüssel (SQLite-Übersetzungsrisiko). `Pending = Total − Probed − Failed`. |
| Aktualisierung der Anzeige | `PeriodicTimer` (15 s) mit `CancellationTokenSource`, in `Dispose()` abgebrochen | Bewährtes Muster aus `LiveTv.razor`; erst so ist „arbeitet vs. hängt" ohne manuelles Neuladen beobachtbar. Einzelfehler beenden die Schleife nicht. |
| Platzierung | Neue `admin-card admin-card-wide` am Ende der Seite (nach der Sendeplan-Karte) | Diagnose-/Statusinformation, stört keine bestehenden Abläufe und greift kein existierendes UI an. |

## Programmabläufe

### Fortschritt abrufen (serverseitig, admin-gegate)

1. `TVSenderAdmin.razor` ruft beim Laden und alle 15 s
   `MediaDurationProbeService.GetProgressAsync(userId, RecentProbeCount, ct)` auf.
2. `GetProgressAsync` ruft `RequireAdminAsync(userId)` (Muster aus `ProgramRuleService`:
   unbekannter Benutzer → `UnauthorizedAccessException`, Nicht-Admin →
   `ForbiddenAccessException`).
3. Basismenge: `_db.MediaItems.AsNoTracking().Where(VideoExtensionPredicate())` — identisch mit
   der Worker-Selektion.
4. Zähler: `TotalCount` = Count; `ProbedCount` = `Duration != null`; `FailedCount` =
   `Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts`; `PendingCount` = Rest.
5. `LastActivityAt` = `MAX(DurationProbedAt)` (über den Index); `ProbedLastHour` =
   `DurationProbedAt >= now - 1 h` (Zeit via `_timeProvider`).
6. `RecentItems` = Items mit `DurationProbedAt != null`, `OrderByDescending(DurationProbedAt)`,
   `Take(recentCount)`, projiziert auf `Name`, `MediaCollection.MediaSource.Name`, `ProbedAt`,
   `Duration`, `Attempts`.

Beteiligte Klassen: `MediaDurationProbeService`, `ApplicationDbContext`, `MediaItem`,
`MediaProbeOptions`, `ForbiddenAccessException`, `VideoFileExtensions`.

### Anzeige auf der Admin-Seite

1. `OnInitializedAsync` (nur `isAdmin`-Zweig) lädt `probeProgress` und startet die
   Refresh-Schleife.
2. Die Karte zeigt: Statushinweis (`ProbeStatusLabel()`), Zählerzeile, Fortschrittsbalken
   (`ProbedCount/TotalCount`), „Letzte Aktivität" (lokale Zeit), „Letzte Stunde: n" und die
   Rolling-Tabelle `Zeitpunkt | Datei | Quelle | Ergebnis` (`Dauer` oder „keine Laufzeit —
   Versuch n").
3. `Dispose()` (bestehend, `@implements IDisposable`) bricht den Timer ab.

Beteiligte Komponenten: `TVSenderAdmin.razor`, `MediaDurationProbeService`,
`MediaDurationProbeProgress`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MediaDurationProbeProgress` | `sealed record` | Aggregat-Ergebnis: `TotalCount`, `ProbedCount`, `PendingCount`, `FailedCount`, `LastActivityAt` (`DateTime?`), `ProbedLastHour` (`int`), `RecentItems` (`IReadOnlyList<MediaDurationProbeRecentItem>`) |
| `MediaDurationProbeRecentItem` | `sealed record` | Ein Rolling-Listen-Eintrag: `Name`, `SourceName`, `ProbedAt` (`DateTime`), `Duration` (`TimeSpan?`), `Attempts` (`int`) |

## Änderungen an bestehenden Klassen

### `MediaDurationProbeService` (`VideoWebPlayer/Services/MediaDurationProbeService.cs`)

- **Neue Methoden:**
  - `GetProgressAsync(string userId, int recentCount = 10, CancellationToken cancellationToken = default)` → `Task<MediaDurationProbeProgress>` — siehe Programmablauf; `recentCount` wird auf ≥ 0 begrenzt.
  - `RequireAdminAsync(string userId, CancellationToken)` — `private`, Muster aus `ProgramRuleService`/`ProgramScheduleService` (Fehlertext fachlich: „…die Laufzeitermittlung einsehen").
- **Keine geänderten Methoden.** `VideoExtensionPredicate()` wird zusätzlich von
  `GetProgressAsync` genutzt (bleibt `private static`).

### `TVSenderAdmin.razor` (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`)

- **Neue Injektion:** `MediaDurationProbeService ProbeService`.
- **Neue Felder:** `MediaDurationProbeProgress? probeProgress`, `CancellationTokenSource? refreshCts`, Konstante `RecentProbeCount = 10`, Refresh-Intervall 15 s.
- **Neue Methoden:**
  - `LoadProbeProgressAsync()` — einmalige Abfrage (im `isAdmin`-Zweig von `OnInitializedAsync`).
  - `ProbeProgressLoopAsync()` — `PeriodicTimer`-Schleife: Abfrage + `InvokeAsync(StateHasChanged)`, `OperationCanceledException` → Ende, sonstige Fehler → weiter (Schleife stirbt nicht).
  - `ProbeStatusLabel()` — Statushinweis (siehe Texte unten).
  - `RecentResultLabel(MediaDurationProbeRecentItem)` — „mm:ss" bzw. „h:mm:ss" bei Erfolg, sonst „keine Laufzeit — Versuch n".
  - `FormatDuration(TimeSpan)` — kompakte Daueranzeige.
- **Geänderte Methoden:** `OnInitializedAsync` (+ Laden/Timer-Start), `Dispose` (+ `refreshCts.Cancel()`/Dispose).
- **Neues Markup:** `admin-card` „Laufzeitermittlung" mit Statuszeile
  (`#tvsender-probe-status`), Zählern (`#tvsender-probe-counts`), Fortschrittsbalken,
  „letzte Aktivität" (`#tvsender-probe-last-activity`), Rate (`#tvsender-probe-rate`) und
  Tabelle `#tvsender-probe-recent` (Zeilen `.tvsender-probe-row`). Leerer Zustand über
  `admin-empty-state`, wenn `TotalCount == 0`.

**Statushinweis-Logik (`ProbeStatusLabel`):**
- `TotalCount == 0` → Karte zeigt Empty-State (kein Status-Label).
- `PendingCount == 0 && FailedCount == 0` → „Alle Videodateien sind ermittelt."
- `PendingCount == 0 && FailedCount > 0` → „Ermittlung abgeschlossen — {n} Datei(en) dauerhaft nicht prüfbar."
- `PendingCount > 0 && ProbedLastHour > 0` → „Ermittlung läuft — {n} Datei(en) in der letzten Stunde."
- `PendingCount > 0 && ProbedLastHour == 0` → „{n} Datei(en) ausstehend, aber keine Aktivität in der letzten Stunde — die Ermittlung hängt möglicherweise (ffprobe verfügbar?)."

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `userId` (Parameter `GetProgressAsync`) | Benutzer muss existieren und `IsAdmin` haben | `UnauthorizedAccessException` bzw. `ForbiddenAccessException` (serverseitig erzwungen) |
| `recentCount` (Parameter) | ≤ 0 → auf 0 begrenzt (leere Liste) | Keine Exception nötig; defensiv geclamped |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Worker-Parallelität:** Die Anzeige ist eine reine Leseabfrage (`AsNoTracking`) auf
  indizierten Spalten — beeinflusst `ProbePendingAsync` nicht.
- **Timer-Lebensdauer:** Die Refresh-Schleife muss sauber abbrechen (`Dispose`), sonst läuft
  ein DbContext-Scope-Zugriff nach Component-Dispose weiter — daher Abbruch vor Ende von
  `Dispose` und `disposed`-Flag beachten.
- **Bestehende Tests:** `TVSenderAdminE2ETests` wird erweitert (konsolidiert, keine neue
  Testmethode); der bestehende Flake `Admin_CreateEditDeactivateDeleteRule` bleibt bekannt.

## Umsetzungsreihenfolge

1. **`MediaDurationProbeProgress.cs` anlegen** (Records `MediaDurationProbeProgress`,
   `MediaDurationProbeRecentItem`)
   - Voraussetzungen: Keine.

2. **`MediaDurationProbeService.GetProgressAsync` + `RequireAdminAsync` implementieren**
   - Voraussetzungen: Schritt 1.

3. **Tests `MediaDurationProbeServiceTests_Progress` (neue Klasse)**
   - Voraussetzungen: Schritt 2; Testinfrastruktur `ProgramScheduleTestDb` (AddUserAsync,
     CreateMediaItemAsync) vorhanden; minimale private Fakes für `IMediaSourceReader`,
     `IMediaDurationProbe`, `IFfprobeLocator` in der Testklasse (werden von der neuen Methode
     nicht aufgerufen).

4. **`TVSenderAdmin.razor`: Fortschritts-Karte + Refresh-Timer**
   - Voraussetzungen: Schritt 2; bestehende UI-Muster (`admin-card`, `admin-chip`,
     `admin-empty-state`, `PeriodicTimer` aus `LiveTv.razor`).

5. **E2E-Erweiterung `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`**
   - Voraussetzungen: Schritt 4. Zwei `MediaItem`s direkt in die Test-DB seeden (1 × ermittelt
     mit `Duration`, 1 × `Attempts >= MaxAttempts` gescheitert — beide **nicht** ausstehend,
     damit der echte Hintergrund-Worker den Zustand nicht verändert), dann im selben
     Testdurchlauf die Karten-Anzeige prüfen.

6. **Dokumentation** (`docs/help/tv-sender.md`, `docs/help/videolaufzeit.md`,
   `docs/RELEASE_NOTES.md`)
   - Voraussetzungen: Schritte 1–5.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetProgressAsync_CountsStatesSeparately` | `MediaDurationProbeServiceTests_Progress` | Total/Probed/Pending/Failed über die vier Zustandsklassen (ermittelt, nie geprüft, in Wiederholung, erschöpft); Nicht-Video-Dateien zählen nicht |
| `GetProgressAsync_RecentItems_OrderedNewestFirstAndLimited` | dito | Rolling-Liste: neueste zuerst, `Take(recentCount)`, Feldinhalte (Name, Quelle, Dauer, Versuche) |
| `GetProgressAsync_ReportsLastActivityAndHourlyRate` | dito | `LastActivityAt` = MAX(`DurationProbedAt`); `ProbedLastHour` zählt nur das Zeitfenster (via injiziertem `TimeProvider` bzw. realen Zeitstempeln) |
| `GetProgressAsync_EmptyLibrary_ReturnsZeros` | dito | Keine Items → alle Zähler 0, `LastActivityAt == null`, leere Liste |
| `GetProgressAsync_NonAdmin_Throws` / `_UnknownUser_Throws` | dito | `ForbiddenAccessException` / `UnauthorizedAccessException` (Admin-Erzwingung) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Erweiterung um den Sichtbarkeits-/Inhaltsnachweis der neuen Karte (konsolidiert, gleicher App-Start) |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin öffnet `/admin/tv-sender` und sieht die Karte „Laufzeitermittlung" mit Zählern und den zuletzt ermittelten Dateien | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (erweitert) | Fortschrittsanzeige sichtbar, korrekte Zähler und Rolling-Liste | Benutzerfluss „Status ohne Log erkennen" ist nur UI-seitig nachweisbar |
| Bestehend | Nicht-Admin sieht „Nicht autorisiert" (Karte nicht erreichbar) | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` (unverändert) | Admin-only | Bereits abgedeckt; Service-Gate zusätzlich per Unit-Test |

Keine weiteren bestehenden E2E-Tests betroffen.

## Offene Punkte

Keine.
