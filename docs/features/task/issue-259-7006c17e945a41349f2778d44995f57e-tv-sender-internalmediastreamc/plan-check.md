# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK 1: Entscheidung dokumentiert — Variante A als Gate-Implementierung nachvollziehbar im Code | Schritt 1: `GetTvSenderEnabledAsync(HttpContext.RequestAborted)` → `NotFound()` als erste Aktion in `Stream` mit kurzem Begründungskommentar; Schritt 2: Klassen-Kommentar (`InternalMediaStreamController.cs` Z. 8–17) um Segmentierer-Pfad (`TryCreateStreamUri`) und Flag-Bindung ergänzt, XML-Kommentar `Setup.TvSenderEnabled` (`Setup.cs` Z. 72–80) nimmt den internen Endpunkt in die Liste der abgedeckten Komponenten auf — alle drei Orte im Code verifiziert | Entscheidung ist Code-/Kommentar-Umsetzung; Nachweis erfolgt über das Gate samt Regressionstest (s. AK 2) | Abgedeckt |
| AK 2 (Variante A): `Stream` prüft `GetTvSenderEnabledAsync` → 404 bei deaktiviertem Flag, konsistent zu `/api/live-stream/*` und `/api/program-guide/*` | Schritt 1: `ProgramSettingsService` per Konstruktor injizieren (scoped-Service, `Extensions/ServiceCollectionExtensions.cs` Z. 267 verifiziert; Vorbilder `LiveStreamController` Z. 33/55–57 und `ProgramGuideController` Z. 30/37–40 verifiziert), Check vor `_tokens.ResolveMediaItemId` (Z. 57) — vom Anwender gewählte Position, konsistent zum „indistinguishable from does not exist"-Muster | Neuer Test `Stream_FeatureDisabled_ValidToken_Returns404` (Flag aus + gültiges Token → 404); „Flag an + gültiges Token → 200 inkl. Range" durch `Stream_ValidToken_ReturnsFile` und `Stream_ValidToken_RangeRequest_Returns206PartialContent` nach Flag-Seed `true` abgesichert — Testnamen und `IssueProbeUriAsync`-Hilfsmethode in `InternalMediaStreamControllerTests.cs` (Z. 55–155) verifiziert | Abgedeckt |
| AK 3 (Variante B): begründende Ausnahme-Kommentierung | Entfällt — Anwender hat Variante A entschieden (in plan.md „Designentscheidungen" festgehalten) | — | Nicht erforderlich mit Begründung (Variante A gewählt) |

## Fehlende oder unvollständige Testanforderungen

Keine — Status `Plan vollständig`.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| AK 2 — Flag-Gate am internen Endpunkt (kein UI-/Nutzerfluss: Loopback-Endpunkt, nur serverseitige Aufrufer mit nicht erratbaren Tokens) | Kein neuer E2E-Test; Begründung im Plan (Z. 160–167) nachvollziehbar: kein anwendersichtbarer Benutzerfluss. HTTP-Ebene durch angepasste `MediaProbeHttpIntegrationTests` (`Category=E2E`, echter Kestrel-Host, verifiziert Z. 24/61–67) mit Flag-Seed als E2E-Regression „Flag an → Endpunkt liefert" abgedeckt; der geforderte Negativnachweis läuft auf HTTP-Ebene über `WebApplicationFactory`/`TestServer` (voller Middleware-/DI-Stack, kein reiner Unit-Test) | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

Keine — Status `Plan vollständig`.

## Hinweise

- `Stream_InvalidToken_Returns404` ruft `IssueProbeUriAsync` nicht auf und erhält damit kein Flag-Seed — der Test bleibt trotzdem grün, da das Gate bei fehlendem `Setup`-Datensatz (`GetOrCreateSetupAsync`, Default `TvSenderEnabled = false`, verifiziert `ProgramSettingsService.cs` Z. 167–171) ebenfalls 404 liefert. Die Planformulierung „die reinen 404-Tests … laufen einheitlich mit gesetztem Flag" ist für diesen Test ungenau, aber ohne Konsequenz für das Ergebnis.
- Vollständigkeit der betroffenen-Tests-Liste verifiziert: Außer den sechs genannten Tests referenzieren weitere Tests (`MediaDurationProbeServiceTests`, `InternalMediaStreamTokenServiceTests`, `FfprobeMediaDurationProbeTests`, `FfmpegLiveStreamSegmenterTests`, `LiveStreamMediaResolverTests`, `LiveStreamCoordinatorTests`) die URL `/api/internal/media-stream/…` nur als Stub-/Zeichenketten-Daten ohne echte HTTP-Anfrage — kein Flag-Seed nötig.
- Konstruktor-Erweiterung risikofrei: `InternalMediaStreamController` wird im gesamten Repo nur über DI aufgelöst (kein `new InternalMediaStreamController(…)` außerhalb der Klasse selbst).
- Risikobeschreibungen im Plan stimmen mit dem Code überein: HTTP→Pipe-Fallback je Slot in `LiveStreamCoordinator` Z. 111–129, HTTP-Transportfehler → `UriUnsupportedFailure` in `FfprobeMediaDurationProbe` Z. 192–198.
- `docs/help/tv-sender.md` Z. 15–16 („Ohne Freigabe antworten die Endpunkte mit 404") wird durch das Gate vollständig zutreffend — Plan sieht nur eine Prüfung des Absatzes vor, das ist ausreichend, da der interne Endpunkt in der Benutzerdoku bewusst nicht namentlich aufgeführt ist.
- Optional (kein Muss): Ein zusätzlicher E2E-Negativtest `InternalStream_FeatureDisabled_Returns404` in `MediaProbeHttpIntegrationTests` (echter Kestrel) würde das Gate auch auf echter Transportebene belegen; die Anforderung fordert den Regressionstest nicht auf dieser Ebene.
