<!-- source: 1000c3db -->
# I-edit ang mundong ginawa gamit ang Expand World, Riverheim o Tunnelheim

I-install ang add-on ng editor para sa mod mo, para bumukas, maiguhit at ma-save ang mundong ginawa nito gaya ng
paggawa ng mod.

**Kailangan mo:** ang mod na naka-install sa isang BepInEx folder sa computer na ito (ang Valheim mo, isang profile ng
mod manager, o kopya ng `BepInEx` folder ng isang server), at ang add-on ng parehong release ng editor.

| Add-on | Para sa | Ang idinadagdag nito |
|---|---|---|
| [Expand World](../../../addons/ExpandWorld/README.md) | Expand World Size, Rivers at Data | Lupa, mapa, mga biome, tubig, mga lokasyon, halaman at mga hula ng Isang bagong mundo gaya ng paggawa ng mga mod |
| [Railroads](../../../addons/Railroads/README.md) | Railroads (`Railroads.dll`) | Iginuguhit ang riles, mga station at mga tren ng mod mula sa sarili nitong mga model; gumagana sa laro ang mga piece na inilalagay, ipine-paste, inililipat o iniikot mo, pati nang live |
| [Riverheim](../../../addons/Riverheim/README.md) | Riverheim (`Riverheim.dll`) | Binubuksan ang mga mundo ng Riverheim (tinatanggihan kung wala ito), iginuguhit gamit ang sarili mong kopya ng mod; Riverheim bilang generator sa Isang bagong mundo |
| [Tunnelheim](../../../addons/Tunnelheim/README.md) | Tunnelheim (`ValheimTunneling.dll`) | Iginuguhit ang mga tunnel ng mod, at isang tool na **Tunnel** (hukay, Add Earth, butas, mga silid, mga shaft, mga hagdan); kasama ang mga tunnel sa mga kopya at blueprint |
| [ValheimRAFT](../../../addons/ValheimRAFT/README.md) | ValheimRAFT (`ValheimRAFT.dll`) | Iginuguhit nang buo ang mga barko at sasakyang panlupa ng mod, kasama ang mga layag at lubid nila; bawat isa ay inililipat, kinokopya at binubura bilang isa |

1. Isara ang editor.
2. Mula sa [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest) ng editor (o Nexus Mods),
   i-download ang `ValheimWorldEditor-addon-<Name>-<version>.zip`.
3. I-unpack ito sa `addons` folder katabi ng editor (sa macOS, katabi ng `ValheimWorldEditor.app`), para magkaroon ka
   ng `addons/<Name>/worldeditor.json`.
4. Simulan ang editor at buksan ang **Mga setting** sa panimulang pahina.
5. Sa ilalim ng **Mga add-on**, hanapin ang add-on. Dapat nakasulat ang "Tumatakbo." (o "Hindi pa nababasa" hanggang
   magbukas ng mundo). Kung nakasulat ang "Naghihintay sa mod nito: wala ang … sa mga plugins folder sa itaas.",
   idagdag ang BepInEx folder na may hawak ng mod sa ilalim ng **Mga BepInEx folder**, saka **I-save**.
6. Para sa mundo ng server na binuksan mula sa mga file nito, magdagdag ng kopya ng `BepInEx` folder ng server sa
   ilalim ng **Mga BepInEx folder**, para mabasa ng editor ang mga setting ng mod ng server.
7. Buksan ang mundo sa ilalim ng **Isang naka-save na mundo**. Ipinapakita ng card nito ang mod (halimbawa
   **Riverheim** o "Expand World").
8. Mag-edit at mag-save gaya ng dati. Sa Tunnelheim, piliin ang **Tunnel** sa ilalim ng **MODS** sa tool rail, pumili
   ng aksiyon, at i-click ang lupa o ang bato.

## Tingnan kung gumana

- Bumubukas ang mundo, at ang lupa at mapa nito ay mukhang gaya sa laro (mga ilog ng Riverheim, laki at mga biome ng
  Expand World).
- Sinasabi ng log ng editor (**Buksan ang log** sa panimulang pahina) kung ano ang nahanap at nabasa ng add-on.
- Sinasabi ng card ng mundo na nananatiling kulay abo kung bakit: tingnan ang [Hindi bumubukas o kulay abo ang isang
  mundo](../help/troubleshooting.md#hindi-bumubukas-o-kulay-abo-ang-isang-mundo).

## I-undo ito

Naa-undo ang bawat edit gaya ng iba (`Ctrl+Z`, Kasaysayan). Para tumigil sa paggamit ng add-on, i-off ang switch nito sa
ilalim ng **Mga setting → Mga add-on** (nananatili ang folder nito), o burahin ang folder nito mula sa `addons`. Kapag
naka-off ito, binubuo ang mga mundo ng Expand World gaya ng ginagawa ng laro nang walang mga mod, at tinatanggihan ang
mga mundo ng Riverheim.

## Mabuting malaman

- Ginagamit lang ang add-on habang nasa isang plugins folder na alam ng editor ang DLL ng mod nito; hindi binibilang ang
  mod na in-off ng mod manager, o ang mga setting na iniwan ng tinanggal na mod.
- Iginuguhit ang mga piece ng Railroads mula sa mga model file na iniingatan ng mod sa `meshes` folder nito: panatilihin
  ang folder na iyon katabi ng `Railroads.dll`, gaya ng pag-install ng mod.
- Kailangan ng mga data file ng Expand World ang library na YamlDotNet na ini-install ng mod sa ilalim ng
  `BepInEx/plugins`; kung wala ito, hindi nababasa ang mga ito, at sinasabi iyon ng log.
- Dapat alam ng Riverheim mo ang preset ng mundo (i-update ito kung mas luma ito kaysa sa gumawa ng mundo). Kailangan
  ng Riverheim 1.3 pataas ang Worldhalla katabi nila, gaya sa laro. Hindi mabubuksan ang mga mundong ginawa gamit ang
  deprecated na beta ng Riverheim. Nang live, dapat ang Riverheim mo ang bersiyong kailangan ng mga player na sumasali sa
  laro (parehong bersiyon mula 1.3, parehong release bago noon).
- Kailangan ng mundo ng Riverheim ang Riverheim sa laro ng bawat player, kaya hindi makakasali rito ang mga console
  player sa crossplay.
- Nang live, binabasa ang mga file ng Expand World mula sa laro mismo sa pamamagitan ng plugin: tingnan ang
  [Live mode](../live-mode.md).

## Kaugnay

- [Isang bagong mundo](../new-world.md): Riverheim at Expand World kapag gumagawa ng mundo
- [Live mode](../live-mode.md): mga larong may mod, nang live
- [Magbukas ng mundong wala sa listahan](open-a-world.md)
