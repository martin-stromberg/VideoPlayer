# Quellenwechsel und Menue

## Navigationsmenue

`VideoWebPlayer/Components/Layout/NavMenu.razor:26-40` rendert fuer authentifizierte Benutzer die vom Client geladenen Quellen. Jede Quelle wird als `NavLink` mit `href="/mediasource/{source.Id}"` gerendert. Das Menue verwendet damit dieselbe routbare Komponente fuer alle Quellen; es gibt keinen separaten Vollseitenwechsel und keinen expliziten Reload-Hook.

Die Quellenliste wird in `NavMenu.OnInitializedAsync` einmal ueber `Client.RequestSourcesAsync()` geladen (`NavMenu.razor:92-97`). Das ist fuer den Wechsel vorhandener Quellen nicht der problematische Zustand; relevant ist die Zielkomponente.

## Zielroute

`VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor:1` definiert `/mediasource/{Id:long}`. `Id` ist ein routengebundener Parameter (`:36-37`). Die Seite verwendet `@rendermode InteractiveServer` und injiziert ein scoped `MediaSourceDetailsViewModel` (`:4-7`).

## Parameterwechsel

Aktuell wird nur `OnInitializedAsync` verwendet (`MediaSourceDetails.razor:68-71`). Ein Wechsel von `/mediasource/1` auf `/mediasource/2` innerhalb derselben Komponente kann daher den Parameter aktualisieren, ohne diesen Initialisierungscode erneut auszufuehren. `OnAfterRenderAsync` laedt den ersten Chunk ebenfalls nur bei `firstRender` (`:73-92`).

## API-Berechtigung

`SourcesController.GetSources` ermittelt die fuer den Benutzer sichtbaren Quellen (`SourcesController.cs:36-60`). `GetSource` prueft den Zugriff und liefert die einzelne Quelle (`SourcesController.cs:78-99`). Die API-Seite ist damit quellenbezogen; der Befund liegt voraussichtlich in der UI-Lebensdauer und nicht in der Quellenverwaltung.
