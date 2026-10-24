# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

Bewusst geprüft und als unauffällig bewertet:

- **`MediaDurationProbeService.GetProgressAsync`**: vier `CountAsync` + `MaxAsync` + Top-N-
  Projektion — bewusst getrennte Queries statt `GroupBy` mit bedingtem Schlüssel
  (SQLite-Übersetzungssicherheit); alle auf gezählten/indizierten Spalten (`DurationProbedAt`
  ist indiziert). Kein DbContext-Materialisieren des Gesamtbestands. `recentCount <= 0`
  geclamped (Take(0)-Semantik von SQLite umgangen).
- **`RequireAdminAsync`-Duplikation**: dritte Kopie des 6-Zeilen-Musters — die beiden
  bestehenden Services halten es ebenfalls privat; eine Extraktion wäre eine Änderung
  außerhalb des Auftrags. Konsistent, kein Befund.
- **`TVSenderAdmin.razor` Refresh-Loop**: `PeriodicTimer` + `CancellationTokenSource` —
  gleiches Muster wie `LiveTv.razor`. `catch (Exception)` ist absichtlich breit
  (Einzelfehler darf die Schleife nicht töten) und kommentiert; `OperationCanceledException`
  wird vorher sauber weitergegeben. `Dispose` ruft nur `Cancel()` — kein `Dispose` der
  Quelle, damit ein laufender `WaitForNextTickAsync` nicht in eine ObjectDisposed-Race läuft.
- **Geteilter scoped `DbContext`**: `ProbeService` und das injizierte `DbContext`-Feld der
  Seite teilen eine Context-Instanz; eine Timer-Kollision mit einem UI-Handler erzeugt eine
  transiente `InvalidOperationException`, die die Schleife abfängt — gleiches Risikoprofil
  wie der bestehende `LiveTv.razor`-Timer.
- **Records**: `sealed record` mit `IReadOnlyList` (null-sicherer als `record struct` mit
  Listenfeld); XML-Dokumentation wie üblich; Ablage neben `MediaDurationProbeResult.cs`.
- **Tests**: AAA-Struktur, ein Verhalten pro Test, echtes In-Memory-SQLite,
  `TestContext.Current.CancellationToken` (kein xUnit1051), private Stubs ohne Seiteneffekte.
- **E2E-Erweiterung**: konsolidiert im bestehenden Test (Projektregel: minimale FlaUI/Playwright-
  Laufzeit); Seeds wählen bewusst nicht-ausstehende Zustände, damit der mitlaufende
  `MediaDurationProbeWorker` die Erwartungen nicht verändert.

## Geprüfte Dateien

- `VideoWebPlayer/Services/MediaDurationProbeProgress.cs` (neu)
- `VideoWebPlayer/Services/MediaDurationProbeService.cs` (geändert)
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (geändert)
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs` (neu)
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` (geändert)
