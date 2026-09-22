# UI-Komponenten – Bestandsaufnahme

## `MediaSourceAdminDetails.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` (Windows-1252-kodiert)

Routen: `/admin/mediasources/new`, `/admin/mediasources/{Id:long}`; `@rendermode InteractiveServer`.

- `EditForm` auf `MediaSource` (`editSource`) mit `DataAnnotationsValidator`/`ValidationSummary`.
- Formularfelder (immer sichtbar, keine Typauswahl): `Name`, `Icon` (`InputFile` + Vorschau über `/api/sourceicons/{id}`), `Host`, `Port` (`InputNumber`), `Path`, `Username`, `Password` sowie Checkbox-Liste „Freigegebene Benutzer".
- `IsNew`-Zweig: `new MediaSource { Port = 22 }` (Zeile ~168).
- Beim Bearbeiten wird `editSource` aus dem geladenen Datensatz neu befüllt (Zeilen ~177–193): `Id`, `Name`, `Host`, `Port`, `Path`, `Username`, `Password`, `IconPictureId`, `CreatedAt`, `LastScannedAt`, `MediaSourceUsers`.
- `Save()`: Icon-Upload in `MediaSourceIcons`, dann `DbContext.AddMediaSourceAsync` (Zeile 286) bzw. `DbContext.UpdateMediaSourceAsync` (Zeile 299), danach `MediaSourceUsers`-Zuordnung komplett neu. **Keine Pfadvalidierung** (z. B. `Directory.Exists`) vorhanden.
- `Delete()` über `Client.DeleteSourceAsync` (→ `AdminSourcesController`/`SourcesController`), `OpenExplorer()` navigiert zu `/admin/mediasources/{Id}/explorer`.
- Kein Quelltyp-/Typ-Auswahlfeld vorhanden.

## `MediaSourceAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`

Route: `/admin/mediasources`.

- Übersichtstabelle mit Spalten: `Icon`, `Name`, `Host`, `Port`, `Pfad`, `Benutzername`, `Aktionen` — keine Typ-Spalte.
- Aktionen pro Zeile: `Löschen` (via `Client.DeleteSourceAsync`), `Scan zurücksetzen` (`source.LastScannedAt = DateTime.MinValue` + `UpdateMediaSourceAsync`, Zeile ~194).
- `RunFullScan` löst Komplettscan über injizierte `MediaSourceScanner`/`MediaSourceClassifier` aus (Scan aller Quellen + inkrementelle Collection-Scans + Klassifizierung, mit `IBackgroundProcessingGate`).

## `MediaSourceExplorer.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor`

Route: `/admin/mediasources/{SourceId:long}/explorer`.

- Arbeitet ausschließlich auf bereits gescannten DB-Einträgen (`DbContext.MediaCollections`/`MediaItems`), Breadcrumb-Navigation entlang `ParentMediaCollectionId`.
- `RescanCurrent` (Zeile ~260): `MediaSourceScanner.ScanCollectionTreeAsync(currentCollection.Id)` + `MediaSourceClassifier.ClassifyCollectionTreeAsync(currentCollection.Id)` — indirekt vom Quelltyp abhängig, aber kein direkter Reader-Zugriff.

## Benutzerseitige Anzeige

- `MediaSourceDetailsViewModel` (`VideoWebPlayer/ViewModels/MediaSourceDetailsViewModel.cs`) lädt `DtoMediaSource` über `VideoWebPlayerClient.RequestSourceAsync` — kein Quelltyp im DTO.
- `NavMenu.razor` (Zeile 98) listet `DtoMediaSource`-Quellen für den Quellenwechsel.
