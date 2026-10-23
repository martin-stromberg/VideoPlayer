# Plan-Check — Schritt 27 (N26–N28)

**Status: Plan vollständig**

## Abdeckung Anforderung ↔ Plan

| Anforderung | Planelement | Deckung |
|-------------|-------------|---------|
| N26 Invariante Formatierung + Format-Test | N26-1..3 | ✅ inkl. der zwei int-basierten Fundstellen bewusst ausgenommen + VideoPlayer.razor als latent gleicher Bug mit erfasst |
| N27 Tages-Header + Divider bei Regelwechsel + bUnit | N27-1..3 | ✅ lokale Datumsgrenze entspricht der angezeigten Zeit |
| N28 Allowlist-Tabelle statt Setup-Feld | N28-1 | ✅ Begründung im Plan |
| N28 Admin-UI Benutzerauswahl | N28-5 | ✅ ProgramSettings.razor (beim globalen Schalter) |
| N28 serverseitige Prüfung NavMenu/Seiten/Controller | N28-4 | ✅ inkl. 404 (Flag) vs. 403 (User) Trennung |
| N28 Admin implizit | N28-3, Tests | ✅ |
| N28 Migration + OptionalRestore | N28-2, N28-7 | ✅ |
| N28 User-Löschung räumt Zeilen | N28-6 | ✅ |

## Testbedarf

- Neue Tests laufen gegen echtes SQLite (FK auf AspNetUsers wird erzwungen —
  `ProgramScheduleTestDb`/`PairingTestDb`), bUnit für Markup (N27, NavMenu, LiveTv).
- Alt-Backup-Regressionstest für die neue Tabelle vorhanden (Muster
  `ProgramRuleLinks`).
- xUnit1051: `TestContext.Current.CancellationToken` überall.

## Risiken / Hinweise

- `ApplicationDbContext.cs` enthält ein nicht-UTF-8-Byte (`für` als cp1252 in einem
  Kommentar) — DbSet-Zeile wird byte-sicher eingefügt bzw. das Byte beim Editieren zu
  UTF-8 repariert (Kommentar-Semantik unverändert).
- `dotnet ef` baut das Projekt; läuft eine Benutzer-App, sperrt sie `bin/Debug` —
  dann Migration manuell (wie Schritt 23/24 dokumentiert) oder mit Scratch-OutDir.
- Keine neuen NuGet-Pakete.
