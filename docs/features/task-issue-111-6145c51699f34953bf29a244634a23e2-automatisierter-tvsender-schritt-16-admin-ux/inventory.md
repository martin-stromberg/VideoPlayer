# Bestandsaufnahme: TV-Sender-Adminseite restrukturieren + Probe-Detail-Listen (N13+N14)

Analysiert wurden `TVSenderAdmin.razor`, `MediaDurationProbeService`/`MediaDurationProbeProgress`,
`app.css` sowie die zugehörigen Tests, bezogen auf die Anforderung in `requirement.md`.

## Zusammenfassung

- `TVSenderAdmin.razor` (1061 Zeilen) rendert alle vier Belange untereinander: Sender-Karte,
  Programmregeln-Karte, Sendeplan-Karte, Fortschrittskarte `#tvsender-probe-card`. Der
  Regel-Editor ist ein Overlay-Dialog (`admin-dialog-overlay`), der Lösch-Dialog ebenfalls.
- `MediaDurationProbeProgress` enthält Zähler + `RecentItems`; Listen ausstehender bzw.
  fehlgeschlagener Dateien fehlen noch — die Datenlage (`Duration`, `DurationProbedAt`,
  `DurationProbeAttempts`, `EffectiveMaxAttempts`) reicht aus.
- CSS-Seitig fehlen sämtliche Tab-/Detail-Klassen des abgenommenen Mockups
  (`.admin-tabs`, `.admin-tab`, `.tab-pane`, `.rule-editor-card`, `.probe-detail`,
  `.chip-detail-btn`, `.probe-counts`, `.admin-sub-card`, `.admin-header-side`,
  `.admin-card-header-left`, `.actions-cell`, `.row-displaced`, `.table-wrap-inner`).
  `.progress`/`.progress-bar`/`.btn-sm`/`.mt-2`/`.form-check` kommen aus dem gebündelten
  `bootstrap.min.css`; `.alert-info`, `.admin-toggle-row`, `.rule-multi-select`,
  `.admin-empty-state`, `.admin-dialog*` existieren bereits.
- `tvsender-slot-displaced` wird in der Razor-Datei gesetzt, hat aber derzeit keine
  CSS-Regel (Verdrängt-Anzeige läuft nur über den Status-Chip).
- Tests: `TVSenderAdminE2ETests` (Playwright, 3 Methoden) adressiert Elemente, die nach
  der Umstellung in anderen Tabs liegen (`#tvsender-name` wird beim Seitenaufruf per
  `WaitForSelectorAsync` erwartet → muss an Tab-Struktur angepasst werden).
  `MediaDurationProbeServiceTests_Progress` (6 Methoden) deckt die bisherige
  `GetProgressAsync`-Form ab. bUnit-Beispiel für Admin-Seiten: `GenreAdminTests`
  (SQLite In-Memory + `ctx.AddAuthorization()`).

Test-Ausgangszustand: siehe [Tests](inventory/tests.md) — vollständiger
`dotnet test`-Lauf auf dem unveränderten Stand (Nachweis in `inventory/test-results/`).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [UI](inventory/ui.md)
- [Tests](inventory/tests.md)
