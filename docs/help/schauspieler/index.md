# Schauspieler

## Übersicht

Die Schauspieler-Ansicht listet alle Schauspieler, die in den Metadaten der erfassten Filme und Serien gefunden wurden.

## Bilder

Schauspielerbilder werden aus dem `<thumb>`-Element der NFO-Metadaten geladen (lokale Datei oder URL) und in der Übersicht sowie in der Detailansicht angezeigt.

## Menü

Über den Menüpunkt "Schauspieler" in der Navigation gelangen Sie zur Übersicht.

## Suche und Filter

- **Suchfeld:** Filtern nach Schauspielernamen.
- **Buchstaben-Filter:** Es werden nur die Anfangsbuchstaben angezeigt, zu denen Schauspieler existieren.

## Detailansicht

Klicken Sie auf einen Schauspieler, um alle Medien zu sehen, in denen er mitwirkt.

- Wirkt ein Schauspieler in **allen Filmen einer Filmsammlung** mit, wird nur die Sammlung gelistet.
- Wirkt er in **nur einem Film**, wird nur dieser Film gelistet.
- Bei **mehreren, aber nicht allen** Filmen einer Sammlung entscheidet der konfigurierbare Schwellenwert (`ActorCollectionThresholdPercent`, Standard 50 %): Ab dem Schwellenwert wird die Sammlung mit den betroffenen Filmen gelistet, sonst die einzelnen Filme.
- Bei Serien gilt analog: Mitwirkung in allen Episoden einer Staffel → Staffel; Mitwirkung in allen Staffeln einer Serie → Serie; ansonsten einzelne Episoden.
