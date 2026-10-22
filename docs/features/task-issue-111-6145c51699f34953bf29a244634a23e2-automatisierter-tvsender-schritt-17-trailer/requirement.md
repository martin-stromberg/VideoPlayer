# Übersetzte Anforderung: Trailer als Vorlauf-Füllmaterial zu eingeplanten Filmen (N11)

## Fachliche Zusammenfassung

Der automatisierte TV-Sender soll Film-Trailer als Vorlauf-Füllmaterial nutzen: Liegt zu einem
Film eine Trailer-Datei vor (Plex/Jellyfin-Dateikonvention), steht sie in den Tagen vor der
Ausstrahlung des verknüpften Films als Füllmaterial zur Verfügung. Dazu muss der Scan/Classifier
Trailer-Dateien erkennen und die Beziehung Trailer↔Film persistieren, die Füller-Auswahl des
`ProgramScheduleService` erhält eine neue Materialart „Trailer anstehender Filme", und der
Regel-Editor in `TVSenderAdmin.razor` bietet diese Materialart an. Trailer-Dateien dürfen
niemals als reguläre Filme im Sendeplan oder in Genre-Expansionen auftauchen; ihre
Laufzeitermittlung läuft über den bestehenden Duration-Probe-Pfad.

## Akzeptanzkriterien

1. **Trailer-Erkennung beim Scan/Classifier:** Eine Videodatei mit `-trailer`-Suffix neben der
   Filmdatei (`Film.mp4` → `Film-trailer.mp4`) sowie Videodateien in einem `trailers/`-Unterordner
   werden als Trailer erkannt und über eine persistierte Beziehung dem Film zugeordnet.
2. **Kein regulärer Inhalt:** Trailer-Items erzeugen keinen `Movie`-Datensatz, werden nicht als
   Hauptdatei eines Films (Wiedergabe, repräsentatives `MovieMediaItem`, Genre-Expansion) gewählt
   und landen nicht als eigenständiger Film im Sendeplan.
3. **Duration-Probing:** Trailer-Items durchlaufen die bestehende Laufzeitermittlung wie jede
   andere `MediaItem`-Videodatei.
4. **Füller-Bevorzugung:** Eine neue Materialart `Trailer` („Trailer anstehender Filme") ist für
   Füller-Regeln wählbar. Bei der Lückenfüllung werden Trailer bevorzugt, deren verknüpfter Film
   innerhalb der nächsten `ProgramSchedule:TrailerLeadDays` Tage (Default 7) im Sendeplan steht.
   Passen keine Trailer, greifen die übrigen Füller-Regeln (Fallback auf normales Füllmaterial).
5. **UI:** Die Materialart ist im Regel-Editor auswählbar (konsistent zu den bestehenden
   Genre-/Playlist-Auswahlelementen), wird in der Regel-Liste und im Sendeplan sinnvoll dargestellt.
6. **Persistenz/Migration:** Neue Spalte in `MovieMediaItems` mit EF-Migration; Alt-Backups ohne
   die Spalte bleiben über `OptionalRestoreColumns`/`OptionalRestoreBoolDefaults` wiederherstellbar
   (Regressionstest).
7. **Dokumentation:** `docs/help/tv-sender.md`, `RELEASE_NOTES.md` (EN+DE) und `changes.log`
   werden aktualisiert.

## Betroffene Klassen und Komponenten

- **Datenmodell:** `MovieMediaItem` (neues Flag `IsTrailer`), `Movie`, `MediaItem`, `MediaCollection`,
  `ProgramSlot` (Trailer-Slots tragen `MovieId` für die Titelauflösung), `ProgramRuleCursor` /
  `ProgramCursorContentType` (neuer Inhaltstyp `Trailer`).
- **Enums:** `ProgramRuleMaterialType` (neuer Wert `Trailer`), `ProgramCursorContentType`
  (neuer Wert `Trailer`).
- **Logik/Services:** `MediaSourceClassifier` (`ProcessCollectionAsMovieAsync`,
  `ProcessEpisodesForTVShowAsync`, `BackfillMissingActorsAsync`, `AssignPicturesToMovieAsync`),
  `ProgramScheduleService` (`ExpandMaterialAsync`, `FillGaps`, `MovieMediaItemsAsync`,
  `EnsureCoverageAsync`, `ReconcileAsync`, `GetRuleDiagnosesAsync`), `ProgramRuleService`
  (`GetAllowedMaterialTypes`, `ValidateRuleInputAsync`), `ItemsController`
  (`FindMediaItemAsync` für `movie`).
- **Konfiguration:** neue Optionsklasse `ProgramScheduleOptions` (Abschnitt `ProgramSchedule`,
  Schlüssel `TrailerLeadDays`, Default 7), Registrierung in `ServiceCollectionExtensions`,
  `appsettings.json`.
- **UI:** `TVSenderAdmin.razor` (Materialauswahl im Regel-Editor, `MaterialLabel`).
- **Persistenz:** EF-Migration für `MovieMediaItems.IsTrailer`,
  `VideoWebPlayerBackupData` (`OptionalRestoreColumns`, `OptionalRestoreBoolDefaults`).
- **Tests:** `ProgramScheduleServiceTests_*` (neu: Trailer-Füller), `MediaSourceClassifier`-Tests
  (neu: Trailer-Erkennung), `VideoWebPlayerBackupDataTests` (Alt-Backup-Regression),
  `ProgramRuleServiceTests_Validation` (Materialart-Zulässigkeit), `TVSenderAdminTests`/bUnit
  (Materialart wählbar), ggf. `ApplicationDbContextTests` (Migration/Spalte).

## Implementierungsansatz

- Trailer↔Film-Beziehung über das vorhandene Join `MovieMediaItem` plus Flag `IsTrailer`
  (kein eigenes Entity nötig; mehrere Trailer pro Film möglich; Trailer bleibt reguläres
  `MediaItem` → Duration-Probing automatisch).
- Erkennung in `ProcessCollectionAsMovieAsync`: `-trailer`-Suffix → Link zum Film des
  gleichnamigen Stamms in derselben Collection; `trailers/`-Unterordner → Link zu den Filmen der
  Eltern-Collection. In beiden Fällen keine Movie-Klassifizierung der Trailer-Datei; gleiche
  Ausnahme im Episoden-Pfad (`ProcessEpisodesForTVShowAsync`).
- Neue Materialart `ProgramRuleMaterialType.Trailer` (kein `MaterialId`-Bezug — globales
  Material „alle Trailer anstehender Filme"). Expansion nach dem Materialisieren der
  Regel-Ausstrahlungen: Trailer, deren Film einen geplanten `ProgramSlot` im bedeckten Fenster hat,
  sortiert nach nächster Ausstrahlung. In `FillGaps` pro Lückenposition der erste Trailer, dessen
  Film innerhalb `TrailerLeadDays` nach der Position startet; sonst Fallback auf die anderen
  Füller-Regeln.
- Serverseitige Absicherung bleibt im `ProgramRuleService` (`RequireAdminAsync`, Validierung).

## Konfiguration

`ProgramSchedule:TrailerLeadDays` (int, Tage, Default 7) als IOptions-Abschnitt analog
`MediaProbe:`/`Playlists:` — Betreiber-Einstellung ohne DB-Migration.

## Offene Fragen

Keine — Konvention (`-trailer`-Suffix, `trailers/`-Ordner), Vorlauflogik und Fallback sind in der
Anforderung und in N11 hinreichend bestimmt; die Entscheidung „`MovieMediaItem.IsTrailer` statt
eigenem Trailer-Entity" folgt dem vorhandenen Datenmodell (siehe Plan).
