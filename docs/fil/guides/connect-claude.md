<!-- source: e9f26daf -->
# Hayaang humubog o magtayo si Claude

Ikonekta ang Claude Code o Claude Desktop sa editor, saka hilingin kay Claude sa simpleng salita na hubugin ang lupa,
magtanim o magtayo.

**Kailangan mo:** ang editor, at ang [Claude Code](https://claude.com/claude-code) o Claude Desktop sa parehong computer.

1. Sa panimulang pahina ng editor, buksan ang **Mga setting** at hanapin ang seksiyong **Claude**.
2. I-on ang **Payagang kumonekta si Claude**. Opsiyonal: i-on ang **Maaaring magpatakbo si Claude ng script** para
   hayaan si Claude na sumulat at magpatakbo ng mga C# script (tumatakbo ang script nang may mga karapatan mo sa
   computer na ito; iwanan itong naka-off kung hindi ka sigurado).
3. Pindutin ang **I-save** (isinasara nito ang Mga setting), saka buksan ulit ang **Mga setting**: ipinapakita ng
   seksiyon ang "Listening on http://127.0.0.1:5731/mcp".
4. Idagdag ang editor kay Claude, isang beses lang:
   - **Claude Code:** pindutin ang **Kopyahin** sa tabi ng command, i-paste ito sa isang terminal at patakbuhin.
   - **Claude Desktop:** pindutin ang **Kopyahin** sa tabi ng mga linya ng configuration, idagdag ang mga ito sa Claude
     Desktop → Settings → Developer → **Edit Config** (`claude_desktop_config.json`), saka i-restart ang Claude Desktop.
5. Sa editor, buksan ang area ng isang mundo sa 3D, o ang Gawaan.
6. Hilingin kay Claude, halimbawa: "Patagin ang isang 40 m na parisukat sa gitna ng area, sementuhin ito, at maglagay ng
   isang bilog ng mga sulo sa paligid nito."
7. Panoorin si Claude na magtrabaho sa 3D view. Lumalabas ang bawat hakbang nito sa **Kasaysayan** bilang "Claude: …".
8. Panatilihin ito gamit ang **I-save sa mundo**, **I-apply nang live** o **I-save ang blueprint**, o bawiin ito
   (sa ibaba).

<!-- picture: claude-code-mcp.jpg: a terminal running Claude Code after typing /mcp, the list showing "valheim-editor" as connected, next to the editor window with a 3 × 3 area open -->

## Tingnan kung gumana

- Sa Claude Code, inililista ng `/mcp` ang **valheim-editor** bilang nakakonekta.
- Inilalarawan ng unang sagot ni Claude kung ano ang nakabukas sa editor.
- Lumalaki ang bilang ng nakabinbin sa top bar kasabay ng mga pagbabago ni Claude; walang naisusulat hangga't hindi
  ka nagse-save o nag-a-apply.

Kung sinasabi ni Claude na hindi nito maabot ang editor, tingnan ang
[Hindi makakonekta si Claude](../help/troubleshooting.md#hindi-makakonekta-si-claude).

## I-undo ito

- Ibinabalik ng `Ctrl+Z` ang huling hakbang, kay Claude man o sa iyo.
- Ang **Bawiin ang mga pagbabago ni Claude**, sa itaas ng Kasaysayan, ay nagtatanggal ng bawat hakbang na ginawa ni
  Claude sa nakabukas na area at pinapanatili ang sa iyo.
- Itinatapon ng **Itapon** ang lahat ng hindi naka-save.
- Para pigilan si Claude na maabot ang editor, i-off ang **Payagang kumonekta si Claude** at **I-save**. Ang
  **Bagong token** saka **I-save** ay nagla-lock out sa anumang kopya ng lumang command.

## Kaugnay

- [Claude](../claude.md): bawat setting, ang kayang gawin ni Claude, kaligtasan
- [Kaligtasan](../concepts/safety.md#mga-script-at-si-claude): ang naaabot ng mga script at ni Claude
- [Mag-undo ng hakbang, bawiin ang isang pagbabago, o itapon ang lahat](undo-changes.md)
