# Bestehende Dokumentation — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`). Übersicht der von der Anforderung berührten Dokumentationsdateien.

## `docs/API.md`

- Beschreibt den API-Vertrag; DTOs liegen laut Einleitung unter `VideoWebPlayer.Client/` (Zeile 8).
- Authentifizierungsabschnitt (Zeilen 10–17): `GET /api/health` ohne Auth; `POST /api/auth/login` benötigt `X-API-Key: <CLIENT_API_TOKEN>`; alle übrigen Endpunkte `Authorization: Bearer`.
- Kein Pairing-Endpunkt dokumentiert (erwartet: `POST /api/pairing/exchange` hinzuzufügen).
- Abschnitt „Vertragscheck" (Zeilen 311–317) verweist auf `ApiDocumentationContractTests`, der Pflicht-Routenliste gegen die Datei prüft — ein neuer öffentlicher Endpunkt wäre dort ggf. zu ergänzen.

## `docs/SECRETS_MANAGEMENT.md`

- Tabellarische Secrets-Übersicht (Zeilen 12–18) ohne `Jwt:ApiToken:Maui`/`Jwt:ApiToken` (siehe [configuration.md](configuration.md)).
- Anforderung: Rolle von `Jwt:ApiToken:Maui` als Fallback bzw. Entfall dokumentieren.

## `docs/GUIDE_Installation.md`

- Enthält Setup-Anleitung mit `dotnet user-secrets`-Beispielen für `Jwt:Key`, `Jwt:ApiToken:Web`, `Jwt:Issuer` (Zeilen 35–37, 70–72) und Produktions-Environment-Variablen `Jwt__*` (Zeilen 93–101).
- Abschnitte: Voraussetzungen, Linux/Windows-Installation, Produktive Konfiguration, Git-Hooks, Tests, Häufige Fehler. Kein Geräte-Pairing-Abschnitt vorhanden.

## `docs/help/`

- `einrichtung.md`: beschreibt den Admin-Bereich `Einrichtung` mit den Kacheln Quellen, Backups, Updates, Sicherheit (blockierte IPs), Genres, Allgemein, Anwender (Zeilen 7–17). Keine Pairing-/Geräte-Hilfeseite vorhanden.
- `index.md` sowie Unterordner (`episoden`, `medienquellen`, `schauspieler`, `weiterschauen`) — keine Sicherheits-/Pairing-Seite.

## `issue.md` (Repo-Wurzel)

- Enthält die Originalanforderung (Issue #228) mit dem vorgeschlagenen Flow: Einmal-Pairing-Code → `X-API-Key`-Geräte-Token, ECDH-Absicherung wegen HTTP ohne TLS, TV-App-Perspektive. Kontext: App soll öffentlich verteilbar sein, jeder Nutzer hostet selbst.

## Weitere

- `docs/INDEX.md`, `docs/RELEASE_NOTES.md`, `docs/PUBLICATION_CHECKLIST.md` — für diese Bestandsaufnahme nicht im Detail relevant.
