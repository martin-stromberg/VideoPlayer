# Bestandsaufnahme: Genre-Material — Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3)

Analysiert wurde der Bestandscode rund um `ProgramRule`-Material vom Typ `Genre`: Datenmodell, Regelverwaltung, Sendeplan-Expansion, Genre-Sichtbarkeit/Alternativnamen sowie die Admin-UI.

## Zusammenfassung

- `ProgramRule.MaterialId` referenziert genau eine `Genre`-Zeile; Genre-Zeilen sind quellbezogen (`Genre.MediaSourceId`) und werden in Auswahllisten quellenübergreifend nach `Name` dedupliziert (`MediaMetadataEditorService.GetGenreOptionsAsync`, `GenreAdmin.ReloadGenres`).
- `ExpandGenreAsync` in `ProgramScheduleService` expandiert genau ein Genre über `MovieGenre`/`TVShowGenre`-Links (Filme nach Release-Datum, Episoden der verlinkten Serien) — ohne Mehrfachauswahl, ohne Quell-Filter, ohne Alternativnamen-Auflösung.
- Sichtbarkeitsregeln existieren bereits, werden aber uneinheitlich angewendet: `SourceGenresController` filtert `!IsHidden`; `GenreService.GetSeasonalGenresAsync` implementiert das saisonale `StartDate`/`EndDate`-Fenster (jährlich wiederkehrend); `MediaSourceClassifier.GetOrCreateGenresAsync` lenkt beim Scan über `GenreName`-Alternativnamen auf kanonische Genres um — `GetGenreOptionsAsync` ignoriert all das und gruppiert nur nach Roh-`Name`.
- Für n:m-Beziehungen existiert ein etabliertes Muster (`MovieGenre`, `TVShowGenre`, `PlaylistGenre`): eigenständige Entitäten, Composite-PK oder FKs mit Kaskade. Neue Tabellen werden in `VideoWebPlayerBackupData.OptionalRestoreTables` registriert; `LegacyBackupArchiveBuilder` stellt den Alt-Backup-Regressionstest bereit.
- Die Admin-Seite `TVSenderAdmin.razor` nutzt `rule-material-genre` als Einfach-`<select>`; Mehrfachauswahl-UI muss neu gebaut werden (Vorlage: `admin-chip`-/Checkbox-Muster).

Test-Ausgangszustand: volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — **1975 erfolgreich, 0 fehlgeschlagen, 1 übersprungen** (bekannter Alt-Skip `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`), Debug-Build 0 Fehler. Nachweis: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
