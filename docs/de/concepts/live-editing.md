<!-- source: 6167da37 -->
# Live bearbeiten

Im Live-Modus arbeitet der Editor an der Welt eines **laufenden** Spiels statt an ihren Dateien. Spieler können verbunden bleiben:
Sie sehen deine Änderungen, sobald du sie anwendest, und du siehst, was sie bauen, während du bearbeitest. Diese Seite erklärt, wie
der Editor das Spiel erreicht, was zwischen beiden hin- und hergeht und was passiert, wenn Spieler und der Editor dieselbe
Stelle ändern.

## Wie die Verbindung funktioniert

Der Live-Modus braucht ein kleines Plugin, **WorldEditorBridge**, im Spiel, das die Welt **hostet**: der dedizierte Server
oder dein eigenes Spiel, wenn du allein spielst oder hostest. Es läuft unter **BepInEx**, dem üblichen Mod-Loader für Valheim. Spieler, die beitreten,
brauchen keins von beiden, auch Konsolenspieler nicht, und das Bearbeiten offline braucht keins von beiden.

Das Plugin antwortet dem Editor auf einem Port des Computers, auf dem es läuft (anfangs 5182), und es ist darauf gebaut, privat zu bleiben:

- **Es lauscht nur auf diesem Computer selbst** (`127.0.0.1`), daher kann nichts im Netzwerk es erreichen. Seine
  Einstellung `BindAddress` kann es für die lokalen Netzwerke und VPNs des Computers öffnen (nie für das Internet: Solche Anfragen
  werden abgelehnt), mit unverschlüsseltem Token; lass sie, wie sie ist, außer du bearbeitest von einem anderen Computer im Haus aus
  ([Ohne SSH](../live-mode.md#ohne-ssh)).
- **Es braucht sein geheimes Token.** Beim ersten Start schreibt das Plugin `BepInEx/config/Tie.WorldEditorBridge.cfg` mit
  einem **Token** aus 24 zufälligen Buchstaben und Ziffern. Eine Anfrage ohne es wird abgelehnt. Bis das Spiel oder der Server einmal
  mit dem Plugin gestartet ist, gibt es kein Token.

**Dein eigenes Spiel.** Der Editor läuft auf demselben Computer, daher liest er den Port und das Token selbst aus dieser Datei, in
jedem BepInEx-Ordner, den er kennt (deinem Valheim-Ordner, deinen Mod-Manager-Profilen, den in den **Einstellungen** hinzugefügten Ordnern), und
fragt jedes gefundene Plugin, ob eine Welt geladen ist.

**Ein dedizierter Server.** Das Plugin ist von deinem Computer aus nicht direkt erreichbar, daher baut der Editor seinen eigenen Weg hinein:

```text
 dein Computer                                   der Server
 ┌──────────────────────┐ SSH, verschlüsselt ┌─────────────────────────────────────┐
 │ Editor ── Port 15182 ═╪═══════════════════╪═► 127.0.0.1:5182  WorldEditorBridge │
 └──────────────────────┘                    │                   in Valheim        │
                                             └─────────────────────────────────────┘
```

1. Er meldet sich per **SSH** mit deinem Konto am Server an (er hat sein eigenes SSH, daher wird kein `ssh`-Programm gebraucht).
2. Er leitet einen freien Port deines Computers (ab 15182) durch diese verschlüsselte Verbindung an den Port des Plugins auf
   dem Server weiter: ein **SSH-Tunnel**.
3. Er sendet das Token, das **du** eintippst: Der Editor liest es nie vom Server. Eine SSH-Anmeldung allein reicht also nicht,
   um die Welt zu ändern; wer sich verbindet, muss auch das Geheimnis des Plugins kennen.

Ein Server, den ein Game-Panel (Pterodactyl, Pelican) in einem Docker-Container ausführt, hat sein eigenes `127.0.0.1`, das der
Tunnel zur Maschine nicht erreicht, und ein bei einem Anbieter gemieteter Spielserver bietet meist überhaupt keine SSH-Anmeldung: siehe
[Einen gemieteten oder über ein Panel gehosteten Server bearbeiten](../guides/edit-a-rented-server.md).

Der Editor merkt sich außerdem bei der ersten Verbindung die Identität des Servers. Ändert sie sich je, verweigert der Editor die
Verbindung, weil sich jemand als dein Server ausgeben könnte.

Das Plugin ändert nur eine Welt, die sein Spiel hostet. In einem Spiel, das dem Server eines anderen beigetreten ist, verweigert es, da ein solches
Spiel nur die Objekte um seinen eigenen Spieler kennt. Benutze das Plugin aus demselben Release wie deinen Editor.

## Was wann gesendet wird

**Wenn du dich verbindest**, liest der Editor die ganze Welt aus dem Spiel: jedes Objekt, die Zonen, die das Spiel erzeugt hat, die
Orte. Es sind dieselben Daten, die ein Speicherstand enthält, daher funktionieren die Karte, die Suche und jedes Werkzeug wie offline. Bei einer großen Welt
dauert das eine Weile, bis zu ein paar Minuten. Er fragt auch nach den Objekten, die das Spiel kennt und die nicht von Valheim selbst sind
(Bauteile von Mods), damit sie ihre Namen bekommen und in der Liste von Platzieren und in der Leiste Bauen erscheinen.

**Wenn du anwendest** (**Live anwenden** oder mit **Auto** jeden Schritt sofort), sendet er, was sich seit dem letzten Anwenden geändert hat, in
dieser Reihenfolge:

1. **Den Boden** jeder Zone, die du geändert hast, Boden mit Ohne Grenze eingeschlossen (siehe [Ohne Grenze](ground-limits.md#ohne-grenze)).
2. **Die Objekte**, die du gelöscht, platziert, eingefügt, ersetzt, geändert oder verschoben hast.
3. **Zonen-Resets**, zuletzt. Das Spiel erzeugt diese Zonen neu, daher liest der Editor danach die Welt erneut; der Verlauf
   kommt mit, ohne das, was er in diesen Zonen getan hat.

Das Spiel teilt jede Änderung mit jedem verbundenen Spieler, wie es das mit dem tut, was Spieler bauen, und speichert sie mit der
Welt. Live platzierte Objekte erzeugt der Server, daher sehen Spieler sie wie etwas gerade Gebautes oder Gepflanztes.

Das Plugin tut all das in der eigenen Schleife des Spiels, ein paar Anfragen pro Bild, sodass das Spiel seine
Welt nie von zwei Stellen gleichzeitig liest oder ändert. Eine große Änderung (Tausende von Objekten) kann den Server kurz stocken lassen. Nimmt das Spiel
eine Anfrage nicht innerhalb von 30 Sekunden an, verwirft das Plugin sie: Nichts davon wird ausgeführt und nichts als angewendet markiert, also
wende erneut an. Geht stattdessen die Antwort auf dem Rückweg verloren (ein abgebrochener Tunnel), kann der Editor nicht wissen, ob es
ausgeführt wurde; erneutes Senden ist sicher, da das Plugin eine Anfrage erkennt, die es schon ausgeführt hat, und ihre Objekte nicht doppelt erzeugt.
Es läuft immer nur ein Anwenden gleichzeitig.

### Ein verschobenes Objekt bleibt dasselbe Objekt

Ein Objekt, das du verschiebst oder drehst, bewegt sich im Spiel unter seiner eigenen ID, wenn sich sonst nichts daran geändert hat. Eine Mod, die
Objekte verfolgt, während das Spiel läuft (das Beschriftungsschild einer Truhe, die Truhen eines Lagers, eine Ward), kennt es dann weiterhin, und eine
Truhe behält ihren Inhalt, wie das Spiel ihn hat. Ein Objekt, dessen Daten du geändert hast, behält seine ID auf dieselbe Weise.

Es gibt einen Haken. Das Spiel eines Spielers setzt ein stehendes Objekt (eine Wand, eine Truhe) einmal an seinen Platz, wenn es in Reichweite kommt, und
bewegt es danach nie. Ein Verschieben behält die ID daher nur, wenn kein Spieler sowohl seine alte als auch seine neue Stelle geladen hat
(etwa 160 m um jeden Spieler). Sonst wird das Objekt an seiner neuen Stelle neu erzeugt und an der alten entfernt,
und die Meldung zum Anwenden sagt, wie viele: Für andere Mods sind das neue Objekte. Verschiebe einen Bau, während niemand in seiner Nähe ist, um
alles daran zu behalten. Die verbundene Gruppe einer Mod (ein Schiff und seine Bauteile) und Kreaturen werden immer neu erzeugt.

## Dem Spiel folgen

Solange ein Bereich offen ist, fragt der Editor das Spiel alle 2 Sekunden, was sich in den Zonen dieses Bereichs geändert hat. Das ist billig: Das
Spiel antwortet mit einer Zahl pro Zone, die sich ändert, wann immer sich in der Zone etwas ändert, und nur die Zonen, deren
Zahl sich geändert hat, werden neu gelesen. Die Statusleiste sagt, was hereingekommen ist („Aus dem Spiel: 2 neue(s) Objekt(e), 1 entfernt, der Boden
von 1 Zone(n).“); eine reine Datenänderung, etwa ein herunterbrennendes Feuer, kommt kommentarlos herein.

- **Objekte**, die Spieler bauen, entfernen oder ändern, erscheinen, verschwinden oder ändern sich im Editor, ohne einen Schritt im Verlauf. Ein geändertes
  Objekt bleibt im Editor dasselbe Objekt, daher bleiben die Auswahl, der Inspektor und seine Stelle im Verlauf bei ihm.
- **Deine eigenen Objekte werden, einmal angewendet,** so gezeigt, wie das Spiel sie wirklich hat: wo die Physik ein Bauteil abgesetzt hat, die
  Gesundheit und den Spawnpunkt, die das Spiel einer Kreatur gegeben hat.
- **Dinge, die sich von selbst bewegen oder ändern** (Kreaturen, Schiffe, Karren, herumliegende Gegenstände, Feuer und Schmelzöfen), werden
  verfolgt, während sie sich bewegen, ohne jedes Mal ihre ganze Zone zu lesen.
- **Boden**, der im Spiel gegraben, geebnet oder bemalt wurde, kommt dort herein, wo du in dieser Zone keine eigenen Änderungen hast (siehe unten,
  wenn du welche hast).

Das Folgen geschieht nur im 3D-Editor, nie während eines Pinselstrichs oder beim Anwenden. Die Karte zeigt die Welt so, wie sie
zuletzt gelesen wurde: **Neu laden** liest sie ganz neu.

## Mehrere Editoren und Spieler

Spieler und der Editor können dieselbe Stelle ändern. Der Editor regelt das so:

| Was passiert | Was der Editor tut |
|---|---|
| Spieler ändern den Boden einer Zone, in der du noch nicht angewendete Änderungen hast | Deine bleiben erhalten, und die Statusleiste sagt es. **Live anwenden** fragt dann zuerst (**Über die Änderungen des Spiels anwenden**): Anwenden schreibt deinen Boden über diese Zone, und was Spieler seitdem daran getan haben, kann verloren gehen. **Nicht jetzt** lässt deine Änderungen ausstehend. |
| Spieler nehmen oder zerstören ein Objekt | Es ist endgültig weg: Rückgängig und Wiederholen lassen es in Ruhe, und ein Löschen davon, das du nicht angewendet hattest, fällt weg. Das gilt auch für Objekte, die du platziert hast. |
| Du löschst ein Objekt und wendest an | Es bleibt gelöscht; Rückgängigmachen holt es als Kopie zurück (siehe [Live anwenden](saving-and-history.md#live-anwenden)). |
| Du verschiebst einen Bau, während Spieler in der Nähe sind | Er wird neu erzeugt statt verschoben (siehe [Ein verschobenes Objekt bleibt dasselbe Objekt](#ein-verschobenes-objekt-bleibt-dasselbe-objekt)). |
| Du änderst ein Objekt im Inspektor, während das Spiel es auch ändert | Der Inspektor wendet nicht über die neuere Version an: Er zeigt sie, um sie erneut zu ändern. |
| Du setzt eine Zone zurück, in deren Nähe ein Spieler steht | Das Spiel erzeugt sie sofort neu; sonst beim nächsten Mal, wenn jemand kommt. |

Das Plugin antwortet jedem Editor, der das Token hat, und es unterscheidet die Änderungen eines Editors nicht von denen eines Spielers: Was
ein anderer Editor anwendet, erreicht deinen so, wie die Änderung eines Spielers.

**Auto und Claude.** Mit **Auto** an erreicht jeder Schritt die Spieler in dem Moment, in dem du ihn beendest, ein langer Pinselstrich
als ein Anwenden. Deshalb ändert Claude nichts in einer Live-Welt, solange Auto an ist (siehe
[Skripte und Claude](safety.md#skripte-und-claude)).
