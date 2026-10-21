# Translating the editor

The editor's words are in catalogues, one per language: a JSON file of texts by key. English,
[`editor/Core/Lang/en.json`](../editor/Core/Lang/en.json), is the source every other catalogue is translated
from. The editor shows the language chosen under **Settings › Language**, by default the
computer's when there is a catalogue of it; a text a catalogue does not have yet shows in
English. A new language is taken when the editor starts again.

Not every text comes from the catalogue yet. These do: every hover tip (the Inspector's tips made
from an object's data keep its names as the save has them), Settings, the small dialogs
(questions, messages, a blueprint's details) and the words they share, the History panel, the save bar with what saving, applying, discarding, leaving
and quitting ask and say, the brush options with the stamps and the cursor readout, the Measure,
Mask, Script, Path, Cave, Shape, Mountain, Select and Paste tools, the biome names, the 3D editor's
area bar with the live game's messages and players, the View panel, the help card, the status
bar's readouts, the Blueprints panel, the Workshop and its Build panel, the Place and Area tools,
the Dungeon tool with its generator, the world map page, the New world page, the start page, what
the status bar and the History say of Claude's changes, and the Inspector; the rest moves over
window by window. A catalogue made now keeps working as more texts are added: the new ones show in
English until translated.

## Making a catalogue

1. Copy `en.json` into the `lang` folder of the editor's data folder
   (`~/.local/share/ValheimWorldEditor/lang` on Linux, `%LOCALAPPDATA%\ValheimWorldEditor\lang` on
   Windows; **Settings › Language** shows it), named after the language's code: `fr.json`,
   `de.json`, `pt-BR.json`.
2. Translate the values, never the keys. `lang.name` is the language's own name ("Français"), as
   Settings lists it.
3. Keep each `{0}`, `{1}`… : the editor puts a value there (a folder, a number). Their order in the
   sentence can change. A text whose places differ from the English, or with a stray `{` or `}`,
   is not used.
4. Choose the language in **Settings**, restart the editor and look. Keys you leave out, or delete
   from your copy, show in English.

The same folder also takes corrections: a file there with the code of a catalogue the editor
carries is laid over it, text by text. There, a key you leave out (or a text that is not used)
keeps the carried translation; English shows only where neither has the text.

To have a catalogue carried by the editor, open a pull request adding it to `editor/Core/Lang/` (keys
only from `en.json`; `LangTests` checks them).

## For developers

- A text the user sees goes in `en.json` under a key named after where it is shown
  (`settings.port`, `common.cancel` for words used everywhere) and is asked for with
  `Lang.T("settings.port")`, or `Lang.T("settings.worldNotFound", path)` with values for its
  `{0}`… places (put in with the invariant culture). A hover tip is a `tip.` key, given with
  `.Tip("settings.port")` (see `editor/Desktop/Tips.cs`).
- Write the key in full in the code, even when it is passed along: `LangTests` checks that every
  key asked for is in `en.json` and every key of `en.json` is asked for somewhere.
- Texts for Claude (the MCP tools' descriptions and answers), the log, command-line output and
  file formats stay in English and outside the catalogue. A text shown to the user that Claude is
  also given (a piece's cost) asks for English with `Lang.En` on Claude's side.
- Windows are built once, in the language taken at start (`Lang.Use` in `Program.Main`); nothing
  has to listen for a change of language.

### Naming keys

- The first part is the window, spelled as its tip keys are (`area`, `place`, `select`, `path`,
  `blueprints`, `newWorld`…). Shared prefixes: `common.*` for a word used in three windows or more
  (OK, Yes, Cancel, Save, Discard, Stay, Keep them…), `dialog.*` for the small dialogs, `save.*` for
  the save, apply, discard and leave questions, `biome.*` for biome names.
- The middle part says what kind of text it is: `label` (row labels, buttons, switches), `head`
  (section headings), `hint` (muted explanations), `msg` (status bar and results), `undo` (History
  labels), `error` (problems shown in a panel), `ask.<name>.title|text|yes|no` (questions), or a
  list's name for its entries (`brush.falloff.flatTop`). A window's lone texts can do with two
  parts (`settings.port`). The last part is camelCase.
- One English word has one key: reuse `common.*` rather than adding "Save" again. Claude's names
  for the window's controls are worked out from the English (`Lang.EnglishOf`), which needs it.
- A text moved into the catalogue keeps its wording; change words in a change of their own.
- An enum's texts come from a `switch` naming each full key, never from `"prefix." + value`.

### Values, plurals and sentences

- Values go into `{0}`, `{1}`… with their format and unit inside the text (`"{0:0.0} m"`,
  `"{0} × {1} zones"`), as raw numbers and names, not formatted strings. A key is always asked for
  with the same number of values; a text asked for with values doubles its literal braces (<code>&#123;&#123;</code>),
  one asked for without never has any.
- A name already in the user's language (a biome, a kind of object) goes in as a value. Do not
  change its case to fit it in a sentence (`ToLowerInvariant`): write the sentence around it.
- There are no plural rules. Keep "object(s)" in one text; where the code already picks between
  one and many, use two full keys (`….one`, `….many`). Never add an ending in code.
- One key per whole sentence. An optional extra sentence is a key of its own, joined with a space;
  never glue parts of a sentence together. Lists of translated names are joined with ", ".

### What stays English

- Everything Claude reads: the MCP tools' descriptions and answers (`ClaudeTools*`, `ClaudeGuide`),
  the log and console, command-line output, file formats (`prefs.json`, the history file,
  blueprints), game data (prefab, item and piece names, object data keys), the names of products and
  mods inside the texts (Valheim, Homestead, Steam), and the messages of exceptions (the sentence
  around one is translated).
- Claude's steps in the History start with the mark `Claude: ` (`ClaudeTools.ClaudesStep`), which
  stays English: Take back Claude's changes finds Claude's steps by it. The words after it are
  translated.
- A text shown to the user that Claude is also given has both: the user's language from `Lang.T`,
  Claude's English from `Lang.En` (`PieceCost.Describe(english: true)`).
- The status bar's message is read back by Claude. Put it there with `Msg("area.msg.applied", n)`,
  or `Msg(said)` with a `Said` (`Said.Of(key, values…)`, `Said.Raw(text)` for a text the same in
  both, `+` to add a sentence): the window shows the user's language and keeps the English, which
  Claude's answers read (`MainWindow.LastMessageEn`). Add to the last message with
  `LastSaid + …`, never by reading `MessageText.Text`.
- A remembered list choice (`Prefs.Bind`) is kept in English, so it comes back in any language.

### Checking a window

The tests have a made-up language, `PseudoLang`, that wraps every catalogue text as `⟦…⟧`.
`PseudoLang.Untranslated(window)` lists the texts a window shows outside `⟦⟧`, that is, not from
the catalogue. Each window has a `<Window>LangTests` test that builds it under that language and
expects none; such tests run in the `DataDir` collection, since the language is shared.
