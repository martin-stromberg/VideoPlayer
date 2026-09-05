# Offene Aufgaben

Erstellt am: 2026-09-06
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` meldet Status „Vollständig umgesetzt", keine offenen Planelemente.

## Code-Review-Befunde

- [ ] `VideoWebPlayer/Components/Playlists/PlaylistForm.razor` — `SaveAsync` erkennt einen Namenskonflikt per `ex.Message.Contains("existiert bereits", ...)`, obwohl `PlaylistDetail.razor` (dieselbe Codebasis, `VideoWebPlayerClient` setzt `HttpRequestException.StatusCode`) dafür konsequent `ex.StatusCode == HttpStatusCode.Forbidden/NotFound` nutzt. Zwei unterschiedliche, inkonsistente Fehlerbehandlungsstile für denselben Zweck; der String `"existiert bereits"` ist dadurch an drei Stellen (Service, Controller, UI) dupliziert. Empfehlung: `PlaylistForm.razor` auf denselben `StatusCode`-basierten Ansatz wie `PlaylistDetail.razor` umstellen.
- [ ] `VideoWebPlayer/Controllers/PlaylistsController.cs` — `MapInvalidOperationException(ex, conflictSubstring = "existiert bereits")` hat einen optionalen Parameter, der an allen vier Aufrufstellen nie überschrieben wird (Speculative Generality). Empfehlung: Optionalen Parameter entfernen und den Substring-Default fest verdrahten, sofern kein Aufrufer je einen abweichenden Wert benötigt.

## Fehlgeschlagene Tests

Keine — `test-results.md` meldet Status „Keine Fehler" (339/339 Tests bestanden).
