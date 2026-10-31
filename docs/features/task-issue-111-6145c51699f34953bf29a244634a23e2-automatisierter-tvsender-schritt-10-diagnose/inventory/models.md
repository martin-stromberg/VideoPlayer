# Datenmodell — Bestandsaufnahme

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Duration` | `TimeSpan?` | Ermittelte Laufzeit; `null` = nicht planbar. |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs; `null` = nie geprobt. Gesetzt + `Duration == null` = Versuch fehlgeschlagen. |
| `DurationProbeAttempts` | `int` | Anzahl fehlgeschlagener Versuche; `>= MediaProbeOptions.EffectiveMaxAttempts` = permanent gescheitert. |

Probe-Zustände (aus `MediaDurationProbeService` abgeleitet):
- ausstehend: `Duration == null && (DurationProbedAt == null || DurationProbeAttempts < max)`
- dauerhaft gescheitert: `Duration == null && DurationProbedAt != null && DurationProbeAttempts >= max`

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MaterialType` | `ProgramRuleMaterialType` | TVShow / Movie / MovieCollection / Genre / Playlist |
| `MaterialId` | `long` | Referenzierte Entität (bei Genre: erstes Genre / Legacy-Fallback) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Join-Tabelle (Mehrfachauswahl, Schritt 9) |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter (leer = alle Quellen) |
| `IsActive` | `bool` | inaktive Regeln gehen nicht in den Plan ein |

Kein Fremdschlüssel auf `MaterialId` — verwaiste Referenzen sind möglich und werden von den
Expand-Methoden per `AnyAsync`/`FirstOrDefaultAsync` erkannt.

## `MediaProbeOptions`
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `EffectiveMaxAttempts` | `int` | Erschöpfungsschwelle der Laufzeit-Probe (Default 10). |

DI: `services.Configure<MediaProbeOptions>(configuration.GetSection("MediaProbe"))` in
`ServiceCollectionExtensions.cs:234` — `IOptions<MediaProbeOptions>` ist injizierbar.

## `ProgramSlot`, `ProgramRuleCursor`, `TVChannel`
Für die Diagnose nur lesend relevant (Sendeplan-Tabelle, Cursor-Position); keine Änderungen nötig.
**Keine neue Persistenz** — es ist keine Migration und keine `OptionalRestoreColumns`-Erweiterung
erforderlich.
