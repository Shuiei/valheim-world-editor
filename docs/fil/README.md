<!-- source: d6008f75 -->
# Tulong ng Valheim World Editor

Binubuksan ng Valheim World Editor ang isang mundo ng Valheim, naka-save man o tumatakbo, sa isang 3D na window. Hubugin
ang lupa, magtayo, maglagay at maglipat ng kahit ano, saka i-save ito pabalik o panoorin itong magbago sa laro habang
nagtatrabaho ka.

**Bago ka rito?** [I-install ang editor](start/install.md), saka sundan ang [una mong edit](start/first-edit.md):
sampung minuto sa isang kopya ng mundo, walang mawawala. May hinahanap ka? Maghanap sa tulong (`Ctrl+F` sa Help
window), o magsimula sa isang gawain sa ibaba. Sa editor, binubuksan ng `F1` o ng maliit na **?** sa itaas ng isang
panel ang tulong sa tool na iyon.

> [!IMPORTANT]
> Magtabi ng kopya ng mundo bago mo ito baguhin: tingnan ang [Mag-backup ng mundo bago mag-edit](guides/back-up-a-world.md).
> Sinusuri ng editor na tama ang pagbasa pabalik ng bawat save bago nito bitawan ang luma, pero hindi ito nagtatabi ng
> sarili nitong kopya.

## Pagsisimula

- [I-install ang editor](start/install.md): pag-download, unang pagsisimula sa Windows, Linux o macOS, at ang plugin
  para sa live na pag-edit.
- [Ang una mong edit](start/first-edit.md): magbukas ng kopya ng mundo, magtaas ng burol, magtanim ng mga puno,
  mag-save, at tingnan ito sa laro.
- [Ang una mong gusali](start/first-build.md): magtayo ng kubo sa Gawaan, i-save ito bilang blueprint at i-paste ito sa
  isang mundo.

## Mag-edit ng mga mundo

- [Mag-backup ng mundo bago mag-edit](guides/back-up-a-world.md)
- [Magbukas ng mundong wala sa listahan](guides/open-a-world.md)
- [I-edit ang mundong nilalaro mo](guides/edit-your-game-live.md)
- [I-edit ang dedicated server habang may naglalaro](guides/edit-a-server-live.md)
- [I-edit ang inuupahang server o server sa panel](guides/edit-a-rented-server.md)
- [Mag-undo ng hakbang, bawiin ang isang pagbabago, o itapon ang lahat](guides/undo-changes.md)
- [Ibalik ang isang area mula sa backup](guides/restore-from-backup.md)
- [Buuin ulit ang mga zone](guides/reset-zones.md)
- [Hanapin ang isang object, item sa chest o karatula](guides/find-things.md)
- [I-edit ang mundong ginawa gamit ang Expand World, Riverheim o Tunnelheim](guides/use-mods-worlds.md)

## Hubugin ang kalupaan

- [Itaas o hukayin lampas sa 8 m na hangganan ng laro](guides/raise-past-the-limit.md)
- [Gumawa ng kalsada o rampa paakyat ng burol](guides/make-a-road.md)
- [Humukay ng ilog o lawa](guides/make-a-river.md)
- [Linisin ang mga puno at bato, patagin ang puwesto ng gusali](guides/clear-an-area.md)

## Magtayo at maglagay

- [Magtanim ng gubat, bukid o bakod na halaman](guides/plant-a-forest.md)
- [Ilipat o kopyahin ang isang gusali](guides/move-a-building.md)
- [Baguhin ang laman ng chest o ang nakasulat sa karatula](guides/fill-a-chest.md)
- [I-paste ang blueprint sa isang mundo](guides/paste-a-blueprint.md)
- [Bumuo ng dungeon at ilagay ito sa mundo](guides/make-a-dungeon.md)
- [Maghanap ng seed na may gusto mo malapit sa simula](guides/find-a-seed.md)
- [Hayaang humubog o magtayo si Claude](guides/connect-claude.md)

## Mga tool

- [Ang window ng editor](editor-basics.md): ang panimulang pahina, ang top bar, ang tool rail, ang camera, ang area,
  pag-save, Kasaysayan, ang panel na Tanaw at Gupitin.
- [Ang mapa ng mundo](map.md): maghanap ng lugar, buksan ito sa 3D, maghanap sa buong mundo.
- [Lilok at pintura](terrain.md): ang mga brush sa lupa, mga stamp at Walang limitasyon.
- [Lupain](landform.md): isang buong anyong-lupa sa isang click, mula sa sarili mong pormula (mga burol, bunganga,
  talampas, moat), sa generator ng bundok (mga tuktok, tagaytay at bulkan lampas sa hangganan ng laro) o sa isang
  larawan.
- [Daan](path.md): mga kalsada, rampa at ilog sa kahabaan ng linya.
- [Mask](masks.md): limitahan ang kahit anong tool sa isang biome, taas, dahilig o pintura; nasa card din nito ang
  switch na Walang limitasyon.
- [Pagsukat at mga overlay](measure.md): mga layo, dahilig, kulay ng dahilig at linya ng taas.
- [Rehiyon, kopyahin at i-paste](area.md): patagin o pintahan ang isang kahon o polygon, piliin ang mga object nito,
  kopyahin at i-paste ito, patubuin ito muli o ibalik ito mula sa backup.
- [Paglagay](place.md): mga puno, bato, pananim at spawn point.
- [Pagtayo](build.md): ang mga piece na itinatayo ng mga player, isa-isa o sa kahabaan ng mga linya, gamit ang bar ng
  Pagtayo.
- [Pagpili](select.md): pumili, maglipat, mag-ikot, kumopya, magpalit at magbura ng mga object, at tingnan ang loob
  nila.
- [Ang Gawaan](workshop.md): magtayo sa isang blangkong plot at itago ito bilang blueprint.
- [Mga dungeon](dungeons.md): bumuo ng dungeon, o i-edit isa-isa ang mga silid ng laro.
- [Isang bagong mundo](new-world.md): tingnan ang mundo ng isang seed bago pa ito umiral, at gawin ito.
- [Live mode](live-mode.md): I-apply nang live, Auto, I-reload at ang mga player, habang tumatakbo ang laro.
- [Script](scripting.md): hubugin, pintahan at punuin ang isang area gamit ang mga C# script.
- [Claude](claude.md): ang kayang gawin ni Claude sa editor, at ang mga hangganan nito.

## Paano ito gumagana

- [Mga area at zone](concepts/areas-and-zones.md): kung bakit isang bloke ng mundo lang ang binubuksan ng editor bawat
  pagkakataon.
- [Pag-save at kasaysayan](concepts/saving-and-history.md): mga nakabinbing pagbabago, pag-save, pag-apply nang live,
  undo at mga backup.
- [Mga hangganan ng lupa](concepts/ground-limits.md): ang 8 m na hangganan ng laro at kung paano ito nilalampasan ng
  editor.
- [Live na pag-edit](concepts/live-editing.md): kung paano nakikipag-usap ang editor sa tumatakbong laro.
- [Mga object](concepts/objects.md): ano ang object, pagkapit, ang suri ng suporta, mga patakaran sa pagtubo, mga spawn
  point.
- [Mga dungeon sa laro](concepts/dungeons.md): kung paano iniingatan ng laro ang mga dungeon, at kung paano bumubuo ng
  isa ang generator.
- [Kaligtasan](concepts/safety.md): ang isinusulat ng editor, at kung bakit walang lumalabas sa computer mo.

## Tulong

- [Pag-troubleshoot](help/troubleshooting.md): ang gagawin kapag may hindi gumagana.
- [Mga tanong](help/faq.md): maiikling sagot sa pinakamadalas itanong.
- [Mga shortcut](help/shortcuts.md): bawat key at aksiyon ng mouse.
- [Glosaryo](help/glossary.md): ang mga salitang ginagamit ng editor.

## Para sa mga tagasalin at gumagawa ng mod

- [Pagsasalin](translating.md): ang mga catalogue ng wika, paano gumawa ng isa, at paano isalin ang dokumentasyong ito.
- [Para sa mga gumagawa ng mod](modding.md): ang natututunan ng editor mula sa mga mod, `worldeditor.json`, mga
  extension at add-on.
