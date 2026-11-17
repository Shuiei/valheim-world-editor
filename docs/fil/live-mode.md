<!-- source: e9428d01 -->
# Live mode

I-edit ang mundo ng tumatakbong laro o dedicated server habang may naglalaro: nakikita nila ang bawat pagbabago sa
sandaling i-apply mo ito.

**Saan:** panimulang pahina, **I-edit nang live** sa **Home** o **Ang laro ko**, o **Isang dedicated server** ·
**Key:** `Ctrl+S` ang nag-a-apply · **Gumagana sa:** sarili mong laro (single player o ang mundong hino-host mo), isang
dedicated server

![Ang 3D editor na nakakonekta sa tumatakbong server: LIVE, I-reload at Auto sa top bar, isang bagong kubo na may markang berdeng tuldok](../images/live.jpg)
*Nakakonekta sa tumatakbong server: **LIVE**, **I-reload** at **Auto** sa top bar, at isang kubong wala pa sa laro
("Hindi na-apply: 1 zone, 40 idinagdag", mga berdeng tuldok sa mga bagong piece).*

Kailangan ng live mode ang **BepInEx** at ang plugin na **WorldEditorBridge** sa larong nagho-host ng mundo. Hindi
kailangan ng mga player na sumasali ang alinman sa dalawa. Dapat ka ring bigyan ng dedicated server ng **SSH access** sa
makinang pinapatakbuhan nito (shell login, hindi lang SFTP o file manager ng web panel): hindi ito ibinibigay ng
karamihan sa mga game server na inuupahan sa isang host, tingnan ang
[I-edit ang inuupahang server o server sa panel](guides/edit-a-rented-server.md). Para i-set up ito at kumonekta,
sundan ang [I-edit ang mundong nilalaro mo](guides/edit-your-game-live.md) o
[I-edit ang dedicated server habang may naglalaro](guides/edit-a-server-live.md). Nasa
[Live na pag-edit](concepts/live-editing.md#paano-ito-kumokonekta) kung paano nakikipag-usap ang editor sa laro.

## Gamitin ito

1. Kumonekta: sa panimulang pahina, i-click ang **I-edit nang live** (ang laro mo) o **Kumonekta** sa tabi ng isang
   naka-save na server.
2. Sa mapa ng mundo, mag-click ng lugar at piliin ang **I-edit sa 3D**.
3. Mag-edit gamit ang kahit anong tool, gaya ng gagawin mo offline. May berdeng tuldok ang mga bagong object; binibilang
   ng top bar ang hindi pa na-apply.
4. Pindutin ang **I-apply nang live** (o `Ctrl+S`). Nakikita agad ng mga player ang pagbabago, at sine-save ito ng laro
   gaya ng dati.
5. Para bawiin ang isang pagbabago, i-undo ito (`Ctrl+Z`) at pindutin ulit ang **I-apply nang live**.

## Mga setting

Gumagana ang live mode gaya ng offline, na may mga kontrol na ito sa top bar:

| Kontrol | Ang ginagawa nito |
|---|---|
| Badge na **LIVE** | Ipinapakitang nakakonekta ang editor sa tumatakbong laro. |
| **I-reload** | Binabasa ulit ang buong mundo mula sa laro. Itinatapon ang mga pagbabagong hindi pa na-apply, matapos magtanong. Mananatili ka kung nasaan ka. |
| **Auto** | On: agad na ina-apply ang bawat stroke, paglalagay at undo, nang hindi pinipindot ang **I-apply nang live**. Kapag in-on ito, ipinapadala ang naghihintay na. Natatandaan sa pagitan ng mga paggamit. |
| Bilang ng nakabinbin | "Hindi na-apply: …" sa kulay amber, o "Na-apply lahat". Ang linya ng mapa ay "Hindi pa na-apply: …". |
| **I-apply nang live** | Pumapalit sa **I-save sa mundo**. Ipinapadala sa laro ang bawat nakabinbing pagbabago. |
| Listahan ng player, **Pumunta** | Sa tabi ng **LIVE**, habang may nakakonektang player: ang mga nakakonektang player ayon sa pangalan. Pumili ng isa, saka **Pumunta** (tingnan ang [Mga player](#mga-player)). |

## I-apply nang live, Auto at I-reload

Ipinapadala ng **I-apply nang live** ang lupa ng bawat zone na binago mo, saka ang mga object na binura, inilagay,
inilipat o binago mo, saka ang mga reset ng zone. Ang nagbago lang mula sa huling pag-apply ang ipinapadala. Nasa
[Ang ipinapadala at kailan](concepts/live-editing.md#ano-ang-ipinapadala-at-kailan) ang pagkakasunod at ang ginagawa ng bawat
bahagi.

Pagkatapos, sinasabi ng status bar ang nagawa, halimbawa "Na-apply sa tumatakbong laro: 2 zone ng lupa; …". Kung
hindi sumasagot ang laro, walang minamarkahang na-apply at ipapadala ulit ito ng susunod na pag-apply. Isang pag-apply
lang ang tumatakbo bawat pagkakataon: naghihintay ang ikalawang pindot at ang nakabinbin pa lang ang ipinapadala.

![Ang parehong kubo pagkatapos ng I-apply nang live: Na-apply lahat, walang berdeng tuldok, at ang status bar na may resulta ng pag-apply](../images/live-applied.jpg)
*Pagkatapos ng **I-apply nang live**: "Na-apply lahat", wala na ang mga berdeng tuldok, at sinasabi ng status bar ang
"Na-apply sa tumatakbong laro: …".*

Ginagawang pag-apply ng **Auto** ang bawat pagbabago. Hinihintay nito ang dulo ng isang stroke, kaya isang pag-apply
lang ang mahabang stroke ng brush. Habang naka-on ang Auto, walang binabago si [Claude](claude.md); i-off ang Auto para
makapagtrabaho siya.

Bihirang kailanganin ang **I-reload**: kusang sinusundan ng bukas na area ang laro. Gamitin ito para i-refresh ang iba
pang bahagi ng mundo (ang mapa), o kapag masyadong luma ang plugin para sumunod.

Pagkatapos ng bawat pag-apply, iniingatan sa disk ang kasaysayan, gaya ng pagkatapos mag-save: kumonekta sa parehong
laro sa ibang pagkakataon at naroon ang mga hakbang para i-undo (tingnan ang
[Kasaysayan](concepts/saving-and-history.md#kasaysayan)).

## Pagsunod sa laro

Habang bukas ang isang area, tinatanong ng editor ang laro bawat 2 segundo kung ano ang nagbago sa mga zone ng area na
iyon. Lumalabas sa editor ang itinatayo, tinatanggal, hinuhukay o sinesementuhan ng mga player doon nang walang hakbang
sa Kasaysayan. Sinasabi ng status bar ang dumating, halimbawa "Mula sa laro: 2 bagong object, 1 inalis, ang lupa ng
1 zone." Dumarating nang walang salita ang pagbabago ng data lang (apoy na nauupos, chest na ginamit).

- **Ang mga object** na binabago ng mga player ay nananatili sa puwesto nila sa Kasaysayan mo at sa pinili mo.
- **Ang sarili mong mga object, kapag na-apply na,** ay lumalabas kung saan at paano talaga sila nasa laro (piece na
  inayos ng physics, health ng creature).
- **Ang lupang** binago ng mga player ay dumarating lang kung saan wala kang naghihintay na pagbabago sa zone na iyon.
  Kung mayroon, pinapanatili ang sa iyo, at nagtatanong muna ang **I-apply nang live** ng **I-apply sa ibabaw ng mga
  pagbabago ng laro**: isinusulat ng **I-apply** ang lupa mo sa ibabaw ng zone na iyon (maaaring mabura ang ginawa roon
  ng mga player mula noon); pinapanatiling nakabinbin ng **Hindi ngayon** ang mga pagbabago mo.
- Sa 3D na tanaw lang nangyayari ang pagsunod, hindi kailanman habang may stroke o habang nag-a-apply. Ipinapakita ng
  mapa ang mundo kung paano ito huling nabasa.

Nasa [Pagsunod sa laro](concepts/live-editing.md#pagsunod-sa-laro) ang mga detalye.

<!-- picture: live-apply-over.jpg: live world, a 3 × 3 area where you raised ground in one zone and a player then dug in the same zone; Apply live pressed: the "Apply over the game's changes" question with its text naming the zone, and the Apply and Not now buttons, over the 3D view -->

## Mga player

Iginuguhit ang mga nakakonektang player sa puwesto nila kasama ang pangalan nila: mga asul na poste sa 3D na tanaw, mga
asul na tuldok sa mapa. Gumagalaw sila kasabay ng mga player (bawat 2 segundo sa 3D, bawat 5 sa mapa). Sa top bar, sa
tabi ng **LIVE**, pumili ng player sa listahan at pindutin ang **Pumunta**: pupunta sa kanila ang camera, at lilipat sa
kanila ang area kapag nasa labas nito sila.

<!-- picture: live-players.jpg: live world with two players connected, 3D view at about 60 m showing two blue posts with their names over a base, the top bar's player list beside LIVE with a name chosen and the Go button visible -->

## Paglilipat ng mga bagay

Ang inilipat na object (paglipat o pag-ikot ng tool na Pagpili, o **Gupitin** saka i-paste) ay lumilipat sa laro bilang
parehong object, kapag puwesto at ikot lang nito ang nagbago. Kilala pa rin ito ng mga mod na sumusubaybay sa mga object
(karatulang label ng chest, mga chest ng warehouse, isang Ward), at pinapanatili ng chest ang laman nito gaya ng nasa
laro. Ibinabalik ito ng undo sa parehong paraan.

Gumagana lang ito kapag walang player na may naka-load na lumang puwesto at bagong puwesto nang sabay (ang mga zone sa
paligid ng bawat player, mga 160 m, higit pa para sa malalaking object). Kung hindi, ginagawang muli ang object sa bago
nitong puwesto at inaalis sa luma, at sinasabi ng mensahe ng pag-apply kung ilan: para sa ibang mga mod, mga bagong
object ang mga iyon. **Ilipat ang gusali habang walang malapit dito** para mapanatili ang lahat tungkol dito.

Ang object na binago mo ang data (sa Inspector ng tool na [Pagpili](select.md), o gamit ang tool ng isang mod) ay
pinapanatili rin ang pagkakakilanlan nito sa laro. Laging ginagawang muli ang mga bahagi ng magkakabit na grupo ng mod
(gaya ng barko) at ang mga creature.

## Pag-undo pagkatapos mag-apply

Maaari pa ring i-undo ang mga na-apply na pagbabago: i-undo (o **Tanggalin** sa Kasaysayan), saka **I-apply nang live**
ulit.

- Bumabalik ang **binurang** object bilang kopya ng kung ano ito noong nabasa ang mundo (chest na may laman gaya noon).
  Para sa laro, bagong object ito.
- Tinatanggal ulit sa laro ang **inilagay** na object. Kung ginawa itong muli ng ibang mod sa bagong id sa pagitan,
  hinahanap ito ng plugin ayon sa uri at puwesto nito (sa loob ng 10 cm) at iyon ang tinatanggal.
- Bumabalik ang **inilipat** na object, bilang parehong object kung kaya.
- Hindi na maa-undo ang **mga reset ng zone** kapag na-apply na.

Minamarkahan nang ganoon sa Kasaysayan ang mga hakbang mula sa naunang session. Maaaring binago na ng mga player ang
mundo mula noon, kaya tatanungin ka nang isang beses bago i-undo o i-redo ang mga ito.

## Mga larong may mod

Ginagawa ng mga mod ang karamihan ng mga object nila habang tumatakbo ang laro, kaya natututunan sila ng editor mula sa
laro kapag nagbukas ang isang live na mundo. Pagkatapos, may pangalan ang mga object ng larong may mod, iniaalok sila ng
listahan ng [Paglagay](place.md), inililista ng dock ng [Pagtayo](build.md), sa mundo man o sa [Gawaan](workshop.md),
ang mga piece ng mga mod sa tab nitong **Mga mod piece** kasama ang gastos nila (isang mod bawat pagkakataon, pinili sa
listahan sa gilid nito; hanggang 300 piece lang ang lumalabas, hanapin ang iba; nakalista roon ang mga mod na
naka-install sa computer na ito kahit walang live na laro, hindi alam ang gastos nila), at sinasabi ng Inspector kung
saang mod galing ang isang object ("Mula sa mod na …" kapag naka-install ang Jötunn, kung hindi ay "Mula sa isang mod").

Iniingatan ang natututunan ng editor (`mod-catalogue.json` sa data folder ng editor), kaya ipinapakita rin ang mga
object ng mga mod sa mundo ng parehong server na binuksan mula sa mga file nito sa ibang pagkakataon. Nagdadagdag dito
ang bawat koneksiyon.

Iginuguhit ang mga model ng mga mod gaya ng sa laro, mula sa sariling mga file ng mga mod sa folder ng Valheim mo at sa
mga profile ng mod manager (dapat naka-install doon ang parehong mga mod). Sa unang pagkakataon, kinokopya ang mga
bundle nila sa data folder ng editor (`mod-bundles`, ilang segundo). Hindi mahahanap ang mga mesh na binubuo ng mod sa
code habang tumatakbo ang laro (mga layag na ginagawa habang naglalaro, halimbawa): walang texture ang mga piece na iyon.
Maaari ring magsama ang mod ng `worldeditor.json` para sa hindi mahulaan ng editor: tingnan ang
[Para sa mga gumagawa ng mod](modding.md).

### Sariling mga plugin ng mga mod

Agad na sinasabihan ang mod na ang plugin ay nagparehistro sa bridge kung ano ang in-apply mo (mga object na ginawa,
tinanggal at inilipat, mga zone na nagbago o na-reset ang lupa), kaya agad na binubuo ulit ang binubuo nito mula sa mga
object nito (barko mula sa mga piece nito). Pinapangalanan ng log ng editor ang mga mod na nakarehistro kapag nagbukas
ang laro; pinapangalanan sa `BepInEx/LogOutput.log` ng laro ang mod na pumalya sa isang pag-apply, at tuloy pa rin ang
pag-apply.

### Ang mga mod na Expand World

Kapag naka-install at naka-on ang [add-on na Expand World](../../addons/ExpandWorld/README.md) (**Mga setting → Mga
add-on**), sinusundan ng live na mundo ang mga mod na mismong pinapatakbo ng laro: binabasa ang mga setting at data file
nila mula sa sariling `BepInEx` folder ng laro sa pamamagitan ng plugin, kaya iginuguhit ang mundo ng isang server kung
paano ito ginagawa ng server na iyon. Ang library na YamlDotNet lang na kailangan ng mga data file ang galing sa isang
lokal na BepInEx folder. Kinokopya ang mga file sa ilalim ng `live-bepinex` sa data folder ng editor; sinasabi ng log
ang nabasa. Sa mas lumang plugin, ang mga lokal na folder mo ang ginagamit sa halip.

### Riverheim

Iginuguhit ang live na mundo ng Riverheim gamit ang sarili mong kopya ng Riverheim: dapat naka-install at naka-on ang
[add-on na Riverheim](../../addons/Riverheim/README.md), at naka-install ang Riverheim sa computer na ito (kasama ang
Worldhalla, para sa Riverheim 1.3 at mas bago). Dapat ang Riverheim mo ay ang bersiyong kailangan ng player na sumasali
sa laro: mula Riverheim 1.3, ang parehong bersiyon ng sa laro; bago iyon, ang parehong release (ang unang dalawang
numero ng bersiyon). Kung hindi, pinapangalanan ng editor ang dalawang bersiyon at hindi nito binubuksan ang mundo.

## Mga setting ng plugin

Nasa `BepInEx/config/Tie.WorldEditorBridge.cfg` ng laro o server ang mga setting ng plugin, isinusulat sa unang
pagsisimula nito. (Pinangalanan ito ng mga plugin na mas luma sa 0.41.0 na `local.worldeditorbridge.cfg`; pinapalitan
ng plugin ang pangalan nito, at nababasa ng editor ang alinman.)

| Setting | Default | Ang ginagawa nito |
|---|---|---|
| `BindAddress` | `127.0.0.1` | Ang address na pinakikinggan ng plugin. Panatilihin ang `127.0.0.1`: naaabot ng editor ang server sa sarili nitong SSH tunnel. Inilalantad ng ibang address ang plugin sa mga lokal na network at VPN ng makina (hindi kailanman sa internet: tinatanggihan ang mga tumatawag mula sa iba), nang hindi naka-encrypt ang token, at nagbababala ang log tungkol dito; tingnan ang [Nang walang SSH](#nang-walang-ssh). Nakikinig ang `+` sa bawat address ng makina: para sa server sa loob ng container (game panel gaya ng Pterodactyl), tingnan ang [I-edit ang inuupahang server o server sa panel](guides/edit-a-rented-server.md#server-sa-loob-ng-container). Hindi gumagana ang `0.0.0.0`: tinatanggihan ng plugin ang bawat request. |
| `Port` | `5182` | Ang port ng plugin. Palitan ito kapag may ibang program na gumagamit ng 5182. |
| `Token` | ginagawa sa unang pagsisimula | Ang lihim na dapat ipadala ng editor, sa isang header (hindi kailanman sa address): 24 na random na titik at numero. Kayang baguhin ng sinumang may hawak nito at may access sa port ang mundo, kaya ilihim ito. Burahin ang laman ng linya at i-restart para makakuha ng bago. |

## Nang walang SSH

Hindi kailangan ng editor ng SSH kapag tumatakbo ang laro sa parehong computer: kusa nitong nahahanap ang plugin doon.
Para sa ibang computer, kailangang makinig ang plugin sa address na naaabot ng computer na iyon, sa halip na
`127.0.0.1`.

**Ibang computer sa parehong bahay** (parehong home network):

1. Sa computer na nagpapatakbo ng laro o server, hanapin ang lokal na address nito: `ipconfig` sa Windows ("IPv4
   Address"), `ip addr` sa Linux. Mukha itong `192.168.1.20`.
2. Sa `BepInEx/config/Tie.WorldEditorBridge.cfg` nito, itakda ang `BindAddress = 192.168.1.20` (eksaktong ang address
   na iyon, hindi `0.0.0.0`) at simulan ulit ang laro o server. Sinasabi ng log nito ang
   `listening on http://192.168.1.20:5182/`, saka isang babalang hindi naka-encrypt ang token habang ipinapadala.
3. Papasukin ang port sa firewall ng computer na iyon, sa mga private network lang. Sa Windows, sa PowerShell bilang
   administrator (ina-apply lang ito ng Windows kapag nakatakda bilang **Private** ang network na iyon):

   ```powershell
   New-NetFirewallRule -DisplayName "WorldEditorBridge" -Direction Inbound -Protocol TCP -LocalPort 5182 -Action Allow -Profile Private
   ```

4. Kopyahin ang linyang `Token` ng parehong file.
5. Sa editor, sa panimulang pahina: **Isang dedicated server** → **Iba pang opsiyon** → **Gumawa ako ng sariling
   tunnel**. I-type ang `192.168.1.20:5182` bilang **Address ng bridge**, i-paste ang token, at pindutin ang
   **Kumonekta**.

Dumaraan sa network nang hindi naka-encrypt ang token at ang mga pagbabago sa mundo: kayang basahin ng sinuman sa
parehong Wi-Fi ang token at baguhin ang mundo. Ayos lang sa bahay; hindi sa pinaghahatiang network (dorm, café). Maaari
ring bigyan ng router ang computer na iyon ng ibang address balang araw: hindi na makikinig ang plugin hangga't hindi
binabago ang `BindAddress`, maliban kung itinatabi ng router ang address para dito (isang "DHCP reservation").

**Computer sa labas ng bahay** (sa kaibigan, inuupahang makina): tinatanggihan ng plugin ang mga tumatawag mula sa
internet, dahil dadaan doon ang token nang lantad. Bigyan ang computer na iyon ng SSH server, na gagamitin ng sariling
naka-encrypt na tunnel ng editor (may isa ang Windows 10 at 11: **Settings** → **Optional features** → **OpenSSH
Server**), o ilagay ang dalawang computer sa isang VPN (Tailscale, ZeroTier, WireGuard), na nag-e-encrypt ng daan, at
sundan ang mga hakbang sa itaas gamit ang address ng computer sa VPN (ang `100.x.y.z` ng Tailscale).

**Game server na inuupahan sa isang host, o pinapatakbo ng game panel** (Pterodactyl, Pelican, Docker container):
tingnan ang [I-edit ang inuupahang server o server sa panel](guides/edit-a-rented-server.md).

## Ang inilo-log ng plugin

Sinasabi ng `BepInEx/LogOutput.log` ng laro o server ang ginawa ng bawat pag-apply, para sa mga pagkakataong hindi
lumalabas ang isang pagbabago sa bawat player. Sa pagsisimula, sinasabi nito ang `WorldEditorBridge <version> listening on http://127.0.0.1:5182/ (token in
BepInEx/config/Tie.WorldEditorBridge.cfg)`. Pagkatapos, bawat pag-apply:

- `WorldEditorBridge: destroyed N object(s) (M found again at their place under a new id, K already gone), created C,
  moved I in place and F by making it again (…); A -> B object(s) known, P peer(s)`: ang mga object. Ang *found again*
  ay mga object na ginawang muli ng ibang mod, nahanap ayon sa uri at puwesto. Ang A at B ay ang mga object na alam ng
  laro bago at pagkatapos (sa susunod na update ng laro nawawala ang mga sinira, kaya binibilang pa sila ng B); ang P
  ay ang mga nakakonektang player.
- `WorldEditorBridge: 1 s after the apply: S of N destroyed object(s) still exist, R other object(s) of their prefab
  within 10 cm of their place (…)`: isang segundo pagkatapos. Kapag lampas 0 ang S, may kung ano sa server (malamang
  isang mod) na gumawa ulit o nagpanatili ng sinirang object. Binibilang ng R ang mga object ng parehong uri na
  nakatayo kung saan nakatayo ang sinira: ginawa ulit ng mod, o naroon na mula pa noon. Kapag parehong 0, ginawa ng
  server ang bahagi nito, at ang nakikita pa ng isang player ay ang sariling laro ng player na iyon.
- `WorldEditorBridge: applied terrain to N zone(s) (C new)`: ang lupa. Binibilang ng *new* ang mga zone na walang
  pagbabago sa lupa dati.
- `WorldEditorBridge: reset N zone(s): D object(s) removed, L location(s) to place again`: mga reset ng zone.
- `WorldEditorBridge: caught at <file>:<line> (<member>) while <doing>: <error type>: <message>`: error na hinawakan
  ng plugin, kasama ang stack trace nito maliban kung inaasahan ito. Isinusulat na `~` ang home folder ng server.

## Mga shortcut

| Key | Aksiyon |
|---|---|
| `Ctrl+S` | **I-apply nang live** |
| `Ctrl+Z`, `Ctrl+Y` | I-undo, i-redo (saka ipinapadala ito ng **I-apply nang live**, o agad ng **Auto**) |

Gumagana ang bawat ibang key gaya ng offline: tingnan ang [Mga shortcut](help/shortcuts.md).

## Mga tip at limitasyon

- Gumagana lang ang plugin sa larong **nagho-host** ng mundo (server, single player o ang host). Sa larong sumali sa
  server ng iba, tumatanggi itong magbago ng kahit ano.
- Agad na nangyayari ang **mga reset ng zone** kung saan may player sa malapit, kung hindi ay sa susunod na may dumating.
  Hindi na maa-undo ang mga ito kapag na-apply na.
- Ginagawa ng server ang mga object na inilagay nang live; nakikita sila ng mga player gaya ng mga object na kaitatayo
  o kaitatanim lang ng isang tao.
- Ina-apply nang minsanan ang libu-libong object nang sabay at maaaring sandaling mapatigil ang server. Maaaring abutin
  nang ilang minuto ang pagbasa ng malaking mundo kapag kumonekta ka.
- Kung hindi sumagot ang laro sa loob ng 30 segundo (naglo-load ito, o abalang-abala), walang nagagawa sa request na
  iyon: mag-apply ulit.
- Gamitin ang plugin mula sa parehong release ng editor mo. Sa mas lumang plugin, sinasabi ng editor ang kulang. Ang mga
  feature na nangangailangan ng bagong plugin:

| Bersiyon ng plugin | Kailangan para sa |
|---|---|
| 0.10.0 | Mga reset ng zone |
| 1.17.0 | Pagsunod sa laro |
| 1.26.0 | Mga object ng mga mod at ang mga pangalan nila |
| 1.27.0 | Mga model ng mga mod |
| 1.35.0 | Pagsabi sa sariling mga plugin ng mga mod kung ano ang na-apply |
| 1.40.0 | Mga paglipat na pinapanatili ang parehong object |
| 1.45.3 | Pag-apply na nawala ang sagot (naputol na tunnel) na hindi gumagawa ng doble kapag ipinadala ulit |
| 1.54.0 | Ang mga file ng mga mod na Expand World at mundo ng Riverheim na binabasa mula sa laro |
| 1.57.3 | Mga creature, barko at iba pang gumagalaw na bagay na sinusundan habang gumagalaw |
| 1.97.0 | Mga binagong object na pinapanatili ang pagkakakilanlan nila |

## Kaugnay

- [I-edit ang mundong nilalaro mo](guides/edit-your-game-live.md)
- [I-edit ang dedicated server habang may naglalaro](guides/edit-a-server-live.md)
- [Live na pag-edit](concepts/live-editing.md): paano ito kumokonekta, ang ipinapadala, ilang editor at mga player
- [Pag-troubleshoot](help/troubleshooting.md#hindi-kumokonekta-ang-server): kapag hindi ito kumokonekta
