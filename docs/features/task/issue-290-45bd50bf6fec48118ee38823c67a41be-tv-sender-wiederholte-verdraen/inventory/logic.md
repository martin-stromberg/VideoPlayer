# Weitere Logikklassen — Bestandsaufnahme (Issue #290)

Der Kern (`ProgramScheduleService`, `ReconcileAsync`, `ReconcileWindow`, `AiringReproducible`, `EnsureCoverageAsync`) ist in [reconcile.md](reconcile.md) dokumentiert; die Auslöser im Detail in [ausloeser.md](ausloeser.md). Hier die übrigen beteiligten Klassen.

## `ProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NotifyScheduleChanged` | public | Schreibt `true` in den 1-Slot-Channel, sofern kein Scan-Scope offen ist (Z. 53–59) |
| `BeginScan` | public | `Interlocked.Increment(_activeScans)`, liefert `ScanScope`-`IDisposable` (Z. 62–66) |
| `WaitAsync` | public | `Channel.Reader.ReadAsync` — blockiert bis ein Signal anliegt (Z. 69–70) |
| `EndScan` | private | `Decrement == 0` → einmaliges Wecken (Z. 72–76) |
| `ScanScope.Dispose` | private sealed | Ruft `EndScan` des Besitzers, nullt ihn (Z. 78–85) |

Singleton in DI (`ServiceCollectionExtensions.cs:268–269`). Konsumenten: `ProgramScheduleWorker`, `LiveStreamWorker`.

## `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — siehe [ausloeser.md](ausloeser.md) Abschnitt 1.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | protected override | Intervall-Loop: `ReconcileAsync` + `EnsureCoverageAsync` je Tick und je Signal (Z. 45–107) |

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync` | public | Bis zu `batchSize` unbemessene Video-Items proben; `SaveChangesAsync` je Item; `NotifyScheduleChanged` einmal je Aufruf bei ≥1 geschriebener `Duration` (Z. 78–132, Signal Z. 126–129) |
| `GetProgressAsync` | public | Probe-Fortschritt für die Admin-UI (Z. 147–206) |
| `ResetFailedProbeAsync` | public | Einzel-Reset des Fehlversuch-Zählers (Admin, Z. 430–447) — kein Signal |
| `ResetFailedProbesAsync` | public | Gesamt-Reset (Admin, Z. 458–475) — kein Signal |
| `RunProbesAsync` | private | ffprobe-Aufrufe parallelisiert über `EffectiveParallelProbes` (Z. 239–258) |
| `ProbeItemAsync` / `ProbeRemoteItemAsync` | private | Lokaler Pfad bzw. Loopback-HTTP/`sftp://`/`pipe:0`-Fallbacks (Z. 272–349) |
| `ApplyOutcome` | private | Schreibt `Duration`/`DurationProbeAttempts`/`DurationProbedAt` (Z. 369–389) |
| `VideoExtensionPredicate` / `FailedProbePredicate` | private static | Item-Filter (Z. 391–416) |

Publizierte Events: `IProgramScheduleSignal.NotifyScheduleChanged()` bei geschriebener `Duration`.

## `MediaDurationProbeWorker`
Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | protected override | `TvSenderEnabled`-Gate + `IBackgroundProcessingGate`-Lease; `do … while (processed > 0)`-Backlog-Schleife über `ProbePendingAsync(EffectiveBatchSize)`; danach `Task.Delay(EffectiveIntervalSeconds)` (Default 60 s) (Z. 48–100) |

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes` | public | Zulässige Materialarten je `RuleType` (Z. 39–49) |
| `GetOrCreateChannelAsync` | public | Kanal holen/erzeugen (Z. 52–62) |
| `RenameChannelAsync` | public | Sendername ändern (Admin) — **kein** Signal (Z. 65–75) |
| `GetRulesAsync` | public | Regelliste inkl. Genre/Quell/Clip-Group-Navs (Admin) (Z. 78–93) |
| `GetGenreMaterialOptionsAsync` | public | Genre-Auswahloptionen via `GenreCanonicalResolver.BuildOptions(now, includeOutOfSeason: true)` (Z. 96–107) |
| `CreateRuleAsync` / `UpdateRuleAsync` | public | Regel schreiben + `ReplaceGenreLinksAsync`/`ReplaceClipGroupLinksAsync`; Signal am Ende (Z. 110–176) |
| `SetRuleActiveAsync` / `DeleteRuleAsync` | public | Aktiv-Flag / Löschung; Signal (Z. 216–241) |
| `ValidateRuleInputAsync` / `ValidateClipGroupInputAsync` / `ValidateGenreInputAsync` | private | Fachliche Validierung (Z. 252–359) |
| `RequireAdminAsync` | private | Admin-Erzwingung (Z. 243–250) |

Publizierte Events: `NotifyScheduleChanged` bei jeder Mutation (Z. 143, 174, 226, 240).

## `ProgramClipGroupService`
Datei: `VideoWebPlayer/Services/ProgramClipGroupService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGroupsAsync` / `GetGroupCountsAsync` / `GetGroupClipsAsync` | public | Lesepfade (Admin) (Z. 32–39, 87–94, 97–113) |
| `GetClipCandidatesAsync` | public | Unklassifizierte Video-Items, Suche/Quell-Filter, `take` ≤ 200 (Z. 116–163) |
| `CreateGroupAsync` / `RenameGroupAsync` / `DeleteGroupAsync` | public | Gruppen-CRUD; `IsSystem`-Sperren; Signal (Z. 42–84) |
| `AddClipsToGroupAsync` / `RemoveClipFromGroupAsync` | public | Mitgliedschafts-Mutationen; Signal — `RemoveClipFromGroupAsync` feuert auch ohne gefundene Zeile (Z. 166–225) |
| `ValidateGroupNameAsync` / `RequireAdminAsync` | private | Namensregeln + Admin (Z. 227–247) |

## `MediaSourceScanService`
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | protected override | Scan-Loop (`GetScanIntervalsAsync`, Fallback 1 h): `BeginScan`-Scopes für `IPlaylistBackfillSignal` **und** `IProgramScheduleSignal` (Z. 101–103) um Scanner+Klassifizierer; Scanende weckt den Plan-Worker einmal |

## `ApplicationDbContext` (partiell: Signal-Hooks)
Dateien: `VideoWebPlayer/Data/ApplicationDbContext.cs`, `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SaveChanges(bool)` / `SaveChangesAsync(bool, ct)` | public override | `CollectMarkCandidates` (neue/umgehängte `Movie`/`TVShowSeason`/`TVShowEpisode`) → gebündelter Marker-Upsert in derselben Transaktion → `NotifyMarkersWritten` + `NotifyScheduleChanged` (Z. 55–144, Signale Z. 85/94/133/142) |
| `DeleteMediaSourceAsync` | public | Quell-Löschung inkl. aller Kinder; `NotifyScheduleChanged` nach Commit (Z. 293–455, Signal Z. 448) |
| `CollectMarkCandidates` / `TryGetChangedForeignKey` / `ResolveMarks(Async)` / `UpsertMarkers*` | private | Markierungslogik — nur Entitäten mit echter FK-Änderung; `ProgramSlot`-/Cursor-Schreibungen der Planläufe lösen nichts aus |

## `GenreCanonicalResolver`
Datei: `VideoWebPlayer/Services/GenreCanonicalResolver.cs` (`internal static`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsVisible` | public static | `!IsHidden` und — nur wenn beide Grenzen gesetzt — `now.Date` im jährlich wiederkehrenden Fenster (Z. 25–42) — **zeitabhängig** |
| `HasEffectiveAlternates` / `EffectiveAlternateNames` | public static / internal static | Weiterleitungs-Namen ≠ Eigenname (Z. 50–51, 112–118) |
| `ResolveCanonicalNames` | public static | Genre-Id → kanonische Namen; alternates-rekursiv, quellgebunden, zykelsicher; unsichtbare Zeilen ohne Alternates leisten nichts (Z. 62–75) — aufgerufen mit `DateTime.UtcNow` in `ExpandGenreAsync` (Z. 2327) und `GetGenreMaterialOptionsAsync` |
| `BuildOptions` | public static | Auswahloptionen; `includeOutOfSeason` für Konfigurations-UI (Z. 92–98) |

## `ProgramScheduleAgeWindow` / `ProgramScheduleAgeWindowRule` / `ProgramScheduleAgeWindowSettings`
Dateien: `VideoWebPlayer/Services/ProgramScheduleAgeWindow.cs`, `ProgramScheduleAgeWindowRule.cs`, `ProgramScheduleAgeWindowSettings.cs`

| Mitglied | Sichtbarkeit | Kurzbeschreibung |
|----------|-------------|------------------|
| `IsAllowedAt(minAge, utcStart, rule)` | public static | Fenster je Mindestalter (`null` → `UnknownMode` bzw. FSK-18-Fenster); Prüfung auf lokale Startzeit; begonnener Titel läuft über die Kante (Z. 27–57) |
| `CountBlockedAt` | public static | Anzahl gesperrter Kandidaten (Diagnose) (Z. 63–65) |
| `ProgramScheduleAgeWindowRule` | record | `(Windows, UnknownMode)` — wandert als Einheit durch Materialisierung/Auswahl/Reproduzierbarkeit |
| `ProgramScheduleAgeWindowSettings` | record | `(Fsk12Earliest, Fsk16Earliest, Fsk18Earliest, NightEnd)`; `Default` 20/22/23 Uhr, Nachtende 06:00; `IsValid()` Monotonie + Nachtende < FSK12 |

## `ProgramSettingsService` (sendeplanrelevante Member)
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetTvSenderEnabledAsync` / `UpdateTvSenderEnabledAsync` | public | Feature-Flag lesen/schreiben (Z. 195–211) |
| `GetHolidayFederalStateAsync` | public | Bundesland für `Holiday`-Regeln (Z. 317) |
| `GetProgramScheduleHorizonAsync` | public | Horizont, Fallback 48 h (Z. 345–350) |
| `GetProgramScheduleIntervalAsync` | public | Worker-Takt, Fallback 15 min (Z. 357–362) |
| `UpdateProgramScheduleSettingsAsync` | public | Horizont + Intervall speichern (Z. 371–383) |
| `GetProgramScheduleContentScopeAsync` / `UpdateProgramScheduleContentScopeAsync` | public | `SharedOnly`/`AllContent`; korrupte Werte → `SharedOnly` (Z. 392–416) |
| `GetProgramScheduleAgeWindowsAsync` / `UpdateProgramScheduleAgeWindowsAsync` | public | Fensterschema; korrupte Werte → `Default` (Z. 426–459) |
| `GetProgramScheduleUnknownRatingModeAsync` / `Update…` | public | Unbekannte-Freigabe-Modus (Z. 469–489) |

## `ProgramGuideService` (Konsument, nur Lesepfad)
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs`

Fragt `ProgramSlots` ausschließlich mit `Status == ProgramSlotStatus.Scheduled` ab (Z. 44–81) — `Displaced`-Zeilen tauchen in Programmauskunft und Live-Stream-Steuerung nicht auf; die Duplikat-Serie des Issues ist nur in der Admin-Planansicht sichtbar.

## `LiveStreamWorker` (Signal-Konsument)
Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs`

Zweiter `IProgramScheduleSignal`-Konsument (Z. 22, 40): weckt die ffmpeg-Synchronisation bei Planänderungen; schreibt nicht auf den Plan.

## `TVSenderAdmin.razor` (Admin-UI)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

| Member | Kurzbeschreibung |
|--------|------------------|
| `OnInitializedAsync` (Z. 1132–1188) | Admin-/Feature-Flag-Prüfung, lädt Kanal, Einstellungen, Regeln, `ReloadScheduleAsync`, Diagnosen, Probe-Fortschritt; feuert **kein** Signal |
| `GenerateNowAsync` (Z. 1411–1437) | „Jetzt generieren": `NotifyScheduleChanged`, 4 s warten, Reload |
| `CleanupDisplacedAsync` (Z. 1439–1457) | `DeleteDisplacedSlotsAsync` + Reload — kein Signal |
| `ResetScheduleAsync` (Z. 1459–1476) | `ResetScheduleAsync` + Signal |
| `ReloadScheduleAsync` (Z. 1478–1499+) | `GetScheduleAsync(channel.Id, now−2h, 200)` — `Scheduled` + `Displaced` gemischt; Chips „Geplant" (Z. 457) / „Verdrängt" (Z. 493); `tvsender-slot-displaced`-CSS-Klasse (Z. 479) |
| `SaveChannelSettingsAsync` (Z. ~1820–1881) | Speichert sämtliche Sender-/Fenster-/Live-Stream-Einstellungen + Signal (Z. 1874) |
| `SaveRuleAsync`/`ToggleRuleActiveAsync`/`DeleteRuleAsync` | Delegiert an `ProgramRuleService` (Signal dort) |
| `BuildScheduleDisplayItems` / Clip-Block-Gruppierung (Z. ~1609+) | Reine Anzeige-Transformation (`IsClipFill && Scheduled` → Blöcke) |

## `ProgramSettings.razor` (Admin-UI)
Datei: `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`

| Member | Kurzbeschreibung |
|--------|------------------|
| `SaveAsync` (Z. 210–245) | Allgemeine Einstellungen + TV-Sender-Allowlist; `NotifyScheduleChanged` bei jedem Speichern (Z. 234) |
