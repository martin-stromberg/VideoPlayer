# Übersetzte Anforderung: TV-Sender — `ProgramSlot.Start`/`End` als UTC lesen (DateTimeKind klären)

Quelle: Issue #260 (Nachbesserung aus Issue #111, PR #257), `issue.md`.

## Fachliche Zusammenfassung

`ProgramSlot.Start` und `ProgramSlot.End` werden als UTC persistiert (Modellkommentar: „UTC start time"), von EF Core + SQLite aber mit `DateTimeKind.Unspecified` materialisiert, weil SQLite `DateTime` als Text ohne Kind-Information speichert. `ToLocalTime()` in der Admin-UI (`TVSenderAdmin.razor`) ist damit ein No-Op: Angezeigt werden die UTC-Rohwerte, und die Tagesgruppierung verschiebt sich in Nicht-UTC-Zonen. Der `Kind` der gelesenen Werte ist explizit als `Utc` zu deklarieren, statt `Unspecified` zu dulden.

## Betroffene Klassen und Komponenten

- Datenmodell / EF-Mapping:
  - `VideoWebPlayer/Data/ProgramSlot.cs` — `Start`, `End` (und `CreatedAt`, ebenfalls UTC-semantisch) als `DateTime`
  - `VideoWebPlayer/Data/Configurations/ProgramSlotConfiguration.cs` — `IEntityTypeConfiguration<ProgramSlot>`; hier ist ein `ValueConverter`/`HasConversion` auf den `DateTime`-Properties der zentrale Ansatzpunkt
- UI (kein Code-Eingriff erforderlich, Verhalten wird korrekt):
  - `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — `s.Start.ToLocalTime().Date` (Tagesgruppierung, Z. 348), `slot.Start/End.ToLocalTime()` (Zeitanzeige, Z. 383)
- Bereits korrekt arbeitende Lesewege:
  - `VideoWebPlayer/Services/ProgramGuideService.cs` — setzt `DateTime.SpecifyKind(slot.Start, DateTimeKind.Utc)` beim Mapping auf `DtoProgramGuideEntry` (Z. 142–143)
  - `VideoWebPlayer/Services/ProgramScheduleService.cs` — reine UTC-Vergleiche/Arithmetik, Kind-unabhängig
- Tests:
  - `VideoWebPlayer.Tests` — neuer Test gegen echtes SQLite (`ProgramScheduleTestDb`), der Kind beim Lesen verifiziert und die `ToLocalTime()`-Umrechnung in einer Nicht-UTC-Zone prüft

## Implementierungsansatz

Zwei vom Issue genannte Optionen:

1. `DateTime.SpecifyKind(..., DateTimeKind.Utc)` konsequent nach dem Laden anwenden (Muster aus `ProgramGuideService`) — dezentral, muss an jeder Lesestelle wiederholt werden, fragil bei neuen Konsumenten.
2. EF-Core-`ValueConverter` auf den `DateTime`-Properties von `ProgramSlot` registrieren (`HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))`) — zentral in `ProgramSlotConfiguration`, greift bei jeder Materialisierung; Schreibrichtung bleibt Identität (keine Schemaänderung, keine Migration erforderlich).

Option 2 ist der bevorzugte Ansatz (Issue: „Kind explizit klären" — zentral statt an Einzelstellen). Ein globaler Konverter für alle `DateTime`-Properties via `ConfigureConventions` ist bewusst NICHT vorgesehen: Spalten ohne UTC-Semantik (z. B. `Movie.ReleaseDate`, `Genre.StartDate`) würden sonst fälschlich als UTC markiert — Seiteneffekt-Risiko, das der Issue selbst benennt.

Annahme (als solche gekennzeichnet): `CreatedAt` wird ebenfalls mit UTC-Kind gelesen — es wird mit `DateTime.UtcNow` befüllt und ist dieselbe Semantik; die Erweiterung ist konsistent und risikofrei.

## Konfiguration

Keine Konfiguration erforderlich — das Verhalten ist eine Korrektur der Persistenz-Semantik, kein einstellbares Feature.

## Offene Fragen

Keine.

## Akzeptanzkriterien (aus dem Issue)

- `ProgramSlot.Start`/`End` erreichen die UI mit klar definiertem `Kind` (oder die UI konvertiert bewusst über `DateTimeOffset`).
- Test, der einen gelesenen Slot in einer Nicht-UTC-Zone per `ToLocalTime()`-Umrechnung verifiziert.
