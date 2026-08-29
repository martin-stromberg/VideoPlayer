# E2E- und Integrationstests

## Vorhandene Teststruktur

`VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj:12-28` referenziert `Microsoft.AspNetCore.Mvc.Testing`, EF Core InMemory/SQLite und `Microsoft.Playwright`. Die Playwright-E2E-Tests starten die Anwendung mit `WebApplicationFactory`, Kestrel und einem zufaelligen Testdatenbankpfad.

## Quellenbezogene Tests

- `VideoWebPlayer.Tests/UnlockedSourceE2ETests.cs:24-151` ist der naechste vorhandene UI-Test. Er initialisiert Playwright, meldet sich an, prueft einen Quellenlink im Menue und oeffnet `/mediasource/1`. Danach prueft er Heading, Anzahl und Titel eines Eintrags.
- `VideoWebPlayer.Tests/MediaSourceDeleteE2ETests.cs` prueft die administrative Loeschung einer Quelle, nicht die Quellenansicht.
- `VideoWebPlayer.Tests/Controllers/SourceVisibilityControllerTests.cs` prueft Quellen-/Sichtbarkeitslogik auf Controller-Ebene, nicht den Blazor-Komponentenwechsel.
- Die uebrigen E2E-Tests betreffen unter anderem About, Continue Watching, MediaBox-Kontextmenue, Metadaten, Freigaben und Updates.

## Abdeckungsluecke

Es gibt keinen Test, der nach der Anmeldung Quelle 1 ueber das Menue oeffnet, deren Titel prueft, anschliessend Quelle 2 ueber dasselbe Menue oeffnet und zugleich die Titel der zweiten Quelle sowie das Verschwinden der Titel der ersten Quelle prueft. Es gibt auch keinen isolierten Test fuer `MediaSourceDetailsViewModel.InitializeAsync` bei wiederholter Initialisierung mit zwei Quellen.

## Testvoraussetzungen fuer die Anforderung

Der neue Playwright-Test kann das Muster aus `UnlockedSourceE2ETests` uebernehmen: Testserver starten, zwei Quellen und eindeutig benannte Titel seed-en, Benutzer anmelden, Menue-Links ueber `nav .nav-link` lokalisieren, auf die erste und zweite Quelle klicken und `.media-title-text` pruefen. Fuer die Aussage "alte Titel nicht mehr sichtbar" muss nach dem zweiten Seitenaufbau explizit auf `not.toContainText` bzw. eine Locator-Anzahl/Locator-Inhalt fuer Quelle-1-Titel geprueft werden.
