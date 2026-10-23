# Bestandsaufnahme: Logik

## `ProgramScheduleService` (implementiert `IProgramScheduleService`)

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (~1118 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync(utcNow, ct)` | public | Erweitert den Plan lückenlos bis `utcNow + Horizont` (extend-only) |
| `ReconcileAsync(utcNow, ct)` | public | Re-Bewertung: verdrängt ungültige Slots (Suffix-Verdrängung), materialisiert Ersatz |
| `GetScheduleAsync(channelId, fromUtc, maxCount, ct)` | public | Slots (Scheduled **und** Displaced) ab `fromUtc`, für die Admin-Ansicht |
| `LoadAccessContextAsync` | private | Lädt Freigabe-Kontext (Quellenzugriff aller Nicht-Admins + Einzelfreischaltungen) |
| `ExpandMaterialAsync` | private | Expandiert Regel-Material zu `SchedulableItem`s (Freigabe- und Laufzeitfilter) |
| `BuildOccurrences` | private | Regel-Wiederholungen → konkrete Ausstrahlungs-Zeitpunkte (Feiertagskalender) |
| `MaterializeOccurrence` / `MaterializeOccurrenceEvicting` | private | Erzeugt `ProgramSlot`s aus einer Ausstrahlung |
| `FillGaps` | private | Füllt Restlücken mit Füller-Regel-Material |
| `GroupAirings` / `AiringReproducible` / `FindItemIndex` / `RewindCursor` | private | Reconcile-Hilfen (Sequenz-Validierung, Cursor-Rücksetzung) |

Konstruktor-Abhängigkeiten: `ApplicationDbContext`, `IProgramRuleService`, `ProgramSettingsService`,
`ILogger<ProgramScheduleService>`.

Wichtig für Schritt 5: `GetScheduleAsync` liefert **alle** Slots inkl. `Displaced` — für die
Programmauskunft ist ein eigenes, ausschließlich `Scheduled`-Slots lieferndes Leseartefakt nötig;
der volle Plan bleibt administrativ.

## `ProgramRuleService` (implementiert `IProgramRuleService`)

Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetOrCreateChannelAsync(ct)` | Liefert den einzigen Sender, legt ihn bei Bedarf an |

Wird von `ProgramScheduleService` und `TVSenderAdmin` verwendet — auch für die Programmauskunft
der Kanal-Accessor.

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

Hosted Service; weckt per `IProgramScheduleSignal` oder Intervall und ruft `EnsureCoverageAsync` +
`ReconcileAsync` hinter `IBackgroundProcessingGate`. Für die Auskunft nicht direkt relevant
(reine Lese-Seite), stellt aber sicher, dass der Plan vorhanden ist.

## `ProgramSettingsService`

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

Persistierte Einstellungen aus `Setups`-Tabelle: `GetProgramScheduleHorizonAsync` (Standard 48 h),
`GetProgramScheduleIntervalAsync` (15 min), `GetHolidayFederalStateAsync`, `UpdateProgramScheduleSettingsAsync`.

## Controller-Muster

- `ApiBaseController` (`VideoWebPlayer/Controllers/ApiBaseController.cs`): `CurrentUser` über
  `IAuthService`, `CheckLoggedIn()` (wirft `UnauthorizedAccessException` → 401), `Logger`.
- `BearerTokenCheckAttribute` (`VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`):
  validiert JWT aus `Authorization: Bearer`-Header **oder** `access_token`-Query; lehnt sonst mit 401 ab.
- `PlaylistsController`: `ExecuteAsync`-Muster (KeyNotFound→404, Unauthorized→401, Argument→400,
  InvalidOperation→400, sonst→500, deutsche Log-Kontexte). `[ApiController]`, `[Route("api/...")]`,
  `[BearerTokenCheck]` auf Klassenebene.
- Admin-Prüfung serverseitig via `User.HasClaim("IsAdmin", "True")` bzw. `CurrentUser.IsAdmin`
  (muster: `UnlockedMediaController`, `AdminSourcesController`).

## Titelauflösung (UI-Muster)

`TVSenderAdmin.razor` (Zeilen ~391-415): lädt für `GetScheduleAsync`-Ergebnis
`slotMovieNames` (`Db.Movies.Where(m => ids.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Name)`)
und `slotEpisodeTitles` (`Db.TVShowEpisodes` inkl. `TVShowSeason.TVShow`, Format
`"{Show} — {Season}, E{Number}: {Name}"`); Fallback „Unbekannt" bei leerlaufender weicher Referenz.
Dieses Muster ist für die Programmauskunft in den Service zu verlagern.
