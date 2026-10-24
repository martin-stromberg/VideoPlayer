# Tasks: InternalMediaStreamController an das TV-Sender-Feature-Flag binden (Issue #259)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Entscheidung | Anwender-Entscheidung Variante A (Flag-Gate → 404) oder Variante B (bewusste Ausnahme + Kommentar) einholen — **entschieden: Variante A** | Fertig | — |
| 2 | Logik | `ProgramSettingsService` per Konstruktor in `InternalMediaStreamController` injizieren (`settings`-Parameter, `_settings`-Feld, XML-Doc; Vorbild `LiveStreamController` Z. 33) | Offen | — |
| 3 | Logik | Flag-Check in `Stream(...)`: `await _settings.GetTvSenderEnabledAsync(HttpContext.RequestAborted)` als erste Aktion vor dem Token-Resolve → `NotFound()` bei `false`, mit Begründungskommentar (Feature unsichtbar, Vorbild `ProgramGuideController` Z. 37–38) | Offen | — |
| 4 | Dokumentation | Klassen-Kommentar `InternalMediaStreamController` (Z. 8–17) ergänzen: Segmentierer-Pfad (`TryCreateStreamUri`, lange Token-Lebensdauer) nennen und die Flag-Bindung festhalten (Endpunkt folgt dem globalen Schalter wie die übrigen TV-Sender-Endpunkte) | Offen | — |
| 5 | Dokumentation | XML-Kommentar `Setup.TvSenderEnabled` (`Data/Setup.cs` Z. 80) angleichen: interner Media-Stream-Endpunkt in die Liste der abgedeckten Komponenten aufnehmen | Offen | — |
| 6 | Tests | Hilfsmethode `SetTvSenderEnabledAsync(bool)` in `InternalMediaStreamControllerTests` anlegen und in `IssueProbeUriAsync` mit `true` aufrufen | Offen | — |
| 7 | Tests | Regressionstest `Stream_FeatureDisabled_ValidToken_Returns404` in `InternalMediaStreamControllerTests` (Flag aus + gültiges Token → 404) | Offen | — |
| 8 | Tests | `MediaProbeHttpIntegrationTests` (`Category=E2E`): `TvSenderEnabled = true` in der Test-DB seeden (`SeedMediaItemAsync` bzw. `InitializeAsync`), damit Kestrel-Tests weiterhin 200/206 erhalten | Offen | — |
| 9 | Dokumentation | Eintrag in `docs/RELEASE_NOTES.md` (englischer + deutscher Abschnitt) | Offen | — |
| 10 | Dokumentation | `docs/help/tv-sender.md` Z. 15 („Ohne Freigabe antworten die Endpunkte mit 404") gegen den finalen Stand prüfen und ggf. präzisieren | Offen | — |
| 11 | Regression | `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
