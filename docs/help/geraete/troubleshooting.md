← [Zurück zur Übersicht](index.md)

# Geräte — Fehlerbehebung

## `POST /api/pairing/exchange` antwortet mit `429`

**Symptom:** Jeder Request an `exchange` — auch mit gültigem Code — liefert `429 Too Many Requests`.

**Ursache:** Die Client-IP hat 5 Fehlversuche erreicht und wurde über `ILoginIpBlockService` gesperrt (`BlockedLoginIps`). Die Sperre ist mit dem Web-Login geteilt.

**Lösung:**
1. Unter `/admin/security` die gesperrte IP prüfen und entsperren (`LoginIpBlockService.Unblock` entfernt auch nicht persistierte Fehlerzähler aus dem Cache).
2. Ursache der Fehlversuche klären (z. B. falsch übertragener Code oder abgelaufener Code im Retry).

> **Hinweis:** `Unblock` entfernt den Cache-Eintrag nun auch dann, wenn noch keine persistierte Sperre existiert — ein reiner Zähler unterhalb der Schwelle wird damit ebenfalls zurückgesetzt.

## Exchange liefert `401` trotz korrektem Code

**Symptom:** `Ungültiger oder abgelaufener Pairing-Code.` bei einem gerade erzeugten Code.

**Ursache:** Der Lookup erfolgt über `SHA-256(code)` als UTF-8-String. Abweichende Schreibweise (z. B. Kleinbuchstaben, Leerzeichen) erzeugt einen anderen Hash. Ebenso führen abgelaufene (`ExpiresAtUtc`) oder bereits verbrauchte (`ConsumedAtUtc`) Codes zu `401` — die Antwort ist bewusst generisch und verrät nicht, welcher Grund zutraf.

**Lösung:**
1. Code exakt wie angezeigt übertragen (Großbuchstaben/Ziffern, ohne Whitespace).
2. Gültigkeitsfenster prüfen (`Pairing:CodeTtlMinutes`, Default 5 Minuten); bei Ablauf neuen Code erzeugen.
3. Prüfen, ob der Code bereits verbraucht wurde (`ConsumedAtUtc` in `PairingCodes`).

## Exchange liefert `400`

**Symptom:** `Ungültiger Pairing-Request.` — die Anfrage wird vor dem Code-Lookup abgelehnt.

**Ursache:** `code` oder `clientPublicKey` fehlt/ist leer, `clientPublicKey` ist nicht als SubjectPublicKeyInfo importierbar oder verwendet nicht die Kurve `nistP256`, oder `deviceName` überschreitet 200 Zeichen.

**Lösung:**
1. `clientPublicKey` als Base64-kodiertes SPKI eines P-256-ECDH-Schlüssels senden (kein Raw-Format, keine andere Kurve).
2. `deviceName` auf ≤ 200 Zeichen beschränken oder weglassen.
3. Kein Retry-Sturm nötig: `400` erhöht den Fehlversuchszähler nicht.

## Geräte-Token wird am Gate mit `401` abgelehnt

**Symptom:** `POST /api/auth/login` mit `X-API-Key: <Geraete-Token>` liefert `401`.

**Ursache:** Token wurde widerrufen (`RevokedAtUtc` gesetzt), falsch übertragen oder das Gerät existiert nicht mehr. Nur `MauiOnly`-Endpunkte prüfen Geräte-Tokens; bei `AnyClient` zählen sie nicht.

**Lösung:**
1. In `/admin/devices` den Status des Geräts prüfen (`Aktiv` vs. `Widerrufen`).
2. Bei Widerruf: Gerät neu koppeln.
3. Sicherstellen, dass der Token vollständig und ohne `Bearer `-Verwechslung übertragen wird (ein führendes `Bearer ` wird serverseitig entfernt).

## Tabellen fehlen nach dem Update

**Symptom:** Fehler beim Zugriff auf `PairedDevices`/`PairingCodes` nach dem Deployment.

**Ursache:** Die Migration `AddDevicePairing` wurde noch nicht angewendet.

**Lösung:**
1. Anwendung neu starten — `MigrateDatabase()` in `WebApplicationExtensions` wendet ausstehende Migrationen automatisch an.
2. Startlog auf Migrationsfehler prüfen.

## Umbenennen eines Geräts schlägt fehl

**Symptom:** `RenameAsync` liefert `false`; in der UI bleibt das Eingabefeld aktiv.

**Ursache:** Der neue Name ist nach dem Trimmen leer oder länger als 200 Zeichen, oder die Geräte-Id existiert nicht.

**Lösung:**
1. Einen nicht-leeren Namen mit höchstens 200 Zeichen wählen.
2. Seite über `Aktualisieren` neu laden und erneut versuchen.
