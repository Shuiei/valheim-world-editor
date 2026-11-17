<!-- source: f7df77d8 -->
# Pagsasalin ng editor

Nasa mga catalogue ang mga salita ng editor, isa bawat wika: isang JSON file ng mga teksto ayon sa key. Ang English,
[`editor/Core/Lang/en.json`](../../editor/Core/Lang/en.json), ang pinagmulang isinasalin sa bawat ibang catalogue.

Dala ng editor ang English, French (`fr`), German (`de`), Spanish (`es`), Brazilian Portuguese (`pt-BR`, ipinapakita rin
sa computer na nakatakda sa Portuguese ng Portugal), Thai (`th`) at Filipino (`fil`, ipinapakita rin sa computer na
nakatakda sa Tagalog), at isinalin ang dokumentasyon sa bawat isa (`docs/fr/`…, kinunan ang mga larawan nito sa wikang
iyon). Ipinapakita nito ang wikang pinili sa **Mga setting → Wika**, bilang default ang sa computer kapag may catalogue
nito. Lumalabas sa English ang tekstong wala pa sa isang catalogue. Ginagamit ang bagong wika kapag nagsimula ulit ang
editor.

Hindi pa lahat ng teksto ay galing sa catalogue. Ang mga ito ay oo:

- bawat hover tip (pinapanatili ng mga tip ng Inspector na gawa mula sa data ng isang object ang mga pangalan nito gaya
  ng nasa save);
- ang panimulang pahina, Mga setting, ang mapa ng mundo, ang pahina ng Bagong mundo, at ang maliliit na dialog (mga
  tanong, mensahe, mga detalye ng isang blueprint) kasama ang mga salitang pinaghahatian nila;
- ang top bar ng 3D editor, kasama ang itinatanong at sinasabi ng pag-save, pag-apply, pagtatapon, pag-alis at
  pagsasara, ang mga mensahe ng live na laro at ang mga player;
- ang mga readout ng status bar, ang panel na Kasaysayan, ang panel na Tanaw, ang help card, at ang sinasabi ng status
  bar at ng Kasaysayan tungkol sa mga pagbabago ni Claude;
- ang mga tool: ang mga brush kasama ang mga stamp nila, Pagsukat, Mask, Script, Daan, Lupain, Pagpili, I-paste,
  Paglagay, Pagtayo (ang dock nito at ang bar ng Pagtayo), Rehiyon, ang tool na Dungeon kasama ang generator nito, ang
  panel na Blueprints, ang Gawaan, at ang Inspector;
- ang mga pangalan ng biome.

Inililipat ang iba nang window por window. Patuloy na gumagana ang catalogue na ginawa ngayon habang nadadagdagan ng mga
teksto: lumalabas sa English ang mga bago hanggang maisalin.

## Paggawa ng catalogue

1. Kopyahin ang `en.json` sa folder na `lang` ng data folder ng editor
   (`~/.local/share/ValheimWorldEditor/lang` sa Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` sa
   Windows; ipinapakita ito ng **Mga setting → Wika**), ipinangalan sa code ng wika: `fr.json`,
   `de.json`, `pt-BR.json`.
2. Isalin ang mga halaga, hindi kailanman ang mga key. Ang `lang.name` ay ang sariling pangalan ng wika ("Français"),
   gaya ng paglista nito sa Mga setting.
3. Panatilihin ang bawat `{0}`, `{1}`… : naglalagay ang editor ng halaga roon (isang folder, isang numero). Maaaring
   magbago ang pagkakasunod nila sa pangungusap. Hindi ginagamit ang tekstong iba ang mga puwesto sa English, o may
   ligaw na `{` o `}`.
4. Piliin ang wika sa **Mga setting**, i-restart ang editor at tumingin. Lumalabas sa English ang mga key na iniwan mo,
   o binura mo sa kopya mo.

Tumatanggap din ng mga pagwawasto ang parehong folder: ang file doon na may code ng isang catalogue na dala ng editor ay
ipinapatong dito, teksto por teksto. Doon, ang key na iniwan mo (o tekstong hindi ginagamit) ay pinapanatili ang dalang
salin; lumalabas lang ang English kung saan wala sa dalawa ang teksto.

Para madala ng editor ang isang catalogue, magbukas ng pull request na nagdadagdag nito sa `editor/Core/Lang/` (mga key
lang mula sa `en.json`; sinusuri ang mga ito ng `LangCatalogueTests`).

## Pagsasalin ng dokumentasyon

Ipinapakita ng Help window ang dokumentasyon sa wika ng editor kapag may salin ang pahina, at sa English kung wala.
Ang salin ng dokumentasyon ay isang folder ng `docs/` na ipinangalan sa code ng wika, gaya ng catalogue nito:
`docs/fr/`, `docs/pt-BR/`. Ang sarili nitong `README.md` ang gumagawa rito na salin (ang unang pahina, at ang nilalaman
ng Help window); walang ganoon ang mga English na folder (`guides/`, `help/`, `concepts/`, `start/`).

- **Mga pahina.** Ginagaya ng folder ang mga English na pahina: isinasalin ng `docs/fr/map.md` ang `docs/map.md`, at
  isinasalin ng `docs/fr/guides/make-a-road.md` ang `docs/guides/make-a-road.md`. Isalin ang mga pahinang dala ng Help
  window (inililista ng [writing-docs.md](../dev/writing-docs.md) ang mga hindi nito dala). Lumalabas sa English ang
  pahinang hindi pa naisasalin, kaya maaaring magsimula ang salin sa ilang pahina.
- **Mga link.** Isulat ang mga ito gaya ng pagsulat ng English na pahina, mula sa folder: `map.md#…` at `guides/x.md`
  mula sa `docs/fr/README.md`, `../area.md` mula sa `docs/fr/guides/`. Ang link na papalabas ng `docs/` ay umaakyat nang
  isa pang `../` kaysa sa English (`../../README.md`, `../../editor/Core/Lang/en.json`), at ganoon din ang link sa file
  ng `docs/` na hindi isinasalin (`../worldeditor.schema.json`). Gumagana pa rin sa editor ang mga link sa pahinang wala
  pa sa folder (ipinapakita nito ang English na pahina), pero hindi sa GitHub hangga't hindi naisasalin ang pahina.
- **Mga larawan.** Kinukunan ulit sa wika mo ang larawang may mga salita sa `docs/fr/images/`, sa parehong pangalan, at
  isinusulat na `images/x.jpg`. Ang larawang walang isasalin ay maaaring manatiling English: `../images/x.jpg`. Hangga't
  hindi naidadagdag sa folder mo ang isang larawan ng `images/`, ipinapakita ng Help window ang English na kapangalan
  nito.
- **Mga heading.** Isalin ang mga ito; nagbabago kasama nila ang mga id nila (ginagawa ito ng GitHub mula sa mga salita:
  nagiging `#rechercher-dans-le-monde` ang `#search-the-world`), at ginagamit ng sariling mga link ng salin ang mga
  isinaling id, gaya ng kailangan ng GitHub. Panatilihin ang mga heading ng English na pahina: kasindami, sa parehong
  antas, sa parehong pagkakasunod. Dinadala rin ng editor ang mga English na id sa mga isinaling heading nila, kaya
  napupunta sa tamang lugar ang mga help button nito (`F1`, ang `?` ng mga panel), na pumapangalan sa mga English na
  heading.
- **Source mark.** Maglagay ng linya sa pahina, una sa lahat, na nagsasabi kung saang English na teksto ito isinalin:
  `<!-- source: 1a2b3c4d -->`, ang unang 8 character ng SHA-256 ng English na pahina (hindi ito ipinapakita ng GitHub o
  ng Help window). Kunin ang mga ito gamit ang `sha256sum docs/map.md | cut -c1-8` (macOS: `shasum -a 256`). Kinukuwenta
  ang mark sa pahinang may Unix line ends: sa Windows checkout na ginawa itong `\r\n`, gamitin ang
  `git show HEAD:docs/map.md | sha256sum | cut -c1-8`, na nagbibigay ng pahina gaya ng pag-iingat dito ng repository.

Kapag nagbago ang English na pahina, nagbabago ang mark nito: ipinapakita pa rin ng Help window ang salin, may paalala na
nagbago ang pahina sa English at may button na nagpapakita ng English na pahina. Para i-update ang salin, tingnan ang
nagbago sa English mula noon (`git log -p docs/map.md`), isalin ito, at isulat ang bagong mark. Sinusuri ng mga test ang
bawat link at larawan ng isang salin, at na nasa up-to-date na salin ang mga heading ng English na pahina nito.

## Para sa mga developer

- Ang tekstong nakikita ng user ay napupunta sa `en.json` sa ilalim ng key na ipinangalan sa kung saan ito ipinapakita
  (`settings.port`, `common.cancel` para sa mga salitang ginagamit kahit saan) at hinihingi gamit ang
  `Lang.T("settings.port")`, o `Lang.T("settings.worldNotFound", path)` na may mga halaga para sa mga puwestong
  `{0}`… nito (inilalagay gamit ang invariant culture). Ang hover tip ay isang `tip.` na key, ibinibigay gamit ang
  `.Tip("settings.port")` (tingnan ang `editor/Desktop/Tips.cs`).
- Isulat nang buo ang key sa code, kahit ipinapasa ito: sinusuri ng `LangCatalogueTests` na nasa `en.json` ang bawat key
  na hinihingi at hinihingi sa kung saan ang bawat key ng `en.json`.
- Ang mga teksto para kay Claude (mga paglalarawan at sagot ng mga MCP tool), ang log, ang output ng command line at ang
  mga format ng file ay nananatiling English at nasa labas ng catalogue. Ang tekstong ipinapakita sa user na ibinibigay
  rin kay Claude (ang gastos ng isang piece) ay humihingi ng English gamit ang `Lang.En` sa panig ni Claude.
- Isang beses binubuo ang mga window, sa wikang kinuha sa pagsisimula (`Lang.Use` sa `Program.Main`); walang kailangang
  makinig sa pagbabago ng wika.

### Pagpapangalan ng mga key

- Ang unang bahagi ay ang window, binabaybay gaya ng mga tip key nito (`area`, `place`, `select`, `path`,
  `blueprints`, `newWorld`…). Pinapanatili ng tool na pinalitan ng pangalan sa window ang mga key nito: `area.*` ang sa
  tool na Rehiyon, at pinapanatili ng mga uri ng tool na Lupain ang `shape.*` (Pormula) at `mountain.*` sa tabi ng
  `landform.*`. Mga pinaghahatiang prefix: `common.*` para sa salitang ginagamit sa tatlo o higit pang window (OK, Oo,
  Kanselahin, I-save, Itapon, Manatili, Panatilihin ang mga ito…), `dialog.*` para sa maliliit na dialog, `save.*` para
  sa mga tanong sa pag-save, pag-apply, pagtatapon at pag-alis, `biome.*` para sa mga pangalan ng biome.
- Sinasabi ng gitnang bahagi kung anong uri ng teksto ito: `label` (mga label ng row, button, switch), `head` (mga
  heading ng seksiyon), `hint` (mahinang paliwanag), `msg` (status bar at mga resulta), `undo` (mga label ng
  Kasaysayan), `error` (mga problemang ipinapakita sa panel), `ask.<name>.title|text|yes|no` (mga tanong), o ang pangalan
  ng listahan para sa mga entry nito (`brush.falloff.flatTop`). Maaaring dalawang bahagi lang ang mga nag-iisang teksto
  ng isang window (`settings.port`). camelCase ang huling bahagi.
- Isang key para sa isang English na salita: gamitin ulit ang `common.*` sa halip na idagdag ulit ang "Save". Inaalam
  mula sa English (`Lang.EnglishOf`) ang mga pangalan ni Claude para sa mga kontrol ng window, at kailangan iyon nito.
- Pinapanatili ng tekstong inilipat sa catalogue ang pananalita nito; baguhin ang mga salita sa sarili nitong pagbabago.
- Ang mga teksto ng isang enum ay galing sa `switch` na pumapangalan sa bawat buong key, hindi kailanman sa
  `"prefix." + value`.

### Mga halaga, plural at pangungusap

- Napupunta ang mga halaga sa `{0}`, `{1}`… kasama ang format at unit nila sa loob ng teksto (`"{0:0.0} m"`,
  `"{0} × {1} zones"`), bilang hilaw na numero at pangalan, hindi bilang naka-format na string. Laging hinihingi ang
  isang key na may parehong bilang ng halaga; dinodoble ng tekstong hinihingi na may mga halaga ang mga literal na brace
  nito (<code>&#123;&#123;</code>), at walang ganoon kailanman ang hinihingi nang walang halaga.
- Ang pangalang nasa wika na ng user (isang biome, isang uri ng object) ay pumapasok bilang halaga. Huwag baguhin ang
  case nito para magkasya sa pangungusap (`ToLowerInvariant`): isulat ang pangungusap sa paligid nito.
- Walang mga patakaran sa plural. Panatilihin ang "object(s)" sa iisang teksto; kung saan pumipili na ang code sa
  pagitan ng isa at marami, gumamit ng dalawang buong key (`….one`, `….many`). Huwag kailanman magdagdag ng hulapi sa
  code.
- Isang key bawat buong pangungusap. Ang opsiyonal na dagdag na pangungusap ay sariling key, pinagdudugtong ng espasyo;
  huwag kailanman pagdikitin ang mga bahagi ng isang pangungusap. Pinagdudugtong ng ", " ang mga listahan ng isinaling
  pangalan.

### Ang nananatiling English

- Lahat ng binabasa ni Claude: ang mga paglalarawan at sagot ng mga MCP tool (`ClaudeTools*`, `ClaudeGuide`), ang log at
  console, ang output ng command line, mga format ng file (`prefs.json`, ang history file, mga blueprint), data ng laro
  (mga pangalan ng prefab, item at piece, mga key ng data ng object), ang mga pangalan ng produkto at mod sa loob ng mga
  teksto (Valheim, Homestead, Steam), at ang mga mensahe ng exception (isinasalin ang pangungusap sa paligid nito).
- Nagsisimula ang mga hakbang ni Claude sa Kasaysayan sa markang `Claude: ` (`ClaudeTools.ClaudesStep`), na nananatiling
  English: nahahanap ng Bawiin ang mga pagbabago ni Claude ang mga hakbang ni Claude ayon dito. Isinasalin ang mga
  salita pagkatapos nito.
- Ang tekstong ipinapakita sa user na ibinibigay rin kay Claude ay may dalawa: ang wika ng user mula sa `Lang.T`, ang
  English ni Claude mula sa `Lang.En` (`PieceCost.Describe(english: true)`).
- Binabasa pabalik ni Claude ang mensahe ng status bar. Ilagay ito roon gamit ang `Msg("area.msg.applied", n)`,
  o `Msg(said)` na may `Said` (`Said.Of(key, values…)`, `Said.Raw(text)` para sa tekstong pareho sa
  dalawa, `+` para magdagdag ng pangungusap): ipinapakita ng window ang wika ng user at pinapanatili ang English, na
  binabasa ng mga sagot ni Claude (`MainWindow.LastMessageEn`). Magdagdag sa huling mensahe gamit ang
  `LastSaid + …`, hindi kailanman sa pagbasa ng `MessageText.Text`.
- Iniingatan sa English ang natandaang pagpili sa listahan (`Prefs.Bind`), kaya bumabalik ito sa kahit anong wika.

### Pagsusuri ng isang window

May gawa-gawang wika ang mga test, ang `PseudoLang`, na binabalot ang bawat teksto ng catalogue bilang `⟦…⟧`.
Inililista ng `PseudoLang.Untranslated(window)` ang mga tekstong ipinapakita ng isang window sa labas ng `⟦⟧`, ibig
sabihin, hindi galing sa catalogue. Bawat window ay may test na `<Window>LangTests` na binubuo ito sa wikang iyon at
inaasahang walang ganoon; tumatakbo ang mga ganoong test sa koleksiyong `DataDir`, dahil pinaghahatian ang wika.
