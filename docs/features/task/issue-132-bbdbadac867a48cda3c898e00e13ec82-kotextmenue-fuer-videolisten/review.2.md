# Plan-Review: Kontextmenü für Videolisten

## Status

Offene Aufgaben vorhanden

## Prüfgrundlage

Der Workspace nach Iteration 2 wurde gegen `plan.md`, `inventory.md`, die Detaildokumente sowie `review.1.md` und `review-code.1.md` geprüft. Zusätzlich wurde `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore` ausgeführt.

Ergebnis des Testlaufs: 183 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen. Es bleiben `NU1903`-Warnungen zu den bereits bekannten Paketversionen `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 und `SSH.NET` 2025.1.0.

## Erledigte Punkte aus Iteration 1

- `MediaBox.razor` fokussiert nach dem Öffnen die erste Aktion und stellt beim Schließen den Kartenlink wieder her. Escape wird am Menücontainer behandelt.
- Das native `contextmenu`-Event unterdrückt lediglich das Browsermenü und öffnet das Aktionsmenü nicht vor Ablauf der Long-Press-Zeit. Tastaturzugriff über `ContextMenu` und `Shift+F10` bleibt separat vorhanden.
- `RequestContinueWatchingAsync()` verschluckt Fehler nicht mehr. `ContinueWatchingList.LoadItemsAsync()` behält den bisherigen Zustand bei und veröffentlicht eine Fehlermeldung, wenn der Reload fehlschlägt.
- Neue Tests prüfen Teile der Zustandslogik, Fokus-/Escape-Verkabelung, die Nicht-Aktivierung von `RecentEntriesList` sowie die Serveraktionen weiterhin erfolgreich.

## Offene Aufgaben

1. **Overlayposition an Listen- und Viewporträndern ist nicht dynamisch korrigiert.**

   `VideoWebPlayer/wwwroot/app.css:1048-1066` positioniert das Menü ausschließlich absolut am unteren rechten Rand der Karte. `max-width` und `max-height` begrenzen das Menü innerhalb der Karte, messen aber weder den Viewport noch den sichtbaren Listenbereich und verschieben es nicht nach oben oder links. Damit ist die im Plan geforderte Randkorrektur für erste/letzte Karten und schmale mobile Viewports nicht nachgewiesen. Die Liste blendet vertikalen Überstand mit `overflow-y: hidden` aus (`app.css:876-887`), was bei einer erforderlichen Position außerhalb der Kartenfläche problematisch bleibt.

   Es fehlen außerdem ein Browser-/Komponententest oder eine gleichwertige Positionsprüfung für erste und letzte Karte bei Desktop- und mobilem Viewport.

2. **Die verbindlichen Pointer-/Touch-Abnahmetests fehlen weiterhin.**

   `VideoWebPlayer.Tests/Components/MediaBoxInteractionTests.cs` prüft Quelltextverkabelung und `MediaContextMenuInteractionState`, simuliert aber keine gerenderten Blazor-Ereignisse beziehungsweise Browserinteraktion. Damit sind exakt 3 Sekunden, Loslassen vor Ablauf, Pointer-Cancel, Bewegung über 10 px, normale Linknavigation, Klick außerhalb, Menüaktion, Escape, natives `contextmenu` und die responsive Randposition nicht als Verhalten abgesichert. Die Planforderung aus Schritt 5 und das Akzeptanzkriterium für Scrollen und Navigation sind daher noch nicht vollständig nachgewiesen.

   Mindestens ein echter Komponenten- oder Browsertest sollte die Pointer-Ereignisse mit Zeitsteuerung auslösen; zusätzlich ist die Negativprüfung für `RecentEntriesList` und die mobile Overlayposition beizubehalten.

## Umgesetzte Planpunkte

- `ContinueWatchingEntry.ListOrder`, Migration, Index und Initialbelegung sind vorhanden.
- Ausblenden und Überspringen sind benutzerbezogen; Episoden- und Filmpfade, fehlendes Folgemedium und Positionsübernahme werden durch Service-Tests abgedeckt.
- Favoriten-Entfernen verwendet die Persistenz-ID und unterstützt die fünf vorgesehenen Favoritentypen.
- `MediaBox` ist nur in Continue Watching und Favoriten aktiviert; `RecentEntriesList` bleibt ohne Aktionen.
- Fokus-, Escape-, Pointer-Abbruch- und natives `contextmenu`-Verhalten wurden gegenüber Iteration 1 nachgebessert.
- Reload-Fehler beim Continue-Watching werden sichtbar behandelt, ohne den bekannten Zustand als leere, erfolgreiche Liste zu ersetzen.
- Mutationen verwenden die vorhandene Client-Kapselung und die bestehende Status-/Benachrichtigungsinfrastruktur.

## Restliches Risiko

Die Server- und Persistenzpfade sowie der Build sind grün. Ohne echte Pointer-/Responsive-Abnahme kann jedoch nicht belastbar bestätigt werden, dass Long-Press, Scroll-Abbruch, normale Navigation und die Overlaybedienbarkeit auf allen unterstützten Bildschirmgrößen zusammen funktionieren. Der Plan ist deshalb noch nicht vollständig umgesetzt.
