# Offene Aufgaben

Erstellt am: 2026-09-23
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `LoginIpBlockService.Unblock` (Zeilen 176-189): Kein `Normalize()` auf dem String-Schlüssel, während alle anderen Methoden normalisieren — `_cache.TryRemove(ip)` und `db.BlockedLoginIps.Find(ip)` verfehlen nicht-normalisierte Formen (z. B. IPv4-mapped-IPv6). Empfehlung: Eingabestring via `IPAddress.TryParse` + `Normalize` auflösen, dann `key` für `TryRemove`/`Find` verwenden.
- [ ] `PairingService.CreatePairingCodeAsync` (Zeilen 152-174): `Pairing:CodeLength`/`Pairing:CodeTtlMinutes` unvalidiert — `codeLength < 0` wirft `ArgumentOutOfRangeException` (HTTP 500), `0` erzeugt leeren Code, `ttl <= 0` sofort abgelaufenen Code. Empfehlung: Werte auf sinnvollen Bereich klemmen bzw. auf Defaults zurückfallen (`Math.Max(4, ...)` / `Math.Max(1, ...)`).
- [ ] `PairingWebApplicationFactory.CreateTempDbPath` (Zeilen 15-20): `File.Delete` auf Pfad mit frischer GUID ist toter Code — `File.Delete` samt `try/catch` entfernen (echte Löschung erfolgt in `Dispose`).

## Usability-Befunde

- [ ] `Devices.razor` (Zeile 227): Schlägt der `UserManager.FindByIdAsync`-Lookup des Code-Erstellers fehl (z. B. gelöschtes Admin-Konto), wird die rohe Benutzer-GUID in der Spalte „Ersteller" angezeigt. Empfehlung: Klartext-Fallback wie „Unbekannt" oder „(gelöschter Benutzer)".
- [ ] `Devices.razor` (Zeile 136): Status-Spalte zeigt für gesperrte Geräte „Widerrufen" — identisch zum Aktions-Button (Zeile 148), kann als Handlungsaufforderung statt Zustand gelesen werden. Empfehlung: eindeutiger Zustandstext, z. B. „Zugriff entzogen" oder „Widerrufen am {RevokedAtUtc.ToLocalTime():g}".

## Fehlgeschlagene Tests

Keine.
