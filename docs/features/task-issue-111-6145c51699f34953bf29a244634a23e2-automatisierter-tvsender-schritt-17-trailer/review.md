# Plan-Review (selbstkritisch)

## Ergebnis

**Status:** Plan eingehalten, keine Abweichungen

## Checkliste

- Alle Plan-Schritte umgesetzt: `MovieMediaItem.IsTrailer` + Migration `AddMovieMediaItemIsTrailer`,
  `OptionalRestoreColumns`/`OptionalRestoreBoolDefaults`, Enums (`Trailer=5`/`Trailer=3`),
  `ProgramScheduleOptions` + DI + appsettings, Classifier-Erkennung (Suffix + Ordner) +
  `!IsTrailer`-Filter an allen Hauptdatei-Konsumenten, verzögerte Trailer-Expansion in
  `EnsureCoverageAsync`, Vorlauf-Check in `FillGaps`, `GetAllowedMaterialTypes(Filler)`,
  `TVSenderAdmin.razor`-Checkbox, Tests, Doku.
- Abweichungen: `ExpandMaterialAsync` erhielt eine Überladung mit Airings-Map statt geänderter
  Signatur — kosmetisch. Die Ausstrahlungskarte schließt Trailer-MediaItems aus, wie geplant.
- In der Umsetzung gefundene und behobene Fehler:
  1. Erste Version der Ordner-Verknüpfung rief `LinkTrailerMediaItemsAsync` mit der
     Eltern-Collection auf → falscher Zweig. Behoben via `EnsureTrailerLinkAsync` direkt.
  2. Test `IsNeverRepresentativeMediaItem`: Pfad enthielt `-trailer` im Temp-Ordnernamen →
     Assertion auf Dateinamen korrigiert.
  3. Bestehender Test `GetAllowedMaterialTypes_ReturnsExpectedSets` pinnte die alte Menge →
     um `Trailer` erweitert (6 statt 5 für Repeat/HolidaySpecial).
