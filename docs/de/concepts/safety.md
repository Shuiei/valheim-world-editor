<!-- source: 4978ef09 -->
# Sicherheit und Datenschutz

Der Editor ist ein Programm auf deinem Computer. Er schreibt nur in deine Welten, wenn du **In Welt speichern** drückst, sendet
nichts irgendwohin, außer du verbindest ihn mit einem Server oder mit Claude, und hält Geheimnisse aus seinem Protokoll heraus. Diese Seite sagt,
was er schreibt, was deinen Computer verlassen kann und was mit deinen Rechten läuft.

## Was der Editor schreibt

**Deine Welten**, nur wenn du speicherst. Bis dahin ist jede Änderung ausstehend und lebt im Speicher des Editors (siehe
[Ausstehende Änderungen](saving-and-history.md#ausstehende-änderungen)). Ein Speichern fügt dem Ordner der Welt einen neuen nummerierten Speicherstand hinzu und
entfernt nichts Altes, bevor es den neuen korrekt zurückgelesen hat (siehe
[Eine Welt speichern](saving-and-history.md#eine-welt-speichern)). Eine neue Welt kommt in einen neuen Ordner, nie über eine Welt
desselben Namens. Schließe zuerst das Spiel: Ein laufendes Spiel überschreibt die Dateien, die der Editor gespeichert hat.

**Ein Live-Spiel**, nur wenn du anwendest. Das Spiel speichert die Änderungen dann selbst, so wie es speichert, was Spieler bauen.

**Deine Baupläne**: eine `.blueprint`-Datei und ihr `.png`-Bild im Ordner von Homestead im Speicherordner von Valheim, wenn du
einen Bauplan speicherst. **Löschen** entfernt beide, nach einer Rückfrage.

**Sein eigener Datenordner**: `~/.local/share/ValheimWorldEditor` unter Linux, `%LOCALAPPDATA%\ValheimWorldEditor` unter Windows,
`~/Library/Application Support/ValheimWorldEditor` unter macOS (unten im Menü der Startseite steht er). Er enthält:

| Was | Dateien |
|---|---|
| Deine Einstellungen und deine Wahl | `settings.json`, `prefs.json`, `place.json` (Vorgaben, Favoriten und zuletzt benutzte Arten von Platzieren), `stamps.json`, `selections.json` |
| Gespeicherte Server | `servers.cfg`: Adresse, Benutzer, Token, die Identität des Servers und das Passwort, wenn du **Passwort speichern** angehakt hast (siehe unten) |
| Verlauf | `history/`, eine Datei pro Welt (siehe [Verlauf](saving-and-history.md#verlauf)) |
| Deine Arbeit | `scripts/` (gespeicherte Skripte), `heightmaps/` (exportierte Höhenkarten), `blueprints/export` (`.vbuild`-Dateien) |
| Was er aus deinem Spiel gelernt hat | `game-index.json` und andere `game-*.json`-Dateien (wo die Dinge in den Dateien von Valheim liegen), `mod-catalogue.json` (in einem Live-Spiel gesehene Objekte von Mods), `mod-bundles/` (Modelle von Mods, aus ihren DLLs kopiert) |
| Das Protokoll | `ValheimWorldEditor.log`, bei jedem Start ein neues; `perf-native.log` nur, solange **Bildraten aufzeichnen** an ist |

**Was er nie ändert.** Der Editor liest deinen Spielordner von Valheim, deine BepInEx-Ordner und die Dateien deiner Mods (Modelle,
Texturen, die Regeln des Spiels, die Einstellungen des Plugins) und schreibt dort nichts. Er liest den Code von Mods, um die Namen
ihrer Werte zu lernen, und führt ihn nie aus.

**Passwörter.** Ein gespeichertes Passwort wird als Klartext in `servers.cfg` aufbewahrt. Unter Linux kann nur du die Datei lesen;
trotzdem: Benutze lieber einen SSH-Schlüssel, wo du kannst. Die Passphrase eines Schlüssels wird nie gespeichert. **vergessen** neben einem gespeicherten Server entfernt
ihn, mit ihm sein Passwort und sein Token.

## Nichts verlässt deinen Computer

Der Editor hat kein Konto, kein Tracking und keine Update-Prüfung. Er spricht nur in den folgenden Fällen mit der Außenwelt, jeder
davon deine Wahl:

| Wann | Was wohin geht |
|---|---|
| Du verbindest dich mit einem **dedizierten Server** | Eine SSH-Verbindung, verschlüsselt, nur zu diesem Server, mit dem Konto, das du angibst. Das Plugin wird darüber erreicht, nie über das offene Internet (siehe [Wie die Verbindung funktioniert](live-editing.md#wie-die-verbindung-funktioniert)). |
| Du bearbeitest **dein eigenes Spiel** live | Nichts verlässt den Computer: Der Editor spricht mit dem Plugin auf `127.0.0.1`. |
| Du verbindest **Claude** | Die App von Claude auf deinem Computer spricht mit dem Editor auf `127.0.0.1`. Was der Editor antwortet (die Daten des Bereichs, Bilder der 3D-Ansicht), geht an Claude, wie alles, was du Claude gibst. |
| Du klickst auf einen Link | **Homestead holen** oder eine auf GitHub geöffnete Hilfeseite öffnet sich in deinem Webbrowser. |

**Das Plugin bleibt privat.** WorldEditorBridge lauscht nur auf dem Computer, auf dem es läuft, und beantwortet nur Anfragen,
die sein geheimes Token tragen. Bei einem Server tippst du das Token selbst ein; der Editor liest es nie vom Server, daher kann
eine SSH-Anmeldung allein die Welt nicht ändern. Halte das Token geheim: Jeder, der es hat und Zugang zum Port hat, kann die
Welt ändern. Leere die Zeile `Token` in `Tie.WorldEditorBridge.cfg` und starte das Spiel neu, um ein neues zu bekommen.

**Die Identität des Servers wird geprüft.** Der Editor merkt sich bei der ersten Verbindung die Identität eines Servers. Ändert sie sich
je, verweigert der Editor die Verbindung, weil sich jemand als dein Server ausgeben könnte. Nachdem du einen
Server neu installiert hast, leere seine Zeile `HostKey` in `servers.cfg`, um die neue zu akzeptieren.

## Skripte und Claude

**Skripte sind Programme.** Ein [Skript](../scripting.md) ist C#-Code, und der Editor führt es mit deinen Rechten aus, wie jedes
Programm, das du startest. Es läuft in einem eigenen Prozess, damit **Stopp** es immer beenden kann, aber dieser Prozess ist keine
Sandbox: Ein Skript kann deine Dateien lesen und schreiben und alles andere tun, was dein Konto kann. Führe nur Skripte aus, die du geschrieben hast oder denen du
vertraust, so wie bei einer Mod.

**Claude arbeitet für dich, mit Grenzen.** Mit **Claude verbinden lassen** an in den Einstellungen kann [Claude](../claude.md)
die Werkzeuge des Editors benutzen. Der Editor hält ihn dabei an Folgendes:

- **Nur dieser Computer, mit einem Token.** Der Editor lauscht nur auf `127.0.0.1`, lehnt Anfragen ab, die an einen anderen
  Namen gerichtet sind (sodass eine Webseite ihn nicht unter einer erfundenen Adresse erreichen kann), und lehnt jede Anfrage ohne sein Token ab (48 zufällige
  Zeichen). Eine andere **Adresse** in den Einstellungen (`0.0.0.0` oder eine dieses Computers) lässt andere Computer in diesem
  Netzwerk sich verbinden, mit dem Token, das dann unverschlüsselt übertragen wird: nur Computer mit einer lokalen Adresse im eigenen Netzwerk dieses
  Computers, nie aus dem Internet, und nie Skripte (die bleiben dem Claude dieses Computers vorbehalten). **Neues Token**
  erzeugt ein anderes; schalte den Schalter aus, und nichts kann sich verbinden.
- **Nur ausstehend.** Was Claude ändert, bleibt ausstehend, je ein Schritt, im Verlauf mit **Claude** markiert. Claude
  speichert nie, wendet nie live an, verwirft nie, lädt nie neu, schreibt oder löscht nie deine Dateien und verbindet sich nie mit einem Live-Spiel: Das bleibt deine Sache.
  Er öffnet keine andere Welt, solange deine ungespeicherte Änderungen hat.
- **Nichts live mit Auto an.** Solange in einer Live-Welt **Auto** an ist, würde jede Änderung die Spieler sofort erreichen, daher
  ändert Claude nichts und bittet dich, Auto auszuschalten.
- **Keine Skripte ohne deine Erlaubnis.** Claudes Skripte werden abgelehnt, bis du **Claude darf Skripte ausführen** in den
  Einstellungen einschaltest (standardmäßig aus). Selbst dann wird dir jedes Skript, das Claude schreibt, vor dem Ausführen gezeigt, zum Ausführen oder Ablehnen:
  Text in einer Welt (ein Schild, ein von einem anderen Spieler geschriebener Tag) könnte Claude dazu bringen, ein schädliches zu schreiben. Seine anderen
  Werkzeuge führen den eigenen Code des Editors aus und werden nicht erfragt.
- **Leicht zurückzunehmen.** **Claudes Änderungen zurücknehmen** im Verlauf entfernt jeden Schritt, den Claude im offenen Bereich gemacht hat,
  und behält deine (siehe [Verlauf](saving-and-history.md#verlauf)).

## Erweiterungen und Add-ons von Mods

Eine Mod kann eine **Editor-Erweiterung** mitliefern: ein kleines Stück Code, das der Editor lädt, um die Objekte dieser Mod zu lesen und zu zeichnen.
Wie ein Skript läuft sie mit deinen Rechten, also entscheidest du. Jede Erweiterung ist **aus**, bis du sie unter **Einstellungen →
Mod-Erweiterungen** einschaltest, und nur genau diese Datei: Der Editor merkt sich ihren Fingerabdruck (SHA-256), daher ist eine geänderte Datei, etwa
ein Mod-Update, wieder aus, bis du sie erneut einschaltest.

**Add-ons**, die du selbst herunterlädst und in den Ordner `addons` des Editors entpackst, laufen wie installiert, da
das Installieren schon deine Wahl ist. **Einstellungen → Add-ons** schaltet jedes davon aus.

## Was das Protokoll heraushält

Der Editor schreibt, was er tut, und jeden Fehler, den er behandelt, in `ValheimWorldEditor.log`. **Protokoll öffnen** unten
im Menü der Startseite öffnet es, damit du es an einen Fehlerbericht anhängen kannst. Bevor eine Zeile geschrieben wird, werden persönliche Daten
herausgenommen:

- dein Benutzerordner wird als `~` geschrieben, und andere Dateipfade, Webadressen, Namen wie `user@host`, IP- und Serveradressen
  sowie dein Kontoname werden durch Platzhalter wie `<path>` ersetzt;
- die Konten, Passwörter, Passphrasen und Tokens der gespeicherten Server erreichen es nie.

Das Plugin WorldEditorBridge schreibt in das eigene `BepInEx/LogOutput.log` des Spiels, wobei der Benutzerordner des Servers ebenfalls
als `~` geschrieben wird.

Lies ein Protokoll trotzdem, bevor du es öffentlich postest. Und behalte eigene Kopien der Welten, die dir wichtig sind (siehe
[Eine Welt vor dem Bearbeiten sichern](../guides/back-up-a-world.md)).
