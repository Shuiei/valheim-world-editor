// Hover help for every control: one place for the wording, applied when the editor starts. The same
// text is used in the documentation (docs/), so the two stay in step.
// Keys are element ids, or CSS selectors when they start with '#', '.' or '['.

export const TIPS = {
  // Top bar
  undo: 'Undo the last change (Ctrl+Z).',
  redo: 'Redo the change you just undid (Ctrl+Y or Ctrl+Shift+Z).',
  pending: 'Changes that are not saved (offline) or not applied to the game (live) yet.',
  discardBtn: 'Undo every change that is not saved or applied yet. Saved and applied changes stay; the page is not reloaded.',
  saveBtn: 'Offline: write the changes into the world files (a backup of the world folder is made first). Live: send them to the running game.',
  liveReload: 'Load the world again from the running game (picks up what players changed since).',
  autoApply: 'Live mode: send every stroke, placement and undo to the game right away, without pressing Apply live.',
  historyToggle: 'Show every change made in this session; roll back to any point or remove one change (L).',
  viewToggle: 'Show or hide kinds of things in the world, and the look options (V).',
  helpToggle: 'Mouse controls and keyboard shortcuts (?).',

  // Brush settings
  radius: 'Brush radius in metres. [ and ] change it.',
  strength: 'How fast the brush works while you hold the mouse button.',
  '[data-e="thermal"]': 'Thermal: where the ground is steeper than the rest angle, it slides down to its neighbours (screes, softened cliffs).',
  '[data-e="water"]': 'Water: rain drops run downhill, dig where they speed up and leave what they carry where they slow down (gullies, fans, smooth valleys).',
  erTalus: 'Thermal: the steepest slope that stays put (degrees). Lower values flatten more.',
  '[data-act="erode"]': 'Weather the ground inside the selection: slopes settle and rain cuts gullies (rest angle from the Erode tool).',
  bShape: 'Shape of the brush: circle, square, ring (only a band around the middle, for rims and moats), ragged (a noisy edge for natural-looking strokes), or a picture (Stamps).',
  bFalloff: 'How the effect fades from the middle to the edge: smooth, linear, dome (round top), flat top (full strength almost to the edge), or peak (strong in the middle only).',
  bTurn: 'Turn of a square brush or a picture (degrees). , and . change it (Shift: 15°).',
  stLoad: 'Load a picture (PNG, JPEG...) as a brush shape: white parts work fully, black parts not at all. It is kept in this browser.',
  stRemove: 'Forget the loaded picture chosen as Shape.',
  stOnce: 'With a stamp as Shape, a click of Raise or Lower puts the whole stamp into the ground at once, to the Height below, instead of painting.',
  stHeight: 'How high (Raise) or deep (Lower) the white parts of the stamp go (m).',
  targetFromClick: 'On: Flatten levels to the height of the ground where you start the stroke. Off: it levels to the Height below.',
  target: 'The height (m) Flatten levels to when the option above is off. Alt + click the ground to pick its height.',

  // Naturalize
  nAmp: 'How tall the natural bumps are (m).',
  nSize: 'How wide the natural bumps are (m): small values give rough ground, large values gentle swells.',
  nSeed: 'Pick a new random bump pattern for the next Naturalize stroke or natural path.',

  // Path
  pAction: 'What happens along the line when you press Apply.',
  pWidth: 'Width of the path (m).',
  pSoft: 'Width of the soft edge on each side (m), blending the path into the ground around it.',
  pHeight: 'Flatten to height: the height of the path (m). Alt + click the ground to pick it.',
  pAmount: 'Raise / Lower by: how much to raise or lower along the path (m).',
  pStart: 'Ramp: height at the first point (m). Alt + click the ground to pick it.',
  pEnd: 'Ramp: height at the last point (m). Alt + Shift + click the ground to pick it.',
  pCurve: 'On: a smooth curve through your points. Off: straight lines between them.',
  pNatural: 'Ragged edges, varying width and small bumps, so the path looks worn rather than tool-made.',
  pApply: 'Do the chosen action along the line (Enter). The line stays, so you can apply another action to it.',
  pClear: 'Remove the line (Esc).',

  // Mask
  mOn: 'Limit brushes, paths, area actions and planting to ground that matches all the settings below.',
  '#mBiomes': 'Only these biomes (none ticked = every biome).',
  mHmin: 'Only ground at or above this height (m). Empty = no limit. Alt + Shift + click fills the range around a spot.',
  mHmax: 'Only ground at or below this height (m). Empty = no limit.',
  mSmin: 'Only ground at least this steep (degrees). Empty = no limit.',
  mSmax: 'Only ground at most this steep (degrees). Empty = no limit; 0 lets only perfectly flat ground through.',
  mPaint: 'Only ground with this paint.',

  // Select
  selDelete: 'Remove the selected objects from the world (Del). Ctrl+Z brings them back.',
  selClear: 'Clear the selection (Esc).',
  selTo: 'The kind to put in place of each selected object.',
  selReplace: 'Replace every selected object by the chosen kind, at the same place and facing.',
  selInspect: 'Show everything the selected object holds in the save (sign text, portal tag, chest contents, timers...) and change it (I).',
  inApply: 'Replace the object by a copy with the changed data (one undo step). Save or Apply live writes it.',
  inRevert: 'Forget the changes made here.',
  inAddSec: 'What kind of value to add.',
  inAddKey: 'The name of the value, as the game calls it (text for a sign, tag for a portal...), or its number.',
  inAddVal: 'The value to add.',
  inAddBtn: 'Add this value to the object (applied with Apply changes).',

  // Area
  '[data-shape="box"]': 'Drag a rectangle on the ground.',
  '[data-shape="poly"]': 'Click corners; double-click or Enter closes the shape. Backspace removes the last corner.',
  aSoft: 'Ground actions fade out over this many metres inside the edge of the selection.',
  '[data-act="flatten"]': 'Level the ground inside to the Height below.',
  '[data-act="raise"]': 'Lift the ground inside by the Amount below.',
  '[data-act="lower"]': 'Dig the ground inside down by the Amount below.',
  '[data-act="smooth"]': 'Even out bumps inside the selection.',
  '[data-act="natural"]': 'Natural-looking bumps inside the selection (Bumps and Size of the Naturalize tool).',
  '[data-act="restore"]': 'Put the ground inside back to how the world generated it, and remove paint.',
  aHeight: 'Height (m) used by Flatten.',
  aAvg: 'Set Height to the average ground height inside the selection.',
  aAmount: 'Metres used by Raise and Lower.',
  aPaint: 'Paint to put on the ground inside.',
  aPaintBtn: 'Paint the ground inside the selection.',
  '#aKinds': 'Which kinds of objects the buttons below act on.',
  aRemove: 'Remove the objects of the ticked kinds inside the selection.',
  aSelectObj: 'Select the objects of the ticked kinds inside the selection (then use the Select tool on them).',
  aFrom: 'The kind of object to replace.',
  aTo: 'The kind to put in its place.',
  aReplace: 'Replace every object of the first kind inside the selection by the second kind.',
  aCopy: 'Copy the ground shape, paint and shown objects inside the selection (Ctrl+C).',
  aPasteBtn: 'Paste the copy (Ctrl+V): click to place it, here or in another area.',
  aSaveBp: 'Save the clipboard as a blueprint: a file with a name, kept in the editor\'s data folder, that can be pasted into any world.',
  aLibrary: 'Open the list of saved blueprints, with a picture of each: paste one, or delete it.',
  bpSearch: 'Filter the blueprints by name.',
  bpImport: 'Import a blueprint file of the PlanBuild mod (.blueprint) or a .vbuild file; it is kept as a blueprint here.',
  aBackup: 'The backup to restore from: the editor\'s backups (made before every save) and the game\'s own, newest first, or another copy of this world.',
  aBkGround: 'Put the ground (height and paint) inside the selection back as it was in the backup.',
  aBkObjects: 'Put the objects of the kinds ticked under Objects inside back as they were in the backup, with all their data. Unchanged objects stay as they are.',
  aBkRestore: 'Restore the selection from the chosen backup (one undo step). Save or Apply live writes it.',
  hmExport: 'Write the ground of the whole area as a 16-bit grayscale PNG (black lowest, white highest, north at the top, 1 pixel per metre). The heights are kept in the picture.',
  hmImport: 'Read a grayscale picture and fit it to the selection (or the whole area, without a selection).',
  hmMin: 'Height (m) for black in the picture.',
  hmMax: 'Height (m) for white in the picture.',
  hmApply: 'Set the ground to the picture\'s heights (one undo step). The soft edge, the Mask and the ±8 m limit apply.',
  aKeepB: 'Player-built pieces in the zones are kept when they are reset.',
  aResetGround: 'Also undo the ground edits in the zones (height and paint).',
  aReset: 'On save, the zones under the selection lose their trees, rocks, ruins and dungeon entrances; the game generates them again when someone goes there.',
  aUnreset: 'Cancel the reset of the zones under the selection.',

  // Paste
  psGround: 'Paste the copied ground shape and paint.',
  psObjects: 'Paste the copied objects.',
  psOffset: 'Move the pasted ground and objects up or down (m).',
  psRot: 'Turn the paste a quarter turn (R). , and . turn it by 1° (Shift: 15°).',
  psFlip: 'Mirror the paste (F).',
  psDone: 'Stop pasting (Esc).',
  psCount: 'How many copies one click places, side by side (like WorldEdit\'s //stack). 1 = a single paste.',
  psDir: 'Which way the copies follow each other: along the copy\'s width or depth (turning with it), or stacked upwards.',
  psGap: 'Space left between two copies (m); negative values make them overlap.',

  // Plant
  '[data-m="brush"]': 'Paint objects under the brush: a click places what the preview shows, a drag keeps adding.',
  '[data-m="line"]': 'Place objects at a fixed spacing along a line you draw.',
  '[data-m="grid"]': 'Drag a box: one object in the middle of each cell.',
  '[data-m="zone"]': 'Draw a zone freely, then fill it by scatter or by a grid.',
  plSearch: 'Filter the list of kinds by name.',
  plDensity: 'Brush and zone scatter: objects per 100 m².',
  plSpacing: 'Brush and zone scatter: minimum distance between objects (m), also from objects already there.',
  plSmin: 'Smallest size, in % of the normal size. Each object gets a random size between the two values.',
  plSmax: 'Largest size, in % of the normal size.',
  plTilt: 'Random lean of each object, up to this many degrees.',
  plRot: 'Turn of the preview layout and of each object\'s facing (degrees). , and . or Alt + wheel change it.',
  plRandomYaw: 'On: each object faces a random direction. Off: they all face the Rotation.',
  plSingle: 'Place one object exactly under the cursor per click (only an object right on the spot blocks it).',
  plGrow: 'Keep saplings and crops at least their in-game grow radius away from everything, so they can grow.',
  plEvery: 'Line: distance between two objects along the line (m).',
  plWiggle: 'Line: random sideways offset from the line, up to this many metres.',
  plAlong: 'Line: each object faces the direction of the line (plus the Rotation), instead of a random facing.',
  plCurve: 'Line: a smooth curve through your points instead of straight segments.',
  '[data-f="scatter"]': 'Zone: random spots inside the zone, using Density and Spacing.',
  '[data-f="grid"]': 'Zone: one object in the middle of each cell inside the zone.',
  plCell: 'Grid: size of each cell (m).',
  plPlace: 'Place the objects the preview shows (Enter).',
  plClearShape: 'Remove the drawn line, box or zone (Esc).',
  plNewLayout: 'New random choices for the preview: positions, kinds, sizes and facings (R).',

  // Measure and view
  msClear: 'Remove the measurement (Esc).',
  gameLook: 'Draw the world with the game\'s own textures, models, water and sky. Off: flat colours, faster.',
  seeThrough: 'Make buildings half see-through, to see the ground and objects inside them.',
  ovSlope: 'Colour the ground by steepness (needs Game look).',
  ovContour: 'Draw height lines every chosen number of metres.',
  ovStep: 'Distance between height lines (m).',
  '[data-show="trees"]': 'Trees, logs and stumps.',
  '[data-show="rocks"]': 'Rocks and boulders.',
  '[data-show="bushes"]': 'Bushes, shrubs and ferns.',
  '[data-show="pickables"]': 'Mushrooms, berries, flowers, stones and other things to pick up.',
  '[data-show="ore"]': 'Copper, tin, silver and other deposits (spoiler).',
  '[data-show="ruins"]': 'Ruins, dungeon entrances and other generated structures (spoiler).',
  '[data-show="markers"]': 'Markers where the game placed locations (villages, dungeons, the trader...) (spoiler).',
  '[data-show="other"]': 'Everything else the save holds.',
  '[data-show="buildings"]': 'Pieces built by players.',
  '[data-show="water"]': 'The sea surface.',
  '[data-show="borders"]': 'Lines between the 64 m zones.',
  showDefaults: 'Back to the default switches (spoilers off).',
  showAll: 'Show everything.',
  terrainOnly: 'Show only the ground.',
};

export function applyTips(root = document) {
  for (const [key, text] of Object.entries(TIPS)) {
    const els = /^[#.[]/.test(key) ? root.querySelectorAll(key) : [root.getElementById?.(key) ?? document.getElementById(key)];
    for (const el of els) {
      if (!el) continue;
      el.title = text;
      // Hovering the label text explains the control too.
      const label = el.closest('label');
      if (label) label.title = text;
    }
  }
}
