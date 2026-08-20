# Tests, Authentifizierung und Betrieb

## Tests

- [`VideoWebPlayer.Tests.csproj`](../../../../../../VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj) ist ein xUnit-v3-Testprojekt mit EF-InMemory/SQLite, MVC-Testhosting, Moq und Playwright.
- Vorhandene Tests decken vor allem `MediaSourceScanner`, `MediaSourceScanService`, Hintergrundbilder, Controllerautorisierung und Backups ab. Es gibt keine Tests für Metadaten-Editierung, Dirty-State, Kontextwechsel oder Scan-Überschreibschutz.
- [`MediaSourceScannerTests.cs`](../../../../../../VideoWebPlayer.Tests/Services/MediaSourceScannerTests.cs) und [`MediaSourceScanServiceTests.cs`](../../../../../../VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs) sind die passenden Ausgangspunkte für Regressionstests gegen spätere Scans.
- [`TVShowDetailsBackgroundImageUrlBuilderTests.cs`](../../../../../../VideoWebPlayer.Tests/TVShowDetailsBackgroundImageUrlBuilderTests.cs) zeigt, dass UI-nahe reine Hilfsmethoden über `InternalsVisibleTo` direkt getestet werden können; vollständige Blazor-Interaktion ist im Bestand nicht sichtbar.

## Autorisierung

- Bestehende API-Controller leiten von [`ApiBaseController.cs`](../../../../../../VideoWebPlayer/Controllers/ApiBaseController.cs) ab und verwenden den aktuellen Benutzer beziehungsweise Auth-Service für geschützte Aktionen.
- Die Anforderung nennt keine Rollen. Der Bestand enthält Identity-/Rolleninfrastruktur, aber keinen medienbezogenen Bearbeitungsanspruch. Die Berechtigungsentscheidung ist deshalb ein Planungs- und offener Anforderungspunkt.

## Betrieb und Persistenz

- Das EF-Modell wird über [`ApplicationDbContext.cs`](../../../../../../VideoWebPlayer/Data/ApplicationDbContext.cs), Konfigurationen und Migrationen persistiert.
- Eine Modelländerung für manuelle Metadaten erfordert eine neue Migration sowie Tests für bestehende Daten ohne Override-Wert.
