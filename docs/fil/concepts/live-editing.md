<!-- source: 6167da37 -->
# Live na pag-edit

Sa live mode, ang mundo ng isang **tumatakbong** laro ang ine-edit ng editor sa halip na ang mga file nito. Puwedeng
manatiling nakakonekta ang mga player: nakikita nila ang mga pagbabago mo sa sandaling i-apply mo ang mga ito, at
nakikita mo ang itinatayo nila habang nag-e-edit ka. Ipinapaliwanag ng pahinang ito kung paano naaabot ng editor ang
laro, kung ano ang naglalakbay sa pagitan nila, at kung ano ang nangyayari kapag parehong lugar ang binago ng mga
player at ng editor.

## Paano ito kumokonekta

Kailangan ng live mode ang isang maliit na plugin, ang **WorldEditorBridge**, sa loob ng larong **nagho-host** ng
mundo: ang dedicated server, o ang sarili mong laro kapag naglalaro ka nang mag-isa o nagho-host. Tumatakbo ito sa
**BepInEx**, ang karaniwang mod loader ng Valheim. Hindi kailangan ng mga sumasaling player ang alinman sa dalawa, pati
ang mga player sa console, at hindi rin kailangan ng offline na pag-edit ang alinman.

Sumasagot ang plugin sa editor sa isang port ng computer kung saan ito tumatakbo (5182 sa simula), at ginawa itong
manatiling pribado:

- **Sa computer lang na iyon ito nakikinig** (`127.0.0.1`), kaya walang anuman sa network ang makakaabot dito.
  Mabubuksan ito ng setting nitong `BindAddress` sa mga lokal na network at VPN ng computer (hindi kailanman sa
  internet: tinatanggihan ang mga tumatawag mula roon), nang hindi naka-encrypt ang token; panatilihin ito gaya ng
  dati, maliban kung nag-e-edit ka mula sa ibang computer sa bahay ([Nang walang SSH](../live-mode.md#nang-walang-ssh)).
- **Kailangan nito ang lihim nitong token.** Sa unang pagbukas nito, isinusulat ng plugin ang
  `BepInEx/config/Tie.WorldEditorBridge.cfg` na may **token** na 24 na random na titik at numero. Tinatanggihan ang
  request na wala nito. Hangga't hindi pa nabubuksan nang isang beses ang laro o server kasama ang plugin, walang token.

**Ang sarili mong laro.** Tumatakbo ang editor sa parehong computer, kaya ito mismo ang bumabasa ng port at token mula
sa file na iyon, sa bawat BepInEx folder na alam nito (ang Valheim folder mo, ang mga profile ng mod manager mo, ang mga
folder na idinagdag sa **Mga setting**), at tinatanong ang bawat nahanap na plugin kung may naka-load na mundo.

**Isang dedicated server.** Hindi direktang maaabot ang plugin mula sa computer mo, kaya gumagawa ang editor ng sariling
daan papasok:

```text
 computer mo                                     ang server
 ┌──────────────────────┐ SSH, naka-encrypt  ┌─────────────────────────────────────┐
 │ editor ── port 15182 ═╪═══════════════════╪═► 127.0.0.1:5182  WorldEditorBridge │
 └──────────────────────┘                    │                   sa loob ng Valheim│
                                             └─────────────────────────────────────┘
```

1. Nagla-log in ito sa server sa pamamagitan ng **SSH** gamit ang account mo (may sarili itong SSH, kaya hindi
   kailangan ang programang `ssh`).
2. Ipinapasa nito ang isang bakanteng port ng computer mo (mula 15182 pataas) sa naka-encrypt na koneksiyong iyon
   papunta sa port ng plugin sa server: isang **SSH tunnel**.
3. Ipinapadala nito ang token, na **ikaw** ang nagta-type: hindi ito kailanman binabasa ng editor mula sa server. Kaya
   hindi sapat ang pag-log in sa SSH para baguhin ang mundo; dapat ding alam ng kumokonekta ang lihim ng plugin.

Ang server na pinapatakbo ng isang game panel (Pterodactyl, Pelican) sa isang Docker container ay may sariling
`127.0.0.1`, na hindi naaabot ng tunnel papunta sa makina, at karaniwang walang ibinibigay na SSH login ang game server
na inuupahan mula sa isang host: tingnan ang
[I-edit ang inuupahang server o server sa panel](../guides/edit-a-rented-server.md).

Tinatandaan din ng editor ang pagkakakilanlan ng server sa unang koneksiyon. Kung magbago man ito, tumatanggi ang
editor na kumonekta, dahil maaaring may nagpapanggap na server mo.

Ang mundong hino-host ng laro nito lang ang binabago ng plugin. Sa larong sumali sa server ng iba, tumatanggi ito,
dahil ang mga object lang sa paligid ng sarili nitong player ang alam ng ganoong laro. Gamitin ang plugin mula sa
parehong release ng editor mo.

## Ano ang ipinapadala at kailan

**Kapag kumonekta ka**, binabasa ng editor ang buong mundo mula sa laro: bawat object, ang mga zone na nagawa na ng
laro, ang mga lokasyon. Ito rin ang data na hawak ng isang save, kaya gumagana ang mapa, ang paghahanap at bawat tool
gaya ng offline. Sa malaking mundo, matagal ito, hanggang ilang minuto. Hinihingi rin nito ang mga object na alam ng
laro na hindi sariling object ng Valheim (mga piece ng mga mod), para magkaroon sila ng pangalan at lumabas sa listahan
ng Paglagay at sa dock ng Pagtayo.

**Kapag nag-apply ka** (**I-apply nang live**, o bawat hakbang agad kapag naka-**Auto**), ipinapadala nito ang nagbago
mula sa huling pag-apply, sa ganitong pagkakasunod:

1. **Ang lupa** ng bawat zone na binago mo, kasama ang lupang Walang limitasyon (tingnan ang
   [Walang limitasyon](ground-limits.md#walang-limitasyon)).
2. **Ang mga object** na binura, inilagay, na-paste, pinalitan, binago o inilipat mo.
3. **Mga pag-reset ng zone**, sa huli. Ginagawa ulit ng laro ang mga zone na iyon, kaya binabasa ulit ng editor ang
   mundo; sumasama ang kasaysayan nang wala ang ginawa nito sa mga zone na iyon.

Ibinabahagi ng laro ang bawat pagbabago sa bawat nakakonektang player, gaya ng ginagawa nito sa itinatayo ng mga player,
at sine-save ito kasama ng mundo. Ginagawa ng server ang mga object na inilagay nang live, kaya nakikita sila ng mga
player gaya ng isang bagay na katatayo o katatanim lang.

Ginagawa ng plugin ang lahat ng ito sa sariling loop ng laro, ilang request bawat frame, kaya hindi kailanman binabasa o
binabago ng laro ang mundo nito mula sa dalawang lugar nang sabay. Ang malaking pagbabago (libu-libong object) ay
maaaring magpatigil sandali sa server. Kung hindi tanggapin ng laro ang isang request sa loob ng 30 segundo, itinatapon
ito ng plugin: walang nagagawa rito at walang minamarkahang na-apply, kaya mag-apply ulit. Kapag nawala naman ang sagot
pabalik (naputol na tunnel), hindi malalaman ng editor kung nagawa ito; ligtas itong ipadala ulit, dahil nakikilala ng
plugin ang request na nagawa na nito at hindi nito ginagawa nang dalawang beses ang mga object nito. Isang pag-apply
lang ang tumatakbo sa bawat pagkakataon.

### Hindi nagbabago ang object kapag inilipat

Gumagalaw sa laro sa ilalim ng sarili nitong id ang object na inilipat o iniikot mo kapag wala nang ibang nagbago rito.
Kaya kilala pa rin ito ng mod na sumusubaybay sa mga object habang tumatakbo ang laro (ang karatula ng label ng isang
chest, ang mga chest ng isang warehouse, isang Ward), at pinapanatili ng chest ang laman nito gaya ng hawak ng laro.
Ganoon din pinapanatili ang id ng object na binago mo ang data.

May kondisyon. Inilalagay ng laro ng isang player ang nakatayong object (pader, chest) nang isang beses, pagpasok nito
sa abot, at hindi na ito inililipat pagkatapos. Kaya napapanatili lang ng paglipat ang id kapag walang player na
naka-load ang parehong luma at bagong puwesto nito (mga 160 m sa paligid ng bawat player). Kung hindi, ginagawa ulit
ang object sa bagong puwesto nito at inaalis sa luma, at sinasabi ng mensahe ng apply kung ilan: para sa ibang mga mod,
mga bagong object ang mga iyon. Ilipat ang isang gusali habang walang malapit dito para mapanatili ang lahat tungkol
dito. Laging ginagawa ulit ang magkaugnay na grupo ng isang mod (barko at mga piece nito) at ang mga creature.

## Pagsunod sa laro

Habang bukas ang isang area, tinatanong ng editor ang laro bawat 2 segundo kung ano ang nagbago sa mga zone ng area na
iyon. Magaan ito: sumasagot ang laro ng isang numero bawat zone, na nagbabago tuwing may nagbabago sa zone, at ang mga
zone lang na nagbago ang numero ang binabasa ulit. Sinasabi ng status bar ang dumating ("Mula sa laro: 2 bagong object,
1 inalis, ang lupa ng 1 zone."); dumarating nang tahimik ang pagbabago sa data lang, gaya ng apoy na nauupos.

- **Mga object** na itinatayo, tinatanggal o binabago ng mga player ay lumilitaw, nawawala o nagbabago sa editor, nang
  walang hakbang sa Kasaysayan. Nananatiling parehong object sa editor ang binagong object, kaya nananatili rito ang
  pinili, ang Inspector at ang puwesto nito sa Kasaysayan.
- **Ang sarili mong mga object, kapag na-apply na,** ay ipinapakita gaya ng talagang hawak ng laro: kung saan inilapag
  ng physics ang isang piece, ang health at spawn point na ibinigay ng laro sa isang creature.
- **Mga bagay na kusang gumagalaw o nagbabago** (mga creature, barko, cart, item na nakakalat, apoy at smelter) ay
  sinusundan habang gumagalaw, nang hindi binabasa ang buong zone nila bawat beses.
- **Lupang** hinukay, pinatag o pininturahan sa laro ay dumarating kung saan wala kang sariling pagbabago sa zone na iyon
  (tingnan sa ibaba kapag mayroon).

Sa 3D editor lang nangyayari ang pagsunod, hindi kailanman habang may stroke ng brush o habang nag-a-apply. Ipinapakita
ng mapa ang mundo gaya noong huling basa: binabasa ulit ng **I-reload** ang lahat ng ito.

## Maraming editor at player

Maaaring baguhin ng mga player at ng editor ang parehong lugar. Ganito ito inaayos ng editor:

| Ang nangyayari | Ang ginagawa ng editor |
|---|---|
| Binabago ng mga player ang lupa ng zone kung saan may mga pagbabago kang hindi pa na-apply | Pinapanatili ang sa iyo at sinasabi iyon ng status bar. Pagkatapos, nagtatanong muna ang **I-apply nang live** (**I-apply sa ibabaw ng mga pagbabago ng laro**): isinusulat ng pag-apply ang lupa mo sa zone na iyon, at maaaring mawala ang ginawa rito ng mga player mula noon. Pinapanatiling nakabinbin ng **Hindi ngayon** ang mga pagbabago mo. |
| Kinukuha o sinisira ng mga player ang isang object | Tuluyan na itong wala: hindi ito ginagalaw ng undo at redo, at itinatapon ang pagbura rito na hindi mo pa na-apply. Kasama rito ang mga object na inilagay mo. |
| Binura at in-apply mo ang isang object | Nananatili itong binura; ibinabalik ito ng undo bilang kopya (tingnan ang [Pag-apply nang live](saving-and-history.md#pag-apply-nang-live)). |
| Inililipat mo ang isang gusali habang may mga player na malapit dito | Ginagawa itong muli sa halip na ilipat (tingnan ang [Hindi nagbabago ang object kapag inilipat](#hindi-nagbabago-ang-object-kapag-inilipat)). |
| Binabago mo ang isang object sa Inspector habang binabago rin ito ng laro | Hindi nag-a-apply ang Inspector sa ibabaw ng mas bagong bersiyon: ipinapakita ito, para baguhin ulit. |
| Nire-reset mo ang zone na may player na nakatayo malapit | Ginagawa ito ulit ng laro agad; kung hindi, sa susunod na may pumunta. |

Sumasagot ang plugin sa anumang editor na may token, at hindi nito pinag-iiba ang mga pagbabago ng isang editor sa
pagbabago ng player: ang ina-apply ng ibang editor ay umaabot sa iyo gaya ng pagbabago ng player.

**Auto at Claude.** Kapag naka-on ang **Auto**, umaabot sa mga player ang bawat hakbang sa sandaling matapos mo ito,
at isang apply lang ang mahabang stroke ng brush. Dahil dito, walang binabago si Claude sa isang live na mundo habang
naka-on ang Auto (tingnan ang [Mga script at si Claude](safety.md#mga-script-at-si-claude)).
