# Offene Aufgaben

Erstellt am: 2026-09-22
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `MediaSourceScannerLocalTests.cs` — Ressourcen-Handling: `CreateServiceProviderAsync` gibt einen `ServiceProvider` zurück, den keiner der drei Aufrufer disposed; die Keeper-`SqliteConnection` wird geöffnet, aber weder im `ServiceCollection` registriert noch disposed. Empfehlung: `keeperConnection` per `services.AddSingleton(keeperConnection)` registrieren und `ServiceProvider` per `await using`/`finally` disposen (Muster aus `LocalMediaSourceClassifierTests`).
- [ ] `ItemsControllerLocalSourceTests.cs` — Ressourcen-Handling: In `CreateControllerWithLocalMovieAsync` werden Keeper-`SqliteConnection` und `ServiceProvider` nie disposed; Rückgabetupel erlaubt kein Cleanup. Empfehlung: Connection per `AddSingleton` registrieren, Provider als Tuple-Element zurückgeben oder in `Dispose()` freigeben.
- [ ] `MediaSourceReaderDispatcher.cs` — Fehlerbehandlung: `GetReader(collection?.MediaSource)` bildet ein nicht geladenes `MediaSource`-Navigation-Property still auf den SFTP-Reader ab → falsche Auswahl für `LocalDirectory`-Collections, Fehler erst als `NullReferenceException` sichtbar. Empfehlung: Vorbedingung prüfen und bei `collection?.MediaSource is null` eine `InvalidOperationException` mit Hinweis auf erforderliches eager loading werfen.
- [ ] `LocalMediaSourceReaderTests.cs` — Ressourcen-Handling: Das im Konstruktor angelegte Verzeichnis `vwp-localreader-outside` unter `Path.GetTempPath()` wird in `Dispose()` nie gelöscht → Temp-Artefakt. Empfehlung: In `Dispose()` ebenfalls löschen (selbes `try/catch`-Muster wie `_baseDir`).

## Usability-Befunde

- [ ] `MediaSourceAdminDetails.razor` — Erreichbarkeit: Feld „Lokales Verzeichnis" erklärt nicht, dass der Pfad auf dem Server (nicht auf dem Rechner der Administratorin) existieren muss. Empfehlung: Hinweistext unter dem Pfad-Feld mit Server-Bezug und Beispiel (z. B. „Absoluter Pfad zu einem Verzeichnis auf dem Server, auf dem der VideoWebPlayer läuft (z. B. `D:\Medien` oder `/mnt/medien`)"); Fehlermeldungen präzisieren („…existiert nicht auf dem Server.", „…der Serverprozess hat keine Leseberechtigung.").
- [ ] `MediaSourceAdminDetails.razor` — Erreichbarkeit: Quelltyp-Auswahl ist beim Bearbeiten deaktiviert ohne Erklärung; bei falsch gewähltem Typ bleibt nur Löschen und Neuanlegen. Empfehlung: Hinweistext am deaktivierten Feld, z. B. „Der Quelltyp kann nach dem Anlegen nicht geändert werden."

## Fehlgeschlagene Tests

Keine.
