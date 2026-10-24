# Umsetzungsplan: TV-Sender — `ProgramSlot.Start`/`End` als UTC lesen (Issue #260)

## Übersicht

`ProgramSlot.Start`/`End` (und `CreatedAt`) werden als UTC persistiert, von EF Core + SQLite aber mit `DateTimeKind.Unspecified` materialisiert, sodass `ToLocalTime()` in der Admin-UI ein No-Op ist. Der `Kind` wird zentral im EF-Mapping geklärt: ein `ValueConverter` auf den drei `DateTime`-Properties in `ProgramSlotConfiguration` setzt beim Lesen `DateTimeKind.Utc`. Abgesichert wird das durch einen Test gegen echtes SQLite, der den gelesenen `Kind` und die `ToLocalTime()`-Umrechnung in einer Nicht-UTC-Zone verifiziert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `Kind`-Korrektur | EF-Core-`ValueConverter` per `HasConversion` in `ProgramSlotConfiguration` (Schreibrichtung Identität, Leserichtung `DateTime.SpecifyKind(v, DateTimeKind.Utc)`) | Zentral — greift bei jeder Materialisierung für alle gegenwärtigen und zukünftigen Konsumenten, statt `SpecifyKind` an jeder Lesestelle wiederholen zu müssen (Issue-Option 2; deckt `TVSenderAdmin.razor` ohne UI-Eingriff ab) |
| Umfang des Konverters | Nur `ProgramSlot.Start`, `End`, `CreatedAt` — kein globaler `ConfigureConventions`-Konverter für alle `DateTime` | Globale Variante würde auch Spalten ohne UTC-Semantik (`Movie.ReleaseDate`, `Genre.StartDate`, `ProgramRule`-Datumswerte) fälschlich als UTC markieren — das vom Issue selbst benannte Seiteneffekt-Risiko |
| `CreatedAt` mitkonvertieren | Ja | Gleiche UTC-Semantik (`DateTime.UtcNow`-Default); sonst bleibt die Entität intern inkonsistent |
| `ProgramGuideService`-`SpecifyKind` | Unverändert lassen | Bleibt korrekt und idempotent; kein Mehrwert im Entfernen, kleinerer Diff |

## Programmabläufe

### Lesen eines `ProgramSlot` (nach der Änderung)

1. EF Core führt eine Query auf `ProgramSlots` aus (z. B. `ProgramScheduleService.GetScheduleAsync`, `ProgramGuideService`-Queries).
2. Bei der Materialisierung jeder Zeile wendet der registrierte `ValueConverter` `DateTime.SpecifyKind(v, DateTimeKind.Utc)` auf `Start`, `End` und `CreatedAt` an.
3. Konsumenten erhalten `Kind = Utc`: `TVSenderAdmin.razor`-Aufrufe `ToLocalTime()` konvertieren nun tatsächlich in die Server-Lokalzeit (Tagesgruppierung und Zeitanzeige); `ProgramGuideService` verhält sich unverändert.

Beteiligte Klassen/Komponenten: `ProgramSlotConfiguration`, `ProgramSlot`, `TVSenderAdmin.razor`, `ProgramGuideService`, `ProgramScheduleService`.

### Schreiben eines `ProgramSlot` (unverändert)

Die Schreibrichtung des Konverters ist die Identität (`v => v`) — die gespeicherte TEXT-Repräsentation in SQLite ändert sich nicht; Bestandsdaten bleiben kompatibel.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ProgramSlotConfiguration` (`IEntityTypeConfiguration<ProgramSlot>`)

- **Geänderte Methode:** `Configure` — die Properties `Start`, `End` und `CreatedAt` erhalten zusätzlich zu `IsRequired()` je ein `HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))`.

## Datenbankmigrationen

Keine. Der `ValueConverter` ändert weder Spaltentyp noch -format (Schreibrichtung = Identität); das DB-Schema und der `ApplicationDbContextModelSnapshot` bleiben unverändert. Zur Verifizierung wird `dotnet ef migrations has-pending-model-changes` ausgeführt.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Query-Übersetzung:** Der Schreib-Teil des Konverters ist die Identität — Parameter- und Literal-Konvertierung in LINQ-Queries (`s.Start >= fromUtc.UtcDateTime` u. a.) bleibt unverändert; kein SQL-Unterschied.
- **Vergleiche/Arithmetik:** `DateTime`-Vergleiche und `-Subtraktionen` ignorieren `Kind` — `ProgramScheduleService`-Interna, `IsRunning`, `ProgressPercent` unverändert.
- **Backup/Restore:** Der Export liest via `DbDataReader` an EF vorbei — kein Einfluss auf Backup-Format oder Restore-Pfad.
- **Bestehende Tests mit `SpecifyKind`-Workaround** (z. B. `ProgramScheduleServiceTests_Generation`): `SpecifyKind` auf einem bereits `Utc`-Wert ist ein No-Op — bleiben grün.
- **`new DateTimeOffset(slot.Start)`-ähnliche Konstruktionen:** `ProgramGuideService` nutzt `SpecifyKind` explizit — unverändert korrekt.
- **Gleiches Muster bei anderen Entitäten** (`MediaItem.DurationProbedAt`, `MediaItem.LastAttemptAt`, Backup-/Pairing-/Update-Zeitstempel in denselben Admin-Views): bleibt bewusst außerhalb des Issue-Umfangs; als mögliche Folgeaufgabe im Review vermerken.

## Umsetzungsreihenfolge

1. **`ValueConverter` in `ProgramSlotConfiguration` registrieren**
   - Voraussetzungen: Keine (EF Core bereits im Projekt)
   - Beschreibung: `HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))` auf `Start`, `End`, `CreatedAt` ergänzen.

2. **Testklasse `ProgramSlotDateTimeKindTests` anlegen**
   - Voraussetzungen: `ProgramScheduleTestDb` (vorhanden), xunit.v3 (vorhanden)
   - Beschreibung: Tests gegen echtes In-Memory-SQLite nach `test-class-structure`-Konvention (kleine, fokussierte Klasse).

3. **Verifikation**
   - Voraussetzungen: Schritte 1–2
   - Beschreibung: `dotnet ef migrations has-pending-model-changes` (kein Pending-Model-Diff erwartet), `dotnet build` Debug + Release, `dotnet test` gefiltert auf die neue Klasse, dann volle Suite; Sabotageprobe: Test ohne Konverter rot machen (Review-Schritt).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ReadBack_StartEndCreatedAt_HaveUtcKind` | `ProgramSlotDateTimeKindTests` | Aus SQLite gelesener Slot: `Start`/`End`/`CreatedAt.Kind == DateTimeKind.Utc`, Tick-Werte identisch zum Seed |
| `ReadBack_Start_ToLocalTime_ConvertsInNonUtcZone` | `ProgramSlotDateTimeKindTests` | `ToLocalTime()` auf dem gelesenen Wert liefert `Kind == Local` und deckt sich mit `TimeZoneInfo.ConvertTimeFromUtc`; zusätzlich Festzeitzonen-Nachweis (`TimeZoneInfo.ConvertTimeFromUtc` in eine feste Nicht-UTC-Zone mit bekanntem Offset, z. B. Tokio UTC+9 ohne DST) — maschinen-unabhängig |

### Betroffene bestehende Tests

Keine. Bestehende `SpecifyKind`-Workarounds in `ProgramScheduleServiceTests_Generation` u. a. bleiben funktional (No-Op auf `Utc`-Werten); sie werden nicht umgeschrieben.

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich. Begründung: Die Änderung liegt ausschließlich in der EF-Materialisierungsschicht — es gibt keinen neuen oder geänderten Benutzerfluss, keine neue UI-Interaktion und kein über die UI auslösbares Fehlerszenario, das nur E2E-nachweisbar wäre. Der sichtbare Effekt (korrekte lokale Zeitanzeige) hängt von der Server-Zeitzone ab und ist in einer E2E-Umgebung nicht steuerbar; der verbindliche Nachweis (`Kind` beim Lesen + Umrechnung) wird durch den SQLite-Test erbracht. `TVSenderAdminE2ETests` bleiben unverändert relevant und werden mit der Suite mitgelaufen.

## Offene Punkte

Keine.
