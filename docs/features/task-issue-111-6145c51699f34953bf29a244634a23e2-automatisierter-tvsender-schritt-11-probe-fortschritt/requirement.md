# Übersetzte Anforderung — Schritt 11: Fortschrittsanzeige Laufzeitermittlung (N6)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N6.

## Fachliche Zusammenfassung

Der Administrator soll auf der TV-Sender-Adminseite (`/admin/tv-sender`,
`TVSenderAdmin.razor`, Admin-only) ohne Log- oder DB-Zugriff erkennen können, ob die
Hintergrund-Laufzeitermittlung (`MediaDurationProbeWorker`/`MediaDurationProbeService`)
voranschreitet oder hängt. Dafür wird eine Fortschritts-/Statusanzeige ergänzt, die den
Probezustand der `MediaItem`-Videodateien aggregiert: Gesamtzahl geprüfter Videos,
ausstehende und dauerhaft fehlgeschlagene Ermittlungen, letzter Aktivitätszeitpunkt sowie
eine Rolling-Liste der zuletzt bearbeiteten Dateien (welche Items gerade erfasst wurden,
mit Ergebnis). Optional ergänzend: eine einfache Rate (Ermittlungen der letzten Stunde),
damit „arbeitet" vs. „hängt" direkt ablesbar ist.

Es wird kein neuer persistenter Zustand angelegt — alle Angaben sind aus
`MediaItem.Duration`, `MediaItem.DurationProbedAt` und `MediaItem.DurationProbeAttempts`
(Versuchszähler; `>= MediaProbeOptions.EffectiveMaxAttempts` = dauerhaft gescheitert)
aggregierbar. Keine Migration erforderlich.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
  - Neue Methode `GetProgressAsync(string userId, int recentCount, CancellationToken)` —
    serverseitige Admin-Prüfung analog `RequireAdminAsync` (Muster aus
    `ProgramRuleService`/`ProgramScheduleService`), Aggregation über `MediaItems`
    gefiltert auf die Video-Dateiendungen (`VideoFileExtensions.All`, gleiche
    Teilmenge wie `ProbePendingAsync`).
  - Neuer Record-Typ für das Ergebnis (Zähler + letzter Aktivitätszeitpunkt +
    Rolling-Liste der jüngsten Probe-Ergebnisse), Ablage analog
    `ProgramRuleDiagnosis` als öffentlicher Record im `VideoWebPlayer.Services`-Namespace
    (z. B. eigene Datei oder bei `MediaDurationProbeResult.cs`).
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
  - Neue Status-Karte bzw. neuer Abschnitt (z. B. „Laufzeitermittlung") im
    `admin-card`-Design der Seite: Zählerzeile (geprüft/ausstehend/fehlgeschlagen/
    gesamt), letzter Aktivitätszeitpunkt, ggf. Rate, und eine kleine Tabelle der
    zuletzt ermittelten Laufzeiten (Dateiname, Ergebnis/Dauer, Zeitpunkt).
  - Befüllung beim Seitenladen (`OnInitializedAsync`) und nach relevanten
    Aktualisierungen; Injektion von `MediaDurationProbeService` (bereits scoped
    registriert).
- Tests
  - Neue Testklasse `MediaDurationProbeServiceTests_Progress.cs` (oder Erweiterung der
    bestehenden Probe-Tests): Zähler korrekt über die Zustandsklassen hinweg
    (ungeprüft, erfolgreich, in Wiederholung, dauerhaft gescheitert), Rolling-Liste
    neueste zuerst und auf `recentCount` begrenzt, nur Video-Endungen gezählt,
    Admin-Erzwingung (`ForbiddenAccessException`/`UnauthorizedAccessException`).
  - Bestehender konsolidierter E2E-Test `TVSenderAdminE2ETests` wird um den
    Sichtbarkeitsnachweis der Fortschrittsanzeige erweitert (kein neuer Testlauf pro
    Aspekt).
- Dokumentation
  - `docs/help/tv-sender.md` (neuer Abschnitt zur Statusanzeige) bzw.
    `docs/help/videolaufzeit.md` (Verweis auf die Anzeige), `docs/RELEASE_NOTES.md`.

## Implementierungsansatz

- Die Aggregatabfrage läuft auf `ApplicationDbContext.MediaItems` mit demselben
  `VideoExtensionPredicate()` wie `ProbePendingAsync` (dorthin verlagert bzw. gemeinsam
  genutzt), damit Zähler und Worker dieselbe Menge betrachten.
- Zähler per `GroupBy`/Einzähl-Queries gegen SQLite — günstig (gezählte Spalten), keine
  Materialisierung aller Items. Rolling-Liste: `DurationProbedAt != null`, absteigend
  sortiert, `Take(recentCount)`, Projektion auf Name/Pfad, `Duration`,
  `DurationProbedAt`, `DurationProbeAttempts`.
- „Letzter Aktivitätszeitpunkt" = `MAX(DurationProbedAt)`. Rate = Anzahl Items mit
  `DurationProbedAt` innerhalb der letzten Stunde (über `TimeProvider` des Services,
  testbar).
- Admin-Gate: seitenbasiert wie bisher (`isAdmin`-Claim, Seite rendert „Nicht
  autorisiert"), service-seitig zusätzlich `RequireAdminAsync`-Muster
  (serverseitige Absicherung gemäß Projektregeln).

## Konfiguration

Keine neue Konfiguration. Die Erschöpfungsschwelle nutzt
`MediaProbeOptions.EffectiveMaxAttempts`, die Rate-Fenster/Listenlänge sind
Methodenparameter bzw. Konstanten.

## Offene Fragen

Keine — Anforderung und Auftragsbeschreibung legen Ort (TV-Sender-Adminseite),
Inhalte (Zähler, Aktivitätszeitpunkt, Rolling-Liste, optional Rate) und Datenquelle
(bestehende `MediaItem`-Spalten) eindeutig fest.
