# Changelog

All notable changes to the Valheim world editor and its WorldEditorBridge plugin, which share one
version number. Newest first. Changes to the plugin are under **WorldEditorBridge** in each version;
the plugin is unchanged in versions without that part.


## v1.47.30 — unreleased

### Fixed
- **Last round of code-quality clean-ups.** A handful of small spots in the dungeon corridors, homestead tags, terrain contacts and growth code were tidied. Nothing behaves differently.

## v1.47.29 — 2026-10-17

### Fixed
- **More code-quality clean-ups.** Several helpers, a texture decoder and a few comparisons were tidied without changing how anything works. Nothing behaves differently.

## v1.47.28 — 2026-10-17

### Fixed
- **Every switch in the code now has a fallback case.** Values that matched before behave as before, and a value that matched nothing still does nothing. Nothing behaves differently.

## v1.47.27 — 2026-10-17

### Fixed
- **Errors the editor handled quietly are now written to its log.** Problems such as a file that cannot be read, a game bundle that cannot be opened, a bridge that does not answer or a model that cannot be loaded now leave a line in `ValheimWorldEditor.log` saying where, what was being done and what went wrong. Personal details like your home folder, file paths, addresses, user names and saved server accounts and tokens are replaced with placeholders first, so the log is safe to share.

### WorldEditorBridge
- **Errors the plugin handled quietly now show up in BepInEx's log.** Things like a listener that could not be stopped, an error answer that could not be sent or an unreadable mod manifest are now logged with where and why they happened. The server's home folder is hidden as `~`.

## v1.47.26 — 2026-10-17

### Fixed
- **Private keys are no longer stored in the project's test files.** The SSH key tests now create two throwaway keys when they start, so no real-looking key sits in the repository. Nothing changes in the editor.

## v1.47.25 — 2026-10-17

### Fixed
- **Loading a stamp picture that could not be opened no longer stops the editor.** If the picture was moved, deleted, unreadable, in use or too large, the status bar now says "That picture could not be read." and the problem goes to the log. Files over 256 MB, or pictures that would need more than that once decoded, are not read.

## v1.47.24 — 2026-10-17

### Changed
- **The Inspector now follows the language chosen in Settings.** Its buttons, headings, explanations, messages and the names of common values like Text, Builder name and Contents are all translatable. Object, item and value names stay as the save has them, and in English nothing changes.

## v1.47.23 — 2026-10-17

### Changed
- **The status bar and History messages about Claude's changes follow the language chosen in Settings.** Messages like "Claude moved 3 object(s); Ctrl+Z puts them back." and the step names in History are translatable, while the "Claude: " mark stays so Take back Claude's changes still finds them. In English nothing changes, and Claude is still answered in English.

## v1.47.22 — 2026-10-17

### Changed
- **The start page now follows the language chosen in Settings.** Its menu, Home tiles, My game steps, saved servers and server form, saved worlds, Workshop blueprints, Homestead state and the card about the game's look are all translatable. In English nothing changes.

## v1.47.21 — 2026-10-17

### Changed
- **The New world page now follows the language chosen in Settings.** Its boxes, buttons, search wishes and results, the lists beside the map, the map switches and tooltips, the reasons a name or seed is refused and the progress messages are all translatable. Claude is still told in English, and in English nothing changes.

## v1.47.20 — 2026-10-17

### Changed
- **The world map page now follows the language chosen in Settings.** Its buttons, switches, pending-save line, live players line, search, zone filter and status lines are translatable, and so are the biome names. In English nothing changes, and world and object names stay as they are.

## v1.47.19 — 2026-10-17

### Changed
- **The Dungeon tool and its generator now follow the language chosen in Settings.** The panel, generator settings, summaries, dungeon kind and entrance names, and the status bar and History lines for dungeon edits are all translatable. In English the only change is that a room added with two or more doors now says how many instead of repeating "and a door".

## v1.47.18 — 2026-10-17

### Changed
- **The Area tool now follows the language chosen in Settings.** Its panel, hints, clipboard line, Reset zones question, file pickers, status messages and History steps are all translatable. Claude still gets action names in English, and in English nothing changes.

## v1.47.17 — 2026-10-17

### Changed
- **The Place tool now follows the language chosen in Settings.** Its modes, line shapes, presets, settings, hints, questions, status messages and History steps are all translatable. In English nothing changes.

## v1.47.16 — 2026-10-17

### Changed
- **The Workshop and its Build panel now follow the language chosen in Settings.** The Build panel, Library, cost line, save bar, support switch, the questions before leaving, and the status and undo messages are all translatable. Claude is still told in English, and in English nothing changes.

## v1.47.15 — 2026-10-17

### Changed
- **The Blueprints panel now follows the language chosen in Settings.** Its buttons, Homestead banner, blueprint lines and costs, replace and delete questions, import picker and status messages are all translatable. Claude is still told in English, and in English nothing changes.

## v1.47.14 — 2026-10-17

### Changed
- **The View panel, help card and status bar readouts now follow the language chosen in Settings.** Switches, object kinds, overlays, 3D resolutions, help controls, frame rate, selection and walking or flying lines and the paste height are all translatable. In English nothing changes.

## v1.47.13 — 2026-10-17

### Changed
- **The 3D editor's area bar and the live game's messages now follow the language chosen in Settings.** The area arrows, size, Follow, Discard, Reload and Auto controls, their questions and messages, the live players and the New world window title are all translatable. In English nothing changes.

## v1.47.12 — 2026-10-17

### Changed
- **The Select and Paste tools now follow the language chosen in Settings.** Their panels, messages, questions, Make player built and the Built by choices are all translatable. In English nothing changes, and the name a mod gives its linked objects ("vehicle") stays as the mod wrote it.

## v1.47.11 — 2026-10-17

### Changed
- **The Shape and Mountain tools now follow the language chosen in Settings.** Their panels, formula mistakes, status messages and History steps are all translatable. In English nothing changes, and scripts still name a mountain preset in English.

## v1.47.10 — 2026-10-17

### Changed
- **The Path and Cave tools now follow the language chosen in Settings.** Their actions, rows, cave presets and roofs, line length and Apply messages are all translatable. In English nothing changes, and Claude's dig_path is still answered in English.

## v1.47.9 — 2026-10-17

### Changed
- **The Measure, Mask and Script tools and the biome names now follow the language chosen in Settings.** The tape hints and values, Mask rows and warnings, the Script panel and its messages, and the biome names in the Mask and map zone filter are all translatable. In English nothing changes, and Claude still names biomes, masks and scripts in English.

### Fixed
- **A run of the Script tool's first example was named "Script: script" in History.** The example shown when the panel opens was not actually chosen in the list, so the run had no name. It is now chosen, and the step is named after it.

## v1.47.8 — 2026-10-17

### Changed
- **The brush options now follow the language chosen in Settings.** Rows, shapes, falloffs, stamps, brush messages, the Workshop's Build label and the status bar cursor readout are translatable, and a built-in stamp chosen as the brush shape is still remembered after a language change. Claude still asks for stamps by their English names, and in English nothing changes.

## v1.47.7 — 2026-10-17

### Changed
- **The History panel and the save bar now follow the language chosen in Settings.** The History card, the Save to world and Apply live buttons, the pending changes line, and the questions and messages for saving, applying, discarding, leaving and quitting are translatable, as are the brush and Regrow failure messages. In English nothing changes.

## v1.47.6 — 2026-10-17

### Changed
- **The small dialogs and their shared words now follow the language chosen in Settings.** Questions, messages, blueprint details, the OK, Yes, Cancel, Stay, Keep them, Leave anyway, Keep building and Discard buttons, and the tooltip for a mod's extension file in Settings are translatable. In English nothing changes.
- The status bar keeps an English copy of its message so Claude is always told in English, whatever language the window shows.
- Remembered list choices (brush shape and falloff, Area action, Shape preset, 3D resolution) are saved in English, so they still come back after the language is changed. Choices saved so far are already in English.

## v1.47.5 — 2026-10-17

### Changed
- Internal test clean-up now reports errors instead of hiding them, and waits for the stand-in game, plugin and SSH server to stop before removing their folders. Nothing in the editor changes.

## v1.47.4 — 2026-10-17

### Fixed
- **The release packages could not be built.** Publishing the single-file editor stopped because the script process could not find the editor's own file in that kind of app. It now finds it from the app's folder, and the published editor starts its scripts as before.

## v1.47.3 — 2026-10-17

### Changed
- Saved objects are now read by one shared reader instead of three copies of the same code, used when loading a world, copying a template object and opening the inspector. Loading, copies and saves give the same results as before, and loading is just as fast.

## v1.47.2 — 2026-10-17

### Changed
- Internal code cleanup of the main window: handlers are named methods and the editor's keys live in one ordered table. Every key, its precedence and whether it is taken stay the same, so nothing changes for users.

## v1.47.1 — 2026-10-17

### Changed
- Internal code cleanup: the 3D view's very large source file is split into smaller parts by topic. Nothing in the editor changes.

## v1.47.0 — 2026-10-17

### Added
- **Your changes are kept when the game saved the world since you opened it.** The editor now offers to bring them onto the game's newer save, asks once per kind where the game changed the same zone or object, and writes nothing until you save ([Saving](docs/editor-basics.md#saving)).

## v1.45.110 — 2026-10-17

### Fixed
- **Edits to a world saved by Valheim 1.0.20 can be saved.** The game now saves worlds in save format 42, which the editor used to refuse. The editor now saves both formats 41 and 42, keeping each file in the format it was read in, and new worlds are made in format 42, which Valheim 1.0.17 and earlier cannot open.

## v1.45.109 — 2026-10-17

### Fixed
- **A changed server key now names every saved entry still holding the old key.** Before, the refusal named only one line to empty, but another saved account on the same server and port also pinned the key, so the connection stayed refused. The message now lists each saved entry to empty.
- **A worldeditor.json file placed directly in BepInEx/plugins no longer offers an extension named "plugins".** Such a file belongs to no mod, so extensions now come only from a mod's own folder. Its data and prefab notes are still read.
- **A line break in a server's details can no longer add lines to servers.cfg.** Details with a line break could have slipped in extra lines and replaced the saved server key. Connecting now refuses such details, a name's line break becomes a space, and out-of-range ports fall back to the defaults.
- **A live server's zone answer can no longer make the editor use unlimited memory.** A misbehaving service on the tunnelled port could have filled memory with an oversized answer. Answers larger than a whole world snapshot are now refused.
- **Claude can no longer create a world outside the game's worlds folders.** Before, it could write world files into any folder the editor could write to. Folders that are not the game's worlds folders are now refused.
- **A damaged history file no longer makes opening a world use gigabytes of memory.** A corrupt count in the file could reserve gigabytes before reading anything. Entries are now kept as they are read, and a negative count is refused.
- **The editor's language now follows the system's display language on Windows and macOS.** Before, Windows used the regional format and macOS apps opened from Finder always started in English. Now Windows reads your display languages and macOS your preferred languages.
- **A failed plugin check no longer turns a live move into a destroyed and remade object.** A slow or failed check made the editor think moving was impossible, so a moved chest was deleted and recreated under a new id and ContainerSigns doubled its signs. The check now happens at each Apply live, a failed check stops the apply, and a restarted game is noticed.
- **Editing a chest no longer wears the items in it.** Durability was cut down when saved, so about one value in five came back a hundredth lower after each save. It is now rounded instead.
- **A History action on a step that was undone meanwhile no longer undoes everything or doubles the change.** If a step was undone or dropped while the History panel asked for confirmation, Back to here could undo every step and Remove could apply the change again. These actions now do nothing for a step that is no longer where it was picked.
- **A blueprint piece with blank or zero rotation fields is placed unturned.** Such a rotation used to produce broken angles in the clipboard and the world. It now counts as no turn.
- **One unreadable blueprint file no longer empties the Blueprints list.** A file without read access used to hide every blueprint. It is now left out and logged, and the list reads each file's summary faster.
- **Blueprints named after a Windows device (con, nul, com1…) can be saved on Windows.** Their file names used to be refused there, so they now start with "_". A Homestead blueprint name cut at 64 characters no longer ends in a dot, so its picture still matches.
- **A world's backups are found when its folder ends in "/" on Windows, and an unreadable folder no longer fails the search.** Before, a trailing slash gave an empty name and no backups, and a neighbouring folder without access broke the search. Both slashes are now handled, and an unreadable folder simply has no backups (logged).
- **Characters whose names hold letters beyond ASCII are found.** A character like "Ørjan" never matched its own file because of how the file name was compared. Names are now compared as text, ignoring case.
- **The recent worlds list handles odd stored data and no longer lists a world twice.** Bad stored data could break opening a world, and the same folder written with a trailing slash or different letter case on Windows appeared twice. Empty lists now read as empty, and entries are compared by their full path.
- **A broken mod bundle no longer fails reading a model or texture, or reads another file's data for it.** Bad sizes or offsets in a bundle could throw an error or read the next file's bytes. Ranges outside the file are now refused (logged) and read as no data.
- **A broken mod mesh no longer fails drawing a building's picture.** Bad submesh sizes or triangle indices used to make drawing fail. They are now checked, and triangles pointing outside the mesh are left out.
- **Following models into other bundles no longer leaves game files open or stops after one bad bundle.** Already-open bundles were opened again (which on Windows can block Steam updates), and one failing bundle left later references unfound. Open bundles are now read as they are, a bad one is skipped (logged), and the list counts as complete only after every bundle was gone through.
- **A shader whose program lengths do not fit together is now reported as unreadable.** Before, it failed with an unrelated error and the message always said "terrain shader". Sizes are now checked first, bad parts are refused, and the message names the right shader.
- **A texture array in a format the editor does not read is now left out instead of read as another format.** Unknown graphics formats could be mistaken for similar ones, such as R8 read as ARGB32. They now count as not read.
- **A game bundle that cannot be read is closed at once during the game-data scan.** One that opened but failed to read used to stay open until the scan ended. It is now closed again.
- **A mod folder that cannot be read no longer stops the start page checking your game.** The search for the plugin under BepInEx/plugins failed at the first folder without access. Such folders are now skipped.
- **A heightmap picture with odd heights written in it no longer breaks its import.** Values like NaN, Infinity or 1e39, or a highest height not above the lowest, used to cause errors or bad imports. Such values are now ignored and the ground's own heights are used.
- **Two mods' bundles whose names differ only by punctuation or letter case are both drawn.** Before, names like "Mod A.bundle" and "Mod_A.bundle" counted as one, so the older one's models were never drawn. Bundles are now matched by their exact names.
- **Renaming a hand-edited Homestead blueprint shows its new name.** Header lines with spaces in front were read but not replaced when the name, description or tags changed, so the old line won. They are now replaced.
- **A copy with numbers stored as text can be saved as a Homestead blueprint.** A number written as a string like "1.5" used to cause an error. Such numbers are now read correctly.
- **One odd line in the game's localisation table no longer stops every world from opening.** A carriage return followed by more text in an unquoted field used to fail the build-cost table and the whole game data pass. It now ends the row, as a line break does.
- **Steam rewriting its library list no longer fails finding the game.** If Steam was writing libraryfolders.vdf (or the file was unreadable), the game check and game data export failed. The search now goes on without that list (logged).
- **A prefab name found in two game bundles is described by the same object in every catalogue.** Some catalogues kept the first object and others the last, so they could disagree. All now keep the first.
- **A broken Vulkan shader program is now reported as unreadable.** Bad data used to cause unexpected out-of-range errors in the terrain drawing. It is now checked and refused as unreadable data.
- **The New world page reads the game's location rules again once the game has run.** Rules that could not be read stayed missing for the whole session, so bosses and traders were absent even after starting the game refreshed its log. They are now read again when the game's log changes.
- **Two of the game's location lists with the same counts are both read.** A second list with identical counts used to replace the first, losing its locations and counting the first twice. Such lists are now taken one after the other in the game's order.
- **A very large terrain texture array is now reported as too large instead of failing with an overflow.** Its size used to come out negative for big arrays like 4096 × 4096 with 32 slices. It is now worked out correctly, and an array too large is refused with a clear message.
- **A tunnel that fails after logging in no longer stays open with the start page waiting.** A bad plugin port, a local port taken meanwhile or another failure left the connection open while the page kept saying "Connecting to …". Ports are now checked first, and any failure closes the connection and is reported.
- **Proton worlds are listed once, and Flatpak Steam's are found.** Worlds played through Proton were listed twice (and on Windows, so were worlds with different letter case or a trailing slash). Duplicates are now merged, and Flatpak Steam's Proton folder is searched too.
- **A world with a stray save file is listed, and one that cannot be read is reported in the log.** A file with an oversized save number used to drop the world from the list silently. Such files are now skipped, and only read errors leave a world out, each logged.
- **Mod models taken while a game's catalogue came in could be lost.** Two updates overlapping could drop one of them. They now take turns safely.
- **A prefab a game update made the game's own no longer shows under Mods or as "From a mod".** The kept mod catalogue never dropped such entries. Names that are now the game's own prefabs are left out, along with their pieces, costs and models.
- **Two saves of the same app file at once could fail or keep a half-written one.** Both writers used the same temporary file and could delete each other's. Each write now has its own temporary file, deleted on any failure.
- **Dungeon generation could rarely stop with an error on a random pick.** Rounding could leave a weighted pick with no result. It now falls back to the last item.
- **The map's zone filter ignored what was placed this session.** A building placed but not yet saved counted as zero pieces, so a "far from buildings" filter could pick its zone for reset. Added objects now count.
- **Linked objects of two mods could join up, and a damaged copy pasted two groups under one id.** Parts were matched by id alone regardless of which mod's rule held them, and duplicate ids in a copy gave both the same new id. Parts are now matched under their own rule, and each duplicate gets its own new id.
- **A shader blob could fail to read on a chance "LOMS" in its data.** The search for the next program could start inside the previous program's data, and huge sizes caused errors. The search now starts after the decoded program, and impossible sizes are skipped.
- **Mod models read by several threads at once could fail.** Models were built on first read from several threads at once. They are now read once when taken in.
- **A pit arena's four corner columns never stood.** The ledges took up the room the columns needed. The columns are now placed first (away from the doorways), then the ledges.
- **Some dungeon stones sank into their plinth or floated over it.** A shrine's memorial stone and dolmen were lifted by the wrong prop's height (a large memorial stone sank 0.13 m). The right height is now used.
- **Dungeon columns could rise through the roof into the room above.** Tall columns could poke 1.3 m into the room two levels up. The last segment may now rise into the roof slab but never past it.
- **Caged foes could walk out of a cell block's end cells.** The first and last cells had no front wall. Their fronts now run on to the end walls.
- **The game's-rooms dungeon was placed under a different name than the panel showed.** The Generate panel named it after the chosen style, while the placed dungeon is named after its biome. The panel now shows the biome name.
- **A portal at a generated dungeon's arrival stood in the entrance's floor.** It was set 5 cm over the slab whatever lay on it. It is now 5 cm over the entrance's floor finish (marble tiles, boards).
- **Chosen foes and Respawn off were not kept in some dungeon rooms.** Elites, swamp burial chamber foes and an ossuary's bone pile ignored your choices. Chosen foes now replace them, and the bone pile is left out when foes are chosen or nothing respawns.
- **A dungeon of the game's rooms failed with a meaningless error when the game's data had none to join.** It now says the game's data has no rooms to join, which can happen after a game update changes its dungeons.
- **A throne room's war table could lie across the dais' steps or a doorway.** The long table was placed with no check. It now stands only where every spot along it is free.
- **A corridor gate could stand on a pilaster's band of wall.** The gate ignored the pilasters placed every 8 m. It now keeps off their bands.
- **A Black Forest dungeon's sword-skeleton elite came back as an ordinary skeleton.** It was paired with a spawner that also makes archers. It now stands once, as the right creature.
- **A dungeon blueprint whose name produced the lowest possible number failed to paste, and a negative seed gave odd portal tags like "dg-123".** The portal pair's tag was worked out in a way that broke for extreme values. It now handles them properly.
- **Support was worked out wrong for scaled pieces.** The centre of mass was not scaled with the piece, so support distances and angles came from the wrong point. It is now scaled too.
- **Pointing the hammer from inside a capsule-shaped piece could place a piece at a broken position.** A ray starting inside the capsule gave an invalid result. It now misses, as it does from inside a sphere or a box.
- **Damaged dungeon or location scan data could crash or hang placing rooms.** Following a part's parents had no limit or loop check. The walk now ends at a missing node or after it gets longer than the list.
- **A location scan with "teleports": null loaded no location prefab at all.** Anything other than a list now counts as none instead of breaking the load.
- **A new world that failed halfway blocked another try in the same folder.** Its files were written before a flat zone was found, so the next try said the folder "already holds a world". Anything written is now removed on failure, along with the folder if it was made for it.
- **Connecting with an odd address or a token holding a line break failed with an exception instead of a message.** An address like "httpsrv:5182" or a token with a line break caused an unhandled error. It now says the address is not valid (only http and https) or that the token cannot be sent.
- **A PNG file with no header was refused as a "0-bit PNG".** The message now says the header is missing. Writing a 16-bit grey PNG also refuses data that cannot make a valid picture.
- **Lifting ground could treat an object at a half metre as standing on a different point than a disc centred on it.** Two parts of the code rounded halves differently. Both now round as the game does (halves up).
- **The save's read-back check missed smoothing and paint changes in zones not edited.** A bug that altered such data would have passed and the old save been deleted. Those are now compared too.
- **Resetting the ground of a zone that held two terrain objects could refuse the save ("terrain zones were lost").** The check allowed only one lost terrain object per reset zone. It now allows however many the reset zones had.
- **Saving a zone's ground with all its terrain objects deleted was refused.** The check found the zone missing after the deleted objects were patched. Deleted objects are now left out and the zone gets a new one.
- **Refusing a world in an older save format named the wrong format.** The message gave the first file's format instead of the first one that differs. It now names the right one.
- **A corridor gate's band of wall could fall in a stave or wood dungeon.** A gap at one end let it fall. It now runs on into the walls.
- **The save message could count the objects a zone reset cleared wrongly, even below zero.** Repeated or irrelevant ids were counted as removals. It now counts the objects the deletions really removed.
- **An object changed in the inspector lost the high flag bits it was saved with.** Some unused flag bits were dropped. They are now kept and written back.
- **A player whose pieces had all gone from the live game could still be named the world's top builder.** Their count stayed in the list at zero. It is now removed when it reaches zero.
- **The ground around locations was grass where the game shows dirt, paving or cleared ground.** The editor read the paint from location and rock terrain modifiers but never applied it. It is now applied with the game's falloff, so ground colour, Regrow's vegetation rules and the paint Uplift restores around locations match the game.
- **After a ground disc was added or taken away, a zone could keep the ground of the old discs.** A zone still being built with the old modifiers was added to a cache that had just been cleared. The modifiers and zones built with them are now swapped together.
- **A location rule of no biome, or of a biome flag the map does not know, kept the seed's locations from being laid out.** Such a try used to fail and drop the whole layout. It now finds no zone and the next try is made.
- **A heightmap picture imported into a selection sat about a metre off its outline, and its size was one point too wide on each side.** It was fitted to a padded box. It is now fitted to the selection's own grid box.
- **A selection whose outline crossed itself showed the wrong area (a figure eight about 0 m²), and Place scattered too few or too many objects in it.** Crossed parts cancelled out in the area calculation. The area now matches what the selection actually picks.
- **A box selection dragged along one axis was kept though it had no area.** Actions then changed nothing or edited a line at half strength. A box is now dropped when it is under a metre across either way.
- **Area Replace replaced nothing when the kind's name held " (" (a mod's).** The kind's name was cut at that point. The full names are now kept.
- **A terrain modifier that smooths with a radius of 0 (odd mod data) made a broken height at its middle.** Such a modifier now smooths nothing.
- **Claude's set_control could set a slider or number to NaN, and failed on a number too big.** NaN and infinities are now refused, and a number box's value is held to its range.
- **Claude's remove_objects and select_objects counted a repeated id as one that was not an object.** Repeats in the list given caused a wrong count. They are now compared with the distinct ids given.
- **Shields and banners Claude put in a chest always got the first style.** The item's variant was never passed on to the chest. It is now kept.
- **Claude's set_dungeon_door answered "Now pending" when no door was put in, and close_dungeon_ends could answer nothing.** A joint without a door of the dungeon's kind was skipped silently. The tools now answer what the Dungeon tool said, or "Nothing changed."
- **Claude's read-only looks changed your view: support_check left the Workshop's Support switch on, and screenshot left the camera moved.** The support check now works without the switch, and the camera is put back after the picture.
- **Claude's forest failed with a confusing script error for a name ending in a backslash.** Names are now written safely into the generated script.
- **On slopes, pieces Claude placed on the ground floated or sank by up to half a metre against the height ground_at gave.** Placing, moving and replacing used the nearest grid point's height. They now use the ground between grid points, as ground_at does.
- **Claude tools given only one coordinate of a point went on without a word.** list_objects dropped its "near" filter and move_objects and copy_objects mixed the given x with the objects' middle z. These tools and several others now refuse a lone coordinate.
- **Claude's replace_objects lost each object's size, and replacing a ship's root left its parts hanging on nothing.** The new object now keeps the old one's scale, and a linked group's root takes its parts with it, as the Area tool's Replace does.
- **Claude's build_roof gave a bare error, or kept going to a runaway piece count, when the roof piece had too few snap points.** It also failed when none sloped one way in the game data. It now says why no roof was laid.
- **Claude's blueprint tools kept using an out-of-date Homestead folder.** A Homestead installed or moved since the Blueprints panel last looked was not seen. They now look for Homestead again.
- **Opening the Workshop failed when there were no pieces to build with.** This happened when the game's data was missing or every piece was hidden by mod manifests. It now chooses none and says so.
- **Live, a dungeon laid out or gone in the game kept showing as it was until an unrelated edit.** The dungeon view did not notice changes coming from the game. It now follows the session's changes too.
- **A formula mixing && and || worked out differently from the web editor.** For example, 1 || 0 && 0 gave 0. && now binds tighter than ||, as in JavaScript.
- **Formulas refused numbers written with a capital E (2E3), and sign() of a NaN failed at every point.** Exponents now take e or E, including after a leading point like .5e1, and sign(NaN) is NaN, as in JavaScript.
- **Erode made or destroyed ground at soft edges and locked points, and drops sped up going uphill.** Sediment and ground changes did not match, drops took their load with them when they stopped, and the speed rule was backwards. A drop now carries exactly what the ground gives or takes, leaves its load when it stops, never digs below or fills above where it goes (no runaway holes), and slows going uphill.
- **The Area tool never said when its ground actions (Erode among them) reached the game's ±8 m limit.** Strokes, Shape and paths already did. The Area panel and Claude's shape_ground now say so.
- **The game's look banner said the editor works without the game's files, though no world, server or Workshop opens without them.** Both hints now say worlds need the game's files.
- **Claude's reset_zones with a huge rectangle could hang the editor or run out of memory.** Every zone in the rectangle was listed before outside ones were dropped. The range is now cut to the area's zones first.
- **In the Generate panel, with the biome's style, choosing another biome kept the old biome's decay.** The decay now follows the biome too while Style is the biome's.
- **Delete in the Dungeon tool could remove the wrong room after an undo.** The selection was a bare position in the room list. It now remembers the room's prefab and place and is cleared when the room is gone.
- **Copies pasted with a negative gap could land at another height than the preview showed.** Each copy read the ground after earlier copies had changed it. Every copy's height is now read before any ground is shaped, as in the preview.
- **After Discard, steps undone in another area could still be redone.** Discard dropped the open area's redo steps but kept those set aside for other areas. It now drops them too, as a new edit does.
- **Removing a change from the history could unmark ground that a later step had modified.** A later flatten back to the original height lost the modified mark. A mark changed by a later step is now kept.
- **Copying an area with no objects reported success, and pasting it did nothing.** Ctrl+C and Ctrl+X now say there is nothing to copy and keep what the clipboard had.
- **A blueprint of ground more than 327.67 m above or below its middle lost height.** Heights were cut to the web editor's range. They are now saved as they are.
- **Opening an area could fill it with removed objects nothing referred to.** Objects from steps kept aside for other areas were added too. Only objects from steps kept in the area are added now.
- **A dungeon whose rooms failed to read in an unexpected way made the editor retry on every frame.** The failure is now logged and the rooms shown are kept until the area changes.
- **A brush stroke or drag could keep going after the 3D view lost the mouse.** If the release happened elsewhere (a dialog or another window), the stroke kept painting until the next click. Losing the pointer now ends the stroke where it last was.
- **A late change from the previous area could break or clear the 3D view of the new one.** The view kept following old areas, which could show wrong objects, cause an error or clear the selection. It now stops following the previous area when another is shown.
- **On a strict OpenGL ES driver the game look's water depth and foam could come out wrong.** A texture was read at low precision. The 3D view's shaders now read textures at high precision.
- **After lowering the 3D resolution and setting it back to Sharp, the smaller picture kept its graphics memory.** A picture the graphics card could not use also went unnoticed. The unused one is now deleted, and an incomplete one is logged and the view drawn at full resolution.
- **Objects scaled up (a rock at ten times its size) vanished from the 3D view far too close.** How far an object is drawn came from its model's size alone. It now accounts for the largest scale it is drawn at.
- **Moving the area with its arrows or going to a player outside it could lose a drawn path, or switch areas during a live apply.** They did not wait for unfinished work. They now refuse and say why, as resizing and Follow do.
- **After placing a mountain, the reason nothing grew on it was erased.** The status line always ended with "twice with what grew", hiding reasons such as no world generator or an area too large. The reason now stays, and the undo hint names one step when nothing grew.
- **Applying an inspector change could end in an error dialog instead of a message.** This happened when another area had been opened meanwhile or the object's world file could no longer be read. It now says so and changes nothing.
- **The inspector blamed the game for objects you had removed.** Applying a change to an object deleted, replaced or undone since opening always said it was removed in the game, even offline. Offline it now says what happened, and live it names both.
- **In the inspector, removing a container's items value could silently do nothing, and a value could be added twice.** The edited contents were written again on Apply and undid the removal, and Add accepted keys already there. The items value cannot be removed while its contents are editable (empty them instead), and an existing key is refused.
- **The inspector could write NaN or infinite numbers into an object.** Values like NaN, Infinity or a number too large for the game were accepted. They are now refused like any other value that does not fit.
- **The inspector's durability column said "%" and dropped fractions.** It edits the game's durability points, so touching it lost a fraction. It is now labelled as points and keeps fractions, and a stack has the same maximum (65535) everywhere.
- **Taking back Claude's changes did not say that Ctrl+Z brings them back one at a time.** Each change is taken out as its own step. The message now says so when there were several.
- **A save could be reported as failed though the world was written, and a failed history write left a ".new" file behind.** Some errors while keeping the history escaped the finished save. Every failure there is now logged instead, and the half-written file is deleted.
- **Ground tools could fail to say a point hit its limit.** Under No limit the ±400 m lift was never reported, and a smoothed point stopped by the ±8 m level limit went unnoticed. The check now compares where the point ended with the height asked for.
- **Reloading a live world from the map could end in an error dialog with the map not refreshed.** A refusal from the game's bridge was not handled there as it is in the 3D editor's Reload. It now says "Could not reach the game", and the busy veil always goes away.
- **Placing a dungeon could end in an error dialog.** This happened when the world's files could not be read or a dungeon had no rooms. It is now said in the status bar and nothing is placed.
- **Pasting a dungeon blueprint with nothing this world can make placed an empty entrance, and one holding only its portal got the wrong reason.** The check counted pieces before the unmakeable ones were left out. It now counts as placing does, and a portal-only blueprint is said to hold only its portal.
- **Help opens on ? (Shift) or F3, as it advertises.** Before, pressing / opened it instead.
- **A map close-up that failed to read was retried on every frame, with nothing logged.** This kept a processor core busy. It is now logged and not asked for again until the view, edits or map change.
- **Pieces deleted from the map could come back until the next refresh, and a failure to read them silently showed no buildings.** Two overlapping reads could finish in the wrong order. Only the latest is shown now, and a failure is logged.
- **The map's distance-from-the-centre filter silently ignored numbers it could not read.** "1,5" or "2 000" was taken as no limit and "NaN" made nothing match. Spaces between thousands and your computer's decimal mark are now read, and anything else is said in the zone filter's line.
- **The new-world page showed distances like "000 m" and "1000 m".** Distances were cut without rounding first. They are now rounded to 100 m, shown as "< 100 m" when that close and in km from 1.0 km.
- **Typing an invalid seed on the new-world page left the last seed's dungeon search running.** It kept a core busy and its marks were still moved about. The search now stops, the marks go and the dungeon list empties.
- **Leaving the new-world page during a seed search could end it with "The search failed".** The search's cancellation was disposed while the search still used it. It is now only disposed after the work ends.
- **A slope mask no longer lets steeper ground through on the area's border.** On the border the slope read about half its real value. Each direction is now divided by its own step.
- **Measure's lowest and highest heights now follow ground edits and include the two points.** They went stale after edits, and a point's height could lie outside them. They now count both points and refresh after an edit and when Measure is chosen again.
- **A double click on a Workshop button no longer opens the Workshop twice.** A second click while the game's files were being checked started a second open, which could ask the same questions and show the plot twice. A second open while one is under way is now refused with a message.
- **A blueprint opened from Homestead's folder by another spelling of its path is saved back without asking.** A trailing slash, a link to the folder or different letter case on Windows made the editor treat it as one from elsewhere. The folders are now compared in a consistent way.
- **A click with a small Place brush now places an object.** A 2 m brush at the default density rounded down to none, giving an empty preview and a click that placed nothing, while dragging did place. The layout now holds at least one.
- **Stacked layers can no longer take a line past 2,000 pieces.** The cap counted pieces along the line, not placements, so 20 layers on a long line could make up to 40,000. The cap now counts every placement.
- **A script that changes the ground from several threads (Parallel.For) no longer loses changes or fails.** Only Noise was safe from several threads. Set, Paint, Mountain, Remove, Place, Move and Print are now safe too.
- **A script setting the ground at a point that is not a number no longer edits the area's west or south edge.** Such a point turned into column or row 0. Points that are not finite numbers are now outside the area.
- **A performance log that cannot be written no longer breaks drawing every 2 seconds.** A refused write (a read-only data folder) escaped and the waiting lines piled up. Refused writes are now handled, the first is logged and the lines are dropped.
- **A --data folder that cannot be made now stops the start with a message instead of a crash.** Before, the editor crashed before its log started. It now reports the problem like any other unusable option and exits with code 2.
- **Saving a script under the name of another saved script now asks first, and one unreadable script no longer hides the others.** Save used to overwrite silently and in place (a crash mid-write lost the script), and a single unreadable script emptied the saved list. Overwriting a different script asks, the file is written safely, and an unreadable script is left out of the list (and logged).
- **The script API's Objects.OfKind now lists every kind of object.** Its note named only ten kinds, so Furniture, Remains, Creatures, SpawnPoints, Nests, Dungeons and Effects could not be found by script authors or Claude. All kinds are now listed.
- **A save that cannot be read back no longer leaves its changes waiting to be written again.** After zone resets or No limit ground, a failed re-read left edits pending, and the next save refused the session's own save as one the game made. Failing to remove earlier saves is now only logged, and a failed read starts the session over and says to open the world again.
- **Typing a place for a selection that holds a runestone now puts the other objects there.** The place was measured from the middle of everything selected, including runestones that never move, so objects landed off the typed values. It now uses only the moved objects.
- **The Noise brush's ragged edge is no longer cut straight where the noise dips lowest.** The brush can reach past 1.42 radii, but only points within that were looked at. It now looks 1.5 radii out.
- **A light first touch of paint no longer clears the generated dirt around it.** A never-painted point started from clear ground instead of the generated mask, so even a 1% soft edge left a hard ring. Brushes, Path, Area and pasted paint now start from the generated mask.
- **A live world that fails to open no longer leaves its connection open.** Each failed attempt left a connection behind. It is now closed when opening fails.
- **Leaving a live world while Apply live is still under way no longer loses the apply's outcome.** Leaving cleaned up something the apply still needed, causing an error. That is no longer cleaned up early.
- **A Discard that cannot read the world no longer makes the next save refuse the session's own save.** Discard forgot the session's saves before reading the world again. The world is now read first.
- **Opening the saved-worlds page no longer freezes on a slow folder or stops half set on a broken one.** The first look for worlds ran on the window's thread with no error handling. It now runs in the background like the later checks.
- **A crash while the loaded stamps are written can no longer lose them all.** The stamps file was written in place, and a file cut short reads as no stamps. It is now written whole through a temporary file, and a failure is logged.
- **Settings that cannot be saved are now reported, not shown as saved.** The Settings window used to close as if saved when the file could not be written (a read-only data folder). It now shows the reason and keeps the settings as they were.
- **Saving from the 3D view worded the changes differently from the map.** The prompt cut part of the status line's text in a way that would read wrongly once translated. Both prompts now describe the pending counts the same way.
- **The tool rail mixed languages.** Most brush names were translated while labels like Natural, Cultivate and Clear stayed English, and the start page kept its own English tip. The rail and the start page's titles now follow the language catalogue.
- **A PlanBuild export lost chest contents and sign texts, and a name with a line break broke its header.** The export now shares Homestead's rules, so an exported blueprint opens again with what its objects hold.
- **A power cut right after a save could leave an empty blueprint, preferences file or game cache.** These files were not flushed to disk before being moved into place. They now use the same durable write as the settings.
- **A game mesh with a short vertex buffer could stop all of the game's data from being read.** Two readers handled vertex data differently, one with no bounds check. Both now share one reader, and a vertex past the data is left at 0.
- **The Workshop's library showed a description differently from a paste of the same file.** The library trimmed every quote (a "Big" house lost its first quote) and matched header names in any case. Both now read headers with one rule.
- **A crash right after making a new world could leave a save the game trusts with files missing.** The new world's files were not flushed to disk. They now use the save's durable writer, with the marker file written last.
- **Searching blueprints found different ones in the Workshop's library and for Claude than in the Blueprints panel.** Only the panel looked at the creator. All three now use one rule (every word in the name, creator, description or tags).
- **Esc did not close message boxes.** Their only button, OK, was not the cancel button. It now is.
- **A typed path starting with another user's ~name (`~bob/worlds`) became `<home>bob/worlds`.** The world folder box, settings folders and SSH key path glued anything after a leading ~ onto the home folder. Only ~ alone or before a separator is now expanded.
- **Claude's Workshop prompt said the user saves the blueprint, while Claude's instructions describe save_blueprint.** The prompt now tells Claude to save with save_blueprint when you ask, and the server's summary says the Workshop's building is the one thing Claude can keep.
- **The start page's bridge address went back to 127.0.0.1:5182 at every start.** The last address connected to was saved but never read. The box now starts from it.

### Changed
- Faster, cleaner terrain shader loading: a graphics library is now loaded once instead of at every terrain shader build, which also stops it leaving unused leftovers.
- Dungeon dressing no longer makes extra random draws in eight places, so the same seed now makes a slightly different dungeon.
- A dungeon's throne is now chosen once per seat, so its turn can no longer be looked up for a different throne than the one placed.
- Dungeon wall windows are now placed the same way whatever your regional settings, so a seed makes the same windows everywhere.
- A copy at scale 1 no longer stores a redundant scale, as new blank objects do.
- The world generator's Step and Smooth no longer produce broken values when their two ends are the same (no caller does this today).
- The world generator's check of its dump file now treats unknown or short lines as unreadable instead of mishandling them or failing.
- The world generator now restores its random state even when something fails, so a failure no longer leaves it reseeded.
- The Claude server now cleans up when it cannot start (its port taken), instead of leaving a leftover at each failed Apply.
- Claude's select_objects and remove_objects now read the area's objects safely, as the other Claude tools do.
- The test driver's type, set and check commands now show their usage when the "|" between their two parts is missing.
- A shader that fails to compile or link no longer leaves leftovers on the graphics card.
- The 3D view's frame-rate measurement now waits for two frames, so 0 seconds no longer causes an error, and it refuses negative or invalid seconds.
- The Mask panel now shows every setting of a mask changed elsewhere (biomes, slopes, paint), so writing the boxes back can no longer undo it.
- Location markers and the View panel's location count now share one 40 m test, which no longer reaches 1 m too far east and north.
- The noise documentation now says it matches the web editor's to float precision, not exactly.
- A planned cave is now dug along the line it was planned on instead of a line worked out again. Nothing differs for users today.
- The Path tool now looks for points to change as far out as its natural look's ragged edge can reach.
- A seed map that was closed no longer keeps a screen-sized close look when it is moved or resized afterwards.
- Window screenshots taken with a dialog open now free the dimming layer behind it.
- Choosing a builder no longer re-reads every character file (several MB each); a file is read again only when it changed.
- Crunched textures are now checked once per load instead of twice, so they load faster.
- Building the ruins for a scene now looks through the world's objects once instead of twice.
- Cut-out textures' colour bleed now looks only at the edge it grows from instead of the whole picture 16 times.
- The game's logs are now read for the location lists' order only when they changed, which speeds things up.
- The editor's log now handles text written in pieces faster and no longer writes Windows line ends as separate lines.
- The start page asks every installed bridge at once, so silent ports no longer add 1.5 s each.
- A game's catalogue report is faster because it gathers the items the mod costs take once.
- Sorting objects into kinds is faster because each prefab name's kind is remembered until the game data changes.
- Regrow's biome lookup and the seed preview's landmass fill now use less memory.
- Placing dungeon stairs is faster because each side of a room is tried once instead of repeating the same search up to 40 times.
- Fitting a two-floor dungeon room is faster because it looks only at the tiles above its own cells.
- Dressing a dungeon room is faster because its walls and corners are worked out once.
- Building stability is faster and uses less memory because only nearby pieces are tested against each other.
- The pending counts in the save bar no longer copy every deleted and added object at each refresh.
- Hammer snapping is faster because the ghost's own snap points are worked out once per move.
- Saving new objects is faster because each one's data is read once instead of up to three times.
- Merging the game's live changes is faster because objects are compared without copying them first.
- Restoring objects from a backup is faster because each chunk file is read once instead of for every object.
- A save that reads the world again (No limit ground, zone resets) is faster because it reads the new save once instead of three times.
- Saving is faster because each new object's chunk file and the saved zones are looked up directly instead of by scanning lists.
- The world map's overview kept for reopening the same world (about 58 MB) is now released once no map uses it.
- Building a zone's base ground is faster because it skips paint-mask blending when no mask is asked for.
- Laying out locations that want the greenest spots is faster because it keeps a running best and average.
- Area actions other than Smooth and Naturalize no longer copy every height of the area first, and those two copy only what they need.
- Restoring objects from a backup is faster because each one is matched only against nearby objects of its prefab.
- A backup read for the Area tool's restore is now released once its world is left or another world is shown.
- Typing in the blueprint searches no longer re-reads every blueprint file and picture, and a blueprint without a picture is drawn once.
- Claude's area map no longer holds up the window while it reads the paint and sorts the objects.
- Claude's place_pieces is faster because it looks only at objects near the pieces it puts down.
- Shape formulas and hydraulic erosion now use less memory per step.
- Taking a step out of the history or marking a zone's ground for reset now redraws only the ground it changed.
- Going back or forward several steps in the history now refreshes the window and asks for a live apply once instead of once per step.
- The 3D view is faster because it looks up its shader settings once instead of at every frame.
- Editing with no mod extension that cuts the ground is faster because the cut grid is no longer scanned two or three times per change.
- Brush strokes now send only the changed part of the ground to the graphics card, which makes them smoother.
- Loading an area or growing it with Follow no longer wastes memory on a copy of the area's empty ground (about 35 MB on 17x17).
- Working out which zones a stroke touched is faster and uses less memory.
- Painting or shaping the ground no longer makes the 3D view look for the area's locations again at every frame of the stroke.
- The save bar and the live players' name labels no longer make new controls at every update.
- The world map no longer redraws its zone outlines and recomputes colours at every frame while nothing changes.
- The Measure panel now updates its values in place instead of rebuilding its rows at every mouse move.
- Ward and crafting-station ranges for the overlays are now made once per catalogue instead of on every overlay build.
- Hill and range mountains are faster because they no longer allocate memory for every ground point.
- Dragging a Place share slider or ticking kinds now saves the settings once after a short pause instead of on every tick.
- The smooth brush uses less memory, and brush frames that change nothing (locked or masked ground) no longer redraw the ground.
- The start page now loads blueprints and their pictures in the background, frees old ones, and looks for Homestead only when the game's state changes instead of every 2.5 s.
- Opening a world folder now writes the settings file once instead of twice.
- Drawing a selection zone is lighter, and releasing it checks only the objects inside the zone's bounding box.
- Opening Settings now reads the mods' extension files in the background so the window stays responsive.
- Internal code cleanup of blueprint rotations; no change for users.
- Copies rewritten by extensions now read and set object values the same way the save does, so a value the save accepts is no longer treated as missing.
- Internal code cleanup: the builder ("creator") data key is declared once; no change for users.
- Internal code cleanup of object data lookups; no change for users.
- Internal code cleanup of dungeon dressing for fallen pillars and statues; no change for users.
- Internal code cleanup of dungeon columns and posts; no change for users.
- Internal code cleanup of dungeon entrance locations and names; no change for users.
- Live bridge requests now share one sender, and every answer is released so its connection goes back to the pool at once.
- Internal code cleanup of the game scan's points and rotations; no change for users.
- Internal code cleanup of the game's compact count format; no change for users.
- Internal code cleanup of the ground height an edit shows; no change for users.
- Internal code cleanup of the save format's object flags and scale keys; no change for users.
- Reading a world's save numbers is more consistent; no change for users.
- Internal code cleanup of how a point's 64 m zone is found; no change for users.
- Building pieces are now packed for the view in one place, and each ruin's catalogue entry is looked up once.
- Internal code cleanup of the world's edge and the biome map's 12 m snap; no change for users.
- Internal code cleanup of the undo-list code behind ground edits, Shape and brush strokes; no change for users.
- Internal code cleanup of how the clipboard and Claude's copy_objects read a copy's source; no change for users.
- Internal code cleanup of the 3D view's coordinate conversions; no change for users.
- Internal code cleanup of how the hovered object's box is drawn; no change for users.
- Internal code cleanup of the 3D view's instance and line shader setup; no change for users.
- Internal code cleanup of how Place, Area, paste and lasso lines follow the ground; no change for users.
- Internal code cleanup of the camera's ground lookup and the sea and sky mesh upload; no change for users.
- Internal code cleanup of the question asked before saving into the world files; no change for users.
- Internal code cleanup of the file and folder pickers; no change for users.
- Internal code cleanup of the open area's world origin; no change for users.
- Internal code cleanup of the panels' floating cards; no change for users.
- Internal code cleanup of how tool panels build their rows, sliders and columns; no change for users.
- Biome names and the list of biomes the map filter and Mask offer now come from one place, so they always agree.
- Internal code cleanup of the Path and Place tools' shared curve and line handling; no change for users.
- Internal code cleanup of the start page's two Edit live buttons; no change for users.
- Internal code cleanup of the dungeon planner's comments and naming; no change for users.
- Dungeon entrance turns now always come back in one range, 0 up to 360.
- Internal code cleanup of a documentation summary for model bundles; no change for users.
- A history step can no longer be lost from the kept history when it is marked applied or removed.
- Internal code cleanup of a redundant condition in the dungeon planner's doorway choice; no change for users.
- Internal code cleanup of two unused assignments in the dungeon dresser's corridors; no change for users.
- The bundled noise library is trimmed to what the world generator uses (about 2,400 unused lines gone), with the same results.
- Internal code cleanup of unused world generator parts; no change for users.
- Internal code cleanup of unused fields in the 3D view; no change for users.
- Internal code cleanup of unused bounds and checks in the area loader; no change for users.

## v1.45.108 — 2026-10-17

### Changed
- **Saving new ground in many zones no longer re-reads the same chunk file for each one.** Each such zone used to read an existing object's whole chunk file (often several megabytes) just to copy a few bytes. Only that object is now read, once per save.

## v1.45.107 — 2026-10-17

### Changed
- **A long live session no longer keeps every past read of the game in memory.** Moving creatures and burning fires change at every check, so memory used to grow all session. Old reads are now released once nothing uses them.

## v1.45.106 — 2026-10-17

### Changed
- **Generating a deep dungeon is faster.** Adding loops between rooms used to repeat a huge amount of searching, which grew badly with many levels. The work is now done once per try, and the dungeons made are the same.

## v1.45.105 — 2026-10-17

### Changed
- **Apply live after a big clear no longer reads every removed object first.** Clearing 20,000 trees used to mean 20,000 pointless reads that held up the live view. An object's data is now read only when it may move, such as when moved back or copied somewhere.

## v1.45.104 — 2026-10-17

### Changed
- **Saving and Apply live no longer copy every edited zone to find the few they need.** With 2,000 edited zones this meant hundreds of megabytes of copying each time, even with nothing lifted. Only the zones needed are copied now, once, and the map's edited-zone marks copy none.

## v1.45.103 — 2026-10-17

### Changed
- **Following a live world no longer slows down after a big Apply live.** After a 3,000-piece paste, every check of the game cost millions of comparisons in the editor's window. Objects the editor made are now found by their game id at once.

## v1.45.102 — 2026-10-17

### Changed
- **Opening an area takes about half as long to work out the generated ground.** Each zone's heights were worked out twice, the second time only to get the base paint. The paint is now kept from the first pass.

## v1.45.101 — 2026-10-17

### Changed
- **Dragging an Area selection no longer stutters on a big area.** Every mouse move used to recount objects, rebuild lists and work out cut and fill, even for actions that do not show it. The outline still follows the mouse at once, while counts and lists update once the waiting moves are handled and cut and fill only for Flatten, Raise and Lower.

## v1.45.100 — 2026-10-17

### Changed
- **Regrowing nature in the Ashlands reads each zone once.** Neighbouring zones were read again for every zone next to them, about three times the work over a 4 × 4 block. The reading is now shared for the whole area, and the same spots are grown.

## v1.45.99 — 2026-10-17

### Changed
- **Apply live no longer waits for the history file to be written.** After each Apply live (with Auto apply, after every edit) the editor used to freeze while writing the whole history, longer as it grew. The history is now written in the background, and opening the world again or closing the editor waits for any writes still to do.

## v1.45.98 — 2026-10-17

### Changed
- **Regrowing nature over a big area no longer stalls the editor.** Each growth spot was checked against every object of the area, so a Mountain with Grow over 49 zones could stall for seconds. Only nearby objects are now checked, in the background, and the same spots are grown (Area Regrow, Mountain Grow and Claude's regrow_nature).

## v1.45.97 — 2026-10-17

### Changed
- **Picking a big saved selection no longer freezes the editor.** Each object was compared with every object of the area, which meant billions of comparisons on large areas. Objects are now sorted once, and the same objects are selected.

## v1.45.96 — 2026-10-17

### Changed
- **Long paths apply without freezing the editor.** A 1.5 km diagonal path in a 17 × 17 area used to make about 1.5 billion tests and freeze the editor for seconds. Each metre of the line now looks only at nearby points with the same result, and a Cave likewise looks only near each tree and rock.

## v1.45.95 — 2026-10-17

### Changed
- **Editing with the map zoomed in no longer piles up memory.** The map kept up to seven large close-ups, using up to about 400 MB for pictures never shown again. It now keeps only the last two close-ups of the current edits.

## v1.45.94 — 2026-10-17

### Changed
- **The map stays light with many search results.** Even with the map idle, every frame rebuilt all the pin shapes, which with 2,000 results meant about 100,000 small allocations a frame. The shapes are now rebuilt only when the results, chosen pin, zoom or players change.

## v1.45.93 — 2026-10-17

### Changed
- **Switching between the map and the 3D editor no longer stalls the map's first frame.** The map used to decode the game's minimap textures again each time it came back. They are now decoded once (again only when another Valheim folder is chosen).

## v1.45.92 — 2026-10-17

### Changed
- **Opening another area no longer reads the game's terrain look again.** Each area shown re-read tens of MB of terrain shader and textures from Valheim's files, and the ground showed plain colours meanwhile. They are now read once, and again only when another Valheim folder is chosen.

## v1.45.91 — 2026-10-17

### Changed
- **Areas with many dungeon rooms or location parts draw faster.** Every frame used to recompute and redraw all of them, in view or not. They are now prepared once and only those in view are drawn, like objects.

## v1.45.90 — 2026-10-17

### Changed
- **Showing spawn points or nests in a big area no longer costs every frame.** Each frame used to search every object of the area for them. They are now found once and searched for again only when the objects change.

## v1.45.89 — 2026-10-17

### Changed
- **Moving the mouse in the Select tool over a big area no longer bogs the window down.** The object under the pointer was looked up on every mouse move, and a fast mouse sends many per frame. It is now looked up once for all the moves waiting.

## v1.45.88 — 2026-10-17

### Changed
- **Moving or dropping a big selection among many objects is much faster.** Dragging and dropping used to compare every snap point and object against everything around. They now look only at what is near, and what snaps and where things land are unchanged.

## v1.45.87 — 2026-10-17

### Changed
- **Finding where locations go (new worlds, seed search) is faster.** A flaw in how the generator cached zone corners west of the world's middle made half the lookups slow. The cache now handles both coordinates properly.

## v1.45.86 — 2026-10-17

### Fixed
- **Connecting to a server whose plugin did not answer could freeze the window for up to 15 seconds.** The check of the server's Valheim folder, which explains why, ran on the window's thread. It and the reading of the plugin's port from the server's settings now run in the background.

## v1.45.85 — 2026-10-17

### Fixed
- **Pasting a hand-edited or older blueprint could stop half-way and leave No limit on.** A blueprint with inconsistent ground data could leave the ground partly changed with no history step, and every later brush then ignored the limit. Such blueprints are now refused with the reason, and No limit always switches back off, even when a tool fails.

## v1.45.84 — 2026-10-17

### Fixed
- **Saving or applying ground lifted past the ±8 m limit in two far-apart places could run out of memory.** Two lifts at opposite ends of the world made the editor work on a huge area in between. Each lift is now handled on its own small area.

## v1.45.83 — 2026-10-17

### Fixed
- **The Place tool could lose its presets, favourites and weights, or fail to open.** A save cut short or a hand-edited settings file could break it or wipe it on the next save. The file is now saved safely, a broken one is copied aside first, and an unreadable one is never written over.

## v1.45.82 — 2026-10-17

### Fixed
- **A crash while saving selections could lose every world's saved selections.** A save cut short could leave the file broken, and the next Keep or Forget would then overwrite it. Saving is now safe, a broken file is copied aside first, and an unreadable one is never overwritten (Keep and Forget tell you they could not save).

## v1.45.81 — 2026-10-17

### Fixed
- **A script could put objects at impossible places into the world.** Scripts that placed or moved objects with invalid numbers, a negative size or a spot outside the open area used to put them in the world anyway. They now stop with an error saying what was wrong, and the boulder and ring-of-trees examples keep their objects inside the area.

## v1.45.80 — 2026-10-17

### Fixed
- **Rooms of a generated dungeon with alcoves had their furnishings off to one side.** Paired pieces such as colonnades and braziers ended up off-centre, sometimes in the wall or missing. Furnishings now follow the room's own walls, and an alcove counts as extra floor beyond them.

## v1.45.79 — 2026-10-17

### Fixed
- **The warning sign by a generated dungeon's locked boss gate was sunk in the wall.** It sat about half a metre deep in the stone. It now hangs on the antechamber side of the wall.

## v1.45.78 — 2026-10-17

### Fixed
- **The steps before the plinth of a generated burial chamber, chapel, shrine or tomb rose away from it.** The stairs faced the wrong way, unlike those in the great hall, arena and feast hall. They are now turned the same way as the others.

## v1.45.77 — 2026-10-17

### Fixed
- **A generated cell block's elite guards could stand locked inside the cells.** Guards and their spawners could be placed behind the grates. Both rows of cells are now kept clear, up to their grates.

## v1.45.76 — 2026-10-17

### Fixed
- **A generated dungeon could hold two kitchens, two forges or two feast halls.** A mirrored side room could duplicate a room that should only appear once in a fortress, prison or Dvergr hold. Such rooms are no longer mirrored.

## v1.45.75 — 2026-10-17

### Fixed
- **A generated dungeon could hold extra antechambers that led nowhere.** When the boss arena did not fit, the antechamber stayed behind as a dead end, sometimes several times over. The antechamber and its corridor are now removed whenever the arena does not fit beyond it.

## v1.45.74 — 2026-10-17

### Fixed
- **A generated dungeon's hidden cache could sit in the upper half of a tall room on the level below.** Tall rooms such as the great hall, chapel, throne room and arena could have a cache placed in their upper 4 m. Nothing is placed over a tall room any more.

## v1.45.73 — 2026-10-17

### Fixed
- **A kind of object whose model failed to load was never drawn at all, and no other model got its textures.** Its objects were neither drawn nor pickable, and other models using the same textures came up without them. The failure is still reported, but its objects now show as a stand-in box and the textures are freed for other models.

## v1.45.72 — 2026-10-17

### Fixed
- **In the Workshop, a wall crossing the cut height was picked and hovered instead of what stands inside.** A wall's hidden top could take the click meant for the chest behind it. Picking now only meets the part of an object below the cut, as drawn.

## v1.45.71 — 2026-10-17

### Fixed
- **Pictures of the 3D view and the map came out part-transparent under water, glass, ghosts and overlays.** Screenshots, the docs' pictures and Claude's screenshots were see-through in those places. Pictures are now always fully opaque.

## v1.45.70 — 2026-10-17

### Fixed
- **Typing in a text box moved the camera or changed how it moves, and so did keys on the start page and the map.** For example, typing F in a search box switched camera mode, and W, A, S or D moved the view. The 3D view now takes keys only when the 3D editor is shown and you are not typing in a box.

## v1.45.69 — 2026-10-17

### Fixed
- **A chest filled through the editor could hold a stack larger than the game allows.** The game loses the extra items, so oversized stacks were silently wasted. Stacks are now cut to each item's maximum, with a note in the message such as "Stone: stack of 70000 cut to 50 (the item's maximum)".

## v1.45.68 — 2026-10-17

### Fixed
- **Claude could run scripts on your computer without your say.** Scripts from Claude now need your permission under **Settings → Claude → Claude may run scripts**, which is off by default. While it is off, Claude's script running (dry runs too) is refused with a message saying where to allow it, your own scripts work as before, and scripts still run with your rights.

## v1.45.67 — 2026-10-17

### Fixed
- **Script: Stop could not end a script busy in one long call.** A single long operation could keep running until the editor closed, using up a CPU core. Scripts now run in a separate process that Stop ends if needed, and a script that crashes is reported as an error (it is still not a sandbox: scripts run with your rights).

## v1.45.66 — 2026-10-17

### Fixed
- **Claude: a step made through a control Claude pressed was not named "Claude: …".** The history and **Take back Claude's changes** could not tell it from your own work. Steps made while Claude sets a control now carry the "Claude: " prefix like Claude's other steps.

## v1.45.65 — 2026-10-17

### Fixed
- **New world names are checked the same way on every system.** Before, "My:World?" worked on Linux and macOS but broke on Windows, and names like CON or COM1 failed there; the same rules now apply everywhere.

## v1.45.64 — 2026-10-17

### Fixed
- **A blueprint file with terrain marks far apart, or with numbers that are not numbers, froze or failed the import.** Pieces far apart could make the editor freeze or run out of memory. Unreadable numbers are now skipped, and terrain marks spanning more than 1200 m are refused with the reason, on import and on paste.

## v1.45.63 — 2026-10-17

### Fixed
- **A settings file held by another program for a moment was written over with the defaults.** If a sync or antivirus program briefly held the file, your game folder, extra folders, allowed extensions and Claude token could be lost. A failed read is now retried, and if it still fails the defaults are used for that session only and never saved over your file.

## v1.45.62 — 2026-10-17

### Fixed
- **One unusual bundle (a mod's, damaged, or laid out otherwise) left every mod model undrawn.** A single odd bundle could stop the whole startup index of mod models. Such a bundle is now skipped and noted in the log, and the rest are indexed.

## v1.45.61 — 2026-10-17

### Fixed
- **The editor could fail to start, or miss pieces and models, depending on where the game's scripts are in its bundles.** A bundle could be closed too early while still being used, so later lookups failed. Bundles now stay open until their last reader is done, and a leaked file handle is fixed too.

## v1.45.60 — 2026-10-17

### Fixed
- **On Windows, a mod updated while the editor ran left its models missing until the editor was restarted.** The old copies of the mod's files were still held open, so Windows refused to replace them. They are now released before the copies are replaced.

## v1.45.59 — 2026-10-17

### Fixed
- **One damaged object ended a world search, and a damaged item stand broke the inspector.** Damaged saves could cause an error that stopped the whole search or the inspector. Unreadable objects are now skipped and the rest carry on.

## v1.45.58 — 2026-10-17

### Fixed
- **A chest could be given something that is not an item, which the game then threw away.** Entries such as "Troll" or "piece_chest" were accepted and saved, then dropped by the game when it loaded the chest. Only real game items are accepted now, and anything else is refused with its name.

## v1.45.57 — 2026-10-17

### Fixed
- **Exporting To Homestead, importing a blueprint or generating a dungeon with a long name could hang the editor.** For very long names the search for a free file name never ended. The name is now shortened so its number always fits.

## v1.45.56 — 2026-10-17

### Fixed
- **Claude's "click" on a switch, toggle or radio button changed nothing.** The control kept its value while Claude was told it worked. A click now presses the control like a real pointer does, so the toggle changes and everything tied to it runs.

## v1.45.55 — 2026-10-17

### Fixed
- **Claude Desktop lost the editor's MCP server on a batch request or a tool that ran too long.** Claude Desktop then showed the server as disconnected. Such requests now get an error reply and the connection stays up.

## v1.45.54 — 2026-10-17

### Fixed
- **Claude's object ids could hit the wrong objects after another area was opened or the world read again.** Claude's tools could then remove or change whatever objects now had those ids. The tools now refuse ids from another area or from before the world was read again, until objects are listed again.

## v1.45.53 — 2026-10-17

### Fixed
- **Claude could not open or add a blueprint whose file was not named after it.** Numbered copies, downloaded blueprints, or ones the game saved under another file name were reported as "not in the library". Blueprints are now found by their name first, then by file name.

## v1.45.52 — 2026-10-17

### Fixed
- **A dungeon Claude generated was not one of Claude's changes, and left a file in the blueprint Library.** **Take back Claude's changes** kept it, and on the Workshop plot it wrote a blueprint into your Library. The step is now named "Claude: generated dungeon …", and on the plot nothing is written to the Library.

## v1.45.51 — 2026-10-17

### Fixed
- **A blueprint Claude refused to add, because a piece would leave the area, took the place of what you could redo.** The refused attempt cleared your redo list and could leave the blueprint ready for the next Ctrl+Y. Refused blueprints are now checked first, so nothing is put down and your history is untouched.

## v1.45.50 — 2026-10-17

### Fixed
- **A piece dragged far did not snap to the building where it was dropped.** A piece dragged more than about 60 m to another building never snapped to it. Snap points are now refreshed once the selection has moved more than 30 m.

## v1.45.49 — 2026-10-17

### Fixed
- **"Move there" moved the selection a little on every axis, even the ones left alone.** Rounded values in the X, Y, Z and Turn boxes made every selected object shift slightly, creating a new undo step and a destroy and create on a live server. It now moves only along the boxes you changed, and does nothing if you changed none.

## v1.45.48 — 2026-10-17

### Fixed
- **Replacing a ship or another linked group's root left its parts behind.** The leftover parts had nothing to attach to in the save. Replace now removes a root's parts with it in the same undo step and says how many went, while a part replaced on its own still goes alone.

## v1.45.47 — 2026-10-17

### Fixed
- **A heightmap read but not yet put could land in another area.** After changing the selection or opening another area, "Put it into the ground" wrote it into the wrong ground. The picture is now dropped when the selection changes, and refused with a message when another area is open.

## v1.45.46 — 2026-10-17

### Fixed
- **Replace's "with" list kept the first world's kinds after another world was opened.** It offered modded kinds the new world cannot copy and lacked the new world's own. The list is now refreshed for another world or when the game's data or mods change, keeping your chosen kind when it is still available.

## v1.45.45 — 2026-10-17

### Fixed
- **Reset zones could say "Select an area first" or miss zones, depending on the Soft slider.** With a high Soft value and a narrow selection, no zone was found, and zones at the soft edge were left out. Zones are now those with a grid point inside the outline, whatever Soft is.

## v1.45.44 — 2026-10-17

### Fixed
- **Removing a move from the history could leave two copies of the object.** The same could happen when removing a Place whose object was moved later. Such a change is now refused with a message ("undo back to it instead"), and "Take back Claude's changes" says how many were left for that reason.

## v1.45.43 — 2026-10-17

### Fixed
- **A hand-made language file could stop the editor from starting.** A file with a badly shaped top level or a repeated key caused an error at start-up, even for a language you never chose. A repeated key now takes the last text, and a malformed file is logged and skipped.

## v1.45.42 — 2026-10-17

### Fixed
- **A translation of "Free" or "Cost unknown" with a brace in it broke the Build panel.** It also broke the blueprint list and Claude's piece search. These texts are now shown exactly as written.

## v1.45.41 — 2026-10-17

### Fixed
- **Mirrored pastes tilted pieces the wrong way.** A beam sloping nose-down came out nose-up, and tilted trees leaned the other way. Mirroring now keeps the tilt, including for mirrored dungeon pastes and the paste ghost.

## v1.45.40 — 2026-10-17

### Fixed
- **A mod extension offered by two mods ran everything twice.** Its shapes and ground cuts were drawn twice and its text and rewrites ran twice, and a failure turned off only one of the two in Settings. Each extension now runs once, and a failure shows on every mod offering it.

## v1.45.39 — 2026-10-17

### Fixed
- **The start page could say "Reading the game's files" for ever, or "ready" when it was not.** This happened when one kind of the game's data could not be read, for example after a game update, and nothing was logged. The editor now logs what failed, retries that data next time, and the start page says some data could not be read.

## v1.45.38 — 2026-10-17

### Fixed
- **A cache that could not be written stopped every world from opening.** A full disk, a read-only data folder or two editors writing at once made the editor throw away what it had just read, and it re-read the whole game on every try. A cache that cannot be saved is now only logged and the data is used anyway.

## v1.45.37 — 2026-10-17

### Fixed
- **A world map that failed to build said "Building the world map…" for ever.** A damaged world file could leave the map blank with no explanation. The map now says it could not be built and why, and the log has the details.

## v1.45.36 — 2026-10-17

### Fixed
- **A live world's map could show another world's players.** A slow answer from an old world could put its players on the new map, and a hung game kept piling up requests. The map now asks again only after the last answer and ignores answers for a world no longer shown.

## v1.45.35 — 2026-10-17

### Fixed
- **The map could pin the results of an older search.** An older or slower search could replace newer results, or pin hits from another world or from before a reload or save. Only the latest search on the world as shown is pinned now, and a dropped one tells you to search again.

## v1.45.34 — 2026-10-17

### Fixed
- **Claude's screenshots could show the wrong view.** When Claude asked for several screenshots at once, one could move the camera before another's picture was taken, so pictures could be from the wrong camera or labelled with the wrong sides. Camera and screenshot actions now take turns.

## v1.45.33 — 2026-10-17

### Fixed
- **A dungeon Claude generated could land in the wrong place, or go live.** Opening another area or the Workshop while it generated could place the dungeon there, closing the area crashed the tool, and turning Auto on sent it straight to the game. It is now checked again before placing, and if something changed nothing is placed and Claude is told why.
- **A dungeon added to the Workshop's plot could land on another plot.** If you opened a different plot while the dungeon was being made, it went there. It is now added only to the plot it was made for; otherwise nothing is placed and the status bar says so (the blueprint stays in the Library).

## v1.45.32 — 2026-10-17

### Fixed
- **Errors on the map went unseen.** A failed area load, Discard or Reload from the map, or the advice after making a new world, only showed in the 3D editor's status bar, which the map does not display. These now also appear in a dialog on the map, and the editor's message dialogs are titled "Valheim World Editor" instead of "Save".

## v1.45.31 — 2026-10-17

### Fixed
- **After a failed Discard or Reload in the 3D editor, every tool silently did nothing.** The editor stayed on an area that was no longer open, tools, Save and Undo did nothing, and a misleading "Discarded" message covered the error. The map now opens instead with a dialog saying the area could not be opened again and why.

## v1.45.30 — 2026-10-17

### Fixed
- **A change finished after going to the map could not be undone.** A script, a Claude change or Grow still running when you went to the map changed the world but lost its undo step. Going to the map or opening another area now closes the area, so such work changes nothing and says so (a script reports "The area was closed while the script ran").

## v1.45.29 — 2026-10-17

### Fixed
- **Trees grown on a mountain could float, be buried or be lost.** If you undid or drew another stroke while the mountain's trees were still being worked out, they could land on ground that was gone; now nothing grows when the area changes in the meantime, and the message tells you why.

## v1.45.28 — 2026-10-17

### Fixed
- **Keys still worked while a world was being saved from the 3D editor.** Pressing Ctrl+S, Ctrl+Z or Follow during a save could start a second save or freeze the editor; now a "Saving…" veil appears and those keys wait until the save is done.

## v1.45.27 — 2026-10-17

### Fixed
- **Pressing Enter again during an Area action could do it twice.** Reset zones, Regrow nature and Restore from a backup could be started a second time while they were still working, adding trees or objects twice; Apply is now greyed out while an action runs, and an action whose area changed meanwhile applies nothing and says so.

## v1.45.26 — 2026-10-17

### Fixed
- **Choosing an SSH key in a format the editor cannot read could crash it.** An old, damaged or cut-short key file now gives the message "The key file could not be read" (or is skipped if it sits in your .ssh folder) instead of an unexpected error.

## v1.45.25 — 2026-10-17

### Fixed
- **Clicking Connect twice could open two connections to the server.** A second click while connecting now does nothing, and quitting or going back to Home mid-connection closes the connection as soon as it is made instead of leaving it open until the editor exits.

## v1.45.24 — 2026-10-17

### Fixed
- **The dungeon list could fail on one damaged dungeon, and gave wrong room counts in older worlds.** Each dungeon is now read in its own world version, so room counts are right in older worlds, and a dungeon that cannot be read is listed with no rooms and a line in the log saying why.

## v1.45.23 — 2026-10-17

### Fixed
- **One damaged ground record made the whole world fail to open.** The editor now keeps the damaged object as it is, notes where it is in the log, and opens the rest of the world, as the game itself would.

## v1.45.22 — 2026-10-17

### Fixed
- **Live, an object the game listed twice could be counted twice or leave a ghost copy behind.** Each object is now taken once, so builders' piece counts are right and objects disappear properly when removed in the game.

## v1.45.21 — 2026-10-17

### Fixed
- **Saving could stop with an error instead of saying why it could not save.** A save that cannot go ahead now reports the reason and writes nothing, and new ground copies the first terrain object the editor can use.

## v1.45.20 — 2026-10-17

### Fixed
- **A power cut just after saving could leave a broken save that the game trusts.** The world's data and metadata files are now fully written to disk before the save is marked complete and the previous save is removed, and on Linux and macOS the save folder is flushed too.

## v1.45.19 — 2026-10-17

### Fixed
- **An Apply live that failed partway said nothing had been applied.** The editor now tells you what went into the game and what failed, and marks the finished steps as applied so Discard does not undo changes the game kept; Apply live again sends the rest.

## v1.45.18 — 2026-10-17

### Fixed
- **A save or Apply live that failed still turned No limit ground into ground discs and cleared the undo history.** If nothing was written or sent, your No limit ground is now put back as it was and your area and history stay.

## v1.45.17 — 2026-10-17

### Fixed
- **Objects you had just applied live could vanish from the editor as if players had removed them.** The editor now ignores a game update that started before or during an Apply live and reads the zone again, so your objects stay.

## v1.45.16 — 2026-10-17

### Fixed
- **A dungeon placed on a live world could be built from the wrong object's data.** Placing a dungeon now copies each object from where its data really is, so it comes out right or no longer fails.

## v1.45.15 — 2026-10-17

### Fixed
- **Other users of the computer could read Claude's token.** The settings file holding the token used when **Allow Claude to connect** is on is now readable only by you on Linux and macOS, like the saved servers file.

## v1.45.14 — 2026-10-17

### Fixed
- **In another language, Claude could use the Blueprints buttons that write or delete files.** The Blueprints buttons (**Details**, **.vbuild**, **To Homestead**, **Delete**) and the Select tool's **Delete** are now blocked for Claude in every language.

## v1.45.13 — 2026-10-17

### Fixed
- **Editing a saved server could send another server's password and token to it.** **edit** now fills the whole form from the saved server, including its own password and token, and ticks **Save password** when one was saved.

## v1.45.12 — 2026-10-17

### Changed
- **The Area tool's object actions no longer stall on a big area with the Mask on.** Remove, Select, Restore from backup and Regrow nature are much faster on large areas with thousands of objects, and pick the same objects as before.

## v1.45.11 — 2026-10-17

### Fixed
- **Restore from backup with the Mask on doubled objects.** Objects where the Mask lets nothing through are now left as they are instead of being brought back on top of themselves.

## v1.45.10 — 2026-10-17

### Fixed
- **The map's zone filter could judge one world's zones on another world's counts.** If you open another world (or reload the same one) while counts are loading, **Show matching** and **Mark for reset…** no longer use the old world's numbers or mark its zones.

## v1.45.9 — 2026-10-17

### Fixed
- **A brush stroke made while Apply live was answering could lose ground or undo wrongly.** With **Auto** on, a stroke begun while the game was still answering now keeps its ground and undoes correctly, or is dropped with a message asking you to draw it again.

## v1.45.8 — 2026-10-17

### Fixed
- **Some script calls never ended, even with Stop.** A step of zero, a negative step or a tiny step in `Area.Points`, and a huge radius in `Ground.Shape` or `Ground.Mountain`, could run for ever; such a step is now an error, and Stop works at every point.

## v1.45.7 — 2026-10-17

### Fixed
- **A script with an endless loop of its own could not be stopped.** **Stop** and the 2-minute limit now end a script stuck in its own loop, so you no longer have to restart the editor before running another script.

## v1.45.6 — 2026-10-17

### Fixed
- **Claude's build_floor, build_walls and build_roof could freeze the editor.** The rectangle must now be inside the open area, and a layout of more than 5000 pieces is refused up front with a message to build it in parts.

## v1.45.5 — 2026-10-17

### Fixed
- **Moving objects past the area's edge sank them about 1000 m, or threw others up.** With **Put on ground** on, objects moved past the edge of the open area now keep their own height, and only the ground inside the area places the rest.

## v1.45.4 — 2026-10-17

### Fixed
- **Claude could change a live world with Auto on through the editor's controls.** While a world is live with **Auto** on, Claude can now only change the View panel's controls, so its changes cannot go to the game without your say.

## v1.45.3 — 2026-10-17

### Fixed
- **Applying live again after a lost answer could make every new object twice.** If the game placed your objects but its answer never arrived, the next **Apply live** now resends them in a way the plugin recognises and does not duplicate; this needs the plugin from this version, and an older one works as before.

### WorldEditorBridge
- Repeated object calls are now recognised and answered with what was done the first time instead of being done twice. A call whose answer was lost is not resent to a game that has restarted since, so the apply fails until the world is reloaded.

## v1.45.2 — 2026-10-17

### Fixed
- **Live zone resets were sent again after the world failed to load back.** Zone resets are now cleared as soon as the game has done them, so a failed read-back no longer causes the next apply to wipe what players built since.

## v1.45.1 — 2026-10-17

### Fixed
- **Undoing a second Reset zones on the same zones kept the new options.** Undo, redo and removing the step in History now restore each zone's earlier reset options correctly, so a save cannot wipe buildings the first marking kept.

## v1.45.0 — 2026-10-17

### Added
- **The editor can be translated.** Drop a language file (for example `fr.json` or `pt-BR.json`) in the `lang` folder of the data folder and pick it in **Settings › Language**, which defaults to your computer's language when a file exists; English shows wherever a text is missing. Hover tips and Settings are translatable so far, with the other texts to follow, and [docs/translating.md](docs/translating.md) explains how.

## v1.44.0 — 2026-10-15

### Added
- **A Mac build (experimental), for Apple silicon** (macOS 12 or later). It is on the releases page as a zip, signed ad hoc (macOS asks once, **Open Anyway** in Privacy & Security), and finds Valheim, your worlds, logs and r2modman profiles in their Mac locations, with **Cmd** working as **Ctrl**; it is not tried on a Mac yet, and Intel Macs are not supported.

## v1.43.0 — 2026-10-14

### Changed
- **Generated dungeons read more like buildings.** Side rooms cluster with related rooms (a kitchen by the feast hall, cells by the guard room), light follows a room's purpose, deeper levels hold a fifth more loot and foes than the top one, corridor niches sometimes hide a foe, and dvergr doorways get the dvergr arch.

## v1.42.0 — 2026-10-14

### Changed
- **Generated dungeons vary far more.** Rooms used to be plain boxes entered in the middle of a wall, each kind furnished one way, with torches along every wall; now they differ in several ways.
  - **Room shapes.** Rooms can have notched corners, an apse at the end, a cut-away corner or a narrowed door, and walls, beams, furnishing and the map follow the shape.
  - **Bent corridors.** A corridor may turn and enter its room from the side, often in catacombs, warrens, holds and ruins, and some rooms are entered on their long side.
  - **Two to four furnishings per kind of room.** The dungeon's seed picks one, such as a mead hall, a hall of the dead, a chapel with a raised altar, a tomb on a dais, a scriptorium or a war room, and new props include cobwebs, chains, bedrolls, weapon stands, training dummies, dolmens, candles, a cart, a bath and a round table.
  - **Lighting styles.** Each room uses standing torches, wall sconces or corner braziers, and corridors likewise.
  - **Floors.** In stone or grausten dungeons, half the time grand rooms get black marble tiles and lived-in rooms wooden boards (Furniture at Furnished or Rich), with stone pieces standing properly on the marble.
  - **Built up inside.** Great halls, throne rooms and chapels may have a loft over the entrance, arenas and shrines may be sunk below ledges, burial chambers may hold a vault, and alcoves and niches are furnished.
  - **Clusters.** Besides its main feature, a room gets one or two groups of things that belong together, such as a camp, a meal, a scribe's desk, stores, an armoury, an altar, bones or a workbench.
  - **Hero pieces.** Big rooms may get one special piece that appears once in the whole dungeon, such as a dolmen, a fallen giant's statue, a grave slab, a giant's skull or ribs, an altar of crystal or a cart.
  - **Corridor kinds.** Corridors may have a gate, a half collapse, bones along the walls, a runner, vines or sharpened stakes, and some dead ends lead to a cache with the biome's chest, loot and sometimes a guard, while catacombs and prisons keep some corridors dark.
  - **Pacing.** A few rooms stand nearly bare, clutter never repeats the same thing twice running, and rooms differ in how tidy they are.
  - **The game's own pieces.** Dungeons now use each biome's own growths, lights, chests, crafting stations and special pieces, plus biome corridor kinds and Ashlands windows and gates, and props that floated at the wrong height now stand properly on the ground.
  - **Light and Ruin.** Turning Light or Ruin changes only the lights and decay and nothing else about a dungeon.

## v1.41.0 — 2026-10-14

### Added
- **A placed dungeon gets the entrance of its kind on the ground.** **Place in the world** now puts the game's own entrance for the biome at the middle of the view and the dungeon 5000 m above it, with doors leading up and back out as in the game; where the biome has no dungeon of its own (the Plains) the portal pair stays, and only one dungeon fits over a zone, with a message if one is already there.
- **Go to the dungeon and Go to the entrance.** The view now stays at the entrance when a dungeon is placed, and two buttons under Place in the world take you up into the dungeon and back down. The Dungeon panel's **Go to entrance** does the same for any dungeon of the area.
- **Up to 16 levels.** Dungeons used to go up to 4 levels, and the stairs now go down as far as 16.
- **Dungeon blueprints.** Switch **A dungeon** on when saving a blueprint in the Workshop (or in the Blueprints tab's Details), and when pasted it goes 5000 m up behind the biome's entrance where you click. A portal in the building marks where players arrive, and generated dungeons opened in the Workshop already carry it.
- **Claude's `generate_dungeon`** places the entrance the same way and now takes up to 16 levels.

### Changed
- Internal data scanning now reads each location's teleport doors and interior anchor. Internal code cleanup; no change for users.

## v1.40.0 — 2026-10-14

### Added
- **Moved objects stay the same objects in live mode.** An object you move or turn in the editor (with the Select tool's move, or Cut then paste) now moves in the running game under its own id, so mods that track objects, such as a chest's label sign, keep working and a chest keeps its contents. If a player has both the old and new place loaded the object is made again as before, and the message after Apply live says how many; see [Moving things](docs/live-mode.md#moving-things).

### WorldEditorBridge
- The plugin now accepts objects to move, and moves each under its own id or remakes it where a player has both places loaded. Mods' listeners are told where each object stood and stands now, and editors only send moves to a plugin that supports it.

## v1.38.0 — 2026-10-14

### Added
- **Cut and paste.** **Cut (Ctrl+X)** copies like Ctrl+C and then removes the copied objects as one undo step, so a paste moves a build instead of doubling it, and the ground where it stood stays as it is. See [Copy and paste](docs/area.md#copy-and-paste) and [Select](docs/select.md).

## v1.37.4 — 2026-10-14

### Fixed
- **Spikes and an orange wedge in the 3D view after a visit to the map.** Coming back from the map no longer tears spawn-point and nest markers, the Dungeon tool's marks or the Mountain tool's preview into spikes or draws an orange wedge across the view.

## v1.37.3 — 2026-10-14

### Changed
- **A click finds an object by its drawn shape.** You can now click a chest, a stone or anything under a tree's branches, because clicks follow the object's real model, and leaves and bushes count only where nothing solid is behind them.
- **A standing tree is outlined by its trunk.** The selection outline of a standing tree is now a column round its trunk instead of a box round its whole crown, while logs and stumps keep their box.

## v1.37.2 — 2026-10-14

### Changed
- **A large selection is marked by one box.** Past 500 selected objects the 3D view draws one orange box round all of them instead of one each, so a huge selected structure no longer turns into a solid orange blob.

## v1.37.1 — 2026-10-14

### Changed
- **The green dot on each unsaved object is much smaller.** It was covering small objects, and is now about half the size with a soft edge so it stays round.

## v1.37.0 — 2026-10-14

### Added
- **No limit under the Area tool's Flatten, Raise and Lower.** The **No limit: past the game's ±8 m** switch now shows with those three actions too and follows the one in the brush options, so you can flatten, raise or lower past the limit without leaving the panel. The cut and fill line then counts all of it.

## v1.36.0 — 2026-10-14

### Added
- **The Windows program is signed.** `ValheimWorldEditor.exe` now carries a digital signature, so Windows names the publisher and the "Windows protected your PC" prompt goes away as SmartScreen gets to know it. See [Releasing](docs/development.md#releasing) and [SECURITY.md](SECURITY.md#checking-a-download) for how it is checked.

## v1.35.4 — 2026-10-14

### Fixed
- **Live mode could lose a request with "Connection reset by peer".** The editor now opens a fresh connection after 10 seconds unused, before the game closes the old one, so a look at the game or an apply no longer fails at random.

## v1.35.3 — 2026-10-14

### WorldEditorBridge
- The plugin's log now says more about each apply, to help when a change does not show for every player, including how many objects the game knew before and after and how many players are connected. A second later it also reports how many destroyed objects the game still has; see [What the plugin logs](docs/live-mode.md#what-the-plugin-logs).

## v1.35.2 — 2026-10-14

### Fixed
- **Opening another area erased the history made before it.** Steps that do not fit the new area are now kept aside and come back when you reopen the area they were made in, and the History panel says how many are kept aside.

## v1.35.1 — 2026-10-14

### Fixed
- **Worlds made with generated zones had no locations and no creatures in them.** Worlds created with `WorldCheck create` now get the start temple, altars, ruins and the zone control that creatures spawn from, as the game would make them; worlds from the editor's **New world** page are not affected.

## v1.35.0 — 2026-10-14

### Added
- **Live mode for mods' own plugins (the bridge API).** A mod's plugin can now register with the WorldEditorBridge to be told what the editor applied and to answer calls on routes of its own, and a mod's editor extension can see which mods registered and call their routes. See [Live mode for mods](docs/modding.md#live-mode).

### WorldEditorBridge
- The plugin can now list the registered mods' plugins and call one of a mod's routes, and every apply tells the registered listeners what changed. Older editors see no difference.

## v1.34.0 — 2026-10-14

### Added
- **Ground cuts for mod extensions.** An extension can leave the ground undrawn inside outlines it gives, such as a tunnel mouth or a pit a mod digs, so what the mod builds below shows and clicks go through the hole. See [Extensions](docs/modding.md#extensions).

## v1.33.0 — 2026-10-14

### Added
- **Editor extensions for mods (the SDK).** A mod can ship a small extension that teaches the editor things a file cannot, such as reading the mod's packed data in the inspector or drawing what the mod builds in code. It runs only once you turn it on in **Settings → Mod extensions**, and an updated file waits until you turn it on again; see [Extensions](docs/modding.md#extensions).

## v1.32.0 — 2026-10-14

### Added
- **Unity's cylinder, sphere and capsule.** Mods' pieces built from these shapes are now drawn.

### Fixed
- **Glass and other see-through materials are drawn see-through.** Crystal windows are no longer invisible, nearly clear glass shows as a tint, and the game's own see-through objects such as ship glass, gems and some ice are blended properly, as are building pictures.
- Mods' materials that keep their texture under another name are now drawn with it. For example, VALKEA's dried grass now shows its texture.
- Mods' sounds and effects, which have nothing to draw, are no longer offered in Place, while those already in a world still show.

## v1.31.0 — 2026-10-14

### Added
- **Linked objects.** Objects a mod builds out of others, such as a ship and the pieces built on it, are now drawn where the mod builds them and are moved, turned, copied and deleted as a whole group, with each pasted copy getting its own ids. A mod says how its objects link in its `worldeditor.json`; see [Linked objects](docs/select.md#linked-objects-ships-and-other-mods-groups) and [for mod authors](docs/modding.md#linked-objects).

## v1.30.0 — 2026-10-14

### Added
- **worldeditor.json for mod authors.** A mod can ship an optional `worldeditor.json` that tells the editor things it cannot guess, such as labels and help for object data, prefabs not to offer, and groups for the Mods tab. See [For mod authors](docs/modding.md) and the [schema](docs/worldeditor.schema.json).

## v1.29.0 — 2026-10-14

### Added
- **Mods' data by name in the inspector.** Values that mods keep on objects now show by their names instead of numbers, found by reading the mods' files in your Valheim folder and mod manager profiles without ever running them. The game's own name still comes first when both have one.

## v1.28.0 — 2026-10-14

### Added
- **Crunched textures read.** Textures that mods keep in Unity's crunched formats are now read, so pieces that showed untextured, like OdinsKingdom's stone walls, are drawn with their textures.

## v1.27.0 — 2026-10-14

### Added
- **Modded pieces drawn as in game.** A mod's pieces learned from a live game now appear with their own models and textures in the 3D view, the Workshop and the Build panel's pictures instead of as boxes. Pieces a mod builds in code while the game runs (ValheimRAFT's sails) and crunched textures are not drawn yet, so those show untextured.
- **Mods tab by mod.** The Build panel's Mods tab now shows one mod's pieces at a time, chosen from a row of mods, with at most 300 shown at once (search for the others), so a mod with thousands of pieces no longer buries the rest or slows the panel.

### WorldEditorBridge
- The plugin can now report what the game draws for a prefab, by mesh and material names and sizes.
- A prefab's mod is now named as it was installed rather than after the plugin inside it, so ValheimRAFT's pieces are no longer listed under "Inventory Patch Detector".

## v1.26.0 — 2026-10-14

### Added
- **Modded games' objects.** Opening a live world now asks the game for the objects its mods add, so they get their names and can be placed, the Workshop's Build panel lists them on a new **Mods** tab with their cost, and the inspector says which mod an object comes from (when Jötunn is installed). What was learned is remembered, so a modded server's world read offline shows them too, though the mods' models are not drawn yet; see [Modded games](docs/live-mode.md#modded-games).

### WorldEditorBridge
- The plugin can now list every kind of object the game knows, mods' ones included, and which mod adds each (when Jötunn is installed).

## v1.25.3 — 2026-10-14

### Fixed
- **The Cut sliders now start with no cut.** In the Workshop and the Dungeon tool the right end now means no cut, and sliding left removes pieces from the top down to the ground (Workshop) or the floor (dungeon).
- **A pasted town stands as it was copied.** Pasting a copied town used to leave it half fallen down in game, with empty chests and trees growing through its floors.
  - Copy now takes only the ground the buildings stand on (under each piece and a metre around, blended into the new place), so the town sits naturally where it is pasted with its foundations and cellars supported. The Select tool's copy does the same.
  - Pasted objects keep everything the originals held: chests their contents, signs their text, pieces their builder and health. A copy saved as a blueprint keeps the contents but not the builder.
  - The copied ground can now go past the game's ±8 m limit when the new place needs it. Before, it stopped at 8 m and pieces left floating fell in game.
  - **Clear the site** no longer digs trenches around foundations set into the ground. It also ignores pieces hidden far underground, which used to make it dig a huge pit.
  - A paste that does not fit in the open area is now refused with a message, and its outline turns red while it does not fit. Before, the pieces were placed but the ground and trees there could not be handled.
  - The ground and the objects of a paste now line up exactly. The click point moves by half a metre at most.
  - Claude's copy tool also keeps what the originals hold.

## v1.25.2 — 2026-10-12

### Changed
- **Releases are now published automatically after the maintainer approves them.** The plugin now builds against any Valheim dedicated server and BepInEx folder.

## v1.25.1 — 2026-10-12

### Fixed
- **A ruin you built onto shows whole.** The stones of a ruined tower patched with wood used to be hidden, so only your wood showed in the 3D view and on the map. Pieces of a ruin that touch yours now count as your building, and the map has a new **Show ruins** switch (off at first) to draw every ruin, fainter.

## v1.25.0 — 2026-10-12

### Added
- **The game's locations are drawn whole.** Parts the save does not store, such as a crypt entrance's rocks, the trader's camp, a boss's altar, a troll cave's walls and the rune tablets of ruins, now show in the 3D view with **Ruins & structures**. The game rebuilds them each time a zone loads, so they cannot be picked or changed.

## v1.24.1 — 2026-10-12

### Fixed
- **Windows: no more old frames or black lines across the window.** With some graphics drivers (older ones, laptops with two graphics chips) the 3D view and the map showed stale frames and lines crossed the buttons. The editor now draws through Direct3D on Windows, and **Settings › Drawing › Draw with the graphics driver's OpenGL** switches back at the next start.

## v1.24.0 — 2026-10-12

### Added
- **Claude changes objects already there.** Claude can now move, turn, tilt, resize, replace and inspect objects, and change their values (a portal's tag, a ward's permitted players, health, fuel, a creature's level and more). Objects keep what they hold when moved.
- **Claude finds places in a world.** Claude can list the world's locations (the start, traders, boss altars, dungeon entrances, villages) by the names players use. It can also search every object the way the map's Search does.
- **Claude copies blueprints and places them into worlds.** Claude can make copies once, in a row or in a ring, and can put a library blueprint into a world's area, turned as asked. This counts as one of Claude's steps, so History's **Take back Claude's changes** removes it.
- **Claude weathers and regrows ground.** Claude can run the Area tool's ground actions (smooth, naturalize, erode, restore, raise, lower) over a rectangle. It can also regrow what the game grows there.
- **Scripts move, tilt and snap objects.** Scripts can now read and set an object's turn, tilt and size, place pieces so they snap like the hammer does, and move or resize objects already there. All of it stays one undo step and keeps what objects hold.
- **Claude digs along lines, resets zones and makes worlds.** Claude can run the Path tool along a line (a river, a moat, a roofed cave, a ramp), mark zones for reset, and preview what a seed's world will look like. It can also create and open a new world.
- **Claude stamps, imports heightmaps and masks its ground work.** Claude can press stamps (Mountain, Mesa, Crater rim, Dunes, Rocky ground, your own, or any PNG) into the ground, import a heightmap picture and paint. Its ground tools can use a mask like the Mask panel's (heights, slopes, biomes, paint).
- **Claude edits the game's dungeons.** Claude can look at a dungeon's rooms, open ends and door joints. It can add or remove rooms, close open ends and set doors, as the Dungeon tool does.
- **Claude saves blueprints.** Claude can save the Workshop's building to the library with what objects hold and a picture, and refuses to replace an existing blueprint unless told to. It can also open a blueprint in the Workshop.
- **Claude uses every control of the editor.** Claude can list and set every switch, dropdown, slider, number box and button the editor shows, as you would. Saving, applying live, discarding, reloading, writing or deleting your files and connecting to the live game are refused.
- **Claude reads the ground exactly.** Claude can get the height at any point (now and as generated), plus the biome and any water there. Its object list also gives tilts and size when an object has them.

### Fixed
- **Moving objects on the Workshop's plot** (or a generated dungeon's, before saving) no longer loses what they held. A chest's contents and a sign's text now stay.

## v1.23.1 — 2026-10-12

### Fixed
- **Code coverage reporting is accurate again.** Roughly a third of the app's automated checks used to go uncounted, so coverage is now measured properly and the app has its own 95% target. No change for users.
- **Workshop:** a click that moved the mouse a little before letting go placed nothing. It now places the piece the preview shows.
- **Discard** no longer fails silently when the world's files cannot be read. It now says why and keeps the area open.

## v1.23.0 — 2026-10-12

### Changed
- **Larger areas.** The area can now be 9 × 9, 13 × 13 or 17 × 17 zones (up to 1088 m across), from the map's size box and the top bar's. Follow can grow it up to 17 zones each way, and Claude can open areas up to 17 × 17; very large areas are slow to open and draw, so they suit big works.
- **Faster drawing of large areas.** Only the parts of the area in view are drawn, and small objects stop being drawn far away. A 17 × 17 area with 2.3 million objects went from 31 to about 75 frames a second.

### Fixed
- **Unsaved marks** kept their green dots after **Save to world**. They now go once saved, as after **Apply live**.
- **Areas with very many new objects** were slow on every frame, down to about one frame a second in the largest case. They now stay smooth, and the green dots are only redrawn when objects change.
- **Scripts on areas with very many objects** waited over a second before running. After saving many new objects, counting what is still pending was also slow, and both are now fast.

## v1.22.0 — 2026-10-12

### Added
- **A saved world keeps its list up to date.** World folders added or removed while the editor runs (or a world saved again) now show up by themselves within a few seconds. **Refresh** still looks for them at once.

## v1.21.0 — 2026-10-12

### Added
- **Spawn points and creature nests** (the invisible points the game's camps, villages and dungeons spawn creatures from) now have their own kinds in the Place list and the View panel, each saying what it spawns. In the 3D view they are orange boxes (red when the creature comes back) and nests are purple, seen within 250 m and off at first like ruins.
- **Objects are sorted into real kinds.** Furniture & props, Bones & remains, Creatures & NPCs and Effects & markers join the lists. **Other objects** went from about 470 kinds to a handful.

### Changed
- **Follow grows the area** instead of moving it. When you look near the area's edge, a row of zones is added on that side and the open zones stay, up to 16 zones each way (about 1 km).
- **The tool rail** is now in sections in a clearer order. These are **Sculpt**, **Edit**, **Paint**, **Create** and **Tools**.
- **The Place panel** is now organised in sections with headings, from **Line shape** to **Choose a preset**, **Selected kinds** and **Objects**. The preset list starts on **None**, the selected kinds sit in a box with each kind's share, **Choose kinds…** opens the list under them, and **Tilt** starts at 0.
- **Place's One at a time** now places only the object at the cursor. A drag no longer scatters more, and Density, Spacing and Clumping hide while it is on.

### Fixed
- **Kind names with an underscore** (like Pickable_Flax) showed without it in the Choose kinds list and on the Favourite and Recent buttons. They now show correctly.
- **The Place panel's weight sliders** stuttered during a drag and now run smoothly. Each slider is the kind's share in %, and the others move with it so the shares always make 100%.
- **Previews over the sea** (mountains, placed and pasted objects, rings and lines) were hidden wherever water lay behind them in the 3D view. They now show over the water.

## v1.20.2 — 2026-10-12

### Fixed
- **Location markers** no longer put a post on every invisible ground disc made by No limit, which hid the real locations. The "location(s) here" note no longer counts them either.

## v1.20.1 — 2026-10-12

### Changed
- **While the game's look is prepared** (start page), every action is greyed out until it is done. A world opened meanwhile used to miss the game's look, and the side menu still moves between pages.

### Fixed
- **Rocks that shape the ground** (the Plains' tall rock pillars, coast, heath, forest and copper boulders) were missing from the 3D view and the object lists. They now show and can be selected like other rocks.

## v1.20.0 — 2026-10-11

### Added
- **The start page has a side menu.** It has **Home**, **Edit a world** (my game, a dedicated server, a saved world) and **Create** (the Workshop, a new world). Home shows the game when it runs, with **Edit live**, and what you opened last, and the Workshop's page lists every blueprint to open.

### Changed
- **The world map** of a world opened again shows at once. The last world's map is kept instead of being worked out again, which took seconds.
- **The editor now reads the game's own data from your Valheim** (locations, objects, build pieces, object data, vegetation rules, dungeon rooms and more) instead of keeping its own copy that went out of date. It always matches your game version and takes a few seconds the first time and after a game update, and Valheim is now required to open any world or the Workshop.

### Fixed
- **The start page** said "Preparing the game's look" at every start even when it only checked saved data. It now says so only when it really reads the game's files.
- **Settings > Claude** could still say the editor could not listen on its port after Claude was turned off. The message is now cleared when the switch is off.
- **Script tool:** Ctrl+Enter in the code box runs the script again, as its button says.
- **The world map** zoomed out a little from the closest views kept a processor core busy for nothing. It no longer does.
- **Preparing the game's look** now shows how far it is and what it reads. Before, the bar stood still at the start.
- **The world map** no longer holds up the rest of the editor while it is built. On computers with few cores, a live game's players and other background work waited seconds for it.
- **Damaged or tampered files are refused cleanly.** A save, live snapshot, blueprint or heightmap picture that is damaged now says so, where it could fail with an unrelated error or ask for gigabytes of memory.
- **Generate dungeon** could place an outdated dungeon if you changed a setting while the last one was still being made. A dungeon made for settings that changed since is no longer shown.
- **My game** (start page) now says when your game could not be checked, such as a folder that cannot be read. The log has the error too, where before the card only stopped changing.
- **The history kept on disk** failed when the world's saves were removed while it was open. It is now kept without comparing it to a save.
- **Zone grid** (map) on a scaled screen (200 %, for instance) showed from twice as far out with its lines cramped together. It now shows from the same zoom on every screen.

## v1.19.0 — 2026-10-11

### Added
- **A new world** (on the start page) makes a normal world that players join without any mod, crossplay included. Before it is made, you see the seed's land, biomes, start temple, boss altars and traders, and **Find a seed** searches many seeds for what you ask for.

### Changed
- **The world generator is about a third faster** for map previews, Regrow nature and new worlds. The results are identical.

### WorldEditorBridge
- **A failed request's log line** now stays on one line. Line breaks in it could make it look like several lines in the game's log.

## v1.18.0 — 2026-10-10

### Added
- **Building skill for Claude Code.** A workflow for buildings and places that look hand-made: research and a design brief, mock-ups, phased building, checks for mistakes, then interiors and landscaping.
- **Claude can work in the editor.** Turn on **Allow Claude to connect** in Settings: it runs on this computer only, protected by a secret token, and lets Claude Code or a compatible client look at, shape and build in the open world or Workshop, with each change pending until you save.
- **Item stands and armour stands** can now be filled. The Inspector's **Holds** puts an item on a stand with its quality, style and hanging, and **Wears** dresses an armour stand slot by slot and sets its pose, offering only what the game lets each stand or slot take.
- **Generate dungeon** is now in the Workshop. The Build panel's Library has **Generate dungeon…**, with **Open on the plot** (the dungeon alone) and **Add to the plot** (with what is there, one undo step), and its blueprint is kept in the library.

### Fixed
- **Serving tray food stays on the table.** Food and drink from the serving tray that you placed, pasted or added with a blueprint were loose items in game that fell and despawned away from a base. They are now marked as pieces, as the game does.

### Changed
- The Windows package now holds the program with its four libraries beside it (keep them together) and carries its details such as name and author. Some antivirus programs flagged the old package because it unpacked those libraries at each start.


## v1.17.0 — 2026-10-10

### Changed
- The game's look (models, textures, the terrain shader and the map) is now **read straight from your Valheim** instead of being copied. There is no multi-minute copy at first start and after game updates, no 150 MB folder, and downloads are smaller (about 23 MB less on Linux and 10 MB less on Windows).
- The editor now runs on **.NET 10** because .NET 8's support ends in November 2026. There is nothing to install, as the packages still carry it.

### Added
- When Valheim is not found, the start page now explains step by step how to find its folder. A folder next to the game folder is taken as the game folder it leads to, on the start page and in Settings.
- Live: the open area **follows the game**. Every 2 seconds the editor picks up objects players built, removed or changed in your area, and ground dug or paved there, without adding History steps or dropping your changes.
- Live: where you have unapplied ground changes and the game changed that zone's ground too, yours are kept and **Apply live** asks first. Undo your changes there and the game's ground comes in.

### Fixed
- Place, Line: a line exactly a whole number of spacings long sometimes left out its last object. It no longer does.
- Opening another world while the first one's map or area was still being drawn no longer takes that world's rivers and streams away. Making a world's ground while Regrow or another world runs also no longer shifts its rivers.
- Ground is now worked out a little faster.
- A blueprint or copy with a damaged object value no longer makes saving the world fail. The damaged value is left out when the blueprint is read.
- In the Workshop, the pieces of a ruin blueprint (a generated dungeon) added to a building now stay a ruin's in the saved blueprint instead of becoming the builder's.
- The 3D view no longer stalls for a frame after each Dungeon tool change. The dungeons' rooms are now read in the background.

### WorldEditorBridge
- The plugin can now report which zones changed and send the saved objects of just those zones. This is what lets the editor follow live changes cheaply, ignoring creatures walking or fires burning.

## v1.16.0 — 2026-10-09

### Added
- **Generate a dungeon** (Dungeon panel) makes a whole dungeon from a few choices and a seed, built of building pieces or of the biome's own dungeon rooms. A plan of each level updates as you change the choices.
  - There are eight styles: crypt, catacombs, temple, fortress, prison, Dvergr hold, goblin warren and ruins. Each has its own rooms, furnished for its purpose.
  - There are seven wall materials and eight biomes for monsters, chests, loot and lights.
  - Settings cover size, levels, monsters, the boss, loot, light, furniture, ruin and ways round.
  - The layout reads like a real building, with rooms along axes, wings in pairs, loops between close rooms, stairs between levels and halls rising through two levels.
  - A door closes every room so its monsters wait behind it, and the boss's arena sits behind a gate opened by a crypt key found in a far chest. The treasury lies beyond the arena, and hidden caches sit behind cracked walls.
  - **Place in the world** puts the dungeon 5000 m above the view with a linked portal pair as one undo step, written as a ruin like the game's own dungeons. **Open in the Workshop** opens it as a blueprint.
- **Blueprints keep what objects hold**, including chests' contents, signs' texts, creatures (a dungeon's boss, with its stars) and whether the building is a ruin. The extra data is skipped by Homestead and PlanBuild, so the file still builds in game, and it comes back when the blueprint is opened, saved or placed in a world.
- **Chest contents as in game** (inspector) show the container's slots in a grid. Click one to choose its item, count and quality, empty it, or move it to another slot, and a double click on an object opens its inspector.
- **Dungeon** tool: edit the rooms of Frost Caves, burial chambers, sunken crypts, infested mines and the game's other dungeons. The game builds what you make, with no mod needed.
  - **Go inside** takes the view up to the dungeon and cuts its roofs away to look into its rooms.
  - Pick a room and click an open end (a green frame) to join it there as the game does, and **Turn** (R) joins it by another opening. A red box warns when it would overlap another room or leave the dungeon's space.
  - Added rooms come with what the game would put there, such as chests, creature spawners, torches, ice and decorations.
  - **Doors** go where rooms meet. An added room gets the dungeon's door by the game's chance, and a click on a square between two rooms adds or removes one.
  - Delete a room (its contents and doors go with it), and **Close open ends** caps them with the game's end caps. Each change is one undo step, and **Save to world** and **Apply live** write it.
  - **Build here** lets you build with the hammer's pieces inside the dungeon, on its rooms' floors.
- The game's look now includes the dungeons' rooms, so it is copied once more after this update.
- **Dungeon rooms** in the View panel shows or hides them.

### Fixed
- Contents set in the inspector on a chest never opened were replaced by the game's own loot when it first loaded. Such a chest is now marked as already filled.
- The support check now knows pieces the game never checks, such as Mistlands' large black marble floor, carts, ships and wards, which stay up wherever they are. A building on such floors in the air no longer shows walls that would fall.

## v1.15.6 — 2026-10-09

### Fixed
- Saving a world the game saved since the editor read it no longer throws away what was done in the game. The editor now says Valheim saved the world and saves nothing, so leave the world and open it again to edit the game's save.
- Changes saved or applied live from the map are now kept as saved. Before, **Discard** in the next opened area undid them and saving again took them out of the world.
- **Area → Backup** no longer restores another world's backup. A folder chosen with "Another folder…" stays in the list when another world is opened, and the backup's seed is now checked.
- Live: ground changed while **Apply live** waits for the game (a second stroke with Auto apply on) is no longer lost. It now stays pending and goes with the next apply.
- The editor's keys (Ctrl+Z, Ctrl+Y, Ctrl+S, Ctrl+V, the tool keys) now do nothing on the start page and the map. Before, they acted unseen on the area left open behind, such as undoing its last step.
- A world with two terrain objects in one zone can now be saved. Every save used to fail with "An item with the same key has already been added".
- A save cut short (the game or the editor stopped while writing) is no longer taken for the world's save. The world opens from the last complete save, and leaving it no longer deletes that save or your own files in its folder.
- Copying the game's look no longer breaks for good when it is stopped halfway, such as by closing the editor during the first copy. Each file is now written whole or not at all.
- After a Valheim update, the game's look is copied from the new game files. Before, new kinds got no model, changed ones kept the old one, and a removed file made the copy fail every time.
- Settings and saved servers are written whole or not at all, so a crash or power cut no longer resets your settings or loses saved servers. Settings that cannot be read are kept as a backup file, and a locked settings file no longer stops the app.
- The saved servers file is now readable only by you from the moment it is made, and a saved password with spaces at its ends no longer loses them.
- Opening another area, saving or switching the look while models are still being read no longer closes the editor or draws objects of the area left.
- An unexpected error no longer closes the editor and loses unsaved changes. It is written to the log and shown in the status bar, and saving, applying, Discard and leaving a world now say what went wrong.
- Blueprint import (also by dropping a file on the 3D view), export, delete and conversion to Homestead now say so when the folder cannot be written, instead of closing the editor. So does exporting a heightmap.
- Opening a blueprint in the Workshop that cannot be read now keeps the open world as it was. Before, the world was closed first and the editor kept showing an area that could no longer be saved.
- A folder of worlds added in Settings that holds an unreadable folder, such as a drive's root, no longer leaves the start page with no world listed. The same goes for mod manager profile folders.
- Shape formulas: a function given the wrong number of values is now explained when the formula is read. A very deeply nested formula is refused instead of closing the editor.
- A damaged history file no longer stops its world from opening, which now happens without the earlier history. A stamps file with an entry missing its picture no longer stops the editor from starting.
- Valheim for Windows: when the terrain shader cannot be converted on this computer, the start page now says so. Before, it copied the whole game look again at every start with the same result.
- An object put or pasted far outside the world (beyond about ±16 km) is now left out of the save and listed under "Not saved". Before, the whole save failed.
- The Mask's slope, height and paint rules now judge multi-point edits (Mountain, Shape, Paste, Stamp, Path) on the ground as it was before them. Before, with a slope limit, most of a mountain was left out in a ragged pattern.
- Copies and blueprints now keep objects of kinds the editor has no name for, such as from mods. Before, they came back as another unknown kind and were left out when pasted.
- The Area tool's **Restore** now also takes back No limit ground, as the Restore brush does. Over a mountain it used to change nothing visible.
- A **Path** with Smooth along the west or east edge of the area no longer mixes in the ground of the opposite edge.
- Pointing past the edge of the area no longer finds "ground" 1000 m down. This affected the Measure tool, pastes, brushes and the Path and Area tools, and ward and workbench rings crossing the edge no longer plunge down outside it.
- An object found with the map's search and opened with **Edit in 3D** now stays selected in the editor.
- **Script**: a script's changes are no longer applied when the area changed while it ran, which could overwrite your stroke or remove the wrong objects. A script with an invalid object number no longer leaves its ground change without an undo step.
- Closing the window with unsaved changes and answering **Keep editing** no longer stops the copy of the game's look running in the background.
- Live, SSH: a saved server that uses a key file with a passphrase can connect again, with a new **Key passphrase** box that is never saved. An unusable key in your SSH folder no longer stops the login before the password is tried, and the server's identity is now checked for every account on that address.
- The README in the editor's packages now names the plugin's file as it is on the release page.
- The world generator is closer to the game's: of the heights recorded in the game, 2992 of 3000 now match bit for bit (2982 before), and the rest differ by less than a millimetre. Rivers, streams and lakes are worked out with the game's precision.
- The map and areas are made faster on computers with many cores. The ground of 256 zones takes about 0.1 s instead of 0.6 to 1.3 s.
- Areas with very many objects of one kind (tens of thousands of one piece) now open, save and switch the look without a pause of a second or more.
- Worlds open and save faster, since reading no longer searches every edited zone for each object. A world of 355,000 objects reads in 0.24 s instead of 0.57 s, and worlds with thousands of edited zones gain much more.
- The editor uses less memory over a long session, since models are released once on the graphics card and let go when opening another area past 1 GB. Before, they piled up to gigabytes with Valheim open beside it.
- Areas with hundreds of kinds of objects no longer hold up other background work while their models are read. The next area, the map's search and close-up used to wait seconds behind them.

### WorldEditorBridge
- An apply the game takes more than 30 seconds to start is no longer done later anyway. Before, the editor was told it failed and sent it again, so new objects were made twice for every player.
- Objects linked to others (a creature spawner and what it spawned, two connected portals) now keep the link when a live delete or move is undone. Before, a spawner could spawn a second creature, and the other side still pointed at the removed object.
- A malformed object to make now fails the whole call before anything changes. Before, objects processed earlier stayed done, and sending the call again made them twice.

## v1.15.5 — 2026-10-09

### Fixed
- Copying the game's look now works with Valheim for Windows. It used to stop with an error because it looked for a shader only the Linux and Mac games have, and it now uses the Windows one, so the ground looks as it does from a Linux game.

### Added
- **Open log**, at the bottom of the start page and when copying the game's look fails, opens the editor's log so you can attach it to a bug report on GitHub.
- The log is now `ValheimWorldEditor.log`, started over at each run, with a time on each line. It also says more, including the game-look copy's output and errors, which Valheim folder and build it copied from, and errors in background tasks.

## v1.15.4 — 2026-10-09

### Fixed
- Undoing a placed object after **Apply live** now removes it for the players too, on servers with mods that remake objects under a new id (ServersideQoL does, for build pieces, plants, fires, turrets and more). Before, players kept seeing the new copy, and this needs the plugin from this version on the server.

### WorldEditorBridge
- An object to remove whose id is gone is now looked for by its kind and place (within 10 cm) and removed if found. Another kind of object or an empty place is left alone, and older editors still work as before.

## v1.15.3 — 2026-10-09

### Fixed
- The editor no longer fills the graphics card's memory over a session. Each trip between the map and the 3D editor and each move to another area used to leave data behind, so the editor now stays near the same size and leaves room for a game played beside it.

## v1.15.2 — 2026-10-09

### Added
- The history is kept after the editor closes. After each **Save to world** or **Apply live** it is stored with the world, and when you open the world again its steps are back in **History**, tagged **earlier session**, ready to undo, and the editor asks once before undoing them if the world may have changed since.

### WorldEditorBridge
- A new mod page explains what live editing is for, how it works, what to expect and what each connection message means. The plugin itself is unchanged.

## v1.15.1 — 2026-10-09

### Changed
- **Blueprints** on the tool rail now works like the other tools. It lights up and opens the blueprint list next to the rail instead of on the right over the View panel, and **Blueprints…** in the Area tool opens it the same way.

### Fixed
- The saved-world page no longer says each save makes a full backup, which has not been true since 1.14.0. Each save is read back and checked before the old one is removed.
- The server form now says the plugin's token is written the first time the server starts with the plugin, and the README, the plugin's readme and its mod page say so too. The start page pictures also show the Workshop.

## v1.15.0 — 2026-10-08

### Added
- The area's size (3 × 3, 5 × 5 or 7 × 7 zones) can now be changed on the fly next to the Area arrows, around the same middle zone. Your view, changes and history stay.

## v1.14.1 — 2026-10-08

### Fixed
- Zooming out no longer fogs the whole area over. The fog now starts where the view turns around, so only what lies beyond it fades.

## v1.14.0 — 2026-10-08

### Changed
- **Save to world** (offline) now works like Apply live, so the area and its history stay after saving and you can undo a step and save again. No backup folder is made at each save any more, since nothing of the old save is removed before the new one reads back correctly.

## v1.13.0 — 2026-10-08

### Added
- **Mountain** now shows a preview before you click, as a mesh on the ground as high as the mountain will rise. The circle turns red where it does not fit in the open area.

### Fixed
- A wide brush or Mountain circle no longer drops a line straight down where it passes the edge of the open area, and it follows hills instead of cutting through them.
- Zone borders and other overlays on the ground now follow the ground when it changes. After undoing or discarding a mountain they no longer outline it in the air.

## v1.12.1 — 2026-10-08

### Fixed
- Buildings copied with the Select tool now keep their shape when pasted on a slope. Each piece no longer follows the ground on its own, while trees and rocks still do.

## v1.12.0 — 2026-10-08

### Added
- **Paste** now shows what it will place as see-through models at its height, not only posts. Ctrl + wheel lowers or raises it, and Clear the site digs it out if it goes into a hill.

## v1.11.0 — 2026-10-08

### Added
- In a world, a **Blueprints** button on the tool rail opens the library so you can paste a blueprint made in the Workshop.
- **Clear the site** (Paste, on by default) digs the ground in a pasted building's way down to its lowest piece and removes the trees, rocks, bushes and pickables there. It is part of the paste's undo step.

## v1.10.0 — 2026-10-08

### Changed
- A blueprint opened or added in the Workshop now stands on its lowest buildable piece, and rocks and other hoe-placed things no longer count. The support check also assumes the ground in game was under the lowest piece at each spot, and leaves rocks out.

## v1.9.1 — 2026-10-08

### Fixed
- Objects no longer show untextured (white) after opening another area while models were still loading.

## v1.9.0 — 2026-10-08

### Added
- **Cut** (the Workshop) shows the building only up to a height, a metre at a time. The cursor goes through what is hidden, so you can build inside it.

### Changed
- The **support check** now tints pieces in the game's build-mode colours (light blue on the ground, green to red) instead of outlining them. The pieces' own look still shows.
- The Workshop now draws pieces solid, and See-through buildings is for worlds.

## v1.8.0 — 2026-10-08

### Added
- The Workshop's **Library** tab shows your blueprints with pictures, cost and search. You can **Open** one alone on the plot, **Add** it, drag it onto the plot, or drop .blueprint and .vbuild files in to import them, and blueprints keep their form exactly.

### Changed
- The start page's Workshop card now opens the Workshop, and the blueprints are in its Library.
- There are no unsaved marks in the Workshop, since everything on the plot is new.

## v1.7.0 — 2026-10-08

### Added
- **Select**: Ctrl + click adds to the selection, and Shift + click selects the row from the last object clicked (wall 1, Shift + click wall 3 selects walls 1 to 3).

## v1.6.0 — 2026-10-08

### Added
- The Workshop now places pieces where the **game's hammer** would, using the game's own data for every piece. The piece touches what you point at, snap points within half a metre meet, and pointing at a wall's top puts the next one on it.
- **Ctrl + wheel** lifts the piece (Shift: 0.1 m), and , and . turn it by the game's 22.5° or another step.
- The Build panel's **Plants** (cultivator) and **Feasts** (serving tray) tabs now offer everything players can build.

### Fixed
- Turning with the wheel now takes one step per notch, smooth wheels and touchpads included, and a snapped piece keeps the turn you asked for.

## v1.5.0 — 2026-10-08

### Changed
- The Workshop now keeps to building, with **Build**, **Select** and **View**. The hammer's pieces are in tabs with pictures, searchable by crafting station and name, with snap, grid and turn step, and the world tools, Mask, View panel and zone borders are gone there.

## v1.4.0 — 2026-10-08

### Added
- The blueprint library now shows each blueprint's **3D picture**, its in-game **cost**, a **description** and **tags**. The search looks through all of them, and **Details** changes them.

## v1.3.0 — 2026-10-08

### Added
- **The Workshop** is a blank, flat plot (from the start page or the Blueprints panel) where you build a building and then **Save blueprint**, keeping only its building pieces. Blueprints open in it to be changed.
- **Support check** (in the Workshop) works out each piece's structural support by the game's own rules, from full on the ground to what would fall. Saving asks first when some pieces would fall.

## v1.2.0 — 2026-10-08

### Added
- Blueprints now use **Homestead**'s format (the in-game building mod), saved into its folder so they show in its hammer tab, ready to build in game. The editor finds Homestead in the game's BepInEx and mod manager profiles and warns when it is not installed.

### Changed
- **Save blueprint…** now writes Homestead blueprints (pieces only, no ground). Blueprints in the editor's older format are still listed, and **To Homestead** moves them.

## v1.1.0 — 2026-10-08

### Added
- **No limit** (in the brush options, for every ground tool) moves the ground past the game's ±8 m for mountains, cliffs and canyons. Saving or Apply live turns it into invisible ground that every player's game accepts, with no mod needed and console players included, and trees, rocks, ore, bushes and pickables where the ground moved more than 2 m are removed.
- **Mountain** tool: a click raises a lone peak, a ridge, a mountain range, a mesa, a volcano or rolling hills past the ±8 m. Each is made from a seed with natural-looking slopes that mostly keep grass and trees, Randomize rolls a new one that fits the open area, and it clears what it buries and grows the biome's own on its slopes.
- **Cave** tool (the Path tool's new Cave action) digs a trench along a drawn line with an entrance slope at each end, roofed with the game's boulders. A ring makes a ring-shaped cave with one entrance, presets (tunnel, cave, cavern) and Randomize are included, and players can mine the rock roof like any boulder.
- **Script** tool runs C# scripts on the open area to generate anything the tools do and more, and the editor compiles them itself with no .NET install needed. A script runs in the background and its changes go in as one undo step, nothing on failure, and examples are included (a mountain range, terraces, a canyon with a river, scattered boulders, and more).

### Changed
- The editor's download no longer holds the WorldEditorBridge plugin. It is now its own download on the same releases page, Thunderstore and Hexium.

## v1.0.1 — 2026-10-08

### Fixed
- Linux: the 3D view and the map stayed blank without a GPU driver and in VMware virtual machines. They now draw there.
- Leaving a live world now closes its connection to the game.

### Changed
- Every push is now checked further, with a stricter code analysis and a start-up check that the Linux and Windows packages can draw a test world. No change for users.

## v1.0.0 — 2026-10-08 (the native app replaces the web editor)

### Added
- Native app: the game's look (terrain textures, map textures and models) is copied from your own Valheim on first start and again after a game update. The start page shows progress, asks for the Valheim folder if it is not found, and offers Try again if it failed.
- Native app: every control explains itself when you hover it, and a row's label tells the same.
- Native app: a log, the window's icon, the version in the title, and a Documentation link on the start page.
- Native app: a world folder or a live game given on the command line opens straight away, as with the web app.
- Native app, editor: Discard in the top bar undoes the steps not saved or applied yet and keeps the rest of the history. Changes without a step are discarded by reading the world again.
- Native app, live: Reload in the editor's top bar, and an Auto switch that applies every change to the game at once (remembered).
- Native app, live: players are shown in the 3D view with their names, and View has "Go to" a player.
- Native app, editor: move the area by one zone with the arrow buttons, or let it Follow the view near its edge (remembered). Follow waits while a selection, stroke, path or apply would be lost.
- Native app, editor: a note says how many locations are in or near the area.
- Native app, Select: saved selections let you keep a selection under a name, then select it again or add it later, even after saving the world.
- Native app, Area and Select: "pick" next to Replace's list takes the kind from an object clicked in the world.
- Native app: the editor remembers your choices between runs, including the View switches, the map's place, brush settings, Area and Shape settings, your own formula, Select's options, the open right-hand panel and the clipboard.
- Native app: the status bar shows where the mouse is, the ground's height against the original (and the ±8 m limit) and the zone.
- Native app, View: Game look on/off, See-through buildings, 3D resolution (Sharp, Balanced, Fast), and Defaults / All / Ground presets.
- Native app: `[` and `]` change the brush size, H picks the View tool, Space + left drag and Shift + right drag slide the view in any tool, and Alt + wheel turns the selection, paste or Place preview. The help lists every key.
- Native app, Naturalize: New pattern gives another noise pattern for the next stroke or natural path.
- Native app, Area: cut and fill now follows every change of the ground and of the action.
- Native app, Place: **fit** sets the spacing when a chosen kind is wider than it, and the panel says so.
- Native app, Place chooser: Shift + click on Pick from world adds the kind, and on a favourite or recent places only it. You can also Untick all, use all / none per row, fold categories and the open ones are remembered.
- Native app: new objects not yet saved or applied carry a green dot in the view.
- Native app, View: **Unsaved marks** hides the green dots on new objects (remembered).
- Native app, View: **Limit marks** hides the red on ground at the game's ±8 m limit (remembered).
- Native app, Path: before a click, a circle under the cursor shows the path's width and soft edge and the stretch the next point adds, and the ground the path covers is tinted.
- Native app, Select: the object under the cursor is outlined faintly before a click picks it.
- Native app, Place: the brush's outline shows under the cursor in Brush mode.
- Native app, View: **Tamed animals** (boars, wolves, lox, hens, deer, necks and more) and **Runestones** now show, both on at first. Runestones can be picked and deleted, offline and live, but not moved or copied, and the game's look is copied again once for their models.
- Native app: placing or pasting a kind switched off in View now switches it on and says so.
- The developer checks of the world generator and the save writer moved to a separate tool. It can also write a new world from a seed, with its middle generated with the game's vegetation, which is used for the documentation's pictures.

### Fixed
- Native app: after a visit to the map, the 3D editor drew its models without textures (trees as big white and blue leaf cards). Textures are now reloaded correctly.
- Native app, Area: after choosing a backup folder with "Another folder…", the folder picker opened again and again, and a hand-chosen folder was also dropped for live worlds. Both are fixed.
- My game: on Linux, mod manager profiles were listed and searched twice. They are now listed once.
- A character file that cannot be read no longer stops worlds from opening.

- Native app: in a short window the View panel and tool panels ran under the status bar and their last rows could not be reached. They now stop above it and scroll.
- The data folder now follows the standard Linux data location even before that folder exists, where it used to fall back to the home folder.
- Native app, start page: empty error lines and an empty server list left gaps, and the data folder is now shown as `~/…`.
- Native app: Space no longer presses the focused button in the editor.
- Native app, Place: Esc cancels Pick from world.
- Native app, Area: "pick" for Replace now works before an area is drawn.
- A character with a backup copy of its file could be listed under the copy's name, depending on the disk. The original is now kept.
- Native app: folders under the home are now shown as `~/…` everywhere (saving, backups, blueprints, Settings), so no user name is on screen.
- Native app, Settings: the dialog is now as tall as what it holds, with no large empty part.
- Native app: the tools' lines (brush circles, path, area, measure, selection boxes) broke into faint dashes when the 3D view ran below the screen's resolution. They are now drawn a few pixels wide.

### Removed
- The web editor (the page in a browser or web window) is gone, since the native app replaces it with every feature it had. Its JavaScript Script console is not carried over, and C# scripts may come later.
- Native app: the development command-line options (pictures, scripted tools) are removed, while the options for opening a world, zone and size remain.

## v0.41.1 — 2026-10-07

### Fixed
- Apply live: an error answered by the game (the plugin refusing a call) is now reported as "Could not apply live" instead of failing, and the changes stay waiting.
- Apply live: when the game refused the objects, deletions were already counted as sent and never sent again. Now nothing counts as applied until the game accepts it.
- Apply live twice at once (a double click) could create the same new objects twice in the game. It no longer does.
- Native preview, Select: Drop (End) right after selecting an object above another one put it too low, by as much as it floated above the ground. It now lands correctly.
- Native preview, Place: a double-click on the last point of a line or zone did not place it, because the click was taken for dragging that point. It now places it.
- Native preview, map: the 1 m close-up never showed when zoomed in, and the coarse whole-world map was drawn instead. It now shows.
- Native preview: after going between the map and the 3D editor, both could draw with leftovers from the previous view and show errors or wrong graphics. They are now rebuilt.
- Native preview, Place: a circle of walls end to end came out one wall short, leaving a gap. It now closes.
- An object whose data is edited or moved now keeps the game's short position form, so it no longer grows when written back.

## v0.41.0 — 2026-10-07 (plugin settings file renamed)

### Changed
- The editor now finds the plugin's settings under either name, on your game and on a server over SSH, so it works with older plugins too. Every hint and guide names the new file.

### WorldEditorBridge
- The plugin's settings file is now `BepInEx/config/Tie.WorldEditorBridge.cfg` instead of `local.worldeditorbridge.cfg`. On its first start the plugin moves the old file to the new name, keeping the port and token, so saved servers keep connecting.

## v0.40.1 — 2026-10-07

### Changed
- The frame rate log is now written only on demand, with **Record frame rates** in Help, which is off each time the editor opens.

## v0.40.0 — 2026-10-07 (3D resolution)

### Added
- **3D resolution** (View, Look) has Sharp (the screen's own pixels), Balanced (one per screen point) and Fast. Fewer pixels give more frames per second, and the frame rate logs say which one was used.

## v0.39.0 — 2026-10-07 (frame rate log)

### Added
- **Frame rate log** records a line every 0.2 s while the 3D view is used, with frames per second, the editor's work per frame and the longest gap between frames. It is written to a log file in the data folder, with header lines for the browser, the view's size and what is loaded, so you can compare the app's window with the browser.

## v0.38.3 — 2026-10-07

### Fixed
- The frame rate line in Help could not be selected to copy it because it was rewritten several times a second. It now stays as it is while selected.

## v0.38.2 — 2026-10-07 (frame rate in Help)

### Added
- **Help** (`?`) shows the frame rate while the view moves and how many milliseconds of work each frame takes. This helps tell a slow graphics path from a busy editor.

### Fixed
- Help said "the graphics card" for the app's own window on Linux and Windows even when it could not tell. It now says it does not know.

## v0.38.1 — 2026-10-07 (fixes found by new tests)

### Fixed
- Area tool: after Undo or Redo, the counts of what is inside the selection and the Replace list still showed what was there before. The Replace list could even be empty while trees were back, and it now updates correctly.

## v0.38.0 — 2026-10-07 (lighter 3D view)

### Changed
- The 3D view is now drawn only while something happens (the view moves, a stroke, the mouse or keys in use) and a few times a second otherwise. An idle editor now leaves the processor alone instead of drawing 60 times a second.

### Added
- **Help** (`?`) says what draws the 3D view, the graphics card or the much slower processor, and the editor says so once when it is the processor.

## v0.37.0 — 2026-10-07 (pieces that snap, elevation)

### Added
- **Snap to pieces already there** (Place tool, building pieces) locks a wall, fence or floor onto the pieces around it like the hammer, **Beside** them or **On top** of the piece under the cursor, facing like it. Pieces are placed one at a time under the cursor.
- **Elevation** for everything placed: on the ground, a height **above the ground**, or **at one height** for all. `PgUp` / `PgDn` change it, and `Alt` + click takes the top of a piece or the ground.
- **Layers** (Line, End to end) stack a line of walls or fences several pieces high.
- A line started next to a piece already there now continues it, at its level.

### Fixed
- Walls and other pieces with their origin in the middle were half sunk into the ground when placed one by one. They now stand on their bottom.

## v0.36.0 — 2026-10-07 (tidier tool panels)

### Changed
- **Tool panels** now show the settings you use most and fold the rest under one line at the bottom that says what is inside, open or closed as you left it. The **Mask** moves into that fold, which says "mask on" while it is on.
- **?** next to the tool name shows what the tool does and how to use it, and the how-to text is hidden otherwise.
- **Area** now has one **Action** list (Ground, Objects inside, Copy, Bring back) instead of six sections. The panel shows only that action's settings, and one button at the bottom does it (`Enter` too).
- **Place** now lists the chosen kinds with their weights and a ✕ each, and **+ Add kinds** opens the full list in a drawer beside the panel.
- **Select** now puts Delete, Whole building, Same kind, Invert and Exact place first. Other actions are under **More actions**, and the moving switches are under **Moving**.
- **Path** now folds Soft edge, curve and natural look, while Apply and Clear stay at the bottom.

## v0.35.0 — 2026-10-07 (script console)

### Added
- **Script console** (Script in the top bar) lets you run a few lines of JavaScript for jobs the tools do not cover, such as finding, adding and removing objects and reading and setting the ground. Each run is one undo step, examples are included (count kinds, remove young beeches, rocks on steep slopes, terraces), and your own scripts are kept in this browser.

## v0.34.0 — 2026-10-07 (cut and fill)

### Added
- **Cut and fill** in the Area tool shows how much ground inside the selection has been raised and dug since it was generated. Before you click, it also shows what Flatten, Raise or Lower would dig and fill (m³) and what the ±8 m limit leaves out.
## v0.33.0 — 2026-10-07 (overlays)

### Added
- **Ward areas**, **Build ranges** and **Location flattening** in View, Overlays: rings on the ground show where wards protect, where crafting stations let you build (10 to 40 m depending on the kind), and where locations flatten the ground.
- **Go to** a player (View, live mode): jump straight to where they are, or move the work area to them.

### Fixed
- After Undo or Redo in the Place tool, the spacing still counted the objects as they were before. It now follows what is really there.

## v0.32.0 — 2026-10-07 (walk and fly)

### Added
- **Walk** and **Fly** views (`F`): see the world through a player's eyes, 1.8 m above the ground or water, and walk with WASD (Shift runs). Press `F` again to fly (Space up, C down) and once more to return to the usual view; tools, Follow and the Area arrows keep working.

## v0.31.0 — 2026-10-07 (area that follows the view)

### Added
- **Follow** (next to the Area arrows): when the point you look at comes within 12 m of the edge of the work area, the area moves there by itself. It waits and tells you why when moving would lose a stroke in progress, a path or Place shape, an Area selection or selected objects.

### Changed
- Moving the area (arrows or Follow) now keeps your view on the same spot and keeps the tool in your hand. Before, it restarted above the middle with the Raise tool.

## v0.30.0 — 2026-10-07 (history survives reloads)

### Changed
- **History** now survives a page reload and a move of the work area, because the editor keeps it with your pending changes. A change that touched ground outside the new area is left out along with the ones before it, and Saving, Discard and reloading from the game still start a new history.

### Fixed
- A long message in the status bar could wrap onto several lines and cover the last buttons of the tool panel, making them unclickable. It now stays within two lines, with the whole text shown on hover.
- "Switched on … in View" used to replace what the tool had just said, such as how many objects were placed or regrown. It is now added to the message instead.
- Regrow nature offered kinds the page cannot place, and then reported them as regrown. It now only offers kinds it can really place.

## v0.29.0 — 2026-10-07 (regrow nature)

### Added
- **Regrow nature** (Area tool): puts back the game's own vegetation inside your selection for the kinds you pick with the chips, following the game's rules for numbers, slopes, heights, biome edges and groves on the ground as it is now. Spots that are already occupied and the ground near buildings are skipped, and on a world the game generated, most of what it places lands exactly where the game put it.

## v0.28.0 — 2026-10-07 (clumping)

### Added
- **Clumping** and **Patch size** in the Place tool (Brush, Zone): objects gather in groves with clearings between them instead of an even spread, following a pattern fixed to the world. Presets can keep these settings, and Meadows woods, Black forest, Berry patch and Meadows rocks use them.

## v0.27.0 — 2026-10-07 (weighted mixes and presets)

### Added
- **Weights** in the Place tool: with several kinds ticked, a slider per kind sets how often it is used, with its share shown in % (Beech 3 and Birch 1 gives three beeches for each birch).
- **Presets**: load a whole mix (kinds, weights, Density, Spacing, Size, Tilt and facing) from built-in looks such as Meadows woods, Black forest, Swamp, Berry patch, Forest floor and Meadows rocks, or save your own.

## v0.26.3 — 2026-10-07 (spacing that fits)

### Added
- **Place tool**: when a ticked kind is wider than the spacing, the panel tells you and a **fit** button sets the spacing to match. A blueberry bush is about 3 m wide, so a 1 m grid of them used to become one big hedge instead of rows.

## v0.26.2 — 2026-10-07 (editable zone points)

### Added
- **Place tool, Zone**: drag a point to move it, drag the outline to add a point, and Ctrl + click a point to remove it, as with lines. The hints above Place now explain this, for lines too.
- **Remove last point** button (Zone and Line), which does the same as Backspace.

### Changed
- **Leave saplings and crops room to grow** now also applies to grown crops (such as carrots) and to trees that grow from saplings (such as Beech and Oak). They keep the room their sapling needs, as the game defines it.
- **Zone** now fills at random only, so its Scatter / Grid choice is gone. Grid mode is the one that makes grids.
- **Grid** now uses as many whole cells as fit best in the box you drag (at least one), centred. A box slightly short of a full number of cells used to lose a row and a small box got nothing, and the **Cell** slider is now called **Spacing**.

## v0.26.1 — 2026-10-07 (placed objects follow View, chip toggles, Nobody builds)

### Changed
- Objects you placed now follow the View switches like all the others. Before, they stayed visible until saved, so switching off Trees & logs did not hide the trees you had just planted.
- A click on a **Favourite** or **Recent** kind now ticks or unticks it, and each row has **all** / **none**. Placing only that kind is now done with Shift + click.

### Added
- **Untick all** in the Place tool, next to Pick.
- **Nobody (not player built)** in Built by, which places pieces without a builder.

## v0.26.0 — 2026-10-07 (Place tool)

### Changed
- The **Plant** tool is now called **Place** (still `T`). It places any kind of object, with a brush for trees and rocks, and lays walls and fences end to end along lines, circles and rectangles; its guide is now the Place page in the docs.
- **Leave saplings and crops room to grow** now only shows and applies when a sapling or crop is ticked.

## v0.25.0 — 2026-10-07 (player-built pieces)

### Added
- **Built by** (View panel, Building): sets the player written as the builder of the pieces you place. You can choose from the world's builders (the one with the most pieces is picked at the start), players named on beds, wards and tombstones, and characters on this computer, or type any player id, and it is remembered per world.
- **Make player built** (Select tool): gives the chosen builder to selected pieces that have none.

### Fixed
- Pieces placed by the editor were sometimes treated as ruins by the game, losing a third of their materials and their base effects. Now every piece placed, pasted or built gets the chosen builder, and moved or edited objects keep theirs.

## v0.24.0 — 2026-10-07 (eyedropper and favourite kinds)

### Added
- **Eyedropper**: the Plant tool's **Pick** button and the **pick** buttons next to the Replace lists (Select and Area tools) take the kind of the object you click in the world.
- **Favourites** and **Recent** kinds in the Plant tool: star kinds with ☆ in the list, and your last eight placed kinds come back as buttons above it. A click plants only that kind, and Shift + click adds it to the ticked ones.

### Changed
- The Select tool's Replace row now reads "Replace with", and its button is now "Replace the selection".

## v0.23.0 — 2026-10-07 (sharp edges)

### Added
- **Sharp edge** brush falloff: full strength right up to the brush's edge and nothing beyond. It is made for steep walls like the game's pickaxe makes.

### Changed
- With **Soft edge 0**, Path and Area actions now give the ground points that an edge crosses a share of the change. A diagonal edge therefore follows the line straight instead of stepping from point to point.

## v0.22.0 — 2026-10-07 (fine-tune drawn lines)

### Added
- **Fine-tune drawn lines** (Path tool, and Plant lines): drag a point to move it, drag the line to add a point there, and Ctrl + click a point to remove it. The preview follows as you edit.

## v0.21.2 — 2026-10-07 (buildings keep their shape)

### Changed
- **Moving a building "on the ground"** now keeps its shape: its pieces move as one block that settles until its bottom layer sits on the ground, sinking into a slope rather than floating. Trees, rocks and other objects in the selection still land on the ground one by one, whereas before every piece landed separately and buildings were pulled apart.

## v0.21.1 — 2026-10-07 (ring brush outline)

### Changed
- The Ring brush shape now draws its inner edge too. This makes the untouched middle visible on the ground.

## v0.21.0 — 2026-10-07 (exact moves and snapping)

### Added
- **Snap to other pieces** (Select tool, on by default): while you move a piece, its snap point locks onto the nearest snap point of a piece around it. Walls, floors and fences therefore join like they do with the hammer.
- **Turning ring** around the selection: drag it to turn, and hold Ctrl for 15° steps.
- **Exact place**: type the selection's position and turn, or how far to move and turn it.

### Fixed
- "Put each object on the ground" put the middle of a piece on the ground for pieces whose origin is in the middle (such as walls), sinking them. Their bottom now rests on the ground.

## v0.20.5 — 2026-10-07 (pieces follow the line)

### Changed
- **End to end switches itself on** in the Plant tool's Line mode when every ticked kind is a piece the game snaps (fences, walls, stakes...), and off for other kinds, until you change it by hand.
- **Objects follow the line**: circles and rectangles always turn their objects along the outline, and "Follow the line" (before: Face along the line) is on by default for drawn lines. A piece now lies along the line with its length, not its front, following it.

## v0.20.4 — 2026-10-06 (dropdown boxes)

### Fixed
- Closed dropdown boxes were drawn in the system theme (a light box in the app window on Linux) under the editor's light text, so the chosen value could not be read. The editor now draws them itself, dark, with its own arrow.
- A dropdown followed by a button (Replace … go, Paint … apply, Saved … keep) pushed the button onto the next line. They now stay together.

## v0.20.3 — 2026-10-06 (readable dropdowns)

### Fixed
- Dropdown lists could be unreadable (pale text on a pale background) depending on the system theme. They are now always dark, with the chosen entry highlighted in the editor's amber.

## v0.20.2 — 2026-10-06 (dropdown labels)

### Fixed
- In the app window on Linux, the group headings of the Replace lists ("Trees & logs"...) could show blank and trigger warnings in the terminal. They now read "Trees and logs" and so on.

## v0.20.1 — 2026-10-06 (fences on slopes)

### Changed
- **End to end on slopes**: every piece now stays level and stands on the ground where it is, a little higher or lower than the last one and touching it, like a fence going up a hill. Before, each piece sat on the lower of its two ends, which could leave it floating in the middle of a bump.

### Fixed
- End to end placed nothing at all when one of the ticked kinds had no known length yet. Such kinds are now left out and the others are placed.

## v0.20.0 — 2026-10-06 (fences end to end)

### Added
- **End to end** (Plant tool, Line mode): places pieces so that each one starts where the last one ends, turned along the line, like the game's hammer snaps them. It uses the pieces' own snap points, or the model's length for other kinds.
- **Circle and Rectangle lines** (Plant tool): press at the centre or a corner and drag to the size, and use **Close the loop** for drawn lines. With End to end the sizes snap to whole pieces, so a fenced ring or yard closes exactly.

## v0.19.2 — 2026-10-06 (moved objects on the ground)

### Changed
- **Moved objects land on the ground** (Select tool): each moved or turned object now stands on the ground where it ends up. A new switch, "Put each object on the ground when moving" (on by default), can be turned off to keep each object's height above the ground as before, which suits buildings.

## v0.19.1 — 2026-10-06 (area selection)

### Added
- **Clear** in the Area tool, next to Box and Polygon, to start a new selection (as `Esc` does).

### Fixed
- The Area tool's selection outline stayed on screen in every other tool. It is now only drawn in the Area and Paste tools, and the selection is kept for when you come back.

## v0.19.0 — 2026-10-06 (selection helpers)

### Added
- **Same kind, Invert and saved selections** (Select tool): select every object of the selected kinds, select the opposite of the current selection, or keep a selection by name for the world.

## v0.18.0 — 2026-10-06 (select a whole building)

### Added
- **Select a whole building** (Select tool, like Axiom's magic select): double-click a piece, or press Whole building, to select every piece connected to it.

## v0.17.0 — 2026-10-06 (river and canal)

### Added
- **River / canal** (Path tool): digs a U-shaped bed below sea level with sloped banks, so the sea flows in.

## v0.16.0 — 2026-10-06 (shape tool)

### Added
- **Shape** (tool `G`, like WorldEdit's `//generate`): one click puts a mound, cone, mesa, crater, moat, bowl or ridged hill into the ground, or any shape you write as a formula.

## v0.15.0 — 2026-10-06 (erosion)

### Added
- **Erode** (sculpt tool `O`, and an Area action): thermal erosion settles slopes to a rest angle, while water erosion cuts gullies and fills hollows.

## v0.14.0 — 2026-10-06 (heightmaps)

### Added
- **Heightmaps** (Area tool, like WorldPainter): export the area's ground as a 16-bit grayscale PNG, and import a picture back into the selection or the whole area between chosen heights.

## v0.13.0 — 2026-10-06 (stamps)

### Added
- **Stamps**: pictures used as brush shapes, either built in (mountain, mesa, crater rim, dunes, rocky ground) or loaded from a file. "Stamp once" puts a whole stamp into the ground with one click, up to a chosen height.

## v0.12.0 — 2026-10-06 (brush shapes)

### Added
- **Brush shapes and falloff** (sculpt and paint tools, like WorldPainter): circle, square, ring or ragged brushes, with a smooth, linear, dome, flat-top or peak falloff. Square brushes can be turned.

## v0.11.0 — 2026-10-06 (restore from a backup)

### Added
- **Restore from a backup** (Area tool, like WorldEdit's `//restore`): brings back the ground and objects of a selection as they were in one of the world's backups, from the editor or the game. Objects come back with all their data.

## v0.10.0 — 2026-10-06 (reset zones across the world)

### Added
- **Reset zones across the world** (map page, like MCA Selector): pick zones by biome, distance from buildings, ground edits and distance from the centre, and mark them all for the game to generate again.
- **Zone resets in live mode** (needs WorldEditorBridge 0.10.0 or newer): Apply live resets the marked zones in the running game. The game generates them again at once where players are.

### Fixed
- A zone reset also removed players' tombstones along with what they carried. Tombstones are now always kept.
- Saving twice within the same second failed because both backups got the same folder name. Each save now gets its own backup folder.

### WorldEditorBridge
- New: the plugin can now reset zones in the running game when the editor asks, and they are generated again at once where players are. Players' tombstones are always kept.

## v0.9.0 — 2026-10-06 (search the world)

### Added
- **Search the world** (map page): finds every object of a kind, every container holding an item, or every sign, portal or ward whose text matches, counted and pinned on the map. A result opens the 3D editor with that object selected.

## v0.8.0 — 2026-10-06 (object inspector)

### Added
- **Object inspector** (Select tool, `I`): shows everything an object holds in the save, by name, and lets you edit it, including chest contents (items, stacks, quality, slots), sign texts, portal tags, ward permissions and timers. An edit is one undo step and is saved or applied live like any change.

### Fixed
- Objects placed or moved in a session lost what they were copied from after the page was reloaded, so moving one again made a fresh copy (a moved chest lost its contents). They now keep their data.

## v0.7.0 — 2026-10-06 (repeat a paste)

### Added
- **Repeat a paste** (like WorldEdit's `//stack`): one click places several copies in a row, along the copy's width or depth or stacked upwards, with a gap between them. It counts as one undo step.

## v0.6.0 — 2026-10-06 (PlanBuild files)

### Added
- **PlanBuild and .vbuild files**: import a PlanBuild `.blueprint` (its terrain marks become ground) or a `.vbuild` file into the blueprint list. You can also write any blueprint back out in either format.

## v0.5.0 — 2026-10-06 (blueprints)

### Added
- **Blueprints** (Area tool → Copy & paste): save the clipboard as a named file, browse the saved ones with a picture of each, and paste them into any world. They are kept in the `blueprints` folder of the editor's data folder.

## v0.4.1 — 2026-10-05 (editor on GitHub only)

### Changed
- **Thunderstore carries only the plugin** (`Tie-WorldEditorBridge`), because Thunderstore does not host programs and rejected the Windows and Linux editor packages. The editor is downloaded from the GitHub releases page, which the plugin's mod page now points to.

## v0.4.0 — 2026-10-05 (clean-up)

### Fixed
- Leaving the world map with **Worlds** while it was still loading could raise an error on the page. This no longer happens.

### Changed
- Internal code cleanup; no change for users.
- Internal development cleanup; no change for users.

## v0.3.3 — 2026-10-05 (Thunderstore dependency)

### Changed
- **Thunderstore**: the editor packages no longer carry their own copy of the plugin. They depend on `Tie-WorldEditorBridge`, which mod managers install with them (one copy per profile), so plugin fixes no longer need a new editor package; the GitHub downloads still include it in `plugin/`.
- The start page's plugin steps now point mod manager users to WorldEditorBridge on Thunderstore.

## v0.3.2 — 2026-10-05 (smaller packages)

### Changed
- **Only what the editor needs**: the bundled Python now keeps just the files a full game-look export uses, with the same output as before. Downloads shrink from 117 to 87 MB on Linux and from 74 to 48 MB on Windows, and a few unneeded files are gone too.

### Fixed
- **Windows**: the bundled Python now includes a runtime file the game-look copy needs. A Windows without the Visual C++ runtime could not copy the game's look before.

## v0.3.1 — 2026-10-05 (Thunderstore packages)

### Added
- **The editor on Thunderstore**: `ValheimWorldEditor_Windows` and `ValheimWorldEditor_Linux`, the editor with the plugin, for r2modman and Thunderstore Mod Manager. `WorldEditorBridge` stays as the plugin alone, for servers.
- Installed by a mod manager, the editor now finds its own profile for **My game**.

### Fixed
- Linux: the game-look export now runs even when a mod manager unpacked the bundled Python without its run permission. The release also no longer holds the extra `python`/`python3` links.

## v0.3.0 — 2026-10-05 (Thunderstore)

### Added
- **WorldEditorBridge on Thunderstore**: install the live-editing plugin with r2modman or Thunderstore Mod Manager, and BepInEx comes with it.

### Changed
- **One version number** for the editor and the plugin (0.3.0, the plugin's version until now). The plugin file's own version said 0.1.0 and now says 0.3.0 too.

### WorldEditorBridge
First release on Thunderstore; from here on the plugin has the editor's version number, so use the two together.
- **Objects live**: deleted, planted, pasted and moved objects are applied in the running world, and undoing after applying takes them back in the game too.
- **Ground live**: terrain edits (sculpt, paint, roads, areas) are sent to the game and shown to every player at once.
- **World snapshot**: the editor opens the running world as the game has it now, without a save.
- Listens on `127.0.0.1:5182` only, with a random token in `BepInEx/config/local.worldeditorbridge.cfg`.


## v0.2.0 — 2026-10-05 (desktop app)

Download, unpack, double-click: no command line, no Python, nothing to install.

### Added
- **Its own window** on Windows and Linux, with an icon. It uses the system's web engine, with the browser as a fallback or with `--browser`.
- **Start page with three ways to edit**, each clearly separate:
  - **My game (live)**: finds Valheim running with the plugin (also in r2modman / Thunderstore Mod Manager profiles) and reads its token from your own game's settings. One click starts live editing, or it shows the steps that are missing (BepInEx, the plugin, starting the game).
  - **A dedicated server (live)**: the editor logs in over SSH (password or key, no `ssh` program needed) and makes its own tunnel to the plugin, while you always type the plugin token yourself. Servers are saved in `servers.cfg` after the first connection for one-click access, with the password only when "Save password" is ticked, and the server's SSH identity is remembered and a change is refused.
  - **A saved world (offline)**: lists the worlds found on the computer (Valheim's usual folders, Proton included) and recently opened ones, and lets you pick any other folder with a folder dialog.
  **Worlds** on the map page goes back to the start page.
- **Settings** for folders that are not in the usual place: the Valheim game folder, extra BepInEx folders or mod manager profiles, and world folders that are always listed. For a server there is also an optional **Server's Valheim folder**, from which the plugin's port is read, and when the plugin does not answer the editor says exactly what is missing there.
- **Automatic game look**: the editor finds Valheim in the Steam libraries and copies the game's textures and models by itself, with progress on the page. It asks for the folder when Valheim is not found and copies again after a Valheim update, keeping the copy in the per-user data folder next to the settings and a log.
- **One download per system**, each with the plugin in `plugin/` and instructions for that system.

### Changed
- The program is now called `ValheimWorldEditor`. It picks the first free port from 5180, so a second copy or another program never blocks it.
- The game-look exporter now comes with its own small Python runtime, and its texture fixes need only Pillow. The visible result is the same.

### Fixed
- Connecting to a server that does not answer gave up only after 3 minutes. The editor now checks the bridge first (at most 10 seconds) and says what is wrong: no tunnel, wrong token, or a game that does not host the world.
- The app now quits cleanly when the system asks it to (logout, shutdown), closing its window.

## v0.1.0 — 2026-10-05 (first release)

Everything below, packaged: the editor for Linux and Windows (x64), and the WorldEditorBridge server plugin (0.3.0) for live mode. Game files are not included.

### Added
- **Release packages**: the program, web page and export tool, without source code or game files.
- **One-command game export**: a single command copies the game's terrain shader and textures, map textures and the models of every kind of object from your own Valheim install (a few minutes). It replaces separate scripts that had paths of the development machine written in.

### Changed
- Models are now exported for every placeable kind (about 800 world objects plus 689 building pieces), not only the kinds one world happened to contain. The Plant tool therefore shows real models for far more kinds.

## 2026-10-05 — Placing, selecting and moving

### Added
- **Hover help on every control**: each slider, switch, button and list in the editor and on the map explains exactly what it does, and tool buttons also show what their tool does.
- **Documentation**: a README with installation for offline and live mode, a page per feature in `docs/` with screenshots, and this changelog.
- **Select inside a zone.** In the Select tool, drag on empty ground (or Alt + drag anywhere, for forests) to draw a zone and select everything shown inside it. Shift adds to the selection.
- **Place any game object.** The editor now knows every network object in the game (about 1,500 placeable kinds), so you can plant or place things the world has none of yet, such as turnips. They are created exactly like the game creates a freshly placed one.
- **Leave saplings room to grow** (Plant tool, on by default). Saplings and crops are never placed closer than their in-game grow radius to anything, and the panel reminds you when a crop needs cultivated ground.
- **Drop to surface** (Select tool, `End`): drops the selection onto the object below it, or onto the ground when there is none.
- **Move arrows** (Select tool): red X, green Y and blue Z arrows at the selection let you move along one axis only by dragging, and Ctrl snaps to 0.5 m.
- **Plant: Line, Grid and Zone modes.** Place objects every N metres along a drawn line (optionally smoothed, facing along it, with sideways wiggle), one per cell of a dragged box, or fill a freely drawn zone by scatter or by an orderly grid. Zones and grids can be turned as a whole.
- **Foldable View panel.** Click a section title (Look, Nature, Spoilers, Overlays) to fold it. Look starts folded and your choice is remembered.

### Changed
- **Rotation in 1° steps everywhere** (`,` `.` and Alt+wheel), or 15° with Shift, in Select, Plant brush, Plant zones/grids and Paste. Paste can now be turned to any angle, not only quarter turns.
- **Flatten levels to the height where the stroke starts**, instead of a fixed 35 m that made it dig like Lower on higher ground. A fixed height is still available by unticking the option or using Alt + click.
- **The Mask judges the ground as it was when the stroke started**, so raising past the mask's height limit or changing the slope no longer stops a stroke half-way. Settings that let nothing through (like a 0° maximum slope) are flagged, and an empty stroke says the Mask left everything out.
- Placing a kind whose View switch is off now switches it on, so what you placed stays visible after a reload.
- The move arrows are smaller and leave the object's middle free for a free drag.
- The editor now tells the browser to check its page files on every load, so an update is used right away instead of an older cached copy.

### Fixed
- With Game look off (or without the extracted game files) the world's objects were not loaded, so nothing could be planted, placed or saved as an object. They now always load.
- The world map no longer fails to load when the map textures are missing, and uses plain colours instead.
- Pressing a second key (End, PgUp, turn) right after a move acted on the hidden original instead of the moved copy. It now acts on the moved copy.

## 2026-10-05 — History, discard and live objects

### Added
- **History panel** (`L`): lists every change with its time. "Back to here" rolls back to any point, and "Remove" takes out a single change while keeping everything done after it.
- **Move, turn, lift, copy and paste selected objects** in the Select tool (drag, `,` `.`, PgUp/PgDn, Ctrl+C / Ctrl+V).
- **Plant preview**: see-through "ghosts" show exactly what a click will place, for every kind. `R` rolls a new layout, Alt+wheel turns it, and there is a single placement mode and foldable categories.
- **Objects live** (WorldEditorBridge 0.3.0): deleted, planted, pasted and replaced objects are applied to the running game, and undo works after applying.
- **Ground live** (WorldEditorBridge 0.2.0): "Apply live" and "Auto" send ground edits to the game.
- **Live mode** (WorldEditorBridge plugin): the editor can open the running world of a server, with its players, instead of a save on disk.

### Changed
- Copies, plants and pastes now create **fresh, independent objects**, so a copied chest is an empty chest, while moving an object keeps all of its data.
- **Discard** now undoes only the changes that are not saved or applied yet, in place, without reloading the page, so your tool, view and zoom are kept.
- The pending counter now only counts zones that really differ from their saved state, so undoing back to the start clears it.
- Objects you placed but have not saved yet are always drawn, with a green marker, whatever the View switches say.

### Fixed
- Raspberry bush leaves disappeared in the plant preview and with see-through buildings. They now show correctly.
- The brush ring and other moving overlays disappeared when zoomed in close. They now stay visible.
- A few kinds (Bush01, shrub_2 and others) were drawn as boxes because the wrong model was matched. They now show their own models.

## 2026-10-05 — World-editor tools

### Added
- **Masks** for brushes, paths, area actions and planting: by biome, height range, slope range and paint.
- **Area tool** (`B`): box or polygon selection to flatten, raise, lower, smooth, naturalize, restore or paint the ground inside, remove, select or replace objects inside, and copy and paste ground and objects (turn and mirror). It can also reset zones so the game generates them again.
- **Plant tool** (`T`): paint trees, rocks, bushes and more onto the ground with density and spacing, and Shift + drag removes the chosen kinds.
- **Measure tool** (`M`): shows distance, height difference and slope between two points, with slope colours and height lines in View.
- **Delete objects** (Select tool, `Del`), and a reworked interface with a top bar, tool rail, tool panel and View panel.

## 2026-10-05 — In-game look

### Added
- The 3D editor now draws the world like the game: the game's own terrain shader and textures, water and sky, real models for buildings, trees, rocks and bushes, and show/hide switches per kind. Spoilers such as ore and ruins are off by default.

## 2026-10-04 — First versions

### Added
- **World map** in the browser, drawn with the game's own map shader, with player buildings, painted ground and edited zones.
- **Base terrain from the seed**: a port of Valheim's world generator that matches the game bit for bit.
- **3D terrain editor** with brushes (Raise, Lower, Flatten, Smooth, Naturalize, Restore), ground paint (Dirt, Cultivate, Paved, Clear), the Path tool, undo/redo, and the game's ±8 m limit.
- **Saving back to the world**: a new save number with a full backup and a read-back check.
- **CrossplayJoinCodeFix** (separate plugin, own repository): keeps a crossplay server from appearing offline when PlayFab answers the join-code check incompletely.
