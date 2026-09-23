# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Devices.razor (Admin-Seite `/admin/devices`)

- **Interne Kennung** — In der Tabelle „Pairing-Codes" wird die Spalte „Ersteller" über `ResolveCreator` aufgelöst. Schlägt der `UserManager.FindByIdAsync`-Lookup fehl (z. B. wurde das ausstellende Admin-Konto gelöscht), wird der rohe `CreatedByUserId` — eine GUID — in der Tabelle angezeigt (Zeile 227: `return creatorNames.TryGetValue(userId, out var name) ? name : userId;`). Eine nicht-technische Anwenderin kann mit einer GUID nichts anfangen und weiß nicht, wer den Code erstellt hat.

  Empfehlung: Statt der GUID einen Klartext-Fallback anzeigen, z. B. „Unbekannt" oder „(geloeschter Benutzer)".

- **Erreichbarkeit** — In der Tabelle „Gekoppelte Geraete" zeigt die Spalte „Status" für gesperrte Geraete das Wort „Widerrufen" (Zeile 136). Dasselbe Wort „Widerrufen" ist zugleich die Beschriftung des Aktions-Buttons (Zeile 148). Zwar verschwindet der Button nach dem Widerruf, dennoch kann eine Laiin „Widerrufen" in der Status-Spalte als Handlungsaufforderung statt als Zustand lesen — insbesondere weil kein Widerruf-Zeitpunkt angezeigt wird.

  Empfehlung: Eindeutigen Zustandstext verwenden, z. B. „Zugriff entzogen" oder „Widerrufen am {RevokedAtUtc.ToLocalTime():g}"; die Aktion kann „Widerrufen" heißen bleiben.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Admin-Kachel „Geraete" in `/admin` aufrufen → unauffällig (Kachel vorhanden, klar beschriftet)
- Pairing-Code erzeugen (Schaltfläche „Pairing-Code erzeugen", Einmalanzeige des Codes) → unauffällig (8-Zeichen-Code ohne verwechselbare Zeichen, Hinweis „wird nur einmal angezeigt")
- TTL/Gültigkeit des Codes ablesen → unauffällig („Gueltig bis HH:mm:ss (5 Minuten)", lokale Zeit)
- Aktive Pairing-Codes einsehen (Erstellt / Gueltig bis / Ersteller) → Befund vorhanden (GUID-Fallback in „Ersteller")
- Gekoppelte Geraete einsehen (Name, Ausgestellt, Zuletzt verwendet, Status) → Befund vorhanden (mehrdeutiger Status-Wortlaut „Widerrufen"); Fallback-Name „Geraet vom <Datum, Uhrzeit>" macht namenlose Geraete unterscheidbar → unauffällig
- Geraet umbenennen → unauffällig (Inline-Eingabefeld mit „Speichern"/„Abbrechen", leere Eingabe blockiert)
- Geraet widerrufen inkl. Bestätigung → unauffällig (Bestätigungskarte mit erklärendem Text, Pflicht-Checkbox und „Widerruf bestaetigen"; entspricht dem etablierten Muster aus Backups.razor)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Devices.razor`
- `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor`
