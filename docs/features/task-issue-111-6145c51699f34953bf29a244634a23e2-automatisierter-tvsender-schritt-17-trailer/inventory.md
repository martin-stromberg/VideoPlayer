# Bestandsaufnahme: Trailer als Vorlauf-Füllmaterial (N11)

Analyse des Bestands rund um Scan/Klassifizierung (`MediaSourceScanner`/`MediaSourceClassifier`),
Programmplanung (`ProgramScheduleService`/`ProgramRuleService`) und Regel-Editor (`TVSenderAdmin.razor`)
bezogen auf die Anforderung „Trailer als Vorlauf-Füllmaterial zu eingeplanten Filmen".

## Zusammenfassung

- **Kein Trailer-Begriff im Produktcode** (`grep -rni "trailer"` ohne Treffer — bestätigt N11).
- Trailer-Dateien werden vom inhaltsagnostischen `MediaSourceScanner` bereits als `MediaItem`
  persistiert → **Duration-Probing gilt automatisch**; die Erkennung + Verknüpfung gehört in den
  `MediaSourceClassifier` (`ProcessCollectionAsMovieAsync`, ggf. `ProcessEpisodesForTVShowAsync`).
- Eine `-trailer`-Datei wird aktuell nur dann zum `Movie`, wenn ein eigenes NFO mit `<movie>`
  vorliegt — der Schutz „kein regulärer Film" muss aktiv ergänzt werden.
- `MovieMediaItem` (PK `MovieId`+`MediaItemId`) ist der natürliche Ort für die Beziehung;
  ein Flag `IsTrailer` reicht — Konsumenten der **Hauptdatei** müssen gefiltert werden:
  `ProgramScheduleService.MovieMediaItemsAsync`, `ItemsController.FindMediaItemAsync`,
  `MediaSourceClassifier.BackfillMissingActorsAsync` + `AssignPicturesToMovieAsync`.
- `ExpandMaterialAsync` läuft vor der Materialisierung — Trailer-Expansion muss verzögert werden,
  bis neue Film-Slots materialisiert sind (`EnsureCoverageAsync`; `ReconcileAsync` nutzt
  persistierte Slots).
- `GetAllowedMaterialTypes(Filler)` steuert UI + serverseitige Validierung;
  `Repeat`/`HolidaySpecial` nutzen `Enum.GetValues` (neue Materialart automatisch enthalten).
- `VideoWebPlayerBackupData` hat etablierte `OptionalRestore*`/Defaults-Mechanismen +
  `LegacyBackupArchiveBuilder`-Testmuster.
- Test-Ausgangszustand: `dotnet test` grün — **2098 bestanden, 0 fehlgeschlagen, 1 übersprungen**
  (bekannter Alt-Skip), siehe [tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
