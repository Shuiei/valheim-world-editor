<!-- source: 1ccdce30 -->
# Para sa mga gumagawa ng mod

Gumagana ang editor sa mga building mod nang walang kailangan mula sa mga gumawa nito: kapag nakakonekta sa larong may
mod, natututunan nito ang mga object ng mga mod, iginuguhit sila gamit ang sarili nilang mga model at pinapangalanan ang
data na iniingatan nila. Para ang pahinang ito sa mga gumagawa ng mod na may gusto pang higit: isang opsiyonal na file na
`worldeditor.json` para sa hindi mahulaan ng editor, **mga magkakabit na object** para sa mga mod na bumubuo ng iisang
bagay mula sa maraming object, **mga extension ng editor** (isang maliit na DLL na binuo laban sa SDK ng editor) para sa
hindi masabi ng isang file, **mga add-on** (pareho, ginawa ng kahit sino para sa mod ng iba), at **mga bridge hook**
para makilahok ang sariling plugin ng isang mod sa live mode.

Opsiyonal ang lahat ng narito, at nagdadagdag ang bawat hakbang sa nauna. Walang dinadala ang editor para sa kahit anong
mod: nasa gumawa ng mod ang pagsuporta sa mod sa editor, gamit ang mga ito. Ang mga halimbawa sa pahinang ito ay sa
isang gawa-gawang mod, ang "Example Vehicles", na kasama ng SDK ang buong sample (tingnan ang
[Ang sample na Example Vehicles](#ang-sample-na-example-vehicles)).

## Ang kusang natututunan ng editor

| Ano | Paano ito natututunan ng editor |
|---|---|
| Ang mga object ng mga mod: mga pangalan, piece ng gusali, gastos, crafting station, ang tool na nagtatayo ng bawat isa | Kapag nakakonekta sa isang laro (live mode, WorldEditorBridge 1.26.0 o mas bago), hinihingi ng editor sa laro ang bawat object na alam nito at iniingatan ang mga hindi sariling object ng Valheim. Iniingatan sila sa `mod-catalogue.json` sa data folder ng editor, kaya ipinapakita rin sila ng mundo ng server na may mod na binuksan sa ibang pagkakataon mula sa mga file nito, offline. Pinapalitan ng mas bagong ulat ng mod tungkol sa isang piece ang mas luma; hindi kailanman binabago ng mod ang sariling object ng Valheim. |
| Ang itsura nila | Sinasabi ng laro ang iginuguhit nito sa bawat object ng mod (WorldEditorBridge 1.27.0 o mas bago); hinahanap ng editor ang mga mesh, material at texture na iyon sa sariling mga file ng mga mod sa computer ng player (ang mga asset bundle nila, kinopya nang isang beses mula sa mga DLL papunta sa `mod-bundles` sa data folder). Hindi mahahanap ang mga mesh na binubuo ng mod sa code habang tumatakbo ang laro: walang texture ang mga object na iyon, maliban kung iginuguhit sila ng isang [extension](#mga-extension). |
| Ang mga pangalan ng data na iniingatan ng mga mod sa mga object | Hash lang ng bawat data key ang sine-save ng laro. Hinahanap ng editor ang mga pangalan sa tekstong nakasulat sa mga DLL ng mga mod (binabasa bilang byte, hindi kailanman pinapatakbo) sa mga plugins folder na alam nito, at iniingatan sila sa `mod-zdo-keys.json` hanggang magbago ang isang DLL. Hindi mahahanap sa ganitong paraan ang key na binubuo ang pangalan habang tumatakbo ang laro (`"Corner" + i`): pangalanan ito sa ilalim ng `data` sa `worldeditor.json` mo (sa ibaba), kung hindi ay ipinapakita ito ng inspector bilang `#` at ang hash nito. |
| Kung saang mod galing ang isang object | Sinasabi ito ng Inspector (kapag naka-install ang Jötunn, na alam ito; kung hindi ay "Mula sa isang mod"). Inililista ng dock ng [Pagtayo](build.md), sa mundo at sa Gawaan, ang mga piece ng mga mod sa tab nitong **Mga mod piece**, isang mod bawat pagkakataon. |

Tingnan ang [Mga larong may mod](live-mode.md#mga-larong-may-mod) para sa panig ng player.

## worldeditor.json

May ilang bagay na hindi mahulaan ng editor. Maaaring sabihin ang mga ito ng mod sa file na `worldeditor.json` na kasama
nito, kahit saan sa folder nito sa ilalim ng `BepInEx/plugins` (ayos lang sa tabi ng DLL nito). Ito ang file ng Example
Vehicles:

```json
{
  "$schema": "https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/worldeditor.schema.json",
  "version": 1,
  "extension": "ExampleVehicles.WorldEditor.dll",
  "gameComponents": ["Ship"],
  "data": {
    "ExampleVehicles_Vehicle": { "label": "Vehicle", "readOnly": true, "help": "The vehicle this part is built on." },
    "ExampleVehicles_Offset": { "label": "Place on the vehicle", "readOnly": true },
    "ExampleVehicles_Packed": { "label": "Linked objects", "help": "The objects this one is tied to." }
  },
  "prefabs": {
    "example_boundary": { "hidden": true },
    "example_fleet": { "hidden": true, "data": true },
    "example_canal": { "hidden": true, "data": true },
    "example_hull": { "group": "Hulls" },
    "example_sail": { "group": "Sails and cables" },
    "example_post": { "group": "Sails and cables" },
    "example_pit": { "group": "Digging" }
  }
}
```

| Key | Ang ginagawa rito ng editor |
|---|---|
| `version` | Ang bersiyon ng format: `1`. Nilalaktawan nang buo ang file na mas bagong bersiyon, o walang bersiyon. |
| `data` | Ayon sa data key, gaya ng pagpapangalan dito ng code mo (ang pangalang ang hash ay sine-save ng laro; nakikilala ayon sa pangalan nito ang key na pinangalanan dito kahit hindi ito hawak ng DLL mo bilang teksto): `label`, kung paano pinapangalanan ng [inspector](select.md#inspector) ang halaga (kung wala, ang key nito; nauuna ang sariling mga pangalan ng editor para sa mga key ng laro); `readOnly`, ipinapakita ng inspector ang halaga pero hindi ito pinapayagang baguhin (mga halagang mismong itinatakda ng mod mo at masisira kapag na-edit); `help`, kung ano ito, ipinapakita kapag itinuro ang halaga. |
| `prefabs` | Ayon sa pangalan ng prefab: `hidden`, hindi iniaalok sa listahan ng [Paglagay](place.md) o sa dock ng [Pagtayo](build.md) (ang mga tool, epekto, katulong mo; lumalabas pa rin ang mga object nito na nasa mundo na); `group`, ang heading na kinalilistahan ng piece mo sa tab na **Mga mod piece** ng dock ng Pagtayo, pagkatapos ng pangalan ng mod ("*pangalan ng mod* · Hulls"). Kapag walang group, nakalista ang piece sa ilalim ng crafting station nito, kung hindi ay sa ilalim ng "Walang kailangang station". Para lang sa sariling mga piece ng mga mod ang `group`. `data`: data lang ng mod mo ang hawak ng mga object nito (isang talaan, ang hinukay sa isang lugar) at walang makikita: hindi kailanman iginuguhit, pinipili o inililista ng editor ang mga ito sa isang area (binabasa pa rin sila ng extension mo), at pinapanatili sila ng [reset ng zone](area.md) kapag pinapanatili nito ang mga gusali, at tinatanggal sila kasama ng lahat ng iba kung hindi. `piece`: piece ng gusali na ginagawa ng mod mo sa code, na walang bundle na may hawak: `{ "name": "Track 2 m", "cost": { "Stone": 4 }, "station": "Stonecutter" }` (kailangan ang `name`, hanggang 64 na character; `cost` ayon sa pangalan ng prefab ng item, hanggang 16 na item; `station` ayon sa English na pangalan nito). Inililista ito ng tab na **Mga mod piece** ng dock ng Pagtayo sa ilalim ng mod mo kahit walang live na laro (nananaig ang sinasabi ng live na laro tungkol dito). Hindi kailangan ng mga piece sa mga bundle mo: hinahanap sila ng editor ayon sa `Piece` nila, o ayon sa mga snap point nila kapag idinadagdag mo ang `Piece` sa code. |
| `links` | Kung paano nagkakabit ang mga object mo para maging isa: tingnan ang [Mga magkakabit na object](#mga-magkakabit-na-object). |
| `ties` | Kung paano nakatali ang mga object mo sa isang pumapangalan sa kanila (mga bagon ng tren): tingnan ang [Mga nakataling object](#mga-nakataling-object). |
| `extension` | Ang DLL ng extension ng editor mo, kaugnay sa folder ng file at nasa loob nito: tingnan ang [Mga extension](#mga-extension). |
| `gameComponents` | Mga pangalan ng class ng sariling mga script ng laro na kailangan ng extension mo ang mga field (`"TerrainOp"`, `"Ship"`; hanggang 32): habang tumatakbo ang extension mo, binabasa ng editor ang bawat component nila sa mga prefab ng laro mula sa Valheim ng player (isang beses, saka iniingatan hanggang magbago ang laro) at ibinibigay ang mga ito bilang `Host.Game` (tingnan ang [Mga hook](#mga-hook)). |
| `settingsFiles` | Mga pangalan ng file ng mga BepInEx settings file ng mod mo na binabasa ng extension mo (`"author.mod.cfg"`, sa `BepInEx/config`; hanggang 8): habang nagbubukas ang mundo, binabasa sila ng editor mula sa unang BepInEx folder ng player na may bawat isa at, kapag live, mula sa sarili ng nakakonektang laro sa pamamagitan ng bridge nito (ang sa server, na ang mga naka-sync na setting ay nakukuha ng mga player nito). Binabasa ng extension mo ang isang setting gamit ang `Host.Setting(file, section, key)` (tingnan ang [Mga hook](#mga-hook)). |
| `worldGenerators` | Ang mga world generator na inirerehistro ng extension mo (tingnan ang [Mga world generator](#mga-world-generator)), ayon sa id na pinapangalanan ng mga file ng mga mundo nila, bawat isa may pangalan ng mod mo para sa player: `{ "author.mod": "Some Mod" }` (hanggang 8). Binabasa ito ng editor kahit hindi tumatakbo ang extension mo, para masabi ng mundong ginawa ng mod mo kung bakit hindi ito mabuksan: naka-off ang add-on, naghihintay sa mod mo, hindi tumatakbo, o hindi naka-on ang extension ng mod mo. |

### Paano ito binabasa ng editor

- **Saan.** Sa bawat `plugins` folder na alam ng editor: ang `BepInEx` ng folder ng Valheim, ang profile ng mod manager
  na pinag-install-an mismo ng editor, ang mga folder na idinagdag sa **Mga setting → Mga BepInEx folder**, at bawat
  profile ng r2modman at Thunderstore Mod Manager. Kasama ang bawat file na may pangalang `worldeditor.json` doon, sa
  kahit anong lalim.
- **Kailan.** Tuwing nagbubukas ang isang mundo o ang Gawaan, at kapag nagbukas ang **Mga setting**. Ang file na nabago
  habang bukas ang isang mundo ay kasama sa susunod na pagkakataon.
- **Aling mod.** Pag-aari ng file ang mod na ang folder ay nasa mismong ilalim ng `plugins` (ang `ExampleVehicles` ng
  `plugins/ExampleVehicles/worldeditor.json`). Pinapangalanan ng Mga setting ang mga extension ayon dito.
- **Dalawang file, iisang pangalan.** Binabasa ang mga file mula sa pinakaluma (ayon sa oras ng huling pagbabago nila):
  kapag may sinasabi ang dalawa tungkol sa parehong data key, prefab o link `id`, nananaig ang pinakabagong file.
- **Mapagpatawad.** Pinapayagan ang mga comment at trailing comma. 1 hanggang 200 character ang mga teksto; hanggang 32
  pangalan ang hawak ng mga listahan; hanggang 1 MB ang isang file. Iniiwan ang entry na wala sa ganitong anyo at kasama
  pa rin ang natitirang bahagi ng file. Nilalaktawan ang file na hindi JSON, o wala sa bersiyong nababasa ng editor;
  sinasabi ito ng log ng editor ("mod manifest …: not a worldeditor.json this editor reads, skipped.").

Inilalarawan ng [schema](../worldeditor.schema.json) ang file: kasama ang linyang `$schema` sa itaas, sinusuri ng mga code
editor ang file at kinukumpleto ang mga key nito habang nagta-type ka.

## Mga magkakabit na object

Kung bumubuo ang mod mo ng mga object mula sa iba (barko at ang mga piece na itinayo rito, kariton at ang karga nito),
sine-save ng laro nang hiwalay ang bawat piece, at nakikita ng editor ang mga hiwalay na piece, nakatambak kung saan sila
sine-save ng laro. Sabihin dito kung paano sila nagkakabit at iguguhit sila nito gaya ng pagbuo sa kanila ng mod mo, at
ililipat, kokopyahin at buburahin nang buo ang bawat grupo (tingnan ang
[Mga magkakabit na object](select.md#mga-barko-at-iba-pang-magkaugnay-na-object) para sa nakikita ng player). Gumagana ito para sa
mga mod na nagkakabit sa karaniwang paraan: may sariling int id ang root (at ang kahit anong bahaging kinasasabitan ng
iba); iniingatan ng bawat bahagi, sa data nito, ang id ng kinasasabitan nito at ang offset at ikot nito sa frame niyon.

```json
{
  "version": 1,
  "links": [
    {
      "name": "vehicle",
      "id": "ExampleVehicles_Id",
      "parents": ["ExampleVehicles_Mount", "ExampleVehicles_Vehicle"],
      "offset": "ExampleVehicles_Offset",
      "rotation": "ExampleVehicles_Turn",
      "rotationQuaternion": "ExampleVehicles_TurnQ",
      "storedAt": "root",
      "references": ["ExampleVehicles_Target", "ExampleVehicles_Drives"],
      "dropOnCopy": ["ExampleVehicles_Packed", "ExampleVehicles_Aboard"]
    }
  ]
}
```

| Key | |
|---|---|
| `id` | Int na data key ng sariling id ng isang object, na pinapangalanan ng mga bahagi nito. Kailangan. |
| `parents` | Mga int na data key na pumapangalan sa kinasasabitan ng isang bahagi, nananaig ang unang nakatakda (pinapangalanan ng piece sa swivel ang swivel bago ang sasakyan). Kahit isa. Kailangan. |
| `offset` | Vec3 na data key ng puwesto ng bahagi sa frame ng kinasasabitan nito, sa metro. Kailangan. |
| `rotation` | Vec3 na data key ng ikot nito sa frame na iyon, bilang Unity Euler angles sa degree. |
| `rotationQuaternion` | Quaternion na data key ng ikot na iyon, ginagamit kapag hindi naka-store ang `rotation`. Kapag wala ang dalawa, hindi nakaikot ang bahagi sa frame ng parent nito. |
| `storedAt` | Kung saan sine-save ng laro ang isang bahagi: `root` (sa root ng grupo nito; binubuo ito ng mod mo mula sa offset nito kapag nilo-load ang grupo; ang default) o `own` (kung saan ito nakatayo). Sa parehong paraan sine-save ng editor ang mga inilipat na bahagi. |
| `references` | Iba pang int na data key na pumapangalan sa id ng isang object (ang palo ng layag, ang target ng switch): sa na-paste na kopya, sinusundan nila ang mga bagong id ng kopya, at inaalis kapag hindi nakopya ang pinapangalanan nila. |
| `dropOnCopy` | Mga data key na iniiwan ng na-paste na kopya: mga id na naka-pack sa paraang hindi kayang isulat ulit ng editor (isang byte array), mga tali na hindi dapat madala (ang nakasakay). |
| `placements` | Ayon sa parent key (isa sa `parents`), kung paano inilalagay ang bahaging nakasabit dito kapag hindi ayon sa sinasabi ng patakaran: ang sarili nitong mga key na `offset`, `rotation` at `rotationQuaternion` at `storedAt` (bawat isang hindi ibinigay: ang sa patakaran), gaya ng pag-iingat ng layag sa puwesto nito sa palo nang hiwalay sa puwesto nito sa barko; o `"carried": true` (at wala sa mga iyon), bahaging dala ng grupo kung saan ito sine-save ng laro (kariton na nakasakay): walang offset na iniingatan ito, iginuguhit kung saan ito naka-save kaugnay sa nagdadala rito, at inililipat, iniikot, kinokopya at binubura kasama ng grupo at sine-save kung saan ito nakatayo. |
| `name` | Ang tawag sa isang grupo sa mga mensahe ng editor ("isang buong sasakyan"). Default: `group`. |

### Paano ginagamit ng editor ang mga link

- **Pagguhit.** Iginuguhit ang bahagi sa offset nito sa frame ng parent nito, ang parent kung saan ito inilalagay ng
  sarili nitong parent, at iba pa hanggang sa root, na nakatayo kung saan ito naka-save: umiikot at kumikiling ang mga
  piece ng barko kasabay ng katawan nito. Nananatili ang dinadalang bahagi kung saan ito naka-save kaugnay sa nagdadala
  rito. Kayang iikot ng extension ang frame na kinasasabitan ng mga bahagi ng isang parent, o mismong ilagay ang isang
  bahagi (`AddLinkPlacer`, tingnan ang [Mga hook](#mga-hook)). Nananatili kung saan ito naka-save ang bahaging wala sa
  mundo ang parent nito (o ang kahit anong parent pataas sa kadena nito), o ang kadena nito ay umiikot o lampas 16 ang
  lalim (sirang data).
- **Aling patakaran.** Kinukuha ng object ang unang patakarang hawak nito ang mga key na `id` o `parents`. Walang epekto
  ang mga pangalan ng prefab: ang data lang. Pinapalitan ng patakaran ng mas bagong file na may parehong `id` ang sa mas
  luma, sa puwesto nito sa pagkakasunod.
- **Paglipat at pagbura.** Kapag pinili ang kahit anong bahagi at inilipat, iniikot o binura, kasama ang buong grupo.
  Pinapanatili ng inilipat na grupo ang taas nito at hindi ito kumakapit; sine-save ang mga bahagi nito ayon sa sinasabi
  ng `storedAt`.
- **Pagkopya.** Kinukuha ng kopya ang buong grupo. Nakakakuha ang bawat paste (at bawat ulit nito) ng mga bagong id na
  walang object sa mundo ang may hawak, kahit ang mga binura, kaya hindi kailanman itinuturing ng laro na iisa ang
  dalawang kopya; sumusunod ang bawat halaga ng parent at `references`. Ang bahaging kinopya nang wala ang parent nito ay
  pine-paste bilang karaniwang object (inalis ang mga link nito), kung saan ito na-paste. Ini-mirror din ng naka-mirror
  na paste ang mga offset at ikot ng mga bahagi. Nawawala ang mga halaga sa `dropOnCopy`, maliban kung hinahawakan sila
  ng copy rewriter ng isang extension.
- Pinapangalanan ang mga data key gaya ng pagpapangalan sa kanila ng code mo, gaya sa `data`.

## Mga nakataling object

May mga mod na nagtatali ng mga object nang hindi iniingatan kung saan nakatayo ang bawat isa mula sa iba: pinapangalanan
ng tren ang mga bagon na hinihila nito, walang pinapangalanan ang deck pero pinapangalanan ng itinayo rito ang deck.
Sine-save ng laro ang bawat object kung saan ito nakatayo, at pinagsasama-sama ulit sila ng mod mo habang tumatakbo ito.
Sabihin sa editor kung paano sila nakatali at pananatilihin nito silang iisang grupo:

```json
{
  "version": 1,
  "ties": [
    { "name": "train", "root": "ExampleVehicles_Wagons", "member": "ExampleVehicles_Self", "idBytes": 12 },
    { "name": "deck", "root": "ExampleVehicles_DeckId", "member": "ExampleVehicles_OnDeck" }
  ]
}
```

| Key | |
|---|---|
| `root` | Data key ng mga id na pinapangalanan ng root: isang int, long, o byte array na may hawak na ilang id nang sunud-sunod. Kailangan. |
| `member` | Data key ng sariling id ng isang member, ng parehong uri (int, long, o byte array). Kailangan. |
| `idBytes` | Para sa byte array, ilang byte ang isang id (1 hanggang 64; 12 para sa mga object id ng laro). Kapag wala ito, iisang id ang buong halaga. |
| `name` | Ang tawag sa isang grupo sa mga mensahe ng editor. Default: `group`. |

Nasa grupo ng root ang member kapag ang sarili nitong id ay isa sa pinapangalanan ng root. Ikinukumpara ang mga id bilang
mga byte, anuman ang ibig sabihin nila sa mod mo: hindi kailanman sila hinahanap ng editor sa laro. Iisang grupo ang mga
grupong may pinaghahatiang object: dinadala ng tren na ang flatbed ay root ng isang deck ang itinayo sa deck.

- **Pagpili, paglipat at pagbura.** Kapag pinili ang kahit anong object ng isang grupo at inilipat o iniikot, kasama ang
  buong grupo, bilang iisang bloke: pinapanatili ng bawat member kung saan ito nakatayo mula sa iba, at sine-save kung
  saan ito nakatayo. Kapag binura ang root, kasama ang mga member nito; kapag binura ang isang member, iyon lang.
- **Pagkopya.** Kinukuha ng kopya ang buong grupo. Nakakakuha ang bawat paste (at bawat ulit nito) ng mga bagong id na
  parehong uri at laki, random, sa root at sa mga member na pinapangalanan nito, kaya hindi kailanman kinukuha ng kopya
  ang mga member ng orihinal: pinapanatili ng listahan ang mga id ng mga member na nakopya, laging bago ang iisang id, at
  nawawalan ng id ang member na hindi nakopya ang root.
- **Live at naka-save.** Ang mod mo ang bahala sa mga bagong id: hinahanap nito ang mga member ng na-paste na root ayon
  sa mga id na hawak nila, gaya ng ginagawa nito pagkatapos mag-load ng laro ng isang mundo (binibigyan ng laro ang bawat
  object ng bagong object id sa bawat load).
- Pinapalitan ng patakarang may parehong mga key na `root` at `member` sa mas bagong file ang sa mas luma, sa puwesto
  nito sa pagkakasunod.

## Mga extension

Ang hindi masabi ng isang file, kaya ng code. Maaaring magdala ang mod ng **extension ng editor**: isang maliit na DLL,
binuo laban sa SDK ng editor, na nilo-load ng editor para basahin at iguhit ang mga object ng mod. Hindi ito tumatakbo sa
laro, at hindi kailanman nito nakikita ang code ng laro o ng Unity: ang data lang ng mga object, gaya ng hawak ng editor.
Kaya nitong:

- ipakita sa mga salita ang halagang byte array sa Inspector;
- isulat ulit sa mga na-paste na kopya ang mga id na pina-pack ng mod mo sa sarili nitong anyo;
- itakda ang inaalam ng mod mo mula sa kinatatayuan ng isang object tuwing inilalagay, ipine-paste, inililipat o
  iniikot ito ng editor (ang riles sa ilalim ng sasakyan, isang ikot na iniingatan ng mod mo);
- iguhit ang binubuo ng mod mo sa code habang tumatakbo ang laro (layag mula sa mga sulok nito, kable mula poste
  hanggang poste);
- ilagay ang mga bahagi ng mga magkakabit mong object kung saan sila binubuo ng mod mo kapag hindi ito sinasabi ng data
  nila lamang (ang mga bahagi sa swivel na iniikot ng mod mo);
- hindi iguhit ang lupa kung saan naghuhukay ang mod mo (bunganga ng tunnel, hukay);
- iguhit ang ginagawa ng mod mo sa isang area bilang kabuuan (kanal, batong hinukay mula sa burol) gamit ang itsura ng
  lupa ng laro, kapalit ng lupa ng editor;
- basahin ang mga setting ng sariling mga script ng laro (ang `gameComponents` nito), gaya ng nasa Valheim ng player;
- basahin ang sariling mga setting ng mod mo (ang `settingsFiles` nito), gaya ng nasa laro ng mundo;
- magdagdag ng sarili mong tool sa rail ng 3D editor, kasama ang card ng mga setting nito, ang preview nito at ang mga
  edit nito sa kasaysayan (paghuhukay ng kanal kung saan nag-click ang player);
- para sa mod ng world generation, basahin at iguhit ang mga mundong ginagawa nito, at gumawa ng mga bago gamit ito;
- sa live mode, hingin sa sariling plugin ng mod mo sa laro ang laro lang ang nakakaalam (tingnan ang
  [Live mode](#live-mode)).

### Paggawa ng isa

1. **Kunin ang SDK** mula sa isang [release](https://github.com/Shuiei/valheim-world-editor/releases):
   hawak ng `ValheimWorldEditor.Sdk-<version>.zip` ang `ValheimWorldEditor.Sdk.dll` (netstandard2.0, ang binubuuan ng
   extension; hawak din ng net10.0 build nito sa `net10.0/` ang world-rules model, para sa extension sa .NET ng editor
   na gumagawa ng mga patakaran ng mundo) at ang `WorldEditorBridge.Api.dll` (ang binubuuan ng plugin ng isang mod para
   sa [live mode](#live-mode)), bawat isa may file ng dokumentasyon nito para sa mga code editor, ang pahinang ito bilang
   `README.md`, at ang buong sample na Example Vehicles sa `sdk/samples/` (nabubuo ito nang ganoon lang).
2. **Bumuo ng netstandard2.0 class library** (net10.0, laban sa `net10.0/ValheimWorldEditor.Sdk.dll`, para sa gumagawa
   ng mga patakaran ng mundo) na tumutukoy sa `ValheimWorldEditor.Sdk.dll` nang hindi ito kinokopya sa output mo
   (`Private="false"`): ibinibigay ng editor ang sarili nito. Walang ibang gamitin kundi ang framework (tumutukoy rin ang
   project ng sample sa package na `System.Numerics.Vectors`, para sa `Vector3` at `Quaternion`). **Iisang DLL** ang
   extension: walang ibang file na nilo-load ng editor mula sa folder mo, kaya hindi kailanman nahahanap ang helper
   library sa tabi nito.
3. **Markahan ang assembly** at bigyan ito ng entry: isang public class na may constructor na walang parameter, na
   nag-i-implement ng `IWorldEditorExtension`. Isang beses itong ginagawa ng editor at isang beses tinatawag ang
   `Register`, bago gamitin ang kahit anong hook. Ito ang sa sample:

   ```csharp
   [assembly: WorldEditorExtension(typeof(ExampleVehicles.WorldEditor.Extension))]

   public sealed class Extension : IWorldEditorExtension
   {
       public void Register(IExtensionRegistry registry)
       {
           var packed = new PackedIds();
           registry.AddDecoder(Keys.Packed, packed);           // inspector
           registry.AddCopyRewriter(packed, Keys.Packed);      // paste
           registry.AddShapes(new Sails(), Keys.Sail);         // 3D view
           registry.AddShapes(new Cables(), Keys.Post);
           registry.AddGroundCuts(new Pits(), Keys.Pit);       // the ground
           registry.AddScene(new Canals());                    // the area as a whole
           registry.AddTool(new CanalTool());                  // a tool on the rail
           registry.AddAreaCopier("canals", new CanalCopies()); // copy and paste
           registry.AddLiveListener(new LiveLink(registry));   // live mode
           registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
       }
   }
   ```

4. **Isama ito** sa mod mo, sa loob ng folder nito, at pangalanan ito sa `worldeditor.json` mo:
   `"extension": "ExampleVehicles.WorldEditor.dll"`. Hindi iniaalok ang path na lumalabas sa folder ng file, o ang file
   na hindi nagtatapos sa `.dll`.

### Pag-on nito: Mga setting → Mga extension ng mod

Ang extension ay code na tumatakbo sa editor gamit ang mga karapatan ng player, kaya **ang player ang nagpapasya**.
Hinahanap ng editor ang mga extension na iniaalok ng mga mod sa mga plugins folder nito at inililista sila sa **Mga
setting**, sa ilalim ng **Mga extension ng mod**: isang switch bawat extension, ipinangalan sa folder ng mod at sa file
("ExampleVehicles · ExampleVehicles.WorldEditor.dll"). Kapag itinuro ito, ipinapakita ang path ng file at ang SHA-256
nito. Sa ilalim ng bawat switch, sinasabi ng editor ang nangyari rito: "Tumatakbo.", "Hindi tumatakbo: …" kasama ang
dahilan, o "Naka-off: the file cannot be read." (hindi mabasa ang file). Kapag walang mod na nag-aalok: "Walang mod
sa mga folder sa itaas na nag-aalok nito."

- **Naka-off bilang default.** Tumatakbo lang ang extension kapag in-on ito ng player at pinindot ang **I-save**;
  tumatakbo ito mula noon, at humihinto ang mga naka-off.
- **Ang file na iyon lang.** Natatandaan ng editor ang SHA-256 ng file. Ibang code na ang binagong DLL (update ng mod):
  naka-off ito hanggang i-on ulit ng player. Ang mga byte na sinuri ay ang mga byte na nilo-load.
- **Kung bakit hindi ito tumatakbo.** "it is not marked as an extension ([assembly: WorldEditorExtension])"
  (hindi ito minarkahan bilang extension), "it needs a newer editor (API …; this one has …)" (kailangan nito ng mas
  bagong editor), "its entry … is not an IWorldEditorExtension" (hindi IWorldEditorExtension ang entry nito), o ang
  exception na ibinato ng `Register`.

### Mga hook

| Hook | Ang ginagawa rito ng editor |
|---|---|
| `AddDecoder(key, IDataDecoder)` | Ipinapakita ng Inspector ang halagang byte array sa mga salita mo, sa ilalim ng halaga (sa halip na ang laki lang nito). Nagbabalik ang `Describe` ng ilang maiikling linya, o `null` kapag wala ang mga byte sa anyong nababasa nito. Kapag dalawang extension ang nagbabasa ng iisang key, nananaig ang unang nagbibigay ng teksto. |
| `AddCopyRewriter(ICopyRewriter, keys)` | Ibinibigay sa iyo ang bawat object ng na-paste na kopya na may hawak na isa sa mga key (`ICopy`), pagkatapos ng sariling pagkakabit ng editor (`links`), kasama ang mga bagong id ng kopya para sa mga sa orihinal (`IIdMap.TryGetNew`; false kapag hindi nakopya ang object na may hawak ng lumang id). Baguhin ang mga halaga nito sa mismong lugar (`SetInt`, `SetQuaternion`, `SetBytes`…; inaalis ng `null` ang isang halaga). Sa iyo ang mga key: hindi na sila inaalis ng `dropOnCopy`. |
| `AddAreaCopier(id, IAreaCopier)` | Ang hawak ng mod mo tungkol sa isang lugar na walang nakopyang object na nagdadala (talaan ng batong hinukay, sa mga data holder na hindi kailanman kinokopya ng editor) ay sumasama sa mga kopya ng editor. Kapag kumopya ang player (ang pinili ng tool na Rehiyon, o ang mga object ng tool na Pagpili), natatanggap ng `Copy(area, region)` ang area at ang kinokopya (`CopyRegion`: ang `Outline` nito at `Contains(x, z)`, sa metro ng mundo, at ang `Middle` nito) at nagbabalik ng sarili mong mga byte (o `null`): iningatan ang mga puwesto bilang mga offset mula sa gitna, dahil maaaring i-paste ang kopya kahit saan, nakaikot at naka-mirror, sa ibang mundo rin. Sumasama ang mga byte sa kopya at sa mga blueprint file, sa ilalim ng pangalan ng mod mo at ng `id` (mga titik, numero, tuldok, gitling at underscore). Kapag na-paste ito, isang beses tinatawag ang `Paste(area, bytes, copies)` kasama ang bawat kopyang nailalapag ng paste (`CopyPlacement`: ang `Middle`, `Turn` at `Mirror` nito; ginagawa ng `Place(offset)` ang offset mula sa lumang gitna na punto sa mundo kung saan ito lumalapag, ng `Yaw(yaw)` ang isang ikot), at ang area gaya ng pag-iwan ng paste sa lupa nito; bahagi ng hakbang ng undo ng paste ang `ToolEdit` na ibinabalik mo. Iniiwan sa paste ang mga byte na hindi tumatakbo ang copier (hindi naka-install ang extension mo). |
| `AddPlacer(IObjectPlacer, prefabs)` | Ibinibigay sa iyo nang isang beses bawat hakbang, lahat nang sabay, ang mga object ng mga prefab na inilalagay ng editor sa sariling puwesto (inilagay, na-paste, inilipat o iniikot, ng kahit anong tool, script o ni Claude) (`ICopy`, bawat isa sa bago nitong puwesto, ang mga halaga nito gaya ng paggawa sa kanila, ang sa na-paste na kopya pagkatapos ng mga copy rewriter), kasama ang area gaya ng pagkakatayo nito bago ang hakbang nang wala ang mga object na inililipat nito: itakda ang inaalam ng mod mo mula sa kinatatayuan ng isang object (ikot na iniingatan nito nang hiwalay sa sa object, ang riles na kinatatayuan ng sasakyan, ang hintuang pinapangalanan ng karatula), gaya ng gagawin ng mod mo kapag itinayo ito ng player. Sine-save kasama ng hakbang at ina-apply nang live kasama nito ang itinakda mo. Iniikot ng `SetTurn` ang object gaya ng paggawa rito, para sa mod na iniingatang hindi nakaikot ang mga object nito at ang hugis nila ay nasa mga halaga nila (iikot ang mga halaga, saka ibalik ang object; editor 1.119.0 pataas). Hindi paglalagay ang mga object na binago kung saan sila nakatayo (ang inspector, ang edit ng isang tool). |
| `AddShapes(IShapeProvider, prefabs)` | Iginuguhit din gamit ang mga hugis mo ang mga object ng mga prefab: mga `Shape`, mga tatsulok sa frame ng object (metro; x pakanan, y pataas, z pasulong; ina-apply ang puwesto, ikot at laki ng object) o sa frame ng mundo (`WorldSpace = true`), iisang kulay bawat isa (pula, berde, asul, alpha mula 0 hanggang 1; alpha na mababa sa 1: naaaninag), nakapulupot nang pakanan kapag tiningnan mula sa harap gaya sa Unity, o nakikita mula sa dalawang panig (`BothSides = true`, para sa layag o tela). Iginuguhit sila kasama ng uri ng object (nakatago kapag itinatago ito ng panel na Tanaw). Ang object na walang model sa editor ay iginuguhit ng mga hugis mo lamang (walang pamalit na kahon), at pinipili at binabalangkas ayon sa kahon ng mga hugis na ibinibigay mo para sa isa sa prefab nito na walang data (ang mga nasa sarili nitong frame). Hindi iginuguhit ang hugis na may tatsulok na pumapangalan sa sulok na wala rito, o sulok na hindi numero. |
| `AddLinkPlacer(ILinkPlacer, prefabs)` | Inilalagay ang mga magkakabit na bahagi (ang `links` ng manifest mo) kung saan sila binubuo ng mod mo kapag hindi ito sinasabi ng data nila lamang. `FrameOf(obj)`, para sa object ng mga prefab na may mga bahagi: ang frame na kinasasabitan nila, sa sarili nitong frame (`LinkFrame`: isang offset at ikot; ang plataporma ng swivel gaya ng pag-ikot o paglipat dito ng mod mo), o `null` para sa sarili nitong frame. `PlaceOf(part, parent)`, para sa bahagi ng mga prefab: kung saan ito nakatayo sa frame ng kinasasabitan nito, kapalit ng offset at ikot nito (ang bahagi sa puwesto at ikot kung saan ito sine-save ng laro, ang parent nito gaya ng pagkakaguhit; hindi ina-apply ang frame ng parent kung gayon), o `null` para sa puwesto ng mga link nito. Sumusunod pa rin sa mga link ang paglipat, pagkopya at pagbura. |
| `AddGroundCuts(IGroundCutter, prefabs)` | Hindi iginuguhit ang lupa sa loob ng mga balangkas (`GroundCut`) na ibinibigay mo para sa mga object ng mga prefab: mga punto sa lupa (x, z) sa frame ng object, o sa frame ng mundo (`WorldSpace = true`); kahit tatlo, sa kahit anong direksiyon paikot (pumuputol ayon sa even-odd rule ang balangkas na tumatawid sa sarili). Tumatagos sa butas ang click doon. Height map ang lupa ng editor na may punto bawat metro: iniiwan ng hiwa ang lupa sa pagitan ng mga punto sa loob ng balangkas, kaya sumusunod ang gilid nito sa 1 m na grid na iyon, at ang lumilitaw ay ang iginuguhit mo sa ibaba (mga hugis) at ang ilalim ng lupa. |
| `AddScene(ISceneProvider)` | Ang ginagawa ng mod mo sa isang area bilang kabuuan, iginuguhit dito: tinatawag ang `Build(area)` sa isang worker tuwing nagbubukas ang area at muli tuwing nagbabago ang mga object nito, ang lupa nito o ang laro (o kapag tinawag mo ang `Host.Redraw()`), at nagbabalik ng `SceneLayer` (o `null`). Iginuguhit ang mga `Meshes` nito (`SceneMesh`: mga sulok at ang mga normal nila sa frame ng mundo, metro, mga tatsulok na nakapulupot nang pakanan kapag tiningnan mula sa harap gaya sa Unity) gamit ang itsura ng lupa ng laro kapag may `BiomeColors` sila (ang kulay ng terrain ng bawat sulok, gaya ng ibinibigay ng `area.BiomeColor`) at pinipintahan mula sa `Mask` nila (maliit na texture ng paint mask ng terrain, `MaskTexture`) sa `MaskUvs`; kapag walang kulay ng biome, sa `Color`. Lumalapag sa kanila ang mga click (maliban kung `Pickable = false`). Hindi iginuguhit ng `Holes` nito (`GroundHole`: mga parihaba sa buong metro) ang lupa ng editor, cell por cell, eksakto hanggang sa mga gilid nila: iguhit ang ipapalit dito. Ibinibigay ng `area` (`IAreaView`) ang mga hangganan nito, ang mga object nito ng isang prefab gaya nila kasama ang mga pagbabagong hindi pa naka-save (`ObjectsOf`, kasama ang mga data holder mo), ang taas ng lupa gaya ng ipinapakita ng laro (`GroundHeight`, sa pagitan ng mga puntong isang metro ang layo sa kahabaan ng sariling mga tatsulok ng laro), ang biome, kung nasa Deep North o Ashlands ang isang punto, at ang kulay at pintura ng terrain sa isang buong metro (`BiomeColor`, `GroundMask`). Alamin ang lahat mula sa area bawat pagkakataon; walang iingatan sa pagitan ng mga tawag. |
| `AddTool(IEditorTool)` | Sarili mong tool sa rail ng 3D editor, sa ilalim ng **MODS**, kasama ang `Info` nito (`ToolInfo`: isang id, natatangi sa extension mo; ang pangalan nito sa rail; isang linya ng tulong sa tooltip nito; `Icon`, isang SVG path na iginuguhit nang 16 × 16, kung hindi ay ang unang titik ng pangalan mo). Kapag pinili, nagpapakita ito ng card ng `Controls` nito: `SliderControl` (min, max, halaga, `Step`, `Unit`), `ChoiceControl` (isang listahan), `ToggleControl`, `ButtonControl` at `TextControl` (isang linyang binabago mo gamit ang `SetText`); natatandaan ang mga halaga ng player mula sa isang paggamit hanggang sa susunod. Tinatawag ng editor ang `OnChosen` at `OnLeft` habang pinipili at iniiwan ito, ang `OnControl(id)` habang nagbabago ang isang halaga o pinipindot ang isang button, ang `OnPointer` sa bawat galaw, pindot, drag at bitaw sa ibabaw ng 3D na tanaw (`PointerInput`: ang ray, ang mga key na pinipindot, at ang itinuturo nito: ang lupa, ang scene layer mo, o isang bagay, kasama ang punto at ang normal ng lupa roon), at ang `OnKey` sa bawat key na pinindot (ang pangalan ng key, gaya ng "Enter", "Escape", "Back" o "A"; ibalik ang true kapag ginamit mo ito, iniiwan ito ng false sa editor, kaya nag-a-undo pa rin ang Ctrl+Z). Ibinibigay ng `IToolContext` na iniaabot sa bawat tawag ang area (`Area`, gaya ng sa mga scene layer), ang mga halaga ng mga kontrol mo (`Number`, `Choice`, `IsOn`), mga hugis na iginuguhit sa ibabaw ng area hanggang baguhin mo sila (`Preview`, sa frame ng mundo), isang linya sa status bar (`Status`) at `Commit(label, ToolEdit)`: mga object na idinagdag (isang prefab, puwesto, ikot at ang data nito na itinakda sa pamamagitan ng `ICopy`, na maaaring iikot ng `SetTurn`), binago (kopya ng object na ibinigay sa iyo ng area, kasama ang mga pagbabago mo) at tinanggal, sa iisang hakbang ng kasaysayan ng area, ina-undo at sine-save gaya ng kahit anong ibang edit. Iniingatan sa kasaysayan ang mga data holder mo gaya ng ibang object, kahit hindi sila kailanman iginuguhit; hindi ginagalaw ang object na hindi mo nakuha mula sa area. |
| `AddLiveListener(ILiveListener)` | Sinasabihan kapag kumonekta ang editor sa isang laro (`OnConnected`, kasama ang laro) at kapag natapos iyon (`OnDisconnected`: isinara ang mundo, o nagbukas ng iba). Tingnan ang [Live mode](#live-mode). |
| `AddWorldGenerator(IWorldGenerator)` | Ang mga mundo ng mod mo ng world generation (tingnan ang [Mga world generator](#mga-world-generator)). |
| `AddWorldRules(IWorldRulesProvider)` | Ang mga patakarang ginagamit sa pagbuo ng mga mundo, mula sa mga setting at data file ng mod mo ng world generation (tingnan ang [Mga patakaran ng mundo](#mga-patakaran-ng-mundo)). |
| `Host` | Ang editor, sa labas ng mga hook: `Host.Live`, ang nakakonektang laro (null kapag wala: naka-save na mundo, ang Gawaan); `Host.Game`, ang mga component ng laro na hinihingi ng `gameComponents` ng manifest mo (`Components("Ship")`: bawat isa kasama ang prefab nito, ang object na kinalalagyan nito at ang mga field nito ayon sa pangalan, gaya ng pag-save sa kanila ng laro; handa kapag bukas na ang isang mundo o ang Gawaan, kaya hingin mula sa mga hook mo, hindi mula sa `Register`); `Host.Setting(file, section, key)`, isang setting mula sa file na pinapangalanan ng `settingsFiles` ng manifest mo, bilang tekstong hawak ng file (null kapag wala roon ang file o ang setting: gamitin ang default ng mod mo; handa, gaya ng `Host.Game`, kapag bukas na ang isang mundo o ang Gawaan); `Host.BepInExFolders`, ang mga BepInEx folder ng player na alam ng editor, sa pagkakasunod ng pagtingin nito sa kanila (bawat isa kasama ang `Path` nito at kung nasaan ito, para sa player); `Host.DataFolder`, ang data folder ng editor (ingatan ang sarili mong mga file sa sarili mong folder doon); `Host.World` (mga net10.0 extension), ang sariling mga patakaran ng mundo at object ng laro (tingnan ang [Mga patakaran ng mundo](#mga-patakaran-ng-mundo)); at `Host.Redraw()`, para gawin ulit ang mga hugis at hiwa mo sa susunod na frame (pagkatapos may sabihing bago sa iyo ang laro). |
| `Log(message)` | Sumusulat ng linya, ipinangalan sa mod mo ("extension ExampleVehicles: …"), sa log ng editor, `ValheimWorldEditor.log` sa data folder nito (**Buksan ang log** sa panimulang pahina; nagsisimula ulit sa bawat paggamit). |

Natatanggap din ng mga hook ng hugis at hiwa ang mundo sa paligid ng object (`IWorldView`): hinahanap ng `TryFind(idKey, id, out
obj)` ang ibang object ayon sa id na hawak nito, para sa lubid o kableng umaabot dito. Ang mga id lang sa ilalim ng key
na pinapangalanan ng isang patakaran ng `links` bilang `id` nito ang nahahanap (ini-index ng editor ang mga iyon). Sa 3D
editor, `IGroundView` din ang mundong iyon (`world is IGroundView ground`): ibinibigay ng `TryGroundHeight(x, z, out height)`
ang taas ng lupa roon gaya ng pagguhit dito ng editor, para sa hugis na umaabot pababa rito (hagdang lubid), false sa
labas ng area.

Dumarating ang mga object bilang `IObjectView`: ang pangalan ng prefab ("" kapag hindi ito alam ng editor), ang puwesto,
ikot (Unity Euler angles sa degree, at `Orientation`, isang quaternion) at laki gaya ng pagkakaguhit (para sa magkakabit
na bahagi, kung saan ito binubuo ng mod mo, hindi kung saan ito sine-save ng laro), at bawat halaga ng data nito ayon sa
key: `TryGetInt`, `TryGetLong`, `TryGetFloat`, `TryGetVector3`, `TryGetQuaternion`, `TryGetString`,
`TryGetBytes`. Pinapangalanan ang mga key at prefab gaya ng pagpapangalan sa kanila ng code mo; hina-hash sila ng editor
gaya ng ginagawa ng laro.

### Mga world generator

Pinapalitan ng mod ng world generation ang lupa ng laro, at minamarkahan ang mga mundong ginagawa nito: nagsisimula ang
world file nila sa -1 kung saan dapat ang bersiyon ng file ng laro, saka ang id ng generator (isang string), saka ang
kahit anong iniingatan ng mod (Riverheim bago ang 1.3: ang bersiyon ng file ng laro, saka ang preset nito). Tinatanggihan
ng editor ang ganoong mundo maliban kung may tumatakbong extension na nagrerehistro ng generator na may ganoong id, gaya
ng pagtanggi rito ng laro nang walang mod: nakalista, naka-grey out, kasama ang dahilan (pangalanan ang mga id sa
`worldGenerators` ng manifest mo para masabi ng editor ang kulang).

Ang mod na kumakabit sa [Worldhalla](https://thunderstore.io/c/valheim/p/Gurebu/Worldhalla/) (Riverheim mula 1.3) ay
minamarkahan ng Worldhalla ang mga mundo nito sa halip, kasama ang id at mga setting ng generator sa sariling header ng
Worldhalla. Magrehistro ng generator para sa kanila na may id na `worldhalla:` na sinusundan ng id ng generator mo sa
Worldhalla (ang sa Riverheim: `worldhalla:dev.gurebu.riverheim`): ibinibigay sa `ReadHeader` nito ang bersiyon ng file
ng laro, saka ang mga setting mo gaya ng pag-iingat sa kanila ng Worldhalla, at dapat basahin nito sila nang buo.
Tinatanggihan, kasama ang dahilan, ang mundong ang header ng Worldhalla ay mula sa Worldhalla na mas bago sa 1.0 (ang
format nito, o mga sariling setting ng Worldhalla). Ibinibigay ng `IWorldGenerator` mo ang:

- `Id`, `PluginId` (ang BepInEx GUID ng mod: sa live mode, sinasabi sa iyo ang bersiyong pinapatakbo ng laro) at `Name`;
- `ReadHeader(file, out fileVersion)` at `WriteHeader(file, fileVersion, settings)`: ang sumusunod sa id, binabasa at
  isinusulat sa mga anyo ng laro (`IWorldFileReader`, `IWorldFileWriter`), iniingatan ang mga setting mo bilang
  `GeneratorSettings` (paglalarawan para sa player, at sarili mong halaga). Magbato ng `InvalidDataException` kasama ang
  dahilan para sa player para sa header na hindi mo mabasa;
- `Problem(settings)`: kung bakit hindi maiguhit ang ganoong mundo, nang mabilis (hinihingi para sa bawat mundong
  inililista ng panimulang pahina), o null;
- `Open(settings, gamePluginVersion)`: handang maglatag ng mga mundo, habang nagbubukas ang isang mundo (`IWorldLayout`:
  ang `Version` mo, kung `Slow` ang paglalatag, at `LayOut(seed)`); magbato ng `InvalidOperationException` kasama ang
  dahilan kapag hindi mo kaya;
- nagbibigay ang `LayOut(seed)` ng `IGroundLayout`: ang biome (ang halaga ng laro), ang taas kasama ang mask nito at ang
  forest factor sa isang punto, kapalit ng katawan ng GetBiome, GetBiomeHeight, GetAshlandsHeight at GetForestFactor ng
  laro (hinihingi mula sa maraming thread). Nauuna pa rin sa mga sagot mo ang stretch ng Expand World, at pagkatapos ang
  mga multiplier nito ng lalim at gubat, gaya ng pagtatagpo ng mga patch ng mga mod sa laro; walang tubig ng laro na
  inilalatag ng editor;
- `NewWorldSettings()`: ang mga setting na nakukuha ng bagong mundo (iniaalok ng pahina ng Bagong mundo ang generator
  mo, piniling una), o null;
- `Notes(settings, expandWorld)`: ang iba pang ginagawa ng mga setting ng Expand World (`ExpandWorldSettings`) sa mga
  mundo mo, para sa log.

Pagkakamali ang kahit anong ibang ibato ng generator mo: ino-off ang extension nito, at sinasabihan ang player na
pumalya ang add-on.

### Mga patakaran ng mundo

Ang mod ng world generation na nagbabago sa sariling pagbuo ng laro sa pamamagitan ng mga setting at data file (mas
malaking mundo, sarili nitong mga biome, lokasyon at halaman, gaya ng ginagawa ng Expand World) ay hindi mismong
naglalatag ng mundo: sinusunod ng generator ng editor ang mga patakarang ibinibigay dito. Buuin ang extension mo para sa
net10.0 (hawak ng `net10.0` build ng SDK ang rules model, ang `WorldRules` at ang mga type na hawak nito, sa namespace na
`ValheimGen`) at magrehistro ng `IWorldRulesProvider`:

- `ForWorld(world)`: ang mga patakarang ginagamit sa pagbubukas ng isang mundo. Ibinibigay ng `WorldRulesRequest` ang
  `Name` nito, ang `StartingGlobalKeys` na iniingatan ng world file nito (mga setting ng isang mod na naka-store doon),
  ang `LoadedGlobalKeys` na hawak ng save nito, ang `GridWidth` na kinalalagyan ng mga chunk file nito, at ang larong
  pinapatakbuhan nito kapag live ito (`Live`, kung hindi ay null): basahin noon ang mga file ng mod mula sa laro, sa
  pamamagitan ng `Live.Plugins()`, `Live.ConfigFile(path)` at `Live.ConfigFolder(path)` (sa ilalim ng BepInEx/config
  nito; null mula sa WorldEditorBridge na mas luma sa 1.54.0), kung hindi ay mula sa `Host.BepInExFolders`;
- `ForNewWorld()`: ang mga patakarang ginagamit sa paggawa at pagtingin sa bagong mundo (ang pahina ng Bagong mundo).

Ibalik ang null (o `WorldRules.Vanilla`) kapag hindi binabago ng mod mo ang mundo (hindi naka-install, o nasa mga
default nito): tatanungin ang susunod na provider, at kapag wala, sariling mundo ng laro ang mundo. Ginagawa ang mga
patakaran ng lokasyon, halaman at silid laban sa sarili ng laro (`Host.World`: ang `Locations` nito kapag nabasa na ng
`ReadLocations()`, ang `Vegetation` nito, `HasPrefab(name)` at `IsGameRoom(name)`). Sinasabi ng
`WorldRulesText.Describe(rules)` sa ilang salita ang binabago ng mga patakaran, para sa mga linya ng log mo. Pagkakamali
ang provider na nagbabato: ino-off ang extension nito, at tatanungin ang susunod.

### Mga tuntunin

- **Maging mabilis, walang iingatang state.** Tinatawag ang mga hook mula sa ilang thread nang sabay at madalas (bawat
  muling pagguhit ng mga hugis at hiwa, bawat na-paste na object): walang iingatang state sa pagitan ng mga tawag (o
  i-lock ito), at bumalik nang mabilis.
- **Huwag kailanman magbato.** Ino-off ng hook na nagbabato ang extension hanggang muling magsimula ang editor (sinasabi
  ng Mga setting kung bakit, at ng log); nagpapatuloy ang editor nang wala ito. Sa code na nagpapatuloy pagkatapos
  bumalik ng isang hook (isang `async` na tawag sa laro), saluhin mismo ang lahat, gaya ng ginagawa ng `LiveLink` ng
  sample.
- **Mga bersiyon.** Ang `SdkInfo.ApiVersion` ang bersiyon ng API (ngayon ay 1), at itinatala ng attribute ang
  bersiyong binuuan mo. Pinapatakbo ng editor ang mga extension na binuo laban sa bersiyon nito o sa mas maaga. Dumarating
  bilang mga dagdag ang mga bagong hook; nagbabago lang ang bersiyon kapag nasira ang API.

## Mga add-on

Kasama ng mod nito ang extension. Kapag hindi gumawa ng isa ang gumawa ng mod (o hindi na inaalagaan ang mod), kaya ito
ng kahit sino: ang **add-on** ay isang `worldeditor.json` at isang extension para sa **mod ng iba**, inilalabas nang
hiwalay, na ini-install ng player sa tabi ng editor. Nananatili ang mod mismo gaya ng dati, sa `BepInEx/plugins`; itinuturo
lang ng add-on sa editor ang tungkol dito.

```
ValheimWorldEditor/                ang folder ng editor (sa macOS: ang folder na may hawak ng ValheimWorldEditor.app)
  ValheimWorldEditor(.exe)
  addons/
    Tunnels/                       isang folder bawat add-on, ipangalan ayon sa gusto mo
      worldeditor.json
      Tunnels.WorldEditor.dll
```

- **Ang file nito** ay isang `worldeditor.json` gaya ng sa itaas (mga label ng data, prefab, link, `extension`,
  `gameComponents`), na may isa pang key: `"serves": "TheirMod.dll"`, ang pangalan ng file ng DLL ng mod. Ginagamit ang
  add-on habang may DLL na ganoon ang pangalan sa isa sa mga plugins folder na alam ng editor (Mga setting, Mga BepInEx
  folder), kahit saan sa ilalim ng `plugins`, at hindi ginagalaw kung wala. Kapag walang `serves`, lagi itong ginagamit
  (sarili mong tool, para sa walang partikular na mod). Ang `serves` na hindi payak na pangalan ng file na nagtatapos sa
  `.dll`, o file na nasa bersiyong hindi nababasa ng editor na ito, ay nag-iiwan sa add-on (sinasabi ng log kung bakit).
- **Ang extension nito** ay ginagawa gaya ng iba (tingnan ang [Paggawa ng isa](#paggawa-ng-isa)): iisang DLL, sa loob ng
  folder ng add-on, pareho ang bawat hook at tuntunin.
- **Naka-on kapag na-install.** Pagpili ng player ang pag-install ng add-on, kaya tumatakbo ito nang hindi ino-on sa Mga
  setting. Inililista ng Mga setting ang mga add-on sa ilalim ng **Mga add-on**, bawat isa may switch na nag-o-off dito
  nang hindi ito tinatanggal (walang ginagamit dito kung gayon), at kasama ang nangyari sa bawat isa: "Naghihintay sa mod
  nito: wala ang TheirMod.dll sa mga plugins folder sa itaas.", "Tumatakbo.", "Hindi tumatakbo: …", na mga paalala lang
  ang dala nito, o na naka-off ito.
- **Nananaig ang salita ng mod mismo.** Binabasa ang mga file ng mga add-on bago ang sa mga mod: kung balang araw ay
  magdala ang mod ng sarili nitong `worldeditor.json`, papalitan ng sinasabi nito ang sinabi ng add-on (kailangan noon ng
  sarili nitong extension ang switch ng player, gaya ng dati).
- **Ilabas ito bilang `.zip` na may hawak ng folder nito**, para i-unpack ng mga player sa `addons`. Kasama sa mga
  package ng editor ang folder na iyon, walang laman maliban sa isang `README.txt`.
- **Nag-iingat ng ilang add-on ang sariling repository ng editor** (`addons/`, bawat isa inilalabas bilang sariling zip
  sa tabi ng mga package ng editor: tingnan ang [development.md](../development.md#add-ons-in-this-repository)); maaaring
  nasa kahit saan ang sa iyo.

## Live mode

Kapag nakakonekta sa tumatakbong laro (live mode), ina-apply ng editor ang mga pagbabago nito sa pamamagitan ng plugin na
WorldEditorBridge, na sumisira, gumagawa at naglilipat ng mga object sa laro. Ang mod na bumubuo ng mga bagay mula sa mga
object nito habang tumatakbo ang laro (sasakyan mula sa mga bahagi nito, tunnel mula sa hinukay) ay nakikita lang ang mga
pagbabago ng editor sa susunod na load, maliban kung nakikilahok ang sarili nitong plugin. Dalawang bagay ang iniaalok ng
bridge sa mga plugin ng mga mod, sa pamamagitan ng `WorldEditorBridge.Api.dll`: kasama ito ng bridge, sa `plugins` folder
nito, at sa zip ng SDK, at walang hawak na type ng laro o ng Unity, kaya hindi ito masisira ng update ng laro.

### Ang panig ng plugin mo

Magrehistro mula sa `Awake` ng plugin mo, na may soft dependency sa bridge, at ilagay sa sarili nitong class ang code na
humahawak sa API: nalo-load din ang plugin mo nang walang bridge. Mula sa `sdk/samples/ExampleVehicles.Bridge` ng
sample:

```csharp
[BepInPlugin(PluginId, "ExampleVehicles", "1.0.0")]
[BepInDependency(BridgeApi.BridgeGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleVehiclesBridgePlugin : BaseUnityPlugin
{
    public const string PluginId = "ExampleVehicles";

    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Register(Logger);   // a class of its own: this plugin loads without the bridge
        }
    }

    private void OnDestroy()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Unregister();       // BridgeApi.Remove(PluginId)
        }
    }
}

internal static class BridgeLink
{
    public static void Register(BepInEx.Logging.ManualLogSource log)
    {
        BridgeApi.AddRoutes(ExampleVehiclesBridgePlugin.PluginId, new Routes());          // answers the editor's calls
        BridgeApi.AddListener(ExampleVehiclesBridgePlugin.PluginId, new Listener(log));   // told what the editor applied
    }

    public static void Unregister() => BridgeApi.Remove(ExampleVehiclesBridgePlugin.PluginId);
    // Routes and Listener: see the sample.
}
```

| Hook | Ang ginagawa rito ng bridge |
|---|---|
| `AddListener(modId, IBridgeListener)` | Tinatawag ang `OnApplied(AppliedChanges)` sa main thread ng laro kaagad pagkatapos ng bawat pag-apply ng editor, kasama ang binago nito: `Created` at `Destroyed`, ang mga object na ginawa at sinira (bawat isa ang `User` at `Id` ng ZDOID nito, ang hash ng prefab nito at ang posisyon nito); `Moved`, ang mga object na inilipat sa ilalim ng sarili nilang ZDOID (bridge 1.40.0 o mas bago: `From` kung saan nakatayo ang bawat isa, `To` kung saan ito nakatayo ngayon; ang object na may naka-load na luma at bagong puwesto sa isang player ay ginagawang muli sa halip, at nasa `Created` at `Destroyed`; mula 1.97.0, narito rin ang object na binago ng editor ang data, nasa kanya na ang bagong data nito); `TerrainZones`, ang mga zone na isinulat ulit ang lupa; `ResetZones`, ang mga zone na inalisan ng laman para buuin ulit. Pagsama-samahin ulit dito ang mga bagay mo. Inilo-log sa log ng laro ang exception at walang binabago sa ginawa ng editor. |
| `AddRoutes(modId, IBridgeRoutes)` | Sinasagot ng `Handle(name, body)` ang `POST /mods/<modId>/<name>` sa main thread ng laro, sa kahit anong byte na pinagkasunduan mo at ng extension ng editor mo; sumasagot ang `null` ng 404 ("the mod has no route of that name"), ang exception ng 500 kasama ang mensahe nito. Naghihintay ang bridge nang hanggang 30 segundo para sa main thread: maging mabilis, o sumagot ng "started" at hayaang magtanong ulit ang extension. Hanggang 16 MB ang body ng isang tawag. |
| `Remove(modId)` | Inaalis ang mga route at listener mo (plugin na nagsasara). |

Ang mga object na ginagawa, inililipat o binabago ng bridge ay iniaabot sa mga mod ng laro gaya ng pag-abot sa kanila ng
laro ng object na galing sa ibang laro: dumaraan ulit ang data nila sa `ZDO.Deserialize` (parehong mga halaga). Ang mod
na nakakaalam ng mga object sa pamamagitan ng pag-patch dito (ang ZdoWatcher ng ValheimRAFT, na naghahanap ng mga barko
at ng tinatalian ng mga lubid ayon sa mga id nila) ay agad na nakakakilala sa na-paste na object, nang walang sariling
listener.

Pinapalitan ng muling pagrehistro sa ilalim ng parehong id ang nairehistro dati. Sa iyo ang id ng mod (magandang id ang
id ng plugin mo): 1 hanggang 100 titik, numero, tuldok, gitling at underscore; nagbabato ang ibang id. Ang
`BridgeApi.Version` ang bersiyon ng API (ngayon ay 1); pinapatakbo ng bridge ang mga plugin na binuo laban sa bersiyon
nito o sa mas maaga, at pinapanatili ito ng mga dagdag. Inililista ng `GET /mods` sa bridge ang bersiyon ng API at ang
mga id na nakarehistro.

### Ang panig ng extension mo

Nakukuha ng extension mo ang laro sa pamamagitan ng `ILiveGame`: mula sa `registry.Host.Live` kahit kailan (null kapag
hindi nakakonekta ang editor), o sa `OnConnected` ng live listener mo.

- `Mods`: ang mga id na nakarehistro sa bridge, gaya ng iniulat ng bridge noong binuksan ang mundo (nakikita sa susunod
  na koneksiyon ang plugin na nagrerehistro nang mas huli).
- `CallAsync(modId, name, body)`: tinatawag ang route mo at ibinabalik ang mga byte ng sagot nito. Nagbabato ito kapag
  hindi nakarehistro ang mod o ang route, kapag pumalya ang route (kasama ang mensahe ng laro), o kapag hindi maabot ang
  laro.
- `Plugins()`, `ConfigFile(path)` at `ConfigFolder(path)`: ang mga BepInEx plugin na nilo-load ng laro, at isang file o
  ang mga entry ng isang folder sa ilalim ng BepInEx/config nito, read only (null mula sa bridge na mas luma sa 1.54.0, at
  para sa path na wala roon). Nagbabato sila ng `InvalidOperationException` kapag tinanggihan ng bridge ang path (labas
  sa folder, lampas 4 MB).

Hingin sa laro ang ito lang ang nakakaalam (ang binuo ng mod mo sa code), ingatan ito, at tawagin ang
`registry.Host.Redraw()` para gawin ulit ang mga hugis at hiwa mo. Tinatawag ng `LiveLink` ng sample ang route na `ping`
ng plugin kapag kumonekta ang editor at inilo-log ang sagot; sinasagot din ng plugin nito ang `players` (ilang player ang
nasa laro).

Kapag nagbukas nang live ang isang mundo, pinapangalanan ng log ng editor ang mga mod na nakarehistro ("bridge mods: …").

## Ang sample na Example Vehicles

Hawak ng zip ng SDK (at ng repository ng editor) ang source ng dalawang bahagi ng isang gawa-gawang mod, para kopyahin.
Bumubuo ang Example Vehicles ng mga sasakyan mula sa isang katawan at sa mga bahaging itinayo rito.

| File | Ang ipinapakita nito |
|---|---|
| `sdk/samples/ExampleVehicles.WorldEditor/worldeditor.json` | Ang file ng mod: mga label, isang nakatagong katulong, mga grupo, ang patakaran ng link na `vehicle`, ang extension. |
| `sdk/samples/ExampleVehicles.WorldEditor/Extension.cs` | Ang entry, nagrerehistro ng isang hook ng bawat uri. |
| `sdk/samples/ExampleVehicles.WorldEditor/Keys.cs` | Ang mga data key at prefab ng mod, gaya ng pagpapangalan sa kanila ng code nito. |
| `sdk/samples/ExampleVehicles.WorldEditor/PackedIds.cs` | Decoder at copy rewriter para sa listahan ng mga id na naka-pack sa byte array (isang byte ng bersiyon, saka apat na byte bawat id): ipinapakita sa inspector ("Linked to 3 object(s): …"), at sa na-paste na kopya, ginagawang pumapangalan sa mga object ng kopya (nawawala ang mga id ng mga object na hindi nakopya). |
| `sdk/samples/ExampleVehicles.WorldEditor/Sails.cs` | Mga hugis: ang tela ng layag na iginuguhit sa pagitan ng tatlo o apat na sulok na iniingatan sa data nito, sa kulay nito, nakikita mula sa dalawang panig. |
| `sdk/samples/ExampleVehicles.WorldEditor/Cables.cs` | Mga hugis sa world space: kable mula sa tuktok ng isang poste hanggang sa tuktok ng posteng pinapangalanan nito, hinanap gamit ang `TryFind`. |
| `sdk/samples/ExampleVehicles.WorldEditor/Canals.cs` | Isang scene layer: bawat kanal (data holder na nag-iingat ng daan, lapad at lalim nito) na hinukay bilang trinsera gamit ang itsura ng lupa ng laro, pininturahang hubad na lupa ang sahig nito, hindi iginuguhit ang lupa sa ibabaw nito. |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalTool.cs` | Isang tool: mag-click ng mga punto sa lupa para sa daan ng kanal (inaalis ng Backspace ang huli), ang lapad at lalim nito mula sa mga slider, naka-preview ang daan; idinadagdag ng Enter o ng **Dig** ang kanal (isang data holder), itinatapon ng **Clear points** o Escape ang mga punto. Tinatambakan ng Shift+click ang kanal (tinatanggal ito), ibinibigay rito ng Ctrl+click ang lapad at lalim ng mga slider (binabago ito). |
| `sdk/samples/ExampleVehicles.WorldEditor/CanalCopies.cs` | Isang area copier: ang mga kanal na ang buong daan ay nasa kinokopya ay iniingatan bilang mga offset mula sa gitna ng kopya, at hinuhukay ulit kung saan na-paste ang bawat kopya, nakaikot at naka-mirror kasama nito, nakatayo sa lupa roon. |
| `sdk/samples/ExampleVehicles.WorldEditor/Pits.cs` | Isang ground cut: bilog na butas na kasinlapad ng radius ng hukay (1.5 m kapag wala itong iniingatan). |
| `sdk/samples/ExampleVehicles.WorldEditor/LiveLink.cs` | Ang panig ng extension sa live mode: pinapangalanan ang sariling mga barko ng laro (`Host.Game`, mula sa `gameComponents` ng manifest), sinusuri kung nagrehistro ang plugin, tinatawag ang route nitong `ping`, inilo-log ang sagot, at sinasalo ang lahat. |
| `sdk/samples/ExampleVehicles.Bridge/ExampleVehiclesBridgePlugin.cs` | Ang BepInEx plugin ng mod na nakikilahok sa live mode: soft dependency, pagrehistro sa sariling class, mga route na `ping` at `players`, isang listener na inilo-log ang in-apply ng editor. Nabubuo ito laban sa mga DLL ng laro at ng BepInEx gaya ng kahit anong plugin (`-p:ValheimDir=…`, ang folder ng isang dedicated server ng Valheim). |
