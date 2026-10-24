# Bestandsaufnahme: Fortschrittsanzeige Laufzeitermittlung (N6)

Analyse des Laufzeitermittlungs-Bereichs (`MediaDurationProbeWorker`/`MediaDurationProbeService`,
`MediaItem`-Probespalten) und der TV-Sender-Adminseite (`TVSenderAdmin.razor`) bezogen auf die
Anforderung, dem Administrator ohne Log-/DB-Zugriff sichtbar zu machen, ob die
Laufzeitermittlung voranschreitet oder hängt.

## Zusammenfassung

- `MediaItem` trägt bereits den kompletten Probezustand: `Duration` (ermittelte Laufzeit oder
  `null`), `DurationProbedAt` (Zeitpunkt des letzten Versuchs, `null` = nie geprüft) und
  `DurationProbeAttempts` (Fehlerzähler; `>= MediaProbeOptions.EffectiveMaxAttempts` = dauerhaft
  gescheitert). Auf `DurationProbedAt` liegt ein Index (`MediaItemConfiguration`), eine
  Rolling-Liste „neueste zuerst" und `MAX(DurationProbedAt)` sind damit günstig.
- `MediaDurationProbeService` (scoped, injizierbar) selektiert über die private
  `VideoExtensionPredicate()`-Methode exakt die Videodateien (`VideoFileExtensions.All` =
  `.mp4`/`.avi`/`.mkv`/`.mpeg`), die der Worker bearbeitet — dieselbe Teilmenge muss die
  Fortschrittsanzeige zählen.
- Es existiert **keine** Schnittstelle, die den aggregierten Probezustand nach außen gibt;
  der Zustand ist nur über Log-Dateien oder direkte DB-Abfrage sichtbar (genau die Lücke aus N6).
- `TVSenderAdmin.razor` ist Admin-only (Claim `IsAdmin` = `True`, sonst „Nicht autorisiert"),
  injiziert bereits `ApplicationDbContext`, `IProgramRuleService`, `IProgramScheduleService`,
  `IProgramScheduleSignal`, `ProgramSettingsService` und besteht aus `admin-card`-Abschnitten
  (Sender, Programmregeln mit Diagnose-Spalte „Planbar", Sendeplan mit „Jetzt generieren").
- Serverseitige Admin-Absicherung existiert als `RequireAdminAsync`-Muster in
  `ProgramRuleService` (Zeile 219) und `ProgramScheduleService` (Zeile 369): unbekannter Benutzer
  → `UnauthorizedAccessException`, Nicht-Admin → `ForbiddenAccessException`
  (`VideoWebPlayer/Controllers/Exceptions/ForbiddenAccessException.cs`, `internal` — die Tests
  sehen sie über `InternalsVisibleTo` bzw. das Testprojekt verwendet sie bereits).
- `MediaDurationProbeService` hat bislang keinen Benutzerbezug — für die Fortschrittsabfrage
  ist das `RequireAdminAsync`-Muster dort zu wiederholen (Konsistenz mit den beiden anderen
  Services; ein gemeinsamer Helper existiert nicht).

Test-Ausgangszustand: gefilterter Lauf der betroffenen Suiten (MediaDurationProbe*,
ProgramSchedule*, ProgramRule*, ProgramSettings*, TVSenderAdmin*) — **131 erfolgreich,
0 fehlgeschlagen, 0 übersprungen** (Release-Build, 0 Fehler; der Debug-Build ist aktuell durch
eine laufende `VideoWebPlayer.exe`-Instanz aus `bin\Debug` gesperrt — siehe
[Tests](inventory/tests.md)). Der volle Suite-Lauf steht für den Abschluss an.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
