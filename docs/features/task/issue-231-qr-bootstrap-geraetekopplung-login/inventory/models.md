# Datenmodell — Bestandsaufnahme

## `PairingCode`
Datei: `VideoWebPlayer/Data/PairingCode.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel |
| `CodeHash` | `string` | SHA-256-Hex des Klartext-Codes (max. 128 Zeichen, Index) |
| `CreatedAtUtc` | `DateTime` | Erstellungszeitpunkt |
| `ExpiresAtUtc` | `DateTime` | Ablaufzeitpunkt (Index) |
| `ConsumedAtUtc` | `DateTime?` | Verbrauchszeitpunkt; `null` = aktiv |
| `CreatedByUserId` | `string?` | Ersteller (bisher Admin; max. 64 Zeichen) |

Fehlt: Richtungs-/Art-Feld (`Kind`), zweiter Hash für ein längeres Ticket-Secret. `CodeHash` ist nicht unique.

## `PairedDevice`
Datei: `VideoWebPlayer/Data/PairedDevice.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel |
| `Name` | `string` | Anzeigename (max. 200) |
| `TokenHash` | `string` | SHA-256 des Geräte-Tokens (unique Index) |
| `IssuedAtUtc` | `DateTime` | Ausstellungszeitpunkt |
| `LastUsedAtUtc` | `DateTime?` | Letzte Gate-Nutzung |
| `RevokedAtUtc` | `DateTime?` | Widerruf (Index) |
| `CreatedByUserId` | `string?` | Ersteller des Pairing-Codes |

## `ApplicationUser`
Datei: `VideoWebPlayer/Data/ApplicationUser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Sources` | `string` | Serialisierte Quellenfreigaben |
| `IsAdmin` | `bool` | Admin-Flag → `IsAdmin`-Claim |

Erbt `IdentityUser` (`Id`, `Email`, `UserName`).

## `BlockedLoginIp`
Datei: `VideoWebPlayer/Data/BlockedLoginIp.cs` — IP-Sperrliste (wird von `ILoginIpBlockService` gepflegt; geteilter Schutz für Login und `exchange`).

## `RefreshToken` — **nicht vorhanden**, neu anzulegen.

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

- `DbSet<PairedDevice>` (Z. ~157), `DbSet<PairingCode>` (Z. ~161), `DbSet<BlockedLoginIp>` u. a.
- `OnModelCreating` wendet `ApplyConfigurationsFromAssembly` an — neue Entitäten brauchen eine `IEntityTypeConfiguration` unter `Data/Configurations/`.
- Migrationen liegen committed unter `VideoWebPlayer/Migrations/` (letzte: `20260923074815_AddDevicePairing`); `MigrateDatabase()` wendet sie beim Start an. `dotnet-ef` 10.0.10 ist als Global-Tool installiert.
