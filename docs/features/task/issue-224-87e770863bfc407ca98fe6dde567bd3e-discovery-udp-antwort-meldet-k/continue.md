# Offene Aufgaben

Erstellt am: 2026-10-05
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `UdpDiscoveryListener.cs` (`UdpDiscoveryListener`) — **Fehlerbehandlung – CancellationToken wird nicht an den blockierenden Empfang durchgereicht**: `udp.ReceiveAsync()` (Zeile 59) wird ohne `CancellationToken` aufgerufen. `Stop()` cancelled `_cts`, weckt den blockierenden Empfang aber nicht auf; die Schleife terminiert erst beim nächsten Datagramm. `_cts` wird nach `Cancel()` nie disposed und `udp` nicht geschlossen. Empfehlung: `udp.ReceiveAsync(cancellationToken)` verwenden (Overload mit `ValueTask<UdpReceiveResult>` unter net10.0 verfügbar) oder Socket beim Stoppen schließen; `_cts` nach `Cancel()` disposen.
- [ ] `UdpDiscoveryListener.cs` (`UdpDiscoveryListener`) — **Fehlerbehandlung – Bind-Fehler faulted die Hintergrund-Task unbeobachtet**: `new UdpClient(_port)` (Zeile 54) steht außerhalb der `try`-Blöcke; schlägt das Binden fehl (Port 5001 belegt), faulted die `Task.Run`-Task unbeobachtet und unprotokolliert. Empfehlung: Binden in try-Block in `ListenAsync` verlegen und `_logger.LogError` ausgeben.
- [ ] `DiscoveryResponseBuilder.cs` (`DiscoveryResponseBuilder`) — **Toter Code – redundante Literal-Prüfungen**: `trimmed is "*" or "+" or "0.0.0.0" or "[::]"` (Zeile 172) — die IP-Literale werden ohnehin von `IPAddress.TryParse` + `Any`/`IPv6Any`-Prüfungen abgelehnt; nur `"*"` und `"+"` sind nötig. Empfehlung: IP-Literale aus dem Pattern entfernen oder als Fast-Path kommentieren.

## Usability-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.
