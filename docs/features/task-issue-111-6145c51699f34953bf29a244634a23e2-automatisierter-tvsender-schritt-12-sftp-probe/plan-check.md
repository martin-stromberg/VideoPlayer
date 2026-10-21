# Plan-Check: Seekbare Remote-Probes via `sftp://` (N8)

**Status: Plan vollständig**

## Prüfung gegen die Anforderung

| Anforderungspunkt | Abgedeckt in Plan? | Anmerkung |
|--------------------|--------------------|-----------|
| `sftp://`-Probe für `MediaSourceType.Sftp` mit Credentials | Ja — `SftpProbeUriBuilder.TryBuildUri` + `ProbeUriAsync` + Service-Verzweigung | URL-Format, Encoding, IPv6 und Port-Konditional festgelegt |
| Pfadauflösung konsistent zu `SftpMediaSourceReader` | Ja — `item.Path` (voller Remote-Pfad, `entry.FullName`) bzw. `collection.Path`-Kombination; relativ → `null` → Pipe-Fallback | In der Bestandsaufnahme verifiziert: `MediaItem.Path` = `entry.FullName`, `OpenFileStream` = `CombineSftpPath(collection.Path, item.Name)` — für gescannte Items äquivalent |
| URL-Encoding für Sonderzeichen in Credentials/Pfad | Ja — `Uri.EscapeDataString` je Komponente/Segment | |
| Passwort nie ins Log | Ja — maskierter `displayInput` + stderr-Sanitizing per Userinfo-Regex | Beide Log-Stellen (`{Input}`, stderr-Auszug) abgedeckt; Test `ProbeUriAsync_LogsDoNotContainPassword` |
| Sichtbarkeit in Prozess-Aufrufzeile — Entscheidung dokumentieren | Ja — Designentscheidung + Doku-Pflicht in `videolaufzeit.md` | libssh bietet keine Credential-Optionen außerhalb der URL (verifiziert: `-h protocol=sftp` listet nur `timeout`, `truncate`, `private_key`) |
| Klassifikation: `moov atom not found`/`Invalid data found` → permanent | Ja — nur bei `seekable` Inputs; `pipe:0` bleibt transient | Korrekte Differenzierung: gleiche stderr-Signatur entsteht bei Pipe auch durch Stream-Abbruch |
| Fallback ohne Credentials / wenn `sftp://` nicht klappt | Ja — zweistufig: nicht baubar → Pipe direkt; `Protocol not found` → `UriUnsupportedFailure` → Pipe im selben Versuch + prozessweites Flag | Verbrennt keinen Versuch |
| Stream-Verhalten (`LiveStreamInput`/`LiveStreamMediaResolver`) nicht ändern | Ja — explizit ausgenommen, in Seiteneffekte benannt | |

## Prüfung auf Testbedarf

- Unit-Tests für URL-Aufbau, Encoding, Maskierung, Klassifikation, Timeout-Wahl und `UriUnsupported`-Cache: geplant.
- Service-Tests auf echtem SQLite (Konvention des Projekts): Credentials-Pfad, beide Fallbacks, permanente Durchreichung: geplant.
- E2E: begründet entbehrlich (kein Nutzerfluss, kein SFTP-Server in der Testumgebung). Der Fehlklassifikations-Risikopunkt wird durch den Kontrast-Test `ProbeStreamAsync_MoovAtomNotFound_RemainsTransient` abgesichert.
- Bestehender Bruchpunkt benannt: `FakeDurationProbe` muss die neue Interface-Methode bekommen (Kompilierfehler sonst).

## E2E-Abdeckung

Keine E2E-Szenarien erforderlich — kein Benutzerfluss betroffen.

## Restrisiken / Hinweise an die Implementierung

- `MaskCredentialsInText` muss stderr maskieren **bevor** `Truncate` läuft (oder Truncate nach Maskierung — Reihenfolge in Implementierung festlegen: erst maskieren, dann kürzen).
- `Item.Path` kann theoretisch leer sein → `ResolveRemotePath` fällt auf `collection.Path`+`item.Name` zurück.
- `_uriProtocolUnsupported` als Instanz-Flag: Probe ist Singleton (`AddSingleton`), ausreichend; bei parallelen Probes kann das Flag von mehreren Tasks gesetzt werden — `bool`-Schreibzugriff ist atomar, Verhalten idempotent.
- Doku muss außerdem die geänderte Semantik von `StreamTimeoutSeconds` (gilt nur noch für den Pipe-Fallback) widerspiegeln.

→ Der Plan deckt die Anforderung vollständig und ohne Lücken ab.
