← [Zurück zur Übersicht](index.md)

# Geräte — Business Rules

## Einmal-Pairing-Code

**Beschreibung:** Ein Pairing-Code darf nur einmal und nur innerhalb seiner Gültigkeitsdauer eingelöst werden.

**Bedingungen:**
- Der eingegebene Code muss per `SHA-256`-Hash einem Datensatz in `PairingCodes` entsprechen.
- `ExpiresAtUtc` darf nicht überschritten sein, `ConsumedAtUtc` muss `null` sein.

**Verhalten:**
- Wenn alle Bedingungen erfüllt sind: Der Code wird atomar als verbraucht markiert und der Exchange läuft weiter.
- Sonst: generische `401`-Antwort und die Client-IP erhält einen Fehlversuchszähler.

**Umsetzung:** `PairingService.ExchangeAsync` — der Verbrauch erfolgt per `ExecuteUpdateAsync` mit `WHERE ConsumedAtUtc IS NULL AND ExpiresAtUtc > now`, sodass parallele Einlöseversuche und ein zwischen Lese- und Schreibzugriff abgelaufener Code sicher abgefangen werden.

## Fehlversuchszählung und IP-Sperre

**Beschreibung:** Bruteforce auf den öffentlichen Exchange-Endpunkt wird über die geteilte IP-Sperrliste gebremst.

**Bedingungen:**
- Nur inhaltlich ungültige Codes (`401`) zählen als Fehlversuch; Formatfehler (`400`) zählen nicht, damit z. B. ein leerer Code den Zähler nicht erhöht.
- Ab 5 Fehlversuchen (`LoginIpBlockService.Threshold`) wird die IP gesperrt; ein erfolgreicher Exchange setzt den Zähler zurück.

**Verhalten:**
- Gesperrte IP: `429`, unabhängig vom Request-Inhalt.
- Die Sperre gilt auch für das Web-Login und ist unter `/admin/security` sichtbar und entsperrbar.

**Umsetzung:** `PairingController.Exchange` in Verbindung mit `ILoginIpBlockService` (`IsBlocked`, `RegisterFailure`, `RegisterSuccess`).

## Klartext-Geheimnisse werden nie gespeichert

**Beschreibung:** Weder Pairing-Codes noch Geräte-Tokens liegen im Klartext in der Datenbank.

**Bedingungen:**
- `PairingCode.CodeHash` und `PairedDevice.TokenHash` enthalten `SHA-256`-Hashes (hohe Entropie, daher ohne Salt).

**Verhalten:**
- Der Klartext-Code wird genau einmal in der Admin-UI angezeigt; `GetActiveCodesAsync` liefert nur Metadaten.
- Das Klartext-Geräte-Token verlässt den Server nur einmal — AES-256-GCM-verschlüsselt in der Exchange-Response.

**Umsetzung:** `HashHelper.Sha256Hex`, `PairingService.CreatePairingCodeAsync`, `DeviceTokenService.IssueAsync`.

## Widerruf

**Beschreibung:** Ein widerrufenes Gerät verliert sofort die Möglichkeit, sich am Gate zu authentifizieren.

**Bedingungen:**
- `RevokeAsync` setzt `RevokedAtUtc`; `IsValidDeviceTokenAsync` akzeptiert nur Einträge mit `RevokedAtUtc == null`.

**Verhalten:**
- Folgeanfragen mit dem Geräte-Token werden mit `401` abgelehnt.
- Bereits ausgestellte Benutzer-JWTs bleiben bis zu ihrem Ablauf (12 Stunden) gültig — bewusst akzeptierte Restlaufzeit.
- Die UI erfordert eine explizite Checkbox-Bestätigung vor dem Widerruf.

**Umsetzung:** `DeviceTokenService.RevokeAsync`, `DeviceTokenService.IsValidDeviceTokenAsync`, `Devices.razor` (`ConfirmRevokeAsync`).

## Fallback-Konfigurationstoken

**Beschreibung:** `Jwt:ApiToken:Maui` bleibt neben den Geräte-Tokens als akzeptierter `X-API-Key` für `MauiOnly` bestehen, damit ältere App-Versionen weiter funktionieren.

**Bedingungen:**
- Nur im Scope `MauiOnly` wird nach einem Config-Nicht-Treffer die Datenbank geprüft; `AnyClient` löst nie einen DB-Zugriff aus.

**Verhalten:**
- Config-Treffer oder gültiges Geräte-Token: Request passiert; bei Geräte-Token wird zusätzlich `LastUsedAtUtc` aktualisiert.
- Weder noch: `401`, Log ohne Token-Wert.

**Umsetzung:** `ApiTokenCheckAttribute.OnActionExecutionAsync`; der Produktions-Pflichtcheck auf `Jwt:ApiToken:Maui` in `ServiceCollectionExtensions` bleibt unverändert.

## Gerätename

**Beschreibung:** Die App kann beim Exchange einen Anzeigenamen liefern; Administratoren können ihn später ändern.

**Bedingungen:**
- `deviceName` ist optional, wird getrimmt und darf höchstens 200 Zeichen haben; Überschreitung → `400` (keine stille Kürzung).
- `RenameAsync` akzeptiert denselben Wertebereich (1–200 Zeichen nach Trimmen).

**Verhalten:**
- Leerer/fehlender Name → Server-Default `Geraet vom <Ausstellungszeitpunkt>`, damit namenlose Geräte unterscheidbar bleiben.

**Umsetzung:** `PairingService.ExchangeAsync` (Validierung), `DeviceTokenService.IssueAsync` (Default) und `DeviceTokenService.RenameAsync`.
