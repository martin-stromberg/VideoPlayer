# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweis: Im Gesamtlauf (Runde 2) schlugen zwei Playwright-Tests fehl, beide einmalige
Flacker unter Last — beide bestanden anschließend bei der Einzelwiederholung:

- `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` — Strict-Mode-Verletzung:
  `Locator(".tvsender-slot-row.tvsender-slot-displaced")` löste auf 2 Elemente auf
  (zeitbedingt zwei verdrängte Slots sichtbar). Einzelwiederholung: **bestanden** (15 s).
- `PlaylistDetailInteractionE2ETests.CoverPanel_InvalidFile_ShowsTheServerValidationMessageInThePanel`
  — `#playlist-cover-apply-button` blieb 5 s `disabled` (Timeout in `ToBeEnabledAsync`),
  feature-fremd (Playlist-Cover). Einzelwiederholung: **bestanden** (8 s).

Kein Test wurde deaktiviert oder abgeschwächt; es gab keine Codeänderungen während des Laufs.

- Lauf 1 (vollständig, mit Coverage): 2488 Tests, 2484 bestanden, 2 fehlgeschlagen,
  2 übersprungen, ~6,2 min
- Lauf 2/3 (Einzelwiederholungen der beiden Ausfälle): je 1/1 bestanden

## Fehlgeschlagene Tests

Keine — die zwei Ausfälle von Lauf 1 (s. o.) bestanden bei Einzelwiederholung.
`Admin_CreateEditDeactivateDeleteRule` ist ein im Plan gelisteter „verifizieren"-Bestandstest
des geänderten `SaveChannelSettingsAsync`-Pfads; der Fehler war ein Playwright-Timing-Flacker
(zwei gleichzeitig sichtbare `tvsender-slot-displaced`-Zeilen), kein Feature-Fehler.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Zuschauer öffnet `/live-tv` in der Warm-up-Phase → Standby läuft, keine Fehleranzeige | `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` | Bestanden (15 s) |
| Externer Player / gekoppeltes Gerät: Playlist ohne produzierten Stream | `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist` | Bestanden (7 s) |
| Externer Player: Standby-Segment ausliefern | `LiveStreamE2ETests.PairedDevice_RequestStandbySegment_ReturnsSegment` | Bestanden (7 s) |
| Übergang Standby → echter Stream (`DISCONTINUITY`, `access_token`-Rewrite, `no-store`) | `LiveStreamE2ETests.PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity` | Bestanden (4 s) |
| Admin wählt Standby-Clip (Medienquelle → MediaItem → Speichern, persistiert) | `TVSenderAdminE2ETests.Admin_StandbyClip_PickAndPersist` | Bestanden (17 s) |
| Konfigurierter Clip wird vorbereitet (Auflösung → Transcode → Cache → Auslieferung) | `LiveStreamE2ETests.PairedDevice_ConfiguredStandbyClip_PreparesAndServes` | Bestanden (4 s) |
| `no-store` auf echter `.ts`-Antwort über die HTTP-Pipeline | `LiveStreamE2ETests.PairedDevice_RequestSegment_ReturnsNoStore` | Bestanden (8 s) |
| Auth-/Freigabefälle (401/403/404 auf fremde Dateien) greifen vor dem Fallback | Bestehende E2E-/Controller-Tests (u. a. `PairedDevice_RequestInvalidOrMissing_Returns404`, `Anonymous_RequestSegment_Returns401`) | Bestanden |

Alle in `plan.md` (Abschnitt „E2E-Tests") und `plan-check.md` (Abschnitt „E2E-Abdeckung")
als Pflicht genannten Szenarien waren im Lauf enthalten und haben bestanden. Die dort als
„verifizieren" gelisteten Bestandstests (`LiveTv_ViaNavMenu_ShowsPlayerAndProgram`,
`LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming`,
`PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist`,
`PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment`,
`Admin_ProbeReset_SingleItemAndAllFailed`, `Admin_GenreRule_MultiSelectAndSourceFilter`,
`NonAdmin_SeesNotAuthorized`, beide `TVSenderAdminE2ETests_FfmpegDownload`-Tests) bestanden
ebenfalls — `Admin_CreateEditDeactivateDeleteRule` nach Einzelwiederholung (s. o.).

## Zusammenfassung

- Gesamt: 2488
- Bestanden: 2486 (2484 in Lauf 1 + 2 nach Einzelwiederholung)
- Fehlgeschlagen: 0 (nach Wiederholung; 2 Flacker in Lauf 1)
- Übersprungen: 2
  - `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` (nur Unix, unter
    Windows erwartungsgemäß übersprungen)
  - `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
    (im Test bedingt übersprungen, feature-fremd)

## Testabdeckung

**Abdeckung:** 95,5 % Zeilenabdeckung gesamt (Cobertura: 161 161 / 168 777 Zeilen;
Anlage `VideoWebPlayer.Tests/TestResults/b8a86187-90ae-4c2f-8a10-93ce33cd0cd7/coverage.cobertura.xml`)

Tabelle nur für in diesem Branch neue/geänderte Quelldateien < 80 %:

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/LiveStreamWorker.cs` (geändert) | 63,0 % |
| `VideoWebPlayer/Services/SegmenterProcessSupport.cs` (neu) | 70,9 % |
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (geändert) | 76,3 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` (geändert) | 77,3 % |
| `VideoWebPlayer/Migrations/20261026154346_AddSetupLiveStreamStandbyMediaItem.cs` (neu) | 58,3 % (nur `Down()` ungetestet, üblich) |

Zum Vergleich, übrige geänderte/neue Dateien ≥ 80 %: `LiveStreamStandbyProvider.cs` 91,1 %,
`LiveStreamStandbySegmenter.cs` 84,8 %, `LiveStreamController.cs` 92,2 %,
`ProgramSettingsService.cs` 88,5 %, `ProgramClipGroupService.cs` 96,5 %,
`IProgramClipGroupService.cs` 80,0 %, `FfmpegLiveStreamSegmenter.cs` 93,6 %,
`ServiceCollectionExtensions.cs` 93,7 %, `LiveTv.razor` 89,1 %, `Setup.cs` 100 %,
`MediaItemClipEligibility.cs` 100 %, `ApplicationDbContextModelSnapshot.cs` 100 %,
Migrations-Designer 100 %.

Erläuterungen zu den < 80 %-Dateien:

- `LiveStreamWorker.cs`: die Restabdeckung ist überwiegend historischer, in diesem Branch
  unveränderter Code; die branch-neue Änderung (Reihenfolge `ClearAsync` vor
  `Task.Delay(_initialDelay)`) ist über `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay`
  abgedeckt.
- `SegmenterProcessSupport.cs` (neu, Prozess-Helfer für beide Segmenter): ungedeckt sind v. a.
  Fehlerpfade in `PumpInputAsync` (59,1 %) und `DrainStandardErrorAsync` (76,5 %) —
  Stderr-Drain-/Pipe-Fehlerfälle, die nur indirekt über die Segmenter-Tests laufen.
- `TVSenderAdmin.razor`: ungedeckt u. a. `DebouncedReloadStandbyCandidatesAsync` (0 %,
  Closure über Debounce-Timer — im E2E `Admin_StandbyClip_PickAndPersist` über echte
  UI-Interaktion abgedeckt) sowie historischer Bestand (`CleanupDisplacedAsync`,
  `ResetScheduleAsync`, `ReloadGenreOptionsAsync`).
- `VideoWebPlayerBackupData.cs`: Restabdeckung überwiegend historischer Restore-Code;
  die branch-neue Spalte ist über
  `ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull` abgedeckt.

## Fehlende Tests

Ansatz: Coverage-Report je in diesem Branch neuer/geänderter Quelldatei plus
Dateinamen-Konvention (`*Tests.cs` im Testprojekt) — nur branch-bezogen, nicht
historisch-projektweit.

Keine branch-neue/geänderte Quelldatei hat 0 % Zeilenabdeckung — es fehlt keine
Testdatei vollständig. Eingeschränkte Detailabdeckung:

- `VideoWebPlayer/Services/SegmenterProcessSupport.cs` — keine eigene Testdatei
  (`*Tests.cs` fehlt); 70,9 % indirekt über `LiveStreamStandbySegmenterTests`/
  `FfmpegLiveStreamSegmenterE2ETests`. Ungedeckt sind nur Stderr-Drain-/Pump-Fehlerpfade.
- `VideoWebPlayer/Services/MediaItemClipEligibility.cs` — keine eigene Testdatei;
  100 % indirekt über `ProgramClipGroupServiceTests` und
  `ProgramSettingsServiceLiveStreamTests.UpdateLiveStreamStandbyClipAsync_Invalid_Throws`.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Methode
  `DebouncedReloadStandbyCandidatesAsync` 0 % im Coverage (Debounce-Closure);
  im E2E `Admin_StandbyClip_PickAndPersist` über echte UI-Eingabe abgedeckt, dessen
  Abdeckung nicht im Report enthalten ist.
