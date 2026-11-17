<!-- source: 94a46b5b -->
# Einen gemieteten oder über ein Panel gehosteten Server bearbeiten

Bearbeite die Welt eines Servers, den du bei einem Spiele-Hoster mietest, oder eines Servers, den ein Spiele-Panel wie
Pterodactyl oder Pelican in einem Container auf deiner eigenen Maschine ausführt.

**Live-Bearbeitung braucht SSH-Zugang zu der Maschine, auf der der Server läuft**: eine Anmeldung an ihrer Shell, nicht
nur SFTP oder einen Dateimanager. Der Editor erreicht das Plugin über SSH, auf der eigenen Adresse `127.0.0.1` des
Servers (siehe [Einen dedizierten Server bearbeiten, während gespielt wird](edit-a-server-live.md)). Bevor du einen
Server mietest, um ihn live zu bearbeiten, frag den Hoster, ob der Tarif SSH-Zugang enthält. Zwei Arten von Servern
passen nicht dazu:

- **Ein bei einem Hoster gemieteter Spieleserver** (du bekommst ein Web-Panel und einen Dateimanager oder FTP/SFTP,
  aber keinen SSH-Zugang zur Maschine): Der Editor kann das Plugin dort nicht erreichen. Bearbeite stattdessen die
  Dateien der Welt: siehe [Ein gemieteter Spieleserver](#ein-gemieteter-spieleserver).
- **Ein Spiele-Panel auf deiner eigenen Maschine** (Pterodactyl, Pelican oder jeder Server, der in einem
  Docker-Container läuft): Live-Bearbeitung funktioniert, über einen Tunnel, den du selbst öffnest. Siehe
  [Ein Server in einem Container](#ein-server-in-einem-container).

Wenn dein Hoster dir eine echte SSH-Anmeldung auf der Maschine gibt, auf der das Spiel läuft (ein gemieteter VPS oder
eine dedizierte Maschine, nicht nur ein Spieleserver), folge stattdessen
[Einen dedizierten Server bearbeiten, während gespielt wird](edit-a-server-live.md).

## Ein gemieteter Spieleserver

**Du brauchst:** das Panel des Hosters oder Dateizugriff (seinen Dateimanager, FTP oder SFTP) und das Recht, den
Server zu stoppen und zu starten.

Der Live-Modus ist nicht möglich: Das Plugin antwortet nur auf der Maschine selbst, und der Hoster bietet keinen Weg
hinein. Die SFTP-Anmeldung eines Panels (oft auf Port 2022) gibt nur Dateien heraus: Das SSH des Editors kann dort
keinen Tunnel öffnen. Bearbeite stattdessen die gespeicherte Welt:

1. **Stoppe** den Server im Panel des Hosters. Ein laufender Server speichert über die Dateien, die du zurücklegst.
2. Lade mit dem Dateimanager des Panels oder einem SFTP-Programm (FileZilla, WinSCP) den Ordner der Welt herunter,
   `worlds_local/<World>` im Speicherordner des Servers. Bewahre eine unveränderte Kopie davon auf: Sie ist deine
   Sicherung.
3. Öffne diesen Ordner im Editor: **Eine gespeicherte Welt** → **Anderer Weltordner** → **Durchsuchen…** (siehe
   [Eine Welt öffnen, die nicht in der Liste steht](open-a-world.md)).
4. Nimm deine Änderungen vor und drücke **In Welt speichern**.
5. Lade den Ordner wieder an dieselbe Stelle hoch, ersetze die Dateien dort und starte dann den Server.

Der Server muss von Schritt 1 bis Schritt 5 gestoppt bleiben: Was Spieler dazwischen tun, geht beim Hochladen
verloren.

## Ein Server in einem Container

**Du brauchst:** eine SSH-Anmeldung auf der Maschine, auf der das Panel läuft (dein VPS, als root oder als Benutzer,
der `docker` ausführen darf), und das Recht, die Dateien des Spieleservers im Panel zu bearbeiten.

Ein Panel führt jeden Spieleserver in einem eigenen Docker-Container aus, der seine eigene Adresse `127.0.0.1` hat. Das
Plugin lauscht dort, im Container, während der eigene Tunnel des Editors die Adresse `127.0.0.1` der Maschine
erreicht, wo nichts antwortet: Der Editor meldet, dass das Plugin nicht antwortet, obwohl der Server damit läuft. Der
Weg hinein ist ein Tunnel zur eigenen Adresse des Containers:

```text
 dein Computer               dein VPS          Container des Servers
┌───────────────────────┐   ┌───────────┐     ┌───────────────────────┐
│ Editor                │SSH│           │     │ WorldEditorBridge     │
│ 127.0.0.1:15182 ══════╪═══╪═► sshd ───┼────►│ 172.18.0.x:5182       │
└───────────────────────┘   └───────────┘     └───────────────────────┘
```

1. Installiere im Panel BepInEx und WorldEditorBridge auf dem Server, wie bei jedem dedizierten Server (Schritte 1
   bis 3 von [Einen dedizierten Server bearbeiten, während gespielt wird](edit-a-server-live.md)), und starte ihn
   einmal.
2. Öffne im Dateimanager des Panels `BepInEx/config/Tie.WorldEditorBridge.cfg`. Setze `BindAddress = +` und kopiere
   den Wert der Zeile `Token`.
3. Starte den Server neu. Seine Konsole oder `BepInEx/LogOutput.log` meldet `WorldEditorBridge <version> listening on
   http://+:5182/`, dann eine Warnung, dass das Token unverschlüsselt übertragen wird: Im Container ist das nur das
   eigene Netzwerk des Panels.
4. Melde dich per SSH am VPS an und finde die Adresse des Containers. Der Container ist nach der ID des Servers
   benannt (das Panel zeigt sie in den Einstellungen des Servers); `docker ps` listet alle auf:

   ```sh
   docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' <server id>
   ```

   Die Antwort ist eine Adresse wie `172.18.0.5`.
5. Öffne auf deinem Computer einen Tunnel dorthin und lass ihn offen, während du bearbeitest (ersetze die Adresse und
   die Anmeldung):

   ```sh
   ssh -N -L 15182:172.18.0.5:5182 user@your-vps
   ```

   Mit PuTTY: **Connection** → **SSH** → **Tunnels**, **Source port** `15182`, **Destination** `172.18.0.5:5182`,
   **Add**, dann die Sitzung öffnen.
6. Auf der Startseite des Editors: **Ein dedizierter Server** → **Weitere Optionen** → **Eigener Tunnel vorhanden**.
   Gib `127.0.0.1:15182` als **Bridge-Adresse** ein, füge das Token ein und drücke **Verbinden**.

Bearbeite dann wie auf jedem Server: siehe [Live-Modus](../live-mode.md#benutzen).

**Denk daran:**

- `+`, nicht die Adresse des Containers und nicht `0.0.0.0`: Mit der Adresse des Containers lehnt das Plugin jede
  Anfrage ab, die durch einen Tunnel kommt, und mit `0.0.0.0` jede Anfrage („The server answered with an
  error (400)“).
- Füge den Port des Plugins (5182) im Panel nicht zu den Ports (Allocations) des Servers hinzu: Das öffnet ihn zum
  Internet.
- Die Adresse des Containers kann sich ändern, wenn der Server neu gebaut oder neu installiert wird. Wenn der Tunnel
  nicht mehr antwortet, schlag sie erneut nach (Schritt 4).
- Behalte `BindAddress = 127.0.0.1` auf einem Server, der nicht in einem Container läuft: Dort lässt `+` das Plugin
  in jedem Netzwerk lauschen, in dem die Maschine ist (Anfragen aus dem Internet werden trotzdem abgelehnt).

## Prüfen, ob es geklappt hat

- **Ein gemieteter Spieleserver:** Der Server startet, und deine Änderungen sind im Spiel da.
- **Ein Server in einem Container:** Die obere Leiste zeigt **LIVE**, und nach **Live anwenden** „Alles angewendet“.

Wenn der Editor stattdessen eine Meldung zeigt, siehe [Ein Server verbindet sich nicht](../help/troubleshooting.md#ein-server-verbindet-sich-nicht).

## Rückgängig machen

- **Ein gemieteter Spieleserver:** Stoppe den Server und lade die Kopie hoch, die du in Schritt 2 aufbewahrt hast.
- **Ein Server in einem Container:** `Ctrl+Z`, dann erneut **Live anwenden**. Um das Plugin wieder zu schließen,
  setze `BindAddress = 127.0.0.1` und starte den Server neu.

## Verwandte Seiten

- [Einen dedizierten Server bearbeiten, während gespielt wird](edit-a-server-live.md)
- [Einstellungen des Plugins](../live-mode.md#plugin-einstellungen): `BindAddress`, `Port`, `Token`
- [Eine Welt öffnen, die nicht in der Liste steht](open-a-world.md)
- [Eine Welt vor dem Bearbeiten sichern](back-up-a-world.md)
