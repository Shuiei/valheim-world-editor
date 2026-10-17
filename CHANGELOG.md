# Changelog

All notable changes to the Valheim world editor and its WorldEditorBridge plugin, which share one
version number. Newest first. Changes to the plugin are under **WorldEditorBridge** in each version;
the plugin is unchanged in versions without that part.

## v1.45.62 — unreleased

### Fixed
- **One unusual bundle (a mod's, damaged, or laid out otherwise) left every mod model undrawn.** The index of prefabs
  read at the start skipped a file that is not a bundle, but a bundle without an assets file first, or whose table
  of contents could not be read, was an exception that ended the index, so every later lookup failed again and no
  mod model was drawn. Such a bundle is now skipped and said in the log, closed like the others, and the rest are
  indexed.

## v1.45.61 — unreleased

### Fixed
- **The editor could fail to start, or miss pieces and models, depending on where the game's scripts are in its
  bundles.** The bundles are read one by one at the start, and the few that hold the game's scripts are kept open
  so each class is looked up once. When the pass reached a bundle already kept open, it opened it a second time and
  closed it after its read; the game's file reader gives the same bundle both times, so the kept one was closed
  too, and the next class looked up in it failed (an exception that stopped the start) or was not found. A bundle
  open already is now given again and stays open until its last reader is done; a file handle leaked by each second
  open goes with it.

## v1.45.60 — unreleased

### Fixed
- **On Windows, a mod updated while the editor ran left its models missing until the editor was restarted.** The
  mods' bundles are copied into the editor's data folder, and the old copies are deleted when a mod changes; the
  reader of the old copies still held their files open, which Windows refuses to delete, so the copy failed and no
  mod model was drawn for the rest of the session. The old reader now lets go of its files before the copies are
  replaced.

## v1.45.59 — unreleased

### Fixed
- **One damaged object ended a world search, and a damaged item stand broke the inspector.** Search world, the
  inspector's chest and stand reads and Claude's get_object skipped an object only when its bytes were short; a bad
  count or length prefix in them (what damaged saves have) was an exception that ended the whole search or the
  inspector. Every reader of one object now skips what cannot be read, whichever way it is damaged, and goes on with
  the rest.

## v1.45.58 — unreleased

### Fixed
- **A chest could be given something that is not an item, which the game then threw away.** The inspector and
  Claude's set_contents checked a container's items against every prefab of the game, so "Troll" or "piece_chest"
  went into a chest and was saved; the game drops such an entry when it loads the chest. Only the game's items are
  taken now; anything else is refused with its name.

## v1.45.57 — unreleased

### Fixed
- **Exporting To Homestead, importing a blueprint or generating a dungeon with a long name could hang the editor.**
  A blueprint's file name keeps 64 characters of its name, so for a name that long every "name (2)", "name (3)"…
  tried when the name was taken already gave the same file, and the search for a free name never ended. The name is
  now cut so that its number stays in the file name.

## v1.45.56 — unreleased

### Fixed
- **Claude's set_control "click" on a switch, toggle or radio button changed nothing.** The click only raised the
  Click event, which skips what a real press does first: the toggle itself (and a button's Command). So the switch kept
  its value, its handlers ran against the old one and Claude was told it worked. A click now presses the control as a
  pointer does: the toggle changes, then its handlers and Command run.

## v1.45.55 — unreleased

### Fixed
- **Claude Desktop lost the editor's MCP server on a batch request or a tool that ran too long.** The --mcp-stdio
  relay ended on a line of JSON that is not one message (a batch array, a bare value), on a method that is not a
  string, and when the editor gave no answer within 5 minutes, so Claude Desktop showed the server disconnected. Such
  a line is now answered with a JSON-RPC error (a request that timed out too) and the relay goes on until its input
  ends.

## v1.45.54 — unreleased

### Fixed
- **Claude's object ids could hit the wrong objects after another area was opened or the world read again.** The ids
  Claude's tools give are indices into the open area's objects, and they were taken as they were once another area
  was open, the same one opened again, or the world read again after a save: remove_objects, move_objects,
  set_object_data and the others then changed whatever objects had those indices. The tools now remember which area's
  objects the ids were given for and refuse ids from another, or from before the world was read again, until
  objects are listed again.

## v1.45.53 — unreleased

### Fixed
- **Claude could not open or add a blueprint whose file was not named after it.** list_blueprints gives each
  blueprint's name from its #Name: header, but open_workshop and add_blueprint looked for a file named after that
  name, so a numbered copy, a downloaded blueprint or one the game saved under another file name was "not in the
  library". The name is now looked up among the library's entries first, the file name after.

## v1.45.52 — unreleased

### Fixed
- **A dungeon Claude generated was not one of Claude's changes, and left a file in the blueprint Library.** Its step
  was named "Dungeon: generated …" (in a world) or "Added …" (on the Workshop's plot), so Take back Claude's changes
  kept it, and on the plot its blueprint was written into the user's Library, which Claude is not to write. The step
  is now "Claude: generated dungeon …", and on the plot the dungeon is added straight from its blueprint's text,
  with no file written.

## v1.45.51 — unreleased

### Fixed
- **A blueprint Claude's add_blueprint refused, because a piece would leave the area, took the place of what the user
  could redo.** The pieces were put down as a step and the step undone at once, which cleared the redo list (and
  could drop the oldest undo step), and left the refused blueprint there for the next Ctrl+Y. What the blueprint
  would add is now worked out first, and nothing is put down when it is refused.

## v1.45.50 — unreleased

### Fixed
- **A piece dragged far did not snap to the building where it was dropped.** The snap points of the pieces around
  were gathered once per move, within 60 m of where the move started, so a piece dragged more than about 60 m to
  another building never snapped to it. They are now gathered again once the selection has moved more than 30 m
  from where they were gathered.

## v1.45.49 — unreleased

### Fixed
- **"Move there" moved the selection a little on every axis, even the ones left alone.** The X, Y, Z and Turn boxes
  show where the selection is rounded to a centimetre and a tenth of a degree, and Move there took all four as the
  place to reach, so changing only Y (or nothing) replaced every selected object by a copy shifted by up to 5 mm and
  turned by up to 0.05° about the middle: a new undo step, new ids, and a destroy and create on a live server. Move
  there now moves only along the axes whose box was changed, by the difference from the exact place (however
  small); with nothing changed it does nothing.

## v1.45.48 — unreleased

### Fixed
- **Replacing a ship or another linked group's root left its parts behind.** Replace (the Area tool's, and the
  Select tool's "Replace the selection") removed only the objects given, unlike Remove, so replacing a vehicle's
  root left its parts in the save with nothing to hang on. Replace now takes a root's parts away with it, in the
  same undo step, and says how many went; a part replaced on its own still goes alone.

## v1.45.47 — unreleased

### Fixed
- **A heightmap read but not yet put could land in another area.** The picture read by Import was fitted to the
  selection and the area open at the time, as grid points of that ground, but it survived a change of selection or
  the opening of another area, so "Put it into the ground" then wrote it into the wrong points of another ground.
  The picture is now dropped when the selection changes, and refused (with a message) when another area is open.

## v1.45.46 — unreleased

### Fixed
- **Replace's "with" list kept the first world's kinds after another world was opened.** The Area tool worked the
  list out once per window, but the kinds a world can make include its own objects (its templates), so after opening
  another world the list still offered the first world's modded kinds, which the other world cannot copy, and lacked
  its own. The list is now worked out again for another world or when the game's data or the mods change, keeping
  the chosen kind when the new world offers it too.

## v1.45.45 — unreleased

### Fixed
- **Reset zones could say "Select an area first" or miss zones, depending on the Soft slider.** The zones under the
  selection were taken from the soft-edge weights, keeping only points weighing at least one half: with Soft at 20 m
  and a 15 m wide selection no point weighed that much, so Reset found no zone, and a zone covered only by the outer
  half of the soft edge was left out. The zones are now those with a grid point inside the outline, whatever Soft is.

## v1.45.44 — unreleased

### Fixed
- **Removing a move from the history could leave two copies of the object.** A move replaces its object by a copy.
  Removing that move from the history (Remove, or "Take back Claude's changes") after a later step had moved the
  copy again brought the original back while the later copy stayed; removing a Place whose object was moved since
  left the moved copy. A change whose added objects a later step moved or deleted is now refused with a message
  ("undo back to it instead"), and "Take back Claude's changes" says how many were left for that reason.

## v1.45.43 — unreleased

### Fixed
- **A hand-made language file could stop the editor from starting.** A file in the data folder's `lang` folder whose
  root is not an object (`[]`), or that gives a key twice, threw while the languages were being read at start-up,
  before the window existed, even for a language that was never chosen. A key given twice now takes the last text,
  and a file that is not one object of texts is logged and skipped like one that is not JSON.

## v1.45.42 — unreleased

### Fixed
- **A translation of "Free" or "Cost unknown" with a brace in it broke the Build panel.** A text without places for
  values is taken from a language file as it is, braces and all, since it is never formatted; but the cost line asked
  for it with an empty list of values and formatted it anyway, which threw and broke the Build panel, the blueprint
  list and Claude's piece search. A text asked for with no values is now shown as it is.

## v1.45.41 — unreleased

### Fixed
- **Mirrored pastes tilted pieces the wrong way.** Pasting with Mirror on reversed the pitch of every object as
  well as its yaw and roll, so a beam sloping nose-down came out nose-up and tilted trees leaned the other way.
  Mirroring east to west now keeps the pitch; the same went for mirrored dungeon pastes and the paste ghost.

## v1.45.40 — unreleased

### Fixed
- **A mod extension offered by two mods ran everything twice.** When two mods ship the same extension DLL and both
  are allowed, the file is loaded once but was put in use once per mod: its shapes and ground cuts were drawn
  twice, its inspector text and pasted-copy rewrites ran twice (the second rewrite working on already rewritten
  data), and a hook that failed turned off only one of the two in Settings. Each loaded extension's hooks now run
  once, and a failure shows on every mod offering it.

## v1.45.39 — unreleased

### Fixed
- **The start page could say "Reading the game's files" for ever, or "ready" when it was not.** When the editor
  could not take one kind of the game's data (after a game update changed it), the error escaped the reading: the
  start page stayed on its progress bar, the kinds before it used the new data and those after it the old, and no
  failure was logged. When the game's files were already read, a failure to get its data still showed "ready". The
  editor now logs what failed, counts none of the data as had (it is taken again next time), and the start page says
  some of the game's data could not be read; "ready" is said only once the game data is in use.

## v1.45.38 — unreleased

### Fixed
- **A cache that could not be written stopped every world from opening.** At its start the editor reads what it needs
  from the game and keeps it in its data folder for the next start. When keeping it failed (a full disk, a read-only
  data folder, two editors writing at once), the data it had just read was thrown away too: no world opened, the log
  said the game could not be read, and each try read the whole game again. On the New world page the bosses and
  traders were lost the same way. A cache that cannot be written is now only logged, and the data read is used.

## v1.45.37 — unreleased

### Fixed
- **A world map that failed to build said "Building the world map…" for ever.** The map is worked out in the
  background when a world opens. If that failed (a damaged world file, for example), the error was lost: the map
  stayed blank, its status kept saying it was building, and nothing went to the log. The map now says it could not
  be built and why, and the log has the details.

## v1.45.36 — unreleased

### Fixed
- **A live world's map could show another world's players.** The map asks the game for its players every 5 seconds,
  without waiting for the last answer: a game that hung got a new question every 5 seconds (each waiting up to
  3 minutes), and the answers changed the players' line in whatever order they came. An answer that came after
  another world was shown put the old world's players on the new map. The map now asks a world's game again only once it
  has answered (also when that world is shown again meanwhile), and drops an answer for a world no longer shown.

## v1.45.35 — unreleased

### Fixed
- **The map could pin the results of an older search.** A search started while another was still running (a slow
  item search, then a switch to objects) could end first, and the older one's results then replaced it: the wrong
  hits were pinned, and picking one opened it the wrong way. A search still running when another world was shown
  pinned the old world's hits on the new map, and the same happened when the world was read again (Discard, Reload)
  or saved during the search. Only the latest search on the world as shown is pinned now; one dropped because the
  world changed says to search again.

## v1.45.34 — unreleased

### Fixed
- **Claude's screenshots could show the wrong view.** Claude can ask for several screenshots at once. Each set the
  camera and then waited for its picture, so another call could move the camera in between: pictures came from the
  other call's camera, and four views could be labelled with the wrong sides. Tools that set the camera or take a
  picture now take turns.

## v1.45.33 — unreleased

### Fixed
- **A dungeon Claude generated could land in the wrong place, or go live.** Claude's generate_dungeon checked that an
  area was open and that Auto was off only before generating, which takes a moment. Opening another area or the
  Workshop meanwhile put the dungeon there, closing the area crashed the tool, and turning Auto on sent the dungeon
  straight to the game. It is now checked again before placing: nothing is placed and Claude is told why.
- **A dungeon added to the Workshop's plot could land on another plot.** Generate dungeon (from the Dungeon panel or
  Claude) writes its blueprint first; a plot opened meanwhile got the dungeon. It is now added only to the plot it
  was made for; otherwise nothing is placed and the status bar says so (the blueprint stays in the Library).

## v1.45.32 — unreleased

### Fixed
- **Errors on the map went unseen.** Opening an area that failed to load (or was dropped because the map was shown
  while it loaded), or a Discard or Reload from the map that failed, said why only in the 3D editor's status bar,
  which the map does not show: the "Loading…" veil flashed and nothing else happened, and the message turned up
  later, out of context, when an area was opened. The advice after making a new world did the same. On the map these
  now show in a dialog too (not for a world left meanwhile). The window's message dialogs are titled "Valheim World
  Editor", as the map's are, instead of "Save" whatever they said.

## v1.45.31 — unreleased

### Fixed
- **After a failed Discard or Reload in the 3D editor, every tool silently did nothing.** Discard (reading the world
  again) and Reload close the area and open it again. When opening it again failed, the editor stayed on the old
  area with no area open behind it, so tools, Save and Undo did nothing, and the status bar said "Discarded: the
  world was read again." over the error. The map now opens instead, and a dialog says the area could not be opened
  again and why (also when another area asked for meanwhile, by an arrow move, fails too); the old area is no longer
  the world's, so its history is not kept after the world is read.

## v1.45.30 — unreleased

### Fixed
- **A change finished after going to the map could not be undone.** Going to the map handed the area's history to
  the world but left the area open behind it, so a script, a Claude change or Grow still running finished in it: the
  change went into the world but its undo step stayed in the closed area, and the history found on reopening lacked
  it. Going to the map (or opening another area) now closes the area, so such work changes nothing and says so (a
  script: "The area was closed while the script ran"); an area that fails to open leaves the one shown open as
  before, unless the map or another area was asked for (or the world left) while it loaded; an area that finishes
  loading after that is not shown over them.

## v1.45.29 — unreleased

### Fixed
- **Trees grown on a mountain could float, be buried or be lost.** After placing a mountain with Grow on, the editor
  works out the biome's trees and rocks for a moment before putting them in. Anything done meanwhile was not noticed:
  after an undo or another stroke the trees went in for ground that was no longer there, and when Follow grew the
  area they went into the old area and were lost, though the message said they grew. When the area changed
  meanwhile, nothing grows now and the message says why.

## v1.45.28 — unreleased

### Fixed
- **Keys still worked while a world was being saved from the 3D editor.** Saving only greyed out the Save button: Ctrl+S
  asked to save again and could start a second write, and Ctrl+Z, Follow or the save bar froze the editor until the
  save ended. Saving now shows the same "Saving…" veil as the map, the editor's keys wait while it is up, and Follow
  waits too.

## v1.45.27 — unreleased

### Fixed
- **Pressing Enter again during an Area action could do it twice.** Reset zones (while its question was open),
  Regrow nature (while the game's vegetation was worked out) and Restore from a backup (while the backup was read)
  wait before they change anything, and another Enter or click meanwhile started the same action again: regrown
  trees or restored objects were added twice. When another area was opened meanwhile, the change still went to the
  old one. Apply is now greyed out while an action runs, and an action whose area changed meanwhile applies nothing
  and says so.

## v1.45.26 — unreleased

### Fixed
- **Choosing an SSH key in a format the editor cannot read could crash it.** Connecting with a key file that the SSH
  library refuses in some ways (an old DSA key, a damaged or cut-short key) stopped the editor with an unexpected
  error instead of saying "The key file could not be read", and such a key in your .ssh folder did the same instead
  of being skipped. Every way a key file can be refused now gives that message (or skips it), and the key files
  read are closed once connecting is over.

## v1.45.25 — unreleased

### Fixed
- **Clicking Connect twice could open two connections to the server.** Connecting to a server (the form, a saved
  server, its Home tile, or a bridge address) takes a few seconds, and nothing stopped a second click meanwhile: both
  opened an SSH tunnel and a world, the first tunnel stayed open unseen until the editor closed, and a failed attempt
  could close the other one's tunnel. A click while connecting now does nothing, and tunnels are made one at a time.
  Quitting or going back to Home while a connection is being made now shuts that tunnel as soon as it connects,
  instead of leaving it open until the editor exits.

## v1.45.24 — unreleased

### Fixed
- **The dungeon list could fail on one damaged dungeon, and gave wrong room counts in older worlds.** Listing a
  world's dungeons read each dungeon's room list. A dungeon whose data could not be read in some ways (a bad text
  length, a world file that could not be read) stopped the whole list, and every dungeon was read in the newest
  save layout, so in worlds saved before chunk files their room counts were wrong. Each dungeon is now read in its
  own world version, and one that cannot be read is listed with no rooms, with a line in the log saying which and why.

## v1.45.23 — unreleased

### Fixed
- **One damaged ground record made the whole world fail to open.** Each zone's ground edits are saved as packed data
  on one object. When that data was damaged or cut short, opening the world stopped with an error, although the game
  only loses that zone's ground edits. The damaged object is now read as an ordinary object and kept as it is, a line
  in the log says where it is, and the rest of the world opens. Damaged data claiming more ground points than a zone
  can have is caught the same way, before the editor sets aside memory for them.

## v1.45.22 — unreleased

### Fixed
- **Live, an object the game listed twice could be counted twice or leave a ghost copy behind.** When the game's list
  of a zone's objects held the same object twice, the editor read it twice: an object it knew went into its lists
  twice and its builder's piece count went up by two, and an object new to it got two entries, one of which never
  went away when the object was removed in the game. Each object is now taken once, with its last data.

## v1.45.21 — unreleased

### Fixed
- **Saving could stop with an error instead of saying why it could not save.** Before writing, a save copies an
  existing terrain object for new ground and the objects new ones are made from. When that failed (the first
  terrain object in a layout the editor does not copy, a world file that could not be read, an object that did not
  read), the error escaped the save and showed as an unexpected error. These now come back as a save that did not
  happen, with the reason, and nothing written; new ground also copies the first terrain object it can use, not
  just the first one.

## v1.45.20 — unreleased

### Fixed
- **A power cut just after saving could leave a broken save that the game trusts.** Saving copies the world's data
  and metadata files (.db2 and .fwl2) to the new save number, then writes the marker that tells the game the save
  is complete, flushed to disk, and removes the previous save. The two copies were not flushed, so after a power
  cut the marker could be on disk next to an empty or cut-short .db2 or .fwl2, with the previous save already gone.
  The copies are now flushed to disk before the marker is written, as the other files of the save already were.
  On Linux and macOS the save folder is flushed too (a file's name is only safe on disk once its folder is; on
  macOS through the drive's cache, as .NET already flushes the files there), before the marker and again before the
  previous save is removed.

## v1.45.19 — unreleased

### Fixed
- **An Apply live that failed partway said nothing had been applied.** The ground goes to the game first, then the
  objects, then zone resets. When a later part failed, the editor only said "Could not apply live", although the
  ground was in the game: the steps that had gone in stayed unapplied in the history, so Discard would undo them in
  the editor while the game kept them. It now says what went in and what failed, and marks as applied the steps the
  game has all of, up to the first one it lacks; Apply live again sends the rest.

## v1.45.18 — unreleased

### Fixed
- **A save or Apply live that failed still turned No limit ground into ground discs and cleared the undo history.**
  Saving or applying first turns ground past the game's ±8 m into ground discs and ordinary changes. When the save
  then failed (a full disk) or the game refused the ground, that was kept: the area was read again, its undo history
  went, and the No limit ground was now discs that nothing had written. When nothing was written or sent, the No
  limit ground is now put back as it was, and the area and its history stay. When the game may have taken the
  ground (no answer, or it failed partway), the discs stay and the area is read again, as before.

## v1.45.17 — unreleased

### Fixed
- **Objects you had just applied live could vanish from the editor as if players had removed them.** The editor
  reads what changed in the game every few seconds. A read that started before an Apply live, and came back once it
  was done, did not have the objects the apply had just made, so they counted as removed in the game: the editor
  dropped them while they stayed in the game, and undo could no longer reach them. Such a read, or one taken while
  an Apply live was under way, is now dropped and the zone read again.

## v1.45.16 — unreleased

### Fixed
- **A dungeon placed on a live world could be built from the wrong object's data.** On a live world, objects the
  game sent since the snapshot (read when you follow the game) keep their data in their own read, not in the
  snapshot. Placing a dungeon copied its objects from the snapshot whatever their model was, so a model read since
  gave the dungeon another object's bytes, or failed. It now copies each model from where its data is, as saving,
  Apply live, search and the inspector already did; they now share one reader.

## v1.45.15 — unreleased

### Fixed
- **Other users of the computer could read Claude's token.** The settings file, which keeps the token Claude
  connects with when **Allow Claude to connect** is on, was written with the default permissions: on Linux and macOS,
  with a home folder others can enter, they could read the token and use the editor's Claude tools. It is now
  written so only you can read it, as the saved servers file already was.

## v1.45.14 — unreleased

### Fixed
- **In another language, Claude could use the Blueprints buttons that write or delete files.** Claude is kept from
  the window's buttons that save, write or delete the user's files. The Blueprints rows' buttons (**Details**,
  **.vbuild**, **To Homestead**, **Delete**) had no tip key, so they were recognised by their English label only:
  shown in another language, they were no longer blocked. And Claude's ids for controls without a tip key were made
  from the label as shown, so they changed with the language, and letters outside a–z were dropped. These buttons
  now have tip keys and are blocked by key, and the other ids and label checks use the label's English text. The
  Select tool's **Delete** is blocked by its key too, so a translated text that reads "Delete" cannot unblock it.

## v1.45.13 — unreleased

### Fixed
- **Editing a saved server could send another server's password and token to it.** **edit** filled the address,
  user, key, folder and name, but left the password, token, key passphrase, plugin port and **Save password** as
  they were: a password and token typed for one server were sent to the edited one on Connect, and a saved password
  was dropped from the file because **Save password** stayed unticked. **edit** now fills the whole form from the
  saved server: its own saved password and token (empty when not saved), no passphrase or port, and **Save
  password** ticked when its password was saved. The password is sent as typed or saved, spaces at its ends
  included.

## v1.45.12 — unreleased

### Changed
- **The Area tool's object actions no longer stall on a big area with the Mask on.** Remove, Select, Restore from
  backup and Regrow nature took a fresh snapshot of the Mask for every object judged, and each snapshot copies the
  whole ground's heights (and paint, with a paint rule): on a 17 x 17 area with thousands of objects that was
  thousands of copies of several megabytes, while the scene's objects were locked. The Mask is now taken once per
  action; what is picked is unchanged.

## v1.45.11 — unreleased

### Fixed
- **Restore from backup with the Mask on doubled objects.** The objects in the backup were picked by the selection
  and the kinds chosen only, those in the area also by the Mask: an unchanged tree or rock where the Mask lets
  nothing through was found in the backup but not in the area, so it was brought back on top of itself. Both
  sides are now picked alike, so objects where the Mask lets nothing through are left as they are.

## v1.45.10 — unreleased

### Fixed
- **The map's zone filter could judge one world's zones on another world's counts.** The counts behind **Show
  matching** and **Mark for reset…** are read in the background; when another world was opened meanwhile, the
  first world's counts were kept for the new one, so its matches, the number in the question and later
  **Show matching** came from the wrong world. And a **Mark for reset…** question answered after switching marked
  the zones in the world left behind. Counts read before the world was shown again (another world, or the same one
  after a save or a discard) are dropped, and so is the marking when the world was shown again while asking.

## v1.45.9 — unreleased

### Fixed
- **A brush stroke made while Apply live was answering could lose ground or undo wrongly.** With **Auto** on, a
  stroke starts an apply when it ends; a new stroke begun before the game answered had its ground so far
  overwritten when the answer came, and the history kept only the rest of it. When that apply also read the area
  again (zone resets, No limit ground), the history was cleared in the middle of the stroke and its undo wrote old
  ground back. The ground now takes the applied edits when the stroke ends, and a stroke under an apply that
  reads the area again is dropped, with a message to draw it again.

## v1.45.8 — unreleased

### Fixed
- **Some script calls never ended, even with Stop.** `Area.Points(0)`, a negative step, or one too small to move
  along the area (`Area.Points(0.0001f)` far from the world's middle) gave the same point for ever, and Stop only
  acted at the start of a row, so a query such as `Area.Points(0).Count()` could not be stopped. `Ground.Shape`
  and `Ground.Mountain` with a radius beyond about 16,000 km counted with a float that stopped growing, so they
  never ended either. Such a step is now an error, the rows and points are counted with whole numbers, only the
  part of a shape within the area is gone through, and Stop acts at every point of `Area.Points()`.

## v1.45.7 — unreleased

### Fixed
- **A script with an endless loop of its own could not be stopped.** **Stop** and the 2-minute limit only ended a
  script when it next called `Area.Points()`, `Ground.Shape` or `Ground.Mountain`: one stuck in a `while (true)` or
  a long `for` loop of plain arithmetic, `Ground.Set` and `Ground.Height` ran on, kept one core busy, and every
  later **Run** and Claude's `run_script` answered "A script is already running" until the editor was restarted.
  Every loop of a script's own (`for`, `foreach`, `while`, `do`, `goto`) now checks for Stop at each turn; the
  script's line numbers in error messages are unchanged.

## v1.45.6 — unreleased

### Fixed
- **Claude's build_floor, build_walls and build_roof could freeze the editor.** They took any rectangle: one
  thousands of metres across read the ground at every metre under it and laid out millions of pieces on the
  window's thread, freezing the editor for minutes, and one far enough out (beyond about 16,000 km) never finished.
  The rectangle must now be within the open area, and a layout of more than 5000 pieces (walls thousands of rows
  high among them) is refused before it is laid out, and before the ground under it is read, with a message to
  build it in parts.

## v1.45.5 — unreleased

### Fixed
- **Moving objects past the area's edge sank them about 1000 m, or threw others up.** With **Put on ground** on, the
  Select tool's move (dragging, **Move there**, **Move by**) took the ground past the edge of the open area, where
  the editor has none, as 1000 m below sea level: a moved object or block of building pieces went there, or the
  rest of the selection was lifted by as much to make up for it, and the save wrote those heights. Past the edge an
  object now keeps its own height, and only the ground inside the area places the rest.

## v1.45.4 — unreleased

### Fixed
- **Claude could change a live world with Auto on through the editor's controls.** Every Claude tool that changes
  the world is refused while the world is live with **Auto** on, so its changes wait for the user, but `set_control`
  was not: pressing a tool's **Apply**, an Area action or a script's **Run** through it changed the world, and Auto
  sent the change to the game at once. While the world is live with Auto on, `set_control` now only sets the View
  panel's controls, and `list_controls` says why the others are blocked.

## v1.45.3 — unreleased

### Fixed
- **Applying live again after a lost answer could make every new object twice.** When the game placed the objects but
  its answer never came back (the SSH tunnel dropped, the connection was reset, or a big apply ran past the editor's
  3-minute wait), the editor recorded nothing and the next **Apply live** sent the same objects again, doubling them in
  the world. Each objects call now goes with an id; a call left without an answer is sent again first under it, and the
  plugin answers with what it did the first time instead of doing it again. The changes made since follow in a call of
  their own. Needs the plugin from this version; with an older one the editor sends as before.

### WorldEditorBridge
- `/objects` takes an `X-Call-Id` header: a call already done under that id (one of the last 16) is answered again
  with what was done, not done twice. `/status` lists `"once"` among its features; editors only send ids to a plugin
  that lists it. `/status` also gives `"run"`, a new id each time the game starts: a call whose answer was lost is not
  sent again to a game started since (it no longer knows what it did); the apply fails until the world is reloaded.

## v1.45.2 — unreleased

### Fixed
- **Live zone resets were sent again after the world failed to load back.** When **Apply live** reset zones in the
  game but reading the world back from it then failed (a timeout, a dropped tunnel), the apply was reported as failed
  and the resets stayed pending, so the next apply reset the same zones again and wiped whatever players had built
  there since. The resets are now taken off as soon as the game has done them, and a failed read-back is reported as
  applied, with the world not read again.

## v1.45.1 — unreleased

### Fixed
- **Undoing a second Reset zones on the same zones kept the new options.** A zone already marked for reset and marked
  again with other options (say, without **Keep buildings**) stayed marked with the new options after Ctrl+Z, a redo
  or removing the step in History, so the next save could wipe the buildings the first marking kept. The step only
  remembered whether the zone was marked before, not how. It now takes the old marking off and puts the new one on,
  and undo, redo and removal set each side's options back; the history file keeps both.

## v1.45.0 — unreleased

### Added
- **The editor can be translated.** Its texts move into language catalogues (`Core/Lang/en.json`, English); a
  catalogue dropped in the data folder's `lang` folder (`fr.json`, `pt-BR.json`…) translates the editor, English
  showing where it has no text yet. **Settings › Language** chooses the language, by default the computer's when
  there is a catalogue of it. Every hover tip (but the Inspector's) and Settings come from the catalogue so far; the other texts follow.
  How to translate: [docs/translating.md](docs/translating.md).

## v1.44.0 — 2026-10-15

### Added
- **A Mac build (experimental), for Apple silicon** (macOS 12 or later): `ValheimWorldEditor-<version>-macos-arm64-experimental.zip`
  on the releases page, ValheimWorldEditor.app signed ad hoc (macOS asks once: **Open Anyway** in Privacy & Security;
  its README says how). Not tried on a Mac yet. The editor finds Valheim inside `valheim.app` in Steam's libraries
  (`~/Library/Application Support/Steam`), the worlds in `~/Library/Application Support/IronGate/Valheim`, the game's
  log in `~/Library/Logs/IronGate/Valheim` and r2modman's profiles; **Cmd** works as **Ctrl** for clicks in the 3D view
  and Script's Ctrl+Enter, as it already did for the shortcuts. Intel Macs are not supported yet.

## v1.43.0 — 2026-10-14

### Changed
- **Generated dungeons read more like buildings.** Side rooms grow in wings of their kin (a kitchen by the feast
  hall, cells by the guard room, an ossuary by the burial chamber, a forge by the workshop); a room's purpose sets
  its light (grand rooms closest lit, the dead's and the cells dimmest); each level down holds a fifth more of the top level's loot
  and foes; a corridor's niche now and then hides a foe; dvergr open doorways carry the dvergr arch.

## v1.42.0 — 2026-10-14

### Changed
- **Generated dungeons vary far more.** Rooms were all boxes entered in the middle of a wall, each kind furnished one
  way, torches along every wall. Now:
  - **Room shapes**: crossings, shrines, tombs and arenas with their corners notched (an octagon's feel), chapels,
    halls and throne rooms ending in an apse, store rooms, quarters, barracks and the like with a corner cut away or
    a neck at the door. Walls, beams, the plan's map and the furnishing follow the shape; twins mirror it.
  - **Bent corridors**: a corridor may run ahead, turn, and enter its room from the side (often in catacombs,
    warrens, holds and ruins; seldom in a temple). Some rooms are entered on their long side.
  - **Two to four furnishings per kind of room**, picked by the seed: a mead hall, a hall of the dead, a bare hall of
    fallen pillars; a chapel's raised altar in a ring of braziers or a nave of rugs and candles; a tomb on a dais;
    bedrolls round a fire; stock stacked in a corner with a cart; shelves in aisles; a scriptorium; a war room; the
    boss enthroned over heaps of skulls; a drill room; a dolmen shrine; and more. New props: cobwebs in the corners,
    chains and hooks from the roofs, bedrolls, weapon stands, training dummies, an archery target, dolmens, candles
    (where monsters do not come back), a cart, a bath, a round table.
  - **Lighting styles** per room: standing torches, wall sconces, or braziers in the corners; corridors likewise.
  - **Floors**: in a stone or grausten dungeon, grand rooms get black marble tiles and lived-in rooms wooden boards,
    half the time (Furniture at Furnished or Rich). Stone pieces stand on the marble through them, as the game's
    support asks.
  - **Built up inside**: in stone and grausten dungeons a great hall, throne room or chapel may carry a loft over
    the way in (a deck on posts 4 m up, a rail, stairs up one side, braziers and a sentinel on it); arenas and
    shrines may be sunk a metre below ledges round the walls; a burial chamber may hold a vault, an inner chamber
    with the sarcophagus inside. Alcoves off rooms and niches off corridors are furnished (a sarcophagus across, a
    statue between candles, a bed in its bay, a shelf and crates; a single piece in a niche).
  - **Clusters**: beside its main feature a room gets one or two groups of things that belong together in its
    corners: a camp, a meal, a scribe's desk, stores, an armoury, an altar, bones, a workbench.
  - **Hero pieces**: big rooms may get one piece that stands once in the whole dungeon: a dolmen, a fallen giant's
    statue in parts, a grave slab, a giant's skull or ribs, an altar of crystal, a cart.
  - **Corridor kinds**: a gate midway under a band of wall, a half collapse, bones along both walls, a runner, vines,
    sharpened stakes. **Dead ends**: corridors that lead nowhere but a cache (the biome's chest, loot, sometimes a
    guard). Catacombs and prisons keep some corridors dark between lit rooms.
  - **Pacing**: a few rooms stand nearly bare (one piece, a light); clutter never repeats the same thing twice
    running, and rooms differ in how tidy they are.
  - **The whole of the game's prefabs**, measured through the editor's models (a `bounds` driver command): each
    biome's own growths (vines, roots, surtling cores; guck sacks, tar, muddy scrap piles; icicles, ice shards,
    snowdrifts, crystals, hung deer; seeker creep, eggs, royal jelly, black cores; charred skulls, wall spikes,
    gems in the walls; frost cores, frozen dead), its own lights, several kinds of chest, the real crafting
    stations in kitchens, forges and workshops, measured hero pieces (standing and memorial stones, a wisp
    fountain, an eternal pyre, a sled, a crane), biome corridor kinds (mud, barricade, creep,
    icicles, spikes, roots, traps), windows in Ashlands walls, Ashlands and mountain gates. Props that stood at
    the wrong height (the fallen statue's parts floated metres up) now stand by their measured feet.
  - Lights and decay draw from random streams of their own, so turning Light or Ruin changes nothing else about a
    dungeon. The tests' copies of the game's data gain the new props.

## v1.41.0 — 2026-10-14

### Added
- **A placed dungeon gets the entrance of its kind on the ground.** **Place in the world** now puts the game's own
  dungeon entrance for the biome (a burial chamber's mound, a sunken crypt's door, a Frost Cave's mouth, an infested
  mine's, a Morgen hole) on the ground at the middle of the view, facing you, and the dungeon 5000 m above it, laid out
  where the game lays that entrance's own (a Frost Cave's at the middle of the zone, as the game does). Its door leads
  up and the door up there leads back out, as in the game's dungeons. Where the biome has no dungeon of its own (the
  Plains), the portal pair stays. One dungeon fits over a zone: with one there already, nothing is placed and the
  editor says so.
- **Go to the dungeon and Go to the entrance.** The view no longer jumps 5000 m up when a dungeon is placed: it stays
  at the entrance, and two buttons under Place in the world take it up into the dungeon and back down. The Dungeon
  panel's **Go to entrance** does the same for any dungeon of the area, next to Go inside.
- **Up to 16 levels.** Levels went up to 4; the stairs go down as far as 16 now.
- **Dungeon blueprints.** A blueprint can be a dungeon: switch **A dungeon** on when saving it in the Workshop (or in
  the Blueprints tab's Details; it is the tag `dungeon`). Pasted into a world from the Blueprints tab, it goes 5000 m up
  behind the entrance of the biome's dungeon where you click, as a generated dungeon does. A portal in the building
  marks where players arrive and the way they face (the entrance's door out opens there); generated dungeons opened
  in the Workshop carry that portal and the tag already.
- **Claude's `generate_dungeon`** places the entrance the same way, and takes up to 16 levels.

### Changed
- The game-data scan reads each location's teleport doors and interior anchor (the tests' copy of `dungeon-rooms.json`
  is made again, and keeps the Frost Cave's entrance too).

## v1.40.0 — 2026-10-14

### Added
- **Moved objects stay the same objects in live mode.** An object moved or turned in the editor (the Select tool's
  move, or a Cut then pasted) moves in the running game under its own id when nothing else about it changed, instead
  of being taken away and made again. Mods that keep track of objects while the game runs keep knowing it: a chest's
  label sign from a sign mod is no longer doubled after a live move, and a chest keeps what is in it as the game has
  it. Undo after applying moves it back. When a player has both its old and new place loaded (a player's game would
  not show a standing object moving), it is made again as before, and the message after Apply live says how many.
  Docs: [Moving things](docs/live-mode.md#moving-things).

### WorldEditorBridge
- `/objects` takes objects to move: each moves under its own ZDOID (sent to every player at once), or is made again
  at its new place where a player has both places loaded. `/status` lists `"features":["move"]`; editors only send
  moves to a plugin that lists it. Mods' listeners get the moves in `AppliedChanges.Moved` (`AppliedMove`: where the
  object stood and stands now); the old `AppliedChanges` constructor stays.

## v1.38.0 — 2026-10-14

### Added
- **Cut and paste.** **Cut (Ctrl+X)**, beside Copy in the Area tool's **Copy and paste** action and on the Select
  tool's selection, makes the same copy as Ctrl+C and then takes the objects copied out of the world, as one undo
  step, so a paste moves a build instead of doubling it. The ground where it stood stays as it is. Docs:
  [Copy and paste](docs/area.md#copy-and-paste), [Select](docs/select.md).

## v1.37.4 — 2026-10-14

### Fixed
- **Spikes and an orange wedge in the 3D view after a visit to the map.** Opening the map and coming back to the 3D
  view throws its OpenGL context away and makes a new one; the spawn-point and nest markers, the Dungeon tool's marks
  and the Mountain tool's preview kept their old buffer names, which in the new context named other things, so the markers
  were drawn into them each frame: models torn into spikes (gone while their kind was hidden, back when shown), and an
  orange wedge across the view. Those names are now forgotten with the rest.

## v1.37.3 — 2026-10-14

### Changed
- **A click finds an object by its drawn shape.** A click found the nearest box round an object, so a chest, a stone
  or any object under a tree's branches could not be clicked: the box round the tree's whole crown was in the way. The
  boxes now only say which objects to try; the triangles of their models decide, so what you see under the pointer is
  what you get. Leaves, bushes and other cut-out foliage count only where nothing solid is behind them along the
  click. An object whose model is not in yet is still its stand-in box.
- **A standing tree is outlined by its trunk.** The selection outline (and the one under the pointer) of a standing
  tree is a column round its trunk, the full height of the tree, instead of the box round its whole crown. Logs and
  stumps keep their box.

## v1.37.2 — 2026-10-14

### Changed
- **A large selection is marked by one box.** Past 500 selected objects the 3D view draws one orange box round
  them all instead of one round each: a whole stacked structure selected at once (tens of thousands of pieces)
  was a solid orange blob that hid it. Up to 500 the boxes are drawn as before.

## v1.37.1 — 2026-10-14

### Changed
- **The green dot on each unsaved object is much smaller.** It was about 1% of the view's height across (11 pixels at
  1080p, 21 at 4K) and covered small objects; it is now 5 pixels at 1080p, 9 at 4K, with a soft edge so it stays round.

## v1.37.0 — 2026-10-14

### Added
- **No limit under the Area tool's Flatten, Raise and Lower.** The **No limit: past the game's ±8 m** switch now shows
  with those three actions too, the same switch as the one at the bottom of the brush options (one follows the
  other), so an area can be flattened, raised or lowered past the limit without leaving the panel. The cut and fill
  line counts all of it then, with nothing "out of reach".

## v1.36.0 — 2026-10-14

### Added
- **The Windows program is signed.** `ValheimWorldEditor.exe` in the Windows package carries an Authenticode
  signature, made when the release is built with the maintainer's code-signing certificate (Certum, through
  SimplySign's cloud) and timestamped, so Windows names the publisher in the file's properties and in its
  "Windows protected your PC" prompt, which goes away as SmartScreen gets to know the signature. How it is
  made and checked: [Releasing](docs/development.md#releasing), [SECURITY.md](SECURITY.md#checking-a-download).

## v1.35.4 — 2026-10-14

### Fixed
- **Live mode could lose a request with "Connection reset by peer".** The game's web server closes a connection it
  has answered on after 15 seconds without another request, and a request the editor sent on it at that moment was
  reset: a look at the game or an apply failed, and one of the live tests now and then. The editor now opens a new
  connection after 10 seconds unused, before the game closes the old one.

## v1.35.3 — 2026-10-14

### WorldEditorBridge
- The plugin's log says more of each apply, for when a change does not show for every player: the apply line adds
  how many objects the game knew before and after and how many peers are connected, and a second later a new line
  says how many of the destroyed objects the game still has and how many other objects of their prefab stand within
  10 cm of their place (made again, or there all along). See [What the plugin logs](docs/live-mode.md#what-the-plugin-logs).

## v1.35.2 — 2026-10-14

### Fixed
- **Opening another area erased the history made before it.** The steps that did not fit the new area (all of them,
  after a paste or any edit in another part of the world) were dropped, so they could never be undone again, there
  or anywhere. They are now kept aside, each in the frame of the area it was made in, and come back when that area
  is opened again; only a step partly in the new area still stops the older ones from being undone there. The
  History panel says how many steps are kept aside, and the history file keeps them too.

## v1.35.1 — 2026-10-14

### Fixed
- **Worlds made with generated zones had no locations and no creatures in them.** `WorldCheck create <radius>` (the
  documentation's, the benchmark's and the demo worlds) marked the zones around the middle as generated, with their
  vegetation, but the game spawns a zone's location (the start temple, an altar, ruins...) and its zone control
  object, which its creatures spawn from, only when it generates the zone itself: so neither ever appeared, in the
  game or in the editor. The creator now spawns them as the game would: every location the seed lays out in those
  zones stands there with its stones, chests and spawners and its location proxy, on cleared ground, and every
  generated zone has its zone control. A zone with a dungeon's entrance (an interior the game lays out) is left to
  the game. Worlds the editor's **New world** page makes are not affected: they leave every zone to the game.

## v1.35.0 — 2026-10-14

### Added
- **Live mode for mods' own plugins (the bridge API)**: a mod's BepInEx plugin can register with the WorldEditorBridge
  (`WorldEditorBridge.Api.dll`, shipped with it and in the SDK's zip): it is told what the editor applied, right after,
  on the game's main thread (to put its things together again at once instead of on the next load), and it answers
  calls on routes of its own. In the editor, a mod's extension gets the game it is connected to (`Host.Live`): which
  mods registered, and calls to their routes, for what only the running game knows; and it can ask for its shapes to
  be drawn again. The SDK's sample shows both halves. See [Live mode for mods](docs/modding.md#live-mode).

### WorldEditorBridge
- `GET /mods` lists the mods' plugins registered with the bridge and the API version; `POST /mods/<id>/<name>` calls
  one of a mod's routes. Every apply (`/objects`, `/terrain`, `/zones/reset`) tells the registered listeners what
  changed. Older editors see no difference.

## v1.34.0 — 2026-10-14

### Added
- **Ground cuts for mod extensions**: an extension can leave the ground undrawn inside outlines it gives for its
  objects (the mouth of a tunnel, a pit the mod digs while the game runs), so what the mod builds below shows; a
  click goes through the hole to what is in it. The SDK's sample mod's pits show how. See
  [Extensions](docs/modding.md#extensions).

## v1.33.0 — 2026-10-14

### Added
- **Editor extensions for mods (the SDK)**: a mod can ship a small DLL, built against the editor's SDK
  (`ValheimWorldEditor.Sdk`, attached to each release with a sample), and name it in its `worldeditor.json`. It
  teaches the editor what a file cannot: reading the mod's packed data in the inspector, rewriting the ids it keeps
  in pasted copies, drawing what the mod builds in code (a sail from its corners). An extension runs only once you
  turn it on in **Settings → Mod extensions**, and only that file: an updated DLL waits until you turn it on again.
  A sample extension of a made-up mod shows every hook. See
  [Extensions](docs/modding.md#extensions).

## v1.32.0 — 2026-10-14

### Added
- **Unity's cylinder, sphere and capsule**: mods' pieces built from them (as from Unity's cube, quad and plane
  already) are drawn.

### Fixed
- **Glass and other see-through materials are drawn see-through**, blended over what is behind them, instead of cut
  out at half (which hid glass less than half opaque) or drawn opaque. Crystal windows are no longer invisible: the
  game's own Crystal Wall 1x1 and the mods' glass using its crystal material (PressurePlate's Crystal Pressure Plate)
  show, and nearly clear glass (VALKEA, CoreWoodExtras, MoreGatesExtended) shows as a tint. GlassPieces' glass is no
  longer an opaque frosted pane. The game's own see-through materials (its ships' glass, gems, some ice) are drawn
  see-through too; its other objects look as before. Building pictures blend them the same way.
- Mods' materials keeping their texture under another name than `_MainTex` (`_Albedo`, `_BaseMap`...) are drawn
  with it (VALKEA's dried grass).
- Mods' sounds and effects (prefabs the game draws nothing of) are no longer offered in Place; those already in a
  world still show.

## v1.31.0 — 2026-10-14

### Added
- **Linked objects**: objects a mod builds out of others (a ship and the pieces built on it) are drawn where the mod
  builds them, around their root, turned and leaning with it, instead of in a pile where the game saves them. Such a
  group is moved, turned, copied and deleted whole: a double click on a part selects it, and any part brings the rest
  along (the Select tool, copy and paste, Claude's tools). Each pasted copy gets ids of its own, so the game keeps the
  copies apart. The editor knows no mod: a mod says how its objects link in the `links` of its `worldeditor.json`. See
  [Linked objects](docs/select.md#linked-objects-ships-and-other-mods-groups) and
  [for mod authors](docs/modding.md#linked-objects).

## v1.30.0 — 2026-10-14

### Added
- **worldeditor.json for mod authors**: a mod can ship an optional `worldeditor.json` telling the
  editor what it cannot guess: labels, help and a read-only flag for the data its objects keep
  (the inspector), prefabs not to offer in Place and the Build panel (its tools, effects, helpers),
  and groups for its pieces in the Mods tab. See [For mod authors](docs/modding.md) and the
  [schema](docs/worldeditor.schema.json).

## v1.29.0 — 2026-10-14

### Added
- **Mods' data by name in the inspector**: the values mods keep on objects (ValheimRAFT's
  `MBParentId`, a mod's levels or owners...) show by their names instead of numbers. Like the
  game's own, the names are found in the string literals of the mods' code, read from the DLLs in
  your Valheim folder and mod manager profiles (never run), and kept until a mod changes. The
  game's own name still comes first when both have one.

## v1.28.0 — 2026-10-14

### Added
- **Crunched textures read**: textures mods keep crunched (Unity's DXT1 and DXT5 Crunched formats)
  are now read, so pieces that showed untextured, like OdinsKingdom's stone walls, are drawn with
  their textures.

## v1.27.0 — 2026-10-14

### Added
- **Modded pieces drawn as in game**: a mod's pieces and objects learned from a live game are drawn
  with their own models and textures (in the 3D view, the Workshop and the Build panel's pictures),
  no longer as boxes. The game tells what it draws of each (mods make most of theirs while it runs,
  from copies of the game's pieces and placeholders it swaps for the game's meshes and materials),
  and the editor finds those meshes, materials and textures by name in the mods' own bundles, which
  it copies out of their DLLs from your Valheim and mod manager profiles, else in the game's.
  Meshes of the same name are told apart by their size, Unity's own cubes and planes that mods use
  are made by the editor, and compressed meshes (some mods' bundles) are read. Meshes a mod builds
  in code while the game runs (ValheimRAFT's sails) are not drawn, and crunched textures
  (OdinsKingdom's stone) are not read yet: those pieces show untextured.
- **Mods tab by mod**: the Build panel's Mods tab lists one mod's pieces at a time, chosen in a row
  of the game's mods with their number of pieces, at most 300 at once (search for the others), so a
  mod that adds thousands (PlanBuild's plans) neither buries the others nor slows the panel.

### WorldEditorBridge
- New `POST /models`: what the game draws of some prefabs, by the names of their meshes and
  materials, with their sizes.
- `GET /catalogue` names a prefab's mod as it was installed (its package's name), not after the
  plugin inside it that added the prefab (ValheimRAFT's pieces were listed under "Inventory Patch
  Detector").

## v1.26.0 — 2026-10-14

### Added
- **Modded games' objects**: opening a live world now asks the game for the objects its mods add
  (building pieces, furniture, stations...), which the editor cannot find in Valheim's files. Their
  objects get their names and can be placed, the Workshop's Build panel lists the mods' pieces on a
  new **Mods** tab with their cost (whatever tool builds them in game: a mod's own hammer too), and
  the inspector says which mod an object comes from (when Jötunn is installed). What was learned is kept in `mod-catalogue.json`, so
  a modded server's world read offline afterwards shows them too. The mods' models are not drawn
  yet. See [Modded games](docs/live-mode.md#modded-games).

### WorldEditorBridge
- New `GET /catalogue`: every saved kind of object the game knows, mods' ones included, with which
  mod adds it (asked of Jötunn when it is installed).

## v1.25.3 — 2026-10-14

### Fixed
- **The Cut sliders start at their right end, with no cut**: the Workshop's and the Dungeon tool's
  started at the left end, where no cut was. Now the right end is off and the left end cuts at the
  ground (the Workshop) or the floor (a dungeon), so sliding left takes away from the top down.
- **A pasted town stands as it was copied**: copying a whole town and pasting it elsewhere left it
  half fallen down in game, its chests empty and trees growing through its floors.
  - Copy no longer takes the whole selection's ground, only the ground the buildings stand on (under
    each piece and a metre around, blended into the new place over three metres): the town sits in
    the land where it is pasted, its foundations and cellars supported as they were. The Select
    tool's copy takes the ground under its pieces too.
  - Pasted objects keep everything the originals held: chests their contents, signs their text,
    pieces their builder and health (a pasted chest was an empty chest). A copy saved as a
    blueprint keeps the contents, not the builder.
  - That ground goes past the game's ±8 m when the new place needs it; it stopped at 8 m, and
    pieces left floating fell in game.
  - **Clear the site** no longer digs where the copied ground is pasted (it dug trenches around
    foundations set into the ground), and ignores pieces hidden far underground (a mod's crafting
    stations 10 km down made it dig a 400 m pit).
  - A paste must fit in the open area: past it the ground could not be pasted nor the trees cleared,
    yet the pieces were placed there. It is now refused with a message, and its outline turns red
    while it does not fit.
  - The ground and the objects of a paste line up exactly (the click point is moved by half a metre
    at most), not up to half a metre apart.
  - Claude's `copy_objects` also keeps what the originals hold.

## v1.25.2 — 2026-10-12

### Changed
- **Releases are made by CI after approval**: once a release's CHANGELOG headings are dated and merged,
  CircleCI tests main, waits for the maintainer's approval, then tags it, publishes the GitHub release
  with the packages it tested, and publishes the plugin on Thunderstore and Hexium and the plugin and
  the editor on Nexus Mods (see docs/development.md, Releasing). The plugin now builds against any
  Valheim dedicated server and BepInEx folder (`ValheimDir`, `BepInExDir`).

## v1.25.1 — 2026-10-12

### Fixed
- **A ruin you built onto shows whole** (#115): the stones of a ruined tower patched with wood
  were hidden (they are the ruin's, under Ruins & structures, off at first so ruins are not
  spoiled), so only the wood showed, in the 3D view and on the map. A ruin's pieces that touch your
  pieces, and the ones touching those, now count as your building: under **Your buildings** in the
  3D view and drawn with **Show buildings** on the map. The map also has **Show ruins** (off at
  first) to draw every ruin, fainter.

## v1.25.0 — 2026-10-12

### Added
- **The game's locations are drawn whole**: the parts of a location that are not objects of the save
  (a crypt entrance's rocks, the trader's camp, a boss's altar, a troll cave's walls, the rune tablets
  of ruins) show in the 3D view with **Ruins & structures**. The save keeps only a marker for each
  location, and the game makes these parts again from it each time the zone loads, so they cannot be
  picked or changed. A part the game keeps only by chance is drawn when it is there at least half the
  time.

## v1.24.1 — 2026-10-12

### Fixed
- **Windows: objects that came and went, black lines across the window** (#115): with some graphics
  drivers (older ones, laptops with two graphics chips) the 3D view and the map showed old frames in
  between new ones, and lines crossed the buttons. The editor now draws through Direct3D on Windows,
  as games do; **Settings › Drawing › Draw with the graphics driver's OpenGL** goes back to the
  driver's OpenGL (taken at the next start). The log says what draws the map, as it did for the 3D
  view.

## v1.24.0 — 2026-10-12

### Added
- **Claude changes objects already there**: `move_objects` moves and turns objects as one group (a
  building keeps its shape; with `ground`, each stays as high above the ground as it was);
  `set_transform` puts each object exactly (place, turn, tilt, size); `replace_objects` swaps them for
  another kind; `get_object` gives everything an object is and holds, as the inspector shows it; and
  `set_object_data` changes its values (a portal's tag, a builder, a ward's permitted players, health,
  fuel, a creature's level…). Objects keep what they hold when moved.
- **Claude finds places in a world**: `list_locations` lists the world's locations (the start, the
  traders, the bosses' altars, dungeon entrances, villages…) with what players call them, and
  `search_world` searches every object as the map's Search does (by kind, items in containers, texts).
- **Claude copies and puts blueprints into worlds**: `copy_objects` makes copies once or in a row or a
  ring; `add_blueprint` now puts a blueprint of the library into a world's area too (not only the
  Workshop's plot), turned as asked, and its step is one of Claude's (History's **Take back Claude's
  changes** takes it).
- **Claude weathers and regrows ground**: `shape_ground` runs the Area tool's ground actions over a
  rectangle (smooth, naturalize, erode, restore, raise, lower), and `regrow_nature` regrows what the
  game grows there.
- **Scripts move, tilt and snap objects**: objects carry their turn, tilts and size (`Yaw`, `Pitch`,
  `Roll`, `Scale`); `Objects.Place` takes `pitch`, `roll` and `snap` (pieces meet the pieces there as
  the hammer makes them); and `Objects.Move` moves, turns or sizes an object that is there, keeping
  what it holds, in the script's one undo step.
- **Claude digs along lines, resets zones and makes worlds**: `dig_path` runs the Path tool along a
  line (a river or moat, a cave roofed with boulders, a ramp, a flattened or smoothed way);
  `reset_zones` marks zones for reset (or unmarks them) as the Area tool does; `preview_seed` shows what
  a seed's world will be like (a map, and where the start, traders and bosses will be) and
  `create_world` makes and opens a new world.
- **Claude stamps, imports heightmaps and masks its ground work**: `stamp` presses a stamp into the
  ground (Mountain, Mesa, Crater rim, Dunes, Rocky ground, the user's own, or any PNG);
  `import_heightmap` puts a heightmap picture into a rectangle; `shape_ground` also paints; and
  `shape_ground`, `dig_path`, `stamp` and `import_heightmap` take a mask, as the Mask panel's (heights,
  slopes, biomes, paint).
- **Claude edits the game's dungeons**: `list_dungeons` and `dungeon_rooms` show a dungeon's rooms, open
  ends and door joints; `add_dungeon_room`, `remove_dungeon_room`, `close_dungeon_ends` and
  `set_dungeon_door` change it as the Dungeon tool does.
- **Claude saves blueprints**: `save_blueprint` saves the Workshop's building to the library (as Save
  blueprint does, with what objects hold and a picture), refusing to replace one unless told to;
  `open_workshop` loads one.
- **Claude uses every control of the editor**: `list_controls` lists every switch, dropdown, slider,
  number and text box and button the editor shows (the View panel's switches, the Workshop's Cut and
  Support check, each tool's settings, the rail of tools) with its value, and `set_control` sets one as
  the user would. Saving, applying live, discarding, reloading, writing or deleting the user's files and
  connecting to the live game are refused.
- **Claude reads the ground exactly**: `ground_at` gives the height at points (now and as generated),
  the biome and any water there; `list_objects` also gives tilts and size when an object has them.

### Fixed
- **Moving objects on the Workshop's plot** (or a generated dungeon's, before saving) lost what they
  held: a chest's contents, a sign's text. They keep it now.

## v1.23.1 — 2026-10-12

### Fixed
- **Code coverage** on Codecov counts every test again: the tests that start the editor in a process
  of their own no longer wipe out what the other tests in their CI container covered (about a third
  of the app's tests went uncounted), and the containers' reports are merged without losing which
  branches ran. The app (`Desktop/`) now has its own 95% coverage check, and many new tests cover its
  untested lines and branches.
- **Workshop**: a click that moved the mouse a little before letting go placed nothing; it now puts
  the piece the preview shows.
- **Discard** that has to read the world again no longer fails without a word when the world's files
  cannot be read: it says why and keeps the area open.

## v1.23.0 — 2026-10-12

### Changed
- **Larger areas**: the area can now be 9 × 9, 13 × 13 or 17 × 17 zones (up to 1088 m across), from
  the map's size box and the top bar's; Follow grows it up to 17 zones each way, and Claude's
  `open_area` opens up to 17 × 17. Large areas are slow to open and draw: meant for big works and
  for testing the editor's speed.
- **Faster drawing of large areas**: only the parts of the area in view are drawn (in 128 m cells),
  and small objects stop being drawn far away (past 400 times their size, a few pixels). A 17 × 17
  area with 2.3 million objects went from 31 to about 75 frames a second.

### Fixed
- **Unsaved marks** kept their green dots after **Save to world**; they now go once saved, as after
  **Apply live**.
- **Areas with very many new objects** were slow at every frame: the green dots were made again for
  each object at each frame (a 17 × 17 area with 2.3 million new objects drew one frame a second or
  less). They are now kept on the graphics card and made again only when objects change.
- **Scripts on areas with very many objects** waited over a second before running (each object's
  kind was worked out again), and after a save of many new objects each change was slow to count
  what is pending.

## v1.22.0 — 2026-10-12

### Added
- **A saved world keeps its list up to date**: world folders added or removed while the editor runs
  (or a world saved again) show by themselves within a few seconds, without going to another page
  and back. **Refresh** looks for them at once.

## v1.21.0 — 2026-10-12

### Added
- **Spawn points and creature nests** (the invisible points the game's camps, villages and dungeons
  spawn their creatures from) have their own kinds in the Place list and the View panel, each saying
  what it spawns (`Boar · 0–2 ★ · spawns once`); Place's search finds them by creature. In the 3D
  view they are orange boxes (red when the creature comes back) and nests purple ones, with a marker
  above each, seen within 250 m; they are off at first, as ruins are. The inspector and Claude's
  `find_prefabs` say what they spawn.
- **Objects are sorted into real kinds**: Furniture & props, Bones & remains, Creatures & NPCs and
  Effects & markers join the lists; **Other objects** went from about 470 kinds to a handful.

### Changed
- **Follow grows the area** instead of moving it: when you look near the area's edge, a row of zones
  is added on that side and the zones already open stay (a 3 × 3 area becomes 4 × 3, then 4 × 4…).
  It never leaves zones behind, and stops at 16 zones each way (about 1 km). The arrows move the
  grown area as it is; the size box makes it square again.
- **The tool rail** is in sections, in a clearer order: **Sculpt**, **Edit** (Area, Path, Place),
  **Paint**, **Create** (Shape, Mountain, Cave, Dungeon, Script, Blueprints) and last **Tools**
  (Select, Measure, View).
- **The Place panel** is in sections with headings: **Line shape** (Points, Circle, Rectangle, right
  under the modes, with how to draw it), **Choose a preset**, **Selected kinds**, **Objects** and the
  mode's own settings. The preset list starts on **None**; choosing None after a preset unticks its
  kinds. The selected kinds are on rows of one layout in a box, each with its share (100% for one
  kind), and **Choose kinds…** opens the list under them. The **Recent** row also shows the kinds
  chosen now, and its **all** / **none** buttons are larger. **Tilt** starts at 0.
- **Place's One at a time** places only the object at the cursor: a drag no longer scatters more, and
  Density, Spacing and Clumping hide while it is on.

### Fixed
- **Kind names with an underscore** (Pickable_Flax) showed without it in the Choose kinds list and on
  the Favourite and Recent buttons (PickableFlax).
- **The Place panel's weight sliders** let go of the mouse at each step of a drag (every row was
  made again): a drag now runs smoothly. Each slider is now the kind's share in %, and the other
  kinds' sliders move with it, keeping their proportions, so the shares always make 100%. An equal
  share sits in the middle of the slider, whatever the number of kinds.
- **Previews over the sea** (the mountain, placed and pasted objects, rings and lines) were hidden
  wherever water was behind them in the 3D view: they now show over the water.

## v1.20.2 — 2026-10-12

### Fixed
- **Location markers** no longer put a post on every invisible ground disc of No limit (one every
  6 m where the ground was taken past ±8 m): they hid the real locations. The "location(s) here"
  note no longer counts them either.

## v1.20.1 — 2026-10-12

### Changed
- **While the game's look is prepared** (start page), every action is greyed out until it is done:
  worlds, servers, the Workshop, a new world and Settings. A world opened meanwhile missed the game's
  look. The side menu still moves between the pages.

### Fixed
- **Rocks that shape the ground** (the Plains' tall rock pillars, coast, heath, forest and copper
  boulders) were missing from the 3D view and the object lists: the editor only used them for the
  ground they flatten. They now show and can be selected like other rocks.

## v1.20.0 — 2026-10-11

### Added
- **The start page** has a side menu: **Home**, then **Edit a world** (my game, a dedicated server,
  a saved world) apart from **Create** (the Workshop, a new world). Home shows the game when it runs,
  with **Edit live**, and what you opened last: saved worlds, blueprints (with their pictures) and the
  servers that connect in one click. The Workshop's page lists every blueprint to open.

### Changed
- **The world map** of a world opened again (Reload, or back to the worlds and in again) shows at once:
  the last world's map is kept instead of being worked out again (seconds).
- **The editor reads how the game's locations shape the ground, the game's objects (what new ones
  need in the save, containers, wards, saplings, what stands hold), the build pieces (their
  footprints, snap points, build tools, what holds them up and how the hammer places them), the names
  of object data, the cave roof rocks' shapes, what pieces cost, the vegetation rules Regrow nature
  follows and the dungeons' rooms the Dungeon tool joins from your Valheim**, like the game's look,
  instead of keeping its own copy, which went out of date with game updates: they always match your
  version of the game. It takes a few seconds the first time and after a game update. Valheim is now
  needed: without it, no world or Workshop opens (the start page asks for its folder).

### Fixed
- **The start page** said "Preparing the game's look" at every start, then "The game's look is ready"
  for the rest of the session, while the editor only checked what it had kept from the last time:
  it now says so only when it really reads the game's files (the first time and after a game update).
- **Settings > Claude** could still say the editor could not listen on its port after Claude was
  turned off: the last start's problem stayed shown. It is cleared when the switch is off.
- **Script tool:** Ctrl+Enter in the code box runs the script again, as its button says; the box took
  the key for itself first.
- **The world map** zoomed out a little from the closest views (3 to 6 m a pixel, on a wide window)
  read its close-up again on every frame, keeping a core busy for nothing.
- **Preparing the game's look** (start page) shows how far it is and what it reads, the game's bundles
  one by one: the bar stood still at the start until the look was ready.
- **The world map** no longer holds up the rest of the editor while it is built: it took every
  background thread, so on a computer with few cores a live game's players, its answers and the other
  work in the background waited seconds for it.
- **Damaged or tampered files are refused cleanly.** A save, live snapshot, blueprint or heightmap
  picture with wrong lengths or counts, damaged compressed data or a broken PNG header now says it is
  damaged, where it could fail with an unrelated error or ask for gigabytes of memory. Found by fuzzing
  the editor's file readers (tests/Fuzz, run with ClusterFuzzLite).
- **Generate dungeon** could offer the dungeon made for the settings before: changing a setting while
  the last one was still being made, then placing it, placed the old one (and **Open on the plot**
  then opened another). A dungeon made for settings that changed since is no longer shown.
- **My game** (start page) says when your game could not be checked (a folder that cannot be read,
  for instance), and the log has the error, where before the card only stopped changing.
- **The history kept on disk** failed when the world's saves were removed while it was open (the
  game or another tool cleaning the folder): it is now kept without comparing it to a save.
- **Zone grid** (map) on a scaled screen (200 %, for instance) showed from twice as far out, its
  lines cramped together: it now shows from the same zoom on every screen.

## v1.19.0 — 2026-10-11

### Added
- **A new world** (on the start page): makes a new world, a normal one that players join
  without any mod (crossplay included). The seed's world shows before it is made, from the editor's
  copy of the game's generator (on **Generate world**, Enter or **Roll**): its land and biomes, and exactly where the game will put the start
  temple, the bosses' altars and the traders' spots (Haldor, the Bog Witch, Hildir), with how far
  each is from the start and whether you need to sail to each biome. **Find a seed** tries many
  random seeds and keeps the best, listed to fill the window, for what you ask: the start on a large landmass, land around it,
  Swamp, Mountain or Plains within a distance, every early biome on foot, a trader or the first five
  bosses within a distance. The map zooms (wheel, drag, double click) and stays sharp; switches in
  its corner show or hide each kind of mark, and the seed's facts are listed beside it. The
  **Dungeons** switches show where the burial chambers, troll caves, sunken crypts, frost caves,
  infested mines, charred fortresses and Hildir's dungeons will be, with how many lie near the start.
  The rules are read from your Valheim, so they follow game updates. See
  [docs/new-world.md](docs/new-world.md).

### Changed
- **The world generator is about a third faster** (worlds' map previews, Regrow nature, new worlds),
  with the same results to the bit.

### WorldEditorBridge
- **A failed request's log line** keeps the request's path on one line: line breaks in it could make
  it look like several lines in the game's log.

## v1.18.0 — 2026-10-10

### Added
- **Building skill for Claude Code** (`.claude/skills/valheim-build`): a workflow for buildings and
  places that look hand-made: research and a design brief first, mock-ups of unfamiliar pieces,
  phased building with a helper library, checks for blocked windows and other mistakes, then
  interiors and landscaping. See [docs/claude.md](docs/claude.md#the-building-skill-claude-code).
- **Claude** can work in the editor (Settings → **Allow Claude to connect**): an MCP server inside
  the editor, on this computer only and with a secret token, for Claude Code or any MCP client. Claude
  looks at the open world or Workshop (its state, heights, biomes, objects, screenshots, the game's
  pieces), shapes it with scripts, puts down building pieces (snapped as the game's hammer does),
  checks that they stand, adds blueprints and generates dungeons. What it changes stays pending, one
  undo step each: it never saves, applies live or saves a blueprint, and changes nothing while live
  with Auto on. It is told how the editor and Valheim's building work when it connects; it builds
  floors, walls with doors and gable roofs over rectangles, levels ground, lays roads, paints and
  plants forests in one step each, fills chests and writes signs, sees four views of a place at
  once or a map of the area with coordinates, and works on what you selected. Claude Desktop connects
  through `ValheimWorldEditor --mcp-stdio`. **History → Take back Claude's changes** removes all of
  Claude's changes and keeps yours. See [docs/claude.md](docs/claude.md).
- **Item stands and armour stands** can be filled: the Inspector's **Holds** puts an item on an item
  stand (a weapon, shield, trophy or fish on a wall, food and potions on one lying flat to set a
  table) with its quality, style and the way it hangs; **Wears** dresses an armour stand slot by slot
  (armour, cape, belt, shield, weapon) and sets its pose. Each list offers only what the game lets
  that stand or slot take, and the item is saved whole, as the game saves it, so it comes off the
  stand as set. Claude does the same with `set_contents`.
- **Generate dungeon** in the Workshop: the Build panel's Library has **Generate dungeon…**, the Dungeon
  panel's generator (building pieces only), with **Open on the plot** (the dungeon alone) and **Add to
  the plot** (with what is there, one undo step). Its blueprint is kept in the library, with what its
  objects hold.

### Fixed
- **Serving tray food stays on the table**: food and drink of the serving tray (Feaster: dishes, meads,
  berries, the feasts) placed, pasted or added with a blueprint in the editor are marked as pieces, as
  the game marks them; before, they were loose items in game, falling and despawning away from a base.

### Changed
- The Windows package holds `ValheimWorldEditor.exe` with its four native libraries beside it (keep
  them together), and the program carries its details (name, description, author). Some antivirus
  programs flagged it: it unpacked those libraries to a temporary folder at each start, which looks
  like what a dropper does, and had no details.


## v1.17.0 — 2026-10-10

### Changed
- The game's look (models, textures, the terrain shader and the map) is **read straight from your
  Valheim**, in the editor itself: nothing is copied any more. No more copy of a few minutes at the
  first start and after every game update (the first time, and after an update, the editor reads the
  game's files for a few seconds), no 150 MB `game-look` folder (the old copy is deleted), and
  smaller release packages, with no Python inside: about 23 MB less to download on Linux (77 MB
  less unpacked) and 10 MB less on Windows (37 MB less unpacked).
- The editor runs on **.NET 10** (it was .NET 8, whose support ends in November 2026). Nothing to
  install: the packages still carry it.

### Added
- When Valheim is not found, the start page explains step by step how to find its folder (Steam's
  **Manage › Browse local files**, the usual places on your system). A folder next to the game folder
  (`steamapps`, `common`, `valheim_Data`) is taken as the game folder it leads to, on the start page
  and in Settings.
- Live: the open area **follows the game**. Every 2 seconds the editor asks the game what changed in
  that area's zones (only those): objects players built, removed or changed appear, go or change in
  the editor, and ground dug or paved there comes in, without steps in History and without
  dropping your changes (Reload is no longer needed for the area you work in). A changed object stays
  the same object (history, selection and inspector follow it). Objects you deleted and applied stay
  deleted; objects the game removed are not brought back by undo or redo.
- Live: where you have ground changes not applied yet and the game changed that zone's ground too,
  yours are kept, the status bar says so, and **Apply live** asks first: applying could erase what
  players did there. Undo your changes there and the game's ground comes in.

### Fixed
- Place, Line: a line exactly a whole number of spacings long sometimes left out its last object.
- Opening another world while the map or an area of the first was still being drawn no longer takes
  that world's rivers and streams away (its ground came out without them), and the map reads its own
  world's biomes. Making a world's ground while Regrow (or another world) runs no longer shifts its
  rivers and streams away from the game's.
- Ground is worked out a little faster (river lookups no longer walk long chains).
- A blueprint or copy with a damaged object value (a number that is not one) no longer makes saving
  the world fail: that value is left out when the blueprint is read.
- In the Workshop, the pieces of a ruin blueprint (a generated dungeon) added to a building stay a
  ruin's in the blueprint saved, instead of becoming the builder's.
- The 3D view no longer stalls for a frame after each Dungeon tool change while it reads the
  dungeons' rooms again: they are read beside it.

### WorldEditorBridge
- `/watch`: one number per asked zone, which changes when an object there is made, removed or
  changed (not when a creature walks or a fire burns); `/zone`: the saved objects of the asked zones,
  portals included, in the snapshot's format. Only those zones are read, from the game's own
  per-zone lists.

## v1.16.0 — 2026-10-09

### Added
- **Generate a dungeon** (Dungeon panel): a whole dungeon from a few choices and a seed. It is made
  of building pieces, or of the biome's own dungeon rooms. A plan of each level updates as you
  change the choices.
  - Eight styles: crypt, catacombs, temple, fortress, prison, Dvergr hold, goblin warren and ruins.
    Each has its own rooms (burial chambers, ossuaries, chapels, barracks, feast halls, cell blocks,
    forges, studies...), each furnished for its purpose.
  - Seven wall materials, and eight biomes for monsters, chests, loot and lights.
  - Settings for size, levels, monsters (spawners that come back, or monsters placed once), the
    boss, loot, light, furniture, ruin and ways round.
  - The layout reads like a building: rooms along axes, wings in pairs, and loops between rooms
    that lie close together. Stairs go down between levels, and some halls rise through two levels.
  - A door closes every room, so its monsters wait behind it. An antechamber leads to the boss's
    arena behind a gate that opens with the crypt key, which lies in a chest far away; the treasury
    is beyond the arena. Hidden caches sit behind cracked walls.
  - **Place in the world** puts it 5000 m above the view with a linked portal pair, as one undo
    step. It is written as a ruin, like the game's own dungeons. **Open in the Workshop** opens it as
    a blueprint.
- **Blueprints keep what objects hold**: chests' contents, signs' texts, creatures (a dungeon's boss,
  with its stars) and whether the building is a ruin. They go in the editor's own lines of the file,
  which Homestead and PlanBuild skip, so the file still builds in game. They come back when the
  blueprint is opened in the Workshop, saved from it, or placed in a world.
- **Chest contents as in game** (inspector): the container's slots in a grid. Click one to choose its
  item, how many and how good; empty it, or move it to another slot. A double click on an object
  opens its inspector.
- **Dungeon** tool: edit the rooms of Frost Caves, burial chambers, sunken crypts, infested mines and
  the game's other dungeons. The game builds a dungeon from a list of its rooms kept in the save
  (5000 m above its entrance); the tool edits that list, and the game builds what you make, no mod
  needed.
  - **Go inside** takes the view up to the dungeon and cuts its roofs away to look into its rooms.
  - Pick one of the dungeon's rooms and click an open end (a green frame): the room joins there the
    way the game joins rooms. **Turn** (R) joins it by another of its openings. A red box warns when
    it would overlap another room or leave the dungeon's space.
  - Added rooms come with what the game would put in them at that place: chests, creatures'
    spawners, torches, ice, decorations, rolled the way the game rolls them.
  - **Doors** where rooms meet: an added room gets the dungeon's door (curtain, gate, ice wall) by
    the game's chance, and a click on a square where two rooms meet puts one there or takes it away.
  - Delete a room (its contents and doors go with it), and **Close open ends** caps them with the
    game's end caps. Each change is one undo step; **Save to world** and **Apply live** write it.
  - **Build here**: build with the hammer's pieces inside the dungeon, on its rooms' floors (where they
    hold in game).
- The game's look now includes the dungeons' rooms (copied once more after this update).
- **Dungeon rooms** in the View panel shows or hides them.

### Fixed
- Contents set in the inspector on a chest never opened were replaced by the game's own loot when it
  first loaded. Such a chest is now marked as already filled.
- The support check knows the pieces the game never checks: Mistlands' large black marble floor
  (also carts, ships and wards) stays up wherever it is and holds what stands on it, as in game. A
  building on such floors in the air no longer shows walls that would fall.

## v1.15.6 — 2026-10-09

### Fixed
- Saving a world the game saved since the editor read it (a play test between two saves) no longer
  throws away what was done in the game: the save was made from the save the world was opened from,
  and leaving the world then deleted the game's save. The editor now says Valheim saved the world
  and saves nothing; leave the world and open it again to edit the game's save.
- Changes saved (or applied live) from the map are kept as saved: **Discard** in the area opened
  next undid them too, and saving again then took them out of the world. The History panel also
  showed them as not saved.
- **Area → Backup** no longer restores another world's backup: a folder chosen with "Another
  folder…" stays in the list when another world is opened, and choosing it there used the backup
  read for the first world without checking its seed.
- Live: ground changed while **Apply live** waits for the game (a second stroke with Auto apply on)
  is no longer lost. It was marked as applied with the first stroke but never sent: players did
  not see it, and Discard did not take it back. It now stays pending and goes with the next apply.
- The editor's keys (Ctrl+Z, Ctrl+Y, Ctrl+S, Ctrl+V, the tool keys) do nothing on the start page
  and the map. On the map they acted on the area left open behind it: Ctrl+Z undid its last step
  unseen (live with Auto apply on, in the game too).
- A world with two terrain objects in one zone (the game makes them now and then) can be saved:
  every save failed with "An item with the same key has already been added". The zone's ground now
  goes into both, so the game shows it whichever one it uses.
- A save cut short (the game or the editor stopped while writing it: no `_main.<n>.ok`) is no longer
  taken for the world's save, as the game does. The world opens from the last complete save, and
  leaving it no longer deletes that save. Leaving a world also no longer deletes your own files named
  `_main.<something>` in its folder (a `_main.backup.zip`).
- Copying the game's look no longer breaks for good when it is stopped halfway (closing the editor
  during the first copy): it could leave a cut file that made every later copy fail, or models
  without their textures that were never copied again. Each file is now written whole or not at
  all, and a model counts as copied only with its materials.
- After a Valheim update, the game's look is copied from the new game files: the copy kept using
  where things were in the old ones (new kinds got no model, changed ones kept the old model, and a
  removed file made the copy fail every time). It now reads the game files again and copies every
  model again when they changed.
- Settings and saved servers are written whole or not at all: a crash or power cut while writing
  them no longer resets the settings (Valheim folder, world folders, recent worlds) or loses every
  saved server. Settings that cannot be read are kept as `settings.json.bad`, and a settings file
  that cannot be written (locked) no longer stops the app.
- `servers.cfg` is only readable by you from the moment it is made (it was readable by others for an
  instant), and a saved password with spaces at its ends no longer loses them (the login failed).
- Opening another area (or saving, or switching the look) while the models are still being read no
  longer closes the editor or draws objects of the area left: a model read for it went into the new
  area, with that area's object numbers.
- An error the editor did not expect no longer closes it, losing every change not saved: it is
  written to the log and said in the status bar (or a message away from the 3D editor). Saving,
  applying and Discard from the map, and leaving a world, say what went wrong (a world folder gone
  or locked) instead of closing the editor or leaving it showing "Saving…".
- Blueprints: importing (also by dropping a file on the 3D view), exporting, deleting and turning an
  older blueprint into a Homestead one say so when the folder cannot be written, instead of closing
  the editor; so does exporting a heightmap.
- Opening a blueprint in the Workshop that cannot be read (removed meanwhile) keeps the open world
  as it was: the world was closed first, and the editor kept showing an area of it that could no
  longer be saved or applied.
- A folder of worlds added in Settings that holds a folder that cannot be read (a drive's root, with
  its "System Volume Information" or "lost+found") no longer leaves the start page with no world
  listed: that folder is skipped. The same for mod manager profile folders.
- Shape formulas: a function given the wrong number of values (`sin()`, `pow(x)`, `clamp(x, 0)`)
  is a mistake explained when the formula is read, instead of every point failing as "bad"; and a
  formula nested very deeply (a pasted one with thousands of brackets) is refused instead of closing
  the editor.
- A damaged history file (kept between runs) no longer stops its world from opening: the world opens
  without the earlier history. A `stamps.json` with an entry missing its picture no longer stops the
  editor from starting.
- Valheim for Windows: when the terrain shader of a copied game look cannot be made into GLSL on
  this computer, the start page says so, instead of copying the whole game look again at every start
  (minutes each time, ending the same way). A later editor whose shader converter changed makes the
  shader again from the copied file, without a new copy.
- An object put (or pasted) outside the world, beyond about ±16 km, is left out of the save and
  listed under "Not saved"; the whole save failed.
- The Mask's slope, height and paint rules judge edits made at once (Mountain, Shape, Paste, Stamp
  once, Path) on the ground as it was before them. They saw the points already changed by the same
  edit: with a slope limit, most of a mountain was left out in a ragged pattern.
- Copies and blueprints keep objects of kinds the editor has no name for (from mods): they are
  written as their number and came back as another, unknown kind, left out when pasted.
- The Area tool's **Restore** also takes back No limit ground (a mountain, a deep paste), as the
  Restore brush does: over a mountain it changed nothing to be seen.
- A **Path** with Smooth along the west or east edge of the area no longer mixes in the ground of the
  opposite edge.
- Pointing past the edge of the area no longer finds "ground" 1000 m down: the Measure tool, pastes,
  brushes and the Path and Area tools took such points. Ward and workbench rings crossing the edge
  no longer plunge down outside it.
- An object found with the map's search and opened with **Edit in 3D** stays selected in the editor:
  the selection went at the first frame drawn, while the inspector still showed the object.
- **Script**: a script's changes are not applied when the area changed while it ran (a stroke, an
  undo, a save that read the world again): they overwrote the stroke, or removed other objects than
  the ones the script chose. And an object number that is not the area's no longer leaves the
  script's ground change without an undo step, while saying nothing changed.
- Closing the window with unsaved changes and answering **Keep editing** no longer stops the copy of
  the game's look running in the background.
- Live, SSH: a saved server that uses a key file with a passphrase can connect again: its row has a
  **Key passphrase** box (the passphrase is never saved, and the connection always failed with "The
  key file could not be read"). One of the usual keys in `~/.ssh` that cannot be used no longer stops
  the login before the password is tried. And the server's remembered identity is checked for every
  account on that address: logging in as another user accepted any identity, and saved it.
- The README in the editor's packages names the plugin's file as it is on the release page
  (`WorldEditorBridge-1.15.6.zip`, it said `WorldEditorBridge-v1.15.6.zip`).
- The world generator is closer to the game's: lengths and distances between points (rivers,
  streams, lakes) are worked out with the game's precision. Of the heights recorded in the game, 2992
  of 3000 now match bit for bit (2982 before) and all 8 zones (6 before); the rest still differ by
  less than a millimetre.
- The map and areas are made faster on computers with many cores: the ground of 256 zones in parallel
  takes about 0.1 s instead of 0.6 to 1.3 s (a cache of the rivers made the threads wait on each
  other).
- Areas with very many objects of one kind (tens of thousands of one piece) open, save and switch
  the look without a pause of a second or more.
- Worlds open and save faster: reading a world looked through every edited zone for each object.
  A world of 355,000 objects and 78 edited zones reads in 0.24 s instead of 0.57 s; worlds with
  thousands of edited zones gain much more.
- Less memory over a long session: a model's vertices are let go once they are on the graphics card,
  and past 1 GB of models and textures there, opening another area lets them go (that area's are
  read again). They piled up with every new kind seen, gigabytes with Valheim open beside it.
- Areas with hundreds of kinds of objects no longer hold up other background work while their models
  are read (the next area, the map's search and close-up waited seconds behind them).

### WorldEditorBridge
- An apply the game takes more than 30 seconds to start (a world save on the server) is no longer
  done later anyway: the editor was told it failed and sent it again, so new objects were made twice
  for every player. Given up on, it never runs; once started, the editor waits for it.
- Objects linked to others (a creature spawner and what it spawned, two connected portals) keep the
  link when a live delete or move is undone: the object came back without it (a spawner then spawned
  a second creature), and the other side still pointed at the removed object. A paste of a linked
  object still there takes no link (it would take the original's).
- A malformed object to make fails the whole call before anything changes. Objects removed and made
  before it stayed done, and sending the call again made them twice.

## v1.15.5 — 2026-10-09

### Fixed
- Copying the game's look works with Valheim for Windows. It stopped with "ValueError: 15 is not in
  list": the Windows game has its terrain shader for Direct3D and Vulkan only, and the copy looked
  for the OpenGL one (which only the Linux and Mac games have). It now takes the Vulkan one there,
  and the editor turns it into the same shader: the ground looks as it does from a Linux game.

### Added
- **Open log**, at the bottom of the start page and when copying the game's look fails: opens the
  editor's log, to attach to a bug report on GitHub.
- The log is now `ValheimWorldEditor.log` (was `log.txt`), started over at each run like the game's
  `LogOutput.log`, and says more: each line has its time, and it holds the game-look copy's own
  output (the error that stopped it, too), which Valheim folder and game build it copied from, and
  errors in background tasks.

## v1.15.4 — 2026-10-09

### Fixed
- Undoing a placed object after **Apply live** removes it for the players too, on servers with mods
  that make objects again under a new id when they appear (ServersideQoL does, for build pieces,
  plants, fires, turrets and more). The undo found the object "already gone", and players kept
  seeing the new copy. Each removal now also says what the object is and where it stands, and the
  plugin removes that object at that place when its id is gone. Needs the plugin from this version
  on the server.

### WorldEditorBridge
- An object to remove whose id is gone is looked for by its kind and place (within 10 cm), once
  each, and removed if found. Another kind of object at that place, or an empty place, is left
  alone. Editors from before still work as they did.

## v1.15.3 — 2026-10-09

### Fixed
- The editor no longer fills the graphics card's memory over a session. Each trip between the map and
  the 3D editor left the whole area's models, textures and ground on the card (about 165 MB a trip),
  and each move to another area left its ground and game look (about 40 MB). Both are now deleted, so
  the editor stays near the same size however long it runs, and leaves room for a game played beside
  it.

## v1.15.2 — 2026-10-09

### Added
- The history is kept after the editor closes. After each **Save to world** or **Apply live** it
  is kept on disk with the world, and when the world is opened again (in a later run too) its steps
  are back in **History**, tagged **earlier session**, ready to undo: the ground as it was, a deleted
  object back with its contents, a placed one removed. When the world may have changed since (live,
  or saved by the game), the editor asks once before undoing or redoing those steps.

### WorldEditorBridge
- A new mod page: what people use live editing for, how it works, what to expect, and what each
  connection message means. The plugin itself is unchanged.

## v1.15.1 — 2026-10-09

### Changed
- **Blueprints** on the tool rail is a tool like the others: it lights up, and the blueprint list
  opens next to the rail in place of the last tool's options (it opened on the right, over the View
  panel). **Blueprints…** in the Area tool opens it the same way.

### Fixed
- The saved-world page no longer says each save makes a full backup (it has not since 1.14.0): each
  save is read back and checked before the old one is removed.
- The server form says the plugin's token is written the first time the server starts with the
  plugin. The README, the plugin's README.txt and its mod page say so too, and the start page
  pictures show the Workshop.

## v1.15.0 — 2026-10-08

### Added
- The area's size (3 × 3, 5 × 5, 7 × 7 zones) changes on the fly next to the Area arrows, around the
  same middle zone, keeping the view, the changes and the history.

## v1.14.1 — 2026-10-08

### Fixed
- Zooming out no longer fogs the area over: the fog starts at the point the view turns around, so
  only what lies beyond it fades.

## v1.14.0 — 2026-10-08

### Changed
- **Save to world** (offline) works like Apply live: the area and its history stay after saving; undo
  a step and save again to take it back. No backup folder is made at each save any more: nothing of
  the old save is removed before the new one reads back right (on a failure the new files go and
  nothing changed), and the save the world was opened from stays until the world is left.

## v1.13.0 — 2026-10-08

### Added
- **Mountain**: a preview before clicking: the mountain under the pointer as a mesh on the ground,
  as high as it will rise. The circle turns red where it does not fit in the open area.

### Fixed
- A wide brush or Mountain circle no longer drops a line straight down where it passes the edge of
  the open area, and follows hills instead of cutting through them.
- Zone borders (and the other overlays lying on the ground) follow the ground when it changes: after
  undoing or discarding a mountain they no longer outline it in the air.

## v1.12.1 — 2026-10-08

### Fixed
- Buildings copied with the Select tool keep their shape when pasted on a slope: each piece no longer
  follows the ground on its own (trees, rocks and the like still do).

## v1.12.0 — 2026-10-08

### Added
- **Paste** shows what it will place as see-through models (not only posts), at its height; Ctrl +
  wheel lowers or raises it (into a hill: Clear the site digs it out).

## v1.11.0 — 2026-10-08

### Added
- In a world: a **Blueprints** button on the tool rail opens the library, to paste a blueprint made in
  the Workshop.
- **Clear the site** (Paste, on by default): the ground in a pasted building's way is dug down to its
  lowest piece (a metre around too, never raised, past the ±8 m if need be), and the trees, rocks,
  bushes and pickables there are taken away, in the paste's undo step.

## v1.10.0 — 2026-10-08

### Changed
- A blueprint opened or added in the Workshop stands on its lowest buildable piece (rocks and other
  things the hoe places do not count), and the support check takes the ground it stood on in game to
  be under its lowest piece at each spot (the terrain reached each post), or at Homestead's terrain
  contact points. Rocks are left out of the support check.

## v1.9.1 — 2026-10-08

### Fixed
- Objects shown untextured (white) after opening another area while models were still loading.

## v1.9.0 — 2026-10-08

### Added
- **Cut** (the Workshop): the building shown only up to a height, a metre at a time, and the cursor
  goes through what is hidden, to build inside it.

### Changed
- The **support check** tints the pieces in the game's build-mode colours (light blue on the ground,
  green to red) instead of outlining them; the pieces' own look still shows.
- The Workshop draws pieces solid (See-through buildings is for worlds).

## v1.8.0 — 2026-10-08

### Added
- The Workshop's **Library** tab: your blueprints with pictures, cost and search; **Open** (alone on
  the plot), **Add**, drag one onto the plot, or drop .blueprint and .vbuild files from your files
  (imported into the library). A blueprint keeps its form exactly; Homestead's keep their anchor's
  height when saved again.

### Changed
- The start page's Workshop card opens the Workshop; the blueprints are in its Library.
- No unsaved marks in the Workshop (everything on the plot is new).

## v1.7.0 — 2026-10-08

### Added
- **Select**: Ctrl + click adds to the selection; Shift + click takes the row from the last object
  clicked (wall 1, Shift + click wall 3: walls 1 to 3).

## v1.6.0 — 2026-10-08

### Added
- The Workshop places pieces where the **game's hammer** would, from the game's own data for every
  piece (colliders, snap points, placement rules): the piece touches what you point at, then snap
  points within half a metre meet; pointed at a wall's top, the next stands on it.
- **Ctrl + wheel** lifts the piece (Shift: 0.1 m); , and . turn it by the game's 22.5° (or another step).
- The Build panel's **Plants** (cultivator) and **Feasts** (serving tray) tabs: everything players build.

### Fixed
- Turning with the wheel: one step a notch (smooth wheels and touchpads too), and a snapped piece keeps
  the turn asked for.

## v1.5.0 — 2026-10-08

### Changed
- The Workshop keeps to building: **Build** (the hammer's pieces in its tabs, with pictures, by
  crafting station and name, searchable; snap, grid and turn step), **Select** and **View**; no
  world tools, Mask, View panel or zone borders there.

## v1.4.0 — 2026-10-08

### Added
- The blueprint library shows each blueprint's **3D picture** (the game's models when its look has
  been copied), its **cost** in game (materials and crafting stations), a **description** and
  **tags**; the search looks through all of them; **Details** changes them.

## v1.3.0 — 2026-10-08

### Added
- **The Workshop**: a blank, flat plot (from the start page or the Blueprints panel) to build a
  building, then **Save blueprint**: only its building pieces are kept. Blueprints open in it to be
  changed.
- **Support check** (in the Workshop): each piece's structural support worked out by the game's own
  rules (per material), from full on the ground to what would fall. Saving asks first when some would.

## v1.2.0 — 2026-10-08

### Added
- Blueprints are **Homestead**'s (the in-game building mod): saved into its folder, so they show in
  its hammer tab ready to build in game, and its own blueprints are listed here. The editor finds
  Homestead in the game's BepInEx and mod manager profiles, and warns when it is not installed.

### Changed
- **Save blueprint…** writes Homestead blueprints (pieces only, no ground). Blueprints kept in the
  editor's own format before are still listed, and **To Homestead** moves them.

## v1.1.0 — 2026-10-08

### Added
- **No limit** (in the brush options; for every ground tool: brushes, Path, Area, pasting): move
  the ground past the game's ±8 m, for mountains, cliffs and canyons. Saving (or Apply live) turns
  that ground into invisible ground discs (the game's own DevGround1 location, placed as location
  proxies) that every player's game counts as generated ground: no mod needed, console players
  included. The hoe and pickaxe then get their ±8 m from the new ground. The disc heights are
  fitted by working out the ground exactly as the game does; what they leave is ordinary ground
  edits, and the discs' dirt is painted back to the biome's ground. Trees, rocks, ore, bushes and
  pickables where the ground moved more than 2 m are taken away; building pieces stay. Lifting
  again near earlier discs works them out again. Zones the game has not generated yet are left as
  they were. Very steep walls come out softer, more so near the world's middle (the game applies
  the discs in an order that depends on their height there).
- **Mountain** tool: a click raises a lone peak, a ridge, a mountain range, a mesa, a volcano or
  rolling hills, past the ±8 m (ground discs on saving). Each is made from a seed, with a natural
  profile (steeper up high, easing out at the foot), a ragged outline and broad ridges and gullies;
  slopes mostly stay under 35°, so they keep grass and trees. Randomize rolls a new one of the preset
  (size, roughness, direction, seed) that fits in the open area. It takes away the trees and rocks it
  buries and grows the biome's own on its slopes, by the game's rules.
- **Cave** tool (the Path tool's new Cave action): along a drawn line, a trench with an entrance
  slope at each end, roofed with the game's boulders (forest, coast, heath or mountain rocks, by
  biome or chosen), upside down so their flat side is a nearly level ceiling, turned, tilted and
  sized at random. Each is hung by its real underside (the game's model) to clear the headroom; more
  are added until the floor is covered, and the walls rise into the rock so no daylight comes in at
  the sides. A line drawn as a ring makes a ring-shaped cave with one entrance. Presets (tunnel,
  cave, cavern) and Randomize. The ground cannot overhang in Valheim, so the roof is rocks; players
  can mine them like any boulder.
- **Script** tool: C# scripts run on the open area, to generate anything the tools do and more.
  The editor compiles them itself (no .NET install needed) and gives them Area (its corners, its
  points, sea level, the world's seed), Ground (Height, Original, Set, Raise, Lower, Paint, Biome,
  Shape, Mountain; past the ±8 m by default), Objects (All, OfKind, Near, Place, Remove, CanPlace),
  Noise, a seeded Rnd and Print. A script runs in the background on a snapshot of the area; what it
  changes goes in as one undo step, nothing when it fails (its mistakes and errors give the line) or
  is stopped. Examples to start from (a mountain range, terraces, a canyon with a river, scattered
  boulders, a ring of trees, a flat paved base, a noise landscape); scripts are saved in the data
  folder's scripts folder. The program file is about 20 MB larger (the C# compiler).

### Changed
- The editor's download no longer holds the WorldEditorBridge plugin: it is its own download,
  `WorldEditorBridge-<version>.zip`, on the same releases page, and on Thunderstore and Hexium.

## v1.0.1 — 2026-10-08

### Fixed
- Linux: the 3D view and the map stayed blank without a GPU driver (Mesa's software OpenGL,
  llvmpipe) and in VMware virtual machines: Avalonia turned OpenGL off for those renderers.
- Leaving a live world closes its connection to the game.

### Changed
- Every push is checked further: the code is analysed (no .NET analyzer warning allowed), and the
  Linux and Windows packages are started and must draw the test world.

## v1.0.0 — 2026-10-08 (the native app replaces the web editor)

### Added
- Native app: the game's look (terrain textures, map textures and models) is copied from your own
  Valheim on first start and again after a game update, as the web editor did. The start page
  shows the copy's progress, asks for the Valheim folder when it is not found, and offers Try
  again when it failed. Choosing another game folder in Settings checks it again.
- Native app: every control explains itself when you hover it, as in the web editor; a row's label
  tells the same.
- Native app: a log (log.txt in the data folder, as the web app kept), the window's icon, the
  version in the title, and a Documentation link on the start page.
- Native app: a world folder (`ValheimWorldEditor <folder>`) or a live game (`--live <url> --token
  <token>`) given on the command line opens straight away, as with the web app.
- Native app, editor: Discard in the top bar undoes the steps not saved (or applied) yet and keeps
  the rest of the history; changes without a step here are discarded by reading the world again.
- Native app, live: Reload in the editor's top bar, and the Auto switch that applies every change
  to the game at once (remembered).
- Native app, live: the players are shown in the 3D view with their names, and View has "Go to" a
  player.
- Native app, editor: move the area by one zone with ◀▲▼▶, or let it Follow the view near its edge
  (remembered); Follow waits while a selection, stroke, path or apply would be lost.
- Native app, editor: a note says how many locations are in or near the area.
- Native app, Select: saved selections. Keep the selection under a name, then select it again or
  add it later, also after saving the world (per world, found again by kind and place).
- Native app, Area and Select: "pick" next to Replace's list takes the kind from an object clicked
  in the world.
- Native app: the editor remembers its choices between runs, as the web editor did: the View
  switches, the map's place, the brush's shape and falloff, the Area action, the Shape preset and
  your own formula, Select's options, the open right-hand panel and the clipboard.
- Native app: the status bar shows where the mouse is (x/z), the ground's height against the
  original (and the ±8 m limit) and the zone.
- Native app, View: Game look on/off, See-through buildings, 3D resolution (Sharp, Balanced, Fast),
  and Defaults / All / Ground presets.
- Native app: `[` `]` change the brush size; H picks the View tool; Space + left drag and Shift +
  right drag slide the view in any tool; Alt + wheel turns the selection, the paste or the Place
  preview; the help lists every key.
- Native app, Naturalize: New pattern (another noise pattern for the next stroke or natural path).
- Native app, Area: cut and fill follows every change of the ground and of the action.
- Native app, Place: **fit** sets the spacing when a chosen kind is wider than it (the panel says
  so).
- Native app, Place chooser: Shift + click on Pick from world adds the kind, and on a favourite or
  recent places only it; Untick all; all / none per row; categories fold, and which are open is
  remembered.
- Native app: new objects not saved (or applied) yet carry a green dot in the view.
- Native app, View: **Unsaved marks** hides the green dots on new objects (remembered).
- Native app, View: **Limit marks** hides the red on ground at the game's ±8 m limit (remembered).
- Native app, Path: before a click, a circle under the cursor shows the path's width and soft edge,
  and the stretch the next point adds; the ground the path covers is tinted.
- Native app, Select: the object under the cursor is outlined faintly before a click picks it.
- Native app, Place: the brush's outline shows under the cursor (Brush mode).
- Native app, View: **Tamed animals** (boars, wolves, lox, hens, deer, necks… that players tamed,
  with their models) and **Runestones**, both shown at first. Runestones can be picked and deleted,
  offline and live; they cannot be moved or copied. The game's look is copied again once for their
  models.
- Native app: placing or pasting a kind switched off in View switches it on, and says so.
- The developer checks of the world generator and the save writer (`--verify`, `--verify-ingame`,
  `--selftest-save`, `--inspect`, `--summary`) moved to a separate tool, `tools/WorldCheck`. It can
  also write a new world from a seed (`create`), its middle generated with the game's vegetation,
  and with `--flat` keep its most even zone of dry Meadows bare (the documentation's pictures).

### Fixed
- Native app: after a visit to the map, the 3D editor drew its models without textures (trees as
  big white and blue leaf cards): the textures being read for the view's old OpenGL context were
  never read again for the new one.
- Native app, Area: after choosing a backup folder with "Another folder…", the folder picker opened
  again and again; a folder chosen by hand was also dropped for live worlds.
- My game: on Linux, mod manager profiles were listed and searched twice.
- A character file that cannot be read no longer stops worlds from opening.

- Native app: in a short window the View panel and the tool panels ran under the status bar and
  their last rows could not be reached; they now stop above it and scroll.
- The data folder follows `XDG_DATA_HOME` on Linux even before that folder exists (it fell back to
  `~/.local/share`).
- Native app, start page: empty error lines and an empty server list left gaps; the data folder is
  shown as `~/…`.
- Native app: Space no longer presses the focused button in the editor.
- Native app, Place: Esc cancels Pick from world.
- Native app, Area: "pick" for Replace now works before an area is drawn.
- A character with a backup copy of its file (`ragnar_copy.fch`) could be listed under the copy's
  name, depending on the disk: files are now read in name order, and the original is kept.
- Native app: folders under the home are shown as `~/…` everywhere (saving, backups, blueprints,
  Settings), so no user name is on screen.
- Native app, Settings: the dialog is as tall as what it holds (it had a large empty part).
- Native app: the tools' lines (brush circles, path, area, measure, selection boxes) broke into faint
  dashes when the 3D view ran below the screen's resolution; they are now drawn a few pixels wide.

### Removed
- The web editor (the page in a WebView2/WebKitGTK window or the browser): the native app replaces
  it, with every feature it had. Its JavaScript Script console is not carried over (C# scripts may
  come later).
- Native app: the development command-line options (pictures, scripted tools); `--world`,
  `--zone` and `--size` remain.

## v0.41.1 — 2026-10-07

### Fixed
- Apply live: an error answered by the game (the plugin refusing a call) is reported as "Could not
  apply live" instead of failing; the changes stay waiting.
- Apply live: when the game refused the objects, deletions were already counted as sent and were
  never sent again. Now nothing counts as applied until the game accepts it.
- Apply live twice at once (a double click) could create the same new objects twice in the game.
- Native preview, Select: Drop (End) right after selecting an object above another one put it too
  low, by as much as it floated above the ground.
- Native preview, Place: a double-click on the last point of a line or zone did not place it (the
  click was taken for dragging that point).
- Native preview, map: the 1 m close-up never showed when zoomed in (its place was passed to the
  shader as whole numbers, which OpenGL refused); the coarse whole-world map was drawn instead.
- Native preview: after going between the map and the 3D editor, both kept OpenGL objects from the
  view's previous context and drew with them (errors, or the wrong buffers); they are rebuilt now.
- Native preview, Place: a circle of walls end to end came out one wall short (a gap), the last
  one falling a hair past the end of the ring through rounding.
- An object whose data is edited or moved keeps the game's short position form (the terrain
  compiler grew 8 bytes when written back).

## v0.41.0 — 2026-10-07 (plugin settings file renamed)

### Changed
- The editor finds the plugin's settings under either name (your game, and the server's Valheim
  folder over SSH), so it works with older plugins too. Every hint and guide names the new file.

### WorldEditorBridge
- The plugin's id is now `Tie.WorldEditorBridge`, so its settings are in
  `BepInEx/config/Tie.WorldEditorBridge.cfg` (was `local.worldeditorbridge.cfg`). On its first start
  the plugin moves the old file to the new name: the port and token are kept, and saved servers in
  the editor keep connecting.

## v0.40.1 — 2026-10-07

### Changed
- The frame rate log is written only on demand: **Record frame rates** in Help, off each time the
  editor opens.

## v0.40.0 — 2026-10-07 (3D resolution)

### Added
- **3D resolution** (View, Look): Sharp (the screen's own pixels), Balanced (one per screen point:
  a quarter of the pixels on a screen scaled ×2) or Fast. Fewer pixels give more frames per
  second; the frame rate logs say which one was used.

## v0.39.0 — 2026-10-07 (frame rate log)

### Added
- **Frame rate log**: a line every 0.2 s while the 3D view is used (camera moving, or still while
  something else happens), with frames per second, the editor's work per frame and the longest gap
  between frames, in `perf-window.log` (the app's window) or `perf-browser.log` (`--browser`) in the
  data folder, to compare the two. Header lines give the browser, the view's size and what is loaded.

## v0.38.3 — 2026-10-07

### Fixed
- The frame rate line in Help could not be selected to copy it: it was rewritten several times a
  second, which dropped the selection. It now stays as it is while selected.

## v0.38.2 — 2026-10-07 (frame rate in Help)

### Added
- **Help** (`?`) shows the frame rate while the view moves, and how many milliseconds of work each
  frame takes, to tell a slow graphics path from a busy editor.

### Fixed
- Help said "the graphics card" for the app's own window on Linux and Windows: WebKit answers "Apple
  GPU" whatever draws the view, so the window now says it does not tell.

## v0.38.1 — 2026-10-07 (fixes found by new tests)

### Fixed
- Area tool: after Undo or Redo, the counts of what is inside the selection and the Replace list
  still showed what was there before (the Replace list could be empty while trees were back).

## v0.38.0 — 2026-10-07 (lighter 3D view)

### Changed
- The 3D view is drawn only while something happens (the view moves, a stroke, the mouse or keys
  in use) and a few times a second otherwise (enough for the game look's water and sky): an idle
  editor leaves the processor alone instead of drawing 60 times a second.

### Added
- **Help** (`?`) says what draws the 3D view: the graphics card, or the processor (llvmpipe,
  SwiftShader), which is much slower; the editor says so once when it is the processor.

## v0.37.0 — 2026-10-07 (pieces that snap, elevation)

### Added
- **Snap to pieces already there** (Place tool, building pieces): a wall, fence or floor locks onto
  the pieces around it like the hammer, **Beside** (end to end, at their level) or **On top** of
  the piece under the cursor, facing like it (Rotation turns it in quarter turns). Pieces are placed
  one at a time under the cursor.
- **Elevation** for everything placed: on the ground, a height **above the ground**, or **at one
  height** for all; `PgUp` / `PgDn` change it, `Alt` + click takes the top of a piece or the ground.
- **Layers** (Line, End to end): a line of walls or fences stacked several pieces high.
- A line started next to a piece already there continues it, at its level.

### Fixed
- Walls and other pieces with their origin in the middle were half sunk into the ground when placed
  one by one: they now stand on their bottom.

## v0.36.0 — 2026-10-07 (tidier tool panels)

### Changed
- **Tool panels** show the settings you use most, and fold the rest under one line at the bottom
  that says what is inside (shape, falloff, stamp, curve, clumping, tilt, facing...), open or closed
  as you left it. The **Mask** moves into that fold, which says "mask on" while it is on.
- **?** next to the tool name shows what the tool does and how to use it; the how-to text is hidden
  otherwise (in every tool, until you click ? again).
- **Area**: one **Action** list (Ground, Objects inside, Copy, Bring back) instead of six sections.
  The panel shows only that action's settings, and one button at the bottom does it (`Enter` too).
- **Place**: the panel lists the chosen kinds with their weights and a ✕ each; **+ Add kinds** opens
  the list of every kind (search, favourites, recent, Pick from world) in a drawer beside the panel.
- **Select**: Delete, Whole building, Same kind, Invert and Exact place first; Replace with, saved
  selections, Inspect data and Make player built under **More actions**; the moving switches under
  **Moving**.
- **Path**: Soft edge, curve and natural look are folded; Apply and Clear stay at the bottom.

## v0.35.0 — 2026-10-07 (script console)

### Added
- **Script console** (Script in the top bar, like MCEdit's filters): a few lines of JavaScript with
  a small API (find, add and remove objects, read and set the ground, work inside the Area
  selection) for jobs the tools do not cover. Each run is one undo step. Comes with examples (count
  kinds, remove young beeches, rocks on steep slopes, terraces); your own scripts are kept in this
  browser. See docs/script.md.

## v0.34.0 — 2026-10-07 (cut and fill)

### Added
- **Cut and fill** in the Area tool: how much ground inside the selection has been raised and dug
  since it was generated, and what Flatten, Raise or Lower would dig and fill (m³) before you click,
  with what the ±8 m limit leaves out.

## v0.33.0 — 2026-10-07 (overlays)

### Added
- **Ward areas**, **Build ranges** and **Location flattening** in View, Overlays: rings on the
  ground where wards protect, where crafting stations let you build (by kind: 10 to 40 m), and where
  locations flatten the ground.
- **Go to** a player (View, live mode): right there, or the area moves to them.

### Fixed
- After Undo or Redo in the Place tool, its spacing still counted the objects as they were before.

## v0.32.0 — 2026-10-07 (walk and fly)

### Added
- **Walk** and **Fly** views (`F`): see the world from a player's eyes, 1.8 m above the ground (or the
  water), and walk it with WASD (Shift runs); `F` again flies (Space up, C down); `F` again goes back
  to the usual view. Tools keep working, and Follow and the Area arrows keep the view.

## v0.31.0 — 2026-10-07 (area that follows the view)

### Added
- **Follow** (next to the Area arrows): when the point you look at comes within 12 m of the edge of
  the area, the area moves there by itself. It waits, and says why, while a stroke, a path or Place
  shape, an Area selection or selected objects would be lost.

### Changed
- Moving the area (arrows or Follow) keeps the view on the same spot and the tool in hand, instead
  of starting over above the middle with the Raise tool.

## v0.30.0 — 2026-10-07 (history survives reloads)

### Changed
- **History** survives a reload of the page and a move of the work area: the editor keeps it with the
  pending changes. A change that touched ground outside the new area is left out, with the ones
  before it. Saving, Discard and reloading from the game start a new history, as before.

### Fixed
- A long message in the status bar wrapped onto several lines and covered the last buttons of the
  tool panel, which then could not be clicked. It now keeps to two lines (the whole text on hover).
- "Switched on … in View" replaced what the tool had just said (how many objects were placed or
  regrown); it is now added to it.
- Regrow nature offered kinds the page cannot place, and then reported them as regrown.

## v0.29.0 — 2026-10-07 (regrow nature)

### Added
- **Regrow nature** (Area tool): puts back the game's own vegetation inside the selection, for the
  kinds chosen with the chips: the trees, rocks, bushes and pickables its rules grow there (how
  many, on which slopes, heights and biome edges, in groves), worked out like the game does
  (its rules and random draws, per zone), on the ground as it is now. Spots where something
  already stands are skipped, and so is the ground near buildings. On a world the game generated,
  most of what it would place lands exactly where the game put it.

## v0.28.0 — 2026-10-07 (clumping)

### Added
- **Clumping** and **Patch size** in the Place tool (Brush, Zone): objects gather in groves with
  clearings between them, following a noise pattern fixed to the world, instead of an even spread.
  Presets keep them (Meadows woods, Black forest, Berry patch and Meadows rocks use some).

## v0.27.0 — 2026-10-07 (weighted mixes and presets)

### Added
- **Weights** in the Place tool: with several kinds ticked, a slider per kind sets how often it is
  used (Beech 3, Birch 1: three beeches for one birch), with its share in %.
- **Presets**: load a mix (kinds, weights, Density, Spacing, Size, Tilt, facing) from built-in looks
  (Meadows woods, Black forest, Swamp, Berry patch, Forest floor, Meadows rocks) or save your own.

## v0.26.3 — 2026-10-07 (spacing that fits)

### Added
- **Place tool**: when a ticked kind is wider than the spacing, the panel says so and **fit** sets
  the spacing to it. A blueberry bush is about 3 m wide: a 1 m grid of them was one big hedge
  instead of rows.

## v0.26.2 — 2026-10-07 (editable zone points)

### Added
- **Place tool, Zone**: drag a point to move it, drag the outline to add a point, Ctrl + click a
  point to remove it (as with lines). The hints above Place say how, for lines too.
- **Remove last point** button (Zone and Line), the same as Backspace.

### Changed
- **Leave saplings and crops room to grow** also shows for grown crops (Pickable_Carrot...) and for
  trees that grow from saplings (Beech1, Oak1...): they keep the room their sapling needs (read
  from the game's saplings).
- **Zone** fills at random only: its Scatter / Grid choice is gone, Grid mode makes grids.
- **Grid** gets as many whole cells as fit best in the box you drag (at least one), centred: a box
  a little short of N cells used to lose a row, a small box got nothing. Its **Cell** slider is now
  called **Spacing** (space between objects).

## v0.26.1 — 2026-10-07 (placed objects follow View, chip toggles, Nobody builds)

### Changed
- Objects you placed follow the View switches like the others (before, they stayed shown until
  saved): switching off Trees & logs hides the trees you just planted too.
- A click on a **Favourite** or **Recent** kind ticks or unticks it (before, it placed only that
  kind, which Shift + click now does), and each row has **all** / **none**.

### Added
- **Untick all** in the Place tool, next to Pick.
- **Nobody (not player built)** in Built by: places pieces without a builder.

## v0.26.0 — 2026-10-07 (Place tool)

### Changed
- The **Plant** tool is now called **Place** (still `T`): it places any kind of object, walls and
  fences end to end along lines, circles and rectangles as well as trees and rocks with a brush.
  Its guide is now docs/place.md.
- **Leave saplings and crops room to grow** only shows (and only applies) when a sapling or crop is
  ticked.

## v0.25.0 — 2026-10-07 (player-built pieces)

### Added
- **Built by** (View panel, Building): the player written as the builder of the pieces you place.
  It lists the world's builders (the one with the most pieces is chosen at the start), the players
  named on beds, wards and tombstones, and the characters on this computer; any other player id can
  be typed. Remembered per world.
- **Make player built** (Select tool): gives selected pieces that have no builder the chosen one.

### Fixed
- Pieces placed by the editor were not always **player built**: kinds the world had none of, and
  copies of pieces from ruins, had no builder, so the game took them for ruins (a third of the
  materials back, no base for fires, ignored by raids, wards and private chests owned by nobody).
  Now every piece of a kind the hammer, hoe, cultivator or serving tray can place gets the chosen
  builder when it is placed, pasted or built. Moved and edited objects keep their builder.

## v0.24.0 — 2026-10-07 (eyedropper and favourite kinds)

### Added
- **Eyedropper**: the Plant tool's **Pick** button and the **pick** buttons next to the Replace lists
  (Select tool, Area tool) take the kind of the object you click in the world.
- **Favourites** and **Recent** kinds in the Plant tool: star kinds with ☆ in the list, and the last
  eight kinds you placed come back as buttons above the list. A click plants only that kind, Shift +
  click adds it to the ticked ones.

### Changed
- The Select tool's Replace row reads "Replace with", and its button is now "Replace the selection".

## v0.23.0 — 2026-10-07 (sharp edges)

### Added
- **Sharp edge** brush falloff: full strength right to the brush's edge and nothing beyond, for
  steep walls like the game's pickaxe.

### Changed
- With **Soft edge 0**, Path and Area actions give the ground points their edge crosses a share of
  the change, so a diagonal edge follows the line straight instead of stepping from point to point.

## v0.22.0 — 2026-10-07 (fine-tune drawn lines)

### Added
- **Fine-tune drawn lines** (Path tool, and Plant lines): drag a point to move it, drag the line to
  add a point there, Ctrl + click a point to remove it; the preview follows.

## v0.21.2 — 2026-10-07 (buildings keep their shape)

### Changed
- **Moving a building "on the ground"** keeps its shape: its pieces move as one block, and the block
  goes up or down until its bottom layer sits on the ground (sinking into a slope rather than
  floating). Trees, rocks and other objects in the selection still land on the ground one by one.
  Before, every piece was put on the ground by itself, pulling buildings apart.

## v0.21.1 — 2026-10-07 (ring brush outline)

### Changed
- The Ring brush shape draws its inner edge too, so the untouched middle shows on the ground.

## v0.21.0 — 2026-10-07 (exact moves and snapping)

### Added
- **Snap to other pieces** (Select tool, on by default): while moving, a piece's snap point locks
  onto the nearest snap point of a piece around it, so walls, floors and fences join like with the
  hammer.
- **Turning ring** around the selection: drag it to turn, Ctrl for 15° steps.
- **Exact place**: type the selection's position and turn, or how far to move and turn it.

### Fixed
- "Put each object on the ground" put a piece's middle on the ground for pieces whose origin is in
  the middle (walls), sinking them; their bottom rests on the ground now.

## v0.20.5 — 2026-10-07 (pieces follow the line)

### Changed
- **End to end switches itself on** in the Plant tool's Line mode when every ticked kind is a piece
  the game snaps (fences, walls, stakes...), and off for other kinds, until it is changed by hand.
- **Objects follow the line**: circles and rectangles always turn their objects along the outline,
  and "Follow the line" (before: Face along the line) is on by default for drawn lines. A piece now
  lies along the line (its length, not its front, follows it).

## v0.20.4 — 2026-10-06 (dropdown boxes)

### Fixed
- The closed dropdown boxes were drawn by the system theme (a light box in the app window on Linux)
  under the editor's light text, so the chosen value could not be read: the editor draws them
  itself now, dark, with its own arrow.
- A dropdown followed by a button (Replace … go, Paint … apply, Saved … keep) pushed the button onto
  the next line.

## v0.20.3 — 2026-10-06 (readable dropdowns)

### Fixed
- Dropdown lists could be unreadable (pale text on a pale background), depending on the system
  theme: the pages now declare themselves dark, so the lists are drawn dark, with the chosen entry
  highlighted in the editor's amber.

## v0.20.2 — 2026-10-06 (dropdown labels)

### Fixed
- In the app window on Linux, the group headings of the Replace lists ("Trees & logs"...) could show
  blank, with GTK warnings in the terminal: they read "Trees and logs" and so on now.

## v0.20.1 — 2026-10-06 (fences on slopes)

### Changed
- **End to end on slopes**: every piece stays level and stands on the ground where it is, a little
  higher or lower than the last one and touching it, like a fence going up a hill (before, each one
  sat on the lower of its two ends, which could leave it floating at the middle of a bump).

### Fixed
- End to end placed nothing at all when one of the ticked kinds had no known length yet; such kinds
  are now left out and the others are placed.

## v0.20.0 — 2026-10-06 (fences end to end)

### Added
- **End to end** (Plant tool, Line mode): pieces placed so that each one starts where the last one
  ends, turned along the line, like the game's hammer snaps them, from the pieces' own snap points
  (now in the build-piece catalogue) or the model's length for other kinds.
- **Circle and Rectangle lines** (Plant tool): press at the centre or a corner and drag to the size;
  **Close the loop** for drawn lines. With End to end the sizes snap to whole pieces, so a fenced
  ring or yard closes exactly.

## v0.19.2 — 2026-10-06 (moved objects on the ground)

### Changed
- **Moved objects land on the ground** (Select tool): each moved or turned object stands on the
  ground where it ends up. A new switch, "Put each object on the ground when moving" (on by
  default), turns it off to keep each object's height above the ground, as before (for buildings).

## v0.19.1 — 2026-10-06 (area selection)

### Added
- **Clear** in the Area tool, next to Box and Polygon, to start a new selection (as `Esc` does).

### Fixed
- The Area tool's selection outline stayed on screen in every other tool; it is only drawn in the
  Area and Paste tools now (the selection is kept for when you come back).

## v0.19.0 — 2026-10-06 (selection helpers)

### Added
- **Same kind, Invert and saved selections** (Select tool): select every object of the selected
  kinds, the opposite of the selection, or a selection kept by name for the world.

## v0.18.0 — 2026-10-06 (select a whole building)

### Added
- **Select a whole building** (Select tool, like Axiom's magic select): double-click a piece, or
  press Whole building, to select every piece connected to it.

## v0.17.0 — 2026-10-06 (river and canal)

### Added
- **River / canal** (Path tool): digs a U-shaped bed below sea level with sloped banks, so the sea
  flows in.

## v0.16.0 — 2026-10-06 (shape tool)

### Added
- **Shape** (tool `G`, like WorldEdit's `//generate`): one click puts a mound, cone, mesa, crater,
  moat, bowl or ridged hill into the ground, or any shape written as a formula.

## v0.15.0 — 2026-10-06 (erosion)

### Added
- **Erode** (sculpt tool `O`, and an Area action): thermal erosion settles slopes to a rest angle,
  water erosion cuts gullies and fills hollows.

## v0.14.0 — 2026-10-06 (heightmaps)

### Added
- **Heightmaps** (Area tool, like WorldPainter): export the area's ground as a 16-bit grayscale PNG
  and import a picture back into the selection or the whole area, between chosen heights.

## v0.13.0 — 2026-10-06 (stamps)

### Added
- **Stamps**: pictures as brush shapes (built in: mountain, mesa, crater rim, dunes, rocky ground;
  or any picture loaded from a file), and "Stamp once" to put a whole stamp into the ground with one
  click, to a chosen height.

## v0.12.0 — 2026-10-06 (brush shapes)

### Added
- **Brush shapes and falloff** (sculpt and paint tools, like WorldPainter): circle, square, ring or
  ragged brushes, with a smooth, linear, dome, flat-top or peak falloff; square brushes turn.

## v0.11.0 — 2026-10-06 (restore from a backup)

### Added
- **Restore from a backup** (Area tool, like WorldEdit's `//restore`): the ground and objects of a
  selection as they were in one of the world's backups (the editor's or the game's), objects with
  all their data.

## v0.10.0 — 2026-10-06 (reset zones across the world)

### Added
- **Reset zones across the world** (map page, like MCA Selector): pick zones by biome, distance
  from buildings, ground edits and distance from the centre, and mark them all for the game to
  generate again.
- **Zone resets in live mode** (needs WorldEditorBridge 0.10.0 or newer): Apply live resets the
  marked zones in the running game, which generates them again at once where players are.

### Fixed
- A zone reset also removed players' tombstones, with what they carried: they are always kept now.
- Saving twice within the same second failed, because both backups got the same folder name.

### WorldEditorBridge
- New: `POST /zones/reset`, so the editor can reset zones in the running game (they are generated
  again at once where players are). Players' tombstones are always kept.

## v0.9.0 — 2026-10-06 (search the world)

### Added
- **Search the world** (map page): every object of a kind, every container holding an item, or every
  sign, portal or ward whose text matches, counted and pinned on the map; a result opens the 3D
  editor with that object selected.

## v0.8.0 — 2026-10-06 (object inspector)

### Added
- **Object inspector** (Select tool, `I`): everything an object holds in the save, by name, and
  editing it: chest contents (items, stacks, quality, slots), sign texts, portal tags, ward
  permissions, timers... An edit is one undo step and is saved or applied live like any change.

### Fixed
- Objects placed or moved in this session lost what they were copied from after the page was
  reloaded, so moving one again made a fresh copy (a moved chest lost its contents).

## v0.7.0 — 2026-10-06 (repeat a paste)

### Added
- **Repeat a paste** (like WorldEdit's `//stack`): one click places several copies in a row, along
  the copy's width or depth or stacked upwards, with a gap; one undo step.

## v0.6.0 — 2026-10-06 (PlanBuild files)

### Added
- **PlanBuild and .vbuild files**: import a PlanBuild `.blueprint` (its terrain marks become ground)
  or a `.vbuild` file into the blueprint list, and write any blueprint back out in either format.

## v0.5.0 — 2026-10-06 (blueprints)

### Added
- **Blueprints** (Area tool → Copy & paste): save the clipboard as a named file, browse the saved
  ones with a picture of each, and paste them into any world. They are kept in the `blueprints`
  folder of the editor's data folder.

## v0.4.1 — 2026-10-05 (editor on GitHub only)

### Changed
- **Thunderstore carries only the plugin** (`Tie-WorldEditorBridge`): Thunderstore does not host
  programs and rejected the `ValheimWorldEditor_Windows` and `_Linux` packages. The editor is
  downloaded from the GitHub releases page, which the plugin's mod page now points to.

## v0.4.0 — 2026-10-05 (clean-up)

### Fixed
- Leaving the world map with **Worlds** while it was still loading could raise an error on the
  page (a cut-off map reply was read as data).

### Changed
- Unused code removed, and the origin notes of the files ported from the game corrected.
- Development: the old one-off test scripts are gone (the CI tests replace them), and the browser
  tests wait for the 3D view to be drawn instead of fixed pauses, so they no longer fail at random
  on a busy machine.

## v0.3.3 — 2026-10-05 (Thunderstore dependency)

### Changed
- **Thunderstore**: the editor packages no longer carry their own copy of the plugin: they depend
  on `Tie-WorldEditorBridge`, which mod managers install with them (one copy per profile; plugin
  fixes no longer need a new editor package). The GitHub downloads keep it in `plugin/`.
- The start page's plugin steps point to WorldEditorBridge on Thunderstore for mod manager users.

## v0.3.2 — 2026-10-05 (smaller packages)

### Changed
- **Only what the editor needs**: the bundled Python keeps just the files a full game-look export
  uses (traced on both systems; the export's output is byte-for-byte the same): 117 → 87 MB on
  Linux, 74 → 48 MB on Windows. Gone with it: `requirements.txt`, the Linux copy of `icon.ico`,
  and in the Thunderstore packages the `README.txt` files (the mod page replaces them).

### Fixed
- **Windows**: the bundled Python carries `msvcp140.dll`, which UnityPy needs; a Windows without
  the Visual C++ runtime could not copy the game's look.

## v0.3.1 — 2026-10-05 (Thunderstore packages)

### Added
- **The editor on Thunderstore**: `ValheimWorldEditor_Windows` and `ValheimWorldEditor_Linux`, the
  editor with the plugin, for r2modman and Thunderstore Mod Manager; `WorldEditorBridge` stays as
  the plugin alone, for servers.
- Installed by a mod manager, the editor finds its own profile for **My game**.

### Fixed
- Linux: the game-look export runs even when the bundled Python lost its run bit (mod managers
  unpack without permissions). The release no longer holds the `python`/`python3` links.

## v0.3.0 — 2026-10-05 (Thunderstore)

### Added
- **WorldEditorBridge on Thunderstore**: install the live-editing plugin with r2modman or
  Thunderstore Mod Manager; BepInEx comes with it.

### Changed
- **One version number** for the editor and the plugin (0.3.0, the plugin's version until now),
  kept in the `VERSION` file. The plugin DLL's file version said 0.1.0; it now says 0.3.0 too.

### WorldEditorBridge
First release on Thunderstore; from here on the plugin has the editor's version number: use the two
together.
- **Objects live**: deleted, planted, pasted and moved objects are applied in the running world,
  and undoing after applying takes them back in the game too.
- **Ground live**: terrain edits (sculpt, paint, roads, areas) are sent to the game and shown to
  every player at once.
- **World snapshot**: the editor opens the running world as the game has it now, without a save.
- Listens on `127.0.0.1:5182` only, with a random token in `BepInEx/config/local.worldeditorbridge.cfg`.


## v0.2.0 — 2026-10-05 (desktop app)

Download, unpack, double-click: no command line, no Python, nothing to install.

### Added
- **Its own window** on Windows and Linux (the system's web engine; the browser as fallback, or
  with `--browser`), with an icon.
- **Start page with three ways to edit**, each clearly separate:
  - **My game (live)**: finds Valheim running with the plugin (also in r2modman / Thunderstore Mod
    Manager profiles) and reads its token from your own game's settings; one click to edit live,
    or the steps that are missing (BepInEx, the plugin, starting the game).
  - **A dedicated server (live)**: the editor logs in over SSH (password or key, no `ssh` program
    needed) and makes its own tunnel to the plugin. The plugin token is always typed by you.
    Servers are saved in `servers.cfg` after the first connection for one-click access, with the
    password only when "Save password" is ticked; the server's SSH identity is remembered and a
    change is refused.
  - **A saved world (offline)**: the worlds found on the computer (Valheim's usual folders, Proton
    included), recently opened ones, and any other folder (with a folder dialog).
  **Worlds** on the map page goes back to the start page.
- **Settings** for folders that are not in the usual place: the Valheim game folder, extra BepInEx
  folders or mod manager profiles, and world folders that are always listed. For a server, an
  optional **Server's Valheim folder**: the plugin's port is read from it, and when the plugin does
  not answer the editor says exactly what is missing there.
- **Automatic game look**: the editor finds Valheim in the Steam libraries and copies the game's
  textures and models from it by itself, with progress on the page; it asks for the folder when
  Valheim is not found, and copies again after a Valheim update. The copy lives in the per-user
  data folder, next to the settings and a log.
- **One download per system**, each with the plugin in `plugin/` and instructions for that system.

### Changed
- The program is now called `ValheimWorldEditor`. It picks the first free port from 5180, so a
  second copy or another program never blocks it.
- The game-look exporter comes with its own small Python runtime; its texture fixes need only
  Pillow now (same visible result).

### Fixed
- Connecting to a server that does not answer gave up only after 3 minutes; the editor now checks
  the bridge first (at most 10 seconds) and says what is wrong: no tunnel, wrong token, or a game
  that does not host the world.
- The app quits cleanly when the system asks it to (logout, shutdown), closing its window.

## v0.1.0 — 2026-10-05 (first release)

Everything below, packaged: the editor for Linux and Windows (x64), and the WorldEditorBridge
server plugin (0.3.0) for live mode. Game files are not included.

### Added
- **Release packages** (`tools/release.sh`): program, web page and export tool, without source
  code or game files.
- **`export_all.py`**: one command that copies the game's terrain shader and textures, map textures
  and the models of every kind of object from your own Valheim install (a few minutes). Replaces
  the separate scripts that had paths of the development machine written in.

### Changed
- Models are now exported for every placeable kind (about 800 world objects plus 689 building
  pieces), not only the kinds one world happened to contain, so the Plant tool shows real models
  for far more kinds.

## 2026-10-05 — Placing, selecting and moving

### Added
- **Hover help on every control**: each slider, switch, button and list in the editor and on the map
  explains exactly what it does; tool buttons also show what their tool does.
- **Documentation**: a README with installation for offline and live mode, a page per feature in
  `docs/` with screenshots, and this changelog.
- **Select inside a zone.** In the Select tool, drag on empty ground (or Alt + drag anywhere, for
  forests) to draw a zone; everything shown inside it is selected. Shift adds to the selection.
- **Place any game object.** The editor now knows every network object in the game (about 1,500
  placeable kinds), so you can plant or place things the world has none of yet, such as turnips.
  Such objects are created exactly like the game creates a freshly placed one.
- **Leave saplings room to grow** (Plant tool, on by default). Saplings and crops are never placed
  closer than their in-game grow radius to anything; the panel also reminds you when a crop needs
  cultivated ground.
- **Drop to surface** (Select tool, `End`): drops the selection onto the object below it, or onto the
  ground when there is none.
- **Move arrows** (Select tool): red X, green Y, blue Z arrows at the selection; drag one to move along
  that axis only, Ctrl snaps to 0.5 m.
- **Plant: Line, Grid and Zone modes.** Place objects every N metres along a drawn line (optionally
  smoothed, facing along it, with sideways wiggle), one per cell of a dragged box, or fill a freely
  drawn zone by scatter or by an orderly grid. Zones and grids can be turned as a whole.
- **Foldable View panel.** Click a section title (Look, Nature, Spoilers, Overlays) to fold it; Look
  starts folded and your choice is remembered.

### Changed
- **Rotation in 1° steps everywhere** (`,` `.` and Alt+wheel), 15° with Shift: Select, Plant brush,
  Plant zones/grids and Paste. Paste can now be turned to any angle, not only quarter turns.
- **Flatten levels to the height where the stroke starts**, instead of a fixed 35 m that made it dig
  like Lower on higher ground. A fixed height is still available (untick the option, or Alt + click).
- **The Mask judges the ground as it was when the stroke started**, so raising past the mask's height
  limit or changing the slope no longer stops a stroke half-way. Settings that let nothing through
  (like a 0° maximum slope) are flagged, and an empty stroke says the Mask left everything out.
- Placing a kind whose View switch is off now switches it on, so what you placed stays visible after
  a reload.
- The move arrows are smaller and leave the object's middle free for a free drag.
- The editor tells the browser to revalidate its page files on every load, so an update is used
  right away instead of an older cached copy.

### Fixed
- With Game look off (or without the extracted game files) the world's objects were not loaded, so
  nothing could be planted, placed or saved as an object; they now always load.
- The world map no longer fails to load when the map textures are missing; it uses plain colours.
- Pressing a second key (End, PgUp, turn) right after a move acted on the hidden original instead of
  the moved copy.

## 2026-10-05 — History, discard and live objects

### Added
- **History panel** (`L`): every change with its time; "Back to here" rolls back to any point, and
  "Remove" takes out a single change while keeping everything done after it.
- **Move, turn, lift, copy and paste selected objects** in the Select tool (drag, `,` `.`, PgUp/PgDn,
  Ctrl+C / Ctrl+V).
- **Plant preview**: see-through "ghosts" show exactly what a click will place, for every kind; `R`
  rolls a new layout, Alt+wheel turns it; single placement mode; categories fold.
- **Objects live** (WorldEditorBridge 0.3.0): deleted, planted, pasted and replaced objects are
  applied to the running game; undo works after applying.
- **Ground live** (WorldEditorBridge 0.2.0): "Apply live" and "Auto" send ground edits to the game.
- **Live mode** (WorldEditorBridge plugin): the editor can open the running world of a server,
  with its players, instead of a save on disk.

### Changed
- Copies, plants and pastes create **fresh, independent objects**: a copied chest is an empty chest;
  moving an object keeps all of its data.
- **Discard** undoes only the changes that are not saved or applied yet, in place, without reloading
  the page (tool, view and zoom are kept).
- The pending counter only counts zones that really differ from their saved state, so undoing back
  to the start clears it.
- Objects you placed but have not saved yet are always drawn (with a green marker), whatever the
  View switches say.

### Fixed
- Raspberry bush leaves disappearing in the plant preview and with see-through buildings.
- The brush ring and other moving overlays disappearing when zoomed in close.
- A few kinds (Bush01, shrub_2 and others) drawn as boxes because the wrong model was matched.

## 2026-10-05 — World-editor tools

### Added
- **Masks** for brushes, paths, area actions and planting: by biome, height range, slope range and
  paint.
- **Area tool** (`B`): box or polygon selection; flatten, raise, lower, smooth, naturalize, restore or
  paint the ground inside; remove, select or replace objects inside; copy and paste ground and
  objects (turn and mirror); reset zones so the game generates them again.
- **Plant tool** (`T`): paint trees, rocks, bushes and more onto the ground with density and spacing;
  Shift + drag removes the chosen kinds.
- **Measure tool** (`M`): distance, height difference and slope between two points; slope colours
  and height lines in View.
- **Delete objects** (Select tool, `Del`), and a reworked interface: top bar, tool rail, tool panel
  and View panel.

## 2026-10-05 — In-game look

### Added
- The 3D editor draws the world like the game: the game's own terrain shader and textures, water
  and sky, real models for buildings, trees, rocks and bushes, and show/hide switches per kind
  (spoilers such as ore and ruins are off by default).

## 2026-10-04 — First versions

### Added
- **World map** in the browser, drawn with the game's own map shader, with player buildings, painted
  ground and edited zones.
- **Base terrain from the seed**: a port of Valheim's world generator that matches the game bit for bit.
- **3D terrain editor** with brushes (Raise, Lower, Flatten, Smooth, Naturalize, Restore), ground
  paint (Dirt, Cultivate, Paved, Clear), the Path tool, undo/redo, and the game's ±8 m limit.
- **Saving back to the world**: a new save number with a full backup and a read-back check.
- **CrossplayJoinCodeFix** (separate plugin, own repository): keeps a crossplay server from appearing
  offline when PlayFab answers the join-code check incompletely.
