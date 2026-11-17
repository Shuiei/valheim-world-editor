<!-- source: 1000c3db -->
# Eine mit Expand World, Riverheim oder Tunnelheim erstellte Welt bearbeiten

Installiere das Add-on des Editors für deine Mod, damit sich eine damit erstellte Welt so öffnet, darstellt und
speichert, wie die Mod sie erzeugt.

**Du brauchst:** die Mod, installiert in einem BepInEx-Ordner auf diesem Computer (deinem Valheim, einem
Mod-Manager-Profil oder einer Kopie des Ordners `BepInEx` eines Servers), und das Add-on aus demselben Release des
Editors.

| Add-on | Für | Was es hinzufügt |
|---|---|---|
| [Expand World](../../../addons/ExpandWorld/README.md) | Expand World Size, Rivers und Data | Boden, Karte, Biome, Wasser, Orte, Vegetation und die Vorhersagen der Seite Neue Welt so, wie die Mods sie erzeugen |
| [Railroads](../../../addons/Railroads/README.md) | Railroads (`Railroads.dll`) | Zeichnet die Gleise, Bahnhöfe und Züge der Mod aus ihren eigenen Modellen; die Bauteile, die du platzierst, einfügst, verschiebst oder drehst, funktionieren im Spiel, auch live |
| [Riverheim](../../../addons/Riverheim/README.md) | Riverheim (`Riverheim.dll`) | Öffnet Riverheim-Welten (ohne das Add-on abgelehnt), dargestellt mit deiner eigenen Kopie der Mod; Riverheim als Generator für Neue Welt |
| [Tunnelheim](../../../addons/Tunnelheim/README.md) | Tunnelheim (`ValheimTunneling.dll`) | Zeichnet die Tunnel der Mod und ein Werkzeug **Tunnel** (Graben, Add Earth, Bohrungen, Räume, Schächte, Treppen); Tunnel gehen mit Kopien und Bauplänen mit |
| [ValheimRAFT](../../../addons/ValheimRAFT/README.md) | ValheimRAFT (`ValheimRAFT.dll`) | Zeichnet die Schiffe und Landfahrzeuge der Mod vollständig, mit ihren Segeln und Seilen; jedes wird als eines verschoben, kopiert und gelöscht |

1. Schließe den Editor.
2. Lade von der [Release-Seite](https://github.com/Shuiei/valheim-world-editor/releases/latest) des Editors (oder von
   Nexus Mods) `ValheimWorldEditor-addon-<Name>-<version>.zip` herunter.
3. Entpacke es in den Ordner `addons` neben dem Editor (unter macOS neben `ValheimWorldEditor.app`), sodass du
   `addons/<Name>/worldeditor.json` hast.
4. Starte den Editor und öffne auf der Startseite **Einstellungen**.
5. Such unter **Add-ons** das Add-on. Es sollte „Läuft.“ anzeigen (oder „Noch nicht gelesen“, bis eine Welt geöffnet
   wird). Zeigt es „Wartet auf seine Mod: … ist nicht in den Plugin-Ordnern oben.“, füge den BepInEx-Ordner, der die Mod
   enthält, unter **BepInEx-Ordner** hinzu, dann **Speichern**.
6. Für die Welt eines Servers, die aus ihren Dateien geöffnet wird, füge eine Kopie des Ordners `BepInEx` des Servers
   unter **BepInEx-Ordner** hinzu, damit der Editor die Mod-Einstellungen des Servers liest.
7. Öffne die Welt unter **Eine gespeicherte Welt**. Ihre Karte zeigt die Mod (zum Beispiel **Riverheim** oder „Expand
   World“).
8. Bearbeite und speichere wie gewohnt. Mit Tunnelheim wähle in der Werkzeugleiste unter **MODS** das Werkzeug
   **Tunnel**, wähle eine Aktion und klicke auf den Boden oder den Fels.

## Prüfen, ob es geklappt hat

- Die Welt öffnet sich, und ihr Boden und ihre Karte sehen aus wie im Spiel (die Flüsse von Riverheim, die Größe und
  Biome von Expand World).
- Das Protokoll des Editors (**Protokoll öffnen** auf der Startseite) sagt, was das Add-on gefunden und gelesen hat.
- Eine Weltkarte, die ausgegraut bleibt, sagt, warum: siehe [Eine Welt öffnet sich nicht oder ist ausgegraut](../help/troubleshooting.md#eine-welt-öffnet-sich-nicht-oder-ist-ausgegraut).

## Rückgängig machen

Jede Bearbeitung lässt sich wie jede andere rückgängig machen (`Ctrl+Z`, Verlauf). Um ein Add-on nicht mehr zu nutzen,
schalte seinen Schalter unter **Einstellungen → Add-ons** aus (sein Ordner bleibt) oder lösche seinen Ordner aus
`addons`. Ist es aus, werden Expand-World-Welten so erzeugt, wie das Spiel es ohne die Mods tut, und Riverheim-Welten
werden abgelehnt.

## Gut zu wissen

- Ein Add-on wird nur genutzt, solange die DLL seiner Mod in einem Plugin-Ordner liegt, den der Editor kennt; eine Mod,
  die ein Mod-Manager deaktiviert hat, oder Einstellungen, die eine entfernte Mod hinterlassen hat, zählen nicht.
- Die Bauteile von Railroads werden aus den Modelldateien gezeichnet, die die Mod in ihrem Ordner `meshes` aufbewahrt:
  lass diesen Ordner neben `Railroads.dll`, so wie die Mod ihn installiert.
- Die Datendateien von Expand World brauchen die Bibliothek YamlDotNet, die die Mod unter `BepInEx/plugins` installiert;
  ohne sie werden sie nicht gelesen, und das Protokoll sagt es.
- Dein Riverheim muss die Vorgabe der Welt kennen (aktualisiere es, wenn es älter ist als das, mit dem die Welt erstellt
  wurde). Riverheim 1.3 und neuer braucht Worldhalla daneben, wie im Spiel. Welten, die mit der eingestellten Beta von
  Riverheim erstellt wurden, lassen sich nicht öffnen. Live muss dein Riverheim die Version sein, die Spieler zum
  Beitreten brauchen (seit 1.3 dieselbe Version, davor dasselbe Release).
- Eine Riverheim-Welt braucht Riverheim im Spiel jedes Spielers, daher können Konsolenspieler über Crossplay ihr nicht
  beitreten.
- Live werden die Dateien von Expand World über das Plugin aus dem Spiel selbst gelesen: siehe [Live-Modus](../live-mode.md).

## Verwandte Seiten

- [Eine neue Welt](../new-world.md): Riverheim und Expand World beim Erstellen einer Welt
- [Live-Modus](../live-mode.md): Spiele mit Mods, live
- [Eine Welt öffnen, die nicht in der Liste steht](open-a-world.md)
