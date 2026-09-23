# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Devices.razor (Admin-Seite `/admin/devices`)

- **Interne Kennung** — In der Tabelle „Pairing-Codes" wird in der Spalte „Ersteller" die rohe `CreatedByUserId` ausgegeben (Zeile 84: `@(codeInfo.CreatedByUserId ?? "-")`). Da die Benutzer auf `IdentityUser` mit String-GUID-Ids basieren, sieht die Administratorin dort eine unleserliche GUID wie `f47ac10b-58cc-...` statt eines erkennbaren Namens. Eine nicht-technische Person kann nicht erkennen, wer den Code erzeugt hat.

  Empfehlung: Die User-Id über `UserManager<IdentityUser>` auflösen und die E-Mail-Adresse anzeigen (Muster wie in `UserManagement.razor`, wo `user.Email` als lesbare Identität dient) — oder die Spalte entfernen, wenn die Information nicht benötigt wird.

- **Erreichbarkeit** — Gekoppelte Geräte lassen sich nicht umbenennen. Der Anzeigename kommt ausschließlich aus dem optionalen `deviceName`-Feld des Exchange-Requests (`PairingExchangeRequest.DeviceName`); sendet die App keinen Namen, speichert `DeviceTokenService.IssueAsync` den Fallback `"Geraet"` für jedes Gerät. In der Geräteliste stehen dann mehrere Einträge mit identischem Namen „Geraet", die sich nur über Ausstellungs-/Nutzungszeitpunkte unterscheiden. Die Administratorin kann beim Widerrufen nicht sicher erkennen, welcher Eintrag zu welchem physischen Gerät (z. B. „TV Wohnzimmer") gehört, und hat keine Möglichkeit, den Namen nachträglich zu vergeben oder zu korrigieren — obwohl die Anforderung diese Option ausdrücklich als offene Frage benennt („oder vergibt der Admin den Namen nachträglich in der UI?").

  Empfehlung: In der Gerätetabelle eine Bearbeiten-/Umbenennen-Aktion anbieten (Inline-Textfeld oder kleiner Dialog, der `PairedDevice.Name` aktualisiert). Alternativ — falls der App-Vertrag einen Namen verpflichtend macht — in der Liste zusätzlich ein unterscheidendes Merkmal anzeigen; die Umbenennen-Funktion ist aber die robustere Lösung, da `deviceName` im Vertrag optional bleibt.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Admin-Kachel „Geraete" auf der Einrichtungs-Übersicht erreichen → unauffällig (`AdminIndex.razor`, Kachel vorhanden)
- Pairing-Code in der Web-UI erzeugen → unauffällig (Button „Pairing-Code erzeugen", Code wird einmalig angezeigt)
- TTL/Gültigkeit des erzeugten Codes ablesen → unauffällig (Anzeige „Gueltig bis ... UTC (x Minuten)")
- Aktive Pairing-Codes einsehen → Befund vorhanden (Spalte „Ersteller" zeigt GUID statt Benutzername)
- Gekoppelte Geräte auflisten (Name, ausgestellt, zuletzt verwendet, Status) → Befund vorhanden (keine Umbenennung; bei fehlendem App-Namen sind Geräte nicht unterscheidbar)
- Gerät widerrufen → unauffällig (beschrifteter Button „Widerrufen" pro aktivem Gerät; kein technischer Identifier nötig)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Devices.razor`
- `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor`
