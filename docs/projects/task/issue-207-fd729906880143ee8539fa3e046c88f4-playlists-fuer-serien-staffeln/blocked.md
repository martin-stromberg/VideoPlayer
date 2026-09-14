# Blockade – Entwicklungsschritt 6

Erstellt am: 2026-09-14
Branch: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug`
Grund: Maximale Anzahl an Nachbesserungsrunden erreicht

Der Schritt konnte nicht automatisiert abgeschlossen werden. In drei aufeinanderfolgenden
Abnahmeprüfungsrunden wurde jeweils dieselbe Problemklasse (falsche Wiedergabeposition beim
Titelwechsel innerhalb einer Playlist) an einer jeweils tieferliegenden Ursache gefunden:

- Runde 1: Position wurde beim Fortsetzen überhaupt nicht übernommen (0:00-Start) — behoben.
- Runde 2: Die Behebung aus Runde 1 ließ die Position des VORHERIGEN Titels auf Folgetitel
  durchschlagen — teilweise behoben (`IsStillCurrentEntry`-Prüfung ergänzt).
- Runde 3: Die Runde-2-Korrektur schützt nur gegen *out-of-order* eintreffende Antworten, nicht
  gegen das Render-Fenster *während* der noch laufenden Anfrage. `PlaylistDetail.razor` setzt
  `StreamUrl`/`MediaId` synchron beim Eintragswechsel, ermittelt `StartPositionSeconds` aber
  über einen asynchronen `StartPlaylistAsync`-Roundtrip. Blazor rendert bereits im Moment des
  `await`-Rücksprungs neu — `VideoPlayer` erhält dabei die neue Stream-URL, aber noch die alte
  Position, wendet diese sofort an (`_startApplied = true`) und ignoriert die später eintreffende
  korrekte Position.

## Verbleibende Abweichung (Runde 3)

Empirisch verifiziert (temporäre bUnit-Sonde mit verzögerter Server-Antwort):

- Vorheriger Eintrag bei 300 s, Folgetitel ohne eigene Position → Folgetitel startet bei 300 s
  statt bei 0.
- Vorheriger Eintrag bei 300 s, Folgetitel mit eigener Position 450 s → Folgetitel startet bei
  300 s, die eigene Position 450 s wird nie angewendet.

Betroffen: Auto-Advance, „Nächster", „Vorheriger", „Neu starten". Nicht betroffen: das initiale
Laden über den `entryId`-Parameter aus der Weiterschauen-Liste (dort ist die Position bereits vor
dem ersten Render bekannt).

Alltagsfall: Ein Anwender setzt einen Weiterschauen-Eintrag bei 300 s fort, sieht die Episode zu
Ende, Auto-Advance startet die nächste Episode — diese beginnt fälschlich mitten im Video statt
am Anfang. Deutlich sichtbarer Bedienbarkeitsmangel.

Vollständiger Bericht: `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/acceptance-schritt-6.md`
(sowie die archivierten Vorrunden `acceptance-schritt-6.1.md`, `acceptance-schritt-6.2.md`).

## Einschätzung

Die Abnahmeprüfung selbst benennt einen sauberen Lösungsweg: Würden die
Playlist-Navigations-DTOs (`DtoPlaylistNavigationResult`) die Zielposition direkt mitliefern
(statt sie über einen separaten `StartPlaylistAsync`-Roundtrip nachzuladen), stünde die Position
synchron zum Eintragswechsel bereit — sowohl der Zusatz-Roundtrip als auch das Render-Fenster
entfielen. Das ist ein kleiner, lokal begrenzter Fix (analog zur bereits erfolgten Erweiterung von
`DtoPlaylistNavigationResult` um `Position` in einer früheren Runde dieses Projekts), erfordert
aber laut Ablaufregel eine menschliche Entscheidung, da die maximale Anzahl automatisierter
Nachbesserungsrunden erreicht ist.

Der Schritt konnte nicht automatisiert abgeschlossen werden. Bitte klären und anschließend
bestätigen, damit die Abarbeitung fortgesetzt werden kann.
