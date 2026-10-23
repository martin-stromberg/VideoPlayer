# Übersetzte Anforderung — TV-Sender: Fehlgeschlagene Laufzeit-Probes zurücksetzen (Issue #258)

Quelle: `issue.md` (GitHub Issue #258, Nachbesserung aus Issue #111 / PR #257).

## Fachliche Zusammenfassung

Der Administrator sieht auf der TV-Sender-Adminseite (`/admin/tv-sender`,
`TVSenderAdmin.razor`, Tab „Laufzeitermittlung") in der Karte `tvsender-probe-card` unter
„▾ Details" die Liste dauerhaft fehlgeschlagener Videodateien
(`tvsender-probe-failed-detail`, „Fehlgeschlagene Dateien — dauerhaft nicht prüfbar").
Bisher ist die Liste rein informativ: Es gibt keinen Weg, ein fehlgeschlagenes Item wieder
in die Laufzeitermittlung zurückzuholen — etwa nachdem die Datei ersetzt oder repariert
wurde, ohne dass der Scan eine Änderung erkannt hat.

Gefordert ist eine Aktion „Probe zurücksetzen" pro Eintrag sowie nach Möglichkeit ein
Massen-Reset für alle fehlgeschlagenen Items. Zurücksetzen heißt: `MediaItem.DurationProbedAt`
und `MediaItem.DurationProbeAttempts` löschen (`DurationProbedAt = null`,
`DurationProbeAttempts = 0`; `Duration` ist bei fehlgeschlagenen Items per Definition
bereits `null`), sodass der `MediaDurationProbeWorker` das Item beim nächsten Lauf wieder
als ausstehend auswählt. Das ist derselbe Reset, den `MediaSourceScanner` bereits
automatisch anwendet, wenn er eine geänderte Datei erkennt
(`MediaSourceScanner.cs`: `Duration = null; DurationProbedAt = null; DurationProbeAttempts = 0`).

Zusätzlicher Fachpunkt (Auswahl/Identifikation): Der Administrator wählt gezielt einen
Eintrag aus der Fehlerliste aus. Dafür muss der Listeneintrag das `MediaItem` eindeutig
identifizieren — `MediaDurationProbeDetailItem` trägt heute nur `Name`, `SourceName`,
`LastAttemptAt`, `Attempts`, also **keine** Item-ID (`Name` ist nicht eindeutig). Der Record
muss um die `MediaItem.Id` erweitert werden.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Data/MediaItem.cs`
  - `Duration` (`TimeSpan?`), `DurationProbedAt` (`DateTime?`), `DurationProbeAttempts`
    (`int`) — die zurückzusetzenden Felder. „Dauerhaft fehlgeschlagen" heißt:
    `Duration == null && DurationProbeAttempts >= MediaProbeOptions.EffectiveMaxAttempts`.
- `VideoWebPlayer/Services/MediaDurationProbeProgress.cs`
  - Record `MediaDurationProbeDetailItem` um eine Identität des Items erweitern
    (z. B. `MediaItemId`), damit pro Eintrag gezielt zurückgesetzt werden kann;
    `MediaDurationProbeProgress.FailedItems` bleibt die Datenquelle der Fehlerliste.
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
  - Neue Admin-only-Methode(n) analog dem vorhandenen `RequireAdminAsync`-Muster
    (`MediaDurationProbeService.cs:225-232`; Vorbilder `ResetScheduleAsync` /
    `DeleteDisplacedSlotsAsync` in `ProgramScheduleService`):
    - Einzel-Reset pro `MediaItem.Id` (Rückgabe: Erfolg/Fundstatus).
    - Massen-Reset über die gesamte Fehlermenge (Rückgabe: Anzahl zurückgesetzter
      Items) — wichtig, weil `FailedItems` auf `detailItemCount` (Default 25) gekappt
      ist, `FailedCount` aber größer sein kann; ein clientseitiges Iterieren über die
      angezeigten Zeilen würde den Rest nicht erfassen.
  - `GetProgressAsync` / `DetailItemsAsync` (`MediaDurationProbeService.cs:213-223`)
    entsprechend um die ID-Projektion erweitern.
  - `ProbePendingAsync` (`MediaDurationProbeService.cs:99-108`) nimmt zurückgesetzte
    Items automatisch wieder auf (`DurationProbedAt == null` ist immer auswählbar) —
    keine Änderung an der Selektion nötig.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
  - Fehler-Tabelle (`tvsender-probe-failed-detail`, Zeilen
    `tvsender-probe-detail-row`): zusätzliche Aktions-Spalte mit Schaltfläche
    „Probe zurücksetzen" je Zeile (Muster: `btn btn-sm`, `actions-cell` der
    Regel-Tabelle).
  - Massen-Reset-Schaltfläche im Bereich der Fehlerliste bzw. der Karte; für die
    bulk-artige Aktion das vorhandene Bestätigungsdialog-Muster
    (`admin-dialog-overlay`/`admin-dialog`, siehe `confirmReset`/`confirmCleanup`)
    wiederverwenden.
  - Nach dem Zurücksetzen `probeProgress` neu laden (`LoadProbeProgressAsync`) und eine
    Statusmeldung ausgeben (Muster `scheduleStatus`/`rulesStatus`); die bestehende
    15-s-Auto-Aktualisierung (`ProbeProgressLoopAsync`) zieht den Fortschritt ohnehin nach.
- `VideoWebPlayer/Services/MediaDurationProbeWorker.cs`
  - Kein Eingriff nötig: der Worker läuft im `MediaProbe:IntervalSeconds`-Takt (Default
    60 s) und greift zurückgesetzte Items beim nächsten Tick auf. (Annahme: dieses
    Zeitverhalten reicht; siehe Offene Fragen.)
- Tests (`VideoWebPlayer.Tests`)
  - `Services/MediaDurationProbeServiceTests_Progress.cs` bzw. neue Testdatei: Reset
    eines einzelnen Items (Zähler/Zeitstempel geleert, Item wandert von
    `FailedItems`/`FailedCount` in die Pending-Menge, taucht danach in der
    `ProbePendingAsync`-Selektion auf); Massen-Reset über die volle Fehlermenge jenseits
    der `detailItemCount`-Kappung; Admin-Erzwingung (`ForbiddenAccessException` /
    `UnauthorizedAccessException` für Nicht-Admins bzw. unbekannte Benutzer);
    erfolgreich geprobte Items bleiben unberührt; Nicht-Fehlgeschlagene werden beim
    Einzel-Reset nicht fälschlich zurückgesetzt. Gegen echtes SQLite
    (`ProgramScheduleTestDb`-Fixture, wie die bestehenden Progress-Tests).
  - `Components/TVSenderAdminTests.cs` (bUnit): Schaltfläche je Fehlerzeile sichtbar
    und wirkt; Massen-Reset inkl. Bestätigungsdialog; Liste/Zähler aktualisieren sich.
  - `TVSenderAdminE2ETests.cs`: Erweiterung des konsolidierten Admin-E2E-Tests um den
    Reset-Nachweis (kein eigener neuer Testlauf pro Aspekt — Konvention aus Schritt 11).
- Dokumentation
  - `docs/help/videolaufzeit.md` (Abschnitt „Fortschritt beobachten": Zurücksetzen
    fehlgeschlagener Ermittlungen beschreiben), `docs/RELEASE_NOTES.md`.

## Implementierungsansatz

- Erweiterung bestehender Strukturen statt neuer Bausteine: neuer Service-Methodensatz
  auf `MediaDurationProbeService` (bereits scoped registriert und in `TVSenderAdmin`
  injiziert), neue Spalte/Schaltfläche in der vorhandenen `probe-detail`-Tabelle, kein
  neuer Worker, keine neue Seite.
- Serverseitige Berechtigung: `RequireAdminAsync` in den Reset-Methoden — eine
  ausgeblendete Schaltfläche allein gilt im Projekt nicht als Absicherung.
- Reset-Semantik: `DurationProbedAt = null`, `DurationProbeAttempts = 0` (bei
  fehlgeschlagenen Items ist `Duration` bereits `null`; beim Einzel-Reset nur auf Items
  anwenden, die tatsächlich zur Failed-Menge gehören, damit kein erfolgreich ermitteltes
  `Duration` verloren geht — Annahme, zur Klärung siehe Offene Fragen).
- Nach dem Schreiben der `Duration` feuert `ProbePendingAsync` bereits
  `IProgramScheduleSignal.NotifyScheduleChanged()` — der Reset selbst muss nichts
  signalisieren, da ein Zurücksetzen allein noch nichts planbar macht.
- Keine EF-Migration und keine Backup-Erweiterung nötig: Es werden nur Werte bestehender
  Spalten verändert (`DurationProbeAttempts` steht bereits in `OptionalRestoreColumns`).

## Konfiguration

Keine neue Konfiguration. Die „fehlgeschlagen"-Schwelle bleibt
`MediaProbeOptions.EffectiveMaxAttempts` (`MediaProbe:MaxAttempts`, Default 10); die
Wiederaufnahme erfolgt über den vorhandenen `MediaProbe:IntervalSeconds`-Takt.

## Offene Fragen

1. Massen-Reset — verpflichtend oder optional? Die Anforderung sagt „ggf. Massen-Reset".
   Annahme: wird mit umgesetzt (geringer Mehraufwand, sinnvoll bei `FailedCount` >
   Listenkappung); Umfang = alle dauerhaft fehlgeschlagenen Video-Items.
2. Soll die Zurücksetzen-Aktion auch in der Liste der ausstehenden Items
   (`tvsender-probe-pending-detail`) angeboten werden, um Items im Retry-Wartezyklus
   (`RetryDelaySeconds`) sofort wieder freizugeben? Annahme: nein — die Anforderung
   nennt nur fehlgeschlagene Items; kann als Erweiterung in der Planung bewertet werden.
3. Soll nach dem Reset sofort geprobt werden (z. B. Worker anstoßen) statt erst beim
   nächsten Intervall (Default 60 s)? Annahme: nächster Tick reicht; ein eigenes
   Signal existiert für den Probe-Worker derzeit nicht.
4. Einzel-Reset ohne Bestätigungsdialog direkt ausführen (Aktion ist gefahrlos und
   reversibel) und den Dialog nur beim Massen-Reset zeigen? Annahme: ja, entspricht dem
   vorhandenen UX-Muster (destruktive Sammelaktionen haben Dialoge).
