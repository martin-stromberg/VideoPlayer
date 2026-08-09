# Risiken, Abhaengigkeiten und offene Entscheidungen

## Risiken

| Risiko | Auswirkung | Gegenmassnahme |
|---|---|---|
| Zwei Web-Frontends im Repository | Aenderungen koennen die produktive Oberflaeche verfehlen oder doppelte Arbeit erzeugen | Zielprojekt vor der Planung festlegen und im Plan als harte Grenze dokumentieren |
| Dynamische Medienbilder und Streams | Screens koennen mit fehlenden, langsamen oder fehlerhaften Medien anders aussehen | Platzhalter-, Lade- und Fehlerzustand in gemeinsamen Medienkomponenten definieren |
| Rollen- und Quellenlogik in Navigation | Links oder Admin-Aktionen koennen bei Layoutumbau ungewollt verschwinden | Bestehende `AuthorizeView`-, Claim- und API-Aufrufe unveraendert testen |
| Feste Hoehen und Overlay-Positionen | Mobile Inhalte koennen ueberlappen oder abgeschnitten werden | Viewport-Matrix und lange Inhalte pruefen; feste Werte auf notwendige Mindestwerte reduzieren |
| Globale CSS-Overrides | Unbeabsichtigte Regressionen in Konto- und Admin-Seiten | Token-/Scope-Strategie festlegen und alle betroffenen Layoutgruppen pruefen |
| Stitch-HTML statt komponentenfaehiger Vorlage | Direkte Uebernahme kann unwartbares HTML oder externe Abhaengigkeiten einfuehren | Nur visuelle Regeln und Struktur uebernehmen; Blazor-Komponenten und bestehende Services beibehalten |

## Offene Entscheidungen

1. Ist `VideoWebPlayer` das verbindliche Ziel oder muss auch `WebPlayer` angepasst werden?
2. Welche konkreten Stitch-Screenshots gelten als erster Lieferumfang: nur Medienseiten und Player oder auch Login, Konto und Admin?
3. Welche Desktop-/Mobile-Breiten und welche Browser sind fuer die visuelle Abnahme verbindlich?
4. Sind Montserrat und Inter als externe Webfonts erlaubt, oder muessen vorhandene lokale/Systemschriften verwendet werden?
5. Gibt es verbindliche Accessibility-Ziele, insbesondere WCAG-Level, Tastaturbedienung und Mindestkontraste?
6. Soll der Stitch-Entwurf die bisherige dunkle Produktidentitaet vollstaendig ersetzen oder nur fuer die referenzierten Medienansichten gelten?

## Abhaengigkeiten fuer die Planung

- Zugriff auf die Stitch-Screenshots und `DESIGN.md` muss waehrend Implementierung und visueller Abnahme erhalten bleiben.
- Die Entscheidung zum Zielprojekt muss vor einer Aenderung an globalen Layout- oder CSS-Dateien fallen.
- Fuer die responsive Abnahme werden lokale Testdaten oder stabile Platzhalterbilder benoetigt.
- Aenderungen an globalen Layoutkomponenten sollten mit den bestehenden fachlichen Tests und einem manuellen Navigationsdurchlauf abgesichert werden.
