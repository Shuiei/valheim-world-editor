<!-- source: 43410d17 -->
# Tastenkürzel

Jede Tasten- und Mausaktion des Editors, danach gruppiert, was du gerade tust. Die Schaltfläche **?** in der oberen
Leiste (oder `F3`) zeigt die wichtigsten, während du arbeitest.

Die Tasten funktionieren nur im 3D-Editor: nicht, während du in ein Feld tippst, und nicht auf der Startseite oder der
Weltkarte (außer `F1`: Es funktioniert überall). Auf einem Mac drücke `Cmd` überall dort, wo diese Seite `Ctrl` sagt
(`Cmd+Z` macht rückgängig, `Cmd`+Klick fügt zu einer Auswahl hinzu), und `Option` für `Alt`.

## Kamera

| Taste oder Maus | Aktion |
|---|---|
| Rechts ziehen | Die Ansicht drehen. Beim Gehen und Fliegen: umherschauen. |
| Mausrad | Zoomen, von 3 m bis 3 km vom Punkt, auf den du blickst. |
| Mitte ziehen | Die Ansicht verschieben. |
| `Space` + links ziehen, `Shift` + rechts ziehen | Die Ansicht verschieben, in jedem Werkzeug (praktisch ohne mittlere Maustaste). |
| Links ziehen (Werkzeug **Ansicht**) | Die Ansicht verschieben. |
| `W` `A` `S` `D` oder die Pfeiltasten | Umherbewegen. Je weiter du herausgezoomt bist, desto schneller. Nicht, während `Ctrl` oder `Alt` gedrückt ist. |
| `F` | Auf Augenhöhe gehen, dann fliegen, dann zurück zur üblichen Ansicht, in jedem Werkzeug. |
| `Shift` (Gehen) | Rennen: 12 m/s statt 6. |
| `Space` / `C` (Fliegen) | Hoch / runter, auch durch den Boden (in die Tunnel und Keller einer Mod). `Shift`: 45 m/s statt 15. |

Beim Gehen oder Fliegen zoomt das Mausrad nicht, und Ziehen verschiebt die Ansicht nicht. Siehe
[Gehen und Fliegen](../editor-basics.md#gehen-und-fliegen).

## Panels und Speichern

| Taste | Aktion |
|---|---|
| `V` | Das Panel **Ansicht** öffnen oder schließen. |
| `L` | Den **Verlauf** öffnen oder schließen. |
| `?` (`Shift+/`) oder `F3` | Die Hilfekarte **Steuerung** öffnen oder schließen. |
| `F1` | Das Hilfefenster auf der Seite des benutzten Werkzeugs (siehe [Das Hilfefenster](#das-hilfefenster)). Das kleine **?** oben an jedem Panel öffnet die Seite dieses Panels. |
| `Ctrl+S` | **In Welt speichern** (eine gespeicherte Welt), **Live anwenden** (live), **Bauplan speichern** (die Werkstatt). |

## Ein Werkzeug wählen

| Taste | Werkzeug |
|---|---|
| `1` `2` `3` `4` `5` | Anheben, Absenken, Ebnen, Glätten, Urzustand |
| `0` | Naturnah |
| `O` (der Buchstabe) | Erodieren |
| `6` `7` `8` `9` | Der Pinsel **Malen** mit Erde, Kultivieren, Gepflastert, Farbe entfernen |
| `B` | Region |
| `P` | Pfad |
| `T` | Platzieren (in der Werkstatt **Bauen**) |
| `U` | Bauen |
| `G` | Gelände |
| `E` | Auswahl |
| `M` | Messen |
| `H` oder `Esc` | Ansicht (`Esc` nur, wenn es sonst nichts abzubrechen gibt, siehe [Wie Esc funktioniert](#wie-esc-funktioniert)) |

Die Ziffern des Ziffernblocks funktionieren auch. **Dungeon**, **Skript** und **Baupläne** haben keine Taste: Klicke
sie in der Werkzeugleiste an. In der Werkstatt wählen nur `U` oder `T`, `E` und `H` oder `Esc` ein Werkzeug.

## Rückgängig, Kopieren und Einfügen

| Taste | Aktion |
|---|---|
| `Ctrl+Z` | Die letzte Änderung rückgängig machen. |
| `Ctrl+Y` oder `Ctrl+Shift+Z` | Sie wiederholen. |
| `Ctrl+C` | Den Rahmen oder das Polygon von Region oder die ausgewählten Objekte kopieren (Werkzeuge Region und Auswahl). |
| `Ctrl+X` | Ausschneiden: dieselbe Kopie, dann verlassen die Objekte die Welt in einem Schritt (der Boden bleibt). |
| `Ctrl+V` | Mit dem Einfügen der Zwischenablage beginnen, aus jedem Werkzeug. |
| `[` / `]` | Pinselgröße 1 m kleiner / größer (1 bis 30 m). |

## Objekte auswählen

Diese funktionieren im Werkzeug **Auswahl** (`E`). Ein Klick im Werkzeug **Ansicht** wählt Objekte auf dieselbe Weise.

| Taste oder Maus | Aktion |
|---|---|
| Klick | Nur dieses Objekt auswählen. Erneuter Klick an derselben Stelle: das nächste Objekt dahinter. |
| `Ctrl`+Klick | Das Objekt hinzufügen, oder es herausnehmen, wenn es ausgewählt war. |
| `Shift`+Klick | Das Objekt aus der Auswahl herausnehmen. |
| `Ctrl+Shift`+Klick | Jedes gezeigte Objekt in einer Reihe hinzufügen, vom zuletzt angeklickten bis zu diesem. |
| Doppelklick | Ein Bauteil: das ganze Gebäude. Ein Teil eines Schiffs oder einer anderen verbundenen Gruppe: die ganze Gruppe. Jedes andere Objekt (eine Truhe, ein Schild, ein Baum): dieses Objekt, mit geöffnetem Inspektor. Im Werkzeug Ansicht wechselt ein Doppelklick auf ein Objekt zu Auswahl. |
| Ziehen auf leerem Boden | Auswählen, was im Rahmen liegt (oder in der Umrisslinie auf dem Boden, wenn **Ziehen zeichnet** so eingestellt ist). `Ctrl`: zur Auswahl hinzufügen. `Shift`: herausnehmen. |
| `Alt` + ziehen | Dasselbe, auch wenn der Beginn über Objekten liegt (Objekte werden nicht getroffen). |
| `Ctrl+A` / `Alt+A` | Jedes gezeigte Objekt auswählen / die Auswahl aufheben. |
| `Ctrl+I` | Die Auswahl umkehren. |
| `Ctrl+H` / `Alt+H` | Die ausgewählten Objekte in der Ansicht ausblenden / wieder zeigen. |
| `Alt+Z` | **Durch Objekte wählen** an oder aus: Ein Rahmen erfasst auch, was hinter anderen verdeckt ist. |
| `Shift+F` oder `.` auf dem Ziffernblock | Die Auswahl ins Bild rücken. |

## Je Werkzeug

### Form- und Malpinsel

| Taste oder Maus | Aktion |
|---|---|
| Links ziehen | Den Pinsel auf den Boden auftragen. |
| `Alt`+Klick | Die Bodenhöhe dort als Höhe für Ebnen übernehmen und zu **Ebnen** wechseln. |
| `Alt+Shift`+Klick | Den Höhenbereich der [Maske](../masks.md) auf 2 m über und unter dieser Stelle setzen (auch mit dem Werkzeug Pfad). |

### Region

| Taste oder Maus | Aktion |
|---|---|
| Ziehen (Rahmen) | Den Rahmen zeichnen. |
| Klick (Polygon) | Einen Punkt hinzufügen. Doppelklick: das Polygon schließen. |
| `Enter` | Das gerade gezeichnete Polygon schließen; bei fertiger Auswahl die gewählte Aktion ausführen. |
| `Enter`, dann `Del` | Mit **Objekte** gewählt: die Objekte darin auswählen, dann im Werkzeug Auswahl löschen. |
| `Backspace` | Den letzten Punkt des Polygons entfernen. |
| `Esc` | Die Auswahl (den Rahmen oder das Polygon) aufheben. |

### Pfad

| Taste oder Maus | Aktion |
|---|---|
| Klick | Einen Punkt hinzufügen. |
| Die Linie ziehen | Dort einen Punkt hinzufügen und ihn verschieben. |
| Einen Punkt ziehen | Ihn verschieben. |
| `Ctrl`+Klick auf einen Punkt | Ihn entfernen. |
| `Alt`+Klick | Die Bodenhöhe dort als Höhe des Pfads übernehmen (Rampe: ihr Anfang). |
| `Ctrl+Alt`+Klick | Rampe: die Höhe als ihr Ende übernehmen. |
| `Alt+Shift`+Klick | Den Höhenbereich der [Maske](../masks.md) setzen, wie mit einem Pinsel. |
| `Enter` | Den Pfad anwenden. |
| `Backspace` | Den letzten Punkt entfernen. |
| `Esc` | Die Linie löschen. |

### Platzieren

| Taste oder Maus | Aktion |
|---|---|
| Links ziehen (Pinsel) | Objekte unter dem Pinsel platzieren. `Shift` + ziehen: sie entfernen. |
| Klick (Linie, Polygon) | Einen Punkt hinzufügen. Den Umriss ziehen: dort einen Punkt hinzufügen. Einen Punkt ziehen: ihn verschieben. `Ctrl`+Klick auf einen Punkt: ihn entfernen. |
| Doppelklick auf den letzten Punkt | Entlang der Form platzieren. |
| Ziehen (Raster, Linienformen) | Den Rahmen des Rasters zeichnen, oder die Form von einem Ende zum anderen. |
| `Alt`+Klick | Die Höhe dort als **Höhenlage** übernehmen. |
| `Enter` | Entlang der Linie, des Kreises oder des Rechtecks platzieren, oder im Raster oder Polygon. |
| `Backspace` | Den letzten Punkt der Linie oder des Polygons entfernen. |
| `Esc` | Die gerade gezeichnete Form löschen oder **Aus Welt wählen** abbrechen. |
| `N` | Eine neue zufällige Anordnung. |
| `R` | Die Vorschau, ein gezeichnetes Raster oder ein Polygon um eine Vierteldrehung drehen. |
| `,` / `.` oder `Alt` + Mausrad | Die Vorschau, ein gezeichnetes Raster oder ein Polygon um 15° drehen (`Shift`, feiner: 1°). |
| `PgUp` / `PgDn` oder `Ctrl` + Mausrad | Das Platzierte um 0,5 m heben oder senken (`Shift`, feiner: 0,1 m). |
| Klick auf ein Objekt (**Aus Welt wählen**) | Seine Art übernehmen. `Shift`+Klick fügt sie zu den gewählten Arten hinzu. |

### Bauen, Einfügen und eine Auswahl verschieben: die Schritte der Bauleiste

[Bauen](../build.md), Einfügen und Auswahl drehen und heben mit denselben Tasten, um die Schritte, die in
[der Bauleiste](../build.md#die-bauleiste) eingestellt sind (**Drehen um**, anfangs 22.5°; **Anhebung**, anfangs
0.5 m). `Shift` macht den Schritt immer feiner.

| Taste oder Maus | Aktion |
|---|---|
| `,` / `.` oder `Alt` + Mausrad | Das Bauteil, das Eingefügte oder die Auswahl um den Schritt **Drehen um** drehen (`Shift`: 1°). |
| `PgUp` / `PgDn` oder `Ctrl` + Mausrad | Es um den Schritt **Anhebung** heben oder senken (`Shift`: ein Fünftel davon). |

### Bauen (Welten, die Werkstatt und Bauen in einem Dungeon)

| Taste oder Maus | Aktion |
|---|---|
| Klick | Das Bauteil setzen (**Verlegen**: **Ein Bauteil**) oder einen Punkt hinzufügen (**Linie**). |
| Ziehen | Eine Reihe des Bauteils (**Ein Bauteil**); den Kreis, das Rechteck oder das Raster (**Verlegen**). |
| `Alt` halten | Das Bauteil viertelmeterweise setzen, ohne Einrasten. |
| `R` | Das Bauteil um eine Vierteldrehung drehen. |
| `Q` / `E` | Mit **Einrasten** auf **Manuell**: der vorige / nächste Einrastpunkt des Bauteils. |
| `Shift` + Rechtsklick | Das Bauteil unter dem Zeiger entfernen. |
| Mittelklick | Mit der Art des Bauteils unter dem Zeiger bauen. |
| `Enter` | Die Linie, den Kreis, das Rechteck oder das Raster setzen. |
| `Backspace` | Den letzten Punkt der Linie entfernen. |
| `Esc` | Die gerade gezeichnete Linie, Figur oder das Raster löschen. |

### Einfügen

| Taste oder Maus | Aktion |
|---|---|
| Klick | Das Eingefügte setzen. Erneut klicken für weitere Kopien. |
| `R` | Um eine Vierteldrehung drehen. |
| `X` | Spiegeln. |
| `Esc` | Das Einfügen beenden, zurück zum Werkzeug Region. |

### Die Auswahl verschieben (Auswahl)

| Taste oder Maus | Aktion |
|---|---|
| Ein ausgewähltes Objekt ziehen | Die Auswahl verschieben. |
| Einen Pfeil oder den Ring ziehen | Entlang dieser Achse verschieben oder drehen. |
| `R` | Die Auswahl um eine Vierteldrehung drehen. |
| `End` | Sie auf den Boden absetzen. |
| `Del` | Die ausgewählten Objekte löschen. |
| `I` | Den Inspektor des ausgewählten Objekts öffnen (ein Objekt ausgewählt). |
| `Esc` | Ein laufendes Verschieben abbrechen, oder einen gerade gezeichneten Rahmen oder Umriss. |

### Andere Werkzeuge

| Werkzeug | Taste oder Maus | Aktion |
|---|---|---|
| Gelände | Klick | Die Geländeform dort setzen: die Form der Formel, den Berg oder das Bild. |
| Messen | Zwei Punkte anklicken | Zwischen ihnen messen. `Esc` löscht die Messung. |
| Dungeon | `R` | Den hinzuzufügenden Raum drehen: Er schließt mit seiner nächsten Öffnung an. |
| | `Del` | Den ausgewählten Raum löschen. |
| Skript | `Ctrl+Enter` | Das Skript ausführen (im Codefeld). |
| Inspektor | `Esc` | Ihn schließen. |
| Das Werkzeug einer Mod | Seine eigenen Tasten | Ein Werkzeug unter **MODS** in der Werkzeugleiste bekommt die Tasten, die es benutzt, zuerst; die anderen funktionieren wie gewohnt. |

## Wie Esc funktioniert

`Esc` tut das Erste davon, das zutrifft, und hört dann auf:

1. Bricht das Wählen einer Art mit **Aus Welt wählen** ab.
2. Beendet das Einfügen (zurück zum Werkzeug Region).
3. Schließt den Inspektor.
4. Bricht ab, was das Werkzeug gerade tut: ein Verschieben in Auswahl, einen gerade gezeichneten Rahmen oder Umriss,
   eine Form in Platzieren oder eine mit Bauen verlegte Linie, einen Rahmen oder ein Polygon von Region, die Punkte
   von Pfad, eine Messung.
5. Wechselt zum Werkzeug Ansicht.

## Die Weltkarte und eine neue Welt

| Maus | Aktion |
|---|---|
| Ziehen | Die Karte verschieben. |
| Mausrad | Zur Maus hin zoomen. |
| Klick | Eine Stelle wählen (die Zone für **In 3D bearbeiten**). |
| Doppelklick (Karte einer neuen Welt) | Die ganze Welt zeigen. |
| `Enter` in einem Suchfeld | Suchen. |

## Das Hilfefenster

| Taste | Aktion |
|---|---|
| `F1` | Die Hilfe bei dem öffnen, was du gerade benutzt: das Werkzeug, der Inspektor, die Karte, die Startseite… (jede Seite). |
| `Ctrl+F` | Die Hilfe durchsuchen. |
| `Alt+Left` / `Alt+Right` | Zurück / vor. |
| `Esc` | Ein vergrößertes Bild schließen, sonst die Suche leeren. |
| `Enter` im Suchfeld | Die erste gefundene Seite öffnen. |

## Verwandte Seiten

- [Das Editorfenster](../editor-basics.md): die obere Leiste, die Werkzeugleiste, die Kamera und die Panels.
- [Auswahl](../select.md): Objekte auswählen und verschieben.
- [Platzieren](../place.md), [Bauen](../build.md), [Pfad](../path.md), [Region, Kopieren und Einfügen](../area.md):
  was die Tasten jedes Werkzeugs tun.
- [Glossar](glossary.md): die Begriffe, die der Editor verwendet.
