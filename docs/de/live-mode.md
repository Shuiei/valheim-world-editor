<!-- source: e9428d01 -->
# Live-Modus

Bearbeite die Welt eines laufenden Spiels oder dedizierten Servers, während gespielt wird: die Spieler sehen jede
Änderung, sobald du sie anwendest.

**Wo:** Startseite, **Live bearbeiten** auf **Startseite** oder **Mein Spiel**, oder **Ein dedizierter Server** ·
**Taste:** `Ctrl+S` wendet an · **Funktioniert in:** deinem eigenen Spiel (Einzelspieler oder die Welt, die du
hostest), einem dedizierten Server

![Der 3D-Editor mit einem laufenden Server verbunden: LIVE, Neu laden und Auto in der oberen Leiste, eine neue Hütte mit grünen Punkten markiert](../images/live.jpg)
*Mit einem laufenden Server verbunden: **LIVE**, **Neu laden** und **Auto** in der oberen Leiste, und eine Hütte, die das
Spiel noch nicht hat („Nicht angewendet: 1 Zone, 40 hinzugefügt“, grüne Punkte auf den neuen Bauteilen).*

Der Live-Modus braucht **BepInEx** und das Plugin **WorldEditorBridge** im Spiel, das die Welt hostet. Spieler, die
beitreten, brauchen keins von beiden. Ein dedizierter Server muss dir außerdem **SSH-Zugang** zu der Maschine geben, auf
der er läuft (eine Shell-Anmeldung, nicht nur SFTP oder den Dateimanager eines Webpanels): die meisten bei einem
Anbieter gemieteten Spielserver bieten das nicht, siehe
[Einen gemieteten oder über ein Panel gehosteten Server bearbeiten](guides/edit-a-rented-server.md). Um ihn
einzurichten und dich zu verbinden, folge
[Die Welt bearbeiten, die du gerade spielst](guides/edit-your-game-live.md) oder
[Einen dedizierten Server bearbeiten, während gespielt wird](guides/edit-a-server-live.md). Wie der Editor mit dem Spiel
spricht, steht unter [Live bearbeiten](concepts/live-editing.md#wie-die-verbindung-funktioniert).

## Benutzen

1. Verbinde dich: Klicke auf der Startseite auf **Live bearbeiten** (dein Spiel) oder auf **Verbinden** neben einem
   gespeicherten Server.
2. Klicke auf der Weltkarte auf eine Stelle und wähle **In 3D bearbeiten**.
3. Bearbeite mit einem beliebigen Werkzeug, wie offline. Neue Objekte tragen einen grünen Punkt; die obere Leiste zählt,
   was nicht angewendet ist.
4. Drücke **Live anwenden** (oder `Ctrl+S`). Die Spieler sehen die Änderung sofort, und das Spiel speichert sie wie
   gewohnt.
5. Um eine Änderung zurückzunehmen, mach sie rückgängig (`Ctrl+Z`) und drücke erneut **Live anwenden**.

## Einstellungen

Der Live-Modus funktioniert wie offline, mit diesen Elementen in der oberen Leiste:

| Element | Was es bewirkt |
|---|---|
| Plakette **LIVE** | Zeigt, dass der Editor mit dem laufenden Spiel verbunden ist. |
| **Neu laden** | Liest die ganze Welt erneut aus dem Spiel. Noch nicht angewendete Änderungen werden nach einer Rückfrage verworfen. Du bleibst, wo du bist. |
| **Auto** | An: jeder Strich, jedes Platzieren und jedes Rückgängigmachen wird sofort angewendet, ohne **Live anwenden** zu drücken. Beim Einschalten wird gesendet, was schon wartet. Bleibt zwischen den Sitzungen erhalten. |
| Zähler der ausstehenden Änderungen | „Nicht angewendet: …“ in Bernstein oder „Alles angewendet“. Die Zeile der Karte lautet „Noch nicht angewendet: …“. |
| **Live anwenden** | Tritt an die Stelle von **In Welt speichern**. Sendet jede ausstehende Änderung an das Spiel. |
| Spielerliste, **Los** | Neben **LIVE**, solange ein Spieler verbunden ist: die verbundenen Spieler mit Namen. Wähle einen, dann **Los** (siehe [Spieler](#spieler)). |

## Live anwenden, Auto und Neu laden

**Live anwenden** sendet den Boden jeder Zone, die du geändert hast, dann die Objekte, die du gelöscht, platziert,
verschoben oder geändert hast, dann die Zonen-Resets. Nur was sich seit dem letzten Anwenden geändert hat, wird
gesendet. Die Reihenfolge und was jeder Teil tut, steht unter
[Was wann gesendet wird](concepts/live-editing.md#was-wann-gesendet-wird).

Die Statusleiste sagt dann, was getan wurde, zum Beispiel „Auf das laufende Spiel angewendet: 2 Zone(n) Boden; …“.
Antwortet das Spiel nicht, wird nichts als angewendet markiert, und das nächste Anwenden sendet es erneut. Es läuft
immer nur ein Anwenden: ein zweiter Druck wartet und sendet nur, was noch aussteht.

![Dieselbe Hütte nach Live anwenden: Alles angewendet, keine grünen Punkte, und die Statusleiste mit dem Ergebnis](../images/live-applied.jpg)
*Nach **Live anwenden**: „Alles angewendet“, die grünen Punkte sind weg, und die Statusleiste meldet „Auf das laufende
Spiel angewendet: …“.*

**Auto** macht aus jeder Änderung ein Anwenden. Es wartet das Ende eines Strichs ab, sodass ein langer Pinselstrich als
ein Anwenden gesendet wird. Solange Auto an ist, ändert [Claude](claude.md) nichts; schalte Auto aus, damit Claude
arbeiten kann.

**Neu laden** brauchst du selten: der offene Bereich folgt dem Spiel von selbst. Nutze es, um den Rest der Welt (die
Karte) aufzufrischen, oder wenn das Plugin zu alt ist, um zu folgen.

Nach jedem Anwenden wird der Verlauf auf der Festplatte aufbewahrt, wie nach einem Speichern: verbinde dich später mit
demselben Spiel, und die Schritte sind zum Rückgängigmachen da (siehe
[Verlauf](concepts/saving-and-history.md#verlauf)).

## Dem Spiel folgen

Solange ein Bereich offen ist, fragt der Editor das Spiel alle 2 Sekunden, was sich in den Zonen dieses Bereichs
geändert hat. Was Spieler dort bauen, entfernen, graben oder pflastern, erscheint im Editor ohne einen Schritt im
Verlauf. Die Statusleiste sagt, was hereinkam, zum Beispiel „Aus dem Spiel: 2 neue(s) Objekt(e), 1 entfernt, der Boden
von 1 Zone(n).“ Eine reine Datenänderung (ein Feuer, das herunterbrennt, eine benutzte Truhe) kommt ohne Meldung
herein.

- **Objekte**, die Spieler ändern, behalten ihren Platz in deinem Verlauf und deiner Auswahl.
- **Deine eigenen Objekte zeigen, sobald sie angewendet sind,** wo und wie das Spiel sie wirklich hat (ein von der
  Physik gesetztes Bauteil, die Lebenspunkte einer Kreatur).
- **Boden**, den Spieler geändert haben, kommt nur dort herein, wo bei dir in dieser Zone keine Änderungen warten. Wo
  doch, bleiben deine erhalten, und **Live anwenden** fragt zuerst **Über die Änderungen des Spiels anwenden**:
  **Anwenden** schreibt deinen Boden über diese Zone (was Spieler dort seitdem getan haben, kann verloren gehen);
  **Nicht jetzt** lässt deine Änderungen ausstehen.
- Das Folgen geschieht nur in der 3D-Ansicht, nie während eines Strichs oder beim Anwenden. Die Karte zeigt die Welt,
  wie sie zuletzt gelesen wurde.

Die Einzelheiten stehen unter [Dem Spiel folgen](concepts/live-editing.md#dem-spiel-folgen).

<!-- picture: live-apply-over.jpg: live world, a 3 × 3 area where you raised ground in one zone and a player then dug in the same zone; Apply live pressed: the "Apply over the game's changes" question with its text naming the zone, and the Apply and Not now buttons, over the 3D view -->

## Spieler

Verbundene Spieler werden an ihrem Ort mit ihrem Namen gezeichnet: blaue Pfosten in der 3D-Ansicht, blaue Punkte auf
der Karte. Sie bewegen sich, wie die Spieler es tun (alle 2 Sekunden in 3D, alle 5 auf der Karte). Wähle in der oberen
Leiste neben **LIVE** einen Spieler in der Liste und drücke **Los**: die Kamera geht zu ihm, und der Bereich wandert zu
ihm, wenn er außerhalb ist.

<!-- picture: live-players.jpg: live world with two players connected, 3D view at about 60 m showing two blue posts with their names over a base, the top bar's player list beside LIVE with a name chosen and the Go button visible -->

## Dinge verschieben

Ein verschobenes Objekt (Verschieben oder Drehen im Werkzeug Auswahl, oder **Ausschneiden** und dann Einfügen) bewegt
sich im Spiel als dasselbe Objekt, wenn sich nur sein Ort und seine Drehung geändert haben. Mods, die Objekte
verfolgen (das Beschriftungsschild einer Truhe, die Truhen eines Lagers, ein Ward), kennen es dann weiterhin, und eine
Truhe behält ihren Inhalt, wie das Spiel ihn hat. Rückgängigmachen bewegt es auf dieselbe Weise zurück.

Das funktioniert nur, wenn kein Spieler den alten und den neuen Ort zugleich geladen hat (die Zonen um jeden Spieler,
etwa 160 m, mehr bei großen Objekten). Andernfalls wird das Objekt an seinem neuen Ort neu erzeugt und vom alten
entfernt, und die Meldung zum Anwenden sagt, wie viele: für andere Mods sind das neue Objekte. **Verschiebe einen Bau,
während niemand in der Nähe ist**, um alles daran zu erhalten.

Ein Objekt, dessen Daten du änderst (im Inspektor des Werkzeugs [Auswahl](select.md) oder mit dem Werkzeug einer Mod),
behält ebenfalls seine Identität im Spiel. Die Teile einer verbundenen Gruppe einer Mod (etwa eines Schiffs) und
Kreaturen werden immer neu erzeugt.

## Rückgängig nach dem Anwenden

Angewendete Änderungen lassen sich noch rückgängig machen: mach sie rückgängig (oder **Entfernen** im Verlauf), dann
erneut **Live anwenden**.

- Ein **gelöschtes** Objekt kommt als Kopie dessen zurück, was es war, als die Welt gelesen wurde (eine Truhe mit ihrem
  damaligen Inhalt). Für das Spiel ist es ein neues Objekt.
- Ein **platziertes** Objekt wird wieder aus dem Spiel entfernt. Hat eine andere Mod es inzwischen unter einer neuen ID
  neu erzeugt, findet das Plugin es über seine Art und seinen Ort (auf 10 cm genau) und entfernt dieses.
- Ein **verschobenes** Objekt wird zurückbewegt, wenn möglich als dasselbe Objekt.
- **Zonen-Resets** lassen sich nach dem Anwenden nicht rückgängig machen.

Schritte aus einer früheren Sitzung sind im Verlauf so markiert. Spieler haben die Welt seitdem vielleicht geändert,
daher wirst du einmal gefragt, bevor du sie rückgängig machst oder wiederholst.

## Gemoddete Spiele

Mods erzeugen die meisten ihrer Objekte, während das Spiel läuft, also lernt der Editor sie vom Spiel, wenn eine
Live-Welt sich öffnet. Die Objekte eines gemoddeten Spiels haben dann ihre Namen, die Liste von
[Platzieren](place.md) bietet sie an, die Leiste [Bauen](build.md), in einer Welt wie in der
[Werkstatt](workshop.md), listet die Bauteile der Mods im Reiter **Mod-Bauteile** mit ihren Kosten (eine Mod nach der
anderen, gewählt in der Liste an seiner Seite; höchstens 300 Bauteile werden gezeigt, suche nach den anderen; die auf
diesem Computer installierten Mods stehen dort auch ohne Live-Spiel, mit unbekannten Kosten), und der Inspektor sagt,
aus welcher Mod ein Objekt stammt („Aus der Mod …“, wenn Jötunn installiert ist, sonst „Aus einer Mod“).

Was der Editor lernt, wird aufbewahrt (`mod-catalogue.json` im Datenordner des Editors), sodass die Welt desselben
Servers, später aus ihren Dateien geöffnet, die Objekte der Mods ebenfalls zeigt. Jede Verbindung ergänzt es.

Die Modelle der Mods werden wie im Spiel gezeichnet, aus den eigenen Dateien der Mods in deinem Valheim-Ordner und
deinen Mod-Manager-Profilen (dort müssen dieselben Mods installiert sein). Beim ersten Mal werden ihre Bundles in den
Datenordner des Editors kopiert (`mod-bundles`, ein paar Sekunden). Meshes, die eine Mod im Code baut, während das Spiel
läuft (zum Beispiel spontan erzeugte Segel), lassen sich nicht finden: diese Bauteile erscheinen ohne Textur. Eine Mod
kann auch eine `worldeditor.json` mitliefern, für das, was der Editor nicht erraten kann: siehe
[Für Mod-Autoren](modding.md).

### Die eigenen Plugins der Mods

Eine Mod, deren Plugin sich bei der Bridge registriert hat, erfährt sofort, was du angewendet hast (erzeugte,
entfernte und verschobene Objekte, Zonen, deren Boden sich geändert hat oder zurückgesetzt wurde), sodass das, was sie
aus ihren Objekten baut (ein Schiff aus seinen Bauteilen), sofort neu zusammengesetzt wird. Das Protokoll des Editors
nennt die registrierten Mods, wenn sich ein Spiel öffnet; eine Mod, die bei einem Anwenden scheitert, wird in
`BepInEx/LogOutput.log` des Spiels genannt, und das Anwenden geht trotzdem durch.

### Die Expand-World-Mods

Mit installiertem und eingeschaltetem [Expand-World-Add-on](../../addons/ExpandWorld/README.md)
(**Einstellungen → Add-ons**) folgt eine Live-Welt den Mods, die das Spiel selbst ausführt: ihre Einstellungen und
Datendateien werden über das Plugin aus dem eigenen `BepInEx`-Ordner des Spiels gelesen, sodass die Welt eines Servers
so gezeichnet wird, wie dieser Server sie erzeugt. Nur die Bibliothek YamlDotNet, die die Datendateien brauchen, kommt
aus einem lokalen BepInEx-Ordner. Die Dateien werden unter `live-bepinex` in den Datenordner des Editors kopiert; das
Protokoll sagt, was gelesen wurde. Mit einem älteren Plugin werden stattdessen deine lokalen Ordner genutzt.

### Riverheim

Eine Live-Welt mit Riverheim wird mit deiner eigenen Kopie von Riverheim gezeichnet: das
[Riverheim-Add-on](../../addons/Riverheim/README.md) muss installiert und eingeschaltet sein, und Riverheim auf diesem
Computer installiert (mit Worldhalla, für Riverheim 1.3 und später). Dein Riverheim muss die Version sein, die ein
Spieler zum Beitreten braucht: seit Riverheim 1.3 dieselbe Version wie die des Spiels; davor dasselbe Release (die
ersten beiden Zahlen der Version). Andernfalls nennt der Editor beide Versionen und öffnet die Welt nicht.

## Plugin-Einstellungen

Die Einstellungen des Plugins stehen in `BepInEx/config/Tie.WorldEditorBridge.cfg` des Spiels oder Servers, beim ersten
Start geschrieben. (Plugins vor 0.41.0 nannten sie `local.worldeditorbridge.cfg`; das Plugin benennt sie um, und der
Editor liest beide.)

| Einstellung | Standard | Was sie bewirkt |
|---|---|---|
| `BindAddress` | `127.0.0.1` | Die Adresse, auf der das Plugin lauscht. Behalte `127.0.0.1`: der Editor erreicht einen Server über seinen eigenen SSH-Tunnel. Eine andere Adresse macht das Plugin in den lokalen Netzwerken und VPNs der Maschine erreichbar (nie im Internet: Anfragen von anderswo werden abgewiesen), mit unverschlüsseltem Token, und das Protokoll warnt davor; siehe [Ohne SSH](#ohne-ssh). `+` lauscht auf jeder Adresse der Maschine: für einen Server in einem Container (ein Spielpanel wie Pterodactyl) siehe [Einen gemieteten oder über ein Panel gehosteten Server bearbeiten](guides/edit-a-rented-server.md#ein-server-in-einem-container). `0.0.0.0` funktioniert nicht: das Plugin weist dann jede Anfrage ab. |
| `Port` | `5182` | Der Port des Plugins. Ändere ihn, wenn ein anderes Programm 5182 nutzt. |
| `Token` | beim ersten Start erzeugt | Das Geheimnis, das der Editor senden muss, in einem Header (nie in der Adresse): 24 zufällige Buchstaben und Ziffern. Wer es hat und Zugriff auf den Port, kann die Welt ändern, also halte es privat. Leere die Zeile und starte neu, um ein neues zu bekommen. |

## Ohne SSH

Der Editor braucht kein SSH, wenn das Spiel auf demselben Computer läuft: er findet das Plugin dort von selbst. Für
einen anderen Computer muss das Plugin statt auf `127.0.0.1` auf einer Adresse lauschen, die dieser Computer erreichen
kann.

**Ein anderer Computer im selben Haus** (dasselbe Heimnetz):

1. Finde auf dem Computer, auf dem das Spiel oder der Server läuft, seine lokale Adresse: `ipconfig` unter Windows
   („IPv4-Adresse“), `ip addr` unter Linux. Sie sieht aus wie `192.168.1.20`.
2. Setze in seiner `BepInEx/config/Tie.WorldEditorBridge.cfg` `BindAddress = 192.168.1.20` (genau diese Adresse, nicht
   `0.0.0.0`) und starte das Spiel oder den Server neu. Sein Protokoll meldet `listening on http://192.168.1.20:5182/`,
   dann eine Warnung, dass das Token unverschlüsselt übertragen wird.
3. Lass den Port durch die Firewall dieses Computers herein, nur in privaten Netzwerken. Unter Windows, in PowerShell
   als Administrator (Windows wendet es nur an, wenn dieses Netzwerk als **Privat** eingestellt ist):

   ```powershell
   New-NetFirewallRule -DisplayName "WorldEditorBridge" -Direction Inbound -Protocol TCP -LocalPort 5182 -Action Allow -Profile Private
   ```

4. Kopiere die Zeile `Token` derselben Datei.
5. Im Editor, auf der Startseite: **Ein dedizierter Server** → **Weitere Optionen** → **Eigener Tunnel vorhanden**. Gib
   `192.168.1.20:5182` als **Bridge-Adresse** ein, füge das Token ein und drücke **Verbinden**.

Das Token und die Änderungen an der Welt gehen unverschlüsselt durchs Netzwerk: jeder im selben WLAN könnte das Token
lesen und die Welt ändern. Zu Hause in Ordnung; nicht in einem geteilten Netzwerk (Wohnheim, Café). Der Router kann
diesem Computer auch eines Tages eine andere Adresse geben: dann lauscht das Plugin nicht mehr, bis `BindAddress`
geändert ist, außer der Router reserviert die Adresse für ihn (eine „DHCP-Reservierung“).

**Ein Computer außer Haus** (der eines Freundes, eine gemietete Maschine): das Plugin weist Anfragen aus dem Internet
ab, da das Token es im Klartext durchqueren würde. Gib diesem Computer entweder einen SSH-Server, den dann der eigene
verschlüsselte Tunnel des Editors nutzt (Windows 10 und 11 haben einen: **Einstellungen** → **Optionale Features** →
**OpenSSH-Server**), oder bring beide Computer in ein VPN (Tailscale, ZeroTier, WireGuard), das den Weg verschlüsselt,
und folge den Schritten oben mit der Adresse des Computers im VPN (bei Tailscale `100.x.y.z`).

**Ein bei einem Anbieter gemieteter oder über ein Spielpanel betriebener Spielserver** (Pterodactyl, Pelican, ein
Docker-Container): siehe [Einen gemieteten oder über ein Panel gehosteten Server bearbeiten](guides/edit-a-rented-server.md).

## Was das Plugin protokolliert

`BepInEx/LogOutput.log` des Spiels oder Servers sagt, was jedes Anwenden getan hat, für den Fall, dass eine Änderung
nicht bei jedem Spieler erscheint. Beim Start meldet es `WorldEditorBridge <version> listening on http://127.0.0.1:5182/ (token in
BepInEx/config/Tie.WorldEditorBridge.cfg)`. Dann, pro Anwenden:

- `WorldEditorBridge: destroyed N object(s) (M found again at their place under a new id, K already gone), created C,
  moved I in place and F by making it again (…); A -> B object(s) known, P peer(s)`: die Objekte. *Found again* sind
  Objekte, die eine andere Mod neu erzeugt hatte, über Art und Ort gefunden. A und B sind die Objekte, die das Spiel
  vorher und nachher kannte (zerstörte verschwinden beim nächsten Update des Spiels, daher zählt B sie noch); P sind die
  verbundenen Spieler.
- `WorldEditorBridge: 1 s after the apply: S of N destroyed object(s) still exist, R other object(s) of their prefab
  within 10 cm of their place (…)`: eine Sekunde später. S über 0 heißt, dass etwas auf dem Server (höchstwahrscheinlich
  eine Mod) ein zerstörtes Objekt neu erzeugt oder behalten hat. R zählt Objekte derselben Art, die dort stehen, wo ein
  zerstörtes stand: von einer Mod neu erzeugt oder schon immer da. Beide auf 0 heißt, dass der Server seinen Teil
  getan hat, und was ein Spieler noch sieht, ist das eigene Spiel dieses Spielers.
- `WorldEditorBridge: applied terrain to N zone(s) (C new)`: der Boden. *New* zählt Zonen, die vorher keine
  Bodenänderungen hatten.
- `WorldEditorBridge: reset N zone(s): D object(s) removed, L location(s) to place again`: Zonen-Resets.
- `WorldEditorBridge: caught at <file>:<line> (<member>) while <doing>: <error type>: <message>`: ein Fehler, den das
  Plugin behandelt hat, mit seinem Stacktrace, außer es ist ein erwarteter. Der Home-Ordner des Servers wird `~`
  geschrieben.

## Tastenkürzel

| Taste | Aktion |
|---|---|
| `Ctrl+S` | **Live anwenden** |
| `Ctrl+Z`, `Ctrl+Y` | Rückgängig, wiederholen (dann sendet **Live anwenden** es, oder **Auto** sofort) |

Jede andere Taste funktioniert wie offline: siehe [Tastenkürzel](help/shortcuts.md).

## Tipps und Grenzen

- Das Plugin funktioniert nur in einem Spiel, das die Welt **hostet** (ein Server, Einzelspieler oder der Host). In
  einem Spiel, das dem Server eines anderen beigetreten ist, weigert es sich, etwas zu ändern.
- **Zonen-Resets** geschehen sofort, wo ein Spieler in der Nähe ist, sonst beim nächsten Mal, wenn jemand kommt. Nach
  dem Anwenden lassen sie sich nicht rückgängig machen.
- Live platzierte Objekte werden vom Server erzeugt; Spieler sehen sie wie Objekte, die jemand gerade gebaut oder
  gepflanzt hat.
- Tausende Objekte auf einmal werden in einem Zug angewendet und können den Server kurz ruckeln lassen. Das Lesen einer
  großen Welt beim Verbinden kann ein paar Minuten dauern.
- Antwortet das Spiel nicht innerhalb von 30 Sekunden (es lädt oder ist sehr beschäftigt), wird nichts von dieser
  Anfrage ausgeführt: wende erneut an.
- Nutze das Plugin aus demselben Release wie deinen Editor. Mit einem älteren Plugin sagt der Editor, was fehlt. Die
  Funktionen, die ein neueres Plugin brauchen:

| Plugin-Version | Nötig für |
|---|---|
| 0.10.0 | Zonen-Resets |
| 1.17.0 | Dem Spiel folgen |
| 1.26.0 | Objekte der Mods und ihre Namen |
| 1.27.0 | Modelle der Mods |
| 1.35.0 | Eigene Plugins der Mods erfahren, was angewendet wurde |
| 1.40.0 | Verschieben unter Beibehaltung desselben Objekts |
| 1.45.3 | Ein Anwenden, dessen Antwort verloren ging (ein abgebrochener Tunnel), erzeugt beim erneuten Senden keine doppelten Objekte |
| 1.54.0 | Die Dateien der Expand-World-Mods und eine Riverheim-Welt aus dem Spiel gelesen |
| 1.57.3 | Kreaturen, Schiffe und andere bewegliche Dinge werden beim Bewegen verfolgt |
| 1.97.0 | Geänderte Objekte behalten ihre Identität |

## Verwandte Seiten

- [Die Welt bearbeiten, die du gerade spielst](guides/edit-your-game-live.md)
- [Einen dedizierten Server bearbeiten, während gespielt wird](guides/edit-a-server-live.md)
- [Live bearbeiten](concepts/live-editing.md): wie die Verbindung entsteht, was gesendet wird, mehrere Editoren und
  Spieler
- [Fehlerbehebung](help/troubleshooting.md#ein-server-verbindet-sich-nicht): wenn keine Verbindung zustande kommt
