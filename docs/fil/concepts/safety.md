<!-- source: 4978ef09 -->
# Kaligtasan at privacy

Isang programa sa computer mo ang editor. Sumusulat lang ito sa mga mundo mo kapag pinindot mo ang **I-save sa mundo**,
walang ipinapadala kahit saan maliban kung ikinonekta mo ito sa server o kay Claude, at hindi isinasama ang mga lihim
sa log nito. Sinasabi ng pahinang ito kung ano ang isinusulat nito, ano ang maaaring lumabas sa computer mo, at ano ang
tumatakbo gamit ang mga karapatan mo.

## Ano ang isinusulat ng editor

**Ang mga mundo mo**, kapag nag-save ka lang. Hanggang doon, nakabinbin ang bawat pagbabago at nasa memory ng editor
(tingnan ang [Mga nakabinbing pagbabago](saving-and-history.md#mga-nakabinbing-pagbabago)). Nagdadagdag ang pag-save ng bagong
save na may numero sa folder ng mundo at walang tinatanggal na luma hangga't hindi nito nababasa nang tama pabalik ang
bago (tingnan ang [Pag-save ng mundo](saving-and-history.md#pag-save-ng-mundo)). Napupunta sa bagong folder ang bagong
mundo, hindi kailanman sa ibabaw ng mundong may parehong pangalan. Isara muna ang laro: sinusulatan ng tumatakbong laro
ang mga file na na-save ng editor.

**Isang live na laro**, kapag nag-apply ka lang. Ang laro na mismo ang nagse-save ng mga pagbabago, gaya ng pag-save
nito sa itinatayo ng mga player.

**Mga blueprint mo**: isang `.blueprint` file at ang larawang `.png` nito sa folder ng Homestead sa save folder ng
Valheim, kapag nag-save ka ng blueprint. Tinatanggal ng **Burahin** ang dalawa, matapos magtanong.

**Ang sarili nitong data folder**: `~/.local/share/ValheimWorldEditor` sa Linux,
`%LOCALAPPDATA%\ValheimWorldEditor` sa Windows, `~/Library/Application Support/ValheimWorldEditor` sa macOS
(ipinapakita ito sa ibaba ng menu ng panimulang pahina). Hawak nito ang:

| Ano | Mga file |
|---|---|
| Ang mga setting at pinili mo | `settings.json`, `prefs.json`, `place.json` (mga preset ng Paglagay, mga paborito, mga kamakailang uri), `stamps.json`, `selections.json` |
| Mga naka-save na server | `servers.cfg`: address, user, token, ang pagkakakilanlan ng server, at ang password kapag in-tick mo ang **I-save ang password** (tingnan sa ibaba) |
| Kasaysayan | `history/`, isang file bawat mundo (tingnan ang [Kasaysayan](saving-and-history.md#kasaysayan)) |
| Ang trabaho mo | `scripts/` (mga naka-save na script), `heightmaps/` (mga na-export na heightmap), `blueprints/export` (mga `.vbuild` file) |
| Ang natutunan nito mula sa laro mo | `game-index.json` at iba pang `game-*.json` file (kung nasaan ang mga bagay sa mga file ng Valheim), `mod-catalogue.json` (mga object ng mod na nakita sa live na laro), `mod-bundles/` (mga model ng mod, kinopya mula sa mga DLL nila) |
| Ang log | `ValheimWorldEditor.log`, bago sa bawat pagbukas; `perf-native.log` habang naka-on lang ang **I-record ang frame rate** |

**Ang hindi nito kailanman binabago.** Binabasa ng editor ang folder ng larong Valheim mo, ang mga BepInEx folder mo
at ang mga file ng mga mod mo (mga model, texture, mga patakaran ng laro, mga setting ng plugin) at walang isinusulat
doon. Binabasa nito ang code ng mga mod para malaman ang mga pangalan ng mga halaga nila, at hindi kailanman
pinapatakbo ito.

**Mga password.** Itinatabi bilang plain text sa `servers.cfg` ang naka-save na password. Sa Linux, ikaw lang ang
makakabasa ng file; gayunman, mas mainam ang SSH key kung puwede. Hindi kailanman sine-save ang passphrase ng key.
Tinatanggal ng **kalimutan** sa tabi ng naka-save na server ang server, kasama ang password at token nito.

## Walang lumalabas sa computer mo

Walang account, walang tracking at walang update check ang editor. Nakikipag-usap lang ito sa labas sa mga sitwasyon sa
ibaba, bawat isa ay pinili mo:

| Kailan | Ano ang napupunta saan |
|---|---|
| Kumonekta ka sa **dedicated server** | Isang SSH na koneksiyon, naka-encrypt, sa server na iyon lang, gamit ang account na ibinigay mo. Naaabot ang plugin sa pamamagitan nito, hindi kailanman sa bukas na internet (tingnan ang [Paano ito kumokonekta](live-editing.md#paano-ito-kumokonekta)). |
| Ine-edit mo nang live ang **sarili mong laro** | Walang lumalabas sa computer: nakikipag-usap ang editor sa plugin sa `127.0.0.1`. |
| Ikinonekta mo si **Claude** | Nakikipag-usap ang app ni Claude sa computer mo sa editor sa `127.0.0.1`. Napupunta kay Claude ang isinasagot ng editor (ang data ng area, mga larawan ng 3D na tanaw), gaya ng anumang ibinibigay mo kay Claude. |
| Nag-click ka ng link | Bumubukas sa web browser mo ang **Kunin ang Homestead**, o ang help page na binuksan sa GitHub. |

**Nananatiling pribado ang plugin.** Nakikinig lang ang WorldEditorBridge sa computer kung saan ito tumatakbo, at
sumasagot lang sa mga request na may dalang lihim nitong token. Para sa server, ikaw mismo ang nagta-type ng token;
hindi ito kailanman binabasa ng editor mula sa server, kaya hindi mababago ng pag-log in sa SSH lang ang mundo. Panatilihing
pribado ang token: mababago ng sinumang may hawak nito at may access sa port ang mundo. Burahin ang laman ng linyang
`Token` ng `Tie.WorldEditorBridge.cfg` at i-restart ang laro para makakuha ng bago.

**Sinusuri ang pagkakakilanlan ng server.** Tinatandaan ng editor ang pagkakakilanlan ng server sa unang koneksiyon. Kung
magbago man ito, tumatanggi ang editor na kumonekta, dahil maaaring may nagpapanggap na server mo. Pagkatapos mong
i-reinstall ang isang server, burahin ang laman ng linyang `HostKey` nito sa `servers.cfg` para tanggapin ang bago.

## Mga script at si Claude

**Mga programa ang mga script.** Ang [script](../scripting.md) ay C# code, at pinapatakbo ito ng editor gamit ang mga
karapatan mo, gaya ng anumang programang binubuksan mo. Tumatakbo ito sa hiwalay na proseso para laging matapos ito ng
**Ihinto**, pero hindi sandbox ang prosesong iyon: kayang basahin at sulatan ng script ang mga file mo at gawin ang
anumang kaya ng account mo. Patakbuhin lang ang mga script na ikaw ang sumulat o pinagkakatiwalaan mo, gaya ng gagawin
mo sa isang mod.

**Nagtatrabaho si Claude para sa iyo, sa loob ng mga hangganan.** Kapag naka-on ang **Payagang kumonekta si Claude**
sa Mga setting, magagamit ni [Claude](../claude.md) ang mga tool ng editor. Ganito siya nililimitahan ng editor:

- **Itong computer lang, may token.** Sa `127.0.0.1` lang nakikinig ang editor, tinatanggihan ang mga request na
  naka-address sa ibang pangalan (kaya hindi ito maaabot ng web page sa ilalim ng gawa-gawang address), at tinatanggihan
  ang anumang request na wala ang token nito (48 random na character). Ang ibang **Address** sa Mga setting (`0.0.0.0`,
  o isa sa computer na ito) ay nagpapahintulot sa ibang computer sa network na iyon na kumonekta, gamit ang token, na
  naglalakbay noon nang hindi naka-encrypt: mga computer lang na may lokal na address sa sariling network ng computer
  na ito, hindi kailanman ang sa internet, at hindi kailanman ang mga script (nananatili ang mga ito sa Claude ng
  computer na ito). Gumagawa ng iba ang **Bagong token**; i-off ang switch at walang makakakonekta.
- **Nakabinbin lang.** Nananatiling nakabinbin ang anumang binabago ni Claude, isang hakbang bawat isa, may tag na
  **Claude** sa Kasaysayan. Hindi kailanman nagse-save, nag-a-apply nang live, nagtatapon, nagre-reload, sumusulat o
  nagbubura ng mga file mo si Claude, o kumokonekta sa live na laro: sa iyo nananatili ang mga iyon. Hindi siya
  nagbubukas ng ibang mundo habang may mga hindi naka-save na pagbabago ang sa iyo.
- **Walang live kapag naka-on ang Auto.** Habang naka-on ang **Auto** sa isang live na mundo, agad na aabot sa mga
  player ang bawat pagbabago, kaya walang binabago si Claude at hinihiling niyang i-off mo ang Auto.
- **Walang script nang wala ang pahintulot mo.** Tinatanggihan ang mga script ni Claude hangga't hindi mo ino-on ang
  **Maaaring magpatakbo si Claude ng script** sa Mga setting (naka-off bilang default). Kahit noon, ipinapakita sa iyo
  ang bawat script na isinusulat ni Claude bago ito tumakbo, para patakbuhin o tanggihan: ang teksto sa isang mundo
  (karatula, tag na isinulat ng ibang player) ay maaaring humikayat kay Claude na sumulat ng mapanganib na script.
  Pinapatakbo ng iba pa niyang tool ang sariling code ng editor at hindi na itinatanong.
- **Madaling bawiin.** Tinatanggal ng **Bawiin ang mga pagbabago ni Claude** sa Kasaysayan ang bawat hakbang na ginawa
  ni Claude sa bukas na area at pinapanatili ang sa iyo (tingnan ang [Kasaysayan](saving-and-history.md#kasaysayan)).

## Mga extension at add-on ng mga mod

Maaaring magdala ang isang mod ng **extension ng editor**: maliit na code na nilo-load ng editor para basahin at
iguhit ang mga object ng mod na iyon. Gaya ng script, tumatakbo ito gamit ang mga karapatan mo, kaya ikaw ang
nagpapasya. **Naka-off** ang bawat extension hangga't hindi mo ito ino-on sa **Mga setting → Mga extension ng mod**, at
ang eksaktong file lang na iyon: tinatandaan ng editor ang fingerprint nito (SHA-256), kaya naka-off ulit ang binagong
file, gaya ng update ng mod, hangga't hindi mo ito ino-on ulit.

Ang **mga add-on**, na ikaw mismo ang nagda-download at nag-a-unpack sa folder na `addons` ng editor, ay tumatakbo
pagka-install, dahil pinili mo na ito nang i-install mo. Ino-off ng **Mga setting → Mga add-on** ang alinman sa kanila.

## Ang hindi isinasama ng log

Isinusulat ng editor ang ginagawa nito, at ang bawat error na hinahawakan nito, sa `ValheimWorldEditor.log`. Binubuksan
ito ng **Buksan ang log** sa ibaba ng menu ng panimulang pahina, para mailakip mo ito sa ulat ng bug. Bago isulat ang
isang linya, inaalis ang personal na data:

- isinusulat na `~` ang home folder mo, at pinapalitan ng mga placeholder gaya ng `<path>` ang ibang file path, web
  address, pangalang `user@host`, IP at address ng server, at ang pangalan ng account mo;
- hindi kailanman umaabot dito ang mga account, password, passphrase at token ng mga naka-save na server.

Sumusulat ang plugin na WorldEditorBridge sa sariling `BepInEx/LogOutput.log` ng laro, na isinusulat ding `~` ang home
folder ng server.

Gayunman, basahin muna ang log bago mo ito i-post sa publiko. At magtabi ng sarili mong kopya ng mga mundong mahalaga
sa iyo (tingnan ang [Mag-backup ng mundo bago mag-edit](../guides/back-up-a-world.md)).
