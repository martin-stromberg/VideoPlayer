# Detailinventar: Veroeffentlichung und Geheimnisschutz

## Bestand

- `.gitignore` ignoriert User-Secrets-nahe Dateien, Build-Ausgaben, Logs, Datenbanken und Publish-Dateien.
- `VideoWebPlayer/VideoWebPlayer.csproj:5` verwendet eine `UserSecretsId`; `appsettings.json` enthaelt keine offensichtlichen JWT-Schluessel.
- `docs/SECRETS_MANAGEMENT.md` dokumentiert User Secrets, Environment Variables und MAUI-Tokens, enthaelt aber tokenartige Beispielwerte sowie konkrete hardkodierte Tokenwerte als vermeintlich unkritische Beispiele.
- `VideoWebPlayer.Maui/Services/AuthService.cs` und verwandte Services muessen auf eingebettete Credentials geprueft werden.
- CI fuehrt einen Dependency Vulnerability Scan aus, aber kein erkennbares Credential- oder Secret-Scanning.

## Betroffene Flaechen

`docs/SECRETS_MANAGEMENT.md`, alle `appsettings*.json`, Launch-/Service-Konfigurationen, MAUI-Authentifizierungsservices, `.gitignore`, CI-Workflows sowie die Git-Historie vor der oeffentlichen Freigabe.

## Risiken

- Platzhalter mit realistisch aussehenden Tokens werden von Nutzern oder Scannern als echte Zugangsdaten interpretiert und koennen bei Wiederverwendung kompromittiert sein.
- Hardkodierte Client-Tokens koennen auch bei geringer Schutzwirkung nicht als geheim behandelt werden; die Dokumentation muss deren Zweck und Grenzen klarstellen.
- Eine aktuelle Dateipruefung reicht nicht aus, wenn Werte bereits in der Historie veroeffentlicht wurden. Erforderlich sind Bewertung, Rotation und gegebenenfalls History-Rewrite durch den Repository-Verantwortlichen.
- Persoenliche Kontaktinformationen in der Lizenz sind beabsichtigt, aber als oeffentlich zu bestaetigen.

## Verifikation

Repository-Dateien und relevante Git-Historie mit einem Secret-Scanner pruefen, nur eindeutig synthetische Platzhalter verwenden, User-Secrets/Environment Variables dokumentieren und die Ausgabe des Scans vor der Veroeffentlichung als Gate behandeln.
