← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — Business Rules

## Vorrangkette der gemeldeten Basis-URL

**Beschreibung:** Welche URL die `VIDEOWEBPLAYER_SERVER`-Antwort enthält, wird pro Anfrage neu entschieden — nicht beim Start und nicht gecacht.

**Bedingungen:**
- `DiscoveryResponseBuilder.Build` wertet die Eingaben in fester Reihenfolge aus.

**Verhalten:**
1. `Setups.DiscoveryPublicBaseUrl` (Admin-Wert) — normalisiert und gültig → gemeldet.
2. `Discovery:PublicBaseUrl` (Betreiberkonfiguration) — gesetzt → gemeldet.
3. Beide leer → automatische Ableitung `scheme://host:port`.

**Umsetzung:** `DiscoveryResponseBuilder.Build`; der Admin-Wert wird je Anfrage in einem eigenen DI-Scope über `ProgramSettingsService.GetDiscoveryPublicBaseUrlAsync` gelesen (`DiscoveryBaseUrlResolver`). Deshalb wirken Admin-Änderungen sofort, ohne Neustart.

## Loopback-, Wildcard- und IPv6-Verbot

**Beschreibung:** Adressen, die ein LAN-Client nicht erreichen kann, werden nie gemeldet.

**Bedingungen:**
- Jede Stufe der Ableitung (konfiguriertes `Host:Address`, gebundene Adressen, Kestrel-URLs, DNS-Adressliste).

**Verhalten:**
- `localhost` (Groß-/Kleinschreibung egal), Loopback (`127.*`, `::1`), Any (`0.0.0.0`, `[::]`) und Wildcards (`*`, `+`) sind keine nutzbaren Hosts.
- IPv6-Literale werden als Host grundsätzlich nicht gewählt; die DNS-Adressliste wird nur nach IPv4 (`AddressFamily.InterNetwork`, nicht Loopback) durchsucht.
- APIPA-Adressen (`169.254.x.x`) gelten als letzte IP-Stufe vor dem Hostnamen — auf dem Link erreichbar, aber kein bevorzugtes Ziel.
- Ein loopback-artiges `Host:Address` wird ignoriert statt unbrauchbar gemeldet (Verhaltensänderung zur Vorgängerversion, gewollt).
- Letzte Stufe ist der Maschinen-Hostname (`Dns.GetHostName()`); ist auch der leer, fällt die Ableitung defensiv auf `localhost` — praktisch unerreichbar.

**Umsetzung:** `DiscoveryResponseBuilder.IsUsableHost`, `ResolveHost`, `IsApipaAddress`.

## Validierungsregel für die öffentliche Basis-URL

**Beschreibung:** Dieselbe Regel gilt an allen drei Eingabestellen — Dateikonfiguration, Admin-Formular und serverseitige Persistenz.

**Bedingungen:**
- Leer/`null` ist zulässig und bedeutet „automatisch ableiten".
- Jeder nicht-leere Wert muss eine absolute `http`-/`https`-URI sein (`Uri.TryCreate` mit `UriKind.Absolute`, Schema `http` oder `https`).

**Verhalten:**
- `Discovery:PublicBaseUrl` ungültig → `DiscoveryOptionsValidator` lässt den Start fehlschlagen (`ValidateOnStart`, fail-fast).
- Admin-Eingabe ungültig → `AbsoluteHttpUrlAttribute` verhindert den Formular-Submit; die serverseitige Absicherung in `ProgramSettingsService.UpdateGeneralSettingsAsync` wirft `DiscoveryUrlValidationException`, die die Seite als `alert-danger`-Meldung anzeigt.
- Eingetragene Werte werden normalisiert (`Trim`, leer → `null`).
- Ein defensiv ungültiger persistierter Admin-Wert (nur über manipulierte Daten erreichbar) wird nicht gemeldet — die Auflösung fällt auf die nächste Stufe.

**Umsetzung:** `DiscoveryUrlRules.IsValidPublicBaseUrl` / `NormalizePublicBaseUrl` als gemeinsame Regel; Meldungstext zentral in `DiscoveryUrlRules.PublicBaseUrlRuleText`.

## Fail-open bei der Auflösung

**Beschreibung:** Transiente Fehler dürfen die Discovery-Präsenz nicht kippen.

**Bedingungen:**
- Fehler beim Lesen des Admin-Werts (z. B. DB-Ausfall) oder bei der DNS-Auflösung.

**Verhalten:**
- Admin-Wert nicht lesbar → geloggte Warning, Auflösung fährt ohne Admin-Override fort.
- DNS nicht auflösbar → geloggte Warning, die Host-Ableitung fällt auf den Hostnamen zurück.
- Fehler im Delegate oder beim Senden → geloggte Warning, die Empfangsschleife läuft weiter; `SocketException` wird still verschluckt (ICMP „Port unreachable" unter Windows).

**Umsetzung:** `DiscoveryBaseUrlResolver.ResolveAsync` (try/catch mit `ILogger`), `UdpDiscoveryListener.ListenAsync`.
