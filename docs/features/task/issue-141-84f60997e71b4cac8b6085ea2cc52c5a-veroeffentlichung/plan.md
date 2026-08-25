# Umsetzungsplan: Veröffentlichung

## Zielbild

Das Web-Repository kann öffentlich bereitgestellt werden. Die bestehende Lizenz bleibt unverändert und wird nur konsistent dokumentiert. Ein versionierter Client-Hook prüft lokale und repositoryinterne Markdown-Links ohne CI-Zwang und ohne Netzwerkzugriff. Die Installations- und Sicherheitshinweise sind für Linux und Windows nachvollziehbar und verwenden ausschließlich eindeutig synthetische Beispielwerte.

Das MAUI-Projekt wird aus dem Web-Repository in den vorhandenen Klon unter `Sub-Repository/` ausgelagert. Das Ziel-Repository wird danach eigenständig buildbar und mit der für das MAUI-Team erforderlichen API-Dokumentation des Webprojekts versorgt.

## Festgelegte Entscheidungen

- Die vorhandene PolyForm Noncommercial 1.0.0-Lizenz bleibt unverändert. Es erfolgt keine Änderung am Lizenztext oder an der Lizenzgenehmigung.
- Der Markdown-Linkcheck wird ausschließlich als versionierter Client-Hook ausgeliefert. Eine CI-Prüfung ist nicht Bestandteil der Umsetzung.
- Für das Webprojekt werden Linux und Windows entsprechend der aktuell vorhandenen Projekt- und Skriptunterstützung dokumentiert.
- Dokumentierte Token und Schlüssel sind ausschließlich synthetische Platzhalter. Die Anleitung weist ausdrücklich darauf hin, dass produktiv andere Werte zu verwenden sind. Die hartkodierte MAUI-Token-Strategie wird als nicht geheim und als zu ersetzender Konfigurationspunkt beschrieben; reale Werte werden weder neu eingeführt noch übernommen.
- Das MAUI-Repository erhält mindestens `VideoWebPlayer.Maui`, `VideoWebPlayer.Maui.Tests`, die zur eigenständigen Kompilierung erforderliche Client-Codebasis beziehungsweise deren versionierte Übergabeschnittstelle sowie Lizenz-, Build- und Installationsdokumentation. Die konkrete Ablage wird anhand der bestehenden `ProjectReference`-Abhängigkeiten umgesetzt, ohne eine unauflösbare Referenz in das Web-Repository zu hinterlassen.
- Die API-Dokumentation bleibt im Web-Repository versioniert und wird als verbindlicher Vertrag für das MAUI-Team ausgebaut. Eine reine Sammlung von XML-Kommentaren gilt nicht als ausreichende Dokumentation.

## Umsetzungsschritte

### 1. Repository-Grenze und MAUI-Auslagerung zuerst klären

1. Den leeren Klon unter `Sub-Repository/` als Ziel-Repository behandeln; dessen bestehende Git-Metadaten und `.gitignore` bleiben erhalten.
2. `VideoWebPlayer.Maui/` und `VideoWebPlayer.Maui.Tests/` mit Historie beziehungsweise nachvollziehbarer Dateiverschiebung in das Ziel-Repository überführen.
3. Die Abhängigkeit auf `VideoWebPlayer.Client` auflösen: entweder den Client als eigenständiges, im MAUI-Repository versioniertes Projekt mit übernehmen oder ihn als reproduzierbar konsumierbares Paket/Artefakt bereitstellen. Bevorzugt wird die Übernahme der gemeinsam benötigten Client-Projekt- und Modelldateien in eine klare Repository-Struktur, solange dadurch keine Server-Implementierung dupliziert wird.
4. MAUI-Solution, Projektverweise, Lizenzreferenzen, Buildskripte, Dokumentationspfade und Testverweise auf die neue Struktur anpassen. Das Web-Repository darf danach keine MAUI-Projektdateien mehr in seiner Solution oder in seinen Build-/Release-Schritten voraussetzen.
5. Für beide Repositories klare Verantwortungsgrenzen dokumentieren: Web-API und API-Vertrag im Web-Repository, mobile App und mobile Tests im MAUI-Repository.
6. Im Ziel-Repository mindestens eine README, Installationshinweise, `LICENSE` beziehungsweise eine eindeutig referenzierte Lizenzkopie und einen reproduzierbaren Build-/Testaufruf ergänzen.

### 2. API-Dokumentation des Webprojekts vervollständigen

1. Alle API-Controller und für das MAUI-Team relevanten SignalR-/Medienzugriffe inventarisieren. Der aktuelle Bestand enthält Controller und XML-Dokumentationskommentare, aber keine eigenständige API-Beschreibung oder OpenAPI-Datei.
2. Im Web-Repository eine zentrale API-Dokumentation anlegen, beispielsweise `docs/API.md`, mit Basis-URL und Konfiguration, Authentifizierung, Headern, Token-Lebensdauer und Statuscodes.
3. Für jeden MAUI-relevanten Endpunkt Methode, Route, Zweck, Berechtigungen, Request- und Response-JSON, DTO-Felder, Binärantworten, Fehlerfälle und Beispielaufrufe dokumentieren. Dazu gehören mindestens Login, Health, Quellen, Genres, Medien-/Bildzugriffe, Favoriten, Continue-Watching, Episoden sowie SignalR-Verbindungen.
4. Admin- und browserinterne Form-Endpunkte klar von der öffentlichen bzw. MAUI-API abgrenzen. Keine CSRF-/Cookie-Endpunkte als mobile API ausweisen.
5. Die Dokumentation mit den tatsächlichen Controller-Routen, `VideoWebPlayer.Client`-Modellen und den von der MAUI-App verwendeten Endpunkten abgleichen. Veraltete oder nicht unterstützte Vertragsannahmen werden entfernt oder ausdrücklich gekennzeichnet.
6. Einen kleinen reproduzierbaren Vertragscheck ergänzen: dokumentierte Routen gegen Controller/Client-Aufrufe prüfen und mindestens Login, Health und einen authentifizierten Lesezugriff per Integrationstest oder manueller Testsequenz verifizieren.

### 3. Lizenz und öffentliche Metadaten konsolidieren

1. `LICENSE` unverändert als maßgeblichen Lizenztext behandeln und die bestehende Bezeichnung in `README.md` sowie den Veröffentlichungs- und Installationsdokumenten konsistent verwenden.
2. Keine Aussage ergänzen, die die PolyForm Noncommercial 1.0.0 als OSI-freie Open-Source-Lizenz bezeichnet.
3. Die Lizenzreferenzen und `PackageLicenseFile`-Metadaten der verbleibenden Web-/Client-/Testprojekte prüfen. Für das ausgelagerte MAUI-Repository eine konsistente Lizenzreferenz herstellen, ohne den Lizenztext inhaltlich zu ändern.
4. Lizenz-Badge, Kontaktangaben und Aussagen zu privater sowie kommerzieller Nutzung auf Widerspruchsfreiheit prüfen.

### 4. Versionierten Markdown-Linkcheck und Client-Hook umsetzen

1. Einen plattformübergreifenden Linkcheck für Windows und Linux als versioniertes Skript beziehungsweise als dotnet-basierten Aufruf im Web-Repository anlegen.
2. Markdown-Dateien gezielt sammeln, relative Pfade vom Speicherort der Quelldatei auflösen, URL-Encoding und Fragmente behandeln und Datei, Zeile, Linkziel sowie Fehlerursache ausgeben.
3. Lokale Dateien und Verzeichnisse prüfen. `http(s)`, `mailto`, reine Anker, Codeblöcke und sonstige nicht lokale Ziele ohne Netzwerkzugriff klassifizieren beziehungsweise auslassen.
4. Einen ungleich-null Exit-Code bei ungültigen lokalen Links sicherstellen und Tests für Erfolg, fehlende Datei, Fragment, Groß-/Kleinschreibung sowie externe Links ohne Netzwerkzugriff ergänzen.
5. Einen versionierten Hook unter `.githooks/` bereitstellen und die Aktivierung über `git config core.hooksPath` dokumentieren. Der Hook läuft am vorgesehenen Client-Prüfzeitpunkt und meldet fehlende lokale Werkzeuge verständlich.
6. Setup und Fehlerfall in einem frischen Arbeitsverzeichnis unter Windows und Linux prüfen. CI-Workflows werden für diese Anforderung nicht erweitert.

### 5. Installations- und Sicherheitshinweise aktualisieren

1. `README.md`, `docs/GUIDE_Installation.md` und `docs/INDEX.md` auf vorhandene Pfade und die tatsächlichen Startbefehle korrigieren; insbesondere `docs/`-Groß-/Kleinschreibung und veraltete Ports bereinigen.
2. Für Linux und Windows die Voraussetzungen aus den Projektdateien dokumentieren: .NET 10 SDK, NuGet.org und `lib/packages`, lokale DLL-Abhängigkeit des Webprojekts, SQLite, erforderliche Schreibrechte sowie plattformabhängige MAUI-Workloads.
3. Clone, Restore, Secrets-Konfiguration, Start, Erfolgsprüfung, Tests und häufige Fehler als nachvollziehbare Schritte beschreiben. Web- und MAUI-Setup sowie nicht verfügbare Zielplattformen getrennt darstellen.
4. `docs/SECRETS_MANAGEMENT.md` vollständig bereinigen: realistisch aussehende Werte durch Platzhalter wie `<PRODUKTIVER_JWT_KEY>` ersetzen, niemals Beispielwerte als produktiv verwendbar darstellen und klar zwischen Backend-Secrets, JWTs und nicht geheimen Client-Konfigurationswerten unterscheiden.
5. `appsettings*.json`, Launch-/Service-Konfigurationen, MAUI-Services und relevante Git-Historie auf Zugangsdaten prüfen. Verdächtige historische Werte bewerten und gegebenenfalls außerhalb dieses Plans rotieren beziehungsweise nach Freigabe bereinigen; kein ungefragter History-Rewrite.
6. Eine Veröffentlichungscheckliste für Lizenz, Linkcheck, saubere Einrichtung, API-Dokumentation, Secret-Scan und Repository-Trennung ergänzen.

## Verifikation und Akzeptanznachweis

1. Web-Repository baut und testet nach Entfernung der MAUI-Abhängigkeit erfolgreich; das MAUI-Ziel-Repository baut und testet mit seinen eigenen Projektverweisen erfolgreich.
2. Die API-Dokumentation enthält die von der MAUI-App tatsächlich verwendeten Verträge und wird anhand mindestens eines Login-, Health- und authentifizierten Medienzugriffs nachvollzogen.
3. Der Linkcheck liefert im Bestand Erfolg, erkennt einen kontrolliert eingefügten fehlenden lokalen Link mit Datei/Zeile und beendet sich ungleich null; der Kontrollfehler wird anschließend entfernt.
4. Der Hook lässt sich in frischen Arbeitsverzeichnissen unter Windows und Linux aktivieren und reproduzierbar ausführen.
5. Eine saubere Web-Installation und ein MAUI-Setup anhand der neuen Repository-Strukturen werden nachvollzogen. Dabei werden keine echten Tokens oder lokalen Secrets benötigt.
6. Secret-Scan für Arbeitsbaum und vereinbarten Historienumfang ist ohne ungeklärte Treffer. Die Dokumentation enthält keine produktiv verwendbaren Beispiel-Credentials.
7. Bestehende Web-Build-, Test-, Formatierungs- und Vulnerability-Prüfungen sowie die neuen fokussierten Linkcheck-, Hook-, API-Vertrags- und Repository-Grenztests laufen erfolgreich.

## Betroffene Dateien und Artefakte

- Web-Repository: `README.md`, `LICENSE` unverändert, `docs/GUIDE_Installation.md`, `docs/INDEX.md`, `docs/SECRETS_MANAGEMENT.md`, neue `docs/API.md`, Linkcheck-/Hook-/Setup-Dateien, Solution-/Projekt- und Builddefinitionen.
- Ziel-Repository `Sub-Repository/`: ausgelagerte MAUI-App und Tests, erforderliche Client-Codebasis oder Paketdefinition, Solution, README, Installations-/API-Verweis- und Lizenzdokumentation sowie eigene Build-/Testdateien.
- Tests: lokale Linkcheck- und Hook-Tests, API-Vertrags-/Integrationsprüfung, Builds und Tests beider Repositorys.

## Abhängigkeiten und Risiken

- Die Auslagerung darf keine implizite Projekt- oder Pfadabhängigkeit auf das Web-Repository zurücklassen. Die gewählte Client-Übergabe muss vor dem Löschen der alten MAUI-Struktur buildbar sein.
- Controller-Routen und Client-Aufrufe können auseinanderlaufen; die API-Dokumentation muss deshalb aus beiden Quellen abgeglichen werden.
- Linux behandelt Pfad-Groß-/Kleinschreibung anders als Windows. Der Linkcheck muss diese Differenz sichtbar und reproduzierbar behandeln, statt sie zu verschleiern.
- Das Ziel-Repository liegt als leerer Klon mit eigenem Git-Verzeichnis vor. Änderungen an den beiden Repositories müssen getrennt geprüft und committed werden.
- MAUI-Plattformen bleiben an Hostbetriebssystem und installierte Workloads gebunden. Linux und Windows sind die dokumentierten Entwicklungsplattformen; iOS/MacCatalyst werden nur als plattformabhängige Ziele beschrieben, sofern die vorhandenen Projektbedingungen sie zulassen.

## Offene Punkte

Keine. Die Anwenderentscheidungen sind berücksichtigt; die verbleibenden technischen Detailentscheidungen werden während der Umsetzung anhand der bestehenden Projektverweise und Buildfähigkeit validiert.
