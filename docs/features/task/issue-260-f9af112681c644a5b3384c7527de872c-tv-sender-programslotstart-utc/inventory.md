# Bestandsaufnahme: TV-Sender — `ProgramSlot.Start`/`End` DateTimeKind (Issue #260)

Analysiert wurde der Persistenz- und Lesepfad der `ProgramSlot`-Zeitwerte (`Start`, `End`, `CreatedAt`) sowie alle Konsumenten dieser Werte, bezogen auf die Anforderung in `requirement.md`.

## Zusammenfassung

- `ProgramSlot.Start`/`End`/`CreatedAt` sind `DateTime`-Properties ohne jede `Kind`-Festlegung im EF-Mapping. `ProgramSlotConfiguration` enthält kein `HasConversion`/`ValueConverter` — im gesamten Projekt existiert bislang kein `ValueConverter`.
- Schreibweg: `ProgramScheduleService.MaterializeOccurrence` erzeugt Slot-Zeiten aus `TimeZoneInfo.ConvertTimeToUtc` → `Kind = Utc` in der Entität; SQLite speichert `DateTime` als TEXT ohne Kind → Leseweg materialisiert `Kind = Unspecified`.
- Fehlerbehafteter Konsument: `TVSenderAdmin.razor` ruft `ToLocalTime()` auf den gelesenen Werten auf (Tagesgruppierung Z. 348, Zeitanzeige Z. 383) — bei `Unspecified` ein No-Op.
- Korrekte Konsumenten: `ProgramGuideService` (`SpecifyKind`-Workaround Z. 142–143), `ProgramScheduleService` (reine UTC-Arithmetik), `LiveStreamCoordinator`/`LiveTv.razor` (arbeiten auf `DateTimeOffset`-DTOs), Backup-Pfad (roher ADO.NET-`DbDataReader`, umgeht EF-Materialisierung komplett).
- Weitere Stellen mit demselben latenten Muster, aber außerhalb des Issue-Umfangs: `ToLocalTime()`-Anzeigen anderer UTC-Spalten in `TVSenderAdmin.razor` (Probe-Felder Z. 531, 557, 594, 630) und in `Backups.razor`, `Devices.razor`, `Updates.razor`, `PlaylistDetail.razor`, `PlaylistEntriesList.razor`.
- Test-Ausgangszustand: siehe [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik und Lesewege](inventory/logic.md)
- [Tests](inventory/tests.md)
