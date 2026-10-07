// Tool panel layout, applied once every module has added its controls: the how-to text hides behind
// the ? next to the tool name, and each tool's less used settings fold under one "more" line at the
// bottom of its panel (open or closed as you left it). The Mask moves into the fold of the tool in hand.

export function applyLayout(ed) {
  const { $ } = ed;
  const style = document.createElement('style');
  style.textContent = `
    body:not(.toolHelp) #toolPanel .help { display: none !important; }
    #toolHelpBtn { position: absolute; top: 10px; right: 10px; width: 22px; height: 22px; padding: 0; border-radius: 50%; font-size: 11.5px; color: var(--muted); }
    body.toolHelp #toolHelpBtn { background: var(--accent); color: var(--accent-ink); border-color: var(--accent); }
    #toolName { padding-right: 28px; }
    #toolDesc.help { margin-bottom: 8px; }
    details.more { margin-top: 10px; border-top: 1px dashed var(--line2); }
    details.more > summary { list-style: none; cursor: pointer; display: flex; align-items: center; gap: 6px; padding: 7px 0 2px; color: var(--muted); font-size: 12px; user-select: none; }
    details.more > summary::-webkit-details-marker { display: none; }
    details.more > summary::after { content: '▾'; margin-left: auto; font-size: 10px; transition: transform .12s; }
    details.more[open] > summary::after { transform: rotate(180deg); }
    details.more > summary:hover { color: var(--text); }
    details.more > summary .on { color: var(--accent); }
    details.more > .moreBody { padding-bottom: 2px; }
    details.more #maskBox { margin-top: 6px; }
    .toolFoot { position: sticky; bottom: -12px; margin: 10px -14px -12px; padding: 10px 14px 12px; background: var(--panel); border-top: 1px solid var(--line); border-radius: 0 0 var(--r) var(--r); z-index: 1; }
    button.primary kbd { color: var(--accent-ink); border-color: rgba(0,0,0,.3); }
    .toolFoot[hidden] { display: none; }`;
  document.head.appendChild(style);

  // ---- Help: the tool's description and every fixed how-to hint show only with ? on (remembered).
  const btn = document.createElement('button');
  btn.id = 'toolHelpBtn'; btn.textContent = '?';
  btn.title = 'Show how to use this tool';
  $('toolName').after(btn);
  $('toolDesc').classList.add('help');
  for (const h of $('toolPanel').querySelectorAll('.hint:not([id])')) h.classList.add('help');
  for (const id of ['plBrushHint', 'plLineHint']) $(id)?.classList.add('help');
  const setHelp = on => {
    document.body.classList.toggle('toolHelp', on);
    btn.title = on ? 'Hide the how-to text' : 'Show how to use this tool';
    try { localStorage.setItem('toolHelp', on ? '1' : '0'); } catch { }
  };
  let helpOn = false;
  try { helpOn = localStorage.getItem('toolHelp') === '1'; } catch { }
  setHelp(helpOn);
  btn.onclick = () => setHelp(!document.body.classList.contains('toolHelp'));

  // ---- "More" folds.
  let openMore;
  try { openMore = new Set(JSON.parse(localStorage.getItem('toolMoreOpen') ?? '[]')); } catch { openMore = new Set(); }
  const folds = new Map();
  function more(key, container, label, items, before = null) {
    const d = document.createElement('details');
    d.className = 'more'; d.dataset.more = key; d.open = openMore.has(key);
    d.innerHTML = `<summary><span class="lab">${label}</span></summary><div class="moreBody"></div>`;
    const body = d.querySelector('.moreBody');
    for (const it of items) if (it) body.appendChild(it);
    before ? before.before(d) : container.appendChild(d);
    d.addEventListener('toggle', () => {
      d.open ? openMore.add(key) : openMore.delete(key);
      try { localStorage.setItem('toolMoreOpen', JSON.stringify([...openMore])); } catch { }
    });
    folds.set(key, d);
    return d;
  }
  const lab = id => $(id)?.closest('label');
  const brushBlock = document.querySelector('[data-for-tools="brush"]');
  const brushMore = more('brush', brushBlock, 'Shape, falloff, stamp, mask', [$('brushShape')]);
  // Place uses the brush's Size and Strength, but its own fold.
  ed.onToolChange.push(t => { brushMore.hidden = t === 'plant'; });
  brushMore.hidden = ed.tool === 'plant';
  more('path', $('pathPanel'), 'Soft edge, curve, natural look, mask', [lab('pSoft'), lab('pCurve'), lab('pNatural')], $('pApply').closest('.row'));
  // The Apply row closes the panel and stays in sight while it scrolls.
  $('pathPanel').appendChild($('pApply').closest('.row'));
  $('pApply').closest('.row').classList.add('toolFoot');
  more('area', $('areaPanel'), 'Mask', [], $('aApplyRow'));
  more('shape', $('shapePanel'), 'Mask', []);
  const plMore = more('plant', $('plantPanel'), 'Clumping, size, tilt, facing, presets, mask',
    [lab('plClump'), $('plPatchRow'), lab('plSmin'), lab('plTilt'), lab('plRot'), lab('plRandomYaw'), lab('plSingle'), $('plPresetSave').closest('.row'), $('plNewLayout').closest('.row')], $('plPlaceRow'));
  $('plantPanel').appendChild($('plPlaceRow'));
  plMore.before($('plUndoRow'), $('plPreview'), $('plBrushHint'));

  // ---- Select: what you do most first (delete, grow the selection, exact place), the rest folded.
  const sp = $('selectPanel'), firstRow = $('selDelete').closest('.row');
  firstRow.after($('selBuilding').closest('.row'));
  $('selBuilding').closest('.row').after($('selNum'));
  $('selNum').hidden = true;   // nothing is selected yet
  const helpHints = [...sp.querySelectorAll(':scope > .hint.help')];
  more('selActions', sp, 'More actions: replace, saved selections, inspect, player built',
    [$('selTo').closest('label').parentElement, $('selSaved').closest('label').parentElement, $('selInspect').closest('.row'), $('selClaim').closest('.row')]);
  more('selMove', sp, 'Moving: on the ground, snap to pieces', [lab('selGround'), lab('selSnapTo')]);
  for (const h of helpHints) sp.appendChild(h);

  // ---- The Mask goes into the fold of the tool in hand, and the fold says when it is on.
  const maskHome = t => ed.isBrushTool(t) ? 'brush' : { path: 'path', area: 'area', shape: 'shape', plant: 'plant' }[t];
  function placeMask(t) {
    const k = maskHome(t), d = k && folds.get(k);
    if (d) d.querySelector('.moreBody').appendChild($('maskBox'));
    syncSummaries();
  }
  function syncSummaries() {
    const on = $('mOn').checked;
    for (const d of folds.values()) {
      let tag = d.querySelector('summary .on');
      const has = d.contains($('maskBox')) && on;
      if (has && !tag) { tag = document.createElement('span'); tag.className = 'on'; tag.textContent = '· mask on'; d.querySelector('summary .lab').after(tag); }
      if (!has && tag) tag.remove();
    }
  }
  $('mOn').addEventListener('change', syncSummaries);
  ed.onToolChange.push(placeMask);
  placeMask(ed.tool);
  ed.layout = { more: k => folds.get(k), setHelp };
}
