# Planprüfung: Wöchentliche NuGet-Paketprüfung (2026-W48, Issue #380)

**Status: Plan lückenhaft**

Der Plan ist insgesamt vollständig, gut begründet und gegen das Repository verifizierbar
korrekt — Paketabdeckung, Versionsgleichheits-Gruppen, Risikologik und Qualitätstore sind
sauber ausgearbeitet. Zwei vom Issue explizit geforderte Prüfpunkte fehlen jedoch als
konkrete Umsetzungsschritte (Pflegestatus/Nachfolgepakete, Inline-Patches aufgeschobener
Linien), dazu einige kleinere Ungenauigkeiten. Die Lücken sind klein und lokal
begrenzt (im Wesentlichen Erweiterungen von Umsetzungsschritt 1), aber inhaltlich
gefordert — daher nicht „vollständig".

Geprüfte Eingaben: `requirement.md`, `inventory.md` + `inventory/`, `plan.md`,
Issue #380 (Body via `gh issue view`), Repo-Fakten (csproj-Stände, `pr-staging-ci.yml`).

## Was geprüft und als korrekt befunden wurde

- **Paketabdeckung vollständig:** Alle 40 `PackageReference`-Einträge / 33 unterschiedliche
  Pakete der vier paketbehafteten Projekte sind den Gruppen A–D zugeordnet
  (stichprobenartig gegen die csproj-Dateien verifiziert — Versionen stimmen exakt mit
  `inventory/packages.md` überein). `MarkdownLinkCheck` und `SecretScan` ohne
  Referenzen korrekt ausgeklammert.
- **Versionsgleichheits-Gruppen vollständig behandelt:** Alle sieben Mehrfachreferenzen
  aus dem Inventory stehen in der Scope-Klärung; die Umsetzung respektiert sie
  (Test.Sdk über beide Testprojekte in einem Commit, xunit-Block und EFCore.Sqlite
  als gekoppelte Einheiten aufgeschoben, unangetastete Paare bleiben gleich).
  Zusätzlich die SkiaSharp/NativeAssets-Paarung korrekt als Kopplung erkannt.
- **Risikoeinstufungen plausibel:** Microsoft.*/Serilog.* 10.x→11.0.0 als hoch
  (net11-TFM/CI-SDK-Voraussetzung, AGENTS.md-§7-Regel) ist richtig; xunit 3.x→4.x
  hoch ist richtig; SkiaSharp 4.157.0 als *stabiler* Milestone-Cut mit mittlerem
  Risiko ist sauber gegen die Issue-Prerelease-Regel begründet (inkl. Prüfung der
  Zwischen-Milestones m154–m156 in Schritt 4); SSH.NET-Patch im Jahresmodell und
  SharpCompress-Patch als niedrig plausibel, beide mit Security-Bezug benannt;
  Test-Tooling mittel regelkonform.
- **Scope-Ausschlüsse** decken die Issue-Vorgaben vollständig ab (kein main-PR,
  kein Hoch-Risiko, transitive unberührt, keine Workflow-/Dependabot-Änderung,
  keine Fachcode-Änderung); die TFM-/SDK-Ausschluss-Zeile und der
  UI-Review-Verzicht sind sinnvoll ergänzt.
- **Offene Issue-Fragen** sind alle entschieden und begründet (Sammel-PR,
  Aufschub-Doku im PR-Body, Scan-Referenz als Run-Link, Fallback Issue-Kommentar).
- **Schritt-10-Festlegung ausreichend:** Restore, Build Debug **und** Release,
  volle Testsuite inkl. Flake-Retry-Konvention, Tool-Tests, PR-Checks
  (`static-checks`, `build-and-test` inkl. Coverage-Gate, `no-pending-backmerge`,
  `os-interface-tests` best-effort), `test-results.md` statt Screenshots —
  korrekt, da keine UI-Änderung. Akzeptanzszenarien 1–7 decken den Zyklus inkl.
  bedingtem Rücknahme-Pfad ab.
- **Repo-Fakten stimmen:** Playwright-CI-Install (`pwsh …/bin/Release/net10.0/
  playwright.ps1 install --with-deps chromium`, `pr-staging-ci.yml` Z. 216),
  lokaler Feed `lib/packages`, Gate-2-Security-Scan, Back-Merge-Vorprüfung und
  `Closes #380` (Schließung erst bei Promotion staging→main — gewünscht) sind
  jeweils korrekt referenziert.

## Gefundene Lücken

- [ ] **(mittel) Kein Check auf Inline-Patches der aufgeschobenen Linien.**
  `dotnet list package --outdated` zeigt nur die absolut neueste Version
  (Microsoft.*/Serilog.* → 11.0.0, xunit → 4.x). Ein Patch innerhalb der
  gehaltenen Linie (z. B. Microsoft.* `10.0.13`, Serilog `10.0.x`, xunit `3.2.x`)
  würde von `--outdated` verschluckt und wäre dennoch ein niedriges Patch-Update
  — der Issue verlangt „Updates patch/minor/major **getrennt** ausweisen", und
  der Präzedenzfall (10.0.10→10.0.12) zeigt, dass solche Inline-Patches hier
  üblich sind. Ergänzen: In Schritt 1 für die Gruppe-C-Pakete die Versionsliste
  der selben Hauptversion prüfen (NuGet-API/`dotnet package search … --exact-match`).
- [ ] **(mittel) Prüfpunkte „veraltete/nicht mehr gepflegte Pakete" und
  „empfohlene Nachfolgepakete" nicht operationalisiert.** `--deprecated` prüft
  nur das NuGet-Deprecation-Flag; das Issue-Kriterium (letztes Release,
  archiviertes/verwaistes Upstream-Repo) und die Nachfolgepaket-Frage werden
  für kein Paket geprüft — betrifft v. a. Gruppe D (`Makaretu.Dns.Multicast.New`,
  `Nito.AsyncEx`, `msTools.*`). Ergänzen: In Schritt 1 kurzer Pflegestatus-Check
  (letztes Release auf nuget.org, Repo-Status) für die Pakete ohne Update; die
  Maßnahme-Spalte der Ergebnistabelle soll den Befund je Paket zeigen.
- [ ] **(klein) Release-Notes-Prüfung nur für „entscheidungstragende" Pakete.**
  Für Moq, Test.Sdk, coverlet.collector, Playwright und bunit ist kein
  Changelog-/Breaking-Changes-Check benannt, obwohl das Issue die Prüfung je
  Update verlangt. Durch die Testsuite abgefedert, aber ein Quercheck
  (v. a. bunit 2.11.3→2.15.2 wegen API-Änderungen in Blazor-Tests) sollte in
  Schritt 3/5 eingeplant werden.
- [ ] **(klein) Kein lokaler Schwachstellen-Re-Scan nach den Bumps.** bunit
  zieht AngleSharp transitiv nach — der neue transitive Bestand wird erst durch
  das CI-Gate-2 geprüft. Ein `dotnet list package --vulnerable
  --include-transitive` nach Schritt 5/6 wäre die konsistente lokale Vorprüfung.
- [ ] **(klein) Ungenauigkeit Akzeptanzszenario 1:** „`git diff` der **vier**
  csproj" — geändert werden drei (`VideoWebPlayer.csproj`,
  `VideoWebPlayer.Tests.csproj`, `MarkdownLinkCheck.Tests.csproj`).
- [ ] **(klein) Playwright-Install lokal unpräzise.** „playwright install
  chromium (bzw. Repo-Mechanismus)" — der Repo-Mechanismus ist der CI-Beleg:
  `pwsh VideoWebPlayer.Tests/bin/<Konfiguration>/net10.0/playwright.ps1 install
  chromium` nach dem Build. Konkretisieren vermeidet Rätseln bei der Umsetzung.
- [ ] **(klein) Kein Duplikat-PR-Check.** Vor PR-Erstellung wird nur der
  Back-Merge-PR geprüft; ein kurzer `gh pr list --state open`-Blick auf einen
  bereits offenen NuGet-PR schadet nicht (Vorgängerzyklus #358 endete ohne PR).

## Hinweise (keine Lücken, aber zu beachten)

- Warnungs-Baseline „139" stammt aus einem Release-Build des *Testprojekts*
  (inventory/tests.md Z. 76–78), nicht zwingend eines Voll-Solution-Builds —
  als Referenzwert ok, bei Abweichung nicht überinterpretieren.
- `test-results.md` als Nachweisartefakt ist sinnvoll; die Flake-Konvention
  (einmalige Einzelwiederholung, ehrlich berichten, max. ein Job-Rerun der
  OS-Lane, niemals `gh run rerun --failed`) ist korrekt übernommen.
- Die Hoch-Risiko-Blöcke kommen automatisch über die Wochen-Reviews wieder —
  die Entscheidung „kein Folge-Issue" ist damit risikolos und begründet.
