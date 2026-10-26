# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine. Die Befunde der Vor-Runde (`review-usability.2.md`) sind im geprüften Stand
behoben:

- Verwaiste Auswahl: `LoadStandbyLabelAsync` setzt die Auswahl beim Anzeigen des
  Fallback-Textes still auf „Standard" zurück — der wirksame Zustand folgt dem
  sichtbaren; „Speichern" funktioniert im angezeigten Zustand (bUnit-Test
  `StandbyCard_OrphanedSelection_SaveStillWorks` deckt den Weg ab). Der Hinweis
  „Auswahl wird mit „Speichern" übernommen" macht dabei ehrlich sichtbar, dass der
  gezeigte Standard erst mit „Speichern" persistiert wird.
- Fachjargon: Überschrift „Standby-Clip (Überbrückung)", Hilfetext „Wird gezeigt,
  bis der Live-Stream nach einem Server-Start wirklich läuft — ebenso bei
  Unterbrechungen. Auswählbar sind Videodateien ohne Film-/Serien-Zuordnung." —
  keine Server-Interna mehr („Warm-up", „Wiedergabeliste", „produzierte Segmente",
  „nicht klassifiziert" sind verschwunden).
- Ungespeicherte Auswahl: `#tvsender-standby-pending` („Auswahl wird mit
  „Speichern" übernommen.") erscheint, sobald die angezeigte Wahl vom gespeicherten
  Stand abweicht, und verschwindet nach erfolgreichem Speichern — konsistent zum
  gebündelten Speichern des Einstellungen-Tabs.
- Picker-Toggle: Beschriftung wechselt „Ändern" ↔ „Auswahl schließen",
  `aria-expanded` und `aria-controls` sind gesetzt.
- Lade-Feedback: `#tvsender-standby-loading` („Clips werden geladen…") verhindert
  den Leerzustand-Flash während der ersten Abfrage und jeder Suche/Filterung;
  „Weitere laden" wird während des Nachladens gesperrt und zeigt den Ladetext.

## Geprüfte Interaktionen

- Admin: Tab „Einstellungen" → Standby-Karte finden → unauffällig (Karte
  „Standby-Clip (Überbrückung)" mit anwenderorientiertem Hilfetext)
- Admin: Aktuellen Clip am Chip ablesen („Standard-Clip (Gleich geht's los)" /
  „Name (Quelle)" / „Zuvor gewählter Clip ist nicht mehr vorhanden — Standard-Clip
  wird verwendet") → unauffällig, keine internen Kennungen
- Admin: „Ändern" → Picker öffnen → unauffällig (Beschriftung wechselt zu
  „Auswahl schließen", `aria-expanded` gesetzt, „Clips werden geladen…" statt
  Leerzustand-Flash)
- Admin: Quelle filtern / Live-Suche (400-ms-Debouncing) → unauffällig
- Admin: „Auswählen" / „Standard-Clip verwenden" → unauffällig (Chip aktualisiert
  sich sofort, Hinweis „Auswahl wird mit „Speichern" übernommen." erscheint,
  „Speichern" persistiert und löst den Hinweis wieder auf)
- Admin: „Weitere laden" → unauffällig (gesperrt + Ladetext während der Abfrage)
- Admin: Zuvor gewählter Clip wurde gelöscht → unauffällig (Fallback-Text +
  stiller Reset; „Speichern" funktioniert, Referenz wird auf Standard geklärt)
- Zuschauerin: Warm-up-Phase, echte Störung, Sendelücke, fehlende Freigabe →
  unverändert unauffällig (`data-error-kind` an `#live-tv-stream-error` ist ein
  reines Diagnosemerkmal ohne sichtbare Wirkung)

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Components/Pages/LiveTv.razor`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor` (Vergleichsmuster)
- `VideoWebPlayer/Services/ProgramSettingsService.cs` (Fehlertext beim Speichern)
- `VideoWebPlayer/ViewModels/LiveTvErrorMessages.cs`
