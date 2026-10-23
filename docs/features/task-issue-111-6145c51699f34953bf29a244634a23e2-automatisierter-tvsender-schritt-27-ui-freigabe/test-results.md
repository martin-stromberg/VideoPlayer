# Testergebnisse — Schritt 27 (N26–N28)

**Status: Keine Fehler**

- Build `VideoPlayer.sln`: Debug **0 Fehler** (144 Warnungen, alle bekannt/alt),
  Release **0 Fehler** (144 Warnungen).
- Volle Suite `dotnet test VideoPlayer.sln -c Debug`: **2210 erfolgreich, 0 Fehler,
  1 übersprungen** (Alt-Skip, bekannt) + 6/6 MarkdownLinkCheck.Tests grün.
- Fokus-Lauf vorab: 64/64 der betroffenen Testsuiten (CssFormat, LiveTv, NavMenuLiveTv,
  TVSenderAdmin, ProgramGuideController, LiveStreamController, ProgramSettingsService,
  TvSenderUserAccess, Backup-TvSenderUserAccess) grün.
- Ein vorab gefundener Selbstfehler: `AccessRow_RequiresExistingUser_FkEnforced` erwartete
  `DbUpdateException`, `SaveDiagAsync` verpackt sie — auf direktes `SaveChangesAsync`
  umgestellt (FK wird tatsächlich erzwungen, reale SQLite).
- Neu: +17 Tests (2× CssFormat/N26-Format, 1× N27-Tagesgliederung, 3× Service-Matrix,
  2× ProgramGuide-403/Admin, 2× LiveStream-403/Admin, 1× LiveTv-Hinweis, 2× NavMenu,
  2× Backup-Regression). E2E-Testsuites mit TV-Sender-Zugriff wurden angepasst
  (`grantsTvAccess: true` / UserA-Zeile), damit sie die reale Freigabe spiegeln.

## Fehlgeschlagene Tests

Keine.
