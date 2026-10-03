# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Die `requirement.md` enthält keine nummerierte Akzeptanzkriterien-Liste; die Kriterien wurden aus „Fachliche Zusammenfassung", „Konfiguration" und „Offene Fragen" abgeleitet. Die Kernanforderung (deployment-seitige Konfiguration überlebt ein Programmupdate) wird in dieser Ausprägung bewusst **nicht** code-seitig gelöst (Anwender-Entscheidung) — der Plan deckt sie über das Anforderungsdokument an `msTools.Updater` plus sauber dokumentierte Vertagung ab.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Deployment-seitige `web.config`-Inhalte (`environmentVariables` mit `Jwt:Key`, `Jwt:Issuer`, `Jwt:ApiToken:*`) überleben ein Update ohne manuelle Nachträge/Secret-Neugenerierung | Kernlieferung: Anforderungsdokument `docs/Anforderung_msTools_Updater_Dateierhalt.md` mit nummerierten Anforderungen D1–D9 (konfigurierbare `Preserve`-/`Merge`-Liste, XML-Merge für `environmentVariables` u. a., JSON-Merge für `appsettings*.json`, Backup, Reihenfolge-Garantie, Fehlertoleranz, Abwärtskompatibilität, Plattformabdeckung) + Issue in `martin-stromberg/msTools.Updater`; tatsächlicher Dateierhalt vertagt auf „nach der Bibliotheksänderung" mit konkreten Folgeschritten (nupkg-Upgrade, Schutz-Konfiguration, Tests, Doku-Nachzug); Übergangslösung maschinenweite `Jwt__*`-Umgebungsvariablen wird in Schritt 3 dokumentiert | Kein automatisierbarer Test vor der Bibliotheksversion möglich/erforderlich — Funktionsnachweis liegt beim Bibliotheksbetreuer bzw. in den vertagten Integrationstests (Bindungstest, Windows-gateter Skript-Ausführungstest nach Muster `MsToolsUpdaterIntegrationTests`); Doku-Änderungen via `MarkdownLinkCheck.Tests` (Schritt 4) | Abgedeckt (vertagt, sauber dokumentiert) |
| `SECRETS_MANAGEMENT.md` und `GUIDE_Installation.md` benennen die empfohlene Produktionsvariante eindeutig | Schritt 3: IIS-`environmentVariables`-Variante als update-gefährdet kennzeichnen, maschinenweite `Jwt__*`-Umgebungsvariablen als update-sichere Übergangslösung eindeutig empfehlen | `MarkdownLinkCheck.Tests`-Lauf | Abgedeckt |
| `docs/help/backups.md` (IIS-`web.config`-Absatz) auf den Update-Umgang abstimmen | Schritt 3: Überschreiben „bei jedem Programmupdate" ergänzen, Verweis auf Bibliothekslösung | `MarkdownLinkCheck.Tests`-Lauf | Abgedeckt |
| Weitere deployment-seitige Verluste (`security/ipSecurity`, `httpProtocol/customHeaders`, `aspNetCore`-Attribute, `appsettings*.json`) berücksichtigen | Im Anforderungsdokument: generischer Mechanismus (D3 XML-Element-/Attribut-Merge, D4 JSON-Merge, D5 `Preserve`); Restrisiko bis zur neuen Bibliotheksversion in „Seiteneffekte und Risiken" benannt | Teil der Akzeptanzkriterien im Anforderungsdokument | Abgedeckt |
| Anforderungsdokument fachlich vollständig und ohne Repo-Interna lesbar | Inhaltsvorgabe-Tabelle mit 11 Pflichtabschnitten (Ziel, Symptome, Reproduktion, Erwartetes Verhalten, Änderungen D1–D9, Anwendungsbeispiel, „Nicht durch VideoWebPlayer lösbar", Akzeptanzkriterien, betroffene Komponenten, Folgeaufgaben, Referenzen), Struktur analog zum Präzedenzfall `docs/Anforderung_msTools_Updater_Installationsskript.md` (verifiziert vorhanden, Gliederung stimmt) | Inhaltsspezifikation als Prüfgrundlage der Abnahme; `MarkdownLinkCheck.Tests` für Verweise | Abgedeckt |
| Issue im `msTools.Updater`-Repository anlegen | Schritt 2: `gh issue create --repo martin-stromberg/msTools.Updater`, deutschsprachig, Querverweis ins Repo-Dokument; Fallback bei Nichterreichbarkeit dokumentiert | In Planung verifiziert und durch Gegenprüfung bestätigt: `gh` 2.92.0, authentifiziert als `martin-stromberg` (Scope `repo`) | Abgedeckt |
| Offene Frage 1 (Lösungsrichtung) | Beantwortet: Bibliotheksseitiger generischer Dateierhalt/Merge — Anwender-Entscheidung in „Designentscheidungen" dokumentiert, In-Repo-Override begründet verworfen | — | Abgedeckt |
| Offene Frage 2 (Änderungen an `msTools.Updater` erlaubt? `IAutoUpdateScriptGenerator`-Überschreibbarkeit) | Beantwortet: ja, externes Repo + Issue; Überschreibbarkeit wurde verifiziert (`TryAddSingleton`, `sealed`) und die Option begründet verworfen | — | Abgedeckt |
| Offene Frage 3 (nur `environmentVariables` oder mehr schützen) | Beantwortet: Merge-Strategie für weitere Elemente/Attribute; Begründung in „Designentscheidungen" (Vollschutz würde Paket-Verbesserungen blockieren) | — | Abgedeckt |
| Offene Frage 4 (weitere geschützte Dateien, z. B. `appsettings.Production.json`) | Beantwortet: `appsettings*.json` über D4-JSON-Merge/D5-`Preserve` im Anforderungsdokument | — | Abgedeckt |
| Offene Frage 5 (Migrationshilfe für Bestandsinstallationen) | Beantwortet: vertagt, Punkt 5 im Vertagt-Abschnitt (Migrationshinweis: bestehende `environmentVariables` bleiben bei korrekter Konfiguration automatisch erhalten, kein manueller Umzug) | — | Abgedeckt |
| `AppPoolName`/`SiteName`-Dokumentationslücke (für IIS-Deployments entscheidend, Probe erkennt keine App-Pools) | Schritt 3: Aufnahme in `TECH_Auto_Update.md`-Konfigurationstabelle und `help/updates.md` (explizit ohne UI-Feld — außerhalb des Auftrags) | `MarkdownLinkCheck.Tests`-Lauf | Abgedeckt |
| `RELEASE_NOTES.md`: bekannte Einschränkung | Schritt 3: Eintrag „Update ersetzt deployment-seitige Dateien" + Übergangsempfehlung | `MarkdownLinkCheck.Tests`-Lauf | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein Benutzerfluss betroffen — Lieferung ist ein Anforderungsdokument plus Markdown-Klarstellungen; der Installationspfad läuft in einem separaten Prozess, während der Anwendungshost beendet ist | Keine | Nicht erforderlich mit Begründung: Plan begründet nachvollziehbar, dass (a) kein UI-/Nutzerablauf berührt wird und (b) ein Playwright-Test das Installationsskript prinzipiell nicht beobachten kann; echter Funktionsnachweis ist in Bibliothek bzw. vertagter Integration verortet; bestehende `UpdatesPageE2ETests` bleiben gültig |

## Hinweise

- `README.md` wird in `requirement.md` unter den betroffenen Dokumenten genannt, enthält aber laut Bestandsaufnahme (und verifiziert per Grep) keinen `web.config`-/Update-Bezug — die fehlende Einplanung ist sachlich korrekt, im Plan aber nicht explizit begründet. Bei der Umsetzung ggf. kurz vermerken, dass `README.md` geprüft und als nicht betroffen befunden wurde.
- Der Plan nennt als Doku-Verifikation `dotnet test tools/MarkdownLinkCheck.Tests`; der kanonische Befehl laut `GUIDE_Installation.md` (Zeile ~161) ist `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj`. Beide Varianten funktionieren (Verzeichnis mit genau einem Projekt); für Konsistenz mit der Doku ggf. die csproj-Variante verwenden.
- `docs/Secrets-Setup.md` wurde weder in der Bestandsaufnahme noch im Plan betrachtet; Verifikation ergab: kein `web.config`-/IIS-/`environmentVariables`-Bezug — keine Änderung nötig.
- Die einzige Ship-Dokumentation mit `web.config`-Bezug außerhalb des Feature-Ordners ist `docs/help/backups.md` (verifiziert); die IIS-`environmentVariables`-Variante ist aktuell in **keiner** Dokumentation beschrieben — der Plan ergänzt sie gezielt in `GUIDE_Installation.md`/`SECRETS_MANAGEMENT.md` samt Update-Warnung. Deckungslücke der Doku-Liste besteht nicht.
- Der optionale Commit zum Bereinigen veralteter Doku-Abschnitte (`TECH_Auto_Update.md`, `GUIDE_Installation.md`) ist korrekt als „außerhalb des Auftrags/eigener Commit" markiert und muss laut AGENTS.md §7 im Handover und Abnahmebericht benannt werden.
