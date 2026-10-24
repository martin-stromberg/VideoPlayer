# Datenmodell

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel (`[Key]`) |
| `TVChannelId` | `long` | FK auf `TVChannel`, Cascade-Delete |
| `TVChannel` | `TVChannel` | Navigation zum Sender |
| `Start` | `DateTime` | UTC-Startzeit der Sendung (Kommentar: „UTC start time") — betroffen |
| `End` | `DateTime` | UTC-Endzeit (`Start` + Laufzeit) — betroffen |
| `MediaItemId` | `long` | Soft Reference auf `MediaItem` (kein DB-FK) |
| `MovieId` | `long?` | Soft Reference auf `Movie`, kein FK |
| `TVShowEpisodeId` | `long?` | Soft Reference auf `TVShowEpisode`, kein FK |
| `ProgramRuleId` | `long?` | Soft Reference auf erzeugende `ProgramRule` |
| `RuleType` | `ProgramRuleType` | Denormalisierter Regeltyp-Snapshot |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) / `Displaced` (nur Historie) |
| `CreatedAt` | `DateTime` | Anlage-Zeitstempel, Default `DateTime.UtcNow` — gleiche UTC-Semantik |

## `ProgramSlotConfiguration`
Datei: `VideoWebPlayer/Data/Configurations/ProgramSlotConfiguration.cs`

`IEntityTypeConfiguration<ProgramSlot>`: Key, `IsRequired` auf `Start`/`End`/`MediaItemId`/`RuleType`/`Status`/`CreatedAt`, `HasOne(TVChannel).WithMany()` Cascade, Index `(TVChannelId, Start)`. **Kein `HasConversion`/`ValueConverter` vorhanden** — Ansatzpunkt der Änderung.

## EF-Materialisierung (SQLite)

- Provider: `Microsoft.EntityFrameworkCore.Sqlite` (siehe `VideoWebPlayer.Tests.csproj` bzw. `ApplicationDbContext`); SQLite speichert `DateTime` als `TEXT` (`yyyy-MM-dd HH:mm:ss.FFFFFFF`), die Kind-Information geht verloren → EF materialisiert `DateTimeKind.Unspecified`.
- `ApplicationDbContext` registriert die Konfigurationen aus `VideoWebPlayer/Data/Configurations/` — `ProgramSlotConfiguration` ist die zentrale, einzige Mapping-Stelle für `ProgramSlot`.
