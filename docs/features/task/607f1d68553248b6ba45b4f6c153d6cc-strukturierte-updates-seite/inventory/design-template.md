# Stitch-Vorlage und UI-Abgleich

Quelle: `stitch_private_media_library-updates.zip` mit `code.html`, `screen.png` und `DESIGN.md`.

## Vorlage

Der Entwurf verwendet eine dunkle, kontrastreiche Oberflaeche mit nahezu schwarzem Hintergrund, roten Primaeraktionen und blauen Informationsakzenten. Die Inhalte sind in drei sichtbare Bereiche gegliedert:

1. `Update Status`: aktueller Zustand, Versionshinweis, letzter Pruefzeitpunkt und Aktionen.
2. `Version Details`: installierte/verfuegbare Version, Prerelease-Kanal, Release-Datum und letzter Download.
3. `Configuration`: zweispaltig `Automation` sowie `Safety & Backups` mit Schaltern, Eingabefeldern und Reset-/Speicheraktionen.

Desktop nutzt eine breite Status-/Detail-Anordnung; auf kleinen Viewports werden die Inhalte untereinander angeordnet. Die Vorlage nutzt Glasflaechen, dezente Umrandungen, kleine Radien, Montserrat fuer Ueberschriften und Inter fuer Bedien-/Metadaten.

## Abgleich mit dem Bestand

- Die Statusdaten sind bereits als `AutoUpdateStatusSnapshot` verfuegbar.
- Installierte Version, verfuegbare Version, Prerelease-Status, Release-Datum und Downloadzustand werden aktuell bereits in `Updates.razor` ausgegeben, allerdings als einfache Bootstrap-Tabelle.
- Check-, Install- und Refresh-Aktionen existieren bereits, mit deutscher Beschriftung und unterschiedlicher Interaktionsform.
- Die Konfigurationsfelder existieren fast vollstaendig und sind an die Persistenz angebunden.
- `Reset Defaults` fehlt.
- Die bestehende App verwendet globale Admin-CSS-Klassen und Layouts; das Stitch-HTML ist ein eigenstaendiger Tailwind-Prototyp mit CDN- und Google-Font-Abhaengigkeiten. Diese externen Abhaengigkeiten sind laut Anforderung nicht einzufuehren.

## Umsetzungsrelevante Hinweise

Die Vorlage ist als visuelle Referenz zu behandeln, nicht als direkt uebernehmbares HTML. Die neue Komponente muss bestehende Authentifizierung, Antiforgery-Formulare, Blazor-Bindings und die globale responsive CSS-Strategie weiterverwenden. Disabled-Zustaende, Tastaturfokus, Fehler-/Leerzustand und mobile Umbrueche sind explizit zu erhalten.
