<!-- source: 9e216b4e -->
# Ein Gebäude verschieben oder kopieren

Verschiebe ein Haus mit allem darin ein paar Meter weit oder quer durch die Welt, oder setze eine Kopie davon woanders hin.

**Du brauchst:** eine im Editor offene Welt, mit offenem Bereich um das Gebäude (vorher eine Sicherung: siehe
[Eine Welt sichern](back-up-a-world.md)).

## Schritte

1. Drücke `E`, um [Auswahl](../select.md) zu öffnen.
2. Doppelklicke auf eine Wand des Gebäudes: Jedes verbundene Bauteil wird ausgewählt, auch Truhen und Möbel auf seinen Böden.
3. Optional: Um lose Objekte darin hinzuzufügen (einen abseits stehenden Tisch), `Ctrl` + Klick darauf.
4. Für ein kurzes Verschieben ziehe ein ausgewähltes Bauteil oder ziehe einen Pfeil, um entlang einer Achse zu verschieben; ziehe den orangefarbenen Ring, um es zu drehen.
   Lass **Beim Verschieben jedes Objekt auf den Boden setzen** an, damit das Gebäude dort auf dem Boden sitzt, wo es landet.
5. Für ein weites Verschieben drücke `Ctrl+X` (oder `Ctrl+C` für eine Kopie). Das Gebäude ist jetzt in der Zwischenablage.
6. Öffne den Bereich an der neuen Stelle (von der [Karte](../map.md) aus), oder bleib in diesem.
7. Drücke `Ctrl+V`: Das Gebäude folgt dem Mauszeiger, durchsichtig.
8. Drehe es mit `R` (eine Vierteldrehung) oder `,` `.` (um **Drehen um** der Bauleiste), dann klicke dorthin, wo es
   stehen soll.
9. Drücke `Esc`, um das Einfügen zu beenden.
10. Klicke auf **In Welt speichern** (oder **Live anwenden**).

Kopien und verschobene Bauteile behalten alles, was sie enthalten: Truheninhalte, Schildtexte, wer sie gebaut hat. Ein mit
`Ctrl+V` eingefügtes Gebäude nimmt den Boden darunter mit.

## Prüfen, ob es geklappt hat

Die Statusleiste sagt, wie viele Objekte verschoben oder eingefügt wurden. Sieh dir das Gebäude von mehreren Seiten an: Nichts
sollte schweben oder zurückgeblieben sein. Öffne die Welt nach dem Speichern im Spiel und geh hinein.

## Rückgängig machen

- Vor dem Speichern: `Ctrl+Z` nimmt das Verschieben, das Ausschneiden oder das Einfügen zurück (ein Ausschneiden und sein Einfügen sind getrennte Schritte).
- Nach dem Speichern: siehe [Einen Schritt rückgängig machen, eine Änderung zurücknehmen oder alles verwerfen](undo-changes.md) oder
  [Einen Bereich aus einer Sicherung wiederherstellen](restore-from-backup.md).

## Tipps

- Ein Einfügen muss in den offenen Bereich passen: Steht dort *Nicht eingefügt*, öffne einen größeren Bereich um die Stelle.
- Die Zwischenablage bleibt erhalten: Du kannst in einem anderen Bereich oder einer anderen Welt einfügen.
- Um um genaue Meter zu verschieben, benutze **GENAUE POSITION** → **Verschieben um** im Panel Auswahl.
- Ein gezogenes Gebäude rastet an Bauteilen im Umkreis von 0,75 m ein, solange **Einrasten** auf **Auto** steht, in
  [der Bauleiste](../build.md#die-bauleiste). Wähle **Frei**, um es genau dort zu lassen, wohin du es ziehst.
- Bei einem Schiff oder einem anderen Fahrzeug einer Mod wähle einen beliebigen Teil aus: Die ganze Gruppe bewegt sich.
- Im Live-Modus bleibt ein Gebäude, das verschoben wird, während kein Spieler in der Nähe ist, im Spiel dasselbe Objekt, sodass Mods, die es
  verfolgen, es weiter verfolgen: siehe
  [Ein verschobenes Objekt bleibt dasselbe Objekt](../concepts/live-editing.md#ein-verschobenes-objekt-bleibt-dasselbe-objekt).

## Verwandte Seiten

- [Auswahl](../select.md) · [Region](../area.md)
- [Einrasten](../concepts/objects.md#einrasten)
- [Einen Bauplan in eine Welt einfügen](paste-a-blueprint.md)
