← [Zurück zur Übersicht](index.md)

# Geräte — Installation und Konfiguration

## Voraussetzungen

- Laufender VideoWebPlayer mit Datenbank; die Migration `AddDevicePairing` (Tabellen `PairedDevices` und `PairingCodes`) wird beim Start über `MigrateDatabase()` automatisch angewendet.
- In Produktion ist `Jwt:ApiToken:Maui` weiterhin ein Pflichtwert (Fallback für ältere App-Versionen); Deployments ohne diesen Wert starten nicht.

## Installationsschritte

1. Anwendung aktualisieren und starten — die Datenbankmigration läuft automatisch.
2. `Jwt:ApiToken:Maui` in Produktion gesetzt halten (User Secrets, Umgebungsvariable oder Secret Store).
3. Als Administrator `Einrichtung` > `Geräte` öffnen und einen Pairing-Code erzeugen.
4. Den Code in der Client-App eingeben; die App ruft `POST /api/pairing/exchange` auf.

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Pairing:CodeLength` | int | `8` | Länge des Pairing-Codes (Alphabet ohne `0/O/1/I/L`, fest im Code). |
| `Pairing:CodeTtlMinutes` | int | `5` | Gültigkeitsdauer eines Pairing-Codes in Minuten. |

Die Fehlversuchsschwelle für `exchange` ist keine eigene Konfiguration, sondern die geteilte Konstante `Threshold = 5` des `LoginIpBlockService`. Es gibt keinen Aufräumlauf für abgelaufene Codes; sie werden bei der Validierung verworfen und in der UI ausgefiltert.

## Umgebungsvariablen

| Variable | Pflicht | Beispielwert | Beschreibung |
|----------|---------|--------------|--------------|
| `Jwt__ApiToken__Maui` | Ja (Produktion) | `<PRODUKTIVER_MAUI_API_TOKEN>` | Fallback-Gate-Token für ältere App-Versionen. |
| `Pairing__CodeLength` | Nein | `8` | Länge des Pairing-Codes. |
| `Pairing__CodeTtlMinutes` | Nein | `5` | Gültigkeitsdauer des Pairing-Codes in Minuten. |

## Überprüfung

- Die Kachel `Geräte` erscheint unter `Einrichtung` für Administratoren.
- `Pairing-Code erzeugen` zeigt einen Code mit Gültigkeitsdauer an; ein Test-Exchange gegen `POST /api/pairing/exchange` liefert `200` mit `serverPublicKey` und `encryptedToken`.
- Ein ungültiger Code liefert `401`; nach 5 Fehlversuchen `429`, die IP erscheint unter `Einrichtung` > `Sicherheit`.
