# Übersetzte Anforderung: UDP-Discovery-Antwort soll die erreichbare Basis-URL melden (Issue #224)

## Fachliche Zusammenfassung

Der `UdpDiscoveryListener` (`VideoWebPlayer/Services/UdpDiscoveryListener.cs`) beantwortet
UDP-Broadcasts `VIDEOWEBPLAYER_DISCOVERY` auf Port 5001 mit
`VIDEOWEBPLAYER_SERVER:{serverAddress}`. Die gemeldete Adresse wird in `Program.cs` (Zeile 54)
als `http://{Host:Address ?? "localhost"}:{Host:Port ?? "5000"}` gebildet und ist für LAN-Clients
in der Default-Konfiguration unbrauchbar (`localhost` zeigt aus Clientsicht auf den Client selbst;
bei IIS-Bereitstellung wie `http://<host>/videoplayer/` stimmen Schema, Host, Port und Pfad nicht).
Die Discovery-Antwort soll künftig die aus Clientsicht tatsächlich erreichbare öffentliche
Basis-URL des Servers enthalten — über eine explizit konfigurierbare öffentliche Basis-URL
oder durch Ableitung aus der empfangenden Serverschnittstelle in Kombination mit dem
konfigurierten Port/Pfad.

Die Anforderung betrifft ein reines Netzwerk-/Konfigurationsverhalten ohne Benutzerinteraktion in
der Oberfläche; eine Auswahl oder Identifikation von Datensätzen durch Anwender wird nicht
verlangt.

## Betroffene Klassen und Komponenten

- **`VideoWebPlayer/Services/UdpDiscoveryListener.cs`**: Der Listener nimmt die Antwortadresse
  derzeit als fertiges `string`-Argument entgegen (Konstruktorparameter `serverAddress`). Er
  benötigt entweder eine auflösende Adressquelle oder eine andere Übergabe (z. B. delegate/
  auflösende Builder-Komponente), damit die Antwort zur Laufzeit ermittelt werden kann.
- **`VideoWebPlayer/Program.cs`** (Zeilen 50–57): Hier wird `serverAddress` zusammengebaut und der
  Listener gestartet, abgeschirmt durch `!app.Environment.IsEnvironment("Testing")`. Diese Stelle
  ist der zentrale Änderungspunkt. Hinweis: Der Listener startet vor `app.Run()` — tatsächlich
  gebundene Adressen (`IServerAddressesFeature.Addresses` über `IServer`, Muster
  `MdnsAdvertiserWorker`) sind erst nach dem Serverstart verfügbar.
- **Neu (Vorschlag): Adressableitungs-Bausteine** in `VideoWebPlayer/Services/` analog zum
  netzwerkfrei testbaren `MdnsServiceProfileBuilder` — z. B. eine reine Ableitungslogik, die aus
  Konfiguration, gebundenen Adressen und ggf. lokaler Empfangsadresse die Antwort-URL erzeugt
  (Namenskonvention im Projekt: `*Builder` für reine Ableitung, `*Worker`/`UdpDiscoveryListener`
  für Laufzeitverhalten).
- **`VideoWebPlayer/appsettings.json`**: neuer Konfigurationsschlüssel für die öffentliche
  Basis-URL (siehe Abschnitt Konfiguration). Neue Schlüssel müssen in die `JsonKeys`-Merge-Liste
  unter `AutoUpdate:ProtectedFiles[1]` (Zeilen ~121–174) aufgenommen werden, damit sie ein
  Auto-Update überstehen — dort stehen bereits `Host:Address` und `Host:Port`.
- **Bestehender Schlüssel `App:BaseUrl`** (`ServiceCollectionExtensions.cs`, Zeile 206): bereits
  eingeführte, undokumentierte Fallback-Basis-URL für den scoped `HttpClient` (Issue #228). Als
  Wiederverwendungskandidat oder Konkurrenzschlüssel zu bewerten (siehe Offene Fragen).
- **Konfigurationsklassen (optional)**: Falls die Discovery-Konfiguration gebündelt wird, ein
  Options-Typ nach Muster `VideoWebPlayer/Configuration/MdnsOptions.cs` samt Validator
  (`MdnsOptionsValidator` als Vorbild, `IValidateOptions<>` mit `ValidateOnStart()`).
- **Tests** in `VideoWebPlayer.Tests/` (Konvention `*Tests.cs`): Für den Listener existieren
  bislang keine Tests. Unit-Tests für die neue Ableitungslogik (konfigurierte URL hat Vorrang,
  kein `localhost`/Loopback in LAN-Antworten, Pfad-Erhalt, Port-Ableitung); der Socket-Versand
  selbst ist wie bei mDNS nur manuell verifizierbar.
- **Keine Datenmodell-/EF-Migrationsänderung** erkennbar — sofern die Basis-URL nicht über
  `Setup`/`ProgramSettingsService` administrierbar sein soll (siehe Offene Fragen).
- **Dokumentation**: `docs/API.md` (Abschnitt „Server-Erkennung (Discovery)", Zeilen 1032–1047 —
  beschreibt das UDP-Protokoll und die Ableitungskette), `docs/GUIDE_Installation.md` (Zeile 53:
  Discovery-Adresse; mDNS-Abschnitt mit IIS-`OutOfProcess`-Hinweis), `docs/help/einrichtung.md`,
  `README.md`, `docs/RELEASE_NOTES.md`.

## Implementierungsansatz

- **Auflösungskette für die gemeldete Basis-URL** (konkrete Reihenfolge offen, Orientierung an der
  `MdnsServiceProfileBuilder`-Portkette `Mdns:Port` → gebundene Adressen →
  `Kestrel:Endpoints:Http:Url` → `Host:Port` → 5000):
  1. explizit konfigurierte öffentliche Basis-URL (Schema, Host, Port, Pfad vollständig) —
     einziger Weg, der IIS-Unterpfade (`/videoplayer/`) und Reverse-Proxy-/TLS-Terminierung
     korrekt abbildet;
  2. Fallback-Ableitung bei direktem Kestrel-Hosting: Adresse der Empfangsschnittstelle oder
     aufgelöster Hostname (`Dns.GetHostName()`, Muster `MdnsServiceProfileBuilder` Zeile 37)
     plus Port aus der bestehenden Ableitungskette; `localhost`/Loopback darf in einer
     LAN-Antwort nie gemeldet werden — bei loopback-artiger Konfiguration ist auf eine
     LAN-Adresse auszuweichen (Annahme, dem Beobachteten-Verhalten-Absatz ableitbar).
- **Responder-Adresse aus der Anfrage**: `UdpClient.ReceiveAsync` liefert nur den
  `RemoteEndPoint` (Absender); die lokale Empfangsadresse eines Broadcasts ist darüber nicht
  direkt bestimmbar. Eine Ableitung „aus der Anfrage" erfordert entweder `Socket`-Ebene mit
  Paket-Informationen oder — einfacher — die Wahl einer primären LAN-Adresse des Hosts.
  Annahme: Eine der beiden Varianten genügt, Entscheidung in der Planung.
- **Ableitung zum Ablesezeitpunkt statt einmalig beim Start**: Der Listener erhält die Antwort
  heute fertig im Konstruktor. Wird die URL von gebundenen Adressen abhängig, muss die Ermittlung
  pro Anfrage oder nach `IHostApplicationLifetime.ApplicationStarted` erfolgen (Muster
  `MdnsAdvertiserWorker.WaitForApplicationStartedAsync`).
- **Testing-Abschaltung beibehalten**: Der Listener startet nicht unter Umgebung `Testing`
  (fester Port 5001, E2E-Schutz); diese Abschirmung in `Program.cs` bleibt unverändert.
- **Protokollformat unverändert**: `VIDEOWEBPLAYER_SERVER:<url>` — der Wert hinter dem Doppelpunkt
  ist für den Server eine opaque Zeichenkette; die Verarbeitung liegt beim Client (MAUI-App
  `VideoPlayer.Maui`, externes Repository). Ob der Client einen Pfad-Bestandteil in der URL
  toleriert, ist zu klären (siehe Offene Fragen).
- **Testbarkeit**: Adressermittlung als reine Funktion kapseln (Muster `MdnsServiceProfileBuilder`),
  damit Konfigurationsvorrang, Loopback-Vermeidung, Port-/Pfadableitung ohne Netzwerk prüfbar
  sind.
- **Keine Berechtigungsprüfung nötig**: Die Discovery-Antwort ist wie bisher unauthentifizierte
  Netzwerk-Präsenz; die gemeldete URL enthält keine sensiblen Daten.

## Konfiguration

Vorschlag auf Betreiberebene (`appsettings.json`, übersteuerbar via `appsettings.Local.json` —
`AddLocalJsonConfiguration` in `Program.cs` Zeile 20 — oder Umgebungsvariablen):

- Eigener Schlüssel für die öffentliche Basis-URL, z. B. `Discovery:PublicBaseUrl` oder
  `Host:BaseUrl` (Benennung offen). Vollständige absolute URL inkl. optionalem Pfad, z. B.
  `http://server.lan:5000/` oder `https://example.com/videoplayer/`. Leer/unset = Ableitung.
- Die bisherigen Schlüssel `Host:Address`/`Host:Port` wirken aktuell ausschließlich auf diese
  Discovery-Antwort (einzige Lesestelle: `Program.cs` Zeile 54). Ob sie als Fallback-Baustein
  der Ableitung bestehen bleiben oder durch den neuen Schlüssel abgelöst werden
  (Kompatibilitätsfrage), ist offen.
- Neue Schlüssel sind in `AutoUpdate:ProtectedFiles[1].JsonKeys` zu ergänzen (Prüfung durch
  `AutoUpdateProtectedFilesTests`).
- Eine datenbankgestützte Admin-Einstellung über `Setup`/`ProgramSettingsService` (mit
  EF-Migration und Backup-Erweiterung, Muster `Setup.MdnsAdvertisementEnabled`) erscheint für
  eine Betreiber-Basis-URL nicht erforderlich — Annahme: Datei-/Umgebungskonfiguration genügt,
  Rückfrage in Offene Fragen.

## Offene Fragen

1. **Client-Vertrag:** Toleriert die MAUI-App (externes Repository) eine Basis-URL mit
   Pfadanteil (z. B. `http://<host>/videoplayer/`) in `VIDEOWEBPLAYER_SERVER`-Antworten,
   oder darf nur `scheme://host:port` gemeldet werden? Koordination mit dem App-Repository nötig.
2. **Schlüsselwahl:** Wiederverwendung/Verallgemeinerung des bestehenden `App:BaseUrl`
   (`ServiceCollectionExtensions.cs` Zeile 206, aktuell nur HttpClient-Fallback) oder eigener
   Discovery-Schlüssel? Bei Wiederverwendung ist die Semantik zu dokumentieren und in
   `JsonKeys` aufzunehmen.
3. **Ableitungsstrategie ohne explizite Konfiguration:** Lokale Empfangsadresse des Broadcasts
   (Socket-Ebene), primäre LAN-Adresse des Hosts oder `Dns.GetHostName()`? Verhalten bei
   mehreren NICs/VLANs und bei IPv6?
4. **Schema bei TLS-Terminierung:** Bei IIS/Reverse-Proxy läuft das Backend auf `http`, extern
   ggf. `https`. Kann die Ableitung das externe Schema je ermitteln, oder ist dafür zwingend
   die explizite Basis-URL-Konfiguration erforderlich (dann Dokumentationspflicht analog zum
   `Mdns:Port`-Hinweis für `OutOfProcess`)?
5. **Rolle von `Host:Address`/`Host:Port`:** Bleiben sie als Ableitungs-Fallback bestehen
   (Rückwärtskompatibilität für bestehende Installationen), oder werden sie durch den neuen
   Schlüssel ersetzt/als veraltet dokumentiert?
6. **Admin-Einstellbarkeit:** Soll die Basis-URL zusätzlich in der Admin-Oberfläche
   (`Setup`/`ProgramSettingsService`, `/admin/program-settings`) pflegbar sein — das würde
   EF-Migration, `VideoWebPlayerBackupData`-Erweiterung und E2E-Tests nach sich ziehen?
