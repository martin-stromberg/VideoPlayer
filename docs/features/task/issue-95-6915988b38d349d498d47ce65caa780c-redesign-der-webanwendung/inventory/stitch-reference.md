# Stitch-Referenz und Designsystem

## Enthaltene Referenzen

`stitch_private_media_library.zip` enthaelt:

- `cinepriv_dashboard/code.html` und `screen.png`
- `cinepriv_serien/code.html` und `screen.png`
- `cinepriv_filme/code.html` und `screen.png`
- `cinepriv_detailansicht/code.html` und `screen.png`
- `video_player_seriendetails/code.html` und `screen.png`
- `cinematic_noir/DESIGN.md`

Die HTML-Dateien sind eigenstaendige Stitch-Ausgaben und keine direkt integrierten Blazor-Komponenten. Die Screenshots sind fuer die visuelle Abnahme relevant; die HTML-Struktur ist nur als Referenz fuer Komposition und Inhalte zu verwenden.

## Designsystem aus DESIGN.md

| Kategorie | Vorgabe |
|---|---|
| Grundflaeche | Deep charcoal, insbesondere `#131313` bzw. `#121212` |
| Primaerakzent | Rot, `#E50914` / `#ffb4aa` je nach Tokenebene |
| Sekundaerakzent | Blau, `#00A8E8` bzw. `#03a8e8` |
| Text | Helle Flaechentexte auf dunklem Grund, reduzierte Sekundaertexte |
| Display-Schrift | Montserrat, schwere Headlines |
| UI-/Body-Schrift | Inter |
| Raster | Desktop 12 Spalten, Mobile 4 Spalten; Poster typischerweise 2:3, Episoden 16:9 |
| Rhythmus | 8-Pixel-Skala, Desktop-Gutter 24 px, mobile Seitenabstand 20 px |
| Flaechen | Tonale Ebenen und dezente 1-Pixel-Rahmen statt starker Schatten |
| Overlays | Dunkle, transluzente Flaechen mit etwa 20-32 px Blur |
| Ecken | Kleine Radien; Karten bis etwa 8 px, Filter-Chips als Ausnahme pillenfoermig |
| Interaktion | Karten-Hover mit moderater Skalierung, Quick-Play und Metadaten-Overlay; rote aktive Navigation |

## Abnahmerelevante Ansichtszustaende

Fuer jede umgesetzte Hauptansicht muessen mindestens Standard-, Hover-, Fokus-, deaktivierter, Lade- und Fehlerzustand geprueft werden. Bei Medienkarten kommen Bildfehler, fehlende Metadaten und lange Titel hinzu. Bei Navigation und Admin-Flaechen sind angemeldete, nicht angemeldete und rollenbeschraenkte Zustaende relevant.

## Nutzungsgrenzen

Die visuelle Referenz definiert kein vollstaendiges Accessibility- oder Browser-Zielprofil. Die vorhandenen Semantik-, Tastatur- und Fokusanforderungen muessen deshalb aus der bestehenden Anwendung abgeleitet und im Plan explizit als Testumfang festgelegt werden.
