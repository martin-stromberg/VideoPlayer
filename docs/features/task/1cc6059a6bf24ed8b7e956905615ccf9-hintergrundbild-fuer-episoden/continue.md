# Offene Aufgaben

Erstellt am: 2026-08-10
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor` — `SelectSeason()` ruft anders als `SelectEpisode()` und `OnInitializedAsync()` nicht `EnsureEpisodeBackgroundImageAsync()` auf. Beim Wechsel zu einer Staffel, deren erste Episode noch kein generiertes Hintergrundbild hat, wird dieses nicht lazy generiert, sondern es wird stillschweigend auf das Banner zurückgefallen.
- [ ] `VideoWebPlayer.Tests/TVShowDetailsBackgroundImageUrlTests.cs` — Der neue Regressionstest für den access_token-Fix nutzt Reflection, um eine private Methode aufzurufen und private Felder der Razor-Komponente zu setzen (einziger derartiger Testfall im gesamten Testprojekt) und weicht damit vom etablierten Muster (z. B. `WebApplicationFactory`-basierte Tests) ab; er testet Implementierungsdetails statt beobachtbares Verhalten.
- [ ] `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataProvider.cs` — `BuildColumnSelectExpression`/`BuildTableFilter` vergleichen Tabellen-/Spaltennamen über hartkodierte String-Literale statt über `nameof(...)`, was riskant ist, da genau diese Spalten im Rahmen dieses Features bereits einmal umbenannt wurden.

## Fehlgeschlagene Tests

Keine.
