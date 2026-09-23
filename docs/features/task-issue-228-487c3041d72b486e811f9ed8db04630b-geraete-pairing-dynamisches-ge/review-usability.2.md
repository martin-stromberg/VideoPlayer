# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Devices.razor (Admin-Seite „Geraete", `/admin/devices`)

- **Erreichbarkeit** — Der Hinweis zum frisch erzeugten Pairing-Code zeigt die Ablaufzeit als „Gueltig bis HH:mm:ss UTC" (Zeile 57); ebenso sind alle Tabellen-Zeitspalten in UTC beschriftet (Zeilen 74–75, 113–114, 134–135). Der Pairing-Code ist nur wenige Minuten gueltig und muss in dieser Zeit auf dem Geraet eingegeben werden. Eine nicht-technische Anwenderin, deren Uhr lokale Zeit (z. B. MESZ = UTC+2) anzeigt, sieht eine scheinbar bereits vergangene Uhrzeit und kann den Code irrtuemlich fuer abgelaufen halten. Betroffene Interaktion: „Pairing-Code erzeugen inkl. TTL-Anzeige".

  Empfehlung: Ablaufzeit in lokaler Zeit anzeigen (`ExpiresAtUtc.ToLocalTime()`), wie es `Backups.razor` und `Updates.razor` bereits tun; die Minuten-Angabe („(X Minuten)") beibehalten. Alternativ zusaetzlich eine Restlaufzeit („noch ca. 4 Minuten") anzeigen.

- **Fehlende Suche/Auswahl** — Geraete, deren App keinen Anzeigenamen im Exchange-Request mitschickt (`DeviceName` ist optional, `PairingExchangeRequest.cs` Zeile 20), erhalten den identischen Fallback-Namen „Geraet" (`DeviceTokenService.cs` Zeile 54). In der Tabelle „Gekoppelte Geraete" sind dann mehrere Zeilen nicht unterscheidbar; die Anwenderin kann beim Widerruf nicht erkennen, welche Zeile das verlorene/auszusperrende Geraet ist — einzig der Ausstellungs-Zeitstempel differenziert. Betroffene Interaktion: „Liste gekoppelter Geraete, Widerruf-Aktion".

  Empfehlung: Unterscheidbaren Fallback-Namen vergeben (z. B. „Geraet vom 23.09.2026, 09:48" oder laufende Nummer) oder eine zusaetzliche unterscheidende Spalte anzeigen, damit das richtige Geraet identifiziert werden kann. Die vorhandene Umbenennen-Aktion bleibt als Korrekturmoeglichkeit bestehen.

- **Abweichendes Muster** — „Widerrufen" (Zeile 148) wird mit einem einzigen Klick sofort und unwiderruflich ausgefuehrt; ein Fehlklick sperrt das Geraet aus und erfordert ein erneutes physisches Pairing. Fuer irreversible Aktionen existiert im Projekt bereits ein Bestaetigungsmuster (Checkbox-Bestaetigung in `Backups.razor`, Zeilen 211–230). Betroffene Interaktion: „Widerruf-Aktion".

  Empfehlung: Kurze Bestaetigung vor dem Widerruf einfordern — entweder das vorhandene Checkbox-/Dialog-Muster aus `Backups.razor` wiederverwenden oder eine Inline-Bestaetigung („Wirklich widerrufen?") in der Zeile anbieten.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Admin-Seite „Geraete" ueber die Einrichtungs-Kachel erreichen (`AdminIndex.razor` Kachel „Geraete", nur fuer Admins sichtbar) → unauffällig
- Pairing-Code erzeugen und TTL ablesen (Button „Pairing-Code erzeugen", Code wird einmalig mit Ablaufzeit und Minutenangabe angezeigt; Code-Alphabet ohne verwechselbare Zeichen, 8 Grossbuchstaben/Ziffern — gut abtippbar) → Befund vorhanden (UTC-Ablaufzeit)
- Aktive Pairing-Codes einsehen inkl. Ersteller (Ersteller wird als E-Mail/Benutzername aufgeloest, keine rohe GUID) → unauffällig
- Gekoppelte Geraete einsehen und das richtige Geraet identifizieren (Tabelle mit Name, Ausstellungs-/Nutzungszeit, Status) → Befund vorhanden (identischer Fallback-Name „Geraet")
- Geraet umbenennen (Inline-Eingabefeld mit „Speichern"/"Abbrechen", leerer Name nicht speicherbar) → unauffällig
- Geraet widerrufen (Button „Widerrufen" je Zeile, Status wechselt auf „Widerrufen") → Befund vorhanden (keine Bestaetigung)
- Interne Kennungen eingeben/kennen muessen → unauffällig (kein Id-/GUID-Feld; der Pairing-Code wird erzeugt statt eingegeben)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Devices.razor`
- `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor`
