# Secrets Management für VideoWebPlayer

> **Dokumenttyp**: Sicherheitsdokumentation
> **Zielgruppe**: Entwickler, Administratoren
> **Version**: 2.0
> **Letzte Aktualisierung**: 2026-08-25

Diese Dokumentation verwendet ausschließlich synthetische Platzhalter. Werte wie `<PRODUKTIVER_JWT_KEY>` oder `<CLIENT_API_TOKEN>` sind keine Beispiele zum Kopieren, sondern Markierungen für selbst erzeugte Secrets.

## Konfigurationswerte

| Name | Geheim? | Verwendung | Empfohlene Quelle |
|------|---------|------------|-------------------|
| `Jwt:Key` | Ja | Signaturschlüssel für JWTs | User Secrets, Umgebungsvariable, Secret Store |
| `Jwt:ApiToken:Web` | Ja | Gate für Web-/Browser-API-Aufrufe | User Secrets, Umgebungsvariable, Secret Store |
| `Jwt:Issuer` | Nein | Aussteller-Name im JWT | Konfiguration |
| Benutzerpasswörter | Ja | Anmeldung | Datenbank/Identity-System |
| JWT Access Token | Ja | Benutzerautorisierung nach Login | Laufzeitspeicher des Clients |

Ein API-Gate-Wert ersetzt keine Benutzeranmeldung. Die eigentliche Autorisierung erfolgt über das JWT nach erfolgreichem Login.

## Entwicklung

```bash
cd VideoWebPlayer
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<ENTWICKLUNGS_JWT_KEY_BASE64_MIN_32_BYTES>"
dotnet user-secrets set "Jwt:ApiToken:Web" "<ENTWICKLUNGS_WEB_API_TOKEN>"
dotnet user-secrets set "Jwt:Issuer" "VideoWebPlayer"
```

Prüfen:

```bash
dotnet user-secrets list
```

Die Ausgabe enthält lokale Entwicklungswerte. Sie darf nicht in Issues, Logs, Dokumentation oder Screenshots veröffentlicht werden.

## Produktion

Linux:

```bash
export Jwt__Key="<PRODUKTIVER_JWT_KEY>"
export Jwt__ApiToken__Web="<PRODUKTIVER_WEB_API_TOKEN>"
export Jwt__Issuer="VideoWebPlayer"
```

Windows PowerShell:

```powershell
$env:Jwt__Key = "<PRODUKTIVER_JWT_KEY>"
$env:Jwt__ApiToken__Web = "<PRODUKTIVER_WEB_API_TOKEN>"
$env:Jwt__Issuer = "VideoWebPlayer"
```

Für dauerhaft betriebene Systeme sollen die Werte aus einem Secret-Management-System oder geschützten Service-Konfigurationen kommen. Produktive JWT-Schlüssel müssen zufällig erzeugt, ausreichend lang und rotationsfähig sein.

## Was nicht ins Repository gehört

- Echte JWT-Schlüssel.
- Echte API-Tokens.
- `secrets.json`.
- `.env`-Dateien.
- Datenbankdateien mit Benutzer- oder Medienquellendaten.
- FTP-/SFTP-Zugangsdaten.
- Produktions-Logs mit Authorization-Headern.

## Rotation und Verdachtsfälle

Wenn ein Wert jemals öffentlich war oder realistisch produktiv verwendet wurde:

1. Wert sofort außer Betrieb nehmen.
2. Ersatzwert erzeugen.
3. Backend- und Client-Konfiguration aktualisieren.
4. Logs und Dokumentation auf weitere Vorkommen prüfen.
5. Git-Historienbereinigung nur nach expliziter Freigabe durchführen.

## Lokaler Scan vor Veröffentlichung

```bash
git grep -n -I -E "Jwt:Key|Jwt__Key|ApiToken|Authorization: Bearer|password|secret|token" -- .
git log --all --source --decorate -G "Jwt:Key|Jwt__Key|ApiToken|Authorization: Bearer|password|secret|token" -- .
```

Treffer in Dokumentation müssen synthetische Platzhalter sein. Treffer in Code müssen Konfigurationszugriffe, Test-Credentials oder bewusst als auslesbare Client-Gate-Platzhalter gekennzeichnete Werte sein. Produktive Werte blockieren die Veröffentlichung bis zur Rotation und bis zur Freigabe des bereinigten Historienumfangs.

## Weitere Dokumentation

- [Installation und Setup](./GUIDE_Installation.md)
- [API-Vertrag](./API.md)
- [Veröffentlichungscheckliste](./PUBLICATION_CHECKLIST.md)
