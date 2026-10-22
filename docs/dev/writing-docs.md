# Writing the documentation

The documentation is for people who play Valheim, not for programmers. Most of them open it with a question ("how do I
undo this?", "why can't I lift this higher?") and read a fifth of a page at most: headings and the first words of each
paragraph carry the meaning. The same Markdown is shown on GitHub and in the editor's Help window
(`editor/Desktop/HelpWindow.cs`), which carries every page of `docs/` but this folder, the release notes,
`architecture.md` and `development.md`.

## Four kinds of page

Each page is one kind. Keep them apart: a how-to that stops to explain the internals loses its reader.

| Kind | Folder | Answers | Shape |
|---|---|---|---|
| **Getting started** | `start/` | "I'm new, show me." | A lesson with a fixed result: numbered steps on a safe world, a picture of what you should see, nothing optional. |
| **How-to guide** | `guides/` | "How do I …?" | Titled as the task ("Restore one area from a backup"). What you need, numbered steps, how to check it worked, how to undo it. |
| **Tool reference** | `docs/` (one page per tool) | "What does this setting do?" | The tool template below. Describes; does not teach or explain internals. |
| **Concept** | `concepts/` | "Why does it work this way?" | Explanation for the curious: how the game stores things, why limits exist. No steps. |

Plus `help/`: troubleshooting (by symptom), questions, every shortcut, the glossary.

## The tool page template

```markdown
# Place

Put trees, rocks, crops, building pieces and spawn points into the world, one at a time or by the hundred.

**Where:** tool rail, EDIT section · **Key:** `T` · **Works in:** saved worlds, live, the Workshop (pieces only)

![What the tool does, in one picture](images/place.jpg)
*One sentence on what the picture shows.*

## Use it
1. …numbered steps for the most common use…

## Settings
| Setting | What it does |   ← in the order of the panel, top to bottom

## Modes / actions                ← one section each when the tool has several
## Shortcuts                      ← every key and mouse action of the tool
## Tips and limits                ← what surprises people; how to undo or recover
## Related                        ← 2–5 links: guides first, then concepts and tools
```

## Writing rules

- **Second person, present tense, active voice.** "Drag to raise the ground", not "The ground can be raised by dragging".
- **Lead with the point.** The first sentence of a page or section says what it is for. No "This page describes…".
- **Steps:** an intro sentence, then numbered steps, one action each, starting with a verb; say where before what
  ("In the **Place** panel, choose **Line**"); give the goal before the action ("To snap, hold `Shift`"). Optional steps
  start with "Optional:". One way of doing it, the best one; alternatives in Tips.
- **Short.** Paragraphs of 1–3 sentences; sections of about 100–300 words. Cut what does not help the reader act.
- **Bold** for labels exactly as the app shows them (**Save to world**, **Edit in 3D**); `code` for keys (`Ctrl+Z`),
  file names and typed text. Keys joined with `+`, no spaces.
- **Never copy a tooltip.** The page adds what the tooltip cannot: the why, the limits, how it combines with the rest.
- **Explain each idea once,** on its concept page or in the glossary, and link to it. The ±8 m limit, zones, pending
  changes, snapping, the support check are each explained in one place only.
- **Say how to recover.** Every page that changes the world says how to take it back (undo, Discard, History,
  backups).
- **No version history** in pages (the CHANGELOG has it) and no internal names (classes, ZDO keys) outside concepts
  pages that need them. Numbers that the code decides (sizes, limits, defaults) only when the reader needs them, checked
  against the code.
- **Callouts** sparingly, GitHub's form: `> [!TIP]`, `> [!NOTE]`, `> [!IMPORTANT]`, `> [!WARNING]`, `> [!CAUTION]`. A
  warning only where data can be lost.
- **Headings** in sentence case, with the words people search for first ("Copy and paste an area", not "Clipboard").
  Their ids are links: renaming a heading breaks links to it (the tests catch it).
- **Links** say where they go ("see [Ground limits](concepts/ground-limits.md)"), never "click here". Paths are
  relative to the page.

## Pictures

- Each picture shows exactly what the text next to it says, nothing covering the subject, framed so the subject fills
  the view: not so close it lacks context, not so far it is lost. Its panel shows the settings the caption names.
- Several configurations of a tool side by side (modes, shapes, before and after) beat one picture per tool.
- The description (`![…]`) says what is in the picture in under 155 characters; the caption in italics under it says
  what to notice.
- Pictures are taken by `tools/DocsScreenshots` (see [development](../development.md#the-driver---driver)), 1440 × 900,
  so they can be taken again when the app changes. A picture not taken yet is marked in the page with a comment where
  it goes, which neither GitHub nor the Help window shows:
  `<!-- picture: place-line-end-to-end.jpg: Place, Line mode, End to end on, a fence of 6 posts on a slope -->`

## The page map

Fixed names, so pages can link to each other. A tool page keeps its file name (links from outside keep working).

| Page | Kind | Holds |
|---|---|---|
| `README.md` | Home | Where to start; every page by part (the Help window's contents). |
| `start/install.md` | Getting started | Download, unpack, first start on Windows, Linux, macOS; finding Valheim; the plugin for live editing. |
| `start/first-edit.md` | Getting started | A copy of a world, open it, raise a hill, plant trees, save, undo, open it in game. |
| `start/first-build.md` | Getting started | The Workshop: a small hut, the support check, save it as a blueprint, paste it into a world. |
| `guides/*.md` | How-to | One task each (list in `README.md`). |
| `editor-basics.md` | Tool reference | The window: start page, top bar, rail, panels, camera, Walk and Fly, the area and Follow, saving, Discard, History, View panel. |
| `map.md`, `terrain.md` (sculpt and paint brushes), `shape.md`, `mountain.md`, `path.md`, `masks.md`, `measure.md`, `area.md`, `place.md`, `select.md`, `workshop.md`, `dungeons.md`, `new-world.md`, `live-mode.md`, `scripting.md`, `claude.md` | Tool reference | One tool or screen each, on the template. |
| `concepts/areas-and-zones.md` | Concept | `## Zones`, `## The area`, `## The locked edge`, `## Follow` |
| `concepts/saving-and-history.md` | Concept | `## Pending changes`, `## Saving a world`, `## Applying live`, `## History`, `## Backups` |
| `concepts/ground-limits.md` | Concept | `## The game's 8 m limit`, `## No limit`, `## Locations level the ground`, `## Limit marks` |
| `concepts/live-editing.md` | Concept | `## How it connects`, `## What is sent and when`, `## Following the game`, `## Several editors and players` |
| `concepts/objects.md` | Concept | `## What an object is`, `## Player-built pieces`, `## Snapping`, `## The support check`, `## Growing rules`, `## Spawn points` |
| `concepts/dungeons.md` | Concept | `## How the game keeps dungeons`, `## How the generator lays out a dungeon`, `## Furnishing, light and guards` |
| `concepts/safety.md` | Concept | `## What the editor writes`, `## Nothing leaves your computer`, `## Scripts and Claude` |
| `help/troubleshooting.md` | Help | By symptom: what you see, why, what to do. |
| `help/faq.md` | Help | Short answers, each linking to the full one. |
| `help/shortcuts.md` | Help | Every key and mouse action, by tool. |
| `help/glossary.md` | Help | Area, zone, pending, apply, bridge, plugin, BepInEx, seed, location, blueprint, … one or two sentences each. |
