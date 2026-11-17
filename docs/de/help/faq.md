<!-- source: 93850bba -->
# Fragen

Kurze Antworten auf das, was am häufigsten gefragt wird, jede mit einem Link zur vollständigen Antwort.

## Ist es sicher für meine Welt?

Ja, wenn du eine Kopie behältst. Nichts wird geschrieben, bis du **In Welt speichern** oder **Live anwenden** drückst,
und jeder Speicherstand wird zurückgelesen und geprüft: liest er sich nicht korrekt zurück, werden die neuen Dateien
entfernt, und die Welt ist, wie sie war. Kopiere trotzdem eine Welt, die dir wichtig ist, vor deinem ersten Speichern:
siehe [Eine Welt vor dem Bearbeiten sichern](../guides/back-up-a-world.md) und
[Was der Editor schreibt](../concepts/safety.md#was-der-editor-schreibt).

## Kann ich nach dem Speichern rückgängig machen?

Ja. Der Verlauf wird nach jedem Speichern oder Anwenden mit der Welt aufbewahrt, auch nachdem du den Editor schließt:
mach einen Schritt rückgängig (oder entferne ihn im Verlauf mit **Entfernen**) und speichere erneut. Siehe
[Einen Schritt rückgängig machen, eine Änderung zurücknehmen oder alles verwerfen](../guides/undo-changes.md).
Zonen-Resets und Boden ohne Grenze behalten ihn auch; was ein Reset neu erzeugt hat, liegt außerhalb seiner Reichweite
(siehe [Was ein Schritt nicht zurücknehmen kann](../concepts/saving-and-history.md#was-ein-schritt-nicht-zurücknehmen-kann)).

Für Schäden, die du später entdeckst: [Einen Bereich aus einer Sicherung wiederherstellen](../guides/restore-from-backup.md).

## Brauchen die Spieler auf meinem Server etwas?

Nein. Nur das Spiel oder der Server, der die Welt hostet, braucht BepInEx und das Plugin, und nur für die
Live-Bearbeitung. Spieler, die beitreten, brauchen nichts, auch nicht für Boden, der über die 8-m-Grenze des Spiels
angehoben wurde. Siehe [Bodengrenzen](../concepts/ground-limits.md#ohne-grenze).

## Können Konsolenspieler über Crossplay einer bearbeiteten Welt beitreten?

Ja: eine bearbeitete Welt ist weiterhin eine normale Valheim-Welt, und der Boden des Editors über 8 m braucht keine Mod.
Ausnahmen sind Welten, die selbst eine Mod brauchen: eine mit Riverheim erstellte Welt oder ein Server, auf dem Homestead
läuft, weist Spieler ohne die Mod ab, und Konsolenspieler können keine Mods installieren. Siehe
[Eine neue Welt](../new-world.md#tipps-und-grenzen).

## Kann ich bearbeiten, während das Spiel läuft?

Ja, im Live-Modus, mit dem Plugin WorldEditorBridge im Spiel oder auf dem Server: siehe
[Die Welt bearbeiten, die du gerade spielst](../guides/edit-your-game-live.md) oder
[Einen dedizierten Server bearbeiten, während gespielt wird](../guides/edit-a-server-live.md). Um die Dateien einer
gespeicherten Welt zu bearbeiten, schließe zuerst das Spiel oder stoppe den Server: ein laufendes Spiel speichert über
das, was der Editor schreibt. Siehe
[Das Spiel hat gespeichert, während du bearbeitet hast](troubleshooting.md#das-spiel-hat-gespeichert-während-du-bearbeitet-hast).

## Kann ich einen Server bearbeiten, den ich bei einem Anbieter miete?

Ja, aber live nur, wenn der Anbieter dir SSH-Zugang zur Maschine gibt (eine Shell-Anmeldung, nicht nur SFTP oder einen
Dateimanager): frag den Anbieter, bevor du mietest. Die meisten bei einem Anbieter gemieteten Spielserver bieten nur ein
Webpanel und FTP, daher kann der Editor das Plugin nicht erreichen: stoppe den Server, lade seine Welt herunter,
bearbeite sie, lade sie wieder hoch. Ein Server, den ein Spielpanel wie Pterodactyl in einem Container auf deiner eigenen
Maschine ausführt, lässt sich live bearbeiten, durch einen Tunnel, den du selbst öffnest. Siehe
[Einen gemieteten oder über ein Panel gehosteten Server bearbeiten](../guides/edit-a-rented-server.md).

## Der Editor fragt nach einem Token. Wo finde ich es?

In `BepInEx/config/Tie.WorldEditorBridge.cfg` des Spiels oder Servers, in der Zeile `Token`. Das Plugin schreibt diese
Datei, wenn das Spiel zum ersten Mal mit ihm startet. Bei deinem eigenen Spiel liest der Editor es selbst; bei einem
Server kopierst du es vom Server. Siehe [Plugin-Einstellungen](../live-mode.md#plugin-einstellungen).

## Funktioniert es mit Mods?

Ja. In einem laufenden Spiel mit Mods lernt der Editor die Objekte der Mods kennen und zeichnet ihre Modelle (siehe
[Spiele mit Mods](../live-mode.md#gemoddete-spiele)). Mods, die ändern, wie Welten erzeugt werden, brauchen dafür das Add-on
des Editors: Expand World, Riverheim und Tunnelheim haben eines. Siehe
[Eine mit Expand World, Riverheim oder Tunnelheim erstellte Welt bearbeiten](../guides/use-mods-worlds.md).

## Ich spiele mit den Mods von Expand World. Weiß der Editor das?

Ja, mit installiertem Add-on Expand World: der Boden, die Karte, die Biome und die Vorhersagen folgen den Einstellungen
und Datendateien der Mods. Ohne es wird jede Welt so erzeugt, wie das Spiel selbst es tut. Siehe
[Eine mit Expand World, Riverheim oder Tunnelheim erstellte Welt bearbeiten](../guides/use-mods-worlds.md).

## Meine Welt wurde mit Riverheim erstellt. Kann ich sie öffnen?

Ja, mit installiertem Add-on Riverheim und Riverheim selbst in einem BepInEx-Ordner, den der Editor kennt: der Editor
zeichnet die Welt mit deiner eigenen Kopie der Mod. Siehe [Eine mit Expand World, Riverheim oder Tunnelheim erstellte Welt bearbeiten](../guides/use-mods-worlds.md).

## Brauche ich Homestead?

Nur, um die Baupläne des Editors im Spiel zu bauen. Die Werkstatt, die Bibliothek und das Einfügen von Bauplänen in eine
Welt funktionieren ohne es; fehlt es, sagt die Startseite es. Beachte, dass ein Server, auf dem Homestead läuft, Spieler
abweist, die es nicht haben. Siehe [Die Werkstatt](../workshop.md).

## Wo lade ich es herunter? Gibt es es auf Thunderstore, Hexium oder Nexus Mods?

- **Der Editor** ist ein Programm, keine Mod: lade ihn von der
  [Release-Seite](https://github.com/Shuiei/valheim-world-editor/releases/latest) oder von
  [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348) herunter.
- **Das Plugin WorldEditorBridge** (für die Live-Bearbeitung) gibt es auf der Release-Seite, auf Nexus Mods,
  [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) und
  [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge), für Mod-Manager.
- **Add-ons** für Mods gibt es auf der Release-Seite und auf Nexus Mods.

Nutze das Plugin und die Add-ons aus demselben Release wie den Editor. Siehe [Installieren](../start/install.md).

## Sendet es etwas über das Internet?

Nein. Alles passiert auf deinem Computer; ein dedizierter Server wird nur über den eigenen verschlüsselten SSH-Tunnel
des Editors erreicht, mit einem Token, das du eingibst. Siehe
[Nichts verlässt deinen Computer](../concepts/safety.md#nichts-verlässt-deinen-computer).

## Ist es sicher, Claude oder ein Skript meine Welt ändern zu lassen?

Was Claude ändert, bleibt ausstehend, bis du speicherst, und Claude kann sich nur von diesem Computer aus mit einem Token
verbinden. Skripte sind Programme, die mit deinen Rechten laufen, daher darf Claude sie nur ausführen, wenn du es
erlaubst. Siehe [Skripte und Claude](../concepts/safety.md#skripte-und-claude).

## Etwas funktioniert nicht. Wo schaue ich nach?

Fang mit der [Fehlerbehebung](troubleshooting.md) an. Um einen Fehler zu melden, hänge das Protokoll des Editors an:
[Das Protokoll und Fehlerberichte](troubleshooting.md#das-protokoll-und-fehlerberichte).
