<!-- source: 94a46b5b -->
# I-edit ang inuupahang server o server sa panel

I-edit ang mundo ng server na inuupahan mo mula sa game host, o ng isang pinapatakbo ng game panel gaya ng Pterodactyl o
Pelican sa loob ng container sa sarili mong makina.

**Kailangan ng live na pag-edit ang SSH access sa makinang nagpapatakbo ng server**: login sa shell nito, hindi lang SFTP o
file manager. Inaabot ng editor ang plugin sa pamamagitan ng SSH, sa sariling `127.0.0.1` ng server (tingnan ang
[I-edit ang dedicated server habang may naglalaro](edit-a-server-live.md)). Bago ka umupa ng server para i-edit ito
nang live, tanungin ang host kung kasama sa plan ang SSH access. Dalawang uri ng server ang hindi pasok doon:

- **Game server na inuupahan mula sa host** (may web panel ka at file manager o FTP/SFTP, pero walang SSH access sa
  makina): hindi maaabot ng editor ang plugin doon. I-edit na lang ang mga file ng mundo: tingnan ang
  [Inuupahang game server](#inuupahang-game-server).
- **Game panel sa sarili mong makina** (Pterodactyl, Pelican, o anumang server na tumatakbo sa Docker container):
  gumagana ang live na pag-edit, sa pamamagitan ng tunnel na ikaw mismo ang nagbubukas. Tingnan ang
  [Server sa loob ng container](#server-sa-loob-ng-container).

Kung bibigyan ka ng host mo ng tunay na SSH login sa makinang nagpapatakbo ng laro (inuupahang VPS o dedicated na
makina, hindi lang game server), sundin na lang ang [I-edit ang dedicated server habang may naglalaro](edit-a-server-live.md).

## Inuupahang game server

**Kailangan mo:** ang panel o file access ng host (ang file manager nito, FTP o SFTP) at karapatang ihinto at simulan
ang server.

Hindi posible ang live mode: sa makina mismo lang sumasagot ang plugin, at walang daang ibinibigay ang host papasok.
Mga file lang ang ibinibigay ng SFTP login ng panel (madalas sa port 2022): hindi makapagbubukas ng tunnel dito ang SSH
ng editor. I-edit na lang ang naka-save na mundo:

1. Sa panel ng host, **ihinto** ang server. Sine-save ng tumatakbong server ang mga file sa ibabaw ng mga ibinalik mo.
2. Gamit ang file manager ng panel o isang SFTP program (FileZilla, WinSCP), i-download ang folder ng mundo,
   `worlds_local/<World>` sa save folder ng server. Magtabi ng kopya nito nang hindi ginagalaw: iyon ang backup mo.
3. Sa editor, buksan ang folder na iyon: **Isang naka-save na mundo** → **Ibang folder ng mundo** → **Mag-browse…**
   (tingnan ang [Magbukas ng mundong wala sa listahan](open-a-world.md)).
4. Gawin ang mga pagbabago mo at pindutin ang **I-save sa mundo**.
5. I-upload ulit ang folder sa parehong lugar, palitan ang mga file doon, saka simulan ang server.

Dapat manatiling nakahinto ang server mula hakbang 1 hanggang hakbang 5: nawawala ang ginawa ng mga player sa pagitan
kapag nag-upload ka.

## Server sa loob ng container

**Kailangan mo:** SSH login sa makinang pinagtatakbuhan ng panel (ang VPS mo, bilang root o user na pinapayagang
magpatakbo ng `docker`), at karapatang i-edit ang mga file ng game server sa panel.

Pinapatakbo ng panel ang bawat game server sa sarili nitong Docker container, na may sariling `127.0.0.1`. Doon
nakikinig ang plugin, sa loob ng container, habang ang sariling tunnel ng editor ay umaabot sa `127.0.0.1` ng makina,
kung saan walang sumasagot: sinasabi ng editor na hindi sumasagot ang plugin, kahit tumatakbo ang server kasama nito.
Ang daan papasok ay tunnel papunta sa sariling address ng container:

```text
 your computer               your VPS          the server's container
┌───────────────────────┐   ┌───────────┐     ┌───────────────────────┐
│ editor                │SSH│           │     │ WorldEditorBridge     │
│ 127.0.0.1:15182 ══════╪═══╪═► sshd ───┼────►│ 172.18.0.x:5182       │
└───────────────────────┘   └───────────┘     └───────────────────────┘
```

1. Sa panel, i-install ang BepInEx at WorldEditorBridge sa server, gaya sa anumang dedicated server (hakbang 1 hanggang
   3 ng [I-edit ang dedicated server habang may naglalaro](edit-a-server-live.md)), at simulan ito nang isang beses.
2. Sa file manager ng panel, buksan ang `BepInEx/config/Tie.WorldEditorBridge.cfg`. Itakda ang `BindAddress = +` at
   kopyahin ang value ng linyang `Token`.
3. I-restart ang server. Sinasabi ng console nito o ng `BepInEx/LogOutput.log` ang `WorldEditorBridge <version>
   listening on http://+:5182/`, saka isang babala na hindi naka-encrypt ang pagdaan ng token: sa loob ng container,
   ang sariling network lang iyon ng panel.
4. Mag-log in sa VPS gamit ang SSH at hanapin ang address ng container. Ipinangalan ang container sa id ng server
   (ipinapakita ito ng panel sa mga setting ng server); inililista lahat ng `docker ps`:

   ```sh
   docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' <server id>
   ```

   Sumasagot ito ng address gaya ng `172.18.0.5`.
5. Sa computer mo, magbukas ng tunnel papunta rito at iwanan itong bukas habang nag-e-edit ka (palitan ang address at
   ang login):

   ```sh
   ssh -N -L 15182:172.18.0.5:5182 user@your-vps
   ```

   Sa PuTTY: **Connection** → **SSH** → **Tunnels**, **Source port** `15182`, **Destination** `172.18.0.5:5182`,
   **Add**, saka buksan ang session.
6. Sa panimulang pahina ng editor: **Isang dedicated server** → **Iba pang opsiyon** → **Gumawa ako ng sariling
   tunnel**. I-type ang `127.0.0.1:15182` bilang **Address ng bridge**, i-paste ang token, at pindutin ang
   **Kumonekta**.

Saka mag-edit gaya sa anumang server: tingnan ang [Live mode](../live-mode.md#gamitin-ito).

**Tandaan:**

- `+`, hindi ang address ng container o `0.0.0.0`: kapag address ng container, tinatanggihan ng plugin ang bawat
  request na dumaraan sa tunnel, at kapag `0.0.0.0`, ang bawat request ("The server answered with an error (400)").
- Huwag idagdag ang port ng plugin (5182) sa mga port ng server (allocations) sa panel: binubuksan nito iyon sa
  internet.
- Maaaring magbago ang address ng container kapag ni-rebuild o ni-reinstall ang server. Kung huminto sa pagsagot ang
  tunnel, hanapin ulit ito (hakbang 4).
- Panatilihin ang `BindAddress = 127.0.0.1` sa server na hindi tumatakbo sa container: doon, pinapakinig ng `+` ang
  plugin sa bawat network na kinaroroonan ng makina (tinatanggihan pa rin ang mga tumatawag mula sa internet).

## Tingnan kung gumana

- **Inuupahang game server:** nagsisimula ang server, at nandoon sa laro ang mga pagbabago mo.
- **Server sa loob ng container:** ipinapakita ng top bar ang **LIVE**, at pagkatapos ng **I-apply nang live**,
  "Na-apply lahat".

Kung may mensaheng ipinapakita ang editor sa halip, tingnan ang [Hindi makakonekta ang server](../help/troubleshooting.md#hindi-kumokonekta-ang-server).

## I-undo ito

- **Inuupahang game server:** ihinto ang server at i-upload ang kopyang itinabi mo sa hakbang 2.
- **Server sa loob ng container:** `Ctrl+Z`, saka **I-apply nang live** ulit. Para isara ulit ang plugin, itakda ang `BindAddress =
  127.0.0.1` at i-restart ang server.

## Kaugnay

- [I-edit ang dedicated server habang may naglalaro](edit-a-server-live.md)
- [Mga setting ng plugin](../live-mode.md#mga-setting-ng-plugin): `BindAddress`, `Port`, `Token`
- [Magbukas ng mundong wala sa listahan](open-a-world.md)
- [Mag-backup ng mundo bago mag-edit](back-up-a-world.md)
