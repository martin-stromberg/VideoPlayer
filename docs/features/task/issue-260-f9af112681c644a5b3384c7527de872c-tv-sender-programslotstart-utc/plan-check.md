# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `ProgramSlot.Start`/`End` erreichen die UI mit klar definiertem `Kind` | `ValueConverter` (`SpecifyKind` → `Utc` beim Lesen) auf `Start`, `End` (und konsistent `CreatedAt`) in `ProgramSlotConfiguration` — zentral für alle Materialisierungen inkl. `GetScheduleAsync` → `TVSenderAdmin.razor` | `ReadBack_StartEndCreatedAt_HaveUtcKind` (echtes SQLite via `ProgramScheduleTestDb`) | Abgedeckt |
| Test: gelesener Slot in Nicht-UTC-Zone per `ToLocalTime()`-Umrechnung verifiziert | Testklasse `ProgramSlotDateTimeKindTests` | `ReadBack_Start_ToLocalTime_ConvertsInNonUtcZone`: `ToLocalTime().Kind == Local` + Gleichheit mit `ConvertTimeFromUtc` + Festzeitzonen-Umrechnung (Tokio, UTC+9, kein DST) — maschinen-unabhängig diskriminierend | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein neuer/geänderter Benutzerfluss — rein interne Materialisierungs-Korrektur; sichtbarer Effekt (lokale Zeitanzeige) ist von der Server-Zeitzone abhängig und E2E nicht steuerbar | Keine — Begründung im Plan-Abschnitt „E2E-Tests" | Nicht erforderlich mit Begründung |

## Hinweise

- Prüfpfad `Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges` (bUnit, `TVSenderAdminTests`): erwartet bereits lokale Tagesdaten (`slot1Start.ToLocalTime()`); durch die Korrektur stimmen Gruppierung und Erwartung jetzt definitionsgemäß überein — bleibt grün, wird stärker.
- `ToLocalTime()` lässt sich testseitig nicht in eine Fremdzone zwingen (`TimeZoneInfo.Local` ist maschinengebunden); der Plan ersetzt den Zonenwechsel durch die `Kind == Local`-Assertion auf dem `ToLocalTime()`-Ergebnis (deterministisch: schlägt unter `Unspecified` immer fehl) plus Festzonen-Umrechnung — fachlich äquivalent zum Akzeptanzkriterium.
- Gleiches `Unspecified`-Muster bei anderen UTC-Spalten (`MediaItem.DurationProbedAt` u. a.) ist bewusst Out-of-Scope — als Hinweis in `inventory/logic.md` und Plan-Seiteneffekte dokumentiert; mögliche Folgeaufgabe.
