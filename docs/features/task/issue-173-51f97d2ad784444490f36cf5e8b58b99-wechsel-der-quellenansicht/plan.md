# Umsetzungsplan: Wechsel der Quellenansicht

## Ziel

Beim Wechsel einer angemeldeten Person zwischen Quellen ueber das bestehende Navigationsmenue soll die wiederverwendete Quellenansicht vollstaendig auf die neue Quelle umschalten. Die Titelliste, Paging- und Filterparameter duerfen keinen Zustand der zuvor geoeffneten Quelle anzeigen oder verwenden.

## Betroffene Komponenten

- `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor`
- `VideoWebPlayer/ViewModels/MediaSourceDetailsViewModel.cs`
- `VideoWebPlayer.Tests/` mit einem Playwright-E2E-Test fuer den Quellenwechsel

`NavMenu.razor`, Login, Quellenverwaltung und die quellenbezogenen API-Endpunkte bleiben unveraendert, sofern die Umsetzung keinen konkreten Fehler in deren Vertrag aufdeckt.

## Umsetzung

1. Einen expliziten Parameterwechselpfad in `MediaSourceDetails.razor` einfuehren, der bei einer Aenderung von `Id` die Initialisierung der neuen Quelle ausloest. Der Pfad muss sowohl beim ersten Aufruf als auch bei einer Navigation von `/mediasource/{ersteId}` nach `/mediasource/{zweiteId}` korrekt funktionieren.
2. `MediaSourceDetailsViewModel` so erweitern oder den bestehenden Initialisierungspfad so nutzen, dass vor dem Laden der neuen Quelle alle quellenabhaengigen Listen- und Navigationszustaende zurueckgesetzt werden: `Entries`, `Page`, `SearchText` und `SelectedGenreId`. Die Genres der neuen Quelle werden anschliessend neu geladen.
3. Sicherstellen, dass die erste Titelabfrage der neuen Quelle mit `Page = 0` und den zurueckgesetzten Filtern erfolgt und die alte Liste vor dem Rendern der neuen Ergebnisse geleert ist.
4. Den Nachlade- und Intersection-Observer-Lebenszyklus beim Parameterwechsel pruefen. Ein alter Observer oder eine noch laufende alte Abfrage darf keine Titel nachtraeglich in die neue Quellenansicht einfuegen. Falls dafuer erforderlich, Observer sauber neu registrieren und parallele bzw. veraltete Ladevorgaenge anhand der aktuellen Quellen-ID verwerfen.
5. Das bestehende Verhalten fuer Suche, Genrewechsel, initialen Quellenaufruf und Berechtigungspruefung beibehalten und nur gemeinsame Reset-/Ladelogik wiederverwenden, wenn dadurch keine Verhaltensaenderung entsteht.

## E2E-Test: exakter Benutzerfluss

In `VideoWebPlayer.Tests` einen Playwright-Test nach dem Muster von `UnlockedSourceE2ETests` anlegen oder dort ergaenzen. Der Test seedet zwei fuer den Testbenutzer sichtbare Quellen mit unterscheidbaren Titeln, zum Beispiel `Quelle Eins` mit `Titel Quelle Eins` und `Quelle Zwei` mit `Titel Quelle Zwei`.

Der Test muss in dieser Reihenfolge ausfuehren:

1. Testserver und Datenbank starten und den Testbenutzer anmelden.
2. Die erste Quelle ueber ihren `NavLink` im Menue oeffnen, nicht durch direkten URL-Aufruf.
3. Den Seitentitel beziehungsweise das sichtbare `h1` auf `Quelle Eins` pruefen und auf das Laden der Titelliste warten. `Titel Quelle Eins` muss sichtbar sein.
4. Die zweite Quelle ueber ihren `NavLink` im selben Menue oeffnen.
5. Den Seitentitel beziehungsweise das sichtbare `h1` auf `Quelle Zwei` pruefen und auf das Laden der Titelliste warten. `Titel Quelle Zwei` muss sichtbar sein.
6. Explizit nachweisen, dass `Titel Quelle Eins` nach dem Wechsel nicht mehr angezeigt wird, zum Beispiel mit einem Locator fuer `.media-title-text` und `not.toContainText` beziehungsweise einer passenden Locator-Assertion. Die Assertion muss nach dem zweiten Quellenaufbau erfolgen, nicht nur vor dem Wechsel.

Der Test soll die Quellen ueber stabile Menue-Locators anhand ihrer sichtbaren Namen identifizieren, den Wechsel mit `WaitForURLAsync` oder einer gleichwertigen UI-Synchronisation abwarten und fuer die Titel auf sichtbare bzw. geladene `.media-title-text`-Elemente warten. Zusaetzlich sollen unerwartete Seitenfehler und HTTP-Fehler wie in den bestehenden E2E-Tests gesammelt und als Testfehler gemeldet werden.

## Tests und Verifikation

- Den neuen Quellenwechsel-E2E-Test gezielt ausfuehren.
- Die vorhandenen quellenbezogenen E2E- und ViewModel-/Client-Tests ausfuehren.
- Bei Bedarf einen fokussierten ViewModel-Test ergaenzen, der eine wiederholte Initialisierung mit zwei Quellen prueft: Liste, Seite, Suchtext und Genre der ersten Quelle duerfen in der zweiten Initialisierung nicht fortbestehen.
- Sicherstellen, dass die E2E-Abdeckung den Wechsel ausschliesslich ueber das Menue und die Negativassertion fuer den alten Titel enthaelt.

## Akzeptanzkriterien

- Eine angemeldete Person sieht nach dem Menue-Aufruf die Titel der ausgewaehlten Quelle.
- Nach dem Wechsel ueber das Menue werden Quelle und Titelliste auf die zweite Quelle aktualisiert.
- Kein Titel der ersten Quelle bleibt nach dem Wechsel sichtbar.
- Der beschriebene Login-, erster Menue-Aufruf-, Titelpruefung-, zweiter Menue-Aufruf- und Titelpruefung-Fluss ist durch einen erfolgreichen E2E-Test abgedeckt.

## Offene Punkte

Keine.
