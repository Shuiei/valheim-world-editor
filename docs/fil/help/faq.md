<!-- source: 93850bba -->
# Mga tanong

Maiikling sagot sa pinakamadalas itanong, bawat isa may link sa buong sagot.

## Ligtas ba ito para sa mundo ko?

Oo, kung may kopya ka. Walang naisusulat hangga't hindi mo pinipindot ang **I-save sa mundo** o **I-apply nang live**,
at binabasa ulit at sinusuri ang bawat pag-save: kung hindi ito tama kapag binasa ulit, tinatanggal ang mga bagong file
at nananatili ang mundo gaya ng dati. Gayunman, kopyahin ang mundong mahalaga sa iyo bago ang una mong pag-save: tingnan
ang [Mag-backup ng mundo bago mag-edit](../guides/back-up-a-world.md) at
[Ang isinusulat ng editor](../concepts/safety.md#ano-ang-isinusulat-ng-editor).

## Puwede ba akong mag-undo pagkatapos mag-save?

Oo. Iniingatan ang kasaysayan kasama ng mundo pagkatapos ng bawat pag-save o pag-apply, kahit pagkatapos mong isara ang
editor: i-undo ang isang hakbang (o **Tanggalin** ito sa Kasaysayan) at i-save ulit. Tingnan ang
[Mag-undo ng hakbang, bawiin ang isang pagbabago, o itapon ang lahat](../guides/undo-changes.md). Pinapanatili rin
ito ng mga pag-reset ng zone at ng lupang Walang limitasyon; lampas sa abot nito ang ginawa ulit ng isang reset (tingnan
ang [Ang hindi kayang bawiin ng isang hakbang](../concepts/saving-and-history.md#ang-hindi-mababawi-ng-isang-hakbang)).

Para sa pinsalang nakita sa ibang pagkakataon, [Ibalik ang isang area mula sa backup](../guides/restore-from-backup.md).

## May kailangan ba ang mga player sa server ko?

Wala. Ang laro o server lang na nagho-host ng mundo ang nangangailangan ng BepInEx at ng plugin, at para lang sa
live na pag-edit. Walang kailangan ang mga player na sumasali, kasama na ang lupang itinaas lampas sa 8 m na hangganan
ng laro. Tingnan ang [Mga hangganan ng lupa](../concepts/ground-limits.md#walang-limitasyon).

## Makakasali ba ang mga console player sa crossplay sa na-edit na mundo?

Oo: normal na mundo ng Valheim pa rin ang na-edit na mundo, at walang kailangang mod ang lupa ng editor na lampas sa
8 m. Ang mga eksepsiyon ay mga mundong nangangailangan mismo ng mod: ang mundong ginawa gamit ang Riverheim, o ang
server na nagpapatakbo ng Homestead, ay tumatanggi sa mga player na walang mod, at hindi makakapag-install ng mod ang mga
console player. Tingnan ang [Isang bagong mundo](../new-world.md#mga-tip-at-limitasyon).

## Puwede ba akong mag-edit habang tumatakbo ang laro?

Oo, sa live mode, kasama ang plugin na WorldEditorBridge sa laro o server: tingnan ang
[I-edit ang mundong nilalaro mo](../guides/edit-your-game-live.md) o
[I-edit ang dedicated server habang may naglalaro](../guides/edit-a-server-live.md). Para i-edit ang mga file
ng naka-save na mundo, isara muna ang laro o ihinto ang server: sinusulatan ng tumatakbong laro ang isinusulat ng editor.
Tingnan ang [Nag-save ang laro habang nag-e-edit ka](troubleshooting.md#nag-save-ang-laro-habang-nag-e-edit-ka).

## Puwede ko bang i-edit ang server na inuupahan ko sa isang game host?

Oo, pero nang live lang kung bibigyan ka ng host ng SSH access sa makina (shell login, hindi lang SFTP o file
manager): tanungin ang host bago ka umupa. Karamihan sa mga game server na inuupahan sa host ay nagbibigay lang ng web
panel at FTP, kaya hindi maabot ng editor ang plugin: ihinto ang server, i-download ang mundo nito, i-edit ito, i-upload
ito pabalik. Ang server na pinapatakbo ng game panel gaya ng Pterodactyl sa isang container sa sarili mong makina ay
puwedeng i-edit nang live, sa pamamagitan ng tunnel na ikaw mismo ang nagbukas. Tingnan ang
[I-edit ang inuupahang server o server sa panel](../guides/edit-a-rented-server.md).

## Humihingi ng token ang editor. Nasaan ito?

Sa `BepInEx/config/Tie.WorldEditorBridge.cfg` ng laro o server, sa linyang `Token`. Isinusulat ng plugin ang file na
iyon sa unang pagkakataong magsimula ang laro kasama nito. Para sa sarili mong laro, binabasa ito ng editor nang kusa;
para sa server, kinokopya mo ito mula sa server. Tingnan ang [Mga setting ng plugin](../live-mode.md#mga-setting-ng-plugin).

## Gumagana ba ito sa mga mod?

Oo. Sa isang live na larong may mod, natututunan ng editor ang mga object ng mga mod at iginuguhit ang mga model nila
(tingnan ang [Mga larong may mod](../live-mode.md#mga-larong-may-mod)). Ang mga mod na nagbabago kung paano ginagawa ang mga
mundo ay nangangailangan ng add-on ng editor para sa kanila: may isa ang Expand World, Riverheim at Tunnelheim. Tingnan
ang [I-edit ang mundong ginawa gamit ang Expand World, Riverheim o Tunnelheim](../guides/use-mods-worlds.md).

## Naglalaro ako gamit ang mga mod ng Expand World. Alam ba ito ng editor?

Oo, kapag naka-install ang add-on ng Expand World: sinusunod ng lupa, mapa, mga biome at mga hula ang mga setting at
data file ng mga mod. Kung wala ito, binubuo ang bawat mundo gaya ng ginagawa ng laro mismo. Tingnan ang
[I-edit ang mundong ginawa gamit ang Expand World, Riverheim o Tunnelheim](../guides/use-mods-worlds.md).

## Ginawa ang mundo ko gamit ang Riverheim. Mabubuksan ko ba ito?

Oo, kapag naka-install ang add-on ng Riverheim at nasa isang BepInEx folder na alam ng editor ang Riverheim mismo:
iginuguhit ng editor ang mundo gamit ang sarili mong kopya ng mod. Tingnan ang [I-edit ang mundong ginawa gamit ang
Expand World, Riverheim o Tunnelheim](../guides/use-mods-worlds.md).

## Kailangan ko ba ang Homestead?

Para lang itayo sa laro ang mga blueprint ng editor. Gumagana nang wala ito ang Gawaan, ang aklatan at ang pag-paste ng
mga blueprint sa mundo; kapag wala ito, sinasabi iyon ng panimulang pahina. Tandaan na tumatanggi ang server na
nagpapatakbo ng Homestead sa mga player na wala nito. Tingnan ang [Ang Gawaan](../workshop.md).

## Saan ko ito ida-download? Nasa Thunderstore, Hexium o Nexus Mods ba ito?

- **Ang editor** ay isang program, hindi mod: i-download ito mula sa
  [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest) o mula sa
  [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348).
- **Ang plugin na WorldEditorBridge** (para sa live na pag-edit) ay nasa releases page, Nexus Mods,
  [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) at
  [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge), para sa mga mod manager.
- **Mga add-on** para sa mga mod ay nasa releases page at Nexus Mods.

Gamitin ang plugin at mga add-on ng parehong release ng editor. Tingnan ang [I-install](../start/install.md).

## May ipinapadala ba ito sa internet?

Wala. Nangyayari ang lahat sa computer mo; naaabot lang ang dedicated server sa pamamagitan ng sariling encrypted na SSH
tunnel ng editor, gamit ang token na tina-type mo. Tingnan ang
[Walang lumalabas sa computer mo](../concepts/safety.md#walang-lumalabas-sa-computer-mo).

## Ligtas bang hayaan si Claude o ang isang script na baguhin ang mundo ko?

Nananatiling nakabinbin ang binabago ni Claude hanggang mag-save ka, at makakakonekta lang ito mula sa computer na ito
gamit ang token. Ang mga script ay mga program na tumatakbo nang may mga karapatan mo, kaya mapapatakbo lang ni Claude ang
mga ito kung papayagan mo. Tingnan ang [Mga script at si Claude](../concepts/safety.md#mga-script-at-si-claude).

## May hindi gumagana. Saan ako titingin?

Magsimula sa [Pag-troubleshoot](troubleshooting.md). Para mag-ulat ng bug, ilakip ang log ng editor:
[Ang log at mga ulat ng bug](troubleshooting.md#ang-log-at-mga-ulat-ng-bug).
