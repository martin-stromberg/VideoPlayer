# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Interaktionen

Die Anforderung enthält keine aktive Benutzereingabe — es geht um eine reine
Status-/Fortschrittsanzeige (Lesen, nicht Bedienen). Geprüft wurde daher die Verständlichkeit
der gebauten Anzeige in `TVSenderAdmin.razor`:

- **Karte „Laufzeitermittlung"** (`#tvsender-probe-card`) — eigenständiger, beschrifteter
  Abschnitt auf der bekannten Admin-Seite; Kopfzeile fasst „n von m ermittelt" zusammen. → unauffällig
- **Zähler** — „ermittelt" (grün), „ausstehend" (gelb), „fehlgeschlagen" (rot) als Chips plus
  Fortschrittsbalken mit Prozent-Breite und ARIA-Rolle. Farb-/Textbedeutung stimmt mit den
  übrigen Chips der Seite überein. → unauffällig
- **Statushinweis** (`#tvsender-probe-status`) — Klartext-Sätze („Alle Videodateien sind
  ermittelt.", „Ermittlung läuft — …", „…keine Aktivität in der letzten Stunde — die
  Ermittlung hängt möglicherweise (ffprobe verfügbar?)"). Der Hinweis auf ffprobe ist der
  tatsächliche Lösungsansatz für Administratoren und dort bewusst genannt. → unauffällig
- **Rolling-Liste** (`#tvsender-probe-recent`) — Tabelle Zeitpunkt/Datei/Quelle/Ergebnis mit
  lesbaren Ergebnissen („1:30:00" bzw. „45:00 min" / „keine Laufzeit — Versuch n") —
  keine internen Kennungen. → unauffällig
- **Leerstand** — `admin-empty-state` („Keine Videodateien gefunden") statt leerer Tabelle. → unauffällig
- **Aktualisierung** — automatische Aktualisierung alle 15 s; kein manuelles Neuladen nötig. → unauffällig
- **Erreichbarkeit** — nur für Administratoren (Seite rendert sonst „Nicht autorisiert.",
  Service wirft zusätzlich). Entspricht der Anforderung (Admin-only). → unauffällig

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
