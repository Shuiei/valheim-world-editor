<!-- source: 4a8a11bc -->
# Mag-backup ng mundo bago mag-edit

Gumawa ng kopya ng folder ng mundo na lagi mong mababalikan, anuman ang mangyari sa mundo pagkatapos.

**Kailangan mo:** ang pangalan ng mundo, at nakasarang Valheim (o nakahintong server na nagpapatakbo ng mundo).

1. Isara ang Valheim, o ihinto ang server. Patuloy na isinusulat ng tumatakbong laro ang mundo, kaya ang kopyang ginawa
   habang tumatakbo ito ay maaaring kalahating luma, kalahating bago.
2. Hanapin ang folder ng mundo: sa panimulang pahina ng editor, i-click ang **Isang naka-save na mundo** at i-hover ang
   card ng mundo. Nagtatapos ang tooltip sa folder, halimbawa `~/.config/unity3d/IronGate/Valheim/worlds_local/Midgard`.
3. Buksan ang folder na naglalaman nito (`worlds_local`) sa file manager mo. Ang karaniwang mga lugar ay:
   - Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local`
   - Linux: `~/.config/unity3d/IronGate/Valheim/worlds_local` (Valheim sa pamamagitan ng Proton:
     `<Steam library>/steamapps/compatdata/892970/pfx/drive_c/users/steamuser/AppData/LocalLow/IronGate/Valheim/worlds_local`)
   - macOS: `~/Library/Application Support/IronGate/Valheim/worlds_local`
   - Dedicated server: `<savedir>/worlds_local` sa server.
4. Kopyahin ang folder ng mundo (`Midgard`) at i-paste ang kopya sa parehong folder na `worlds_local`.
5. Palitan ang pangalan ng kopya ng `<World>_backup_<date>`, halimbawa `Midgard_backup_2026-10-22`. Panatilihin ang
   pangalan ng mundo nang eksakto, saka `_backup_`.

> [!TIP]
> Kapag ganito ang pangalan, nasa tabi ng mundo ang kopya, at inililista ito ng editor bilang backup sa
> **Balik sa isang backup** ng tool na Rehiyon, kaya maibabalik mo mula rito ang isang area sa ibang pagkakataon: tingnan
> ang [Ibalik ang isang area mula sa backup](restore-from-backup.md).

## Tingnan kung gumana

- Pareho ang mga file ng kopya at ng mundo: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` at ang mga `.chunk` na file.
- Sa panimulang pahina, inililista rin ng **Isang naka-save na mundo** ang kopya, sa sarili nitong pangalan. Ang mundo
  mismo ang buksan para mag-edit, hindi ang kopya.
- Sa 3D view ng mundo, inililista ng **Balik sa isang backup** ng tool na [Rehiyon](../area.md) ang kopya bilang
  **Editor · \<date\>**.

## I-undo ito

Walang binabago ang kopya sa mundo: burahin ang folder ng kopya kapag hindi mo na ito kailangan.

Para ibalik ang buong mundo sa dati nitong ayos sa kopya:

1. Isara ang Valheim, o ihinto ang server.
2. Ilipat ang folder ng mundo (`Midgard`) palabas ng `worlds_local`, sa Desktop mo halimbawa (burahin ito kapag sigurado
   ka na).
3. Palitan ang pangalan ng kopya (`Midgard_backup_2026-10-22`) ng pangalan ng mundo (`Midgard`).
4. Simulan ang laro: ilo-load nito ang mundo kung paano ito noong kinopya mo.

May sariling mga backup din ang laro sa tabi ng mundo (`<World>_backup_auto-<date>`); inililista ito ng editor bilang
**Laro · \<date\>**.

## Kaugnay

- [Ang una mong edit](../start/first-edit.md)
- [Ibalik ang isang area mula sa backup](restore-from-backup.md)
- [Mag-undo ng hakbang, bawiin ang isang pagbabago, o itapon ang lahat](undo-changes.md)
- [Mga backup](../concepts/saving-and-history.md#mga-backup)
