# Legacy-Hinweis: Secrets/Keys einrichten

> **Status**: Ersetzt durch [SECRETS_MANAGEMENT.md](./SECRETS_MANAGEMENT.md)
> **Letzte Aktualisierung**: 2026-08-25

Diese Datei bleibt nur als Kompatibilitaetsverweis fuer alte Links erhalten. Die fruehere Anleitung verwendete den allgemeinen Schluessel `Jwt:ApiToken`; fuer neue Installationen gelten getrennte Werte:

- `Jwt:ApiToken:Web` fuer Web-/Browser- und interne Requests.
- `Jwt:ApiToken:Maui` fuer initiale MAUI-Health-/Login-Aufrufe.

Verbindliche Anleitung:

- [Secrets Management](./SECRETS_MANAGEMENT.md)
- [Installation und Setup](./GUIDE_Installation.md)
- [API-Vertrag](./API.md)
