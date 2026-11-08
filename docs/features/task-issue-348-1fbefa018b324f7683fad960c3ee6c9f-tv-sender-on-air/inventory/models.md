# Datenmodell — Bestandsaufnahme „TV-Sender: On-Air" (Issue #348)

Alle Tabellen liegen in `ApplicationDbContext` (`VideoWebPlayer/Data/ApplicationDbContext.cs`).
Die `Setups`-Tabelle ist eine Single-Row-Tabelle: das unique Index `UX_Setups_SingleRow` wird
per Raw-SQL in der Migration `AddSetupsSingleRowIndex` erzwungen (steht nicht im EF-Snapshot).

## `Setup`

Datei: `VideoWebPlayer/Data/Setup.cs` — `DbSet<Setup> Setups` (ApplicationDbContext.cs:101).
Zieltabelle der neuen bool-Spalte (Arbeitsname `TvSenderOnAir`) — bisher **keine** On-Air-Eigenschaft.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel. |
| `DataVersion` | `int` | Aktuelle Datenversion (Data-Upgrade-Mechanismus). |
| `GenresChanged` | `bool` | Merker: Genres haben sich geändert. |
| `ApplicationTitle` | `string` | Anwendungstitel in Navigation/Startseite. Default „Martins Videosammlung". |
| `ScanProcessIntervalMinutes` | `int` | Scan-Intervall. Default 60. |
| `MediaCollectionScanIntervalDays` | `int` | Re-Scan-Intervall. Default 7. |
| `ContinueWatchingEndThresholdSeconds` | `int` | Weiterschauen-Schwelle. Default 30. |
| `ActorCollectionThresholdPercent` | `int` | Schwellwert Schauspieler-Sammlung. Default 50. |
| `MdnsAdvertisementEnabled` | `bool` | Admin-Schalter mDNS-Ankündigung. Default `true`. |
| `DiscoveryPublicBaseUrl` | `string?` | Admin-Override der Discovery-Basis-URL; je Request gelesen. |
| `PlaylistBackfillLastSweepAt` | `DateTime?` | Zeitpunkt des letzten Playlist-Backfill-Sweeps. |
| `TvSenderEnabled` | `bool` | **Äußere Hürde**: Feature-Flag TV-Sender — steuert Navigation, `/live-tv`, `/admin/tv-sender`, `/api/program-guide`, `/api/live-stream`, `/api/internal/media-stream` und alle drei Worker. Default `false` (Zeile 80). Migration `AddSetupTvSenderEnabled` (`nullable: false, defaultValue: false`). |
| `HolidayFederalState` | `string?` | Bundesland (ISO-3166-2) für Feiertagsregeln. |
| `ProgramScheduleHorizonHours` | `int` | Planungshorizont Sendeplan. Default 48. |
| `ProgramScheduleIntervalMinutes` | `int` | Generierungsintervall Sendeplan. Default 15. |
| `ProgramScheduleContentScope` | `ProgramScheduleContentScope` | Inhaltsspektrum des Sendeplans. Default `SharedOnly`. |
| `ProgramScheduleAgeWindow12Local` | `TimeOnly` | Jugendschutz-Fenster ab 12. Default 20:00. |
| `ProgramScheduleAgeWindow16Local` | `TimeOnly` | Jugendschutz-Fenster ab 16. Default 22:00. |
| `ProgramScheduleAgeWindow18Local` | `TimeOnly` | Jugendschutz-Fenster ab 18. Default 23:00. |
| `ProgramScheduleAgeWindowNightEndLocal` | `TimeOnly` | Gemeinsames Nachtfenster-Ende. Default 06:00. |
| `ProgramScheduleUnknownRatingMode` | `ProgramScheduleUnknownRatingMode` | Umgang mit Material ohne Altersfreigabe. Default `RestrictToNightWindow`. |
| `LiveStreamSegmentLengthSeconds` | `int` | HLS-Zielsegmentlänge. Default 6. |
| `LiveStreamSegmentDirectory` | `string?` | Segment-Verzeichnis; `null`/leer = `Data/LiveStream`. |
| `LiveStreamSegmentRetentionMinutes` | `int` | Segment-Aufbewahrung. Default 30. |
| `LiveStreamStandbyMediaItemId` | `long?` | Standby-Clip (Soft Reference ohne FK); `null` = eingebetteter Default-Clip. |

Im Model-Snapshot (`VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`, Setup-Entity
ab Zeile ~300) ist jede Spalte als `b.Property<...>(...)` abgebildet — eine neue bool-Spalte
landet dort analog zu `TvSenderEnabled` (Zeile 330: `b.Property<bool>("TvSenderEnabled").HasColumnType("INTEGER")`).

## `TvSenderUserAccess`

Datei: `VideoWebPlayer/Data/TvSenderUserAccess.cs` — `DbSet<TvSenderUserAccess>` (ApplicationDbContext.cs:229).
Benutzer-Freigabeliste des TV-Senders; vom On-Air-Schalter laut Anforderung **unverändert** ausgenommen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `UserId` | `string` | PK + FK auf `AspNetUsers`. |
| `User` | `ApplicationUser` | Navigation. |

Auswertung: `ProgramSettingsService.IsTvSenderAccessGrantedAsync` (`TvSenderEnabled` UND
(Admin ODER Allowlist-Zeile)).

## `TVChannel`

Datei: `VideoWebPlayer/Data/TVChannel.cs` — `DbSet<TVChannel>` (ApplicationDbContext.cs:200).
Kanalfähiges Modell, aktuell genau ein ausgelieferter Sender — die Anforderung vermerkt eine
On-Air-Spalte an `TVChannel` als denkbare spätere Alternative zur globalen Setup-Spalte
(aktuell nicht erforderlich).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel. |
| `Name` | `string` | Sendername (max. `NameMaxLength` = 255). |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel. |
| `ProgramRules` | `ICollection<ProgramRule>` | Programmregeln des Senders. |

## `LiveStreamJob`

Datei: `VideoWebPlayer/Services/LiveStreamJob.cs` — Record, ein Segmentierungsauftrag.

| Parameter | Typ | Beschreibung / Zweck |
|-----------|-----|----------------------|
| `SlotId` | `long` | `ProgramSlot`-Id des Sendeplatzes. |
| `Input` | `LiveStreamInput` | Medien-Eingang für ffmpeg. |
| `PositionSeconds` | `double` | Startposition im Medium (`-ss`). |
| `RemainingSeconds` | `double` | Restlaufzeit des Sendeplatzes (`-t`). |
| `SegmentSeconds` | `int` | Ziel-Segmentlänge. |
| `SegmentDirectory` | `string` | Zielverzeichnis für Playlist/Segmente. |
| `FfmpegPath` | `string` | Aufgelöster ffmpeg-Pfad. |

## `LiveStreamInput`

Datei: `VideoWebPlayer/Services/LiveStreamInput.cs` — Record, ffmpeg-Eingangsspezifikation;
Erzeugung nur über Factory-Methoden `ForFile` / `ForStream` / `ForHttp` / `ForSftp`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FilePath` | `string?` | Lokaler Dateipfad-Eingang. |
| `OpenStreamAsync` | `Func<CancellationToken, Task<Stream?>>?` | Stream-Factory für `pipe:0`-Eingang. |
| `HttpUrl` | `string?` | Range-fähige URL des internen Media-Stream-Endpunkts. |
| `SftpUrl` | `string?` | Direkter `sftp://`-Eingang (Credentials im Userinfo). |
| `ResolutionNote` | `string?` | Grund, warum kein bevorzugter Eingang gewählt wurde. |
| `IsPipe` | `bool` | `true` bei Pipe-Eingang. |
| `InputKindLabel` | `string` (internal) | Kurzlabel „Datei"/„HTTP"/„sftp"/„pipe:0". |

## `LiveStreamExitInfo`

Datei: `VideoWebPlayer/Services/LiveStreamExitInfo.cs` — Record, Diagnose eines unerwartet
beendeten Segmentierungsprozesses (Eskalationsgedächtnis der Eingangskaskade HTTP → sftp → pipe:0).

| Parameter | Typ | Beschreibung / Zweck |
|-----------|-----|----------------------|
| `SlotId` | `long` | Betroffener Sendeplatz. |
| `ExitCode` | `int` | Prozess-Exitcode (-1 unbekannt). |
| `HttpTransportFailure` | `bool` | HTTP-Eingang transportseitig gescheitert. |
| `OccurredAtUtc` | `DateTimeOffset` | Zeitpunkt der Erkennung. |
| `UndecodableInput` | `bool` | Eingang nicht demuxbar — Slot wird übersprungen. |
| `SftpInputFailure` | `bool` | sftp-Eingang gescheitert. |
| `HttpInputFailedForSlot` / `SftpInputFailedForSlot` | `bool` | Kumulierte Stufen-Fehlschläge je Slot. |

## `LiveStreamSyncResult`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Zeile 34) — Record, Ergebnis eines
Synchronisationstakts.

| Parameter | Typ | Beschreibung / Zweck |
|-----------|-----|----------------------|
| `Action` | `LiveStreamSyncAction` | Entscheidung des Takts (siehe [enums.md](enums.md)). |
| `WakeAtUtc` | `DateTimeOffset?` | Nächster fälliger Entscheidungszeitpunkt; `null` = Poll-Takt. |
| `Detail` | `string?` | Log-Detail. |

## `GeneralSettingsUpdate`

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs` (Zeile 626) — Record,
Parameter-Objekt des atomaren Schreibens der Allgemein-Einstellungen durch
`UpdateGeneralSettingsAsync`; enthält heute `TvSenderEnabled` — für den On-Air-Schalter
ggf. zu erweitern (Platzierungsfrage in der Anforderung).

| Parameter | Typ | Beschreibung / Zweck |
|-----------|-----|----------------------|
| `ApplicationTitle` | `string` | Anwendungstitel. |
| `ScanProcessIntervalMinutes` | `int` | Scan-Intervall. |
| `MediaCollectionScanIntervalDays` | `int` | Re-Scan-Intervall. |
| `ContinueWatchingEndThresholdSeconds` | `int` | Weiterschauen-Schwelle. |
| `MdnsAdvertisementEnabled` | `bool` | mDNS-Schalter. |
| `DiscoveryPublicBaseUrl` | `string?` | Discovery-Basis-URL. |
| `TvSenderEnabled` | `bool` | TV-Sender-Freischaltung. |

## `LiveStreamOptions` / `ProgramScheduleOptions`

Dateien: `VideoWebPlayer/Configuration/LiveStreamOptions.cs`, `ProgramScheduleOptions.cs` —
Konfigurationsklassen (Infrastruktur-Schalter via `appsettings`), **keine** Admin-Einstellungen.
`LiveStreamOptions` trägt nur `UseHttpInput` und `UseSftpInput` (Eingangskaskade). Der
On-Air-Schalter gehört der Anforderung zufolge als Setup-Spalte nicht hierher.

## Standby-Records (internal)

Datei: `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs` — `StandbySegment` (Zeile 20:
`Name`, `Duration`, `Data`) und `StandbySegmentSet` (Zeile 27: `Generation`, `MediaItemId`,
`Segments`, `TotalDuration`, `Find(name)`) — immutabler, atomar getauschter In-Memory-Satz
der Standby-Auslieferung; keine Persistenz.
