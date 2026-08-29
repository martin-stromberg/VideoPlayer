# Titelliste und Datenfluss

## ViewModel

`VideoWebPlayer/ViewModels/MediaSourceDetailsViewModel.cs:19-30` speichert Quelle, Genres, `Entries`, Paging, Suchtext und Genre-Filter. Das ViewModel ist in `ServiceCollectionExtensions.cs:265` scoped registriert.

`InitializeAsync` laedt Authentifizierungsstatus, Quelle und quellenbezogene Genres (`MediaSourceDetailsViewModel.cs:34-58`), setzt aber weder `Entries` zurueck noch `Page`, `SearchText` oder `SelectedGenreId`. `ResetEntries` leert nur `Entries` und setzt `Page` auf 0 (`:60-64`).

`LoadNextPageAsync` fragt ueber den Client mit `sourceId`, `Page`, `PageSize`, `SearchText` und `SelectedGenreId` die API ab und haengt neue Eintraege an (`:68-96`). Die API-Adresse ist `GET /api/items?mediaSourceId=...` (`VideoWebPlayer.Client/VideoWebPlayerClient.cs:332-341`).

## Rendering und Nachladen

Die Seite rendert jedes ViewModel-Element (`MediaSourceDetails.razor:183-207`). Der sichtbare Titel wird in `.media-title-text` ausgegeben (`:200-202`). Die erste Seite wird in `OnAfterRenderAsync` nur fuer `firstRender` nachgeladen (`:73-82`). Weitere Seiten werden ueber den Intersection-Observer und `OnBottomVisible` geladen (`:41-66`).

Suche und Genrewechsel rufen `Vm.ResetEntries()` auf und laden danach erneut (`MediaSourceDetails.razor:108-129`). Dieser Reset-Pfad wird beim Quellenwechsel derzeit nicht verwendet.

## Technische Risiken fuer die Umsetzung

- Alte `Entries` koennen beim Wechsel sichtbar bleiben, weil die Liste eine mutable, nicht ersetzte Sammlung ist.
- Ein alter `Page`-Wert kann dazu fuehren, dass die zweite Quelle nicht bei Seite 0 beginnt.
- Suchtext und Genre koennen aus Quelle 1 in Quelle 2 weiterwirken, obwohl Genres quellenabhaengig neu geladen werden.
- Ein bestehender Intersection-Observer und asynchrone Requests koennen nach dem Parameterwechsel noch gegen den alten Zustand laufen; die Planung muss Race- und Stale-Request-Verhalten beruecksichtigen.
- Die gespeicherte Auswahl in `sessionStorage` (`selectedMediaItem`) und die gemeinsame Scrollposition sind quellenuebergreifend und muessen beim Wechsel auf unerwuenschte Nebeneffekte geprueft werden.
