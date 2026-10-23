# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

— (Status `Keine Befunde`)

## Geprüfte Interaktionen

- **Diagnose-Spalte „Planbar"** in der Regel-Tabelle: rein lesend, deutsche Klartext-Begründungen
  („Material nicht gefunden (verwaist)", „N ohne Mediendatei", „N ohne Laufzeit (Ermittlung
  läuft)", „N ohne Laufzeit (gescheitert)", „N nicht für alle freigegeben", „Keine Inhalte im
  Material"). Keine internen Kennungen sichtbar. Bei mehreren Gründen werden alle genannt;
  bei >0 planbaren Inhalten steht die Zahl vorn.
- **„Jetzt generieren"-Button** auf der Sendeplan-Karte: beschriftet wie üblich
  (`btn btn-sm btn-primary`, id `#tvsender-generate-now`), während des Anstoßes deaktiviert
  (`disabled="@generatePending"`), quittiert sofort per Statustext und aktualisiert die
  Anzeige nach kurzer Zeit selbst; bei großen Bibliotheken weist der Text auf erneutes
  Seitenladen hin.
- **Erreichbarkeit:** beides nur im Admin-Zweig der Seite (`isAdmin`), serverseitig
  abgesichert. Die Diagnose gilt auch für inaktive Regeln — sinnvoll, weil dort gerade die
  Fehlersuche relevant ist.
- **Muster:** Statuszeilen (`form-text` mit id) entsprechen `tvsender-channel-status`/
  `tvsender-rules-status`; Spaltenreihenfolge fügt „Planbar" logisch hinter „Material" ein.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `docs/features/{branch}/requirement.md`

## Hinweise

- Die 4-Sekunden-Verzögerung bis zur Anzeige-Aktualisierung ist ein pragmatischer Kompromiss;
  der Statustext kommuniziert das transparent.
