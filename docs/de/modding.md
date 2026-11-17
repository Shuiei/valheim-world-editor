<!-- source: 1ccdce30 -->
# Für Mod-Autoren

Der Editor funktioniert mit Bau-Mods, ohne dass ihre Autoren etwas beisteuern müssen: mit einem gemoddeten Spiel
verbunden, lernt er die Objekte der Mods, zeichnet sie mit ihren eigenen Modellen und benennt die Daten, die sie
speichern. Diese Seite ist für Mod-Autoren, die mehr wollen: eine optionale Datei `worldeditor.json` für das, was der
Editor nicht erraten kann, **verknüpfte Objekte** für Mods, die ein Ding aus vielen Objekten bauen,
**Editor-Erweiterungen** (eine kleine DLL, gegen das SDK des Editors gebaut) für das, was eine Datei nicht sagen kann,
**Add-ons** (dasselbe, von irgendwem für die Mod eines anderen gemacht) und **Bridge-Hooks**, damit das eigene Plugin
einer Mod am Live-Modus teilnimmt.

Alles hier ist optional, und jeder Schritt baut auf dem vorigen auf. Der Editor bringt für keine Mod etwas mit: eine Mod
im Editor zu unterstützen, ist Sache ihres Autors, mit diesen Mitteln. Die Beispiele auf dieser Seite stammen von einer
erfundenen Mod, „Example Vehicles“, deren vollständiges Beispiel mit dem SDK kommt (siehe
[Das Beispiel Example Vehicles](#das-beispiel-example-vehicles)).

## Was der Editor von selbst lernt

| Was | Wie der Editor es lernt |
|---|---|
| Die Objekte der Mods: Namen, Bauteile, Kosten, Handwerksstationen, das Werkzeug, das jedes baut | Mit einem Spiel verbunden (Live-Modus, WorldEditorBridge 1.26.0 oder neuer), fragt der Editor das Spiel nach jedem Objekt, das es kennt, und behält die, die nicht zu Valheim selbst gehören. Sie werden in `mod-catalogue.json` im Datenordner des Editors aufbewahrt, sodass die Welt eines gemoddeten Servers, später offline aus ihren Dateien geöffnet, sie ebenfalls zeigt. Die neuere Meldung einer Mod zu einem Bauteil ersetzt die ältere; eine Mod ändert nie eines der eigenen Objekte von Valheim. |
| Wie sie aussehen | Das Spiel sagt, was es von jedem Mod-Objekt zeichnet (WorldEditorBridge 1.27.0 oder neuer); der Editor findet diese Meshes, Materialien und Texturen in den eigenen Dateien der Mods auf dem Computer des Spielers (ihren Asset-Bundles, einmal aus den DLLs nach `mod-bundles` im Datenordner kopiert). Meshes, die eine Mod im Code baut, während das Spiel läuft, lassen sich nicht finden: diese Objekte erscheinen ohne Textur, außer eine [Erweiterung](#erweiterungen) zeichnet sie. |
| Die Namen der Daten, die Mods auf Objekten speichern | Das Spiel speichert nur einen Hash jedes Datenschlüssels. Der Editor findet die Namen unter dem Text, der in den DLLs der Mods steht (als Bytes gelesen, nie ausgeführt), in den ihm bekannten Plugin-Ordnern, und bewahrt sie in `mod-zdo-keys.json` auf, bis sich eine DLL ändert. Ein Schlüssel, dessen Name zusammengesetzt wird, während das Spiel läuft (`"Corner" + i`), lässt sich so nicht finden: benenne ihn unter `data` in deiner `worldeditor.json` (unten), sonst zeigt der Inspektor ihn als `#` und seinen Hash. |
| Aus welcher Mod ein Objekt stammt | Der Inspektor sagt es (wenn Jötunn installiert ist, das es weiß; sonst „Aus einer Mod“). Die Leiste [Bauen](build.md), in einer Welt und in der Werkstatt, listet die Bauteile der Mods im Reiter **Mod-Bauteile**, eine Mod nach der anderen. |

Siehe [Gemoddete Spiele](live-mode.md#gemoddete-spiele) für die Seite des Spielers.

## worldeditor.json

Ein paar Dinge kann der Editor nicht erraten. Eine Mod kann sie ihm in einer mitgelieferten Datei `worldeditor.json`
sagen, irgendwo in ihrem Ordner unter `BepInEx/plugins` (neben ihrer DLL ist gut). Das ist die Datei von Example
Vehicles:

```json
{
  "$schema": "https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/worldeditor.schema.json",
  "version": 1,
  "extension": "ExampleVehicles.WorldEditor.dll",
  "gameComponents": ["Ship"],
  "data": {
    "ExampleVehicles_Vehicle": { "label": "Vehicle", "readOnly": true, "help": "The vehicle this part is built on." },
    "ExampleVehicles_Offset": { "label": "Place on the vehicle", "readOnly": true },
    "ExampleVehicles_Packed": { "label": "Linked objects", "help": "The objects this one is tied to." }
  },
  "prefabs": {
    "example_boundary": { "hidden": true },
    "example_fleet": { "hidden": true, "data": true },
    "example_canal": { "hidden": true, "data": true },
    "example_hull": { "group": "Hulls" },
    "example_sail": { "group": "Sails and cables" },
    "example_post": { "group": "Sails and cables" },
    "example_pit": { "group": "Digging" }
  }
}
```

| Schlüssel | Was der Editor damit macht |
|---|---|
| `version` | Die Version des Formats: `1`. Eine Datei einer späteren Version oder ohne Version wird ganz übersprungen. |
| `data` | Nach Datenschlüssel, wie dein Code ihn nennt (der Name, dessen Hash das Spiel speichert; ein hier genannter Schlüssel ist unter seinem Namen bekannt, auch wenn deine DLL ihn nicht als Text enthält): `label`, wie der [Inspektor](select.md#inspektor) den Wert nennt (sonst sein Schlüssel; die eigenen Namen des Editors für die Schlüssel des Spiels haben Vorrang); `readOnly`, der Inspektor zeigt den Wert, lässt ihn aber nicht ändern (Werte, die deine Mod selbst setzt und die beim Bearbeiten kaputtgehen würden); `help`, was er ist, gezeigt, wenn man auf den Wert zeigt. |
| `prefabs` | Nach Prefab-Name: `hidden`, nicht in der Liste von [Platzieren](place.md) oder in der Leiste [Bauen](build.md) angeboten (deine Werkzeuge, Effekte, Hilfsobjekte; Objekte davon, die schon in einer Welt sind, erscheinen weiterhin); `group`, die Überschrift, unter der dein Bauteil im Reiter **Mod-Bauteile** der Leiste Bauen gelistet wird, nach dem Namen der Mod („*Mod-Name* · Hulls“). Ohne Gruppe wird ein Bauteil unter seiner Handwerksstation gelistet, sonst unter „Keine Station nötig“. `group` zählt nur für die eigenen Bauteile der Mods. `data`: Objekte davon enthalten nur die Daten deiner Mod (ein Register, was in einem Gebiet gegraben wurde) und haben nichts zu sehen: der Editor zeichnet, wählt oder listet sie in einem Bereich nie (deine Erweiterung liest sie trotzdem), und ein [Zonen-Reset](area.md) behält sie, wenn er die Gebäude behält, und entfernt sie sonst mit allem anderen. `piece`: ein Bauteil, das deine Mod im Code erzeugt und das kein Bundle enthält: `{ "name": "Track 2 m", "cost": { "Stone": 4 }, "station": "Stonecutter" }` (`name` erforderlich, höchstens 64 Zeichen; `cost` nach Prefab-Name des Gegenstands, höchstens 16 Gegenstände; `station` nach ihrem englischen Namen). Der Reiter **Mod-Bauteile** der Leiste Bauen listet es unter deiner Mod, auch ohne Live-Spiel (die Angabe eines Live-Spiels dazu hat Vorrang). Bauteile in deinen Bundles brauchen das nicht: der Editor findet sie über ihr `Piece` oder über ihre Einrastpunkte, wenn du das `Piece` im Code hinzufügst. |
| `links` | Wie deine Objekte zu einem verknüpft sind: siehe [Verknüpfte Objekte](#verknüpfte-objekte). |
| `ties` | Wie deine Objekte an eines gebunden sind, das sie nennt (die Waggons eines Zugs): siehe [Gebundene Objekte](#gebundene-objekte). |
| `extension` | Die DLL deiner Editor-Erweiterung, relativ zum Ordner der Datei und darin: siehe [Erweiterungen](#erweiterungen). |
| `gameComponents` | Klassennamen der eigenen Skripte des Spiels, deren Felder deine Erweiterung braucht (`"TerrainOp"`, `"Ship"`; höchstens 32): solange deine Erweiterung läuft, liest der Editor jede Komponente davon in den Prefabs des Spiels aus dem Valheim des Spielers (einmal, dann aufbewahrt, bis sich das Spiel ändert) und übergibt sie ihr als `Host.Game` (siehe [Hooks](#hooks)). |
| `settingsFiles` | Dateinamen der BepInEx-Einstellungsdateien deiner Mod, die deine Erweiterung liest (`"author.mod.cfg"`, in `BepInEx/config`; höchstens 8): wenn sich eine Welt öffnet, liest der Editor sie aus dem ersten BepInEx-Ordner des Spielers, der die jeweilige Datei hat, und live aus der des verbundenen Spiels über dessen Bridge (der eines Servers, dessen synchronisierte Einstellungen seine Spieler bekommen). Deine Erweiterung liest eine Einstellung mit `Host.Setting(file, section, key)` (siehe [Hooks](#hooks)). |
| `worldGenerators` | Die Weltgeneratoren, die deine Erweiterung registriert (siehe [Weltgeneratoren](#weltgeneratoren)), nach der ID, die die Dateien ihrer Welten nennen, jeweils mit dem Namen deiner Mod für den Spieler: `{ "author.mod": "Some Mod" }` (höchstens 8). Der Editor liest es auch, solange deine Erweiterung nicht läuft, damit eine Welt, die deine Mod gemacht hat, sagt, warum sie sich nicht öffnen lässt: das Add-on ausgeschaltet, wartet auf deine Mod, läuft nicht, oder die Erweiterung deiner Mod nicht eingeschaltet. |

### Wie der Editor sie liest

- **Wo.** In jedem `plugins`-Ordner, den der Editor kennt: dem `BepInEx` des Valheim-Ordners, dem Mod-Manager-Profil,
  in das der Editor selbst installiert wurde, den unter **Einstellungen → BepInEx-Ordner** hinzugefügten Ordnern und
  jedem Profil von r2modman und Thunderstore Mod Manager. Jede Datei namens `worldeditor.json` dort zählt, in beliebiger
  Tiefe.
- **Wann.** Jedes Mal, wenn sich eine Welt oder die Werkstatt öffnet, und wenn sich die **Einstellungen** öffnen. Eine
  Datei, die sich ändert, während eine Welt offen ist, zählt beim nächsten Mal.
- **Welche Mod.** Eine Datei gehört der Mod, deren Ordner direkt unter `plugins` liegt (das `ExampleVehicles` von
  `plugins/ExampleVehicles/worldeditor.json`). Die Einstellungen benennen Erweiterungen danach.
- **Zwei Dateien, ein Name.** Dateien werden von der ältesten an gelesen (nach dem Zeitpunkt ihrer letzten Änderung):
  sagen zwei etwas über denselben Datenschlüssel, dasselbe Prefab oder dieselbe Link-`id`, gewinnt die neueste Datei.
- **Nachsichtig.** Kommentare und abschließende Kommas sind erlaubt. Texte haben 1 bis 200 Zeichen; Listen enthalten
  höchstens 32 Namen; eine Datei hat höchstens 1 MB. Ein Eintrag, der nicht in dieser Form ist, wird weggelassen, und
  der Rest der Datei zählt trotzdem. Eine Datei, die kein JSON ist oder keine Version hat, die der Editor liest, wird
  übersprungen; das Protokoll des Editors sagt das („mod manifest …: not a worldeditor.json this editor reads,
  skipped.“).

Das [Schema](../worldeditor.schema.json) beschreibt die Datei: mit der Zeile `$schema` oben prüfen Code-Editoren die
Datei und vervollständigen ihre Schlüssel beim Tippen.

## Verknüpfte Objekte

Baut deine Mod Objekte aus anderen (ein Schiff und die darauf gebauten Bauteile, ein Karren und seine Ladung),
speichert das Spiel jedes Teil einzeln, und der Editor sieht lose Teile, dort aufgehäuft, wo das Spiel sie gespeichert
hat. Sag ihm, wie sie verknüpft sind, und er zeichnet sie so, wie deine Mod sie baut, und verschiebt, kopiert und
löscht jede Gruppe als Ganzes (siehe [Verknüpfte Objekte](select.md#schiffe-und-andere-verbundene-objekte) für das, was der
Spieler sieht). Es funktioniert für Mods, die auf die übliche Weise verknüpfen: die Wurzel (und jedes Teil, an dem
andere hängen) hat eine eigene int-ID; jedes Teil speichert in seinen Daten die ID dessen, woran es hängt, und seinen
Versatz und seine Drehung im Bezugssystem von diesem.

```json
{
  "version": 1,
  "links": [
    {
      "name": "vehicle",
      "id": "ExampleVehicles_Id",
      "parents": ["ExampleVehicles_Mount", "ExampleVehicles_Vehicle"],
      "offset": "ExampleVehicles_Offset",
      "rotation": "ExampleVehicles_Turn",
      "rotationQuaternion": "ExampleVehicles_TurnQ",
      "storedAt": "root",
      "references": ["ExampleVehicles_Target", "ExampleVehicles_Drives"],
      "dropOnCopy": ["ExampleVehicles_Packed", "ExampleVehicles_Aboard"]
    }
  ]
}
```

| Schlüssel | |
|---|---|
| `id` | Int-Datenschlüssel der eigenen ID eines Objekts, die seine Teile nennen. Erforderlich. |
| `parents` | Int-Datenschlüssel, die nennen, woran ein Teil hängt; der erste gesetzte gewinnt (ein Bauteil auf einem Drehgelenk nennt das Drehgelenk vor dem Fahrzeug). Mindestens einer. Erforderlich. |
| `offset` | Vec3-Datenschlüssel des Orts eines Teils im Bezugssystem dessen, woran es hängt, in Metern. Erforderlich. |
| `rotation` | Vec3-Datenschlüssel seiner Drehung in diesem Bezugssystem, als Unity-Euler-Winkel in Grad. |
| `rotationQuaternion` | Quaternion-Datenschlüssel dieser Drehung, verwendet, wenn `rotation` nicht gespeichert ist. Ohne beide wird ein Teil im Bezugssystem seines Elternobjekts nicht gedreht. |
| `storedAt` | Wo das Spiel ein Teil speichert: `root` (an der Wurzel seiner Gruppe; deine Mod baut es aus seinem Versatz, wenn die Gruppe lädt; der Standard) oder `own` (wo es steht). Der Editor speichert verschobene Teile auf dieselbe Weise. |
| `references` | Weitere int-Datenschlüssel, die die ID eines Objekts nennen (den Mast eines Segels, das Ziel eines Schalters): in einer eingefügten Kopie folgen sie den neuen IDs der Kopie und werden entfernt, wenn das, was sie nennen, nicht mitkopiert wurde. |
| `dropOnCopy` | Datenschlüssel, die eine eingefügte Kopie weglässt: IDs, die so gepackt sind, dass der Editor sie nicht umschreiben kann (ein Byte-Array), Bindungen, die nicht mitgehen dürfen (was an Bord war). |
| `placements` | Nach Elternschlüssel (einer von `parents`), wie ein daran hängendes Teil platziert wird, wenn nicht wie die Regel es sagt: seine eigenen Schlüssel `offset`, `rotation` und `rotationQuaternion` und `storedAt` (jeder nicht angegebene: der der Regel), so wie ein Segel seinen Ort am Mast getrennt von seinem Ort am Schiff behält; oder `"carried": true` (und keiner davon), ein Teil, das die Gruppe dort trägt, wo das Spiel es gespeichert hat (ein Karren an Bord): es hat keinen Versatz, wird dort gezeichnet, wo es relativ zu dem, was es trägt, gespeichert ist, und wird mit der Gruppe verschoben, gedreht, kopiert und gelöscht und dort gespeichert, wo es steht. |
| `name` | Wie eine Gruppe in den Meldungen des Editors heißt („vehicle komplett“). Standard: `group`. |

### Wie der Editor die Verknüpfungen nutzt

- **Zeichnen.** Ein Teil wird an seinem Versatz im Bezugssystem seines Elternobjekts gezeichnet, das Elternobjekt dort,
  wo sein eigenes Elternobjekt es hinsetzt, und so weiter bis zur Wurzel, die dort steht, wo sie gespeichert ist: die
  Bauteile eines Schiffs drehen und neigen sich mit dem Rumpf. Ein getragenes Teil bleibt dort, wo es relativ zu dem,
  was es trägt, gespeichert ist. Eine Erweiterung kann das Bezugssystem drehen, in dem die Teile eines Elternobjekts
  hängen, oder ein Teil selbst platzieren (`AddLinkPlacer`, siehe [Hooks](#hooks)). Ein Teil, dessen Elternobjekt (oder
  irgendein Elternobjekt weiter oben in seiner Kette) nicht in der Welt ist oder dessen Kette eine Schleife bildet oder
  tiefer als 16 ist (beschädigte Daten), bleibt dort, wo es gespeichert ist.
- **Welche Regel.** Ein Objekt nimmt die erste Regel, deren `id`- oder `parents`-Schlüssel es enthält.
  Prefab-Namen spielen keine Rolle: nur die Daten. Die Regel einer neueren Datei mit derselben `id` ersetzt die einer
  älteren, an ihrer Stelle in der Reihenfolge.
- **Verschieben und Löschen.** Ein beliebiges Teil auszuwählen und zu verschieben, zu drehen oder zu löschen, erfasst
  die ganze Gruppe. Eine verschobene Gruppe behält ihre Höhe und rastet nicht ein; ihre Teile werden gespeichert, wie
  `storedAt` es sagt.
- **Kopieren.** Eine Kopie nimmt die ganze Gruppe. Jedes Einfügen (und jede seiner Wiederholungen) bekommt neue IDs, die
  kein Objekt in der Welt hat, auch keine gelöschten, sodass das Spiel nie zwei Kopien für eine hält; jeder Eltern- und
  `references`-Wert folgt. Ein Teil, das ohne sein Elternobjekt kopiert wurde, wird als gewöhnliches Objekt eingefügt
  (seine Verknüpfungen entfernt), dort, wo es eingefügt wurde. Ein gespiegeltes Einfügen spiegelt auch die Versätze und
  Drehungen der Teile. Werte in `dropOnCopy` fallen weg, außer der Kopier-Umschreiber einer Erweiterung behandelt sie.
- Datenschlüssel werden so benannt, wie dein Code sie nennt, wie in `data`.

## Gebundene Objekte

Manche Mods binden Objekte aneinander, ohne zu speichern, wo jedes relativ zum anderen steht: ein Zug nennt die
Waggons, die er zieht, ein Deck nennt nichts, aber was darauf gebaut ist, nennt das Deck. Das Spiel speichert jedes
Objekt dort, wo es steht, und deine Mod setzt sie wieder zusammen, während sie läuft. Sag dem Editor, wie sie gebunden
sind, und er hält sie als eine Gruppe zusammen:

```json
{
  "version": 1,
  "ties": [
    { "name": "train", "root": "ExampleVehicles_Wagons", "member": "ExampleVehicles_Self", "idBytes": 12 },
    { "name": "deck", "root": "ExampleVehicles_DeckId", "member": "ExampleVehicles_OnDeck" }
  ]
}
```

| Schlüssel | |
|---|---|
| `root` | Datenschlüssel der IDs, die eine Wurzel nennt: ein int, ein long oder ein Byte-Array mit mehreren IDs hintereinander. Erforderlich. |
| `member` | Datenschlüssel der eigenen ID eines Mitglieds, derselben Art (ein int, ein long oder ein Byte-Array). Erforderlich. |
| `idBytes` | Bei einem Byte-Array, wie viele Bytes eine ID hat (1 bis 64; 12 für die Objekt-IDs des Spiels). Ohne diesen Schlüssel ist der ganze Wert eine ID. |
| `name` | Wie eine Gruppe in den Meldungen des Editors heißt. Standard: `group`. |

Ein Mitglied gehört zur Gruppe einer Wurzel, wenn seine eigene ID eine ist, die die Wurzel nennt. Die IDs werden als
die Bytes verglichen, die sie sind, egal was sie für deine Mod bedeuten: der Editor schlägt sie nie im Spiel nach.
Gruppen, die sich ein Objekt teilen, sind eine Gruppe: ein Zug, dessen Flachwagen die Wurzel eines Decks ist, nimmt
mit, was auf dem Deck gebaut ist.

- **Auswählen, Verschieben und Löschen.** Ein beliebiges Objekt einer Gruppe auszuwählen und zu verschieben oder zu
  drehen, erfasst die ganze Gruppe als einen Block: jedes Mitglied behält, wo es relativ zu den anderen steht, und wird
  dort gespeichert, wo es steht. Die Wurzel zu löschen, nimmt ihre Mitglieder mit; ein Mitglied zu löschen, nimmt nur
  dieses.
- **Kopieren.** Eine Kopie nimmt die ganze Gruppe. Jedes Einfügen (und jede seiner Wiederholungen) bekommt neue,
  zufällige IDs derselben Art und Größe, in der Wurzel und in den Mitgliedern, die sie nennt, sodass eine Kopie nie die
  Mitglieder des Originals übernimmt: eine Liste behält die IDs der mitkopierten Mitglieder, eine einzelne ID ist immer
  neu, und ein Mitglied, dessen Wurzel nicht mitkopiert wurde, verliert seine ID.
- **Live und gespeichert.** Die neuen IDs aufzulösen ist Sache deiner Mod: sie findet die Mitglieder einer eingefügten
  Wurzel über die IDs, die sie enthalten, so wie nach dem Laden einer Welt durch das Spiel (das Spiel gibt jedem Objekt
  bei jedem Laden eine neue Objekt-ID).
- Eine Regel mit denselben Schlüsseln `root` und `member` in einer neueren Datei ersetzt die einer älteren, an ihrer
  Stelle in der Reihenfolge.

## Erweiterungen

Was eine Datei nicht sagen kann, kann Code. Eine Mod kann eine **Editor-Erweiterung** mitliefern: eine kleine DLL,
gegen das SDK des Editors gebaut, die der Editor lädt, um die Objekte der Mod zu lesen und zu zeichnen. Sie läuft nicht
im Spiel und sieht nie den Code des Spiels oder von Unity: nur die Daten der Objekte, wie der Editor sie hat. Sie kann:

- einen Byte-Array-Wert im Inspektor in Worten zeigen;
- IDs, die deine Mod in ihrer eigenen Form packt, in eingefügten Kopien umschreiben;
- setzen, was deine Mod daraus ableitet, wo ein Objekt steht, wann immer der Editor es platziert, einfügt, verschiebt
  oder dreht (das Gleis unter einem Fahrzeug, eine Drehung, die deine Mod speichert);
- zeichnen, was deine Mod im Code baut, während das Spiel läuft (ein Segel aus seinen Ecken, ein Kabel von Pfosten zu
  Pfosten);
- die Teile deiner verknüpften Objekte dort platzieren, wo deine Mod sie baut, wenn ihre Daten allein es nicht sagen
  (die Teile auf einem Drehgelenk, das deine Mod gedreht hat);
- den Boden dort ungezeichnet lassen, wo deine Mod gräbt (die Mündung eines Tunnels, eine Grube);
- zeichnen, was deine Mod aus einem Bereich als Ganzem macht (ein Kanal, aus einem Hügel gegrabener Fels), mit der
  Bodenoptik des Spiels, anstelle des Bodens des Editors;
- die Einstellungen der eigenen Skripte des Spiels lesen (seine `gameComponents`), wie das Valheim des Spielers sie hat;
- die eigenen Einstellungen deiner Mod lesen (ihre `settingsFiles`), wie das Spiel der Welt sie hat;
- dem 3D-Editor ein eigenes Werkzeug in der Werkzeugleiste hinzufügen, mit seiner Einstellungskarte, seiner Vorschau
  und seinen Änderungen im Verlauf (einen Kanal graben, wo der Spieler klickt);
- für eine Weltgenerierungs-Mod die Welten lesen und zeichnen, die sie erzeugt, und mit ihr neue erstellen;
- im Live-Modus das eigene Plugin deiner Mod im Spiel nach dem fragen, was nur das Spiel weiß (siehe
  [Live-Modus](#live-modus)).

### Eine erstellen

1. **Hol dir das SDK** aus einem [Release](https://github.com/Shuiei/valheim-world-editor/releases):
   `ValheimWorldEditor.Sdk-<version>.zip` enthält `ValheimWorldEditor.Sdk.dll` (netstandard2.0, wogegen eine
   Erweiterung gebaut wird; sein net10.0-Build in `net10.0/` enthält auch das Weltregel-Modell, für eine Erweiterung auf
   dem .NET des Editors, die Weltregeln erstellt) und `WorldEditorBridge.Api.dll` (wogegen das Plugin einer Mod für den
   [Live-Modus](#live-modus) gebaut wird), jeweils mit ihrer Dokumentationsdatei für Code-Editoren, diese Seite als
   `README.md` und das ganze Beispiel Example Vehicles in `sdk/samples/` (es baut, wie es ist).
2. **Baue eine netstandard2.0-Klassenbibliothek** (net10.0, gegen `net10.0/ValheimWorldEditor.Sdk.dll`, für eine, die
   Weltregeln erstellt), die `ValheimWorldEditor.Sdk.dll` referenziert, ohne sie in deine Ausgabe zu kopieren
   (`Private="false"`): der Editor stellt seine eigene. Nutze nichts außer dem Framework (das Projekt des Beispiels
   referenziert außerdem das Paket `System.Numerics.Vectors`, für `Vector3` und `Quaternion`). Eine Erweiterung ist
   **eine DLL**: der Editor lädt keine andere Datei aus deinem Ordner, eine Hilfsbibliothek daneben wird also nie
   gefunden.
3. **Markiere die Assembly** und gib ihr einen Einstiegspunkt: eine öffentliche Klasse mit einem parameterlosen
   Konstruktor, die `IWorldEditorExtension` implementiert. Der Editor erzeugt sie einmal und ruft `Register` einmal auf,
   bevor irgendein Hook verwendet wird. Das ist die des Beispiels:

   ```csharp
   [assembly: WorldEditorExtension(typeof(ExampleVehicles.WorldEditor.Extension))]

   public sealed class Extension : IWorldEditorExtension
   {
       public void Register(IExtensionRegistry registry)
       {
           var packed = new PackedIds();
           registry.AddDecoder(Keys.Packed, packed);           // inspector
           registry.AddCopyRewriter(packed, Keys.Packed);      // paste
           registry.AddShapes(new Sails(), Keys.Sail);         // 3D view
           registry.AddShapes(new Cables(), Keys.Post);
           registry.AddGroundCuts(new Pits(), Keys.Pit);       // the ground
           registry.AddScene(new Canals());                    // the area as a whole
           registry.AddTool(new CanalTool());                  // a tool on the rail
           registry.AddAreaCopier("canals", new CanalCopies()); // copy and paste
           registry.AddLiveListener(new LiveLink(registry));   // live mode
           registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
       }
   }
   ```

4. **Liefere sie** mit deiner Mod aus, in ihrem Ordner, und nenne sie in deiner `worldeditor.json`:
   `"extension": "ExampleVehicles.WorldEditor.dll"`. Ein Pfad, der den Ordner der Datei verlässt, oder eine Datei, die
   nicht auf `.dll` endet, wird nicht angeboten.

### Einschalten: Einstellungen → Mod-Erweiterungen

Eine Erweiterung ist Code, der im Editor mit den Rechten des Spielers läuft, also **entscheidet der Spieler**. Der
Editor findet die Erweiterungen, die die Mods in seinen Plugin-Ordnern anbieten, und listet sie in den
**Einstellungen** unter **Mod-Erweiterungen**: ein Schalter pro Erweiterung, benannt nach dem Ordner der Mod und der
Datei („ExampleVehicles · ExampleVehicles.WorldEditor.dll“). Wer darauf zeigt, sieht den Pfad der Datei und ihren
SHA-256. Unter jedem Schalter sagt der Editor, was daraus geworden ist: „Läuft.“, „Läuft nicht: …“ mit dem Grund oder
„Aus: the file cannot be read.“. Bietet keine Mod eine an: „Keine Mod in den Ordnern oben bietet eine an.“

- **Standardmäßig aus.** Eine Erweiterung läuft erst, wenn der Spieler sie einschaltet und **Speichern** drückt; ab da
  läuft sie, und ausgeschaltete stoppen.
- **Nur diese Datei.** Der Editor merkt sich den SHA-256 der Datei. Eine geänderte DLL (ein Mod-Update) ist anderer
  Code: sie ist aus, bis der Spieler sie wieder einschaltet. Die geprüften Bytes sind die geladenen Bytes.
- **Warum sie nicht läuft.** „it is not marked as an extension ([assembly: WorldEditorExtension])“,
  „it needs a newer editor (API …; this one has …)“, „its entry … is not an IWorldEditorExtension“
  oder die Exception, die `Register` geworfen hat.

### Hooks

| Hook | Was der Editor damit macht |
|---|---|
| `AddDecoder(key, IDataDecoder)` | Der Inspektor zeigt einen Byte-Array-Wert in deinen Worten, unter dem Wert (statt nur seiner Größe). `Describe` gibt ein paar kurze Zeilen zurück oder `null`, wenn die Bytes nicht in einer Form sind, die es liest. Lesen zwei Erweiterungen denselben Schlüssel, gewinnt die erste, die Text liefert. |
| `AddCopyRewriter(ICopyRewriter, keys)` | Jedes Objekt einer eingefügten Kopie, das einen der Schlüssel enthält, wird dir übergeben (`ICopy`), nach der eigenen Verknüpfung des Editors (`links`), mit den neuen IDs der Kopie für die der Originale (`IIdMap.TryGetNew`; false, wenn das Objekt mit der alten ID nicht mitkopiert wurde). Ändere seine Werte direkt (`SetInt`, `SetQuaternion`, `SetBytes`…; `null` entfernt einen Wert). Die Schlüssel gehören dir: `dropOnCopy` nimmt sie nicht mehr heraus. |
| `AddAreaCopier(id, IAreaCopier)` | Was deine Mod von einem Ort festhält, das kein kopiertes Objekt trägt (eine Aufzeichnung von abgetragenem Fels, in Datenhaltern, die der Editor nie kopiert), geht mit den Kopien des Editors mit. Kopiert der Spieler (die Auswahl des Werkzeugs Region oder die Objekte des Werkzeugs Auswahl), bekommt `Copy(area, region)` den Bereich und was kopiert wird (`CopyRegion`: sein `Outline` und `Contains(x, z)`, Weltmeter, und seine `Middle`) und gibt eigene Bytes zurück (oder `null`): speichere Orte als Versatz von der Mitte, da die Kopie überall eingefügt werden kann, gedreht und gespiegelt, auch in einer anderen Welt. Die Bytes gehen mit der Kopie und in Bauplandateien, unter dem Namen deiner Mod und der `id` (Buchstaben, Ziffern, Punkte, Bindestriche und Unterstriche). Beim Einfügen wird `Paste(area, bytes, copies)` einmal mit jeder Kopie aufgerufen, die das Einfügen setzt (`CopyPlacement`: ihre `Middle`, `Turn` und `Mirror`; `Place(offset)` macht aus einem Versatz von der alten Mitte den Weltpunkt, an dem er landet, `Yaw(yaw)` aus einer Drehung), und mit dem Bereich, wie das Einfügen seinen Boden hinterlässt; die `ToolEdit`, die du zurückgibst, ist Teil des Rückgängig-Schritts des Einfügens. Bytes, deren Kopierer nicht läuft (deine Erweiterung nicht installiert), werden beim Einfügen weggelassen. |
| `AddPlacer(IObjectPlacer, prefabs)` | Objekte der Prefabs, die der Editor an einen eigenen Ort setzt (platziert, eingefügt, verschoben oder gedreht, durch ein beliebiges Werkzeug, ein Skript oder Claude), werden dir einmal pro Schritt alle zusammen übergeben (`ICopy`, jedes an seinem neuen Ort, seine Werte, wie sie erzeugt werden, bei einer eingefügten Kopie nach den Kopier-Umschreibern), mit dem Bereich, wie er vor dem Schritt steht, ohne die Objekte, die der Schritt bewegt: setze, was deine Mod daraus ableitet, wo ein Objekt steht (eine Drehung, die sie getrennt von der des Objekts speichert, das Gleis, auf dem ein Fahrzeug steht, die Haltestelle, die ein Schild nennt), so wie deine Mod es täte, wenn ein Spieler es baut. Was du setzt, wird mit dem Schritt gespeichert und mit ihm live angewendet. `SetTurn` dreht das Objekt, wie es erzeugt wird, für eine Mod, die ihre Objekte ungedreht und ihre Form in ihren Werten speichert (drehe die Werte, dann das Objekt zurück; ab Editor 1.119.0). Objekte, die dort geändert werden, wo sie stehen (der Inspektor, die Änderung eines Werkzeugs), sind kein Platzieren. |
| `AddShapes(IShapeProvider, prefabs)` | Objekte der Prefabs werden auch mit deinen Formen gezeichnet: `Shape`s, Dreiecke im Bezugssystem des Objekts (Meter; x rechts, y oben, z vorne; Ort, Drehung und Größe des Objekts angewendet) oder in dem der Welt (`WorldSpace = true`), je eine Farbe (Rot, Grün, Blau, Alpha von 0 bis 1; Alpha unter 1: durchscheinend), von vorne gesehen im Uhrzeigersinn gewunden wie in Unity, oder von beiden Seiten sichtbar (`BothSides = true`, für ein Segel oder ein Tuch). Sie werden mit der Art des Objekts gezeichnet (ausgeblendet, wenn das Panel Ansicht sie ausblendet). Ein Objekt, von dem der Editor kein Modell hat, wird nur mit deinen Formen gezeichnet (kein Ersatzkasten) und über den Kasten der Formen ausgewählt und umrandet, die du für eines seiner Prefabs ohne Daten lieferst (die in seinem eigenen Bezugssystem). Eine Form mit einem Dreieck, das eine fehlende Ecke nennt, oder einer Ecke, die keine Zahl ist, wird nicht gezeichnet. |
| `AddLinkPlacer(ILinkPlacer, prefabs)` | Verknüpfte Teile (die `links` deines Manifests) werden dort platziert, wo deine Mod sie baut, wenn ihre Daten allein es nicht sagen. `FrameOf(obj)`, für ein Objekt der Prefabs, das Teile hat: das Bezugssystem, in dem sie hängen, in seinem eigenen Bezugssystem (`LinkFrame`: ein Versatz und eine Drehung; die Plattform eines Drehgelenks, wie deine Mod sie gedreht oder verschoben hat), oder `null` für sein eigenes Bezugssystem. `PlaceOf(part, parent)`, für ein Teil der Prefabs: wo es im Bezugssystem dessen steht, woran es hängt, anstelle seines Versatzes und seiner Drehung (das Teil am Ort und mit der Drehung, mit denen das Spiel es gespeichert hat, sein Elternobjekt wie gezeichnet; das Bezugssystem des Elternobjekts wird dann nicht angewendet), oder `null` für den Ort seiner Verknüpfungen. Verschieben, Kopieren und Löschen folgen weiterhin den Verknüpfungen. |
| `AddGroundCuts(IGroundCutter, prefabs)` | Der Boden wird innerhalb der Umrisse (`GroundCut`), die du für Objekte der Prefabs lieferst, nicht gezeichnet: Punkte auf dem Boden (x, z) im Bezugssystem des Objekts oder in dem der Welt (`WorldSpace = true`); mindestens drei, in beliebiger Umlaufrichtung (ein sich selbst kreuzender Umriss schneidet nach der Gerade-Ungerade-Regel). Ein Klick dort geht durch das Loch. Der Boden des Editors ist eine Höhenkarte mit einem Punkt pro Meter: der Schnitt lässt den Boden zwischen den Punkten innerhalb des Umrisses weg, sein Rand folgt also diesem 1-m-Raster, und durchscheint, was du darunter zeichnest (Formen), und die Unterseite des Bodens. |
| `AddScene(ISceneProvider)` | Was deine Mod aus einem Bereich als Ganzem macht, darin gezeichnet: `Build(area)` wird auf einem Worker jedes Mal aufgerufen, wenn sich ein Bereich öffnet, und erneut, wann immer sich seine Objekte, sein Boden oder das Spiel ändern (oder du `Host.Redraw()` aufrufst), und gibt einen `SceneLayer` zurück (oder `null`). Seine `Meshes` (`SceneMesh`: Ecken und ihre Normalen im Bezugssystem der Welt, Meter, Dreiecke von vorne gesehen im Uhrzeigersinn gewunden wie in Unity) werden mit der Bodenoptik des Spiels gezeichnet, wenn sie `BiomeColors` haben (die Geländefarbe jeder Ecke, wie `area.BiomeColor` sie liefert), und aus ihrer `Mask` (eine kleine Textur der Malmaske des Geländes, `MaskTexture`) an `MaskUvs` bemalt; ohne Biomfarben in `Color`. Klicks landen auf ihnen (außer bei `Pickable = false`). Seine `Holes` (`GroundHole`: Rechtecke in ganzen Metern) lassen den Boden des Editors ungezeichnet, Zelle für Zelle, genau bis zu ihren Rändern: zeichne, was ihn ersetzt. Die `area` (`IAreaView`) liefert ihre Grenzen, ihre Objekte eines Prefabs, wie sie mit den noch nicht gespeicherten Änderungen sind (`ObjectsOf`, deine Datenhalter eingeschlossen), die Höhe des Bodens, wie das Spiel sie zeigt (`GroundHeight`, zwischen den Punkten im Meterabstand entlang der eigenen Dreiecke des Spiels), das Biom, ob ein Punkt im Tiefen Norden oder in den Aschlanden liegt, und Farbe und Bemalung des Geländes an einem ganzen Meter (`BiomeColor`, `GroundMask`). Leite alles jedes Mal aus dem Bereich ab; behalte nichts zwischen den Aufrufen. |
| `AddTool(IEditorTool)` | Ein eigenes Werkzeug in der Werkzeugleiste des 3D-Editors, unter **MODS**, mit seinem `Info` (`ToolInfo`: eine ID, eindeutig in deiner Erweiterung; sein Name in der Werkzeugleiste; eine Hilfezeile in seinem Tooltip; `Icon`, ein SVG-Pfad, 16 × 16 gezeichnet, sonst der erste Buchstabe deines Namens). Gewählt, zeigt es eine Karte seiner `Controls`: `SliderControl` (min, max, Wert, `Step`, `Unit`), `ChoiceControl` (eine Liste), `ToggleControl`, `ButtonControl` und `TextControl` (eine Zeile, die du mit `SetText` änderst); die Werte des Spielers bleiben von einer Sitzung zur nächsten erhalten. Der Editor ruft `OnChosen` und `OnLeft` auf, wenn es gewählt und verlassen wird, `OnControl(id)`, wenn sich ein Wert ändert oder eine Schaltfläche gedrückt wird, `OnPointer` bei jeder Bewegung, jedem Drücken, Ziehen und Loslassen über der 3D-Ansicht (`PointerInput`: der Strahl, die gehaltenen Tasten und worauf er zeigt: den Boden, deine Szenenebene oder ein Ding, mit dem Punkt und der Normalen des Bodens dort), und `OnKey` bei jeder gedrückten Taste (der Name der Taste, wie „Enter“, „Escape“, „Back“ oder „A“; gib true zurück, wenn du sie genutzt hast, false überlässt sie dem Editor, sodass Ctrl+Z weiterhin rückgängig macht). Der `IToolContext`, der jedem Aufruf übergeben wird, liefert den Bereich (`Area`, wie bei Szenenebenen), die Werte deiner Elemente (`Number`, `Choice`, `IsOn`), Formen, die über den Bereich gezeichnet werden, bis du sie änderst (`Preview`, im Bezugssystem der Welt), eine Zeile in der Statusleiste (`Status`) und `Commit(label, ToolEdit)`: hinzugefügte Objekte (ein Prefab, ein Ort, eine Drehung und seine Daten, gesetzt über ein `ICopy`, das `SetTurn` drehen kann), geänderte (eine Kopie eines Objekts, das der Bereich dir gegeben hat, mit deinen Änderungen) und entfernte, in einem Schritt des Verlaufs des Bereichs, rückgängig gemacht und gespeichert wie jede andere Änderung. Deine Datenhalter werden im Verlauf aufbewahrt wie andere Objekte, obwohl sie nie gezeichnet werden; ein Objekt, das du nicht vom Bereich bekommen hast, bleibt unberührt. |
| `AddLiveListener(ILiveListener)` | Wird benachrichtigt, wenn sich der Editor mit einem Spiel verbindet (`OnConnected`, mit dem Spiel) und wenn das endet (`OnDisconnected`: die Welt wurde geschlossen oder eine andere geöffnet). Siehe [Live-Modus](#live-modus). |
| `AddWorldGenerator(IWorldGenerator)` | Die Welten deiner Weltgenerierungs-Mod (siehe [Weltgeneratoren](#weltgeneratoren)). |
| `AddWorldRules(IWorldRulesProvider)` | Die Regeln, mit denen Welten erzeugt werden, aus den Einstellungen und Datendateien deiner Weltgenerierungs-Mod (siehe [Weltregeln](#weltregeln)). |
| `Host` | Der Editor, außerhalb der Hooks: `Host.Live`, das verbundene Spiel (null, wenn es keines gibt: eine gespeicherte Welt, die Werkstatt); `Host.Game`, die Komponenten des Spiels, nach denen die `gameComponents` deines Manifests fragen (`Components("Ship")`: jede mit ihrem Prefab, dem Objekt, auf dem sie sitzt, und ihren Feldern nach Namen, wie das Spiel sie speichert; bereit, sobald eine Welt oder die Werkstatt offen ist, also frage aus deinen Hooks, nicht aus `Register`); `Host.Setting(file, section, key)`, eine Einstellung aus einer Datei, die die `settingsFiles` deines Manifests nennen, als der Text, den die Datei enthält (null, wenn die Datei oder die Einstellung nicht da ist: nutze den Standardwert deiner Mod; bereit, wie `Host.Game`, sobald eine Welt oder die Werkstatt offen ist); `Host.BepInExFolders`, die BepInEx-Ordner des Spielers, die der Editor kennt, in der Reihenfolge, in der er sie durchsieht (jeder mit seinem `Path` und wo er liegt, für den Spieler); `Host.DataFolder`, der Datenordner des Editors (bewahre deine eigenen Dateien in einem eigenen Ordner dort auf); `Host.World` (net10.0-Erweiterungen), die eigenen Weltregeln und Objekte des Spiels (siehe [Weltregeln](#weltregeln)); und `Host.Redraw()`, damit deine Formen und Schnitte beim nächsten Frame neu erzeugt werden (nachdem das Spiel dir etwas Neues gesagt hat). |
| `Log(message)` | Schreibt eine Zeile, nach deiner Mod benannt („extension ExampleVehicles: …“), in das Protokoll des Editors, `ValheimWorldEditor.log` in seinem Datenordner (**Protokoll öffnen** auf der Startseite; bei jedem Start neu begonnen). |

Form- und Schnitt-Hooks bekommen auch die Welt um das Objekt herum (`IWorldView`): `TryFind(idKey, id, out
obj)` findet ein anderes Objekt über eine ID, die es enthält, für ein Seil oder ein Kabel, das es erreicht. Nur IDs
unter einem Schlüssel, den eine `links`-Regel als ihre `id` nennt, werden gefunden (der Editor indiziert diese). Im
3D-Editor ist diese Welt auch eine `IGroundView` (`world is IGroundView ground`): `TryGroundHeight(x, z, out height)`
liefert die Höhe des Bodens dort, wie der Editor ihn zeichnet, für eine Form, die bis zu ihm hinunterreicht (eine
Strickleiter), false außerhalb des Bereichs.

Objekte kommen als `IObjectView`: der Name des Prefabs ("" wenn der Editor ihn nicht kennt), Ort, Drehung
(Unity-Euler-Winkel in Grad, und `Orientation`, ein Quaternion) und Größe wie gezeichnet (bei einem verknüpften Teil
dort, wo deine Mod es baut, nicht wo das Spiel es speichert), und jeder Wert seiner Daten nach Schlüssel:
`TryGetInt`, `TryGetLong`, `TryGetFloat`, `TryGetVector3`, `TryGetQuaternion`, `TryGetString`,
`TryGetBytes`. Schlüssel und Prefabs werden so benannt, wie dein Code sie nennt; der Editor hasht sie, wie das Spiel es
tut.

### Weltgeneratoren

Eine Weltgenerierungs-Mod ersetzt den Boden des Spiels und markiert die Welten, die sie erzeugt: ihre Weltdatei beginnt
mit -1 dort, wo die Dateiversion des Spiels steht, dann die ID des Generators (eine Zeichenkette), dann was immer die Mod
speichert (Riverheim vor 1.3: die Dateiversion des Spiels, dann seine Vorgabe). Der Editor weist eine solche Welt ab,
außer eine laufende Erweiterung registriert einen Generator mit dieser ID, so wie das Spiel ohne die Mod sie abweist:
gelistet, ausgegraut, mit dem Grund (nenne die IDs in den `worldGenerators` deines Manifests, damit der Editor sagen
kann, was fehlt).

Eine Mod, die sich in [Worldhalla](https://thunderstore.io/c/valheim/p/Gurebu/Worldhalla/) einklinkt (Riverheim seit 1.3),
lässt ihre Welten stattdessen von Worldhalla markieren, mit der ID und den Einstellungen des Generators in Worldhallas
eigenem Header. Registriere für sie einen Generator mit der ID `worldhalla:`, gefolgt von der ID deines Generators in
Worldhalla (die von Riverheim: `worldhalla:dev.gurebu.riverheim`): sein `ReadHeader` bekommt die Dateiversion des
Spiels, dann deine Einstellungen, wie Worldhalla sie speichert, und muss sie ganz lesen. Eine Welt, deren
Worldhalla-Header von einem neueren Worldhalla als 1.0 stammt (sein Format oder eigene Einstellungen von Worldhalla),
wird mit Begründung abgewiesen. Dein `IWorldGenerator` liefert:

- `Id`, `PluginId` (die BepInEx-GUID der Mod: im Live-Modus erfährst du die Version, die das Spiel ausführt) und `Name`;
- `ReadHeader(file, out fileVersion)` und `WriteHeader(file, fileVersion, settings)`: was auf die ID folgt, gelesen und
  geschrieben in den Formen des Spiels (`IWorldFileReader`, `IWorldFileWriter`), deine Einstellungen als
  `GeneratorSettings` aufbewahrt (eine Beschreibung für den Spieler und ein eigener Wert). Wirf
  `InvalidDataException` mit einem Grund für den Spieler bei einem Header, den du nicht lesen kannst;
- `Problem(settings)`: warum eine solche Welt nicht gezeichnet werden kann, schnell (für jede Welt abgefragt, die die
  Startseite listet), oder null;
- `Open(settings, gamePluginVersion)`: bereit, Welten anzulegen, wenn sich eine Welt öffnet (`IWorldLayout`: deine
  `Version`, ob das Anlegen `Slow` ist, und `LayOut(seed)`); wirf `InvalidOperationException` mit dem Grund, wenn du
  nicht kannst;
- `LayOut(seed)` liefert ein `IGroundLayout`: das Biom (der Wert des Spiels), die Höhe mit ihrer Maske und den
  Waldfaktor an einem Punkt, anstelle der Methodenrümpfe von GetBiome, GetBiomeHeight, GetAshlandsHeight und GetForestFactor
  des Spiels (aus vielen Threads abgefragt). Die Streckung von Expand World kommt weiterhin vor deinen Antworten, seine
  Tiefen- und Waldmultiplikatoren danach, so wie die Patches der Mods im Spiel aufeinandertreffen; der Editor legt kein
  Wasser des Spiels an;
- `NewWorldSettings()`: die Einstellungen, die eine neue Welt bekommt (die Seite Neue Welt bietet deinen Generator an,
  als erstes gewählt), oder null;
- `Notes(settings, expandWorld)`: was die Einstellungen von Expand World (`ExpandWorldSettings`) sonst mit deinen
  Welten machen, für das Protokoll.

Alles andere, was dein Generator wirft, ist ein Fehler: seine Erweiterung wird ausgeschaltet, und dem Spieler wird
gesagt, dass das Add-on fehlgeschlagen ist.

### Weltregeln

Eine Weltgenerierungs-Mod, die die eigene Generierung des Spiels über Einstellungen und Datendateien ändert (eine
größere Welt, eigene Biome, Orte und Bewuchs, wie Expand World es tut), legt die Welt nicht selbst an: der Generator des
Editors folgt den Regeln, die er bekommt. Baue deine Erweiterung für net10.0 (der `net10.0`-Build des SDK enthält das
Regelmodell, `WorldRules` und die Typen, die es enthält, im Namespace `ValheimGen`) und registriere einen
`IWorldRulesProvider`:

- `ForWorld(world)`: die Regeln, mit denen sich eine Welt öffnet. Der `WorldRulesRequest` liefert ihren `Name`, die
  `StartingGlobalKeys`, die ihre Weltdatei speichert (dort abgelegte Einstellungen einer Mod), die `LoadedGlobalKeys`,
  die ihr Speicherstand enthält, die `GridWidth`, auf der ihre Chunk-Dateien liegen, und das Spiel, in dem sie läuft,
  wenn sie live ist (`Live`, sonst null): lies die Dateien der Mod dann aus dem Spiel, über `Live.Plugins()`,
  `Live.ConfigFile(path)` und `Live.ConfigFolder(path)` (unter seinem BepInEx/config; null bei einer WorldEditorBridge
  älter als 1.54.0), sonst aus `Host.BepInExFolders`;
- `ForNewWorld()`: die Regeln, mit denen eine neue Welt erstellt und angesehen wird (die Seite Neue Welt).

Gib null zurück (oder `WorldRules.Vanilla`), wenn deine Mod die Welt nicht ändert (nicht installiert oder auf ihren
Standardwerten): dann wird der nächste Anbieter gefragt, und ohne einen ist die Welt die des Spiels. Orts-, Bewuchs- und
Raumregeln werden gegen die des Spiels erstellt (`Host.World`: seine `Locations`, sobald `ReadLocations()` sie gelesen
hat, seine `Vegetation`, `HasPrefab(name)` und `IsGameRoom(name)`). `WorldRulesText.Describe(rules)` sagt in ein paar
Worten, was Regeln ändern, für deine Protokollzeilen. Ein Anbieter, der wirft, ist ein Fehler: seine Erweiterung wird
ausgeschaltet, und der nächste wird gefragt.

### Regeln

- **Sei schnell, behalte keinen Zustand.** Hooks werden aus mehreren Threads gleichzeitig und oft aufgerufen (bei jedem
  Neuzeichnen der Formen und Schnitte, bei jedem eingefügten Objekt): behalte keinen Zustand zwischen den Aufrufen (oder
  sperre ihn) und kehre schnell zurück.
- **Wirf nie.** Ein Hook, der wirft, schaltet die Erweiterung aus, bis der Editor neu startet (die Einstellungen sagen
  warum, ebenso das Protokoll); der Editor macht ohne sie weiter. In Code, der weiterläuft, nachdem ein Hook
  zurückgekehrt ist (ein `async`-Aufruf an das Spiel), fang alles selbst ab, wie es der `LiveLink` des Beispiels tut.
- **Versionen.** `SdkInfo.ApiVersion` ist die Version der API (derzeit 1), und das Attribut hält die fest, gegen die du
  gebaut hast. Ein Editor führt Erweiterungen aus, die gegen seine Version oder eine frühere gebaut sind. Neue Hooks
  kommen als Ergänzungen; die Version ändert sich nur, wenn die API bricht.

## Add-ons

Eine Erweiterung wird mit ihrer Mod ausgeliefert. Hat der Autor einer Mod keine gemacht (oder wird die Mod nicht mehr
gepflegt), kann es jeder: ein **Add-on** ist eine `worldeditor.json` und eine Erweiterung für die **Mod eines anderen**,
eigenständig veröffentlicht, die der Spieler neben dem Editor installiert. Die Mod selbst bleibt, wie sie ist, in
`BepInEx/plugins`; das Add-on bringt dem Editor nur etwas über sie bei.

```
ValheimWorldEditor/                der Ordner des Editors (unter macOS: der Ordner, der ValheimWorldEditor.app enthält)
  ValheimWorldEditor(.exe)
  addons/
    Tunnels/                       ein Ordner pro Add-on, benannt, wie du willst
      worldeditor.json
      Tunnels.WorldEditor.dll
```

- **Seine Datei** ist eine `worldeditor.json` wie oben (Datenbeschriftungen, Prefabs, Verknüpfungen, `extension`,
  `gameComponents`), mit einem Schlüssel mehr: `"serves": "TheirMod.dll"`, der Dateiname der DLL der Mod. Das Add-on
  wird genutzt, solange eine DLL dieses Namens in einem der Plugin-Ordner liegt, die der Editor kennt (Einstellungen,
  BepInEx-Ordner), irgendwo unter `plugins`, und sonst in Ruhe gelassen. Ohne `serves` wird es immer genutzt (ein
  eigenes Werkzeug, für keine bestimmte Mod). Ein `serves`, das kein einfacher Dateiname mit der Endung `.dll` ist, oder
  eine Datei einer Version, die dieser Editor nicht liest, lässt das Add-on aus (das Protokoll sagt warum).
- **Seine Erweiterung** wird wie jede andere erstellt (siehe [Eine erstellen](#eine-erstellen)): eine DLL, im Ordner des
  Add-ons, jeder Hook und jede Regel gleich.
- **An, sobald installiert.** Ein Add-on zu installieren ist die Entscheidung des Spielers, also läuft es, ohne in den
  Einstellungen eingeschaltet zu werden. Die Einstellungen listen die Add-ons unter **Add-ons**, jedes mit einem
  Schalter, der es ausschaltet, ohne es zu entfernen (dann wird nichts davon genutzt), und mit dem, was aus jedem
  geworden ist: „Wartet auf seine Mod: TheirMod.dll ist nicht in den Plugin-Ordnern oben.“, „Läuft.“, „Läuft nicht: …“,
  dass es nur Hinweise mitbringt oder dass es aus ist.
- **Das Wort der Mod selbst gewinnt.** Die Dateien der Add-ons werden vor denen der Mods gelesen: liefert die Mod eines
  Tages eine eigene `worldeditor.json`, ersetzt das, was sie sagt, das, was das Add-on gesagt hat (eine eigene
  Erweiterung braucht dann den Schalter des Spielers, wie immer).
- **Veröffentliche es als `.zip`, das seinen Ordner enthält**, damit Spieler es nach `addons` entpacken. Die Pakete des
  Editors kommen mit diesem Ordner, leer bis auf eine `README.txt`.
- **Das eigene Repository des Editors enthält ein paar Add-ons** (`addons/`, jedes als eigenes Zip neben den Paketen des
  Editors veröffentlicht: siehe [development.md](../development.md#add-ons-in-this-repository)); deines kann überall
  liegen.

## Live-Modus

Mit einem laufenden Spiel verbunden (Live-Modus), wendet der Editor seine Änderungen über das Plugin WorldEditorBridge
an, das Objekte im Spiel zerstört, erzeugt und verschiebt. Eine Mod, die aus ihren Objekten Dinge baut, während das
Spiel läuft (ein Fahrzeug aus seinen Teilen, ein Tunnel aus dem, was gegraben wurde), sieht die Änderungen des Editors
erst beim nächsten Laden, außer ihr eigenes Plugin nimmt teil. Die Bridge bietet den Plugins der Mods zweierlei, über
`WorldEditorBridge.Api.dll`: sie wird mit der Bridge ausgeliefert, in ihrem `plugins`-Ordner, und im Zip des SDK, und
enthält keinen Typ des Spiels oder von Unity, sodass ein Spielupdate sie nicht kaputtmachen kann.

### Die Seite deines Plugins

Registriere dich aus dem `Awake` deines Plugins, mit einer weichen Abhängigkeit von der Bridge, und halte den Code, der
die API berührt, in einer eigenen Klasse: dann lädt dein Plugin auch ohne die Bridge. Aus dem
`sdk/samples/ExampleVehicles.Bridge` des Beispiels:

```csharp
[BepInPlugin(PluginId, "ExampleVehicles", "1.0.0")]
[BepInDependency(BridgeApi.BridgeGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleVehiclesBridgePlugin : BaseUnityPlugin
{
    public const string PluginId = "ExampleVehicles";

    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Register(Logger);   // a class of its own: this plugin loads without the bridge
        }
    }

    private void OnDestroy()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Unregister();       // BridgeApi.Remove(PluginId)
        }
    }
}

internal static class BridgeLink
{
    public static void Register(BepInEx.Logging.ManualLogSource log)
    {
        BridgeApi.AddRoutes(ExampleVehiclesBridgePlugin.PluginId, new Routes());          // answers the editor's calls
        BridgeApi.AddListener(ExampleVehiclesBridgePlugin.PluginId, new Listener(log));   // told what the editor applied
    }

    public static void Unregister() => BridgeApi.Remove(ExampleVehiclesBridgePlugin.PluginId);
    // Routes and Listener: see the sample.
}
```

| Hook | Was die Bridge damit macht |
|---|---|
| `AddListener(modId, IBridgeListener)` | `OnApplied(AppliedChanges)` wird auf dem Hauptthread des Spiels direkt nach jedem Anwenden des Editors aufgerufen, mit dem, was es geändert hat: `Created` und `Destroyed`, die erzeugten und zerstörten Objekte (jeweils `User` und `Id` ihrer ZDOID, der Hash ihres Prefabs und ihre Position); `Moved`, die unter ihrer eigenen ZDOID verschobenen Objekte (Bridge 1.40.0 oder neuer: `From`, wo jedes stand, `To`, wo es jetzt steht; ein Objekt, dessen alten und neuen Ort ein Spieler zugleich geladen hat, wird stattdessen neu erzeugt und steht in `Created` und `Destroyed`; ab 1.97.0 steht auch ein Objekt hier, dessen Daten der Editor geändert hat, mit seinen neuen Daten schon darauf); `TerrainZones`, die Zonen, deren Boden neu geschrieben wurde; `ResetZones`, die Zonen, die geleert wurden, um neu erzeugt zu werden. Setze deine Dinge hier wieder zusammen. Eine Exception wird im Protokoll des Spiels vermerkt und ändert nichts an dem, was der Editor getan hat. |
| `AddRoutes(modId, IBridgeRoutes)` | `Handle(name, body)` beantwortet `POST /mods/<modId>/<name>` auf dem Hauptthread des Spiels, in welchen Bytes auch immer du und deine Editor-Erweiterung euch einigt; `null` antwortet 404 („the mod has no route of that name“), eine Exception 500 mit ihrer Meldung. Die Bridge wartet höchstens 30 Sekunden auf den Hauptthread: sei schnell, oder antworte „started“ und lass die Erweiterung erneut fragen. Der Body eines Aufrufs hat höchstens 16 MB. |
| `Remove(modId)` | Entfernt deine Routen und deinen Listener (ein Plugin, das herunterfährt). |

Objekte, die die Bridge erzeugt, verschiebt oder ändert, werden den Mods des Spiels so übergeben, wie das Spiel ihnen
ein Objekt übergibt, das aus einem anderen Spiel kommt: ihre Daten gehen erneut durch `ZDO.Deserialize` (dieselben
Werte). Eine Mod, die durch einen Patch davon von Objekten erfährt (der ZdoWatcher von ValheimRAFT, der Schiffe und das,
woran Seile gebunden sind, über ihre IDs findet), kennt ein eingefügtes Objekt sofort, ohne eigenen Listener.

Sich erneut unter derselben ID zu registrieren, ersetzt, was vorher registriert war. Die Mod-ID gehört dir (die ID
deines Plugins ist eine gute): 1 bis 100 Buchstaben, Ziffern, Punkte, Bindestriche und Unterstriche; eine andere ID
wirft. `BridgeApi.Version` ist die Version der API (derzeit 1); eine Bridge führt Plugins aus, die gegen ihre Version
oder eine frühere gebaut sind, und Ergänzungen behalten sie bei. `GET /mods` auf der Bridge listet die API-Version und
die registrierten IDs.

### Die Seite deiner Erweiterung

Deine Erweiterung bekommt das Spiel über `ILiveGame`: jederzeit aus `registry.Host.Live` (null, wenn der Editor nicht
verbunden ist) oder im `OnConnected` deines Live-Listeners.

- `Mods`: die bei der Bridge registrierten IDs, wie die Bridge sie gemeldet hat, als die Welt geöffnet wurde (ein
  Plugin, das sich später registriert, wird bei der nächsten Verbindung gesehen).
- `CallAsync(modId, name, body)`: ruft deine Route auf und gibt die Bytes ihrer Antwort zurück. Es wirft, wenn die Mod
  oder die Route nicht registriert ist, wenn die Route fehlgeschlagen ist (mit der Meldung des Spiels) oder wenn das
  Spiel nicht erreichbar ist.
- `Plugins()`, `ConfigFile(path)` und `ConfigFolder(path)`: die BepInEx-Plugins, die das Spiel geladen hat, und eine
  Datei oder die Einträge eines Ordners unter seinem BepInEx/config, nur lesend (null bei einer Bridge älter als 1.54.0
  und für einen Pfad, der nicht existiert). Sie werfen `InvalidOperationException`, wenn die Bridge den Pfad abweist
  (außerhalb des Ordners, über 4 MB).

Frag das Spiel nach dem, was nur es weiß (was deine Mod im Code gebaut hat), bewahre es auf und rufe
`registry.Host.Redraw()` auf, damit deine Formen und Schnitte neu erzeugt werden. Der `LiveLink` des Beispiels ruft die
Route `ping` des Plugins auf, wenn sich der Editor verbindet, und protokolliert die Antwort; sein Plugin beantwortet
auch `players` (wie viele Spieler im Spiel sind).

Wenn sich eine Welt live öffnet, nennt das Protokoll des Editors die registrierten Mods („bridge mods: …“).

## Das Beispiel Example Vehicles

Das Zip des SDK (und das Repository des Editors) enthält den Quellcode der zwei Hälften einer erfundenen Mod, zum
Abschauen. Example Vehicles baut Fahrzeuge aus einem Rumpf und den darauf gebauten Teilen.

| Datei | Was sie zeigt |
|---|---|
| `sdk/samples/ExampleVehicles.WorldEditor/worldeditor.json` | Die Datei der Mod: Beschriftungen, ein ausgeblendetes Hilfsobjekt, Gruppen, die Verknüpfungsregel `vehicle`, die Erweiterung. |
| `sdk/samples/ExampleVehicles.WorldEditor/Extension.cs` | Der Einstiegspunkt, der je einen Hook jeder Art registriert. |
| `sdk/samples/ExampleVehicles.WorldEditor/Keys.cs` | Die Datenschlüssel und Prefabs der Mod, wie ihr Code sie nennt. |
| `sdk/samples/ExampleVehicles.WorldEditor/PackedIds.cs` | Ein Decoder und Kopier-Umschreiber für eine Liste von IDs, gepackt in ein Byte-Array (ein Versionsbyte, dann vier Bytes pro ID): im Inspektor gezeigt („Linked to 3 object(s): …“) und in einer eingefügten Kopie so umgeschrieben, dass sie die Objekte der Kopie nennt (IDs nicht mitkopierter Objekte fallen weg). |
| `sdk/samples/ExampleVehicles.WorldEditor/Sails.cs` | Formen: das Tuch eines Segels, zwischen drei oder vier in seinen Daten gespeicherten Ecken gezeichnet, in seiner Farbe, von beiden Seiten sichtbar. |
| `sdk/samples/ExampleVehicles.WorldEditor/Cables.cs` | Formen im Bezugssystem der Welt: ein Kabel von der Spitze eines Pfostens zur Spitze des Pfostens, den er nennt, gefunden mit `TryFind`. |
| `sdk/samples/ExampleVehicles.WorldEditor/Canals.cs` | Eine Szenenebene: jeder Kanal (ein Datenhalter, der seinen Pfad, seine Breite und Tiefe speichert) als Graben mit der Bodenoptik des Spiels gegraben, sein Bett mit Erde bemalt, der Boden darüber ungezeichnet. |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalTool.cs` | Ein Werkzeug: Punkte auf dem Boden für den Pfad eines Kanals anklicken (Backspace nimmt den letzten weg), seine Breite und Tiefe aus Schiebereglern, der Pfad in der Vorschau; Enter oder **Dig** fügt den Kanal hinzu (ein Datenhalter), **Clear points** oder Escape verwirft die Punkte. Shift+Klick schüttet einen Kanal zu (entfernt ihn), Ctrl+Klick gibt ihm Breite und Tiefe der Schieberegler (ändert ihn). |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalCopies.cs` | Ein Bereichskopierer: Kanäle, deren ganzer Pfad im Kopierten liegt, werden als Versatz von der Mitte der Kopie aufbewahrt und dort neu gegraben, wo jede Kopie eingefügt wird, mit ihr gedreht und gespiegelt, auf dem dortigen Boden. |
| `sdk/samples/ExampleVehicles.WorldEditor/Pits.cs` | Ein Bodenschnitt: ein rundes Loch so breit wie der Radius der Grube (1,5 m, wenn sie keinen speichert). |
| `sdk/samples/ExampleVehicles.WorldEditor/LiveLink.cs` | Die Seite der Erweiterung im Live-Modus: benennt die eigenen Schiffe des Spiels (`Host.Game`, aus den `gameComponents` des Manifests), prüft, dass sich das Plugin registriert hat, ruft seine Route `ping` auf, protokolliert die Antwort und fängt alles ab. |
| `sdk/samples/ExampleVehicles.Bridge/ExampleVehiclesBridgePlugin.cs` | Das BepInEx-Plugin der Mod, das am Live-Modus teilnimmt: weiche Abhängigkeit, Registrierung in einer eigenen Klasse, Routen `ping` und `players`, ein Listener, der protokolliert, was der Editor angewendet hat. Es baut gegen die DLLs des Spiels und von BepInEx wie jedes Plugin (`-p:ValheimDir=…`, der Ordner eines Valheim-Dedicated-Servers). |
