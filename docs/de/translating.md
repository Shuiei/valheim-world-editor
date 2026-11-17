<!-- source: 45d36264 -->
# Den Editor übersetzen

Die Texte des Editors stehen in Katalogen, einer pro Sprache: eine JSON-Datei mit Texten nach Schlüssel. Englisch,
[`editor/Core/Lang/en.json`](../../editor/Core/Lang/en.json), ist die Quelle, aus der jeder andere Katalog übersetzt
wird.

Der Editor bringt Englisch, Französisch (`fr`), Deutsch (`de`), Spanisch (`es`), brasilianisches Portugiesisch (`pt-BR`,
auch auf einem auf portugiesisches Portugiesisch eingestellten Computer gezeigt), Thai (`th`) und Filipino (`fil`, auch auf einem auf Tagalog eingestellten Computer gezeigt) mit. Die Dokumentation ist in jede davon übersetzt (`docs/de/`…, ihre Bilder in dieser Sprache aufgenommen). Er zeigt die unter
**Einstellungen → Sprache** gewählte Sprache, standardmäßig die des Computers, wenn es einen Katalog dafür gibt. Ein
Text, den ein Katalog noch nicht hat, erscheint auf Englisch. Eine neue Sprache gilt ab dem nächsten Start des Editors.

Noch kommt nicht jeder Text aus dem Katalog. Diese schon:

- jeder Hover-Tipp (die Tipps des Inspektors, die aus den Daten eines Objekts gebildet werden, behalten dessen Namen, wie
  der Speicherstand sie hat);
- die Startseite, die Einstellungen, die Weltkarte, die Seite Neue Welt und die kleinen Dialoge (Fragen, Meldungen, die
  Details eines Bauplans) mit den Wörtern, die sie teilen;
- die obere Leiste des 3D-Editors, mit dem, was Speichern, Anwenden, Verwerfen, Verlassen und Beenden fragen und sagen,
  den Meldungen des Live-Spiels und den Spielern;
- die Anzeigen der Statusleiste, das Panel Verlauf, das Panel Ansicht, die Hilfekarte und was Statusleiste und Verlauf
  über Claudes Änderungen sagen;
- die Werkzeuge: die Pinsel mit ihren Stempeln, Messen, Maske, Skript, Pfad, Gelände, Auswahl, Einfügen, Platzieren,
  Bauen (die Leiste Bauen und die Bauleiste), Region, das Werkzeug Dungeon mit seinem Generator, das Panel Baupläne, die
  Werkstatt und der Inspektor;
- die Namen der Biome.

Der Rest zieht Fenster für Fenster um. Ein jetzt erstellter Katalog funktioniert weiter, wenn mehr Texte hinzukommen: die
neuen erscheinen auf Englisch, bis sie übersetzt sind.

## Einen Katalog erstellen

1. Kopiere `en.json` in den Ordner `lang` im Datenordner des Editors
   (`~/.local/share/ValheimWorldEditor/lang` unter Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` unter
   Windows; **Einstellungen → Sprache** zeigt ihn), benannt nach dem Code der Sprache: `fr.json`,
   `de.json`, `pt-BR.json`.
2. Übersetze die Werte, nie die Schlüssel. `lang.name` ist der eigene Name der Sprache („Français“), wie die
   Einstellungen ihn auflisten.
3. Behalte jedes `{0}`, `{1}`…: der Editor setzt dort einen Wert ein (einen Ordner, eine Zahl). Ihre Reihenfolge im Satz
   darf sich ändern. Ein Text, dessen Platzhalter sich vom Englischen unterscheiden, oder mit einem verirrten `{` oder
   `}`, wird nicht verwendet.
4. Wähle die Sprache in den **Einstellungen**, starte den Editor neu und sieh nach. Schlüssel, die du weglässt oder aus
   deiner Kopie löschst, erscheinen auf Englisch.

Derselbe Ordner nimmt auch Korrekturen an: eine Datei dort mit dem Code eines Katalogs, den der Editor mitbringt, wird
Text für Text darübergelegt. Dort behält ein weggelassener Schlüssel (oder ein Text, der nicht verwendet wird) die
mitgebrachte Übersetzung; Englisch erscheint nur, wo keiner von beiden den Text hat.

Damit der Editor einen Katalog mitbringt, öffne einen Pull Request, der ihn zu `editor/Core/Lang/` hinzufügt (nur
Schlüssel aus `en.json`; `LangCatalogueTests` prüft sie).

## Die Dokumentation übersetzen

Das Hilfefenster zeigt die Dokumentation in der Sprache des Editors, wenn es eine Übersetzung der Seite gibt, sonst auf
Englisch. Eine Übersetzung der Dokumentation ist ein Ordner von `docs/`, benannt nach dem Code der Sprache, wie ihr
Katalog: `docs/fr/`, `docs/pt-BR/`. Ihre eigene `README.md` macht sie zu einer (die erste Seite und das
Inhaltsverzeichnis des Hilfefensters); die englischen Ordner (`guides/`, `help/`, `concepts/`, `start/`) haben keine.

- **Seiten.** Der Ordner spiegelt die englischen Seiten: `docs/fr/map.md` übersetzt `docs/map.md`, und
  `docs/fr/guides/make-a-road.md` übersetzt `docs/guides/make-a-road.md`. Übersetze die Seiten, die das Hilfefenster
  enthält ([writing-docs.md](../dev/writing-docs.md) listet die, die es nicht enthält). Eine noch nicht übersetzte Seite
  erscheint auf Englisch, also kann eine Übersetzung mit ein paar Seiten beginnen.
- **Links.** Schreibe sie so, wie die englische Seite sie schreibt, vom Ordner aus: `map.md#…` und `guides/x.md` aus
  `docs/fr/README.md`, `../area.md` aus `docs/fr/guides/`. Ein Link, der aus `docs/` hinausführt, steigt ein `../` mehr
  hinauf als der englische (`../../README.md`, `../../editor/Core/Lang/en.json`), ebenso ein Link zu einer nicht
  übersetzten Datei von `docs/` (`../worldeditor.schema.json`). Links zu einer Seite, die der Ordner noch nicht hat,
  funktionieren im Editor trotzdem (er zeigt die englische Seite), auf GitHub aber erst, wenn die Seite übersetzt ist.
- **Bilder.** Ein Bild mit Text darin wird in deiner Sprache in `docs/fr/images/` neu aufgenommen, unter demselben Namen,
  und als `images/x.webp` geschrieben. Eines ohne etwas zu übersetzen kann das englische bleiben: `../images/x.jpg`.
  Solange ein Bild von `images/` nicht in deinem Ordner liegt, zeigt das Hilfefenster das englische gleichen Namens.
- **Überschriften.** Übersetze sie; ihre IDs ändern sich mit ihnen (GitHub bildet sie aus den Wörtern:
  `#search-the-world` wird zu `#rechercher-dans-le-monde`), und die eigenen Links der Übersetzung nutzen die übersetzten
  IDs, wie GitHub es braucht. Behalte die Überschriften der englischen Seite: genauso viele, auf denselben Ebenen, in
  derselben Reihenfolge. Der Editor führt dann auch die englischen IDs zu ihren übersetzten Überschriften, sodass seine
  Hilfeknöpfe (`F1`, das `?` der Panels), die englische Überschriften nennen, an der richtigen Stelle landen.
- **Quellmarke.** Setze als erste Zeile auf die Seite, aus welchem englischen Text sie übersetzt wurde:
  `<!-- source: 1a2b3c4d -->`, die ersten 8 Zeichen des SHA-256 der englischen Seite (weder GitHub noch das
  Hilfefenster zeigt sie). Du bekommst sie mit `sha256sum docs/map.md | cut -c1-8` (macOS: `shasum -a 256`). Die Marke
  wird über die Seite mit Unix-Zeilenenden berechnet: bei einem Windows-Checkout, der sie in `\r\n` umgewandelt hat,
  nutze `git show HEAD:docs/map.md | sha256sum | cut -c1-8`, was die Seite so liefert, wie das Repository sie aufbewahrt.

Ändert sich die englische Seite, ändert sich ihre Marke: das Hilfefenster zeigt die Übersetzung weiterhin, mit dem
Hinweis, dass sich die Seite auf Englisch geändert hat, und einem Knopf, der die englische Seite zeigt. Um eine
Übersetzung auf den neuesten Stand zu bringen, sieh nach, was sich seitdem auf Englisch geändert hat
(`git log -p docs/map.md`), übersetze es und schreibe die neue Marke. Die Tests prüfen jeden Link und jedes Bild einer
Übersetzung und dass eine aktuelle die Überschriften ihrer englischen Seite hat.

## Für Entwickler

- Ein Text, den der Benutzer sieht, kommt in `en.json` unter einen Schlüssel, der danach benannt ist, wo er gezeigt
  wird (`settings.port`, `common.cancel` für Wörter, die überall vorkommen), und wird mit `Lang.T("settings.port")`
  abgefragt, oder mit `Lang.T("settings.worldNotFound", path)` mit Werten für seine `{0}`…-Platzhalter (mit der
  invarianten Kultur eingesetzt). Ein Hover-Tipp ist ein `tip.`-Schlüssel, vergeben mit `.Tip("settings.port")` (siehe
  `editor/Desktop/Tips.cs`).
- Schreibe den Schlüssel im Code vollständig aus, auch wenn er weitergereicht wird: `LangCatalogueTests` prüft, dass
  jeder abgefragte Schlüssel in `en.json` steht und jeder Schlüssel von `en.json` irgendwo abgefragt wird.
- Texte für Claude (die Beschreibungen und Antworten der MCP-Werkzeuge), das Protokoll, Kommandozeilenausgaben und
  Dateiformate bleiben Englisch und außerhalb des Katalogs. Ein Text, den der Benutzer sieht und den auch Claude bekommt
  (die Kosten eines Bauteils), fragt auf Claudes Seite mit `Lang.En` nach Englisch.
- Fenster werden einmal gebaut, in der beim Start gewählten Sprache (`Lang.Use` in `Program.Main`); nichts muss auf
  einen Sprachwechsel hören.

### Schlüssel benennen

- Der erste Teil ist das Fenster, so geschrieben wie seine Tipp-Schlüssel (`area`, `place`, `select`, `path`,
  `blueprints`, `newWorld`…). Ein im Fenster umbenanntes Werkzeug behält seine Schlüssel: die des Werkzeugs Region sind
  `area.*`, und die Arten des Werkzeugs Gelände behalten `shape.*` (Formel) und `mountain.*` neben `landform.*`.
  Gemeinsame Präfixe: `common.*` für ein Wort, das in drei oder mehr Fenstern vorkommt (OK, Ja, Abbrechen, Speichern,
  Verwerfen, Bleiben, Behalten…), `dialog.*` für die kleinen Dialoge, `save.*` für die Fragen zu Speichern, Anwenden,
  Verwerfen und Verlassen, `biome.*` für Biomnamen.
- Der mittlere Teil sagt, welche Art von Text es ist: `label` (Zeilenbeschriftungen, Schaltflächen, Schalter), `head`
  (Abschnittsüberschriften), `hint` (gedämpfte Erklärungen), `msg` (Statusleiste und Ergebnisse), `undo` (Einträge im
  Verlauf), `error` (in einem Panel gezeigte Probleme), `ask.<name>.title|text|yes|no` (Fragen) oder der Name einer
  Liste für ihre Einträge (`brush.falloff.flatTop`). Die einzelnen Texte eines Fensters kommen mit zwei Teilen aus
  (`settings.port`). Der letzte Teil ist camelCase.
- Ein englisches Wort hat einen Schlüssel: verwende `common.*` wieder, statt „Save“ noch einmal hinzuzufügen. Claudes
  Namen für die Elemente des Fensters werden aus dem Englischen ermittelt (`Lang.EnglishOf`), das das braucht.
- Ein in den Katalog verschobener Text behält seinen Wortlaut; ändere Wörter in einer eigenen Änderung.
- Die Texte eines Enums kommen aus einem `switch`, der jeden vollständigen Schlüssel nennt, nie aus `"prefix." + value`.

### Werte, Plurale und Sätze

- Werte kommen in `{0}`, `{1}`… mit ihrem Format und ihrer Einheit im Text (`"{0:0.0} m"`,
  `"{0} × {1} zones"`), als rohe Zahlen und Namen, nicht als formatierte Zeichenketten. Ein Schlüssel wird immer mit
  derselben Anzahl an Werten abgefragt; ein mit Werten abgefragter Text verdoppelt seine wörtlichen Klammern
  (<code>&#123;&#123;</code>), einer ohne Werte hat nie welche.
- Ein Name, der schon in der Sprache des Benutzers ist (ein Biom, eine Objektart), kommt als Wert hinein. Ändere seine
  Groß- und Kleinschreibung nicht, um ihn in einen Satz einzupassen (`ToLowerInvariant`): schreibe den Satz um ihn
  herum.
- Es gibt keine Pluralregeln. Behalte „object(s)“ in einem Text; wo der Code schon zwischen einem und mehreren wählt,
  nutze zwei vollständige Schlüssel (`….one`, `….many`). Füge nie im Code eine Endung an.
- Ein Schlüssel pro ganzem Satz. Ein optionaler zusätzlicher Satz ist ein eigener Schlüssel, mit einem Leerzeichen
  angefügt; klebe nie Teile eines Satzes zusammen. Listen übersetzter Namen werden mit „, “ verbunden.

### Was Englisch bleibt

- Alles, was Claude liest: die Beschreibungen und Antworten der MCP-Werkzeuge (`ClaudeTools*`, `ClaudeGuide`), das
  Protokoll und die Konsole, Kommandozeilenausgaben, Dateiformate (`prefs.json`, die Verlaufsdatei, Baupläne),
  Spieldaten (Prefab-, Gegenstands- und Bauteilnamen, Datenschlüssel von Objekten), die Namen von Produkten und Mods in
  den Texten (Valheim, Homestead, Steam) und die Meldungen von Exceptions (der Satz um eine herum wird übersetzt).
- Claudes Schritte im Verlauf beginnen mit der Marke `Claude: ` (`ClaudeTools.ClaudesStep`), die Englisch bleibt:
  Claudes Änderungen zurücknehmen findet Claudes Schritte daran. Die Wörter danach werden übersetzt.
- Ein Text, den der Benutzer sieht und den auch Claude bekommt, hat beides: die Sprache des Benutzers aus `Lang.T`,
  Claudes Englisch aus `Lang.En` (`PieceCost.Describe(english: true)`).
- Die Meldung der Statusleiste wird von Claude zurückgelesen. Setze sie mit `Msg("area.msg.applied", n)` dorthin, oder
  mit `Msg(said)` und einem `Said` (`Said.Of(key, values…)`, `Said.Raw(text)` für einen in beiden gleichen Text, `+`, um
  einen Satz anzufügen): das Fenster zeigt die Sprache des Benutzers und behält das Englische, das Claudes Antworten
  lesen (`MainWindow.LastMessageEn`). Ergänze die letzte Meldung mit `LastSaid + …`, nie durch Lesen von
  `MessageText.Text`.
- Eine gemerkte Listenauswahl (`Prefs.Bind`) wird auf Englisch gespeichert, sodass sie in jeder Sprache wiederkommt.

### Ein Fenster prüfen

Die Tests haben eine erfundene Sprache, `PseudoLang`, die jeden Katalogtext als `⟦…⟧` einschließt.
`PseudoLang.Untranslated(window)` listet die Texte, die ein Fenster außerhalb von `⟦⟧` zeigt, also nicht aus dem
Katalog. Jedes Fenster hat einen Test `<Window>LangTests`, der es in dieser Sprache baut und keine erwartet; solche Tests
laufen in der Sammlung `DataDir`, da die Sprache geteilt wird.
