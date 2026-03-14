# Secrets Management für VideoWebPlayer

Diese Dokumentation beschreibt, wie du die API-Tokens und JWT-Keys für Backend (Blazor) und MAUI-Client konfigurierst.

## Überblick

Das System verwendet folgende geheime Werte:

| Secret | Verwendung | Projekt | Speicherort |
|--------|-----------|---------|-----------|
| `Jwt:Key` | JWT Token Signaturschlüssel | Backend | User Secrets |
| `Jwt:ApiToken:Web` | API-Key für Blazor Web | Backend | User Secrets |
| `Jwt:ApiToken:Maui` | API-Key für MAUI-App | Backend | User Secrets |
| `Jwt:Issuer` | JWT Token Aussteller | Backend | appsettings.json |

**MAUI App**: Der API-Token ist hardkodiert im Code. Die **Sicherheit ergibt sich aus der Benutzer-Authentifizierung** (JWT Token nach erfolgreichem Login).

---

## 🔐 Sicherheitskonzept

### Backend (Blazor Web)
1. API-Key im Header prüfen (schützt vor zufälligen Requests)
2. Benutzer authentifiziert sich mit Credentials
3. Server gibt JWT Token aus
4. Token wird bei zukünftigen Requests verwendet

### MAUI App
1. API-Token hardkodiert (für Initial-Requests, Health-Check, Login)
2. Benutzer meldet sich mit Credentials an
3. Server gibt JWT Token zurück
4. JWT Token wird in Preferences gespeichert
5. Zukünftige Requests nutzen JWT Token (nicht API-Token!)

**Resultat**: Der API-Token ist nicht kritisch, da die echte Sicherheit vom JWT Token kommt.

---

## Backend (VideoWebPlayer) - User Secrets

### Schritt 1: User Secrets initialisieren (einmalig)

```sh
cd VideoWebPlayer

# User Secrets ID anzeigen
dotnet user-secrets list

# Falls leer, neu initialisieren mit:
dotnet user-secrets init
```

Die ID wird in der `.csproj` Datei gespeichert und die Secrets unter:
```
%APPDATA%\Microsoft\UserSecrets\<ID>\secrets.json
```

### Schritt 2: Secrets setzen

```sh
cd VideoWebPlayer

# JWT Signaturschlüssel (min. 256 Bit / 32 Bytes in Base64)
dotnet user-secrets set "Jwt:Key" "TXYxcnFJU2NYZjErOUlXYjdydC9tOFN4OGdLQ24yV2t0eEdBZHJYZHM3dUhvb2dqSE5RNzdRV25EMW42ZHQ2c1dmc1Q1eGZlbVRhdTN1YnE3RW5hanJJWHgzWWI1eCtFNUU3MkFMcUdRMkk9"

# API-Token für Blazor Web
dotnet user-secrets set "Jwt:ApiToken:Web" "web-api-token-12345"

# API-Token für MAUI App (Backend validiert damit den Client)
dotnet user-secrets set "Jwt:ApiToken:Maui" "maui-api-token-12345"

# JWT Issuer (Optional, Default: "VideoWebPlayer")
dotnet user-secrets set "Jwt:Issuer" "de.MartinStromberg.WebVideoPlayer"
```

### Schritt 3: Secrets überprüfen

```sh
cd VideoWebPlayer
dotnet user-secrets list
```

Erwartet Output:
```
Jwt:Key = TXYxcnFJU2NYZjErOUlXYjdydC9tOFN4OGdLQ24yV2t0eEdBZHJYZHM3dUhvb2dqSE5RNzdRV25EMW42ZHQ2c1dmc1Q1eGZlbVRhdTN1YnE3RW5hanJJWHgzWWI1eCtFNUU3MkFMcUdRMkk9
Jwt:ApiToken:Web = web-api-token-12345
Jwt:ApiToken:Maui = maui-api-token-12345
Jwt:Issuer = de.MartinStromberg.WebVideoPlayer
```

### Schritt 4: Secrets löschen (falls nötig)

```sh
cd VideoWebPlayer
dotnet user-secrets remove "Jwt:ApiToken:Web"
dotnet user-secrets clear  # Alle Secrets löschen
```

---

## MAUI Client (VideoWebPlayer.Maui) - Einfach!

### API-Token hardkodiert in Code

**Dateien:**
- `AuthService.cs`
- `ConnectionService.cs`

```csharp
private const string ApiToken = "maui-api-token-12345";
```

**Das ist ausreichend, weil:**
- ✅ API-Token ist nicht kritisch (schützt nur vor zufälligen Requests)
- ✅ Echte Sicherheit kommt von Benutzer-Authentifizierung (Credentials → JWT)
- ✅ Funktioniert überall (iPad, iPhone, Android, Release-Builds)
- ✅ Keine komplexe Konfiguration nötig

**Wenn du den Token ändern möchtest:**
1. Ändere `maui-api-token-12345` in den Services
2. Ändere `Jwt:ApiToken:Maui` im Backend (User Secrets)
3. Fertig!

---

## Automatisiertes Setup-Script für Backend

Erstelle diese PowerShell-Datei `setup-secrets.ps1`:

```powershell
# setup-secrets.ps1
# Automatisches Setup der User Secrets für Backend nur
# MAUI API-Token ist hardkodiert - siehe AuthService.cs

$apiTokenWeb = "web-api-token-12345"
$apiTokenMaui = "maui-api-token-12345"
$jwtKey = "TXYxcnFJU2NYZjErOUlXYjdydC9tOFN4OGdLQ24yV2t0eEdBZHJYZHM3dUhvb2dqSE5RNzdRV25EMW42ZHQ2c1dmc1Q1eGZlbVRhdTN1YnE3RW5hanJJWHgzWWI1eCtFNUU3MkFMcUdRMkk9"
$issuer = "de.MartinStromberg.WebVideoPlayer"

Write-Host "Setting up secrets for VideoWebPlayer Backend..." -ForegroundColor Green
cd VideoWebPlayer
dotnet user-secrets set "Jwt:Key" $jwtKey
dotnet user-secrets set "Jwt:ApiToken:Web" $apiTokenWeb
dotnet user-secrets set "Jwt:ApiToken:Maui" $apiTokenMaui
dotnet user-secrets set "Jwt:Issuer" $issuer
Write-Host "Backend secrets set!" -ForegroundColor Green

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "MAUI API-Token ist hardkodiert in: AuthService.cs und ConnectionService.cs" -ForegroundColor Yellow
```

Ausführen:
```powershell
.\setup-secrets.ps1
```

---

## Sicherheits-Best-Practices

### ✅ DO:
- ✅ **Unterschiedliche Tokens pro Client** (Web vs. MAUI)
- ✅ **User Secrets für Backend** verwenden
- ✅ **JWT Token verwenden** für authentifizierte Requests (nach Login)
- ✅ **Tokens regelmäßig rotieren**
- ✅ **secrets.json NICHT in Git committen** (`.gitignore` prüfen)

### ❌ DON'T:
- ❌ Secrets in `appsettings.json` hardcodieren (Backend)
- ❌ Gleiche Tokens für verschiedene Clients nutzen
- ❌ Secrets in Logs/Fehlermeldungen ausgeben
- ❌ Schwache Token-Strings verwenden

---

## Secrets in verschiedenen Umgebungen

### Entwicklung (Development) - Backend
- **Quelle**: User Secrets
- **Priorität**: User Secrets > appsettings.Development.json > appsettings.json
- **Tool**: `dotnet user-secrets set "Key" "Value"`

### Entwicklung - MAUI
- **Quelle**: Hardkodiert im Code (AuthService.cs, ConnectionService.cs)
- **Keine Konfiguration nötig!**

### Produktion (Production) - Backend
- **Quelle**: Environment Variables oder Secret Management (Azure KeyVault, etc.)
- **Priorität**: Environment Variables > appsettings.Production.json > appsettings.json

```bash
# Linux/Mac: Environment Variable setzen
export Jwt__Key="value"
export Jwt__ApiToken__Web="value"
export Jwt__ApiToken__Maui="value"

# Windows PowerShell
$env:Jwt__Key = "value"
$env:Jwt__ApiToken__Web = "value"
$env:Jwt__ApiToken__Maui = "value"
```

**Hinweis**: Doppelunterstrich `__` wird zu `:` konvertiert bei Environment Variables!

### Produktion - MAUI
- **Quelle**: Hardkodiert im Code (AuthService.cs, ConnectionService.cs)
- **Keine Konfiguration nötig!**
- Bei Bedarf: Ändere Konstanten und release neue App-Version

---

## Fehlerbehebung

### "The user secrets ID was not specified in the project file."

Lösung: `.csproj` Datei öffnen und `<UserSecretsId>` Tag hinzufügen:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <UserSecretsId>d7482f8a-36bd-4416-9261-680904a1c426</UserSecretsId>
</PropertyGroup>
```

### "Jwt:ApiToken:Web or Jwt:ApiToken:Maui is missing"

Lösung: User Secrets wurden nicht gesetzt. Siehe **Schritt 2** oben.

### Secrets werden in Development nicht geladen

Lösung: 
1. `ASPNETCORE_ENVIRONMENT=Development` Umgebungsvariable prüfen
2. `dotnet user-secrets list` ausführen
3. App neu starten

### "API-Token funktioniert nicht auf MAUI!"

Prüfe:
1. Token in `AuthService.cs` Zeile ~13: `private const string ApiToken = "maui-api-token-12345";`
2. Token in `ConnectionService.cs` Zeile ~11: `private const string ApiToken = "maui-api-token-12345";`
3. Backend User Secret `Jwt:ApiToken:Maui` hat gleichen Wert
4. Manuell mit `dotnet user-secrets list` checken (Backend)

---

## Weiterführende Ressourcen

- [Microsoft: Safe storage of app secrets in development](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [.NET User Secrets CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-user-secrets)
- [MAUI SecureStorage](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage)
- [Azure Key Vault für Produktion](https://learn.microsoft.com/en-us/azure/key-vault/general/overview)

---

## Zusammenfassung

| Komponente | Token Quelle | Speichert im Code |
|-----------|-------------|-----------------|
| **Backend (Blazor Web)** | User Secrets | Nein - sicher in Secrets |
| **MAUI Client** | Hardkodiert | ✅ Ja - das ist okay! |
| **Sicherheit** | JWT Token nach Login | ✅ Das ist das wichtigste! |

**Der API-Token ist ein Gatekeeper, der JWT Token ist das Sicherheitsschloss!** 🔐
