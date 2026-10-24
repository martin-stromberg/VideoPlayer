# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `HasConversion` (`v => v`, `v => DateTime.SpecifyKind(v, DateTimeKind.Utc)`) auf `ProgramSlot.Start` in `ProgramSlotConfiguration` — vorhanden (`UtcOnRead`-Konverter)
- [x] `HasConversion` auf `ProgramSlot.End` — vorhanden
- [x] `HasConversion` auf `ProgramSlot.CreatedAt` — vorhanden
- [x] Test `ReadSlot_AfterSaveAndReload_ExposesUtcKind` in `ProgramSlotTests_DateTimeKind` — prüft `Kind == Utc` auf `Start`/`End`/`CreatedAt` plus Tick-Identität zum Seed
- [x] Test `ReadSlot_ToLocalTime_ConvertsInNonUtcZone` in `ProgramSlotTests_DateTimeKind` — prüft `ToLocalTime()`-Ergebnis gegen `ConvertTimeFromUtc` (Maschinenzone) und Festzone Tokio (03:30 am Folgetag)
- [x] Verifikation `dotnet ef migrations has-pending-model-changes` — ausgeführt, Ergebnis „No changes have been made to the model since the last migration" → keine Migration nötig

## Hinweise

- **Namensabweichung:** Testklasse heißt `ProgramSlotTests_DateTimeKind` statt `ProgramSlotDateTimeKindTests` (Skill-Konvention `{TestedClass}Tests_{Topic}`); Testnamen entsprechend `MethodName_Scenario_ExpectedOutcome` angelehnt.
- **Zusätzlicher Test:** `ReadSlot_KindAwareApis_TreatValueAsUtc` — beim Implementieren ergänzt, weil die Sabotageprobe zeigte, dass der `ToLocalTime`-Test allein nicht diskriminiert (s. u.).
- **Fachliche Korrektur der Issue-Prämisse:** Empirisch (net10.0, Zone `W. Europe Standard Time`) konvertiert `DateTime.ToLocalTime()` einen `Unspecified`-Wert als UTC → die Anzeige in `TVSenderAdmin.razor` war bereits korrekt; die tatsächliche Fragilität liegt bei kind-sensitiven APIs (`new DateTimeOffset(slot.Start)` nimmt bei `Unspecified` den lokalen Offset an, `ToUniversalTime()` konvertiert rückwärts, JSON-Serialisierung ohne `Z`-Suffix). Sabotageprobe: Konverter auf `Unspecified` gesetzt → `ReadSlot_AfterSaveAndReload_ExposesUtcKind` und `ReadSlot_KindAwareApis_TreatValueAsUtc` schlagen fehl, `ReadSlot_ToLocalTime_ConvertsInNonUtcZone` bleibt grün (das Anzeigeverhalten war bereits korrekt — der Test sichert die Akzeptanzsemantik, nicht den Bug). Fix danach wiederhergestellt; 3/3 grün.
