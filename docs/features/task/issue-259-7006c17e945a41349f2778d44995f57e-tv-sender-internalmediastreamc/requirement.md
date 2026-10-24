# Übersetzte Anforderung: InternalMediaStreamController an das TV-Sender-Feature-Flag binden oder Ausnahme begründen

Quelle: Kundenanforderung Issue #259 (Nachbesserung aus Issue #111 / PR #257), siehe Aufgabenbeschreibung.

## Fachliche Zusammenfassung

Der interne, token-gesicherte Media-Stream-Endpunkt `GET /api/internal/media-stream/{token}` (`InternalMediaStreamController.Stream`) ist die einzige Komponente des automatisierten TV-Senders, die das globale Feature-Flag `Setup.TvSenderEnabled` nicht prüft: Während der öffentliche Live-Stream (`LiveStreamController`), die Programmauskunft (`ProgramGuideController`) und alle Sender-Worker (`MediaDurationProbeWorker`, `LiveStreamWorker`, `ProgramScheduleWorker`) bei deaktiviertem Flag mit 404 antworten bzw. ruhen, bleibt der interne Endpunkt ohne Flag-Prüfung erreichbar. Es ist zu entscheiden, ob der Endpunkt analog `LiveStreamController` (`GetTvSenderEnabledAsync` → 404) an das Flag gebunden wird oder ob die Abweichung bewusst bestehen bleibt und im Code begründet dokumentiert wird. Die Entscheidung ist anschließend im Code festzuhalten; bei Flag-Bindung ist ein Regressionstest gefordert.

## Akzeptanzkriterien

Die konkreten Kriterien hängen von der zu treffenden Entscheidung ab (siehe „Offene Fragen"):

1. **Entscheidung dokumentiert:** Die getroffene Entscheidung (Variante A oder B) steht nachvollziehbar im Code — bei Variante A als Gate-Implementierung, bei Variante B als begründender Kommentar zur bewussten Ausnahme (z. B. im Klassen-Kommentar des `InternalMediaStreamController`, wo die bisherige Begründung bereits angedeutet ist).
2. **Bei Variante A (Flag-Bindung):** `Stream(...)` prüft `GetTvSenderEnabledAsync` und antwortet bei deaktiviertem Flag mit 404 — konsistent zum Verhalten von `/api/live-stream/*` und `/api/program-guide/*`. Ein Regressionstest belegt: gültiges Token + deaktiviertes Flag → 404; gültiges Token + aktiviertes Flag → weiterhin 200 (inkl. Range-Verhalten).
3. **Bei Variante B (bewusste Ausnahme):** Das Verhalten bleibt unverändert; der Kommentar begründet, warum der interne Endpunkt sich bewusst nicht an die globale Freischaltung hält (kurzlebige, nicht erratbare, serverseitig ausgestellte Tokens als einzige Berechtigung; Vermeidung des Abbruchs laufender Probes/Stream-Eingaben beim Flag-Wechsel).

## Betroffene Klassen und Komponenten

- **Bestehend (zu ändern bzw. zu kommentieren):** `VideoWebPlayer/Controllers/InternalMediaStreamController.cs` — Methode `Stream(string token, CancellationToken)`; ggf. Klassen-Kommentar.
- **Bestehend (genutzt, nur bei Variante A):** `ProgramSettingsService.GetTvSenderEnabledAsync` (`VideoWebPlayer/Services/ProgramSettingsService.cs`, Z. 167) — der Controller müsste `ProgramSettingsService` zusätzlich injiziert bekommen (Vorbild: `LiveStreamController` Z. 33/56, `ProgramGuideController` Z. 30/40).
- **Bestehend (Kontext, unverändert):** `IInternalMediaStreamTokenService`/`InternalMediaStreamTokenService` (Token-Ausstellung und -Auflösung; `TryCreateProbeUri` für kurzlebige Probe-Tokens, `TryCreateStreamUri` für langlebige Tokens des Live-Stream-Segmentierers), `MediaDurationProbeWorker` (Flag-Prüfung pro Zyklus, Z. 65), `LiveStreamWorker` (Flag-Prüfung pro Tick, stoppt laufenden Segmentierer, Z. 73–79), `LiveStreamMediaResolver` (stellt Tokens für die ffmpeg-HTTP-Eingabe des Segmentierers aus, Z. 127).
- **Tests:** `VideoWebPlayer.Tests/InternalMediaStreamControllerTests.cs` — bei Variante A neuer Regressionstest (Flag aus + gültiges Token → 404) sowie Anpassung der bestehenden Tests, damit `Setup.TvSenderEnabled` in der Testdatenbank aktiviert ist (z. B. über `UpdateTvSenderEnabledAsync`); bestehende Tests (`Stream_InvalidToken_Returns404`, `Stream_ValidToken_ReturnsFile`, Range-Test) müssen grün bleiben.

## Implementierungsansatz

- **Variante A:** In `Stream(...)` nach dem Token-Resolve (oder davor, analog `LiveStreamController` Z. 56) `await _settings.GetTvSenderEnabledAsync(HttpContext.RequestAborted)` prüfen und bei `false` `NotFound()` liefern. Dazu `ProgramSettingsService` per Konstruktor injizieren. Die Reihenfolge relativ zum Token-Resolve ist in der Planung festzulegen: Ein Check vor dem Token-Resolve beantwortet auch ungültige Tokens mit 404 (verhält sich wie „Endpunkt existiert nicht" — konsistent zur Begründung im `ProgramGuideController`, Z. 37–38); ein Check nach dem Token-Resolve lässt die 404-Antwort für ungültige Tokens unverändert. `GetTvSenderEnabledAsync` legt den `Setup`-Datensatz bei Bedarf an (`GetOrCreateSetupAsync`) — eine DB-Abfrage pro Request, wie bei den übrigen Flag-Gates.
- **Variante B:** Keine Codeänderung an der Ablauflogik; der Klassen- bzw. Methoden-Kommentar wird um die explizite Begründung ergänzt, warum das Flag bewusst nicht geprüft wird.
- **Zu beachtende Nebenbedingung (für beide Varianten relevant):** Der Endpunkt wird nicht nur vom kurzlebigen Laufzeit-Probe (`ffprobe`) genutzt, sondern auch vom Live-Stream-Segmentierer über `TryCreateStreamUri` mit langer Token-Lebensdauer (restliche Sendeplatz-Laufzeit + 15 Minuten Marge, Fallback 45 Minuten — `LiveStreamMediaResolver` Z. 24–27, 124–127). Bei Variante A würde ein Flag-Wechsel während eines laufenden ffmpeg-Reads den Segmentierer mit einem HTTP-Fehler abbrechen, bevor der `LiveStreamWorker` ihn im nächsten Tick regulär stoppt; ebenso bräche eine laufende Probe mitten im Seek ab. Bei Variante B bleiben bereits ausgestellte Tokens bis zu ihrem Ablauf gültig — für Stream-Tokens des Segmentierers kann das Stunden statt Minuten bedeuten (die Formulierung „kurzlebig" aus der Anforderung trifft nur auf Probe-Tokens uneingeschränkt zu).

## Konfiguration

Keine neue Konfiguration — es wird ausschließlich der bestehende Admin-Schalter `Setup.TvSenderEnabled` (`ProgramSettingsService.GetTvSenderEnabledAsync`/`UpdateTvSenderEnabledAsync`, Admin-Oberfläche „Einrichtung → Allgemein → Automatisierten TV-Sender freischalten") herangezogen bzw. die bewusste Ausnahme davon dokumentiert.

## Offene Fragen

1. **Zu entscheiden durch den Anwender — Variante A oder Variante B?**
   - **Variante A:** Flag-Prüfung wie in `LiveStreamController` (Z. 56) nachrüsten; bei deaktiviertem `TvSenderEnabled` antwortet der Endpunkt 404. Vorteil: Konsistenz — beim Abschalten des Senders sind alle zugehörigen Endpunkte geschlossen. Nachteil: Laufende Probes und insbesondere die ffmpeg-HTTP-Eingabe des Segmentierers (`TryCreateStreamUri`, lange Token-Lebensdauer) brechen beim Flag-Wechsel mitten im Stream ab, statt regulär über den Worker gestoppt zu werden.
   - **Variante B:** Endpunkt bleibt bewusst ohne Flag-Prüfung; einzige Berechtigung bleibt das nicht erratbare, ablaufende, nur vom Server ausgestellte Token. Vorteil: Keine harten Abbrüche laufender Lesevorgänge; ohne laufende Worker werden keine neuen Tokens erzeugt. Nachteil: Inkonsistenz — der Endpunkt bleibt mit noch gültigem Token erreichbar, obwohl das Feature abgeschaltet ist (für Segmentierer-Tokens potenziell stundenlang).
2. **Falls Variante A:** Soll der Flag-Check vor oder nach dem Token-Resolve erfolgen? (Prüfung zuerst → jedes ungültige Token bekommt ebenfalls 404, Feature ist unsichtbar wie bei den anderen Gates; Token-Resolve zuerst → minimaler Diff, Verhalten für ungültige Tokens unverändert.)
