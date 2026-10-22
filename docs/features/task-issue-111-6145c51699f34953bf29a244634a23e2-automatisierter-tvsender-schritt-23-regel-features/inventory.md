# Bestandsaufnahme: Schritt 23 — Regel-Features N19–N21

Bestandsaufnahme der Sendeplan-Erzeugung (`ProgramScheduleService`), der Regelverwaltung
(`ProgramRuleService`, `TVSenderAdmin`) und der Cursor-Persistenz bezogen auf die Anforderungen
N19 (Serien-Interleaving), N20 (Werktag/Wochenende) und N21 (Regel-Editor als Overlay).

## Zusammenfassung

- **N19:** Genre-Material expandiert aktuell in EINE flache `SchedulableItem`-Liste
  (Filme zuerst, dann Episoden sortiert nach Serienname/Staffel/Nummer). `RuleState` führt
  genau einen Ring (`Items`) und genau einen Cursor (`NextIndex` ↔ `ProgramRuleCursors`-Zeile,
  Unique-Index auf `ProgramRuleId`). Ergebnis: Stripping — eine Serie füllt das Fenster bis
  ihr Ring weitergedreht ist. Es fehlt: Strang-Aufteilung je Serie, Round-Robin-Auswahl,
  Cursor-Persistenz je Strang.
- **N20:** `ProgramRuleRecurrence` hat `Daily/Weekly/Once/Holiday` (0–3). `BuildOccurrences`
  wertet sie in einem `switch` aus; `ProgramRuleService.ValidateRuleInputAsync` verlangt
  `DayOfWeek` nur bei `Weekly`, `SingleDate` nur bei `Once`; der Editor bietet die vier
  Optionen hartcodiert an; `RecurrenceLabel` bildet die Anzeige. Es fehlen `Weekday`/`Weekend`
  an allen vier Stellen.
- **N21:** Der Regel-Editor (`#rule-editor`, `.rule-editor-card`) ist eine normale
  `admin-card` im Regeln-Tab — Tests (`TVSenderAdminTests.RuleEditor_RendersAsCard_InsideRulesTab`,
  `TVSenderAdminE2ETests`) assertieren ausdrücklich, dass KEIN `.admin-dialog-overlay`
  existiert; beide müssen angepasst werden. App-Konvention für Overlays:
  `.admin-dialog-overlay` + `.admin-dialog` (Bestätigungsdialoge auf derselben Seite,
  `ConfirmationDialog`-Komponente, `PlaylistCoverPanel`).
- **Backup/Migration:** Neue Spalten müssen in `VideoWebPlayerBackupData.OptionalRestoreColumns`
  stehen (plus ggf. Defaults) und brauchen eine EF-Migration (`dotnet ef` 10.0.10 verfügbar,
  `EntityFrameworkCore.Design` referenziert).

Test-Ausgangszustand: **2158 gesamt — 2157 grün, 0 Fehler, 1 bekannt übersprungen**
(Alt-Skip). Nachweis: [tests.md](inventory/tests.md) +
[baseline-dotnet-test.log](inventory/test-results/baseline-dotnet-test.log). Hinweis:
`dotnet build VideoPlayer.sln` (Debug) schlägt wegen der laufenden Benutzer-App
(`VideoWebPlayer`, sperrt `bin\Debug`) mit MSB3027 fehl — bekannter Umgebungszustand;
Verifikation über `-p:OutDir=artifacts/schritt23-bin` (identisches Verfahren wie Schritt 21/22).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
- [UI](inventory/ui.md)
