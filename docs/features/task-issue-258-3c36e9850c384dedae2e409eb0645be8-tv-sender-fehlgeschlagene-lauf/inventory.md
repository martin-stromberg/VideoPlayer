# Bestandsaufnahme — TV-Sender: Fehlgeschlagene Laufzeit-Probes zurücksetzen (Issue #258)

Inventar zum Ist-Stand vor der Umsetzung. Anforderung:
[`requirement.md`](requirement.md) (Fehlgeschlagene Items im Tab „Laufzeitermittlung" der
Seite `/admin/tv-sender` sollen per Einzel- und Massen-Aktion zurücksetzbar sein:
`DurationProbedAt = null`, `DurationProbeAttempts = 0`).

## Zusammenfassung

- **Datenmodell vollständig vorhanden:** `MediaItem` besitzt bereits `Duration`
  (`TimeSpan?`), `DurationProbedAt` (`DateTime?`, indiziert) und `DurationProbeAttempts`
  (`int`); die Failed-Schwelle `DurationProbeAttempts >= MediaProbeOptions.EffectiveMaxAttempts`
  ist im Code an zwei Stellen umgesetzt (`GetProgressAsync`-Zähler/-Detailfilter,
  `ProgramScheduleService`-Diagnose Z. 1565). Keine neue Spalte/Migration nötig;
  `DurationProbedAt`/`DurationProbeAttempts` stehen bereits in
  `VideoWebPlayerBackupData.OptionalRestoreColumns` (Z. 84-85).
- **Reset-Semantik existiert als Vorbild:** `MediaSourceScanner` setzt bei geänderter
  Datei bereits `Duration = null; DurationProbedAt = null; DurationProbeAttempts = 0`
  (`MediaSourceScanner.cs:274-276`).
- **Admin-Muster existiert:** `MediaDurationProbeService.RequireAdminAsync` (privat,
  Z. 225-232) mit `UnauthorizedAccessException`/`ForbiddenAccessException`; Vorbilder
  für Admin-Aktionen mit Mengenrückgabe: `ProgramScheduleService.DeleteDisplacedSlotsAsync`
  (`int`) und `ResetScheduleAsync` (`ProgramScheduleResetResult`), beide über
  `ExecuteDeleteAsync` auf Tabellen-Ebene.
- **Lücke 1 — Identifikation:** `MediaDurationProbeDetailItem` (Name, SourceName,
  LastAttemptAt, Attempts) trägt **keine `MediaItem.Id`**; die Projektion liegt in der
  privaten `DetailItemsAsync` (`MediaDurationProbeService.cs:213-223`). `FailedItems` ist
  auf `detailItemCount` (Default 25) gekappt, `FailedCount` zählt die volle Menge.
- **Lücke 2 — Service:** Es gibt keine Reset-Methode; `ProbePendingAsync` nimmt
  zurückgesetzte Items (`DurationProbedAt == null`) aber bereits automatisch wieder auf
  (Z. 103), und der `MediaDurationProbeWorker` greift sie im `EffectiveIntervalSeconds`-Takt
  (Default 60 s) auf — kein eigenes Aufweck-Signal vorhanden.
- **Lücke 3 — UI:** `tvsender-probe-failed-detail` und `tvsender-probe-pending-detail`
  haben nur 4 Spalten (Name | Quelle | Versuche | Letzter Versuch), keine Aktionszelle;
  kein Reset-Button, kein Reset-Bestätigungsdialog, kein eigenes `probeStatus`-Feld.
  Vorhandene Muster: `actions-cell` + `btn btn-sm` (Regel-Tabelle), Dialoge
  `admin-dialog-overlay`/`admin-dialog` (`confirmCleanup`/`confirmReset`),
  `LoadProbeProgressAsync` + 15-s-`ProbeProgressLoopAsync` zum Nachladen.
- **Tests:** Volle Suite im Ausgangslauf **grün** (2210 erfolgreich, 0 fehlgeschlagen,
  1 übersprungen). Die übersprungene Testmethode ist ein dokumentierter, bekannter
  Produktfehler außerhalb dieses Bereichs. Vorhandene Test-Infrastruktur für den Bereich:
  `ProgramScheduleTestDb`-Fixture, bUnit-Kontext in `TVSenderAdminTests.CreateContextAsync`,
  konsolidierter Playwright-Test `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`.
  Keine bestehenden Tests für einen Reset (Funktion existiert nicht).

## Detaildokumente

| Datei | Inhalt |
|---|---|
| [`inventory/models.md`](inventory/models.md) | `MediaItem`/`MediaEntry`-Eigenschaften, EF-Konfiguration, Backup-Tabellen, `MediaProbeOptions` |
| [`inventory/logic.md`](inventory/logic.md) | `MediaDurationProbeService`, `MediaDurationProbeWorker`, `MediaSourceScanner`, `ProgramScheduleService`-Vorbild, `TVSenderAdmin.razor`-Struktur, CSS |
| [`inventory/enums-interfaces.md`](inventory/enums-interfaces.md) | Records/DTOs, Enums, Interfaces, Exceptions, `VideoFileExtensions` |
| [`inventory/tests.md`](inventory/tests.md) | Test-Ausgangszustand (Zeitpunkt, Commit, SDK), Lauftabelle, übersprungene Tests, Testlücken, Testklassen-Übersicht |

## Testergebnis (Kurzform)

`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` @ `7fdb3a1` —
Exit 0: **2210 erfolgreich / 0 fehlgeschlagen / 1 übersprungen** (2211 gesamt, 4 m 36 s).
Nachweis: `inventory/test-results/dotnet-test-run1-console.txt`.
