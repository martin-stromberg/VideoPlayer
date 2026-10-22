# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK 1: Vier Tabs, „Regeln" initial aktiv, kein Funktionsverlust | Tab-Navigation mit `activeTab`, `.tab-pane`/`.active`, IDs `tvsender-tab-*`/`tab-*`; explizite Invarianz aller Bindings/IDs/Loops | bUnit `RendersFourTabs_WithRulesActive`, E2E Tab-Assertions im konsolidierten Test | Abgedeckt |
| AK 2: Regel-Editor als einklappbare Card im Regeln-Tab | `#rule-editor` als `.admin-card.rule-editor-card`, `StartCreate`/`StartEdit`/`CancelEdit` unverändert | bUnit `RuleEditor_RendersAsCard_InsideRulesTab`, E2E Regel-Lifecycle | Abgedeckt |
| AK 3: „▾ Details" an ausstehend/fehlgeschlagen, Tabellen mit Name/Quelle/Versuche/letztem Versuch, serverseitig limitiert mit Hinweis | `chip-detail-btn` + `<details class="probe-detail">` mit `open`-Binding, `.tvsender-probe-detail-row`, „… und N weitere" | bUnit `SwitchesTabs_AndShowsProbeDetailTables`, E2E Detail-Assertions | Abgedeckt |
| AK 4: `GetProgressAsync` liefert `PendingItems`/`FailedItems` konsistent zu Zählern, admin-only | `detailItemCount`-Parameter, zwei Projektionsabfragen identisch zur Zähler-Filterlogik, `RequireAdminAsync` unverändert | Drei neue Service-Tests (Inhalt/Sortierung/Limit); bestehender `NonAdmin_Throws` | Abgedeckt |
| AK 5: E2E-Lifecycle über neue Struktur, bUnit-/E2E-Abdeckung | Bestehender konsolidierter E2E wird angepasst und um Tab-/Detail-Assertions erweitert; neuer bUnit-Test | E2E-Tabelle (drei Pflicht-Szenarien) | Abgedeckt |
| AK 6: Build Debug+Release, Testsuite grün | Schritt 10 des Lifecycle (`/run-tests`), AGENTS.md-DoD | Testlauf nach Implementierung | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Tab-Navigation zwischen den vier Bereichen (AK 1) | `Admin_CreateEditDeactivateDeleteRule` — Tab-Buttons anklicken, Pane-Sichtbarkeit prüfen | Abgedeckt |
| Detail-Listen aufklappen (AK 3) | `Admin_CreateEditDeactivateDeleteRule` — „▾ Details" klicken, Zeileninhalte prüfen | Abgedeckt |
| Regel-Lifecycle + Einstellungen + Sendeplan-Anstoß (AK 2, 5) | `Admin_CreateEditDeactivateDeleteRule` (bestehend, angepasst) | Abgedeckt |
| Admin-Gate (Sichtbarkeit) | `NonAdmin_SeesNotAuthorized` (bestehend, unverändert) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- `Admin_GenreRule_MultiSelectAndSourceFilter` bleibt voraussichtlich unverändert
  lauffähig, weil der Regel-Editor weiterhin über `#tvsender-new-rule` im initial
  aktiven Regeln-Tab erreichbar ist — Verifikation im Testlauf.
- Das optionale „Probe zurücksetzen" aus N13 ist bewusst nicht geplant (Mockup ohne
  Reset, in `nachbesserungen.md` optional markiert) — dokumentiert in `requirement.md`.
