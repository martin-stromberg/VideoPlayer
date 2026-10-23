# Code-Review — Schritt 27 (N26–N28)

**Status: Keine Befunde**

Geprüft selbstkritisch:

- `CssFormat.Percent` ist die einzige Stelle, die Kultur-unabhängig in CSS schreibt; beide
  double-Fundstellen (`LiveTv.razor`, `VideoPlayer.razor`) nutzen sie. int-basierte Stellen
  brauchen keinen Fix.
- N27-Gruppierung nutzt `ToLocalTime().Date` konsistent zur angezeigten `dd.MM.` Uhrzeit;
  `previousRuleId`-Reset je Tag verhindert Divider am Tagesanfang. `schedule` ist nach `Start`
  sortiert → Tage und Slots in Reihenfolge.
- N28: `IsTvSenderAccessGrantedAsync` wertet Flag zuerst (kein Allowlist-Query nötig, wenn aus),
  Admin implizit, danach `AnyAsync` auf `UserId`-PK. Replace-Update lädt nur betroffene Zeilen;
  ein SaveChanges → atomar.
- Serverseitig: 404 (Flag) vs. 403 (User) trennt „Feature nicht vorhanden" sauber von
  „kein Zugriff"; `CheckLoggedIn()` bleibt erste Stufe (401).
- Blazor-Seiten prüfen Claims (`IsAdmin`, `NameIdentifier`); die API nutzt `CurrentUser.IsAdmin`
  aus der DB — authoritative.
- `UserManagement.DeleteUser` entfernt Freigabezeilen wie die übrigen userbezogenen Daten
  (zusätzlich zur FK-Cascade).
- Migration wurde per `dotnet ef` erzeugt (Build lief — kein Lock-Konflikt).
- `ApplicationDbContext.cs` enthält ein Alt-cp1252-Byte in einem Kommentar; der DbSet wurde
  byte-sicher eingefügt, Datei sonst unverändert.
