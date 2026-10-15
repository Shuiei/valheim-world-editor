# Translating the editor

The editor's words are in catalogues, one per language: a JSON file of texts by key. English,
[`Core/Lang/en.json`](../Core/Lang/en.json), is the source every other catalogue is translated
from. The editor shows the language chosen under **Settings › Language**, by default the
computer's when there is a catalogue of it; a text a catalogue does not have yet shows in
English. A new language is taken when the editor starts again.

Not every text comes from the catalogue yet: every hover tip does (but the Inspector's), and
Settings; the rest moves over window by window. A catalogue made now keeps working as more texts
are added: the new ones show in English until translated.

## Making a catalogue

1. Copy `en.json` into the `lang` folder of the editor's data folder
   (`~/.local/share/ValheimWorldEditor/lang` on Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` on
   Windows; **Settings › Language** shows it), named after the language's code: `fr.json`,
   `de.json`, `pt-BR.json`.
2. Translate the values, never the keys. `lang.name` is the language's own name ("Français"), as
   Settings lists it.
3. Keep each `{0}`, `{1}`… : the editor puts a value there (a folder, a number). Their order in the
   sentence can change. A text whose places differ from the English is not used (English shows).
4. Choose the language in **Settings**, restart the editor and look. Keys you leave out, or delete
   from your copy, show in English.

The same folder also takes corrections: a file there with the code of a catalogue the editor
carries is laid over it, text by text.

To have a catalogue carried by the editor, open a pull request adding it to `Core/Lang/` (keys
only from `en.json`; `LangTests` checks them).

## For developers

- A text the user sees goes in `en.json` under a key named after where it is shown
  (`settings.port`, `common.cancel` for words used everywhere) and is asked for with
  `Lang.T("settings.port")`, or `Lang.T("settings.worldNotFound", path)` with values for its
  `{0}`… places (put in with the invariant culture). A hover tip is a `tip.` key, given with
  `.Tip("settings.port")` (see `Desktop/Tips.cs`).
- Write the key in full in the code, even when it is passed along: `LangTests` checks that every
  key asked for is in `en.json` and every key of `en.json` is asked for somewhere.
- Texts for Claude (the MCP tools' descriptions and answers), the log, command-line output and
  file formats stay in English and outside the catalogue. A text shown to the user that Claude is
  also given (a piece's cost) asks for English with `Lang.En` on Claude's side.
- Windows are built once, in the language taken at start (`Lang.Use` in `Program.Main`); nothing
  has to listen for a change of language.
