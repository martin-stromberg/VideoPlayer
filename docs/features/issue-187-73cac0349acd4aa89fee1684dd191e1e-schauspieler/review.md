# Plan-Review: Schauspieler

## Kritische Probleme

1. **Schwelle-Semantik unklar**: Die 50%-Regel wird als "mindestens 50 % der Filme" formuliert. Bei 2 von 4 Filmen ergibt das 50 %; bei 1 von 4 (25 %) logischerweise Einzelfilme. In der Test-Matrix ist das korrekt. Allerdings ist unklar, ob ab- oder aufgerundet wird (`>= 0.5` empfohlen).
2. **Nacherfassung kann Host start verzögern**: `IHostedService` synchron beim Start ausführen verzögert den Start. Besser in `BackgroundService` auslagern und als `IHostedService` mit `StartAsync` abfeuern, aber nicht blockieren.
3. **Sichtbarkeit gegenüber Quellen**: Der Plan sagt "nur Schauspieler aus freigeschalteten Quellen". Das ist nicht trivial: ein Schauspieler kann in einem freigeschalteten Film UND in einer gesperrten Serie vorkommen. Die Detail-DTO muss einzelne Medien filtern, aber der Schauspieler selbst sollte für den Nutzer sichtbar sein, sobald ein Medium zugänglich ist. Das muss im Service-Test verifiziert werden.

## Wesentliche Schwächen

1. **Bild-Import fehlt**: Das `PictureId`-Feld ist ein guter Anker, aber es gibt keine Strategie, wie Porträtbilder aus NFO-`<thumb>`-Tags oder extern bezogen werden. Erste Version ohne Bild ist akzeptabel, sollte aber als Einschränkung dokumentiert werden.
2. **Nacherfassung `LastActorBackfillId`**: Fortsetzung über ein einzelnes `LastActorBackfillId` funktioniert nur, wenn die Abfrage stabil nach `Id` sortiert. Gleichzeitige Einfügungen (neue Medien während Backfill) könnten das durcheinanderbringen. Einfacher: `ActorsClassifiedAt == null` holen und nach `Id` paginieren.
3. **Keine Rollback-Strategie**: Migration `AddActors` ist hinzugefügt; Down-Migration sollte dokumentiert sein. Bei Fehlschlag muss `ActorsClassifiedAt` zurückgesetzt werden können.
4. **Backup-Kompatibilität**: Bestehende Backups ohne `Actors` werden als älteres Schema betrachtet. `VideoWebPlayerBackupData` muss diese optional behandeln, was im Plan erwähnt, aber konkret getestet werden muss.
5. **Fehlende negative Tests für Suche**: Was passiert bei `search=""`, Sonderzeichen, Unicode, mehreren Wörtern? Was passiert bei nicht vorhandenem Filterbuchstaben? Nicht explizit geplant.

## Offene Fragen

1. **Schauspieler-Bilder**: Sollen in der ersten Version gar keine Bilder gezeigt werden, oder soll ein generisches Platzhalterbild verwendet werden?
2. **Externe Namensnormierung**: Sollen Schauspielernamen getrimmt und normalisiert (z. B. "  John Doe " → "John Doe")? Reicht `NormalizedName = Name.ToUpperInvariant()`?
3. **Regisseur vs. Schauspieler**: `Movie` hat bereits `Director` und `Credits`. Sollen Regisseure ebenfalls in `Actor` eingehen, oder bleibt `Director` separat?
4. **Re-Klassifizierung bei Metadaten-Änderungen**: Wenn ein neuer Film in eine Sammlung kommt, wird der reguläre Scan alle `MovieActor`-Verknüpfungen für diesen Film neu anlegen. Sollen bestehende Sammlungs-Aggregationen neu berechnet werden (nur bei Detailabfrage live) oder gecacht?

## Urteil

Der Plan ist grundsätzlich umsetzbar, aber die Nacherfassungs-Start-Logik und die Autorisierungsgrenzen müssen vor der Implementierung präzisiert werden. Empfohlen: Implementierung in vertikalen Scheiben (Datenmodell → Parser → Hintergrundworker → API → UI) statt parallel, um Rücksetzpunkte zu erhalten.
